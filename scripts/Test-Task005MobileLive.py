"""TASK-005 mobile API UAT on HTTPS localhost, fake users only.
Run after admin has changed the bootstrap password; includes Razor HTTP protocol checks.
Credentials use hidden prompt or a temporary external user-secrets key; passwords/tokens/bodies are never printed.
Generated fake account is retained for audit; no delete or TASK-006 calls.
Pacing avoids exhausting the per-user Google read quota. No timeout overrides.
Includes real 15-minute access/lockout expiry; does not prove browser, long web/refresh expiry or MAUI.
"""
import argparse
import datetime
import getpass
import html
import http.cookiejar
from html.parser import HTMLParser
import urllib.parse
import json
import os
from pathlib import Path
import tempfile
import xml.etree.ElementTree as ET
import secrets
import ssl
import sys
import time
import urllib.error
import urllib.request

BASE = "https://localhost:7008"
MIN_INTERVAL = 5
last_request = 0
results = []


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


opener = urllib.request.build_opener(NoRedirect(), urllib.request.HTTPSHandler(
    context=ssl._create_unverified_context()))


def call(method, path, payload=None, token=None, accept="application/json"):
    global last_request, admin_token, admin
    if token and token == globals().get("admin_token"):
        expiry = datetime.datetime.fromisoformat(admin["accessExpiresAt"].replace("Z", "+00:00"))
        if (expiry - datetime.datetime.now(datetime.timezone.utc)).total_seconds() < 180:
            status, _, rotated_admin = call("POST", "/api/auth/refresh",
                {"refreshToken": admin["refreshToken"]})
            expect("Admin runner refresh rotation", status, 200)
            admin.update(rotated_admin)
            admin_token = admin["accessToken"]
            token = admin_token
    delay = last_request + MIN_INTERVAL - time.monotonic()
    if delay > 0:
        time.sleep(delay)
    headers = {"Accept": accept}
    if token:
        headers["Authorization"] = "Bearer " + token
    data = None
    if payload is not None:
        headers["Content-Type"] = "application/json"
        data = json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(BASE + path, data=data, headers=headers, method=method)
    last_request = time.monotonic()
    try:
        response = opener.open(request, timeout=90)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        body = response.read()
        parsed = json.loads(body) if body and "json" in response.headers.get("Content-Type", "") else None
        return response.status, response.headers, parsed


def expect(case, actual, expected):
    passed = actual == expected
    results.append(passed)
    print(json.dumps({"case": case, "expected": expected, "actual": actual, "pass": passed}), flush=True)
    if not passed:
        raise RuntimeError("Unexpected UAT result")


def checked(case, method, path, expected, payload=None, token=None, accept="application/json"):
    status, headers, value = call(method, path, payload, token, accept)
    expect(case, status, expected)
    return headers, value


def login(username, password):
    _, value = checked("Mobile login", "POST", "/api/auth/login", 200,
                       {"username": username, "password": password})
    return value


def new_password():
    return "".join(secrets.choice("abcdefghijklmnopqrstuvwxyz") for _ in range(8))


def assignments(role_codes):
    def scopes(role):
        return (["OU-DEMO-B", "LOC-DEMO-B", "CAT-DEMO-VEHICLE"] if role == "DocumentManager"
                else ["OU-DEMO-A", "LOC-DEMO-A", "CAT-DEMO-EQUIPMENT"])
    return {"assignments": [{"roleCode": role,
        "effectiveFrom": datetime.datetime.now(datetime.timezone.utc).isoformat(),
        "effectiveTo": None, "positionId": None,
        "scopes": [{"scopeType": i, "scopeId": scopes(role)[i], "includeDescendants": True}
                   for i in range(3)]} for role in role_codes]}


def invalid_session(value, case):
    checked(case + " access revoked", "GET", "/api/me", 401, token=value["accessToken"])
    checked(case + " refresh revoked", "POST", "/api/auth/refresh", 401,
            {"refreshToken": value["refreshToken"]})


