"""
Docket API client.
Wraps the Docket REST API for use by Keeper.
"""
import os
import httpx
from typing import Any

DOCKET_BASE_URL = os.environ["DOCKET_BASE_URL"].rstrip("/")
DOCKET_USER_ID = os.environ["DOCKET_STUB_USER_ID"]


def _client() -> httpx.Client:
    """
    Return an httpx client configured for Docket.
    verify=False handles the dev HTTPS certificate on localhost.
    In production this should be True.
    """
    return httpx.Client(
        base_url=DOCKET_BASE_URL,
        verify=False,  # dev cert — set to True in production
        headers={
            "Content-Type": "application/json",
            "Accept": "application/json",
        },
        timeout=30.0
    )


# ── Series ────────────────────────────────────────────────────────

def create_series(name: str, project: str | None = None) -> dict:
    with _client() as c:
        r = c.post("/series", json={"name": name, "project": project})
        r.raise_for_status()
        return r.json()


def get_series(series_id: str) -> dict:
    with _client() as c:
        r = c.get(f"/series/{series_id}")
        r.raise_for_status()
        return r.json()


# ── Minutes ───────────────────────────────────────────────────────

def create_minutes(series_id: str, scheduled_for: str) -> dict:
    with _client() as c:
        r = c.post("/minutes", json={
            "seriesId": series_id,
            "scheduledFor": scheduled_for
        })
        r.raise_for_status()
        return r.json()


def get_minutes(minutes_id: str) -> dict:
    with _client() as c:
        r = c.get(f"/minutes/{minutes_id}")
        r.raise_for_status()
        return r.json()


def finalize_minutes(minutes_id: str) -> dict | None:
    """
    Attempt to finalize minutes.
    Returns None if blocked by unresolved non-recurring items,
    with the blocking item IDs attached as an attribute on the exception.
    Raises httpx.HTTPStatusError for other errors.
    """
    with _client() as c:
        r = c.post(f"/minutes/{minutes_id}/finalize", json={})
        if r.status_code == 409:
            data = r.json()
            if data.get("title") == "UNRESOLVED_ITEMS":
                err = httpx.HTTPStatusError(
                    "Unresolved items", request=r.request, response=r)
                err.blocking_item_ids = data.get("blockingItemIds", [])
                raise err
        r.raise_for_status()
        return r.json()


# ── Topics ────────────────────────────────────────────────────────

def create_topic(minutes_id: str, title: str,
                 topic_type: str = "Discussion") -> dict:
    with _client() as c:
        r = c.post(f"/minutes/{minutes_id}/topics", json={
            "title": title,
            "type": topic_type
        })
        r.raise_for_status()
        return r.json()


# ── Action Items ──────────────────────────────────────────────────

def create_action_item(topic_id: str, title: str,
                       responsible_id: str,
                       due_date: str | None = None,
                       priority: int = 2) -> dict:
    with _client() as c:
        payload: dict[str, Any] = {
            "title": title,
            "responsibleId": responsible_id,
            "priority": priority
        }
        if due_date:
            payload["dueDate"] = due_date
        r = c.post(f"/topics/{topic_id}/action-items", json=payload)
        r.raise_for_status()
        return r.json()


def update_action_item(action_item_id: str, **kwargs) -> dict:
    with _client() as c:
        r = c.patch(f"/action-items/{action_item_id}", json=kwargs)
        r.raise_for_status()
        return r.json()


# ── Users ─────────────────────────────────────────────────────────

def get_user(user_id: str) -> dict:
    with _client() as c:
        r = c.get(f"/users/{user_id}")
        r.raise_for_status()
        return r.json()


def find_or_create_user(email: str, display_name: str) -> dict:
    """
    Create a user, returning the existing one if the email is already registered.
    """
    with _client() as c:
        r = c.post("/users", json={
            "email": email,
            "displayName": display_name
        })
        if r.status_code in (200, 201):
            return r.json()
        if r.status_code == 409:
            # Email exists — fetch by searching (placeholder until
            # a GET /users?email= endpoint exists)
            raise NotImplementedError(
                "User lookup by email not yet supported by Docket API. "
                "Use user ID directly.")
        r.raise_for_status()
        return r.json()
