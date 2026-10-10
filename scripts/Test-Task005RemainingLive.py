"""Focused TASK-005 W10 and real W13 HTTP UAT. Fake DEV users only.
No bootstrap/init/provisioning or TASK-006 calls. No credentials in args/output.
Temporary operator secret is removed before long waits and verified in finally.
Long web fixtures keep cookies only in RAM; interruption never means PASS.
"""
import argparse
import datetime as dt
import hashlib
import http.cookiejar
from html.parser import HTMLParser
import json
import os
from pathlib import Path
import secrets
import ssl
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
BASE = "https://localhost:7008"
REPORT = ROOT / "docs/07-testing/TASK-005-REMAINING-LIVE-EVIDENCE.json"
KEY = "UatTask005:AdminPassword"
UTC = dt.timezone.utc
last_request = 0
operator_password = None
store = None
cfg = {}
google_grant = None
google_grant_deadline = 0
backup_token = None
primary_path = None
original = None
primary_dirty = False
web_fixtures = []
report_preexisted = REPORT.exists()
evidence = {"task": "TASK-005", "source": "AI direct HTTP/Google; not browser",
            "startedAtUtc": dt.datetime.now(UTC).isoformat(), "state": "RUNNING", "cases": [],
            "webTimeouts": [], "temporaryCredentialRemoved": False}


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Inputs(HTMLParser):
    def __init__(self):
        super().__init__()
        self.hidden = {}

    def handle_starttag(self, tag, attrs):
        data = dict(attrs)
        if tag == "input" and data.get("type") == "hidden" and data.get("name"):
            self.hidden[data["name"]] = data.get("value", "")


opener = urllib.request.build_opener(NoRedirect(),
    urllib.request.HTTPSHandler(context=ssl._create_unverified_context()))


def save():
    if report_preexisted:
        return
    temp = REPORT.with_suffix(".tmp")
    temp.write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    os.replace(temp, REPORT)


def expect(case, actual, expected):
    passed = actual == expected
    entry = {"case": case, "expected": expected, "actual": actual,
             "pass": passed, "observedAtUtc": dt.datetime.now(UTC).isoformat()}
    evidence["cases"].append(entry)
    save()
    print(json.dumps(entry, ensure_ascii=True), flush=True)
    if not passed:
        raise RuntimeError("Unexpected focused UAT result")


def request(method, path, payload=None, token=None, client=None, form=False, cookie=None):
    global last_request
    delay = last_request + 5 - time.monotonic()
    if delay > 0:
        time.sleep(delay)
    headers = {"Accept": "text/html" if client or cookie else "application/json"}
    if token:
        headers["Authorization"] = "Bearer " + token
    if cookie:
        headers["Cookie"] = "dt_session=" + cookie
    data = None
    if payload is not None:
        data = urllib.parse.urlencode(payload).encode() if form else json.dumps(payload).encode()
        headers["Content-Type"] = "application/x-www-form-urlencoded" if form else "application/json"
    last_request = time.monotonic()
    req = urllib.request.Request(BASE + path, data=data, headers=headers, method=method)
    try:
        response = (client or opener).open(req, timeout=90)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        body = response.read().decode("utf-8")
        value = json.loads(body) if body and "json" in response.headers.get("Content-Type", "") else body
        return response.status, response.headers, value


def checked(case, method, path, expected, **kwargs):
    status, headers, value = request(method, path, **kwargs)
    expect(case, status, expected)
    return headers, value


def login(username, password):
    return checked("Login " + username, "POST", "/api/auth/login", 200,
                   payload={"username": username, "password": password})[1]


def assignments(role="Admin", scope_ids=None):
    ids = scope_ids or ["ALL"] * 3
    return {"assignments": [{"roleCode": role, "effectiveFrom": dt.datetime.now(UTC).isoformat(),
        "effectiveTo": None, "positionId": None, "scopes": [
            {"scopeType": i, "scopeId": ids[i], "includeDescendants": True} for i in range(3)]}]}


def config(key):
    return os.environ.get(key.replace(":", "__"), cfg.get(key))


