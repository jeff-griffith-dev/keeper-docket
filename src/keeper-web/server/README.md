# keeper-web / server

The web server for the keeper-web client. Serves the HTML pages
and proxies all API calls to Docket.

## Why a Separate Server

The browser never talks to Docket directly. All API calls go through
this server, which:

- Injects the auth header on every request (`X-Keeper-Token`)
- Handles the dev HTTPS certificate on localhost
- Normalizes Docket error responses before they reach the browser
- Provides a single origin for the client (no CORS)

When V3 RBAC arrives, the auth logic lives here — the client and
Docket don't change structurally.

When multiple Docket instances exist (projects, tenants), the proxy
routing lives here too.

## Setup

```bash
cd src/keeper-web/server
python -m venv .venv
.venv\Scripts\activate          # Windows
pip install -r requirements.txt
copy .env.template .env         # fill in your values
python app.py
```

Then open http://localhost:3001

## Configuration (.env)

| Variable | Default | Purpose |
|---|---|---|
| `DOCKET_BASE_URL` | `https://localhost:57526` | Docket API base URL |
| `DOCKET_STUB_USER_ID` | (guid) | Current user until real auth exists |
| `KEEPER_API_KEY` | `keeper-dev-key` | Token injected on proxied requests |
| `DOCKET_SSL_VERIFY` | `false` | Set `true` in production |
| `PORT` | `3001` | Web server port |
| `DEBUG` | `true` | Flask debug mode |

## Routes

| Route | Purpose |
|---|---|
| `GET /` | Redirect to `/app/attention` |
| `GET /app/{page}` | Serve `{page}.html` from `src/` or `prototypes/` |
| `ANY /api/{path}` | Proxy to Docket `/{path}` with auth headers |
| `GET /health` | Server health check |
| `GET /pages` | Dev helper — list available pages |

## Page Resolution

When you request `/app/attention`, the server looks for:
1. `src/keeper-web/src/attention.html` — the wired client (production)
2. `src/keeper-web/prototypes/attention.html` — the prototype (fallback)

This means you can wire pages incrementally. Until a wired version
exists, the prototype is served automatically.

## Auth Stub

Every proxied request includes `X-Keeper-Token: {KEEPER_API_KEY}`.
Docket currently logs this header but does not validate it.

When V3 RBAC is implemented:
- This server gains a login endpoint that validates credentials
  and issues a session token
- The proxy validates the session token before forwarding
- Docket gains real token validation middleware
- The client gains a login page

The header slot is already in place. Nothing structural changes.

## Growth Path

```
V1  Single Docket instance, stub auth, one user
V2  Projects grouping Series, still single Docket
V3  Enterprise RBAC, multi-tenant, Keeper connects to
    multiple Docket instances via config
```

The proxy routing in `docket_proxy.py` is where multi-instance
support will live when V3 arrives.