class HiddenInputs(HTMLParser):
    def __init__(self):
        super().__init__()
        self.values = {}
        self.inputs = {}
        self.selected_roles = []
        self.select_name = None

    def handle_starttag(self, tag, attrs):
        fields = dict(attrs)
        if tag == "select":
            self.select_name = fields.get("name")
        if tag == "option" and self.select_name == "Input.RoleCode" and "selected" in fields:
            self.selected_roles.append(fields.get("value"))
        if tag == "input" and fields.get("name", "").startswith("Input.") and fields.get("type") != "hidden":
            self.inputs[fields["name"]] = fields
        if tag == "input" and fields.get("type") == "hidden" and fields.get("name"):
            self.values[fields["name"]] = fields.get("value", "")


def web_client():
    jar = http.cookiejar.CookieJar()
    client = urllib.request.build_opener(NoRedirect(), urllib.request.HTTPCookieProcessor(jar),
        urllib.request.HTTPSHandler(context=ssl._create_unverified_context()))
    return client, jar


def web_call(client, method, path, fields=None, expected=200, case="Razor HTTP"):
    global last_request
    # A Razor page may perform more Google reads than one API request.
    delay = last_request + MIN_INTERVAL - time.monotonic()
    if delay > 0:
        time.sleep(delay)
    data = urllib.parse.urlencode(fields).encode("utf-8") if fields is not None else None
    request = urllib.request.Request(BASE + path, data=data, method=method, headers={"Accept": "text/html"})
    last_request = time.monotonic()
    try:
        response = client.open(request, timeout=90)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        body = response.read().decode("utf-8")
        expect(case, response.status, expected)
        return response.headers, body


def csrf_fields(body):
    fields = HiddenInputs()
    fields.feed(body)
    expect("Razor anti-forgery field present", "__RequestVerificationToken" in fields.values, True)
    return fields.values


def web_login(username, password, remember=False):
    client, jar = web_client()
    _, body = web_call(client, "GET", "/Account/Login", case="Razor login form")
    fields = csrf_fields(body)
    fields.update({"Input.Username": username, "Input.Password": password, "Input.RememberMe": str(remember).lower()})
    headers, _ = web_call(client, "POST", "/Account/Login", fields, 302, "Razor valid login")
    cookie = next((item for item in jar if item.name == "dt_session"), None)
    expect("Session cookie present", cookie is not None, True)
    expect("Cookie HttpOnly/Secure/SameSite Lax", cookie.secure and "httponly" in {key.lower() for key in cookie._rest} and
           next((value for key, value in cookie._rest.items() if key.lower() == "samesite"), "").lower() == "lax", True)
    expect("Remember cookie persistence", cookie.expires is not None, remember)
    if remember:
        expect("Remember absolute expiry 7 days", 604650 <= cookie.expires - time.time() <= 604800, True)
    return client, jar


def check_scope_page(client, path, role, scope_ids):
    _, body = web_call(client, "GET", path, case="Persisted Razor role/scope detail")
    parsed = HiddenInputs()
    parsed.feed(body)
    expect("Persisted selected role", parsed.selected_roles, [role])
    for field, scope_id in zip(["Input.OrgUnitScopeId", "Input.LocationScopeId", "Input.CategoryScopeId"], scope_ids):
        expect("Persisted scope field " + field, parsed.inputs.get(field, {}).get("value"), scope_id)
    expect("Persisted descendant checkbox", "checked" in parsed.inputs.get("Input.IncludeDescendants", {}), True)
    expect("Persisted scope display", all(scope_id in body for scope_id in scope_ids), True)


def web_invalid(client, case):
    headers, _ = web_call(client, "GET", "/Admin/Users", expected=302, case=case + " cookie revoked")
    expect(case + " requests login", headers.get("Location", "").startswith("/Account/Login"), True)


parser = argparse.ArgumentParser(description="TASK-005 dev UAT; no credential arguments")
parser.add_argument("--operator-secret", action="store_true", help="Read temporary UatTask005 key from local user-secrets and remove it on exit")
parser.add_argument("--restart-checkpoint", action="store_true", help="Pause for an authorized backend restart; a temporary marker file acknowledges completion")
args = parser.parse_args()
if args.restart_checkpoint and not args.operator_secret:
    parser.error("--restart-checkpoint requires --operator-secret")
operator_store = None
operator_password = None
operator_key = "UatTask005:AdminPassword"