def google_json(url):
    global google_grant, google_grant_deadline
    if google_grant is None or time.monotonic() >= google_grant_deadline:
        data = urllib.parse.urlencode({"client_id": config("GoogleWorkspace:OAuthClientId"),
            "client_secret": config("GoogleWorkspace:OAuthClientSecret"),
            "refresh_token": config("GoogleWorkspace:OAuthRefreshToken"),
            "grant_type": "refresh_token"}).encode()
        req = urllib.request.Request("https://oauth2.googleapis.com/token", data=data)
        with urllib.request.urlopen(req, timeout=90) as response:
            google_grant = json.load(response)
        if google_grant.get("scope") != "https://www.googleapis.com/auth/drive.file":
            raise RuntimeError("Unexpected OAuth scope")
        google_grant_deadline = time.monotonic() + int(google_grant["expires_in"]) - 120
    req = urllib.request.Request(url, headers={"Authorization": "Bearer " + google_grant["access_token"]})
    with urllib.request.urlopen(req, timeout=90) as response:
        return json.load(response)


def dev_preflight():
    resource = config("GoogleWorkspace:SystemSpreadsheetId")
    fields = "name,mimeType,ownedByMe,trashed,capabilities(canEdit),permissions(type,role),shared"
    value = google_json("https://www.googleapis.com/drive/v3/files/" +
                        urllib.parse.quote(resource, safe="") + "?fields=" + urllib.parse.quote(fields))
    acl = value.get("permissions", [])
    valid = (value.get("name") == "DuyTien Asset Management - DEV System" and
             value.get("mimeType") == "application/vnd.google-apps.spreadsheet" and
             value.get("ownedByMe") and not value.get("trashed") and
             value.get("capabilities", {}).get("canEdit") and not value.get("shared") and
             len(acl) == 1 and acl[0].get("type") == "user" and acl[0].get("role") == "owner" and
             resource != config("GoogleWorkspace:SpreadsheetId"))
    expect("DEV owner-only separate resource / exact drive.file", bool(valid), True)


def tables():
    names = ["Users", "Roles", "UserRoleAssignments", "RoleAssignmentScopes", "Sessions"]
    url = "https://sheets.googleapis.com/v4/spreadsheets/" + urllib.parse.quote(
        config("GoogleWorkspace:SystemSpreadsheetId"), safe="") + "/values:batchGet?"
    url += urllib.parse.urlencode([("ranges", "'" + name + "'!A:Q") for name in names])
    value = google_json(url)
    if value.get("spreadsheetId") != config("GoogleWorkspace:SystemSpreadsheetId"):
        raise RuntimeError("Unexpected snapshot resource")
    ranges = value.get("valueRanges", [])
    if len(ranges) != len(names):
        raise RuntimeError("Invalid snapshot")
    result = {}
    for name, item in zip(names, ranges):
        if item["range"].split("!")[0].strip("'") != name:
            raise RuntimeError("Invalid snapshot range")
        rows = item.get("values", [])
        if not rows:
            raise RuntimeError("Missing snapshot header")
        result[name] = [dict(zip(rows[0], row + [""] * (len(rows[0]) - len(row)))) for row in rows[1:]]
    return result


def current_assignments(snapshot, user_id):
    roles = {row["RoleId"]: row["Code"] for row in snapshot["Roles"]}
    grants = []
    now = dt.datetime.now(UTC)
    for row in snapshot["UserRoleAssignments"]:
        if row["UserId"] != user_id or row["State"] != "Active":
            continue
        if parse_date(row["EffectiveFrom"]) > now or (
                row["EffectiveTo"] and parse_date(row["EffectiveTo"]) <= now):
            continue
        scopes = [{"scopeType": ["OrgUnit", "Location", "CategorySubtree"].index(scope["ScopeType"]),
                   "scopeId": scope["ScopeId"], "includeDescendants": scope["IncludeDescendants"].lower() == "true"}
                  for scope in snapshot["RoleAssignmentScopes"] if scope["AssignmentId"] == row["AssignmentId"]]
        grants.append({"roleCode": roles[row["RoleId"]], "effectiveFrom": row["EffectiveFrom"],
                       "effectiveTo": row["EffectiveTo"] or None, "positionId": row["PositionId"] or None,
                       "scopes": scopes})
    return {"assignments": grants}


def eligible(value):
    return any(row["roleCode"] == "Admin" and all(any(
        scope["scopeType"] == i and scope["scopeId"] == "ALL" for scope in row["scopes"])
        for i in range(3)) for row in value["assignments"])


def parse_date(value):
    return dt.datetime.fromisoformat(value.replace("Z", "+00:00"))


