"""Synthetic HTTP acceptance check. Requires the opt-in loopback smoke host; never accepts a remote URL."""
import http.cookiejar
import io
import json
import re
import time
import urllib.error
import urllib.request
import zipfile

BASE = "http://127.0.0.1:5191"
jar = http.cookiejar.CookieJar()
client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(jar))
token = ""

def request(path, method="GET", body=None, csrf=True, expected=200, content_type="application/json"):
    headers = {}
    if csrf and method != "GET": headers["RequestVerificationToken"] = token
    if body is not None:
        if not isinstance(body, bytes): body = json.dumps(body).encode()
        headers["Content-Type"] = content_type
    try:
        with client.open(urllib.request.Request(BASE + path, data=body, headers=headers, method=method), timeout=40) as result:
            status, data = result.status, result.read()
    except urllib.error.HTTPError as error:
        status, data = error.code, error.read()
    assert status == expected, (path, status, data.decode(errors="replace")[:1500])
    if data[:1] in (b"{", b"["): return json.loads(data)
    return data

def login(mode="admin"):
    global token
    html = request("/smoke-login?mode=" + mode, expected=403 if mode == "unrelated" else 200)
    if mode != "unrelated":
        text = html.decode(); assert "Migration Centre" in text
        token = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', text).group(1)

login("unrelated")
login("viewonly")
request("/api/migration/projects", "POST", {"name": "Denied", "sourceSystem": "SMOKE", "openingDate": "2026-10-01"}, expected=403)
login("prepareonly")
request("/api/migration/projects/1/scope", "PUT", {"kind": "Customer", "code": "C002", "requiredModules": ["CustomerOpenings", "ReceivedCheques"], "evidence": "Unapproved classification", "noLegacyOpening": True}, expected=403)
request("/api/migration/projects/1/approve", "POST", {"selection": {"modules": ["CustomerOpenings"]}, "hash": "none", "evidence": "denied"}, expected=403)
login()
request("/api/migration/projects", "POST", {"name": "No CSRF", "sourceSystem": "SMOKE", "openingDate": "2026-10-01"}, csrf=False, expected=400)
pack = zipfile.ZipFile(io.BytesIO(request("/api/migration/projects/templates")))
modules = [x[:-4] for x in pack.namelist() if x.endswith(".csv")]
assert len(modules) == 12 and "ControlTotals" in modules
assert request("/api/migration/projects") == [], "Use a fresh disposable harness database."
p = request("/api/migration/projects", "POST", {"name": "Synthetic complete migration", "sourceSystem": "SMOKE", "openingDate": "2026-10-01"})
pid = p["id"]; root = f"/api/migration/projects/{pid}"
lookup = request("/api/migration/projects/lookups")
accounts = {x["code"]: x["id"] for x in lookup["account"]}
settings = request(root)["settings"]
settings["openingEquityAccountId"] = accounts["OBE"]
settings["defaults"]["SafeAccount"] = accounts["SAFE"]
settings["mappings"]["BankBranch"] = {"7010-001": lookup["bankBranch"][0]["id"]}
for module in modules:
    if module not in ("CustomerOpenings", "ReceivedCheques", "TrialBalance"):
        settings["datasetStates"][module] = "NotApplicable"
        settings["zeroEvidence"][module] = "Synthetic independent signed zero schedule"
request(root + "/mappings", "PUT", settings)

def upload(module, csv):
    boundary = "NexusFlowSyntheticBoundary"
    fields = [("module", module), ("closingDate", "2026-09-30")]
    payload = b""
    for key, value in fields:
        payload += f'--{boundary}\r\nContent-Disposition: form-data; name="{key}"\r\n\r\n{value}\r\n'.encode()
    payload += f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="{module}.csv"\r\nContent-Type: text/csv\r\n\r\n'.encode() + csv.encode() + f'\r\n--{boundary}--\r\n'.encode()
    return request(root + "/upload", "POST", payload, content_type="multipart/form-data; boundary=" + boundary)

upload("CustomerOpenings", "LegacyRecordId,Code,Reference,Kind,DocumentDate,Amount\nAR,C001,OLD-INV,Invoice,2026-09-01,1000\nCR,C001,OLD-RET,ReturnCredit,2026-09-02,-150\n")
upload("ReceivedCheques", "LegacyRecordId,Code,Reference,ChequeNumber,ChequeDate,ReceivedDate,BankBranchCode,Amount,Status,PartyReflected,BankReflected\nCH,C001,OLD-R,000001,2026-10-10,2026-09-25,7010-001,200,InSafe,true,false\n")
assert request("/smoke-state")["journals"] == 0

def post(selected):
    preview = request(root + "/validate", "POST", selected)
    assert preview["canApprove"], preview
    request(root + "/approve", "POST", {"selection": selected, "hash": preview["hash"], "evidence": "Synthetic complete-position review; physical instruments checked"})
    request(root + "/execute", "POST", selected)
    for _ in range(50):
        run = request(root)["runs"][0]
        if run["status"] == "Completed": return
        assert run["status"] != "Failed", run
        time.sleep(.2)
    raise AssertionError("Posting did not complete")

selection = {"modules": ["CustomerOpenings", "ReceivedCheques"], "customers": ["C001"]}
post(selection)
controls = request(root + "/reconciliation/Customer/C001")
request(root + "/release/Customer/C001", "POST", {"evidence": "Independent source controls and full customer scope confirmed", "controls": {x["key"]: x["source"] for x in controls}, "rowCounts": {x["key"]: x["rows"] for x in controls}})
repeat = request(root + "/validate", "POST", selection)
assert not repeat["canApprove"] and all(x["status"] == "Already imported" for x in repeat["units"])
assert request("/api/migration/status")["provisional"]
request("/api/finance/customer-migration/posting-setting", "POST", {"enabled": True}, expected=410)
upload("TrialBalance", "Code,Debit,Credit\nAR,850,0\nSAFE,200,0\nBANK,0,0\nAP,0,0\nOBE,0,1000\nREV,0,50\n")
post({"modules": ["TrialBalance"]})
ledger = request(root + "/reconciliation")
assert all(x["difference"] == 0 for x in ledger)
request(root + "/finalize", "POST", {"evidence": "Synthetic final financial sign-off", "controls": {}, "rowCounts": {}})
assert not request("/api/migration/status")["provisional"]
request(root + "/mappings", "PUT", settings, expected=400)
assert request("/smoke-state")["journals"] == 3
print("PASS: Razor/DI, twelve templates, permissions, CSRF, retained staging, durable worker, credits, cheque separation, release, replay, legacy retirement, residual GL and final read-only state.")
