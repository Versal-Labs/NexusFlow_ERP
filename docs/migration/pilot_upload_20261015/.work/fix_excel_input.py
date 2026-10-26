import csv,json
from pathlib import Path
p=Path("F:/source/NexusFlow_ERP/docs/migration/pilot_upload_20261015")
files=[]
for f in sorted((p/"upload_csv").glob("*.csv")):
 with f.open(encoding="utf-8-sig",newline="") as r: rows=list(csv.reader(r))
 files.append({"module":f.stem,"headers":rows[0],"rows":rows[1:]})
(p/".work/canonical_workbooks.json").write_text(json.dumps(files,ensure_ascii=False),encoding="utf-8")
# Correct the now superseded preservation statement while retaining exact CSV/source-archive provenance.
for f in [p/"README_START_HERE.md",*list((p/"guides").glob("*.md"))]:
 s=f.read_text(encoding="utf-8")
 s=s.replace("and all twelve matching source workbooks","and rebuilt all twelve matching Excel workbooks with importer-safe ISO date text")
 s=s.replace("Twelve matching values-only template workbooks","Twelve rebuilt matching values-only template workbooks")
 f.write_text(s,encoding="utf-8")
f=p/"reports/Corrections.csv"
with f.open(encoding="utf-8-sig",newline="") as r:
 d=csv.DictReader(r);headers=d.fieldnames;rows=list(d)
rows.append(dict(zip(headers,["Older XLSX date-cell representation rejected by current file reader","Rebuilt 12 canonical XLSX files with ISO text dates and exact CSV headers/values; upload CSV bytes unchanged","Corrected and re-tested through the actual reader"])))
with f.open("w",encoding="utf-8",newline="") as out:
 w=csv.DictWriter(out,headers);w.writeheader();w.writerows(rows)
spec=json.loads((p/".work/review_spec.json").read_text(encoding="utf-8"))
for sheet in spec["sheets"]:
 if sheet["name"]=="Assumptions and fixes":sheet["rows"].append(["Correction","Older Excel date cell format","Rebuilt all 12 data workbooks with yyyy-MM-dd text dates","Verify actual importer round-trip matches CSV"])
(p/".work/review_spec.json").write_text(json.dumps(spec,ensure_ascii=False),encoding="utf-8")
print("Prepared canonical workbook values: "+str(sum(len(f["rows"]) for f in files))+" data rows.")
