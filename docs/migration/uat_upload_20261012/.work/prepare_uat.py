import csv,datetime,hashlib,io,json,re,collections,zipfile
from decimal import Decimal as D,ROUND_HALF_UP
from pathlib import Path
REPO=Path('F:/source/NexusFlow_ERP');ROOT=REPO/'docs/migration/uat_upload_20261012';OLD=REPO/'docs/migration/preparation_20261011';WORK=ROOT/'.work'
for name in ['upload_csv','review_workbooks','reports','stage_packages','.work/previews']:(ROOT/name).mkdir(parents=True,exist_ok=True)
SNAP=json.loads((WORK/'local_masters.json').read_text(encoding='utf-8-sig'));M=SNAP['Tables']
ACT=json.loads((WORK/'activity.json').read_text(encoding='utf-8-sig'));assert all(v==0 for v in ACT.values())
SOURCES=json.loads((OLD/'.work/sources.json').read_text(encoding='utf-8'))
for s in SOURCES:assert hashlib.sha256(Path(s['path']).read_bytes()).hexdigest()==s['sha256'],s['filename']
OPEN='2026-10-01';CLOSE='2026-09-30';TYPE={1:'Asset',2:'Liability',3:'Equity',4:'Revenue',5:'Expense'}
project=M['MigrationProjects'][0];assert project['Status']=='Preparing'
SOURCE=project['SourceSystem'];assert project['OpeningDate'].startswith(OPEN) and project['ClosingDate'].startswith(CLOSE)
modules=['Accounts','CustomerMaster','SupplierMaster','CustomerOpenings','SupplierOpenings','ReceivedCheques','IssuedCheques','Stock','BankOpening','BankRecon','ControlTotals','TrialBalance']
headers={k:next(csv.reader(io.StringIO((REPO/'docs/migration/templates'/f'{k}.csv').read_text(encoding='utf-8-sig')))) for k in modules}
catalog=(REPO/'NexusFlow.AppCore/Features/Migration/MigrationModels.cs').read_text(encoding='utf-8-sig')
for k,h in headers.items():
    fields=re.findall(r'"([^"]+)"',re.search(r'\["'+k+r'"\]\s*=\s*\[(.*?)\]',catalog).group(1));assert h==fields+['ClosingDate']
data={k:[] for k in modules};changes=[];maps=[];defaults=[]
def money(v):return format(D(str(v)).quantize(D('.01'),rounding=ROUND_HALF_UP),'.2f')
def changed(module,r,field,value,reason):
    old=r.get(field,'')
    if str(old)!=str(value):changes.append([module,r.get('Code',''),r.get('Reference',''),field,str(old),str(value),reason])
    r[field]=value
def add(module,values,reason):
    row={h:values.get(h,'') for h in headers[module]};data[module].append(row)
    changes.append([module,row.get('Code',''),row.get('Reference',''),'Added UAT row','',json.dumps(values,ensure_ascii=False),reason]);return row
def rows(path):return list(csv.DictReader(io.StringIO(path.read_text(encoding='utf-8-sig'))))
def write_csv(path,head,rr):
    with path.open('w',encoding='utf-8',newline='') as f:
        w=csv.writer(f,lineterminator='\r\n');w.writerow(head)
        for r in rr:w.writerow([r.get(h,'') for h in head] if isinstance(r,dict) else r)
def table(head,rr):
    esc=lambda v:str(v).replace('|','\\|').replace('\n',' ').replace('\r',' ')
    return '\n'.join(['| '+' | '.join(head)+' |','|'+'---|'*len(head)]+['| '+' | '.join(esc(x) for x in r)+' |' for r in rr])
