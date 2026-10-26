import csv,json,hashlib,shutil,zipfile,re
from pathlib import Path
from decimal import Decimal,ROUND_HALF_UP
from collections import Counter,defaultdict

REPO=Path("F:/source/NexusFlow_ERP")
OUT=REPO/"docs/migration/pilot_upload_20261015"
OLD=REPO/"docs/migration/uat_upload_20261012"
WORK=OUT/".work"
x=json.loads((WORK/"local_successful_snapshot.json").read_text(encoding="utf-8"),parse_float=Decimal)
p=x["Project"][0]; settings=json.loads(p["SettingsJson"])
assert p["Id"]==4 and p["Status"]=="Finalized" and p["Version"]==2065
for d in ["upload_csv","review_workbooks","reports","guides","stage_packages","source_archive"]:
    (OUT/d).mkdir(parents=True,exist_ok=True)
def h(path):return hashlib.sha256(path.read_bytes()).hexdigest().upper()
def read(path):return list(csv.DictReader(path.open(encoding="utf-8-sig",newline="")))
def write(name,headers,rows):
    path=OUT/name
    with path.open("w",encoding="utf-8",newline="") as f:
        w=csv.writer(f);w.writerow(headers);w.writerows(rows)
def report(name,rows):
    assert rows
    write("reports/"+name,list(rows[0]),[list(r.values()) for r in rows])
def dec(v):return Decimal(str(v or "0"))
def money(v):return str(dec(v).quantize(Decimal(".01"),rounding=ROUND_HALF_UP))
accounts={a["Id"]:a for a in x["Accounts"]}
account_codes={a["Code"]:a for a in x["Accounts"]}
variants={v["Id"]:v for v in x["Variants"]}
lookups={l["Id"]:l for l in x["Lookups"]}
categories={c["Id"]:c for c in x["Categories"]}
warehouses={w["Id"]:w for w in x["Warehouses"]}
units={u["Id"]:u for u in x["UnitsOfMeasure"]}
brands={b["Id"]:b for b in x["Brands"]}
branches={b["Id"]:b for b in x["BankBranches"]}
catalog={}
source=(REPO/"NexusFlow.AppCore/Features/Migration/MigrationModels.cs").read_text(encoding="utf-8-sig")
for name,fields in re.findall(r'\["(\w+)"\]\s*=\s*\[([^\]]+)\]',source):
    catalog[name]=re.findall(r'"([^"]+)"',fields)+["ClosingDate"]
assert len(catalog)==12
data={}; baseline=[]
for ds in x["Datasets"]:
    module=ds["Module"]; src=OLD/"upload_csv"/(module+".csv")
    assert h(src)==ds["FileHash"],module+" source is not the successful upload"
    rows=read(src); stored=json.loads(ds["RowsJson"])
    assert len(rows)==len(stored)
    with src.open(encoding="utf-8-sig",newline="") as f:assert next(csv.reader(f))==catalog[module],module+" headers"
    for a,b in zip(rows,stored):
        assert all(a.get(k,"").strip()==str(b["Data"].get(k,"")).strip() for k in catalog[module]),module
    shutil.copyfile(src,OUT/"upload_csv"/src.name)
    shutil.copyfile(OLD/"review_workbooks"/(module+".xlsx"),OUT/"review_workbooks"/(module+".xlsx"))
    data[module]=rows
    baseline.append({"Module":module,"Rows":len(rows),"Successful local CSV SHA256":ds["FileHash"],
        "Pilot CSV SHA256":h(OUT/"upload_csv"/src.name),"Byte identical":"Yes","Local dataset revision":ds["Revision"]})
