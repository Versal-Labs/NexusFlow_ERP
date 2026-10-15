import csv,hashlib,json,shutil,zipfile,stat,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];REPO=ROOT.parents[2];OLD=REPO/'docs/migration/preparation_20261011'
def rd(p):return list(csv.DictReader(p.open(encoding='utf-8-sig',newline='')))
def write(p,h,rows):
    with p.open('w',encoding='utf-8',newline='') as f:
        w=csv.writer(f);w.writerow(h);w.writerows(rows)
summary=json.loads((ROOT/'.work/summary.json').read_text(encoding='utf-8'))
spec=json.loads((ROOT/'.work/workbooks.json').read_text(encoding='utf-8'))
verification=json.loads((ROOT/'.work/verification.json').read_text(encoding='utf-8'));assert verification['checked_workbooks']==13
archive=ROOT/'source_archive';archive.mkdir(exist_ok=True)
for name in ['Legacy_Originals.zip','source_manifest.json']:
    dst=archive/name
    if dst.exists():dst.chmod(stat.S_IWRITE|stat.S_IREAD)
    shutil.copyfile(OLD/'archive'/name,dst)
    assert hashlib.sha256(dst.read_bytes()).digest()==hashlib.sha256((OLD/'archive'/name).read_bytes()).digest()
    dst.chmod(stat.S_IREAD)
(archive/'README.md').write_text('# Preserved source snapshots\n\nLegacy_Originals.zip and source_manifest.json are byte-identical read-only copies of the original preparation archive. Ten supplied Excel/CSV files are included. This archive contains the supplied snapshots, not a complete pre-cutover transaction history. The UAT ClosingDate does not change their actual report dates. Obtain the full transaction history separately for the production read-only legacy archive.\n',encoding='utf-8')
ref=ROOT/'reports/legacy_reference';ref.mkdir(exist_ok=True)
for name in ['Source_inventory.csv','Source_comparisons.csv','Excluded_report_rows.csv','Row_trace.csv','Stock_valuation.csv','Customer_totals.csv','Supplier_totals.csv','Reconciliation.csv','Exceptions.csv','Transformations.csv']:
    shutil.copyfile(OLD/'reports'/name,ref/name)