accounts={r['Code']:r for r in M['Accounts'] if r['IsActive'] and r['IsTransactionAccount']}
for c,t in [('1120',1),('2110',2),('4110',4),('5110',5),('6210',5),('6220',5),('1131',1),('1132',1),('1151',1),('1112',1),('3200',3)]:assert accounts[c]['Type']==t
variants={r['SKU']:r for r in M['ProductVariants']};products={r['Id']:r for r in M['Products']}
cats={r['Id']:r for r in M['Categories']};units={r['Id']:r for r in M['UnitOfMeasures']};brands={r['Id']:r for r in M['Brands']}
wh=next(r for r in M['Warehouses'] if r['Code']=='WH_MAIN' and r['IsActive'])
fgcat=next(r for r in M['Categories'] if r['Code']=='MSH' and r['InventoryAccountId']==accounts['1132']['Id'])
brand=next(r for r in M['Brands'] if r['Name']=='UNBRANDED');piece=next(r for r in M['UnitOfMeasures'] if r['Symbol']=='pcs')
for k in ['CustomerMaster','SupplierMaster','CustomerOpenings','SupplierOpenings','ReceivedCheques','Stock']:
    folder='staging_csv' if k.endswith('Master') else 'held_csv';data[k]=rows(OLD/folder/(k+'.csv'))
    for r in data[k]:changed(k,r,'ClosingDate',CLOSE,'UAT metadata only; original snapshot date remains in the production archive.')
for r in data['SupplierMaster']:
    changed('SupplierMaster',r,'GroupCode','DOM','Confirmed domestic group; canonical code for explicit DOM mapping.')
    changed('SupplierMaster',r,'PayableAccountCode','2110','Verified AP posting account used directly.')
    if not r['ExpenseAccountCode']:changed('SupplierMaster',r,'ExpenseAccountCode','5110','Temporary UAT expense account; not a production supplier classification.')
for k in ['CustomerOpenings','SupplierOpenings']:
    for r in data[k]:
        if r['DocumentDate']>CLOSE:
            changed(k,r,'DocumentDate',CLOSE,'UAT only: move post-closing fixture to test closing; original date archived.')
            changed(k,r,'DueDate',(datetime.date.fromisoformat(CLOSE)+datetime.timedelta(days=30)).isoformat(),'UAT due date = test document date +30 days.')
        assert r['DocumentDate']<=CLOSE and D(r['Amount'])!=0
for kind,master,opening in [('Customer','CustomerMaster','CustomerOpenings'),('Supplier','SupplierMaster','SupplierOpenings')]:
    present={r['Code'] for r in data[master]};required={r['Code'] for r in data[opening]}
    if kind=='Customer':required|={r['Code'] for r in data['ReceivedCheques']}
    required|={r['Code'] for r in M.get('MigrationScopes',[]) if r['Kind']==kind}
    for c in sorted(required-present):
        v=dict(Code=c,Name='UAT '+kind+' '+c,GroupCode='TRADE' if kind=='Customer' else 'DOM',PaymentTermCode='NET30',ClosingDate=CLOSE)
        if kind=='Customer':v.update(PriceLevelCode='STANDARD TIER',ReceivableAccountCode='1120',RevenueAccountCode='4110')
        else:v.update(PayableAccountCode='2110',ExpenseAccountCode='5110')
        add(master,v,'Explicit missing-master UAT fixture. Legacy code retained; name begins UAT; contacts blank.')
new_skus=[]
for r in data['Stock']:
    changed('Stock',r,'LayerDate',CLOSE,'UAT summarized layer date; not an original FIFO receipt date.')
    changed('Stock',r,'ConversionFactor','1','UAT source/base units treated as equal; quantity and cost unchanged.')
    existing=variants.get(r['Code'])
    if existing:
        p=products[existing['ProductId']];cat=cats[p['CategoryId']]
        inv=next(a for a in accounts.values() if a['Id']==(wh['OverrideInventoryAccountId'] or cat['InventoryAccountId']));assert inv['Type']==1 and p['Type'] in [1,2]
        values={'ProductType':'RawMaterial' if p['Type']==1 else 'FinishedGood','CategoryCode':cat['Code'],'BrandCode':brands[p['BrandId']]['Name'],'UnitCode':units[p['UnitOfMeasureId']]['Symbol'],'InventoryAccountCode':inv['Code']}
        for h,v in values.items():changed('Stock',r,h,v,'Use exact existing SKU master/category/base unit/posting account for UAT.')
    else:
        new_skus.append(r['Code'])
        for h,v in {'CategoryCode':fgcat['Code'],'BrandCode':brand['Name'],'UnitCode':piece['Symbol'],'InventoryAccountCode':'1132'}.items():changed('Stock',r,h,v,'UAT only: new FG uses MSH / UNBRANDED / pcs / FG inventory.')
