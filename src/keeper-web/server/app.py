"""
app.py — keeper-web server

Serves the keeper-web HTML client and proxies all
/api/* requests to the Docket API.

Routes:
  GET  /                    → redirect to /app/attention
  GET  /app/<page>          → serve HTML client pages
  ANY  /api/<path>          → proxy to Docket API
  GET  /health              → server health check
"""
import os
import sys
from pathlib import Path
from flask import Flask, send_from_directory, redirect, jsonify, request
from dotenv import load_dotenv

load_dotenv()

# ── Paths ──────────────────────────────────────────────────────────
# Server lives at src/keeper-web/server/
# HTML client lives at src/keeper-web/src/
# Prototypes live at src/keeper-web/prototypes/
SERVER_DIR     = Path(__file__).parent
WEB_ROOT       = SERVER_DIR.parent
CLIENT_DIR     = WEB_ROOT / "src"
PROTOTYPE_DIR  = WEB_ROOT / "prototypes"

app = Flask(__name__)

# ── Health ─────────────────────────────────────────────────────────
@app.route("/health")
def health():
    return jsonify({
        "status": "ok",
        "service": "keeper-web",
        "docket": os.environ.get("DOCKET_BASE_URL", "not configured"),
        # Auth status — stub until V3 RBAC
        "auth": "stub — X-Keeper-Token header injected, not validated"
    })


# ── Docket API proxy ───────────────────────────────────────────────
@app.route("/api/<path:path>", methods=["GET","POST","PUT","PATCH","DELETE","OPTIONS"])
def api_proxy(path):
    """
    All /api/* requests are proxied to Docket.
    The browser calls /api/series, we forward to Docket /series.
    Auth headers are injected by docket_proxy.proxy_request().
    """
    if request.method == "OPTIONS":
        # Preflight — keeper-web is same-origin so this shouldn't
        # be needed, but handle it cleanly just in case.
        from flask import Response
        return Response(
            headers={
                "Access-Control-Allow-Origin":  "*",
                "Access-Control-Allow-Methods": "GET,POST,PUT,PATCH,DELETE",
                "Access-Control-Allow-Headers": "Content-Type,Accept,X-Keeper-Token",
            },
            status=204
        )

    from docket_proxy import proxy_request
    return proxy_request(f"/{path}")


# ── Client pages ───────────────────────────────────────────────────
@app.route("/")
def index():
    return redirect("/app/attention")


@app.route("/app/<page>")
def serve_page(page):
    """
    Serve HTML pages from src/keeper-web/src/.
    Falls back to prototypes/ if the wired version doesn't exist yet.
    """
    filename = f"{page}.html"

    if (CLIENT_DIR / filename).exists():
        return send_from_directory(CLIENT_DIR, filename)

    if (PROTOTYPE_DIR / filename).exists():
        return send_from_directory(PROTOTYPE_DIR, filename)

    return f"Page '{page}' not found. "  \
           f"Add {filename} to keeper-web/src/ to wire it up.", 404


@app.route("/app/")
def app_index():
    return redirect("/app/attention")


# ── Static assets ──────────────────────────────────────────────────
@app.route("/static/<path:filename>")
def static_files(filename):
    return send_from_directory(CLIENT_DIR / "static", filename)


# ── Dev helper: list available pages ──────────────────────────────
@app.route("/pages")
def list_pages():
    pages = {}
    if CLIENT_DIR.exists():
        pages["wired"] = [
            f.stem for f in CLIENT_DIR.glob("*.html")
        ]
    if PROTOTYPE_DIR.exists():
        pages["prototypes"] = [
            f.stem for f in PROTOTYPE_DIR.glob("*.html")
        ]
    return jsonify(pages)


# ── Entry point ────────────────────────────────────────────────────
if __name__ == "__main__":
    port  = int(os.environ.get("PORT", 3001))
    debug = os.environ.get("DEBUG", "true").lower() == "true"

    print(f"\nkeeper-web server")
    print(f"  serving:  http://localhost:{port}")
    print(f"  client:   {CLIENT_DIR}")
    print(f"  protos:   {PROTOTYPE_DIR}")
    print(f"  docket:   {os.environ.get('DOCKET_BASE_URL','not set')}")
    print(f"  auth:     stub (X-Keeper-Token, not validated)")
    print()

    app.run(host="0.0.0.0", port=port, debug=debug)
