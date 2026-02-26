"""
Claude API wrapper for Keeper.
Handles transcript analysis and commitment extraction.
"""
import os
import json
import anthropic

_client = anthropic.Anthropic(api_key=os.environ["ANTHROPIC_API_KEY"])

MODEL = "claude-opus-4-6"

EXTRACTION_SYSTEM_PROMPT = """
You are Keeper, an AI assistant that extracts commitments from meeting transcripts.

A commitment is a clear statement where a named person agrees to do something,
optionally by a specific date. Your job is to identify every commitment in the
transcript and return them in structured form.

Rules:
- Only extract explicit commitments — "I will", "I'll", "I can do that",
  "let me take that", "I'll handle it", etc.
- Do not invent commitments that aren't clearly stated.
- If a due date is mentioned, include it in ISO 8601 format (YYYY-MM-DD).
- If no due date is mentioned, omit the field.
- Group related commitments under a topic if they share a clear subject.
- Return ONLY valid JSON — no preamble, no explanation, no markdown.

Return format:
{
  "meeting_name": "inferred name or null",
  "topics": [
    {
      "title": "topic title",
      "type": "Recurring or Discussion",
      "action_items": [
        {
          "title": "what was committed to",
          "responsible_name": "person's name as stated",
          "due_date": "YYYY-MM-DD or null",
          "priority": 2
        }
      ]
    }
  ]
}

Priority scale: 1=Low, 2=Medium, 3=High, 4=Critical.
Infer priority from urgency language in the transcript.
"""


def extract_commitments(transcript: str) -> dict:
    """
    Send a transcript to Claude and extract structured commitments.
    Returns the parsed JSON response.
    """
    response = _client.messages.create(
        model=MODEL,
        max_tokens=4096,
        system=EXTRACTION_SYSTEM_PROMPT,
        messages=[
            {
                "role": "user",
                "content": f"Extract all commitments from this transcript:\n\n{transcript}"
            }
        ]
    )

    raw = response.content[0].text.strip()

    # Strip markdown fences if present
    if raw.startswith("```"):
        lines = raw.split("\n")
        raw = "\n".join(lines[1:-1])

    return json.loads(raw)


def summarize_extraction(extraction: dict) -> str:
    """
    Produce a human-readable Slack summary of what was extracted.
    """
    topics = extraction.get("topics", [])
    if not topics:
        return "I didn't find any commitments in that transcript."

    total_items = sum(len(t.get("action_items", [])) for t in topics)
    meeting_name = extraction.get("meeting_name") or "this meeting"

    lines = [f"Here's what I captured from *{meeting_name}*:"]
    lines.append("")

    for topic in topics:
        items = topic.get("action_items", [])
        if not items:
            continue
        lines.append(f"*{topic['title']}*")
        for item in items:
            due = f" _(due {item['due_date']})_" if item.get("due_date") else ""
            lines.append(f"  • {item['responsible_name']}: {item['title']}{due}")
        lines.append("")

    lines.append(
        f"_{total_items} commitment(s) recorded in Docket. "
        "React with ✅ to confirm or ✏️ to edit._"
    )

    return "\n".join(lines)