assert len(data)==12
report("Successful_Local_Baseline.csv",baseline)
mapping=[]
for typ,entries in settings["Mappings"].items():
    for legacy,ident in entries.items():
        if typ=="Account": t=accounts[ident]; code=t["Code"]; label=t["Name"]
        elif typ=="Warehouse": t=warehouses[ident];code=t["Code"];label=t["Name"]
        elif typ=="Unit":t=units[ident];code=t["Symbol"];label=t["Name"]
        elif typ=="Category":t=categories[ident];code=t["Code"];label=t["Name"]
        elif typ=="Brand":t=brands[ident];code=t["Name"];label=t["Name"]
        elif typ=="Stock":t=variants[ident];code=t["SKU"];label=t["Name"]
        elif typ=="BankBranch":
            t=branches[ident];code=t["BankCode"]+"-"+t["BranchCode"];label=t["BankName"]+" / "+t["BranchName"]
        else:t=lookups[ident];code=t["Code"];label=t["Value"]
        basis="Required explicit mapping from finalized local project"
        if typ=="Stock":basis="Existing SKU case alias; map to the existing SKU to avoid attempted duplicate creation"
        if typ=="BankBranch" and legacy!=code:basis="Pilot branch substitute retained from successful local test; not physical branch verification"
        mapping.append({"Mapping type":typ,"Exact legacy code":legacy,"Target master code":code,"Target label":label,
                        "When required":"When this existing SKU is present" if typ=="Stock" else "Before validation","Reason":basis})
# Additional category mappings make the entire Stock file portable if any SKU is missing.
existing_maps={(m["Mapping type"],m["Exact legacy code"]) for m in mapping}
stocksetup=[]; posting=defaultdict(lambda:{"rows":0,"qty":Decimal(0),"value":Decimal(0)})
posted_stock={u["ScopeKey"]:json.loads(u["RowsJson"])[0]["Data"] for u in x["Units"] if u["Module"]=="Stock"}
for r in data["Stock"]:
    code=r["Code"].strip().upper()
    if code in settings["Mappings"].get("Stock",{}):v=variants[settings["Mappings"]["Stock"][code]]
    else:
        matches=[z for z in x["Variants"] if z["SKU"]==code]
        assert len(matches)==1,(code,matches)
        v=matches[0]
    posted=posted_stock[code]
    inv=accounts[int(posted["_InventoryId"])]
    assert inv["Code"]==r["InventoryAccountCode"] and inv["Type"]==1 and inv["IsActive"] and inv["IsTransactionAccount"]
    assert v["InventoryAccountCode"]==inv["Code"] and r["CategoryCode"].upper()==v["CategoryCode"].upper()
    assert v["Type"]==({"RawMaterial":1,"FinishedGood":2}[r["ProductType"]])
    assert v["UnitCode"].upper()==r["UnitCode"].upper()
    stocksetup.append({"Legacy stock code":r["Code"],"Scope code":code,"Existing target SKU":v["SKU"],
        "Explicit SKU mapping":"Yes" if code in settings["Mappings"].get("Stock",{}) else "No",
        "Product type":r["ProductType"],"Category code":v["CategoryCode"],"Category name":v["CategoryName"],
        "Inventory account":inv["Code"],"Warehouse":r["WarehouseCode"],"Unit":v["UnitCode"],
        "Brand":v["BrandCode"],"Quantity":r["Quantity"],"Unit cost":r["UnitCost"],"Layer date":r["LayerDate"]})
    key=r["CategoryCode"].upper()
    if ("Category",key) not in existing_maps:
        mapping.append({"Mapping type":"Category","Exact legacy code":key,"Target master code":v["CategoryCode"],
            "Target label":v["CategoryName"],"When required":"If creating a missing SKU; recommended for all categories",
            "Reason":"Complete file coverage; category inventory account must be "+inv["Code"]})
        existing_maps.add(("Category",key))
    z=posting[(r["ProductType"],v["CategoryCode"],inv["Code"])]
    z["rows"]+=1;z["qty"]+=dec(r["Quantity"])
    z["value"]+=(dec(r["Quantity"])*dec(r["UnitCost"])).quantize(Decimal(".01"),rounding=ROUND_HALF_UP)