def web_login(username, password, remember=False):
    jar = http.cookiejar.CookieJar()
    client = urllib.request.build_opener(NoRedirect(), urllib.request.HTTPCookieProcessor(jar),
        urllib.request.HTTPSHandler(context=ssl._create_unverified_context()))
    _, page = checked("Razor login form " + username, "GET", "/Account/Login", 200, client=client)
    parser = Inputs()
    parser.feed(page)
    if "__RequestVerificationToken" not in parser.hidden:
        raise RuntimeError("Missing anti-forgery")
    fields = parser.hidden | {"Input.Username": username, "Input.Password": password,
                             "Input.RememberMe": str(remember).lower()}
    headers, _ = checked("Razor login " + username, "POST", "/Account/Login", 302,
                         payload=fields, client=client, form=True)
    expect("Razor login leaves mandatory-change flow " + username,
           "/Account/ChangePassword" not in headers.get("Location", ""), True)
    cookie = next((item.value for item in jar if item.name == "dt_session"), None)
    if cookie is None:
        raise RuntimeError("Missing web cookie")
    return cookie


def cleanup_operator():
    global operator_password
    if store is None:
        return
    current = json.loads(store.read_text(encoding="utf-8-sig"))
    if KEY in current:
        if current[KEY] != operator_password:
            raise RuntimeError("Operator credential changed; replacement preserved")
        del current[KEY]
        temporary = None
        try:
            with tempfile.NamedTemporaryFile("w", dir=store.parent, prefix=".uat005-", suffix=".tmp",
                                             encoding="utf-8", delete=False) as output:
                temporary = Path(output.name)
                json.dump(current, output, ensure_ascii=False, indent=2)
                output.flush()
                os.fsync(output.fileno())
            os.replace(temporary, store)
        finally:
            if temporary is not None and temporary.exists():
                temporary.unlink()
    removed = KEY not in json.loads(store.read_text(encoding="utf-8-sig"))
    evidence["temporaryCredentialRemoved"] = removed
    save()
    if not removed:
        raise RuntimeError("Temporary operator credential not removed")
    operator_password = None
    cfg.pop(KEY, None)
    print("Temporary UAT credential removed and absence verified.", flush=True)


def restore_primary():
    global primary_dirty
    if primary_dirty and backup_token and original:
        checked("Restore primary active state", "POST", primary_path + "/unlock", 204, token=backup_token)
        checked("Restore original primary assignments", "PUT", primary_path + "/role-assignments", 204,
                payload=original, token=backup_token)
        snapshot = tables()
        row = next(row for row in snapshot["Users"] if row["UserId"] == primary_path.rsplit("/", 1)[1])
        expect("Primary restored active / effective Admin three ALL",
               row["State"] == "Active" and eligible(current_assignments(snapshot, row["UserId"])), True)
        primary_dirty = False


def new_fixture(admin_token, case, remember, interval):
    username = "uat005_web_" + case + "_" + secrets.token_hex(3)
    _, created = checked("Create fake web fixture " + case, "POST", "/api/admin/users", 201,
                         payload={"username": username, "displayName": "TASK-005 fake web timeout " + case},
                         token=admin_token)
    user = login(username, created["temporaryPassword"])
    password = "".join(secrets.choice("abcdefghijklmnopqrstuvwxyz") for _ in range(8))
    checked("Change fixture temporary password " + case, "POST", "/api/auth/change-password", 204,
            payload={"currentPassword": created["temporaryPassword"], "newPassword": password},
            token=user["accessToken"])
    cookie = web_login(username, password, remember)
    checked("Fixture protected page initially valid " + case, "GET", "/Account/ChangePassword", 200, cookie=cookie)
    snapshot = tables()
    fingerprint = hashlib.sha256(cookie.encode()).hexdigest().upper()
    session = next(row for row in snapshot["Sessions"] if row["AccessTokenHash"] == fingerprint)
    idle = parse_date(session["IdleExpiresAt"])
    absolute = parse_date(session["AbsoluteExpiresAt"])
    created_at = parse_date(session["CreatedAt"])
    expect("Issued real timeout policy " + case,
           ((idle - created_at).total_seconds(), (absolute - created_at).total_seconds()),
           (86400, 604800) if remember else (1800, 28800))
    deadline = idle if interval is None else absolute
    record = {"case": case, "username": username, "source": "AI HTTP / real wall-clock",
              "state": "PENDING_ELAPSED", "createdAtUtc": created_at.isoformat(),
              "idleExpiresAtUtc": idle.isoformat(), "absoluteExpiresAtUtc": absolute.isoformat(),
              "checkAfterUtc": (deadline + dt.timedelta(seconds=3)).isoformat(),
              "activityIntervalSeconds": interval, "checks": []}
    evidence["webTimeouts"].append(record)
    web_fixtures.append({"cookie": cookie, "record": record, "deadline": deadline,
                         "next": dt.datetime.now(UTC) + dt.timedelta(seconds=interval) if interval else deadline})
    save()