for a in sorted(accounts.values(),key=lambda x:x['Code']):data['Accounts'].append({h:dict(Code=a['Code'],Name=a['Name'],AccountType=TYPE[a['Type']],ClosingDate=CLOSE).get(h,'') for h in headers['Accounts']})
add('IssuedCheques',dict(LegacyRecordId='UAT:ISSUED:AH143:000001:20260925',Code='AH143',Reference='UAT-ISSUED-001',ChequeNumber='000001',ChequeDate='2026-09-29',DocumentDate='2026-09-25',Amount='15000.00',BankAccountCode='1112',PartyReflected='true',BankReflected='true',ClosingDate=CLOSE),'Synthetic issued cheque already reflected in UAT party and bank book.')
add('BankOpening',dict(Code='1112',Amount='100000.00',StatementBalance='112500.00',ClosingDate=CLOSE),'Synthetic bank book/statement fixture.')
add('BankRecon',dict(LegacyRecordId='UAT:BANK:OUTSTANDING-DEPOSIT:001',Code='1112',Reference='UAT-DEPOSIT-001',DocumentDate='2026-09-28',Amount='2500.00',Kind='OutstandingDeposit',BankReflected='true',ClosingDate=CLOSE),'Synthetic deposit; bank book = 112500 +2500 -15000 =100000.')
def mapping(typ,code,target,label,basis):maps.append([typ,code,target,label,basis])
for typ,code in [('CustomerGroup','TRADE'),('SupplierGroup','DOM'),('PaymentTerm','NET30'),('PriceLevel','STANDARD TIER')]:
    r=next(r for r in M['SystemLookups'] if r['Type']==typ and r['Code']==code and r['IsActive'])
    mapping(typ,code,r['Id'],r['Code']+' — '+r['Value'],'Required explicit mapping.')
mapping('Warehouse','WH_MAIN',wh['Id'],wh['Code']+' — '+wh['Name'],'Required on stock rows and controls.')
for sym in sorted({r['UnitCode'].upper() for r in data['Stock']}):
    matches=[r for r in M['UnitOfMeasures'] if r['Symbol'].upper()==sym];assert len(matches)==1
    r=matches[0];mapping('Unit',sym,r['Id'],r['Symbol']+' — '+r['Name'],'PCS covers pcs and Pcs after code normalization.')
mapping('Category',fgcat['Code'],fgcat['Id'],fgcat['Code']+' — '+fgcat['Name'],'Temporary new-FG UAT classification.')
mapping('Brand',brand['Name'],brand['Id'],brand['Name'],'Required for new FG fixture SKUs.')
banks={r['Id']:r for r in M['Banks']};branches=[r for r in M['BankBranches'] if r['IsActive'] and banks[r['BankId']]['IsActive']]
branch_codes={str(banks[r['BankId']]['BankCode'])+'-'+str(r['BranchCode']):r for r in branches};branch_fallbacks=0
for code in sorted({r['BankBranchCode'] for r in data['ReceivedCheques']}):
    r=branch_codes.get(code);basis='Exact current bank/branch code.'
    if not r:
        opts=[b for b in branches if str(banks[b['BankId']]['BankCode'])==code.split('-')[0]]
        r=sorted(opts or branches,key=lambda b:b['Id'])[0];branch_fallbacks+=1
        basis='UAT-only substitute branch; bank retained where available. Not physical-cheque verification.'
    bank=banks[r['BankId']];targetcode=str(bank['BankCode'])+'-'+str(r['BranchCode'])
    mapping('BankBranch',code,r['Id'],targetcode+' — '+bank['Name']+' / '+r['BranchName'],basis)
    if code not in branch_codes:changes.append(['ReceivedCheques','',code,'BankBranch mapping',code,targetcode,basis])