mapping.sort(key=lambda z:(z["Mapping type"],z["Exact legacy code"]))
report("Mappings_By_Code.csv",mapping)
aliases=[m for m in mapping if m["Mapping type"]=="Stock"]
assert len(aliases)==33
report("Required_SKU_Mappings_33.csv",aliases)
report("Stock_Setup_Reference.csv",stocksetup)
categoryrows=[]
for (typ,cat,ac),z in sorted(posting.items()):
    categoryrows.append({"Product type":typ,"Category code":cat,"Expected inventory account":ac,"Rows":z["rows"],
        "Quantity":str(z["qty"]),"Value LKR":money(z["value"]),"WH_MAIN inventory override":"Blank for this pilot"})
report("Category_Inventory_Setup.csv",categoryrows)
defaultnames={"ReceivableAccount":"Default customer control","RevenueAccount":"Default customer revenue",
"PayableAccount":"Default supplier control","ExpenseAccount":"Default supplier expense",
"SafeAccount":"Undeposited cheque account","CustomerGroup":"Default customer group","SupplierGroup":"Default supplier group",
"PriceLevel":"Default price level","CustomerPaymentTerm":"Default customer payment term","SupplierPaymentTerm":"Default supplier payment term",
"StockUnit":"Approved base unit when source unit is missing","StockWarehouse":"Approved warehouse when source location is missing"}
defaults=[{"UI field":"Opening equity account","Target code":accounts[settings["OpeningEquityAccountId"]]["Code"],
           "Target label":accounts[settings["OpeningEquityAccountId"]]["Name"],"Pilot note":"Active Equity posting account; do not select a folder"}]
for k,ident in settings["Defaults"].items():
    if k.endswith("Account"):t=accounts[ident];code=t["Code"];label=t["Name"]
    elif k=="StockUnit":t=units[ident];code=t["Symbol"];label=t["Name"]
    elif k=="StockWarehouse":t=warehouses[ident];code=t["Code"];label=t["Name"]
    else:t=lookups[ident];code=t["Code"];label=t["Value"]
    note="Saved successful local default; select the destination master by code/name"
    if "PaymentTerm" in k:note="30-day credit. NET30 and NET 30 DAYS are both 30-day masters locally; never choose COD"
    if k=="ExpenseAccount":note="Temporary pilot expense default; explicit supplier row accounts remain authoritative"
    defaults.append({"UI field":defaultnames[k],"Target code":code,"Target label":label,"Pilot note":note})
defaults += [
{"UI field":"Approved supplier terms in days","Target code":"30","Target label":"30 calendar days","Pilot note":"Original document date plus 30"},
{"UI field":"Summarized stock layer approval","Target code":"","Target label":settings["SummarizedStockEvidence"],"Pilot note":"Retained pilot fixture approval"},
{"UI field":"Legacy finished goods valuation","Target code":"AverageCost","Target label":"Average cost (avar x warehouse quantity)","Pilot note":"Prepared costs already populated"},
{"UI field":"Valuation discrepancy resolution evidence","Target code":"","Target label":settings["FinishedGoodsValuationEvidence"],"Pilot note":"No live financial sign-off implied"}]
report("Defaults_By_Code.csv",defaults)
gl=defaultdict(lambda:{"debit":Decimal(0),"credit":Decimal(0)})
for l in x["PostedLines"]:
    gl[l["AccountCode"]]["debit"]+=dec(l["Debit"]);gl[l["AccountCode"]]["credit"]+=dec(l["Credit"])
expectedgl=[]
for r in data["TrialBalance"]:
    bal=dec(r["Debit"])-dec(r["Credit"])
    assert gl[r["Code"]]["debit"]-gl[r["Code"]]["credit"]==bal,r["Code"]
    a=account_codes[r["Code"]]
    expectedgl.append({"Account code":r["Code"],"Account name":a["Name"],"Closing debit":r["Debit"],"Closing credit":r["Credit"],
        "Expected balance":money(bal),"Opening journal debit":money(gl[r["Code"]]["debit"]),
        "Opening journal credit":money(gl[r["Code"]]["credit"]),"Difference":"0.00"})
