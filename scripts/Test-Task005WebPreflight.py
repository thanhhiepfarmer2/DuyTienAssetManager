"""Read-only/empty-form HTTP probe for TASK-005 on HTTPS localhost.
Does not authenticate, print response bodies, cookies, or anti-forgery tokens.
The local development certificate is allowed only for this fixed localhost URL.
This is HTTP evidence, not browser interaction or layout evidence.
"""
import html
import http.cookiejar
import json
import re
import ssl
import urllib.error
import urllib.parse
import urllib.request


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


base = "https://localhost:7008"
jar = http.cookiejar.CookieJar()
opener = urllib.request.build_opener(
    NoRedirect(), urllib.request.HTTPCookieProcessor(jar),
    urllib.request.HTTPSHandler(context=ssl._create_unverified_context()))


def request(path, data=None, accept="text/html"):
    req = urllib.request.Request(base + path, data=data, headers={"Accept": accept})
    if data is not None:
        req.add_header("Content-Type", "application/x-www-form-urlencoded")
    try:
        response = opener.open(req, timeout=30)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        return response.status, response.headers, html.unescape(response.read().decode("utf-8"))


results = []


def check(case, actual, expected):
    passed = actual == expected
    results.append(passed)
    print(json.dumps({"case": case, "expected": expected, "actual": actual, "pass": passed}, ensure_ascii=False))


try:
    status, headers, page = request("/Account/Login")
    check("Login GET", status, 200)
    check("Login labels/password field", all(value in page for value in
          ["T\u00ean \u0111\u0103ng nh\u1eadp", "M\u1eadt kh\u1ea9u", 'type="password"']), True)
    check("Remember-me unchecked by default", re.search(
        r'<input(?=[^>]*name="Input.RememberMe")(?=[^>]*type="checkbox")[^>]*checked', page) is None, True)
    csrf = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page)
    check("Anti-forgery hidden field", csrf is not None, True)
    status, _, _ = request("/Account/Login", urllib.parse.urlencode(
        {"Input.Username": "", "Input.Password": ""}).encode())
    check("Login POST without anti-forgery", status, 400)
    if csrf:
        status, _, page = request("/Account/Login", urllib.parse.urlencode(
            {"Input.Username": "", "Input.Password": "", "__RequestVerificationToken": csrf.group(1)}).encode())
        check("Login empty server validation", status == 200 and
              "Nh\u1eadp t\u00ean \u0111\u0103ng nh\u1eadp." in page and "Nh\u1eadp m\u1eadt kh\u1ea9u." in page, True)
    status, headers, _ = request("/Admin/Users")
    check("Anonymous admin page redirects to login", status == 302 and
          headers.get("Location", "").startswith("/Account/Login"), True)
    for accept in ["application/json", "text/html"]:
        status, headers, _ = request("/api/admin/users", accept=accept)
        check("Anonymous admin API " + accept, status, 401)
        check("API challenge has no redirect " + accept, headers.get("Location") is None, True)
    status, _, page = request("/Account/AccessDenied")
    check("Access denied page", status == 200 and "quy\u1ec1n" in page, True)
except Exception as error:
    print("Probe failed: " + type(error).__name__)
    raise SystemExit(1)
print("RESULT: passed=%d total=%d" % (sum(results), len(results)))
raise SystemExit(0 if all(results) else 1)