for label,c in [('Opening equity account','3200'),('Default customer control','1120'),('Default customer revenue','4110'),('Default supplier control','2110'),('Default supplier expense','5110'),('Undeposited cheque account','1151')]:
    defaults.append([label,c+' — '+accounts[c]['Name'],'Temporary UAT expense' if c=='5110' else 'Verified active posting account'])
defaults += [
 ['Default customer group','TRADE — Trade','Also map TRADE explicitly'],['Default supplier group','DOM — Domestic Suppliers','Also map DOM explicitly'],
 ['Default price level','STANDARD TIER — Standard Tier','Also map explicitly'],['Default customer payment term','NET30 — 30 Days','Do not use COD'],['Default supplier payment term','NET30 — 30 Days','Do not use COD'],
 ['Approved base unit when source unit is missing','pcs — Piece','Temporary UAT unit for new FG'],['Approved warehouse when source location is missing','WH_MAIN — Main Garnet','All stock rows populated'],
 ['Approved supplier terms in days (optional)','30','Due dates already populated'],
 ['Summarized stock layer approval','UAT fixture: approved summarized layer per SKU in WH_MAIN at 2026-09-30; original FIFO dates not claimed.','Paste evidence'],
 ['Legacy finished goods valuation','Average cost (avar × warehouse quantity)','Final UnitCost already populated'],
 ['Valuation discrepancy resolution evidence','UAT uses approved avar; original costval difference -3173818.42 LKR retained in archive; not production sign-off.','Paste evidence']]
by_module=collections.defaultdict(lambda:collections.defaultdict(lambda:D(0)))
for r in data['CustomerOpenings']:by_module['CustomerOpenings']['1120']+=D(r['Amount'])
for r in data['SupplierOpenings']:by_module['SupplierOpenings']['2110']-=D(r['Amount'])
for r in data['ReceivedCheques']:by_module['ReceivedCheques']['1151']+=D(r['Amount'])
for r in data['Stock']:by_module['Stock'][r['InventoryAccountCode']]+=D(money(D(r['Quantity'])*D(r['UnitCost'])))
by_module['BankOpening']['1112']=D('100000');opening=collections.defaultdict(lambda:D(0))
for module,movements in by_module.items():
    movements['3200']=-sum(movements.values());assert sum(movements.values())==0
    for c,v in movements.items():opening[c]+=v
closing={c:opening[c] for c in accounts};closing['6210']+=D('1000000');closing['4110']-=D('1000000');assert sum(closing.values())==0
for c,v in sorted(closing.items()):data['TrialBalance'].append({h:dict(Code=c,Debit=money(max(v,0)),Credit=money(max(-v,0)),ClosingDate=CLOSE).get(h,'') for h in headers['TrialBalance']})
changes.append(['TrialBalance','','','Entire dataset','188 original legacy rows; imbalance -613120.00','39 current account rows; balanced UAT position','Synthetic TB = opening contributions plus rent debit / sales credit 1000000 each. Original TB preserved.'])
def control(kind,c,component,amount,count):
    data['ControlTotals'].append({h:dict(Kind=kind,Code=c,Component=component,Amount=money(amount) if not component.endswith(':Quantity') else format(D(amount),'.4f'),RowCount=str(count),ClosingDate=CLOSE).get(h,'') for h in headers['ControlTotals']})