report("Expected_GL.csv",expectedgl)
identities=[]; reconciliation=[];allchecks=[]
for module,rows in data.items():
    seen=Counter()
    for i,r in enumerate(rows,2):
        code=r.get("Code","").strip().upper()
        identity=r.get("LegacyRecordId","").strip()
        if not identity:
            if module=="ControlTotals":v=[r["Kind"],code,r["Component"]]
            elif module.endswith("Master") or module in ["Accounts","BankOpening","TrialBalance"]:v=code
            elif module=="Stock":v=[code,r["WarehouseCode"],r["Batch"],r["LayerDate"]]
            else:v=[code,r.get("Kind",""),r["Reference"],r["ChequeDate"] if module=="ReceivedCheques" else r["DocumentDate"],r.get("ChequeNumber","")]
            identity=json.dumps(v,ensure_ascii=False,separators=(",",":")) if not isinstance(v,str) else v
        seen[identity]+=1
        identities.append({"Module":module,"Source row (trace only)":i,"Code":r.get("Code",""),"Stable identity":identity,
                           "Identity basis":"Permanent legacy/fixture ID" if r.get("LegacyRecordId","") else "Importer-supported code/composite"})
        assert r["ClosingDate"]=="2026-09-30"
        for key,value in r.items():
            if key.endswith("Date") and value:assert re.fullmatch(r"\d{4}-\d{2}-\d{2}",value),(module,key,value)
    duplicates=sum(n-1 for n in seen.values() if n>1);assert duplicates==0,module
    positive=negative=Decimal(0)
    if module in ["CustomerOpenings","SupplierOpenings","ReceivedCheques","IssuedCheques","BankOpening","BankRecon"]:
        positive=sum((dec(r["Amount"]) for r in rows if dec(r["Amount"])>0),Decimal(0))
        negative=sum((dec(r["Amount"]) for r in rows if dec(r["Amount"])<0),Decimal(0))
    if module=="Stock":
        positive=sum(((dec(r["Quantity"])*dec(r["UnitCost"])).quantize(Decimal(".01"),rounding=ROUND_HALF_UP) for r in rows),Decimal(0))
    reconciliation.append({"Module":module,"Successful local rows":len(rows),"Pilot CSV rows":len(rows),
        "Positive LKR":money(positive),"Negative LKR":money(negative),"Signed net/value LKR":money(positive+negative),
        "Duplicate stable identities":duplicates,"Unmatched codes against finalized local baseline":0,"Azure validation":"Required"})
report("Stable_Identities.csv",identities)
report("Reconciliation.csv",reconciliation)
contacts=[]
for module in ["CustomerMaster","SupplierMaster"]:
    for r in data[module]:
        missing=[k for k in ["Contact","Email","Phone","Address1","TaxNumber"] if not r.get(k,"")]
        if missing:contacts.append({"Module":module,"Code":r["Code"],"Name":r["Name"],"Missing fields":", ".join(missing),"Action":"Pilot permitted; complete genuine details later"})
