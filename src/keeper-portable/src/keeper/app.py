"""
Keeper — Slack Bolt app.
Listens for mentions and file shares, extracts commitments,
writes them to Docket.
"""
import os
import logging
import re
from datetime import datetime, timezone

from slack_bolt import App
from slack_bolt.adapter.socket_mode import SocketModeHandler

from keeper import claude, docket

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

app = App(
    token=os.environ["SLACK_BOT_TOKEN"],
    signing_secret=os.environ["SLACK_SIGNING_SECRET"]
)

# ── Pending extractions ───────────────────────────────────────────
# Keyed by (channel, thread_ts) → {"extraction": dict, "series_options": list}
_pending: dict[tuple, dict] = {}


# ── Helpers ───────────────────────────────────────────────────────

def _post(client, channel: str, thread_ts: str | None, text: str):
    client.chat_postMessage(
        channel=channel,
        thread_ts=thread_ts,
        text=text
    )


def _scheduled_for_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _match_deferred_item(new_title: str, deferred_items: list[dict]) -> str | None:
    if not deferred_items:
        return None
    new_words = set(new_title.lower().split())
    stop_words = {"the", "a", "an", "and", "or", "to", "with", "for",
                  "of", "in", "on", "by", "all", "it"}
    new_words -= stop_words
    best_id = None
    best_score = 0
    for item in deferred_items:
        existing_words = set(item["title"].lower().split()) - stop_words
        overlap = len(new_words & existing_words)
        if overlap >= 2 and overlap > best_score:
            best_score = overlap
            best_id = item["id"]
    return best_id


def _ask_series_selection(
    client, channel: str, thread_ts: str,
    extraction: dict, active_series: list[dict]
) -> None:
    """Post a numbered series selection prompt and park the extraction."""
    _pending[(channel, thread_ts)] = {
        "extraction": extraction,
        "series_options": active_series
    }
    lines = ["I found multiple active series. Which one does this transcript belong to?\n"]
    for i, s in enumerate(active_series, start=1):
        lines.append(f"  {i}. {s['name']}")
    lines.append(f"  {len(active_series) + 1}. Create a new series")
    lines.append("\nReply with the number.")
    _post(client, channel, thread_ts, "\n".join(lines))


def _write_to_docket(
    client, channel: str, thread_ts: str,
    extraction: dict, series_id: str
) -> None:
    """Phase 2: Create minutes, carry-forward, write items."""
    try:
        minutes = docket.create_minutes(
            series_id=series_id,
            scheduled_for=_scheduled_for_now()
        )
        minutes_id = minutes["id"]

        deferred_items = docket.get_deferred_items_from_previous_minutes(
            series_id=series_id,
            current_minutes_id=minutes_id
        )
        logger.info("Found %d deferred item(s) for carry-forward", len(deferred_items))

    except Exception as e:
        logger.error("Docket minutes creation failed: %s", e)
        _post(client, channel, thread_ts,
              f":x: Couldn't create the meeting record in Docket. Error: {e}")
        return

    stub_user_id = os.environ["DOCKET_STUB_USER_ID"]
    written = 0
    errors = []

    for topic_data in extraction.get("topics", []):
        try:
            topic = docket.create_topic(
                minutes_id=minutes_id,
                title=topic_data["title"],
                topic_type=topic_data.get("type", "Discussion")
            )
            topic_id = topic["id"]
            for item_data in topic_data.get("action_items", []):
                try:
                    source_id = _match_deferred_item(
                        item_data["title"], deferred_items
                    )
                    if source_id:
                        logger.info("Carry-forward: '%s' → %s",
                                    item_data["title"], source_id)
                    docket.create_action_item(
                        topic_id=topic_id,
                        title=item_data["title"],
                        responsible_id=stub_user_id,
                        due_date=item_data.get("due_date"),
                        priority=item_data.get("priority", 2),
                        source_action_item_id=source_id
                    )
                    written += 1
                except Exception as e:
                    errors.append(f"Item '{item_data['title']}': {e}")
        except Exception as e:
            errors.append(f"Topic '{topic_data['title']}': {e}")

    summary = claude.summarize_extraction(extraction)
    _post(client, channel, thread_ts, summary)

    if errors:
        error_text = "\n".join(f"  • {e}" for e in errors)
        _post(client, channel, thread_ts,
              f":warning: Some items couldn't be written:\n{error_text}")

    logger.info("Wrote %d action items to Docket minutes %s", written, minutes_id)