def wait_real_timeouts():
    evidence["state"] = "W10_PASS_W13_PENDING_REAL_TIME"
    save()
    print("W10 complete. Four W13 real-time fixtures started; credential absent, cookies only in RAM.", flush=True)
    while any(item["record"]["state"] == "PENDING_ELAPSED" for item in web_fixtures):
        now = dt.datetime.now(UTC)
        for item in web_fixtures:
            record = item["record"]
            if record["state"] != "PENDING_ELAPSED":
                continue
            if now >= item["deadline"] + dt.timedelta(seconds=3):
                status, headers, _ = request("GET", "/Account/ChangePassword", cookie=item["cookie"])
                passed = status == 302 and headers.get("Location", "").startswith("/Account/Login")
                record["checks"].append({"atUtc": dt.datetime.now(UTC).isoformat(), "expected": "302 login",
                                         "actualStatus": status, "redirectToLogin": passed})
                record["state"] = "PASS_REAL_ELAPSED" if passed else "FAIL_REAL_ELAPSED"
                save()
                expect("Real elapsed server expiry " + record["case"], passed, True)
                item["cookie"] = None
            elif record["activityIntervalSeconds"] and now >= item["next"]:
                status, _, _ = request("GET", "/Account/ChangePassword", cookie=item["cookie"])
                record["checks"].append({"atUtc": dt.datetime.now(UTC).isoformat(),
                                         "expected": 200, "actualStatus": status})
                save()
                expect("Keep idle active without extending absolute " + record["case"], status, 200)
                item["next"] = dt.datetime.now(UTC) + dt.timedelta(seconds=record["activityIntervalSeconds"])
        time.sleep(10)
    evidence["state"] = "PASS_W10_AND_W13_REAL_HTTP"
    save()


parser = argparse.ArgumentParser(description="Focused fake DEV UAT, no credentials in arguments")
parser.add_argument("--start-web-timeouts", action="store_true", help="Four real wall-clock fixtures, up to seven days")
args = parser.parse_args()

