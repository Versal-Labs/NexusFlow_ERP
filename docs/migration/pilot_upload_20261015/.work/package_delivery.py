from pathlib import Path
import zipfile,csv,hashlib,json,re
P=Path("F:/source/NexusFlow_ERP/docs/migration/pilot_upload_20261015")
parent=P.parent
H=lambda b:hashlib.sha256(b).hexdigest().upper()
# Explicit delivery allowlist excludes private local database snapshots, scripts, logs and dependencies.
dirs=["stage_packages","upload_csv","review_workbooks","reports","source_archive","guides"]
files=[P/"README_START_HERE.md"]
for folder in dirs:
 files.extend(f for f in (P/folder).rglob("*") if f.is_file() and f.suffix.lower() in {".zip",".csv",".xlsx",".json",".md",".sql"} and not f.name.endswith(".inspect.ndjson"))
assert len(list((P/"stage_packages").glob("*.zip")))==7
assert len(list((P/"upload_csv").glob("*.csv")))==12
assert len(list((P/"review_workbooks").glob("*.xlsx")))==13
assert not any(".work" in f.relative_to(P).parts or "Settings_reference" in f.name for f in files)
# Final native XLSX cell check: text cheque numbers still include all leading zeroes.
ns={"m":"http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
import xml.etree.ElementTree as ET
with zipfile.ZipFile(P/"review_workbooks/IssuedCheques.xlsx") as z:
 root=ET.fromstring(z.read("xl/worksheets/sheet1.xml"))
 c=root.find(".//m:c[@r='D2']",ns);assert c is not None
 value=c.find("m:v",ns)
 if c.attrib.get("t")=="s":
  ss=ET.fromstring(z.read("xl/sharedStrings.xml"));value="".join(ss.findall("m:si",ns)[int(value.text)].itertext())
 elif c.attrib.get("t")=="inlineStr":value="".join(c.find("m:is",ns).itertext())
 else:value=value.text if value is not None else ""
 assert value=="000001",value
checksum=P/"Package_SHA256.csv"
with checksum.open("w",encoding="utf-8",newline="") as f:
 w=csv.writer(f);w.writerow(["Relative path","Bytes","SHA256"])
 for p in sorted(files):w.writerow([p.relative_to(P).as_posix(),p.stat().st_size,H(p.read_bytes())])
files.append(checksum)
full=parent/"NexusFlow_Pilot_Package_20261015.zip"
with zipfile.ZipFile(full,"w",zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for f in sorted(files):z.write(f,f.relative_to(P).as_posix())
with zipfile.ZipFile(full) as z:
 assert z.testzip() is None
 names=z.namelist();assert len(names)==len(files)==len(set(names))
 assert not any(".work/" in n or "node_modules/" in n or "local_successful_snapshot" in n or "Reset" in n for n in names)
 manifest=list(csv.DictReader(z.read("Package_SHA256.csv").decode("utf-8").splitlines()))
 for r in manifest:assert H(z.read(r["Relative path"]))==r["SHA256"]
 # Verify each upload ZIP against independent reader-verified source CSVs.
 for n in names:
  if n.startswith("stage_packages/"):
   import io
   with zipfile.ZipFile(io.BytesIO(z.read(n))) as inner:
    assert inner.testzip() is None
    m=json.loads(inner.read("manifest.json"))
    assert len(inner.namelist())==len(m["Files"])+1
    for entry in m["Files"]:assert inner.read(entry["File"])==z.read("upload_csv/"+entry["File"])
review=parent/"NexusFlow_Pilot_Review_Workbooks_20261015.zip"
with zipfile.ZipFile(review,"w",zipfile.ZIP_DEFLATED,compresslevel=9) as z:
 for f in sorted((P/"review_workbooks").glob("*.xlsx")):z.write(f,f.name)
with zipfile.ZipFile(review) as z:assert z.testzip() is None
summary={"FullPackage":str(full),"Bytes":full.stat().st_size,"SHA256":H(full.read_bytes()),"DeliveredFiles":len(files),"ExcelWorkbooks":13,"IndividualCSVs":12,"StageZIPs":7,"ReviewOnlyZIP":str(review),"Integrity":"All delivered hashes, CRCs, nested manifests and payloads pass"}
(P/".work/package_result.json").write_text(json.dumps(summary,indent=2),encoding="utf-8")
print(json.dumps(summary,indent=2))