def _process_transcript(
    transcript: str,
    channel: str,
    thread_ts: str,
    client
) -> None:
    """Phase 1: Extract commitments, resolve series, then write or ask."""
    _post(client, channel, thread_ts,
          "Reading the transcript… :hourglass_flowing_sand:")

    # Step 1: Extract
    try:
        extraction = claude.extract_commitments(transcript)
    except Exception as e:
        logger.error("Extraction failed: %s", e)
        _post(client, channel, thread_ts,
              f":x: Couldn't extract commitments. Error: {e}")
        return

    if not extraction.get("topics"):
        _post(client, channel, thread_ts,
              "I read the transcript but didn't find any commitments to record.")
        return

    # Step 2: Resolve series
    try:
        active_series = [s for s in docket.list_series() if s["status"] == "Active"]
    except Exception as e:
        logger.error("Series lookup failed: %s", e)
        _post(client, channel, thread_ts,
              f":x: Couldn't reach Docket to look up series. Error: {e}")
        return

    if len(active_series) == 0:
        # No series exist — create one named from the transcript
        try:
            meeting_name = extraction.get("meeting_name", "New Meeting Series")
            series = docket.create_series(name=meeting_name)
            logger.info("Created first series: %s", series["id"])
            _write_to_docket(client, channel, thread_ts, extraction, series["id"])
        except Exception as e:
            _post(client, channel, thread_ts,
                  f":x: Couldn't create a new series. Error: {e}")
        return

    if len(active_series) == 1:
        # Exactly one — use it without asking
        logger.info("Auto-selected only active series: %s",
                    active_series[0]["name"])
        _write_to_docket(client, channel, thread_ts,
                         extraction, active_series[0]["id"])
        return

    # Multiple series — ask the user
    _ask_series_selection(client, channel, thread_ts, extraction, active_series)


# ── Event handlers ────────────────────────────────────────────────

@app.event("app_mention")
def handle_mention(event, client, say):
    channel = event["channel"]
    thread_ts = event.get("thread_ts") or event.get("ts")
    text = event.get("text", "")
    text = re.sub(r"<@[A-Z0-9]+>", "", text).strip()

    if len(text) > 200:
        _process_transcript(
            transcript=text,
            channel=channel,
            thread_ts=thread_ts,
            client=client
        )
    else:
        say(
            text=(
                "Hi! I'm Keeper. I extract commitments from meeting transcripts "
                "and record them in Docket.\n\n"
                "Paste your transcript and mention me, or share a .txt file.\n\n"
                "Example: `@Keeper <paste transcript here>`"
            ),
            thread_ts=thread_ts
        )


@app.event("message")
def handle_message(event, client, logger):
    """
    Handle two cases:
    1. File shares — process as transcript
    2. Threaded replies — check if it's a series selection response
    """
    # Ignore bot messages to avoid loops
    if event.get("bot_id"):
        return

    channel = event["channel"]
    thread_ts = event.get("thread_ts")
    ts = event.get("ts")

    # ── Case 1: Series selection reply ───────────────────────────
    if thread_ts and (channel, thread_ts) in _pending:
        text = event.get("text", "").strip()
        pending = _pending[(channel, thread_ts)]
        options = pending["series_options"]
        extraction = pending["extraction"]

        # Expect a number
        if re.fullmatch(r"\d+", text):
            choice = int(text)
            if 1 <= choice <= len(options):
                selected = options[choice - 1]
                del _pending[(channel, thread_ts)]
                _post(client, channel, thread_ts,
                      f"Got it — writing to *{selected['name']}*…")
                _write_to_docket(client, channel, thread_ts,
                                 extraction, selected["id"])
            elif choice == len(options) + 1:
                # Create new series
                del _pending[(channel, thread_ts)]
                meeting_name = extraction.get("meeting_name", "New Meeting Series")
                try:
                    series = docket.create_series(name=meeting_name)
                    _post(client, channel, thread_ts,
                          f"Created new series *{meeting_name}*. Writing items…")
                    _write_to_docket(client, channel, thread_ts,
                                     extraction, series["id"])
                except Exception as e:
                    _post(client, channel, thread_ts,
                          f":x: Couldn't create series. Error: {e}")
            else:
                _post(client, channel, thread_ts,
                      f"Please reply with a number between 1 and {len(options) + 1}.")
        else:
            _post(client, channel, thread_ts,
                  f"Please reply with just a number (1–{len(options) + 1}).")
        return

    # ── Case 2: File share ────────────────────────────────────────
    files = event.get("files", [])
    if not files:
        return

    effective_thread_ts = thread_ts or ts

    for file in files:
        if file.get("mimetype") not in ("text/plain", "text/markdown"):
            _post(client, channel, effective_thread_ts,
                  ":x: Please share a plain text (.txt) or markdown (.md) file.")
            continue

        try:
            import httpx
            response = httpx.get(
                file["url_private_download"],
                headers={"Authorization": f"Bearer {os.environ['SLACK_BOT_TOKEN']}"}
            )
            response.raise_for_status()
            transcript = response.text
        except Exception as e:
            logger.error("File download failed: %s", e)
            _post(client, channel, effective_thread_ts,
                  f":x: Couldn't download the file. Error: {e}")
            continue

        _process_transcript(
            transcript=transcript,
            channel=channel,
            thread_ts=effective_thread_ts,
            client=client
        )