for r in data['CustomerMaster']:
    c=r['Code'];ar=[D(x['Amount']) for x in data['CustomerOpenings'] if x['Code']==c];ch=[D(x['Amount']) for x in data['ReceivedCheques'] if x['Code']==c]
    control('Customer',c,'CustomerOpenings:Debit',sum((max(x,0) for x in ar),D(0)),len(ar));control('Customer',c,'CustomerOpenings:Credit',sum((max(-x,0) for x in ar),D(0)),len(ar));control('Customer',c,'ReceivedCheques',sum(ch,D(0)),len(ch))
for r in data['SupplierMaster']:
    c=r['Code'];ap=[D(x['Amount']) for x in data['SupplierOpenings'] if x['Code']==c];ch=[D(x['Amount']) for x in data['IssuedCheques'] if x['Code']==c]
    control('Supplier',c,'SupplierOpenings:Debit',sum((max(x,0) for x in ap),D(0)),len(ap));control('Supplier',c,'SupplierOpenings:Credit',sum((max(-x,0) for x in ap),D(0)),len(ap));control('Supplier',c,'IssuedCheques',sum(ch,D(0)),len(ch))
for r in data['Stock']:
    control('Stock',r['Code'],'Stock:WH_MAIN:Quantity',D(r['Quantity']),1);control('Stock',r['Code'],'Stock:WH_MAIN:Value',D(money(D(r['Quantity'])*D(r['UnitCost']))),1)
control('Bank','1112','BankOpening',D('100000'),1);control('Bank','1112','BankStatementBridge',D('100000'),2);control('Bank','1112','BankRecon',D('2500'),1)
def ident(k,r):
    if r.get('LegacyRecordId'):return r['LegacyRecordId']
    if k=='ControlTotals':v=[r['Kind'],r['Code'],r['Component']]
    elif k in ['Accounts','CustomerMaster','SupplierMaster','BankOpening','TrialBalance']:return r['Code']
    elif k=='Stock':v=[r['Code'],r['WarehouseCode'],r['Batch'],r['LayerDate']]
    else:v=[r['Code'],r.get('Kind',''),r.get('Reference',''),r['ChequeDate'] if k=='ReceivedCheques' else r.get('DocumentDate',''),r.get('ChequeNumber','')]
    return json.dumps(v,separators=(',',':'))
for k,rr in data.items():
    assert len({ident(k,r) for r in rr})==len(rr),(k,'duplicate');assert len(rr)<=20000
    write_csv(ROOT/'upload_csv'/(k+'.csv'),headers[k],rr)
partysets={'Customer':{r['Code'] for r in data['CustomerMaster']},'Supplier':{r['Code'] for r in data['SupplierMaster']},'Stock':{r['Code'] for r in data['Stock']},'Bank':{'1112'}}
assert all(r['Code'] in partysets[r['Kind']] for r in M.get('MigrationScopes',[]))
scope_count=sum(len(v) for v in partysets.values())
stages={'01_Masters':['Accounts','CustomerMaster','SupplierMaster'],'02_Openings_and_Controls':['CustomerOpenings','SupplierOpenings','ReceivedCheques','IssuedCheques','Stock','BankOpening','BankRecon','ControlTotals'],'03_TrialBalance':['TrialBalance']}
for stage,kk in stages.items():
    manifest={'Version':'1.0','SourceSystem':SOURCE,'ClosingDate':CLOSE,'Files':[{'Module':k,'File':k+'.csv'} for k in kk]}
    with zipfile.ZipFile(ROOT/'stage_packages'/(stage+'.zip'),'w',zipfile.ZIP_DEFLATED) as z:
        z.writestr('manifest.json',json.dumps(manifest,indent=2))
        for k in kk:z.write(ROOT/'upload_csv'/(k+'.csv'),k+'.csv')
