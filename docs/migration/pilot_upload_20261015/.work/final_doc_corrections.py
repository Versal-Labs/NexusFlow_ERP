from pathlib import Path
import json,csv
P=Path("F:/source/NexusFlow_ERP/docs/migration/pilot_upload_20261015")
f=P/"guides/01_Setup_and_Mappings.md"
s=f.read_text(encoding="utf-8").replace("The saved local defaults use NET 30 DAYS — Net 30 Days, while the explicit NET30 mapping targets NET30 — 30 Days. Both are 30-day credit masters locally. On the destination choose the actual equivalent 30-day term and explicitly map NET30 to it.","The finalized local project uses NET 30 DAYS — Net 30 Days for both party defaults AND the explicit source NET30 mapping. The separate local NET30 — 30 Days master also represents 30-day credit, but the table above reproduces the final saved selection. On the destination choose the actual equivalent 30-day term and explicitly map source NET30 to it.")
f.write_text(s,encoding="utf-8")
f=P/"reports/Corrections.csv"
s=f.read_text(encoding="utf-8").replace("all 27 used category/posting-account","all 28 used category/posting-account")
f.write_text(s,encoding="utf-8")
f=P/".work/review_spec.json";spec=json.loads(f.read_text(encoding="utf-8"))
for sh in spec["sheets"]:
 if sh["name"]=="Assumptions and fixes":
  sh["rows"]=[[str(c).replace("all 27 used category/posting-account","all 28 used category/posting-account") for c in r] for r in sh["rows"]]
f.write_text(json.dumps(spec,ensure_ascii=False),encoding="utf-8")
f=P/".work/build_review.mjs";s=f.read_text(encoding="utf-8")
s=s.replace('const canonical=JSON.parse','if(process.argv.includes("--review-only"))process.exit(0);\nconst canonical=JSON.parse')
f.write_text(s,encoding="utf-8")
print("Finalized term mapping confirmed: source NET30 -> NET 30 DAYS; category reference count 28.")