def operator_password_from_store():
    global operator_store, operator_password
    project = Path(__file__).resolve().parents[1] / "src/DuyTien.AssetManagement.Server/DuyTien.AssetManagement.Server.csproj"
    secret_id = ET.parse(project).findtext(".//UserSecretsId")
    operator_store = Path(os.environ["APPDATA"]) / "Microsoft/UserSecrets" / secret_id / "secrets.json"
    cfg = json.loads(operator_store.read_text(encoding="utf-8-sig"))
    operator_password = cfg.get(operator_key)
    if not operator_password:
        raise RuntimeError("Temporary UAT secret missing")
    return operator_password


def cleanup_operator_secret():
    if operator_store is None or operator_password is None:
        return
    cfg = json.loads(operator_store.read_text(encoding="utf-8-sig"))
    # Preserve a replacement entered by the user while the test was running.
    if cfg.get(operator_key) != operator_password:
        return
    del cfg[operator_key]
    temporary = None
    try:
        with tempfile.NamedTemporaryFile("w", dir=operator_store.parent, prefix=".uat005-", suffix=".tmp",
                                         encoding="utf-8", delete=False) as output:
            temporary = Path(output.name)
            json.dump(cfg, output, ensure_ascii=False, indent=2)
            output.flush()
            os.fsync(output.fileno())
        os.replace(temporary, operator_store)
        print("Temporary UAT secret removed; no credential printed.", flush=True)
    finally:
        if temporary is not None and temporary.exists():
            temporary.unlink()


if not args.operator_secret and not sys.stdin.isatty():
    print("Run interactively or use the authorized temporary operator-secret store.")
    raise SystemExit(2)