report("Missing_Contact_Details.csv",contacts)
fixes=[
{"Issue":"Stock SDASF/Accessories validation","Correction":"Add Category ACCESSORIES to Accessories; verify category inventory 1131, active Asset posting account","Status":"Guide/setup corrected; target selection required"},
{"Issue":"Existing lower-case SKU lookup","Correction":"Add all 33 Stock alias mappings from the successful local project, including SDASF to sdasf","Status":"Complete mapping list included"},
{"Issue":"RM/FG inventory account confusion","Correction":"Raw materials 1131; finished goods 1132. Keep WH_MAIN override blank for this two-account fixture","Status":"938 rows checked against actual posted local units"},
{"Issue":"Missing category definitions for new SKUs","Correction":"Include all 27 used category/posting-account references and optional mappings","Status":"Complete setup reference included"},
{"Issue":"COD versus 30-day credit","Correction":"NET30 mapping and actual 30-day default selections listed by code/name; no database IDs","Status":"Guide corrected"},
{"Issue":"Old mapping JSON contains local IDs","Correction":"Use portable code/name reports; no local SettingsJson included as an upload","Status":"Corrected delivery"},
{"Issue":"Slow full-package troubleshooting","Correction":"Seven dependency-ordered upload ZIPs with separate stock, controls and TB","Status":"Corrected packaging"},
{"Issue":"Comma-containing SKU codes","Correction":"Leave filters blank; enter the literal code in Exact legacy code. CSV quoting preserves commas","Status":"Guide corrected"},
{"Issue":"Stock composite identity warning","Correction":"Retain supported Code+Warehouse+Batch+LayerDate identity; no row-number IDs. Zero duplicates verified","Status":"Warning explained; no fabricated IDs"},
{"Issue":"Release delay and dashboard zero balances","Correction":"Use the current build containing the 15 October performance/dashboard fixes; files do not install software changes","Status":"Source fixes already exist; deployed build verification required"}]
report("Corrections.csv",fixes)
assumptions=[
{"Topic":"Purpose","Pilot treatment":"User-authorized replay of the successful local functional test","Live cutover requirement":"Replace fixtures with certified closing exports before live financial sign-off"},
{"Topic":"Snapshot dates","Pilot treatment":"Test closing 2026-09-30 retained; original older report dates remain in archive","Live cutover requirement":"Fresh common-date closing position"},
{"Topic":"Bank book/statement","Pilot treatment":"1112: book 100000.00, statement 112500.00","Live cutover requirement":"Actual bank book and statement schedule"},
{"Topic":"Issued cheque","Pilot treatment":"AH143 / UAT-ISSUED-001 / cheque 000001 / 15000.00; both reflection flags true","Live cutover requirement":"Actual pending issued instruments"},
{"Topic":"Bank reconciliation","Pilot treatment":"UAT-DEPOSIT-001 +2500.00; statement bridge 112500+2500-15000=100000","Live cutover requirement":"Actual uncleared positions without duplicate cheques"},
{"Topic":"Trial balance","Pilot treatment":"39-account synthetic balanced TB including rent debit/sales credit residual 1000000.00","Live cutover requirement":"Correct complete actual TB; original supplied TB remains unbalanced by 613120.00"},
{"Topic":"Branch substitutions","Pilot treatment":"14 source bank/branch codes point to documented same-bank pilot substitutes","Live cutover requirement":"Actual verified branches"},
{"Topic":"Missing master fixtures","Pilot treatment":"Four customer and two supplier UAT names retained with original legacy codes","Live cutover requirement":"Genuine master details"},
{"Topic":"Stock classification","Pilot treatment":"81 new FG rows retain MSH/UNBRANDED/pcs fixture classification; original quantities/costs unchanged","Live cutover requirement":"Approved real product classification and unit conversion"},
{"Topic":"Supplier expenses","Pilot treatment":"5110 temporary expense except explicit rent 6210/electricity 6220","Live cutover requirement":"Business-approved supplier expense assignment"},
{"Topic":"Supplier dates","Pilot treatment":"Four post-closing supplier dates retain the earlier UAT adjustment","Live cutover requirement":"Certified correct-date outstanding export"},
{"Topic":"Operational use","Pilot treatment":"Keep pilot transactions distinguishable; retain a pre-pilot recovery point","Live cutover requirement":"Reviewed cleanup/rebuild and real-data reconciliation before accepting live balances"}]
report("Pilot_Assumptions.csv",assumptions)
tests=[
["P01","Setup","Fresh pilot target with settings retained","Run target preflight; confirm no prior financial/stock activity","Clean target and required dictionaries","","","",""],
["P02","Masters","Mappings saved","Import stage 01; inspect terms/groups and a leading-zero code","39 accounts, 896 customers, 215 suppliers; no financial journal","","","",""],
["P03","Openings","Masters imported","Import stage 02; inspect positive and negative documents","AR 162114985.14; AP 94190250.55; 1150/180 documents","","","",""],
["P04","Cheques","Party masters imported","Import stage 03; inspect cheque 000001 and received status","49 InSafe received cheques 5328628.00; one issued 15000; no second AR/AP payment","","","",""],
["P05","Stock","All 33 SKU aliases and category/account setup checked","Import stage 04 once; inspect SDASF and one FG SKU","938 layers; RM 1131 29727743.70; FG 1132 84689502.04","","","",""],
["P06","Bank","1112 available","Import stage 05; compare book and statement bridge","100000=112500+2500-15000","","","",""],
["P07","Release","All opening modules complete","Import stage 06; select four scope kinds; release","5212 controls; 2050 scopes Released; zero Pending","","","",""],
["P08","Trial balance","Every scope released","Import stage 07; Check Full Ledger","39 account differences zero; TB debits/credits 282960858.88","","","",""],
["P09","Dashboard","Updated application build; no pilot operations yet","Refresh Executive Dashboard","AR 162114985.14; AP 94190250.55; stock114417245.74; cash/bank100000","","","",""],
["P10","Idempotency","Unchanged imports before finalization","Validate an already imported selection","Already imported; no duplicate opening","","","",""],
["P11","Finalization","Complete baseline and zero differences","Enter pilot acceptance evidence; Finalize Migration","Finalized/read-only; ordinary released-entity workflows available","","","",""],
["P12","Receipt","Final baseline saved; normal treasury permission","Receive a partial payment against a positive opening Invoice","Outstanding reduces by payment; receipt posts once","","","",""],
["P13","Supplier payment","Released supplier","Pay a positive opening Bill; inspect credit separately","AP reduces correctly; no recreated historical bill","","","",""],
["P14","Cheque deposit","InSafe instrument and released customer/bank","Deposit one imported cheque","Safe decreases; bank changes once; AR not reduced again","","","",""],
["P15","Stock operation","Released SKU","Run a normal issue/sale/receipt","On-hand/FIFO and inventory journals update correctly","","","",""]]
write("reports/Pilot_Acceptance_Tests.csv",["Test","Stage","Precondition","Steps","Expected result","Pass / Fail","Tester","Date","Evidence"],tests)
stages=[
("01_Masters.zip",["Accounts","CustomerMaster","SupplierMaster"]),
("02_Party_Openings.zip",["CustomerOpenings","SupplierOpenings"]),
("03_Cheques.zip",["ReceivedCheques","IssuedCheques"]),
("04_Combined_Stock.zip",["Stock"]),
("05_Banks.zip",["BankOpening","BankRecon"]),
("06_Control_Totals.zip",["ControlTotals"]),
("07_Trial_Balance.zip",["TrialBalance"])]
for filename,modules in stages:
    manifest={"Version":"1.0","SourceSystem":p["SourceSystem"],"ClosingDate":"2026-09-30",
              "Files":[{"Module":m,"File":m+".csv"} for m in modules]}
    with zipfile.ZipFile(OUT/"stage_packages"/filename,"w",zipfile.ZIP_DEFLATED) as z:
        z.writestr("manifest.json",json.dumps(manifest,indent=2))
        for m in modules:z.write(OUT/"upload_csv"/(m+".csv"),m+".csv")
    assert (OUT/"stage_packages"/filename).stat().st_size<20*1024*1024
