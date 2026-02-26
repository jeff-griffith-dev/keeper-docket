"""
Keeper — Slack Bolt app.
Listens for mentions and file shares, extracts commitments,
writes them to Docket.
"""
import os
import logging
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

# ── Helpers ───────────────────────────────────────────────────────

def _post(client, channel: str, thread_ts: str | None, text: str):
    """Post a message, optionally in a thread."""
    client.chat_postMessage(
        channel=channel,
        thread_ts=thread_ts,
        text=text
    )


def _scheduled_for_now() -> str:
    return datetime.now(timezone.utc).isoformat()


def _process_transcript(
    transcript: str,
    series_name: str,
    channel: str,
    thread_ts: str | None,
    client
) -> None:
    """
    Core pipeline: transcript → Claude → Docket → Slack confirmation.
    """
    _post(client, channel, thread_ts,
          "Reading the transcript… :hourglass_flowing_sand:")

    # Step 1: Extract commitments
    try:
        extraction = claude.extract_commitments(transcript)
    except Exception as e:
        logger.error("Extraction failed: %s", e)
        _post(client, channel, thread_ts,
              f":x: I couldn't extract commitments from that transcript. "
              f"Error: {e}")
        return

    if not extraction.get("topics"):
        _post(client, channel, thread_ts,
              "I read the transcript but didn't find any commitments to record.")
        return

    # Step 2: Create series and minutes in Docket
    try:
        meeting_name = extraction.get("meeting_name") or series_name
        series = docket.create_series(
            name=meeting_name,
            project=None
        )
        series_id = series["id"]

        minutes = docket.create_minutes(
            series_id=series_id,
            scheduled_for=_scheduled_for_now()
        )
        minutes_id = minutes["id"]

    except Exception as e:
        logger.error("Docket series/minutes creation failed: %s", e)
        _post(client, channel, thread_ts,
              f":x: I extracted the commitments but couldn't create the "
              f"meeting record in Docket. Error: {e}")
        return

    # Step 3: Write topics and action items
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
                    docket.create_action_item(
                        topic_id=topic_id,
                        title=item_data["title"],
                        responsible_id=stub_user_id,
                        due_date=item_data.get("due_date"),
                        priority=item_data.get("priority", 2)
                    )
                    written += 1
                except Exception as e:
                    errors.append(
                        f"Item '{item_data['title']}': {e}")

        except Exception as e:
            errors.append(f"Topic '{topic_data['title']}': {e}")

    # Step 4: Report back
    summary = claude.summarize_extraction(extraction)
    _post(client, channel, thread_ts, summary)

    if errors:
        error_text = "\n".join(f"  • {e}" for e in errors)
        _post(client, channel, thread_ts,
              f":warning: Some items couldn't be written to Docket:\n{error_text}")

    logger.info("Wrote %d action items to Docket minutes %s", written, minutes_id)


# ── Event handlers ────────────────────────────────────────────────

@app.event("app_mention")
def handle_mention(event, client, say):
    """
    Respond to @Keeper mentions.
    If the message contains a transcript (long text), process it.
    Otherwise respond with usage instructions.
    """
    channel = event["channel"]
    thread_ts = event.get("thread_ts") or event.get("ts")
    text = event.get("text", "")

    # Strip the bot mention from the text
    import re
    text = re.sub(r"<@[A-Z0-9]+>", "", text).strip()

    if len(text) > 200:
        # Long enough to be a transcript
        _process_transcript(
            transcript=text,
            series_name="Meeting from Slack",
            channel=channel,
            thread_ts=thread_ts,
            client=client
        )
    else:
        say(
            text=(
                "Hi! I'm Keeper. I extract commitments from meeting transcripts "
                "and record them in Docket.\n\n"
                "To get started, paste your transcript in a message and mention me, "
                "or share a text file with me directly.\n\n"
                "Example: `@Keeper <paste transcript here>`"
            ),
            thread_ts=thread_ts
        )


@app.event("message")
def handle_file_share(event, client, logger):
    """
    Handle direct messages containing file uploads.
    """
    files = event.get("files", [])
    if not files:
        return

    channel = event["channel"]
    thread_ts = event.get("ts")

    for file in files:
        if file.get("mimetype") not in ("text/plain", "text/markdown"):
            client.chat_postMessage(
                channel=channel,
                thread_ts=thread_ts,
                text=":x: Please share a plain text (.txt) or markdown (.md) file."
            )
            continue

        # Download the file content
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
            client.chat_postMessage(
                channel=channel,
                thread_ts=thread_ts,
                text=f":x: Couldn't download the file. Error: {e}"
            )
            continue

        _process_transcript(
            transcript=transcript,
            series_name=file.get("name", "Meeting transcript"),
            channel=channel,
            thread_ts=thread_ts,
            client=client
        )