try:
    username = "admin" if args.operator_secret else (input("Admin username [admin]: ").strip() or "admin")
    password = operator_password_from_store() if args.operator_secret else getpass.getpass("Current admin password (hidden): ")
    admin = login(username, password)
    password = None
    if admin.get("mustChangePassword"):
        print("NOT_RUN: complete mandatory admin password change in Razor Pages first.")
        raise SystemExit(2)
    admin_token = admin["accessToken"]
    _, me = checked("Admin identity", "GET", "/api/me", 200, token=admin_token)
    expect("Admin role present", "Admin" in me["roles"], True)
    web_admin, web_admin_jar = web_login(username, operator_password) if args.operator_secret else (None, None)
    if args.restart_checkpoint:
        marker = Path(tempfile.gettempdir()) / ("DuyTien-Task005-Uat-Restart-" + str(os.getpid()) + ".continue")
        if marker.exists():
            raise RuntimeError("Unexpected pre-existing restart marker")
        print("RESTART_CHECKPOINT_READY: after backend restart, create marker " + str(marker), flush=True)
        deadline = time.monotonic() + 600
        try:
            while not marker.exists():
                if time.monotonic() >= deadline:
                    raise RuntimeError("Restart checkpoint timeout")
                time.sleep(2)
        finally:
            if marker.exists():
                marker.unlink()
        _, persisted = checked("Admin session survives backend restart", "GET", "/api/me", 200, token=admin_token)
        expect("Admin identity/roles after restart", persisted["userId"] == me["userId"] and persisted["roles"] == me["roles"] and
               persisted["mustChangePassword"] is False, True)
        web_call(web_admin, "GET", "/Admin/Users", case="Admin Razor cookie survives restart")
        after_restart = login(username, operator_password)
        checked("Additional restart login session logout", "POST", "/api/auth/logout", 204, token=after_restart["accessToken"])
    _, original_users = checked("Last-admin precondition", "GET", "/api/admin/users", 200, token=admin_token)
    if len(original_users) != 1 or original_users[0]["userId"] != me["userId"]:
        print("NOT_RUN_LAST_ADMIN: precondition is not exactly one bootstrap user; no self-mutation attempted.", flush=True)
    else:
        own_path = "/api/admin/users/" + me["userId"]
        checked("Last admin cannot be locked", "POST", own_path + "/lock", 409, token=admin_token)
        checked("Last admin cannot become Viewer", "PUT", own_path + "/role-assignments", 409,
                assignments(["Viewer"]), admin_token)
        partial_all = assignments(["Admin"])
        for scope in partial_all["assignments"][0]["scopes"]:
            scope["scopeId"] = "ALL" if scope["scopeType"] != 2 else "CAT-DEMO-EQUIPMENT"
        checked("Last admin cannot lose one ALL", "PUT", own_path + "/role-assignments", 409,
                partial_all, admin_token)
        _, unchanged = checked("Last admin remains unchanged", "GET", "/api/admin/users", 200, token=admin_token)
        expect("Last-admin user snapshot unchanged", unchanged == original_users, True)
    if web_admin:
        check_scope_page(web_admin, "/Admin/Users/Detail/" + me["userId"], "Admin", ["ALL", "ALL", "ALL"])
        _, page = web_call(web_admin, "GET", "/Admin/Users", case="Admin Razor user list")
        expect("User list contains admin", "admin" in html.unescape(page), True)
        web_call(web_admin, "POST", "/Account/Logout", {}, 400, "Authenticated missing anti-forgery")
        web_call(web_admin, "POST", "/Account/Logout", {"__RequestVerificationToken": "invalid"}, 400, "Authenticated invalid anti-forgery")
        api_response = web_call(web_admin, "GET", "/api/admin/users", expected=403, case="Cookie cannot call bearer admin API")
        remembered, _ = web_login(username, operator_password, True)
        _, home = web_call(remembered, "GET", "/", case="Remember session authenticated")
        web_call(remembered, "POST", "/Account/Logout", csrf_fields(home), 302, "Remember logout")
        web_invalid(remembered, "Remember logout")
    # Independent fake fixtures allow real expiry/lockout time to elapse during other cases.
    expiry_name = "uat005_expiry_" + secrets.token_hex(4)
    _, expiry_created = checked("Create expiry fixture", "POST", "/api/admin/users", 201,
        {"username": expiry_name, "displayName": "TASK-005 fake expiry account"}, admin_token)
    expiry_session = login(expiry_name, expiry_created["temporaryPassword"])
    expiry_created = None
    checked("Expiry fixture access initially valid", "GET", "/api/me", 200, token=expiry_session["accessToken"])
    expiry_deadline = datetime.datetime.fromisoformat(expiry_session["accessExpiresAt"].replace("Z", "+00:00"))
    print("Real 15-minute access expiry fixture started; token kept only in RAM.", flush=True)
    lockout_name = "uat005_lockout_" + secrets.token_hex(4)
    _, lockout_created = checked("Create lockout fixture", "POST", "/api/admin/users", 201,
        {"username": lockout_name, "displayName": "TASK-005 fake lockout account"}, admin_token)
    wrong_password = new_password()
    for attempt in range(5):
        checked("Failed login attempt " + str(attempt + 1), "POST", "/api/auth/login", 401,
                {"username": lockout_name, "password": wrong_password})
    _, locked_record = checked("Read fifth-failure lockout", "GET", "/api/admin/users/" + lockout_created["userId"],
                               200, token=admin_token)
    lockout_deadline = datetime.datetime.fromisoformat(locked_record["lockoutEndAt"].replace("Z", "+00:00"))
    expect("Fifth failure sets 15-minute lockout", 820 <= (lockout_deadline - datetime.datetime.now(datetime.timezone.utc)).total_seconds() <= 900, True)
    checked("Correct password blocked during lockout", "POST", "/api/auth/login", 401,
            {"username": lockout_name, "password": lockout_created["temporaryPassword"]})
    for attempt in range(7, 11):
        checked("Failed/rate-limited window attempt " + str(attempt), "POST", "/api/auth/login", 401,
                {"username": lockout_name, "password": wrong_password})
    checked("Eleventh attempt rate limited", "POST", "/api/auth/login", 429,
            {"username": lockout_name, "password": wrong_password})
    wrong_password = None
    fake_username = "uat005_" + secrets.token_hex(4)
    headers, created = checked("Create fake account", "POST", "/api/admin/users", 201,
        {"username": fake_username, "displayName": "TASK-005 fake UAT account"}, admin_token)
    expect("Temporary password response no-store", headers.get("Cache-Control"), "no-store")
    fake_id = created["userId"]
    print("Fake username: " + fake_username, flush=True)
    user_path = "/api/admin/users/" + fake_id
    checked("Assign Viewer/scopes", "PUT", user_path + "/role-assignments", 204,
            assignments(["Viewer"]), admin_token)
    if web_admin:
        check_scope_page(web_admin, "/Admin/Users/Detail/" + fake_id, "Viewer", ["OU-DEMO-A", "LOC-DEMO-A", "CAT-DEMO-EQUIPMENT"])
    user = login(fake_username, created["temporaryPassword"])
    expect("First login must change", user["mustChangePassword"], True)
    checked("Must-change blocks admin API", "GET", "/api/admin/users", 403, token=user["accessToken"])
    checked("Password 7 rejected", "POST", "/api/auth/change-password", 400,
        {"currentPassword": created["temporaryPassword"], "newPassword": new_password()[:7]}, user["accessToken"])
    fake_password = new_password()
    checked("Simple lowercase password 8 accepted", "POST", "/api/auth/change-password", 204,
        {"currentPassword": created["temporaryPassword"], "newPassword": fake_password}, user["accessToken"])
    created = None
    invalid_session(user, "Password change")
    user = login(fake_username, fake_password)
    expect("Must-change cleared", user["mustChangePassword"], False)
    expiry = datetime.datetime.fromisoformat(user["accessExpiresAt"].replace("Z", "+00:00"))
    seconds = (expiry - datetime.datetime.now(datetime.timezone.utc)).total_seconds()
    expect("Access expiry issued for 15 minutes", 850 <= seconds <= 900, True)
    for accept in ["application/json", "text/html"]:
        headers, _ = checked("Viewer admin denied " + accept, "GET", "/api/admin/users", 403,
                             token=user["accessToken"], accept=accept)
        expect("Viewer API no redirect " + accept, headers.get("Location") is None, True)
    _, rotated = checked("Refresh rotation", "POST", "/api/auth/refresh", 200,
                         {"refreshToken": user["refreshToken"]})
    expect("Access and refresh changed", rotated["accessToken"] != user["accessToken"] and
           rotated["refreshToken"] != user["refreshToken"], True)
    checked("Old access after rotation", "GET", "/api/me", 401, token=user["accessToken"])
    checked("Rotated access valid", "GET", "/api/me", 200, token=rotated["accessToken"])
    checked("Old refresh reuse denied", "POST", "/api/auth/refresh", 401,
            {"refreshToken": user["refreshToken"]})
    invalid_session(rotated, "Refresh reuse family")
    user = login(fake_username, fake_password)
    web_user, _ = web_login(fake_username, fake_password)
    denied_headers, _ = web_call(web_user, "GET", "/Admin/Users", expected=302, case="Viewer Razor admin denied")
    expect("Viewer redirects to access denied", denied_headers.get("Location", "").startswith("/Account/AccessDenied"), True)
    web_call(web_user, "GET", "/Account/AccessDenied", case="Viewer access denied page")
    checked("Change role assignments", "PUT", user_path + "/role-assignments", 204,
            assignments(["AssetEditor", "DocumentManager"]), admin_token)
    invalid_session(user, "Authorization change")
    web_invalid(web_user, "Authorization change")
    user = login(fake_username, fake_password)
    _, identity = checked("Multiple role identity", "GET", "/api/me", 200, token=user["accessToken"])
    expect("Two role assignments active", sorted(identity["roles"]), ["AssetEditor", "DocumentManager"])
    if web_admin:
        _, multi_page = web_call(web_admin, "GET", "/Admin/Users/Detail/" + fake_id, case="Multiple assignments in Razor detail")
        expect("Razor displays both role/scope groups", all(value in multi_page for value in
               ["AssetEditor", "DocumentManager", "OU-DEMO-A", "OU-DEMO-B", "LOC-DEMO-A", "LOC-DEMO-B", "CAT-DEMO-EQUIPMENT", "CAT-DEMO-VEHICLE"]), True)
    checked("Editor has no admin permission", "GET", "/api/admin/users", 403, token=user["accessToken"])
    web_user, _ = web_login(fake_username, fake_password)
    checked("Lock fake user", "POST", user_path + "/lock", 204, token=admin_token)
    invalid_session(user, "Lock")
    web_invalid(web_user, "Lock")
    checked("Locked login denied", "POST", "/api/auth/login", 401,
            {"username": fake_username, "password": fake_password})
    checked("Unlock fake user", "POST", user_path + "/unlock", 204, token=admin_token)
    user = login(fake_username, fake_password)
    web_user, _ = web_login(fake_username, fake_password)
    headers, reset = checked("Reset fake user", "POST", user_path + "/reset-password", 200, token=admin_token)
    expect("Reset response no-store", headers.get("Cache-Control"), "no-store")
    invalid_session(user, "Reset")
    web_invalid(web_user, "Reset")
    user = login(fake_username, reset["temporaryPassword"])
    expect("Reset requires password change", user["mustChangePassword"], True)
    fake_password = new_password()
    checked("Change reset password", "POST", "/api/auth/change-password", 204,
        {"currentPassword": reset["temporaryPassword"], "newPassword": fake_password}, user["accessToken"])
    reset = None
    user = login(fake_username, fake_password)
    checked("Mobile logout", "POST", "/api/auth/logout", 204, token=user["accessToken"])
    invalid_session(user, "Logout")
    six_sessions = [login(fake_username, fake_password) for _ in range(6)]
    checked("Sixth session revokes oldest", "GET", "/api/me", 401, token=six_sessions[0]["accessToken"])
    checked("Newest session remains active", "GET", "/api/me", 200, token=six_sessions[-1]["accessToken"])
    checked("Cleanup six-session test", "POST", "/api/auth/logout-all", 204, token=six_sessions[-1]["accessToken"])
    first = login(fake_username, fake_password)
    second = login(fake_username, fake_password)
    checked("Logout all", "POST", "/api/auth/logout-all", 204, token=first["accessToken"])
    invalid_session(first, "Logout-all first")
    invalid_session(second, "Logout-all second")
    elapsed_wait = (max(expiry_deadline, lockout_deadline) - datetime.datetime.now(datetime.timezone.utc)).total_seconds() + 2
    if elapsed_wait > 0:
        print("Waiting for actual dev expiry/lockout deadlines; no auth timeout override.", flush=True)
        while elapsed_wait > 0:
            time.sleep(min(elapsed_wait, 10))
            elapsed_wait = (max(expiry_deadline, lockout_deadline) - datetime.datetime.now(datetime.timezone.utc)).total_seconds() + 2
    checked("Access expired after real 15 minutes", "GET", "/api/me", 401, token=expiry_session["accessToken"])
    _, expiry_refreshed = checked("Refresh after access expiry", "POST", "/api/auth/refresh", 200,
                                 {"refreshToken": expiry_session["refreshToken"]})
    checked("Expiry fixture refreshed access valid", "GET", "/api/me", 200, token=expiry_refreshed["accessToken"])
    checked("Expiry fixture logout", "POST", "/api/auth/logout", 204, token=expiry_refreshed["accessToken"])
    unlocked_session = login(lockout_name, lockout_created["temporaryPassword"])
    expect("Login allowed after actual lockout expiry", unlocked_session["mustChangePassword"], True)
    checked("Lockout fixture logout", "POST", "/api/auth/logout", 204, token=unlocked_session["accessToken"])
    lockout_created = None
    _, events = checked("Read audit", "GET", "/api/admin/security-events", 200, token=admin_token)
    actions = {event["action"] for event in events}
    expect("Expected audit actions", {"USER_CREATE", "AUTHORIZATION_REPLACE", "PASSWORD_CHANGE",
        "REFRESH_REUSE", "USER_LOCK", "USER_UNLOCK", "PASSWORD_RESET", "LOGOUT", "LOGOUT_ALL"}.issubset(actions), True)
    if web_admin:
        _, page = web_call(web_admin, "GET", "/", case="Admin web logout form")
        web_call(web_admin, "POST", "/Account/Logout", csrf_fields(page), 302, "Admin web logout")
        web_invalid(web_admin, "Admin web logout")
    checked("Admin runner session logout", "POST", "/api/auth/logout", 204, token=admin_token)
    print("PASS_LIVE_API: %d assertions; fake account retained. Browser interaction/layout and long web/refresh expiry still require separate evidence; restart/last-admin depend on cases printed above." % len(results))
except Exception as error:
    print("FAIL: " + type(error).__name__ + "; no password/token/hash/response body printed.")
    raise SystemExit(1)

finally:
    try:
        cleanup_operator_secret()
    except Exception as cleanup_error:
        print("FAIL: temporary secret cleanup " + type(cleanup_error).__name__ + "; no credential printed.", flush=True)
        raise SystemExit(1)
    operator_password = None
