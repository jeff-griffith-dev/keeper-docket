"""
Keeper entry point.
Runs in HTTP mode so ngrok can forward Slack events to it.
"""
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), "src"))

from dotenv import load_dotenv
load_dotenv()

from slack_bolt.adapter.flask import SlackRequestHandler
from flask import Flask, request
from keeper.app import app as bolt_app 

flask_app = Flask(__name__)
handler = SlackRequestHandler(bolt_app)

@flask_app.route("/slack/events", methods=["POST"])
def slack_events():
    return handler.handle(request)

@flask_app.route("/health", methods=["GET"])
def health():
    return {"status": "ok", "service": "keeper"}

if __name__ == "__main__":
    port = int(os.environ.get("PORT", 3000))
    flask_app.run(host="0.0.0.0", port=port, debug=True)