(ref/'README.md').write_text('# Original preparation evidence\n\nThese reports describe the preserved legacy snapshots and the earlier production review. HOLD findings are production findings; documented UAT changes resolve test-fixture needs only. Do not upload these report CSVs. Use ../../Source_to_Template_Mapping.md and ../Source_column_mappings.csv for the final UAT transformations. Original rows, source-to-CSV comparisons, excluded report subtotals and production discrepancies remain traceable here.\n',encoding='utf-8')
base={(r['Module'],r['Template column']):r for r in rd(OLD/'reports/Transformations.csv')}
mapping=[]
for book in spec[:-1]:
    sh=book['sheets'][0];k=sh['name']
    for h in sh['headers']:
        b=base.get((k,h));source=b['Source column or rule'] if b else 'No supplied value; blank where importer permits'
        transform=b['Transformation'] if b else 'Blank value retained.'
        if h=='ClosingDate':source='Explicit UAT metadata';transform='2026-09-30 test closing. Original snapshot dates remain archived; not certified production balances.'
        elif h=='LegacyRecordId':
            source='Permanent source transaction ID if available; otherwise supported composite'
            transform='Legacy rows keep permanent ID if present, else blank and supported stable composite; never row-number identity. Synthetic issued/bank rows use permanent semantic UAT IDs.'
        if k=='SupplierMaster':
            if h=='GroupCode':source='supgroup A/B/C/G/S; confirmed UAT default';transform='Canonical DOM for every supplier; save SupplierGroup DOM explicitly.'
            elif h=='PayableAccountCode':source='Current active posting account';transform='2110 AP, verified Liability. Legacy account values remain in original archive.'
            elif h=='ExpenseAccountCode':source='Clear supplier purpose / temporary UAT expense';transform='RE187 and RS149 use 6210 Rent; EL264 uses 6220 Electricity; all remaining rows use temporary UAT 5110. This is not production expense sign-off.'
        if k=='SupplierOpenings':
            if h=='DocumentDate':source='dtfdate';transform='Original date except four post-closing PU131 documents moved to 2026-09-30 for UAT only; exact original dates in UAT_changes.csv.'
            elif h=='DueDate':source='Final UAT document date + 30 calendar days';transform='ISO date; four shifted documents due 2026-10-30. Original paydate/crdays remain in source archive.'
        if k=='Stock':
            if h=='CategoryCode':source='Existing exact SKU category / new FG UAT default';transform='Existing Category.Code; 81 new FG use MSH. Raw legacy type/category retained in archive.'
            elif h=='BrandCode':source='Existing exact SKU brand / new FG UAT default';transform='Existing Brand.Name; new FG UNBRANDED.'
            elif h=='UnitCode':source='Existing exact SKU base unit / new FG UAT default';transform='Existing Unit.Symbol; new FG pcs; save Unit PCS mapping.'
            elif h=='ProductType':source='Existing exact SKU product type / source population';transform='Use actual current existing master type; new FG FinishedGood. Approved test factor does not alter original quantity.'
            elif h=='WarehouseCode':source='Confirmed WH_MAIN';transform='All loc1/loc3 FG and all RM consolidated into one WH_MAIN dataset. No separate second Stock upload.'
            elif h=='InventoryAccountCode':source='Warehouse override or existing category / new FG';transform='Existing category or warehouse inventory Asset account; new FG 1132. Current exact master accounts verified.'
            elif h=='LayerDate':source='Approved summarized UAT stock layer';transform='2026-09-30 for every row. Not an original FIFO receipt date.'
            elif h=='ConversionFactor':source='Explicit UAT base-unit assumption';transform='1 for all rows. Final quantities and approved source costs retained.'
        if k=='ReceivedCheques' and h=='BankBranchCode':
            source='G / Bank Code';transform='Original bank-branch code text retained; save the 17 explicit BankBranch mappings. Three exact matches, 14 documented substitute UAT branches.'
        if k in ['Accounts','IssuedCheques','BankOpening','BankRecon','ControlTotals','TrialBalance']:
            source={'Accounts':'Current verified active posting accounts','IssuedCheques':'Explicit semantic UAT issued-cheque fixture','BankOpening':'Explicit UAT bank book / statement fixture','BankRecon':'Explicit UAT pending-deposit fixture','ControlTotals':'Per-scope totals calculated from final UAT CSVs','TrialBalance':'Expected opening contributions plus balanced UAT rent / sales pair'}[k]
            transform={'Accounts':'Mirror all 39 active posting account codes, names and types; ParentCode blank because existing masters are linked. No folders created.','IssuedCheques':'AH143; UAT-ISSUED-001; cheque 000001; date 2026-09-29; document 2026-09-25; 15000.00; bank 1112; both reflection flags true. Already reflected; no second AP/bank movement.','BankOpening':'Code1112; book100000.00; statement112500.00; test closing2026-09-30.','BankRecon':'UAT-DEPOSIT-001; bank1112; 2026-09-28; +2500.00; OutstandingDeposit; BankReflected=true.','ControlTotals':'5212 components for 2050 scopes. AR/AP debt/credit positive magnitudes and full-document count for both; separate cheque counts; per-SKU quantity/value; bank bridge count2. Zero components cover missing schedules without fabricated documents.','TrialBalance':'All 39 current account codes. Opening module expected journal values plus 6210 Debit1000000 / 4110 Credit1000000; remaining accounts explicit test zeros. Debit=Credit282960858.88. Full original TB preserved.'}[k]
            if h=='ClosingDate':transform='Explicit UAT 2026-09-30; original dates preserved.'
            if h=='LegacyRecordId':transform='Synthetic fixture IDs are semantic and permanent; other blank IDs use supported code/composite. Never based on Excel row number.'
        mapping.append([k,source,h,transform,'Applied for UAT'])
write(ROOT/'reports/Source_column_mappings.csv',['Module','Source column or rule','Template column','Final UAT transformation','Status'],mapping)
md='# Source to template mapping\n\nThis is the final UAT transformation guide. Every supported field is listed in [the complete column mapping](reports/Source_column_mappings.csv). [Exact Migration Centre selections](GUIDE_02_Mappings_and_Defaults.md) contains every required master mapping and default.\n\nLegacy financial amounts and signed outstanding documents are unchanged. The separate fixture changes are listed in [UAT changes](reports/UAT_changes.csv); original source-to-output tracing and subtotal exclusions are in [legacy reference](reports/legacy_reference/README.md). Four missing customer and two missing supplier masters use explicit UAT names and genuine blanks. The synthetic bank/issued-cheque/TB records are test-only additions.\n\n'
for k in summary['rows']:
    md+='## '+k+'\n\n| Source column or rule | Template column | Final UAT transformation |\n|---|---|---|\n'
    for module,src,h,tr,state in mapping:
        if module==k:md+='| '+' | '.join(x.replace('|','\\|').replace('\n',' ') for x in [src,h,tr])+' |\n'
    md+='\n'