summaryrows=[[k,len(data[k]),'Legacy-derived with recorded UAT changes' if k in ['CustomerMaster','SupplierMaster','CustomerOpenings','SupplierOpenings','ReceivedCheques','Stock'] else 'Current verified masters' if k=='Accounts' else 'Synthetic fixture/expected controls','Ready for UAT staging','Target validation/approval required'] for k in modules]
totals={}
for k in ['CustomerOpenings','SupplierOpenings','ReceivedCheques','IssuedCheques']:
    v=[D(r['Amount']) for r in data[k]];totals[k]={'positive':money(sum((x for x in v if x>0),D(0))),'negative':money(sum((x for x in v if x<0),D(0))),'net':money(sum(v,D(0)))}
glrows=[[c,accounts[c]['Name'],TYPE[accounts[c]['Type']],money(opening[c]),money(closing[c]),money(closing[c]-opening[c])] for c in sorted(accounts)]
journalrows=[]
for k,mm in by_module.items():
    for c,v in sorted(mm.items()):journalrows.append([k,c,accounts[c]['Name'],money(max(v,0)),money(max(-v,0)),money(v)])
journalrows += [['TrialBalance','6210',accounts['6210']['Name'],'1000000.00','0.00','1000000.00'],['TrialBalance','4110',accounts['4110']['Name'],'0.00','1000000.00','-1000000.00']]
testrows=[
 ['UAT-01','Project','Isolated UAT project','Select saved test project','Preparing; opening 2026-10-01; closing 2026-09-30','',''],
 ['UAT-02','Staging','Preparing project','Upload Stage 1 package/CSVs','39 accounts, 896 customers, 215 suppliers; no ledger posting','',''],
 ['UAT-03','Mappings','Masters staged','Save mappings/defaults','NET30 means 30 Days; DOM/TRADE mapped; no COD','',''],
 ['UAT-04','Masters','Clean validation','Validate, approve, execute masters','All masters imported; genuine missing-contact warnings remain','',''],
 ['UAT-05','Customer documents','Masters imported','Import CustomerOpenings','1150 items, positive Invoice / negative ReturnCredit; net 162114985.14','',''],
 ['UAT-06','Zero-net customer','Opening items posted','Inspect DC969, FA111, JY308, SA280, VI852','Debt and credit documents both retained','',''],
 ['UAT-07','Supplier documents','Masters imported','Import SupplierOpenings','180 items; positive Bill / negative Credit; net 94190250.55','',''],
 ['UAT-08','Received cheques','Customer masters imported','Import and inspect cheque numbers','49 InSafe instruments; zeros retained; safe 5328628.00; AR unchanged','',''],
 ['UAT-09','Issued cheque','AH143 imported','Import UAT-ISSUED-001','000001 / 15000 pending bank item; no second AP/bank payment','',''],
 ['UAT-10','Stock','No prior SKU activity; mappings saved','Import one complete Stock dataset','938 WH_MAIN layers; quantities/costs preserved','',''],
 ['UAT-11','Bank','1112 active Asset','Import bank opening/recon','100000 =112500 +2500 -15000','',''],
 ['UAT-12','Controls','Opening modules posted','Import ControlTotals','5212 expected components, including master-only zero controls','',''],
 ['UAT-13','Release','Controls imported','Bulk release all scope kinds','2050 scopes Released; zero Pending','',''],
 ['UAT-14','TB','All scopes released','Import complete test TB','39 accounts; balanced; residual rent debit / revenue credit 1000000','',''],
 ['UAT-15','Ledger','TB imported','Check Full Ledger','All 39 differences zero','',''],
 ['UAT-16','Retry','Already imported selection','Validate same unchanged selection again','Already imported; no duplicate openings','',''],
 ['UAT-17','Finalize','All rows imported and scopes released','Enter UAT evidence; Finalize Migration','Finalized; migration edits/execution disabled','',''],
 ['UAT-18','Customer payment','Customer scope Released','Receive payment against positive opening Invoice','Outstanding reduces and receipt posts once','',''],
 ['UAT-19','Supplier payment','Supplier scope Released','Pay positive opening Bill; apply separate Credit','Balances update without recreating historical bill','',''],
 ['UAT-20','Stock operation','Stock scope Released','Normal issue/sale/receipt for imported SKU','On-hand/layer quantities and journal accounts update','',''],
 ['UAT-21','Cheque deposit','Customer/bank scopes Released','Deposit one imported InSafe cheque','Safe reduces, bank movement once, no second AR reduction','','']]
