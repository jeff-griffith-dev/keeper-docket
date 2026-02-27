"""
docket_proxy.py

Proxies requests from keeper-web to the Docket API.
Handles:
  - Base URL resolution
  - Auth header injection (stub for V3 RBAC)
  - Dev HTTPS certificate bypass
  - Error normalization

All keeper-web API calls go through here — the browser
never talks to Docket directly.
"""
import os
import httpx
from flask import request, Response
import json

DOCKET_BASE_URL = os.environ["DOCKET_BASE_URL"].rstrip("/")
KEEPER_API_KEY  = os.environ.get("KEEPER_API_KEY", "keeper-dev-key")

# Headers the proxy always injects
_PROXY_HEADERS = {
    "X-Keeper-Token": KEEPER_API_KEY,   # stub auth — validate in V3
    "Content-Type":   "application/json",
    "Accept":         "application/json",
}

# Headers from the browser request we forward to Docket
_FORWARD_HEADERS = {"content-type", "accept"}


def _client() -> httpx.Client:
    """
    httpx client for Docket.
    verify=False handles the dev HTTPS cert on localhost.
    In production: set DOCKET_SSL_VERIFY=true in .env.
    """
    verify = os.environ.get("DOCKET_SSL_VERIFY", "false").lower() == "true"
    return httpx.Client(
        base_url=DOCKET_BASE_URL,
        verify=verify,
        timeout=30.0,
        headers=_PROXY_HEADERS,
    )


def proxy_request(path: str) -> Response:
    """
    Forward the incoming Flask request to Docket and return
    the response as a Flask Response.

    path: the Docket resource path, e.g. "/series" or "/minutes/abc-123"
    """
    # Build forwarded headers
    headers = dict(_PROXY_HEADERS)
    for key, value in request.headers:
        if key.lower() in _FORWARD_HEADERS:
            headers[key] = value

    # Get request body if present
    body = request.get_data() or None

    try:
        with _client() as c:
            docket_response = c.request(
                method=request.method,
                url=path,
                headers=headers,
                content=body,
                params=request.args,
            )

        # Pass the Docket response back to the browser
        return Response(
            response=docket_response.content,
            status=docket_response.status_code,
            headers={
                "Content-Type": docket_response.headers.get(
                    "content-type", "application/json"
                ),
                # CORS — keeper-web server is the only origin
                "Access-Control-Allow-Origin": "*",
            },
        )

    except httpx.ConnectError:
        return Response(
            response=json.dumps({
                "title": "DOCKET_UNAVAILABLE",
                "detail": f"Cannot reach Docket at {DOCKET_BASE_URL}. "
                           "Is the API running?"
            }),
            status=503,
            mimetype="application/json",
        )
    except httpx.TimeoutException:
        return Response(
            response=json.dumps({
                "title": "DOCKET_TIMEOUT",
                "detail": "Docket did not respond in time."
            }),
            status=504,
            mimetype="application/json",
        )
    except Exception as e:
        return Response(
            response=json.dumps({
                "title": "PROXY_ERROR",
                "detail": str(e)
            }),
            status=500,
            mimetype="application/json",
        )