(ROOT/'Source_to_Template_Mapping.md').write_text(md,encoding='utf-8')
sourcecounts={'Accounts':0,'CustomerMaster':892,'SupplierMaster':213,'CustomerOpenings':1150,'SupplierOpenings':180,'ReceivedCheques':49,'IssuedCheques':0,'Stock':938,'BankOpening':0,'BankRecon':0,'ControlTotals':0,'TrialBalance':188}
rr=[]
for k,n in summary['rows'].items():
    basis='Legacy importable detail rows, including zeros where supported'
    treatment='Legacy documents/amounts retained'
    if k=='Accounts':basis='No legacy code mapping used for this fixture';treatment='39 current master accounts mirrored'
    elif k in ['CustomerMaster','SupplierMaster']:treatment='Added explicit missing-master UAT fixtures: '+str(n-sourcecounts[k])
    elif k in ['IssuedCheques','BankOpening','BankRecon','ControlTotals']:basis='No corresponding supplied legacy schedule';treatment='Explicit synthetic UAT fixture / expected controls'
    elif k=='TrialBalance':treatment='Original 188 rows archived; replaced by balanced complete current-account UAT fixture'
    elif k=='Stock':treatment='494 FG +444 RM, one dataset; duplicate Excel/CSV representations compared, not appended'
    rr.append([k,sourcecounts[k],n,n-sourcecounts[k],basis,treatment,0,0])
write(ROOT/'reports/Source_to_UAT_reconciliation.csv',['Module','Source importable detail rows','Final UAT rows','Row count change','Source basis','Treatment','Duplicate identities','Unmatched required master codes'],rr)
readme=ROOT/'README_UAT.md';text=readme.read_text(encoding='utf-8')
text+='\n[Complete source-column mapping](Source_to_Template_Mapping.md). [Source/UAT row reconciliation](reports/Source_to_UAT_reconciliation.csv). [UAT exceptions](reports/UAT_Exceptions.csv). [Preserved source evidence](reports/legacy_reference/README.md). Missing-contact warnings remain; destination validation is the execution gate.\n'
readme.write_text(text,encoding='utf-8')
for p in ROOT.glob('*.md'):
    text=p.read_text(encoding='utf-8')
    assert '\u00e2\u20ac' not in text and '\u00c3\u2014' not in text,p.name
    text=re.sub(r'^(#{1,6} .+)\n(?!\n)',r'\1\n\n',text,flags=re.M)
    lines=text.splitlines();out=[]
    for line in lines:
        if line.startswith('|') and out and out[-1] and not out[-1].startswith('|'):out.append('')
        out.append(line)
    p.write_text('\n'.join(out)+'\n',encoding='utf-8')
evidence=[]
paths=[r['File relative to repository'] for r in rd(OLD/'reports/Application_source_evidence.csv')]
paths+=['NexusFlow.AppCore/Features/Migration/MigrationBulkRelease.cs']
paths+=['docs/migration/templates/'+k+'.csv' for k in summary['rows']]
for name in dict.fromkeys(paths):
    p=REPO/name;assert p.is_file(),name
    evidence.append([name,p.stat().st_size,hashlib.sha256(p.read_bytes()).hexdigest(),'2026-10-12'])
write(ROOT/'reports/Application_source_evidence.csv',['File relative to repository','Bytes','SHA256','Checked date'],evidence)
for p in (ROOT/'review_workbooks').glob('*.inspect.ndjson'):
    target=ROOT/'.work'/p.name
    if target.exists():target.unlink()
    p.rename(target)
files=[p for p in ROOT.rglob('*') if p.is_file() and '.work' not in p.relative_to(ROOT).parts and p.name not in ['Package_Manifest.csv','NexusFlow_UAT_Upload_Package_20261012.zip']]
manifest=[]
for p in sorted(files):
    rel=p.relative_to(ROOT).as_posix()
    status='Ready for UAT staging; validate in destination before posting' if rel.startswith(('upload_csv/','stage_packages/')) else 'Review workbook / values-only' if rel.startswith('review_workbooks/') and p.stem!='UAT_Review' else 'Review/reference; do not upload as dataset'
    manifest.append([rel,p.stat().st_size,hashlib.sha256(p.read_bytes()).hexdigest(),status])
write(ROOT/'Package_Manifest.csv',['File','Bytes','SHA256','Readiness'],manifest)
bundle=ROOT/'NexusFlow_UAT_Upload_Package_20261012.zip'
with zipfile.ZipFile(bundle,'w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted(files)+[ROOT/'Package_Manifest.csv']:z.write(p,p.relative_to(ROOT).as_posix())
with zipfile.ZipFile(bundle) as z:
    assert z.testzip() is None
    assert len(z.namelist())==len(files)+1
    for rel,size,sha,status in manifest:assert hashlib.sha256(z.read(rel)).hexdigest()==sha,rel
payload={'package':str(bundle),'files':len(files)+1,'bytes':bundle.stat().st_size,'sha256':hashlib.sha256(bundle.read_bytes()).hexdigest(),'workbooks':13,'csv_uploads':12,'staging_packages':3,'guides':7,'preserved_sources':10,'source_field_mappings':len(mapping),'zip_integrity':'Pass'}
(ROOT/'.work/final_package_verification.json').write_text(json.dumps(payload,indent=2),encoding='utf-8')
print(json.dumps(payload,indent=2))

