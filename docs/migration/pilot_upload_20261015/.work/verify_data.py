import csv,json,hashlib,re,zipfile
from pathlib import Path
from decimal import Decimal,ROUND_HALF_UP
from datetime import date,timedelta
from collections import defaultdict
P=Path("F:/source/NexusFlow_ERP/docs/migration/pilot_upload_20261015")
def read(m):
 with (P/"upload_csv"/(m+".csv")).open(encoding="utf-8-sig",newline="") as f:return list(csv.DictReader(f))
data={p.stem:read(p.stem) for p in (P/"upload_csv").glob("*.csv")}
D=lambda v:Decimal(v or "0")
M=lambda v:v.quantize(Decimal("0.01"),rounding=ROUND_HALF_UP)
expected={}
for kind,master,opening,cheque in [("Customer","CustomerMaster","CustomerOpenings","ReceivedCheques"),("Supplier","SupplierMaster","SupplierOpenings","IssuedCheques")]:
 for party in data[master]:
  code=party["Code"].upper()
  docs=[r for r in data[opening] if r["Code"].upper()==code]
  cheques=[r for r in data[cheque] if r["Code"].upper()==code]
  expected[(kind,code,opening+":Debit")]=(sum((max(D(r["Amount"]),D("0")) for r in docs),D("0")),len(docs))
  expected[(kind,code,opening+":Credit")]=(sum((max(-D(r["Amount"]),D("0")) for r in docs),D("0")),len(docs))
  expected[(kind,code,cheque)]=(sum((D(r["Amount"]) for r in cheques),D("0")),len(cheques))
for r in data["Stock"]:
 code=r["Code"].upper();wh=r["WarehouseCode"].upper()
 for suffix,amount in [("Quantity",D(r["Quantity"])*D(r["ConversionFactor"])),("Value",M(D(r["Quantity"])*D(r["UnitCost"])))]:
  key=("Stock",code,f"Stock:{wh}:{suffix}")
  a,n=expected.get(key,(D("0"),0));expected[key]=(a+amount,n+1)
for r in data["BankOpening"]:
 code=r["Code"].upper();recon=[x for x in data["BankRecon"] if x["Code"].upper()==code]
 issued=[x for x in data["IssuedCheques"] if x["BankAccountCode"].upper()==code]
 expected[("Bank",code,"BankOpening")]=(D(r["Amount"]),1)
 expected[("Bank",code,"BankRecon")]=(sum((D(x["Amount"]) for x in recon),D("0")),len(recon))
 bridge=D(r["StatementBalance"])+sum((D(x["Amount"]) for x in recon),D("0"))-sum((D(x["Amount"]) for x in issued),D("0"))
 assert bridge==D(r["Amount"])
 expected[("Bank",code,"BankStatementBridge")]=(D(r["Amount"]),len(recon)+len(issued))
controls=data["ControlTotals"]
assert len(expected)==len(controls)==5212
for c in controls:
 key=(c["Kind"],c["Code"].upper(),c["Component"])
 assert expected[key]==(D(c["Amount"]),int(c["RowCount"])),(key,expected[key],c)
for module in ["CustomerOpenings","SupplierOpenings"]:
 for r in data[module]:
  assert date.fromisoformat(r["DueDate"])==date.fromisoformat(r["DocumentDate"])+timedelta(days=30)
  assert date.fromisoformat(r["DocumentDate"])<=date(2026,9,30)
  assert r["Kind"]==("Invoice" if D(r["Amount"])>0 else "ReturnCredit") if module=="CustomerOpenings" else r["Kind"]==("Bill" if D(r["Amount"])>0 else "Credit")
for module,rows in data.items():
 for r in rows:
  assert r["ClosingDate"]=="2026-09-30"
  for key,v in r.items():
   if key.endswith("Date") and v:assert re.fullmatch(r"\d{4}-\d{2}-\d{2}",v)
customer_codes={r["Code"].upper() for r in data["CustomerMaster"]}
supplier_codes={r["Code"].upper() for r in data["SupplierMaster"]}
assert all(r["Code"].upper() in customer_codes for m in ["CustomerOpenings","ReceivedCheques"] for r in data[m])
assert all(r["Code"].upper() in supplier_codes for m in ["SupplierOpenings","IssuedCheques"] for r in data[m])
tb=data["TrialBalance"]
assert sum(D(r["Debit"]) for r in tb)==sum(D(r["Credit"]) for r in tb)==D("282960858.88")
assert sum(M(D(r["Quantity"])*D(r["UnitCost"])) for r in data["Stock"])==D("114417245.74")
assert all(r["WarehouseCode"]=="WH_MAIN" for r in data["Stock"])
assert len(data["Stock"])==938
assert all(r["Status"]=="InSafe" and r["PartyReflected"]=="true" and r["BankReflected"]=="false" for r in data["ReceivedCheques"])
# Upload worksheets must have no formula, macro, external link or typed date cells.
ns={"m":"http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
import xml.etree.ElementTree as ET
xlsx_counts={}
for f in (P/"review_workbooks").glob("*.xlsx"):
 with zipfile.ZipFile(f) as z:
  assert not z.testzip()
  assert not any("vbaProject" in n or "externalLinks/" in n for n in z.namelist())
  for n in z.namelist():
   if n.startswith("xl/worksheets/") and n.endswith(".xml"):
    doc=ET.fromstring(z.read(n))
    assert len(doc.findall(".//m:f",ns))==0
    assert len(doc.findall(".//m:c[@t='e']",ns))==0
    if f.stem!="Pilot_Review":assert len(doc.findall(".//m:c[@t='d']",ns))==0
  xlsx_counts[f.stem]=len([n for n in z.namelist() if re.fullmatch(r"xl/worksheets/sheet\d+\.xml",n)])
assert len(xlsx_counts)==13
report={"CheckedOn":"2026-10-15","ControlsRecomputed":len(expected),"ControlMismatches":0,"DatesAndThirtyDayTerms":"Pass","PartyReferences":"All matched to included masters","StockRows":938,"StockValue":"114417245.74","BankBridge":"100000.00 = 112500.00 + 2500.00 - 15000.00","TrialBalanceEachSide":"282960858.88","ValuesOnlyWorkbooks":xlsx_counts,"FormulaMacroDateCellErrors":0}
(P/"reports/Independent_Data_Checks.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
print(json.dumps(report,indent=2))
