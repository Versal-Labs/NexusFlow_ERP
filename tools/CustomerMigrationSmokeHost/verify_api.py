"""Run only against the opt-in disposable loopback smoke host, never a live ERP."""
import html
import http.cookiejar
import json
import re
import urllib.error
import urllib.request
import uuid

BASE = 'http://127.0.0.1:5190'
ROOT = '/api/finance/customer-migration'

def client():
    return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

def call(opener, path, body=None, headers=None, expected=200):
    request = urllib.request.Request(BASE + path, data=body, headers=headers or {})
    try:
        response = opener.open(request, timeout=60)
    except urllib.error.HTTPError as error:
        response = error
    data = response.read()
    assert response.code == expected, (path, response.code, data[:600])
    return data

def post(opener, path, data, token=None, expected=200):
    headers = {'Content-Type': 'application/json'}
    if token:
        headers['RequestVerificationToken'] = token
    return json.loads(call(opener, ROOT + path, json.dumps(data).encode(), headers, expected))

opener = client()
page = call(opener, '/smoke-login').decode()
token = html.unescape(re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', page).group(1))
setup = json.loads(call(opener, ROOT + '/setup'))
assert {'postingEnabled', 'signedOff', 'branches', 'lookups', 'accounts', 'salesReps'} <= setup.keys()
assert json.loads(call(opener, ROOT + '/batches')) == []
call(opener, ROOT + '/masters/template')
for mode in ['viewonly', 'financeonly', 'treasuryonly']:
    restricted = client()
    call(restricted, '/smoke-login?mode=' + mode, expected=403)
    call(restricted, ROOT + '/setup', expected=403)
call(opener, ROOT + '/posting-setting', b'{"enabled":true,"uatApproved":true}', {'Content-Type': 'application/json'}, 400)
call(opener, ROOT + '/posting-setting', b'{"enabled":true,"uatApproved":true}', {'Content-Type': 'application/json', 'Authorization': 'Bearer invalid'}, 400)
post(opener, '/posting-setting', {'enabled': True, 'uatApproved': False}, token, 400)
post(opener, '/posting-setting', {'enabled': True, 'uatApproved': True}, token)

boundary = 'migration-' + uuid.uuid4().hex
parts = []
for name, value in [('CutoverDate', '2026-10-01'), ('SourceAsOfDate', '2026-09-30')]:
    parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())
for name, kind in [('Outstanding', 'outstanding'), ('Cheques', 'cheques')]:
    data = call(opener, '/smoke-fixture/' + kind)
    parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"; filename="{kind}.xlsx"\r\nContent-Type: application/octet-stream\r\n\r\n'.encode() + data + b'\r\n')
parts.append(f'--{boundary}--\r\n'.encode())
headers = {'Content-Type': 'multipart/form-data; boundary=' + boundary, 'RequestVerificationToken': token}
review = json.loads(call(opener, ROOT + '/preview', b''.join(parts), headers))
assert review['canPost'], review['errors']
batch = '/' + str(review['batchId'])
post(opener, batch + '/post', {'reviewHash': review['reviewHash'], 'physicallyVerified': False}, token, 400)
post(opener, batch + '/post', {'reviewHash': 'stale', 'physicallyVerified': True}, token, 400)
post(opener, batch + '/post', {'reviewHash': review['reviewHash'], 'physicallyVerified': True}, token)
post(opener, batch + '/post', {'reviewHash': review['reviewHash'], 'physicallyVerified': True}, token)
state = json.loads(call(opener, '/smoke-state'))
assert state == {'items': 4, 'cheques': 2, 'journals': 1, 'receipts': 0}, state
reconciliation = json.loads(call(opener, ROOT + batch + '/reconciliation'))
assert reconciliation['matches']
call(opener, ROOT + batch + '/audit.csv')
call(opener, ROOT + batch + '/source/outstanding')
call(opener, ROOT + '/masters/template')
signed = post(opener, batch + '/sign-off', {}, token)
assert signed['isSignedOff']
post(opener, '/posting-setting', {'enabled': True, 'uatApproved': True}, token, 400)
post(opener, batch + '/reverse', {'date': '2026-10-01', 'reason': 'Test reversal'}, token, 400)
print('PASS: page setup/controller activation, permissions, CSRF including spoofed Authorization, UAT gate, multipart upload, physical check, stale review, idempotency, reconciliation, downloads, sign-off lock.')
print('Synthetic state:', state)