report_tables={
 'Datasets':(['Module','Rows','Data basis','Upload status','Execution status'],summaryrows),
 'UAT changes':(['Module','Code','Reference','Field','Original value','UAT value','Reason'],changes),
 'Mappings':(['Mapping type','Exact legacy code','Local target ID','Target label','Basis'],maps),
 'Defaults':(['UI field','Selection','Reason'],defaults),
 'Expected GL':(['Code','Account','Type','Opening module net (LKR)','UAT closing net (LKR)','TB residual (LKR)'],glrows),
 'Expected journals':(['Module','Account code','Account name','Debit (LKR)','Credit (LKR)','Net (LKR)'],journalrows),
 'UAT tests':(['Test ID','Stage','Precondition','Steps','Expected result','Pass / Fail','Evidence / tester'],testrows)}
for name,(h,rr) in report_tables.items():write_csv(ROOT/'reports'/(name.replace(' ','_')+'.csv'),h,rr)
books=[{'file':'review_workbooks/'+k+'.xlsx','sheets':[{'name':k,'headers':headers[k],'rows':[[r[h] for h in headers[k]] for r in data[k]],'template':True}]} for k in modules]
books.append({'file':'review_workbooks/UAT_Review.xlsx','sheets':[{'name':name,'headers':h,'rows':rr,'template':False} for name,(h,rr) in report_tables.items()]})
(WORK/'workbooks.json').write_text(json.dumps(books,ensure_ascii=False),encoding='utf-8')
summary={'date':'2026-10-12','opening':OPEN,'closing':CLOSE,'source_system':SOURCE,'project_id':project['Id'],'rows':{k:len(v) for k,v in data.items()},'totals':totals,'stock_value':money(sum(D(money(D(r['Quantity'])*D(r['UnitCost']))) for r in data['Stock'])),'fg_quantity':str(sum(D(r['Quantity']) for r in data['Stock'][:494])),'rm_quantity':str(sum(D(r['Quantity']) for r in data['Stock'][494:])),'tb_debit':money(sum(D(r['Debit']) for r in data['TrialBalance'])),'tb_credit':money(sum(D(r['Credit']) for r in data['TrialBalance'])),'scopes':scope_count,'new_skus':len(new_skus),'branch_substitutions':branch_fallbacks,'changes':len(changes),'read_at_utc':SNAP['ReadAtUtc'],'activity':ACT}
(WORK/'summary.json').write_text(json.dumps(summary,indent=2),encoding='utf-8')
settings={'OpeningEquityAccountId':accounts['3200']['Id'],'Mappings':{},'Columns':{},'Defaults':{'ReceivableAccount':accounts['1120']['Id'],'RevenueAccount':accounts['4110']['Id'],'PayableAccount':accounts['2110']['Id'],'ExpenseAccount':accounts['5110']['Id'],'SafeAccount':accounts['1151']['Id'],'CustomerGroup':13,'SupplierGroup':15,'PriceLevel':10,'CustomerPaymentTerm':27,'SupplierPaymentTerm':27,'StockUnit':piece['Id'],'StockWarehouse':wh['Id']},'SupplierTermDays':30,'SummarizedStockEvidence':defaults[-3][1],'FinishedGoodsValuation':'AverageCost','FinishedGoodsValuationEvidence':defaults[-1][1],'DatasetStates':{k:'Provided' for k in modules},'ZeroEvidence':{}}
for typ,c,target,label,basis in maps:settings['Mappings'].setdefault(typ,{})[c.upper()]=target
(ROOT/'reports/UAT_Settings_reference.json').write_text(json.dumps(settings,indent=2),encoding='utf-8')
print(json.dumps(summary,indent=2))