try:
    if REPORT.exists():
        raise RuntimeError("Existing focused evidence; do not overwrite or duplicate fixtures")
    project = ROOT / "src/DuyTien.AssetManagement.Server/DuyTien.AssetManagement.Server.csproj"
    secret_id = ET.parse(project).findtext(".//UserSecretsId")
    store = Path(os.environ["APPDATA"]) / "Microsoft/UserSecrets" / secret_id / "secrets.json"
    cfg = json.loads(store.read_text(encoding="utf-8-sig"))
    operator_password = cfg.get(KEY)
    if not operator_password:
        raise RuntimeError("Missing temporary UAT credential")
    dev_preflight()
    admin = login("admin", operator_password)
    _, identity = checked("Primary identity", "GET", "/api/me", 200, token=admin["accessToken"])
    expect("Primary unrestricted Admin", "Admin" in identity["roles"] and not identity["mustChangePassword"], True)
    snapshot = tables()
    original = current_assignments(snapshot, identity["userId"])
    expect("Primary effective Admin three ALL", eligible(original), True)
    primary_path = "/api/admin/users/" + identity["userId"]
    _, users = checked("Check existing backup before create", "GET", "/api/admin/users", 200, token=admin["accessToken"])
    if any(row["username"].lower() == "admin_second_demo" for row in users):
        raise RuntimeError("Existing backup preserved; separate credential verification required")
    _, created = checked("Create fake backup admin", "POST", "/api/admin/users", 201,
        payload={"username": "admin_second_demo", "displayName": "TASK-005 fake backup administrator"},
        token=admin["accessToken"])
    backup_path = "/api/admin/users/" + created["userId"]
    checked("Assign backup Admin three ALL", "PUT", backup_path + "/role-assignments", 204,
            payload=assignments(), token=admin["accessToken"])
    backup = login("admin_second_demo", created["temporaryPassword"])
    backup_password = "".join(secrets.choice("abcdefghijklmnopqrstuvwxyz") for _ in range(8))
    checked("Change backup mandatory password", "POST", "/api/auth/change-password", 204,
            payload={"currentPassword": created["temporaryPassword"], "newPassword": backup_password},
            token=backup["accessToken"])
    backup = login("admin_second_demo", backup_password)
    backup_token = backup["accessToken"]
    _, backup_identity = checked("Backup effective Admin identity", "GET", "/api/me", 200, token=backup_token)
    expect("Backup unrestricted Admin", backup_identity["roles"] == ["Admin"] and
           not backup_identity["mustChangePassword"], True)
    snapshot = tables()
    expect("Google backup active effective Admin same assignment three ALL",
           next(row for row in snapshot["Users"] if row["UserId"] == created["userId"])["State"] == "Active"
           and eligible(current_assignments(snapshot, created["userId"])), True)
    backup_cookie = web_login("admin_second_demo", backup_password)
    checked("Backup Razor protected administration", "GET", "/Admin/Users", 200, cookie=backup_cookie)
    backup_password = None
    created = None
    primary_dirty = True
    checked("Two admins allow primary lock", "POST", primary_path + "/lock", 204, token=backup_token)
    checked("Backup remains admin while primary locked", "GET", "/api/admin/users", 200, token=backup_token)
    checked("Primary old access revoked by lock", "GET", "/api/me", 401, token=admin["accessToken"])
    checked("Two admins allow primary unlock", "POST", primary_path + "/unlock", 204, token=backup_token)
    checked("Backup permits primary downgrade", "PUT", primary_path + "/role-assignments", 204,
            payload=assignments("Viewer"), token=backup_token)
    checked("Backup permits restore primary Admin", "PUT", primary_path + "/role-assignments", 204,
            payload=original, token=backup_token)
    checked("Backup permits primary lose one ALL", "PUT", primary_path + "/role-assignments", 204,
            payload=assignments("Admin", ["ALL", "ALL", "CAT-DEMO-EQUIPMENT"]), token=backup_token)
    restore_primary()
    admin = login("admin", operator_password)
    _, identity = checked("Primary relogin after restoration", "GET", "/api/me", 200, token=admin["accessToken"])
    expect("Primary restored login Admin", "Admin" in identity["roles"] and not identity["mustChangePassword"], True)
    checked("Backup session cleanup including Razor", "POST", "/api/auth/logout-all", 204, token=backup_token)
    backup_token = None
    backup_cookie = None
    evidence["backup"] = {"username": "admin_second_demo", "state": "Active", "effectiveRole": "Admin",
                          "scopeIds": ["ALL", "ALL", "ALL"], "mustChangePassword": False,
                          "loginVerified": True, "passwordRetained": False}
    evidence["state"] = "PASS_W10"
    save()
    if args.start_web_timeouts:
        new_fixture(admin["accessToken"], "default_idle", False, None)
        new_fixture(admin["accessToken"], "default_absolute", False, 1200)
        new_fixture(admin["accessToken"], "remember_idle", True, None)
        new_fixture(admin["accessToken"], "remember_absolute", True, 43200)
    checked("Primary runner session cleanup", "POST", "/api/auth/logout", 204, token=admin["accessToken"])
    admin = None
    cleanup_operator()
    if args.start_web_timeouts:
        wait_real_timeouts()
except BaseException as error:
    evidence["state"] = "INTERRUPTED" if isinstance(error, (KeyboardInterrupt, SystemExit)) else "FAIL"
    evidence["errorType"] = type(error).__name__
    save()
    print("FAIL/INTERRUPTED: " + type(error).__name__ + "; no secret/body/trace printed.", flush=True)
    raise SystemExit(1)
finally:
    try:
        restore_primary()
        cleanup_operator()
    except BaseException as cleanup_error:
        evidence["cleanupErrorType"] = type(cleanup_error).__name__
        evidence["state"] = "FAIL_CLEANUP"
        save()
        print("FAIL: cleanup " + type(cleanup_error).__name__ + "; no secret printed.", flush=True)
        raise SystemExit(1)
    operator_password = None
    backup_token = None
    google_grant = None