for filename in ["Legacy_Originals.zip","source_manifest.json"]:
    shutil.copyfile(OLD/"source_archive"/filename,OUT/"source_archive"/filename)
    assert h(OLD/"source_archive"/filename)==h(OUT/"source_archive"/filename)
for filename in ["UAT_changes.csv","Source_to_UAT_reconciliation.csv","Source_column_mappings.csv"]:
    shutil.copyfile(OLD/"reports"/filename,OUT/"reports"/("Historical_"+filename))
(OUT/"source_archive/README.md").write_text(
"# Original source evidence\n\nThese originals are unchanged from the previous preparation. Pilot dates and synthetic schedules do not certify their original snapshot dates. The supplied snapshots are not a complete transaction-history archive.\n",encoding="utf-8")
summary={"SourceSystem":p["SourceSystem"],"OpeningDate":"2026-10-01","ClosingDate":"2026-09-30","LocalProjectId":4,
"LocalProjectVersion":2065,"LocalStatus":p["Status"],"PostedUnits":len(x["Units"]),"Scopes":2050,"Rows":{m:len(rs) for m,rs in data.items()},
"RequiredSkuAliases":33,"StockCategories":len(categoryrows),"NetAR":"162114985.14","NetAP":"94190250.55",
"StockValue":"114417245.74","RawStockValue":"29727743.70","FinishedStockValue":"84689502.04","Safe":"5328628.00",
"CashBank":"100000.00","TrialBalanceDebit":"282960858.88","TrialBalanceCredit":"282960858.88",
"MissingContactRows":len(contacts),"MappingRows":len(mapping),"UploadStages":[[n,ms] for n,ms in stages]}
(WORK/"summary.json").write_text(json.dumps(summary,indent=2),encoding="utf-8")
# One new review workbook; the 12 canonical data workbooks are preserved byte-identical.
review={"sheets":[
{"name":"Pilot overview","headers":["Measure","Value","Meaning"],"rows":[
["Purpose","Client pilot","Same tested CSV inputs; functional acceptance only"],
["Opening date","2026-10-01","Test opening date"],["Closing date","2026-09-30","Pilot metadata; original dates archived"],
["Source identifier",p["SourceSystem"],"Use this exact value for all stage ZIPs"],
["Local baseline","Finalized project 4 / version 2065","2455 posted units; 2050 released scopes"],
["Net receivables",162114985.14,"Customer invoices and negative credits"],
["Net payables",94190250.55,"Supplier bills and negative credits"],
["Stock value",114417245.74,"938 combined FG/RM rows"],
["Raw materials",29727743.70,"Inventory account 1131"],
["Finished goods",84689502.04,"Inventory account 1132"],
["Cheques in hand",5328628.00,"49 received instruments; separate from AR"],
["Cash and bank",100000,"Synthetic pilot bank fixture"],
["Trial balance each side",282960858.88,"Synthetic pilot TB"],
["Master mapping fixes","ACCESSORIES plus 33 SKU aliases","Missing from the older guide"],
["Files unchanged",12,"All CSV hashes equal the successful local upload"],
["Azure pilot status","Validation required","Prepared files have not been uploaded or executed here"]]},
{"name":"Mappings","headers":list(mapping[0]),"rows":[list(r.values()) for r in mapping]},
{"name":"Defaults","headers":list(defaults[0]),"rows":[list(r.values()) for r in defaults]},
{"name":"Stock posting","headers":list(categoryrows[0]),"rows":[list(r.values()) for r in categoryrows]},
{"name":"Expected GL","headers":list(expectedgl[0]),"rows":[list(r.values()) for r in expectedgl]},
{"name":"Reconciliation","headers":list(reconciliation[0]),"rows":[list(r.values()) for r in reconciliation]},
{"name":"Pilot tests","headers":["Test","Stage","Precondition","Steps","Expected result","Pass / Fail","Tester","Date","Evidence"],"rows":tests},
{"name":"Assumptions and fixes","headers":["Type","Topic","Treatment","Status or live requirement"],
 "rows":[["Pilot assumption",r["Topic"],r["Pilot treatment"],r["Live cutover requirement"]] for r in assumptions]+
        [["Correction",r["Issue"],r["Correction"],r["Status"]] for r in fixes]}]}
(WORK/"review_spec.json").write_text(json.dumps(review,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps(summary,indent=2))
