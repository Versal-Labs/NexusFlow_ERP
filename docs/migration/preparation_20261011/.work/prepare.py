import json, csv, re, hashlib, zipfile, os, datetime, collections
from pathlib import Path
from decimal import Decimal as D, ROUND_HALF_UP

ROOT=Path(__file__).resolve().parents[1]
REPO=ROOT.parents[2]
WORK=ROOT/'.work'
SOURCES=json.loads((WORK/'sources.json').read_text(encoding='utf-8'))
MASTER=json.loads((WORK/'local_masters.json').read_text(encoding='utf-8-sig'))
M=MASTER['Tables']
OPENING='2026-10-01'; CLOSING='2026-09-30'
DECISIONS={
 'opening_date':OPENING,'closing_date':CLOSING,'currency':'LKR',
 'customer_due_days':30,'supplier_due_days':30,'supplier_negative_kind':'Credit',
 'supplier_positive_signs':'Preserve dtfbal, including ADVANCE/OVER PAID remarks',
 'warehouse':'WH_MAIN','finished_goods_cost':'avar','summarized_layers':True,
 'cheque_date':'To be Dep Date, carried forward within its report group',
 'cheque_status':'InSafe','party_reflected':'true','bank_reflected':'false',
 'customer_group':'TRADE','price_level':'STANDARD TIER','payment_term':'NET30',
 'supplier_group_mappings':{x:'DOM' for x in ['A','B','C','G','S']},
 'BA366_email':'Blank, explicitly confirmed genuinely unknown',
 'supplier_expense_account':'6210 assigned to RE187/RS149; 6220 assigned to EL264; remaining 210 need approved supplier-specific expense assignments',
 'history':'Retain supplied source files read-only; full pre-cutover transaction archive still required'
}
(ROOT/'business_decisions.json').write_text(json.dumps(DECISIONS,indent=2),encoding='utf-8')
for sub in ['review_workbooks','staging_csv','held_csv','reports','archive','.work/previews']:(ROOT/sub).mkdir(parents=True,exist_ok=True)
def txt(v):
    if v is None:return ''
    if isinstance(v,(int,float)):return format(D(str(v)),'f').rstrip('0').rstrip('.') if '.' in str(v) else str(v)
    return str(v).strip()
def num(v):return D(str(v))
def money(v):return format(num(v).quantize(D('.01'),rounding=ROUND_HALF_UP),'.2f')
def fixed(v,n):return format(num(v),f'.{n}f')
def date(v):
    s=txt(v)
    if not s or s.replace('-','').replace(' ','')=='':return ''
    for fmt in ['%Y-%m-%d','%d/%m/%Y']:
        try:return datetime.datetime.strptime(s,fmt).strftime('%Y-%m-%d')
        except ValueError:pass
    raise ValueError('Invalid date '+s)
def due(v):return (datetime.date.fromisoformat(date(v))+datetime.timedelta(days=30)).isoformat()
def source(key):return next(x for x in SOURCES if x['key']==key)
def records(key):
    r=source(key)['sheets'][0]['rows'];return [(i+2,dict(zip(r[0],row))) for i,row in enumerate(r[1:]) if any(v not in ('',None) for v in row)]
def write_csv(path,headers,rows):
    with path.open('w',encoding='utf-8',newline='') as f:
        w=csv.writer(f,lineterminator='\r\n');w.writerow(headers)
        for row in rows:w.writerow([row.get(h,'') for h in headers] if isinstance(row,dict) else row)
def table_md(headers,rows):
    def escape(v):return str(v).replace('|','\\|').replace('\r',' ').replace('\n',' ')
    return '\n'.join(['| '+' | '.join(headers)+' |','|'+'---|'*len(headers)]+['| '+' | '.join(escape(x) for x in r)+' |' for r in rows])

TYPE={1:'Asset',2:'Liability',3:'Equity',4:'Revenue',5:'Expense'}
accounts={r['Id']:r for r in M['Accounts']}; acccode={r['Code']:r for r in M['Accounts']}
products={r['Id']:r for r in M['Products']}; variants={r['SKU']:r for r in M['ProductVariants']}
units={r['Id']:r for r in M['UnitOfMeasures']}; categories={r['Id']:r for r in M['Categories']};brands={r['Id']:r for r in M['Brands']}
current_customers={r['LegacyCustomerCode']:r for r in M['Customers'] if r['LegacyCustomerCode']}
current_suppliers={r['LegacySupplierCode']:r for r in M['Suppliers'] if r['LegacySupplierCode']}
headers={k:next(csv.reader((REPO/'docs/migration/templates'/f'{k}.csv').read_text(encoding='utf-8-sig').splitlines())) for k in ['CustomerMaster','SupplierMaster','CustomerOpenings','SupplierOpenings','ReceivedCheques','Stock','TrialBalance']}
model=(REPO/'NexusFlow.AppCore/Features/Migration/MigrationModels.cs').read_text(encoding='utf-8-sig')
for module,head in headers.items():
    match=re.search(r'\["'+module+r'"\]\s*=\s*\[(.*?)\]',model)
    contract=re.findall(r'"([^"]+)"',match.group(1))
    assert head==contract+['ClosingDate'],(module,head,contract)

datasets={k:[] for k in headers}; trace=[]; exceptions=[]; excluded=[]; transformations=[]; maps=[]; duplicates=[]
def issue(module,code,ref,row,field,severity,value,reason,action):
    exceptions.append([module,code,ref,row,field,severity,txt(value),reason,action])
def mapping(module,src,target,method,status='Applied'):
    transformations.append([module,src,target,method,status])
def select(kind,legacy,target_id,target_label,status,reason):maps.append([kind,legacy,target_id,target_label,status,reason])
def add(module,data,key,row,notes,snapshot='',basis=''):
    out={h:data.get(h,'') for h in headers[module]};datasets[module].append(out)
    trace.append([module,len(datasets[module])+1,source(key)['filename'],source(key)['sheets'][0]['name'],row,out.get('Code',''),out.get('Reference',''),snapshot,basis,notes])

# Master fields come from source columns or explicit confirmed classifications.
for rn,r in records('Customers'):
    c=txt(r['debcode']); a2='; '.join(txt(r[k]) for k in ['debadd2','debadd3'] if txt(r[k]))
    email='' if c=='BA366' else txt(r['debmail'])
    data=dict(Code=c,Name=txt(r['debname']),Contact=txt(r['debcper']),Email=email,Phone=txt(r['debtel1']),Mobile=txt(r['debtel2']),Address1=txt(r['debadd1']),Address2=a2,GroupCode='TRADE',PaymentTermCode='NET30',PriceLevelCode='STANDARD TIER',ReceivableAccountCode='1120',RevenueAccountCode='4110',ClosingDate=CLOSING)
    add('CustomerMaster',data,'Customers',rn,'Stable identity = Code. Confirmed classifications. Configured account codes. Address2 retains debadd3. ClosingDate is project metadata only.')
    missing=[k for k in ['Contact','Email','Phone','Address1','TaxNumber'] if not data.get(k)]
    if missing:issue('CustomerMaster',c,'',rn,','.join(missing),'Later completion','','Genuine missing master details','Complete through Customer Master later; blank contact details are allowed.')
    if c=='BA366':issue('CustomerMaster',c,'',rn,'Email','Resolved',r['debmail'],'Invalid legacy email replaced with a genuine blank by user confirmation','Original retained in archive.')
    if txt(r['debadd3']):issue('CustomerMaster',c,'',rn,'Address2','Review',r['debadd3'],'Third address line joined to Address2','Review combined address; phone-like text is retained without assigning a new phone number.')
for rn,r in records('Suppliers'):
    c=txt(r['supcode']);a2='; '.join(txt(r[k]) for k in ['supadd2','supadd3','supadd4'] if txt(r[k]))
    data=dict(Code=c,Name=txt(r['supname']),Contact=txt(r['supcontact']),Email=txt(r['supemail']),Phone=txt(r['suptele']),Address1=txt(r['supadd1']),Address2=a2,TaxNumber=txt(r['tinno']),GroupCode=txt(r['supgroup']),PaymentTermCode='NET30',PayableAccountCode=txt(r['supacc']),ExpenseAccountCode={'RE187':'6210','RS149':'6210','EL264':'6220'}.get(c,''),ClosingDate=CLOSING)
    add('SupplierMaster',data,'Suppliers',rn,'Stable identity = Code. A/B/C/G/S preserved for explicit DOM mappings. Source payable code preserved; no fabricated expense code. Address2 retains later address lines.')
    missing=[k for k in ['Contact','Email','Phone','Address1','TaxNumber'] if not data.get(k)]
    if missing:issue('SupplierMaster',c,'',rn,','.join(missing),'Later completion','','Genuine missing master details','Complete through Supplier Master later.')
    if not data['ExpenseAccountCode']:issue('SupplierMaster',c,'',rn,'ExpenseAccountCode','Blocks validation','','No source expense account and no general purchasing expense default','Assign the actual active Expense posting account for this supplier or establish an approved purchasing expense account in master maintenance.')

customer_master_codes={r['Code'] for r in datasets['CustomerMaster']};supplier_master_codes={r['Code'] for r in datasets['SupplierMaster']}
ar_aging=collections.Counter()
for rn,r in records('CustomerOpenings'):
    amount=num(r['creamt']);c=txt(r['debcode']);kind='Invoice' if amount>0 else 'ReturnCredit'
    add('CustomerOpenings',dict(Code=c,Reference=txt(r['ref']),Kind=kind,DocumentDate=date(r['tdate']),DueDate=due(r['tdate']),Amount=money(amount),ClosingDate='2026-09-28'),'CustomerOpenings',rn,'Amount=creamt only; no debamt subtraction; original dates +30 days. No cheque value added. Composite identity retained.','2026-09-28','Original document date + days on types 1/2; 958 rows agree.')
    if r['xtyp'] in ['1','2']:ar_aging[(datetime.date.fromisoformat(date(r['tdate']))+datetime.timedelta(days=int(r['days']))).isoformat()]+=1
    if c not in customer_master_codes and c not in current_customers:issue('CustomerOpenings',c,txt(r['ref']),rn,'Code','Blocks validation',c,'No matching supplied or current local customer legacy code','Obtain genuine master or exact existing legacy-code binding; retain every document.')
    ages=sum(num(r[k]) for k in ['today','first30','second30','third30','fourth30','fifth30'])
    if ages!=amount:issue('CustomerOpenings',c,txt(r['ref']),rn,'Amount','Blocks posting',str(ages),'Ageing columns do not equal creamt','Refresh and reconcile source.')
assert ar_aging=={'2026-09-28':958},ar_aging

ap_aging=collections.Counter(); ap_due_changes=[]; postcutover=[]
for rn,r in records('SupplierOpenings'):
    c=txt(r['supcode']); amount=num(r['dtfbal']);d=date(r['dtfdate']);pd=date(r['paydate']);dd=due(d)
    add('SupplierOpenings',dict(Code=c,Reference=txt(r['dtfref']),Kind='Bill' if amount>0 else 'Credit',DocumentDate=d,DueDate=dd,Amount=money(amount)),'SupplierOpenings',rn,'dtfbal preserved as reported remaining balance, with user-confirmed signs: positive Bill / negative returned-goods Credit. DueDate=document+30. Source paydate retained in archive. ClosingDate unknown.','2026-10-07','Inferred: document date + grndays, not certified by a report header.')
    ap_aging[(datetime.date.fromisoformat(d)+datetime.timedelta(days=int(r['grndays']))).isoformat()]+=1
    if c not in supplier_master_codes and c not in current_suppliers:issue('SupplierOpenings',c,txt(r['dtfref']),rn,'Code','Blocks validation',c,'No matching supplied or current local supplier legacy code','Obtain genuine SH331/WL329 master; do not match by name or customer debcode.')
    if pd and pd!=dd:
        ap_due_changes.append([c,txt(r['dtfref']),rn,d,pd,dd,txt(r['crdays'])])
        issue('SupplierOpenings',c,txt(r['dtfref']),rn,'DueDate','Resolved',pd,'Source paydate differs from confirmed document+30 terms','Confirmed 30-day date exported; original paydate and crdays retained in archive.')
    if pd and pd<d:issue('SupplierOpenings',c,txt(r['dtfref']),rn,'DueDate','Resolved',pd,'Original due date precedes document date','User-confirmed 30-day date replaces it; obtain corrected source for closing refresh.')
    if d>CLOSING:
        postcutover.append([c,txt(r['dtfref']),rn,d,money(amount)])
        issue('SupplierOpenings',c,txt(r['dtfref']),rn,'DocumentDate','Blocks posting',d,'Document is after 30 September closing','Retain in review file. Exclude from opening only through a complete refreshed 30 September export; handle it in the October activity transfer.')
    if amount<0:issue('SupplierOpenings',c,txt(r['dtfref']),rn,'Kind','Resolved',r['dtfremarks'],'Source negative remarks include advance/returned cheque; user confirms negative balances mean returns','Kind=Credit, negative sign preserved; retain original remark.')
    if amount>0 and re.search('ADVANCE|OVER PAID',txt(r['dtfremarks']),re.I):issue('SupplierOpenings',c,txt(r['dtfref']),rn,'Amount','Resolved',r['dtfremarks'],'Positive balance has ADVANCE/OVER PAID remark','User explicitly confirmed preserve positive source sign. Kind=Bill.')
assert ap_aging=={'2026-10-07':180},ap_aging

# Cheque report: recognize only CH instrument lines; carry face date within a subtotal group.
sheet=source('ReceivedCheques')['sheets'][0]; group_date=''; group_amt=D(0); grand=D(0); group_checks=[]
for rn,row in enumerate(sheet['rows'],1):
    r=(row+['']*10)[:10];ref=txt(r[2]);label=txt(r[6])
    if label=='Total for':
        group_checks.append([rn,group_date,money(group_amt),money(r[8]),group_amt==num(r[8])]);group_date='';group_amt=D(0)
    elif label=='Grand Total':
        assert grand==num(r[8]),(grand,r[8])
    if re.fullmatch(r'CH\d+',ref):
        if txt(r[0]):group_date=date(r[0])
        assert group_date,'Missing cheque date group'
        amount=num(r[8]);group_amt+=amount;grand+=amount;c=txt(r[4]);chq=txt(r[7])
        add('ReceivedCheques',dict(Code=c,Reference=ref,DocumentReferences=txt(r[3]),ChequeNumber=chq,ChequeDate=group_date,ReceivedDate=date(r[1]),BankBranchCode=txt(r[6]),Amount=money(amount),Status='InSafe',PartyReflected='true',BankReflected='false',SafeAccountCode='1151',ClosingDate='2026-09-28'),'ReceivedCheques',rn,'Instrument only; grouped date carry confirmed as face date. User confirmed InSafe/party true/bank false. Safe account from current configuration.','2026-09-28','Explicit report title.')
        if c not in customer_master_codes and c not in current_customers:issue('ReceivedCheques',c,ref,rn,'Code','Blocks validation',c,'No matching customer legacy code','Obtain genuine master.')
    elif any(txt(v) for v in r):excluded.append(['ReceivedCheques',rn,'Presentation or subtotal/grand-total row', ' | '.join(txt(v) for v in r)])
assert all(x[-1] for x in group_checks),'Cheque subtotal mismatch'

# Combine all stock in one complete module. Aggregate loc1..loc8 per SKU because one warehouse was explicitly approved.
stock_analysis=[];fg_value=D(0);fg_costval=D(0);rm_value=D(0);fg_quantity=D(0);rm_quantity=D(0);loc_totals={f'loc{x}':D(0) for x in range(1,9)}
for rn,r in records('FinishedGoodsExcel'):
    c=txt(r['itemcode']);qty=sum(num(r[f'loc{x}']) for x in range(1,9));cost=num(r['avar']);source_value=num(r['costval']);candidate=qty*cost
    assert qty==num(r['totqty']),(rn,qty,r['totqty'])
    for k in loc_totals:loc_totals[k]+=num(r[k])
    fg_quantity+=qty;fg_value+=candidate;fg_costval+=source_value
    existing=variants.get(c);unit=''
    if existing:unit=units[products[existing['ProductId']]['UnitOfMeasureId']]['Symbol']
    data=dict(Code=c,Name=txt(r['itemname']),ProductType='FinishedGood',BrandCode=txt(r['brand']),CategoryCode=txt(r['type']),UnitCode=unit,WarehouseCode='WH_MAIN',Quantity=fixed(qty,4),UnitCost=fixed(cost,6),Batch=txt(r['lotno']),SellingPrice=money(r['selprice']),Size=txt(r['size']),Color=txt(r['color']))
    add('Stock',data,'FinishedGoodsExcel',rn,'One summarized layer per SKU; approved sum of loc1..loc8 in WH_MAIN. avar cost approved; no costval substitution. LayerDate/ClosingDate unknown. Existing exact-match base unit only.')
    stock_analysis.append([c,'FinishedGood',rn,fixed(qty,4),fixed(cost,6),money(candidate),money(source_value),money(candidate-source_value),txt(r['loc1']),txt(r['loc3']),existing['Id'] if existing else '',unit])
    if candidate.quantize(D('.01'),rounding=ROUND_HALF_UP)!=source_value:issue('Stock',c,'',rn,'UnitCost','Resolved',money(source_value),'costval differs from approved avar × quantity','User selected avar. Difference retained in Stock valuation report and requires accountant reconciliation to GL.')
    if source_value<0:issue('Stock',c,'',rn,'UnitCost','Resolved',money(source_value),'Negative source costval is not an acceptable stock valuation','User selected nonnegative avar; no absolute-value conversion performed.')
    if not existing:
        issue('Stock',c,'',rn,'UnitCode,BrandCode,CategoryCode','Blocks validation',txt(r['type']),'New finished-goods SKU lacks a source unit and brand and category code lacks a mapped business meaning','Create/match genuine SKU or explicitly approve its base unit, brand and category. Defaults do not resolve nonblank unknown CategoryCode.')
    else:
        product=products[existing['ProductId']];category=categories[product['CategoryId']]
        data['InventoryAccountCode']=accounts[category['InventoryAccountId']]['Code'] if category['InventoryAccountId'] else ''
        datasets['Stock'][-1]['InventoryAccountCode']=data['InventoryAccountCode']
        if product['Type']!=2:issue('Stock',c,'',rn,'ProductType','Blocks validation',product['Type'],'Exact existing SKU product type is not FinishedGood','Resolve product master; do not remap a different legacy identity by similar name.')
        if category['InventoryAccountId']!=1003:issue('Stock',c,'',rn,'InventoryAccountCode','Blocks posting',data['InventoryAccountCode'],'Existing FG category does not use configured 1132 FG inventory','Verify category/warehouse inventory posting account.')
for rn,r in records('RawMaterialExcel'):
    c=txt(r['SKU']);qty=num(r['Quantity']);cost=num(r['UnitCost']);value=qty*cost;rm_quantity+=qty;rm_value+=value
    add('Stock',dict(Code=c,Name=txt(r['ProductName']),ProductType='RawMaterial',BrandCode=txt(r['Brand']),CategoryCode=txt(r['Category']),UnitCode=txt(r['Unit']),WarehouseCode='WH_MAIN',Quantity=fixed(qty,4),UnitCost=fixed(cost,6),ClosingDate=date(r['OpeningDate'])),'RawMaterialExcel',rn,'Raw source quantity/cost unchanged; user approved common WH_MAIN. Source 13 September snapshot retained. No snapshot date substituted for FIFO LayerDate.','2026-09-13','Explicit OpeningDate.')
    existing=variants.get(c)
    unit=''
    if existing:
        product=products[existing['ProductId']];category=categories[product['CategoryId']];unit=units[product['UnitOfMeasureId']]['Symbol']
        datasets['Stock'][-1]['InventoryAccountCode']=accounts[category['InventoryAccountId']]['Code'] if category['InventoryAccountId'] else ''
        if product['Type']!=1:issue('Stock',c,'',rn,'ProductType','Blocks validation',product['Type'],'Existing raw-material SKU product type differs','Correct/confirm existing master.')
        if unit.lower()!=txt(r['Unit']).lower():issue('Stock',c,'',rn,'UnitCode,ConversionFactor','Blocks validation',unit,'Source unit differs from existing base unit','Supply approved source-to-base conversion. Never assume factor from unit label alone.')
        if category['InventoryAccountId']!=1002:issue('Stock',c,'',rn,'InventoryAccountCode','Blocks posting',datasets['Stock'][-1]['InventoryAccountCode'],'Existing RM category does not use configured 1131 RM inventory','Confirm/correct existing category posting account.')
    stock_analysis.append([c,'RawMaterial',rn,fixed(qty,4),fixed(cost,6),money(value),'','','','',existing['Id'] if existing else '',unit])
for rn,r in records('TrialBalance'):
    add('TrialBalance',dict(Code=txt(r['acccod']),Debit=money(r['cfamtdr']),Credit=money(r['cfamtcr'])),'TrialBalance',rn,'Closing debit/credit columns copied as final numeric values, including all 102 zero accounts. Account internal spaces preserved. No balancing plug. Snapshot date unknown.')
    if num(r['cfamtcr'])<0 or num(r['cfamtdr'])<0 or num(r['cfamtcr'])!=0 and num(r['cfamtdr'])!=0:issue('TrialBalance',txt(r['acccod']),'',rn,'Debit,Credit','Blocks validation','','Invalid debit/credit sides','Correct legacy trial balance.')
    assert num(r['cfamt'])==num(r['cfamtdr'])-num(r['cfamtcr']),rn

# Exact supported identity; source row numbers only locate evidence, never identify transactions.
def identity(module,r):
    if r['LegacyRecordId']:return r['LegacyRecordId']
    if module in ['CustomerMaster','SupplierMaster','TrialBalance']:return r['Code']
    if module=='Stock':return json.dumps([r['Code'],r['WarehouseCode'],r['Batch'],r['LayerDate']],separators=(',',':'))
    return json.dumps([r['Code'],r.get('Kind',''),r['Reference'],r['ChequeDate'] if module=='ReceivedCheques' else r['DocumentDate'],r.get('ChequeNumber','')],separators=(',',':'))
identities=[]
for module,rows in datasets.items():
    groups=collections.defaultdict(list)
    for i,r in enumerate(rows,2):
        ident=identity(module,r);groups[ident].append(i);identities.append([module,i,r['Code'],r.get('Reference',''),ident,('Uploaded Stock composite; approved summarized LayerDate is normalized to project closing before posting' if module=='Stock' else 'Supported composite/code identity; source ID not present')])
        for field in ['Name','Contact','Email','Phone','Mobile','Address1','Address2','TaxNumber']:
            maxlen={'Name':200,'Contact':100,'Email':150,'Phone':50,'Mobile':50,'Address1':200,'Address2':200,'TaxNumber':50}[field]
            if len(r.get(field,''))>maxlen:issue(module,r['Code'],'',i,field,'Blocks validation',r[field],f'Exceeds supported {maxlen}-character field','Correct source without silent truncation.')
    for ident,rr in groups.items():
        if len(rr)>1:
            duplicates.append([module,ident,','.join(map(str,rr)),len(rr)])
            issue(module,'','',rr[0],'LegacyRecordId','Blocks validation',ident,'Duplicate supported identity','Obtain permanent transaction IDs or resolve source duplicates; do not invent row-based IDs.')

# Verify both supplied stock file formats, preventing duplicate inventory.
equivalence=[]
for excel,csvkey,numeric in [('FinishedGoodsExcel','FinishedGoodsCSV',{'avar','totqty',*(f'loc{x}' for x in range(1,9)),'packing','costpic',*(f'costl{x}' for x in range(1,8)),'totcost','costval','selprice','minsellpri'}),('RawMaterialExcel','RawMaterialCSV',{'Quantity','UnitCost'})]:
    aa=records(excel);bb=records(csvkey);assert len(aa)==len(bb)
    differences=0
    for (ra,a),(rb,b) in zip(aa,bb):
        for h in a:
            va=num(a[h]) if h in numeric else txt(a[h]);vb=num(b[h]) if h in numeric else txt(b[h])
            if va!=vb:differences+=1;issue('Stock',txt(a.get('itemcode',a.get('SKU',''))),'',ra,h,'Review',str(va)+' / '+str(vb),'Excel/CSV values differ','Use Excel as primary; both original versions archived.')
    equivalence.append([source(excel)['filename'],source(csvkey)['filename'],len(aa),differences,'Excel primary; CSV compared, not appended'])

# Explicit wizard mappings, including matching lookup codes.
for kind,legacy,target_id in [('CustomerGroup','TRADE',13),('PriceLevel','STANDARD TIER',10),('PaymentTerm','NET30',27)]:
    r=next(x for x in M['SystemLookups'] if x['Id']==target_id);assert r['Type']==kind and r['Code']==legacy and r['IsActive']
    select(kind,legacy,target_id,r['Value'],'Confirmed','Explicit mapping required even when code already matches.')
for g in ['A','B','C','G','S']:select('SupplierGroup',g,15,'Domestic Suppliers','Confirmed','User approved all source supplier groups to DOM.')
for legacy,target in [('B450    -001','2110'),('1120','1120'),('4110','4110'),('1151','1151'),('6210','6210'),('6220','6220')]:
    a=acccode[target];select('Account',legacy,a['Id'],target+' — '+a['Name'],'Verified configuration' if legacy==target else 'Reconciliation candidate','Active posting account of type '+TYPE[a['Type']]+'. Preserve internal source-code spaces.')
select('Warehouse','WH_MAIN',3,'Main Garnet','Confirmed','All source locations combined into this approved warehouse.')
select('Unit','Pcs',1,'Piece','Exact label candidate','Required explicit mapping. Source quantities must be pieces; current exact matched base unit must agree.')
for u in sorted({r['UnitCode'] for r in datasets['Stock'] if r['UnitCode']} - {'Pcs'}):
    matches=[x for x in M['UnitOfMeasures'] if x['Symbol']==u]
    if len(matches)==1:select('Unit',u,matches[0]['Id'],matches[0]['Name'],'Exact existing base unit','Copied from exact SKU master, without inventing source conversions.')
select('Brand','UNBRANDED',5,'UNBRANDED','Exact label candidate','Explicit mapping when needed for a new product.')
banks={r['Id']:r for r in M['Banks']};branch_lookup=collections.defaultdict(list)
for b in M['BankBranches']:
    bank=banks[b['BankId']]
    if b['IsActive'] and bank['IsActive']:branch_lookup[bank['BankCode']+'-'+b['BranchCode']].append((b,bank))
for c in sorted({r['BankBranchCode'] for r in datasets['ReceivedCheques']}):
    matches=branch_lookup[c]
    if len(matches)==1:
        b,bank=matches[0];select('BankBranch',c,b['Id'],bank['Name']+' / '+b['BranchName'],'Exact code match','Verify the branch on the physical instrument.')
    else:select('BankBranch',c,'','','Unmatched','Create/verify actual bank branch; do not choose by similar bank name.');issue('ReceivedCheques','',c,0,'BankBranchCode','Blocks validation',c,'No unique current active bank/branch code match','Obtain exact bank/branch mapping.')
for r in datasets['Stock']:
    if r['Code'] in variants:
        v=variants[r['Code']];select('Stock',r['Code'],v['Id'],v['SKU']+' — '+v['Name'],'Exact SKU match','Optional explicit match; Code remains SKU in template.')

defaults=[['Opening equity account','3200 — Retained Earnings','Verified local Equity posting account; project 3 currently selects it.'],['Default customer control','1120 — Accounts Receivable (AR)','Configured Asset posting account.'],['Default customer revenue','4110 — Sales Revenue - FG','Configured Revenue posting account.'],['Default supplier control','2110 — Accounts Payable (AP)','Configured Liability posting account; map B450    -001 to it.'],['Default supplier expense','Leave unresolved','5000 is a folder. 5110 Raw Material Consumption and 5120 Outsourced Job Work Costs are active Expense accounts, but supplier applicability is not established.'],['Undeposited cheque account','1151 — Cheques In Hand','Configured Asset posting account; current SalaryAdvancesReceivable also uses 1151, so confirm GL coverage.'],['Default customer group','Trade (TRADE)','Confirmed, but also add explicit CustomerGroup mapping.'],['Default supplier group','Domestic Suppliers (DOM)','Confirmed; add explicit A/B/C/G/S mappings because source fields are nonblank.'],['Default price level','Standard Tier (STANDARD TIER)','Confirmed; add explicit PriceLevel mapping.'],['Default customer payment term','30 Days (NET30)','Confirmed; explicit NET30 mapping to ID 27. Do not use COD.'],['Default supplier payment term','30 Days (NET30)','Confirmed; explicit NET30 mapping to ID 27.'],['Approved base unit when source unit is missing','Leave unresolved for new FG SKUs','Source lacks unit for 81 unmatched finished goods. Existing matched base unit is retained.'],['Approved warehouse when source location is missing','Main Garnet (WH_MAIN)','Confirmed; populated on every Stock row with explicit mapping.'],['Approved supplier terms in days (optional)','30','Confirmed. Canonical DueDate is already document+30 on every supplier row.'],['Summarized stock layer approval','User approval recorded 11 October 2026','Missing LayerDate deliberately left blank. The validator assigns closing only after approved summarized-layer evidence.'],['Legacy finished goods valuation','Average cost (avar × warehouse quantity)','User confirmed avar. Candidate discrepancy = 3,173,818.42 less than costval; retain signed financial review evidence.']]

# A comprehensive source-to-template field guide, including every blank/default.
for src,target in [('debcode','Code'),('debname','Name'),('debcper','Contact'),('debmail','Email'),('debtel1','Phone'),('debtel2','Mobile'),('debadd1','Address1')]:mapping('CustomerMaster',src,target,'Trim surrounding whitespace only; preserve identifiers as text.'+(' BA366 email is blank by explicit user confirmation.' if target=='Email' else ''))
mapping('CustomerMaster','debadd2 + debadd3','Address2','Join nonblank original address lines with semicolon and space; no truncation or inferred contact number.')
for target,value in [('GroupCode','TRADE'),('PaymentTermCode','NET30'),('PriceLevelCode','STANDARD TIER'),('ReceivableAccountCode','1120'),('RevenueAccountCode','4110')]:mapping('CustomerMaster','User confirmation / current configuration',target,value+'; actual master code, never database ID.')
for src,target in [('supcode','Code'),('supname','Name'),('supcontact','Contact'),('supemail','Email'),('suptele','Phone'),('supadd1','Address1'),('tinno','TaxNumber'),('supgroup','GroupCode'),('supacc','PayableAccountCode')]:mapping('SupplierMaster',src,target,'Trim outer whitespace only. Preserve original codes/internal spaces; A/B/C/G/S explicitly map to DOM.')
mapping('SupplierMaster','supadd2 + supadd3 + supadd4','Address2','Join nonblank lines with semicolon and space.')
mapping('SupplierMaster','User confirmed 30 days','PaymentTermCode','NET30; explicitly map to 30 Days.')
mapping('SupplierMaster','No source / unresolved accounting assignment','ExpenseAccountCode','6210 for RE187/RS149 (explicit rent suppliers), 6220 for EL264 (explicit electricity supplier), others blank. Active Expense posting accounts selected for clear source purposes.','Needs business confirmation')
for module,code,ref,d,amount in [('CustomerOpenings','debcode','ref','tdate','creamt'),('SupplierOpenings','supcode','dtfref','dtfdate','dtfbal')]:
    for src,target in [(code,'Code'),(ref,'Reference'),(d,'DocumentDate'),(amount,'Amount')]:mapping(module,src,target,'Preserve actual value/sign; ISO date or numeric LKR amount. No original-minus-paid calculation.')
    mapping(module,d+' + 30 days','DueDate','Confirmed calendar-day terms.'+(' Original supplier paydate/crdays retained and discrepancies reported.' if module=='SupplierOpenings' else ' Original customer document date retained.'))
    mapping(module,amount+' sign','Kind','Invoice / ReturnCredit for customers; Bill / Credit for suppliers. Negative amount preserved.')
for src,target in [('E / Customer Code','Code'),('C / Ref No','Reference'),('D / Inv No','DocumentReferences'),('H / Cheque Number','ChequeNumber'),('B / Received Date','ReceivedDate'),('G / Bank Code','BankBranchCode'),('I / Amount','Amount')]:mapping('ReceivedCheques',src,target,'Only instrument detail rows recognized by CH receipt reference; retain text references/leading zeros.')
mapping('ReceivedCheques','A / To be Dep Date, within report group','ChequeDate','Carry forward only until Total for subtotal; user confirmed it is cheque face date.')
for field,value in [('Status','InSafe'),('PartyReflected','true'),('BankReflected','false'),('SafeAccountCode','1151')]:mapping('ReceivedCheques','User confirmation / current configuration',field,value)
for src,target in [('itemcode / SKU','Code'),('itemname / ProductName','Name'),('type / Category','CategoryCode'),('brand / Brand','BrandCode'),('lotno / absent','Batch'),('selprice / absent','SellingPrice'),('size / absent','Size'),('color / absent','Color')]:mapping('Stock',src,target,'Finished goods / raw materials respectively. Preserve source code meanings; new product classifications need mapping.')
mapping('Stock','Source file','ProductType','FinishedGood for stock.xls; RawMaterial for Raw Material Stcok.xlsx.')
mapping('Stock','loc1..loc8 / Quantity','Quantity','Sum FG locations per SKU in one approved WH_MAIN layer; use raw Quantity unchanged. Do not also add totqty.')
mapping('Stock','avar / UnitCost','UnitCost','User-approved FG avar; raw supplied cost unchanged. Six decimal places. costval is retained only for reconciliation.')
mapping('Stock','Existing exact SKU base unit / Unit','UnitCode','FG exact-match base unit Symbol where available; new FG blank. Raw Unit retained; explicit Pcs mapping required.')
mapping('Stock','User-approved common warehouse','WarehouseCode','WH_MAIN on all rows, replacing FG loc1/loc3 and raw WH-MAIN. Original locations retained in archive.')
mapping('Stock','Existing exact SKU category posting account','InventoryAccountCode','Actual current account code where available; verify RM Asset 1131 / FG Asset 1132.')
mapping('Stock','Original FIFO dates absent','LayerDate','Blank. Approved summarized layer; never use 13 September snapshot as an invented receipt date.')
mapping('Stock','Source units not proven to differ','ConversionFactor','Blank, which validator treats as 1. Any actual unit difference blocks validation until a verified source-to-base factor is supplied.')
for src,target in [('acccod','Code'),('cfamtdr','Debit'),('cfamtcr','Credit')]:mapping('TrialBalance',src,target,'Preserve actual closing balances, all zero rows and internal code spaces. cfamt checked independently against Debit-Credit.')
for module in datasets:
    mapping(module,'No permanent transaction ID field present','LegacyRecordId','Blank. Use importer-supported code/composite identity; exact duplicate check included. Excel row number is provenance only.')
    if module in ['CustomerMaster','SupplierMaster']:method='2026-09-30 project metadata only; master files contain no opening amounts.'
    elif module in ['CustomerOpenings','ReceivedCheques']:method='2026-09-28, actual report date. HOLD: importer rejects it for 30 September project.'
    elif module=='Stock':method='Raw rows 2026-09-13 from OpeningDate; FG blank because unknown. HOLD complete combined file.'
    elif module=='SupplierOpenings':method='Blank because uncertified/unknown. Supplier ageing separately infers 2026-10-07. HOLD; do not select 30 September in UI to disguise it.'
    else:method='Blank because the trial-balance snapshot date is uncertified. HOLD; obtain a certified 30 September export.'
    mapping(module,'Snapshot evidence','ClosingDate',method)
    mapped={r[2] for r in transformations if r[0]==module}
    for field in headers[module]:
        if field not in mapped:mapping(module,'Not supplied / not applicable',field,'Blank, preserved as missing. Do not fabricate a value.')

global_issues=[
 ['CustomerOpenings','ClosingDate','2026-09-28','Actual ageing snapshot is two days before approved closing','Request complete 30 September open-item export, retaining zero-net offsets.'],
 ['ReceivedCheques','ClosingDate','2026-09-28','Cheque report is two days before approved closing','Refresh full physical/ledger cheque schedule at 30 September; user status confirmation does not refresh amount snapshot.'],
 ['Stock','ClosingDate','Raw 2026-09-13 / FG unknown','Combined file is not a certified 30 September stock position','Refresh both datasets and warehouse counts/costs. One complete combined replacement revision only.'],
 ['SupplierOpenings','ClosingDate','Inferred 2026-10-07','Uniform ageing identifies 7 October, and post-closing documents are present','Request a complete 30 September outstanding export; do not merely remove later documents because earlier items may have settled during October.'],
 ['TrialBalance','ClosingDate','','Source has no certified snapshot date','Request full corrected 30 September TB, including every zero account.'],
 ['TrialBalance','Debit,Credit','Credits exceed debits by 613120.00','Source TB is unbalanced','Accountant must correct/explain source. Do not create an opening-equity or suspense plug.'],
 ['All financial','October activity','','Transactions since 1 October exist only in the old system','Transfer every October transaction separately under controlled normal posting after opening scopes release; the Migration Centre cannot replay full history.'],
 ['Archive','Historical transactions','','Supplied files are snapshots and masters, not a complete pre-cutover transaction history','Retain full old-system backup and transaction/document exports through 30 September in a controlled read-only archive.'],
 ['Financial dependencies','Missing files','BankOpening, BankRecon, IssuedCheques, ControlTotals','These schedules were not supplied','Obtain full closing schedules and independent controls or signed true zero declarations. Do not treat missing files as zero.'],
 ['Accounts','Mapping','188 legacy accounts / 39 active transaction accounts','A complete approved legacy-to-current account mapping/allocation is absent','Create missing genuine accounts in normal maintenance or approve complete mappings and inventory split. Cover every target active posting account.'],
 ['ReceivedCheques','SafeAccountCode','1151','Cheques In Hand and SalaryAdvancesReceivable are configured to the same account','Confirm all balances included under 1151 and reconcile independent safe asset balance.'],
 ['CustomerMaster','PaymentTermCode','COD saved in project 3','Current saved payment-term mapping is cash on delivery, not confirmed 30-day credit','Add NET30 → 30 Days mapping; use prepared NET30 rows. Do not preserve COD as the master term.'],
]
for module,field,value,reason,action in global_issues:issue(module,'','',0,field,'Blocks posting' if module not in ['CustomerMaster','Archive'] else 'Required action',value,reason,action)

amount_totals={}
for module in ['CustomerOpenings','SupplierOpenings','ReceivedCheques']:
    values=[num(r['Amount']) for r in datasets[module]]
    amount_totals[module]={'positive':money(sum((x for x in values if x>0),D(0))),'negative':money(sum((x for x in values if x<0),D(0))),'net':money(sum(values,D(0))),'positive_rows':sum(x>0 for x in values),'negative_rows':sum(x<0 for x in values),'zero_rows':sum(x==0 for x in values)}
tb_dr=sum(num(r['Debit']) for r in datasets['TrialBalance']);tb_cr=sum(num(r['Credit']) for r in datasets['TrialBalance'])
customer_summary=[]
for code,rs in collections.defaultdict(list).items():pass
for code in sorted({r['Code'] for r in datasets['CustomerOpenings']}|{r['Code'] for r in datasets['ReceivedCheques']}):
    ar=[num(r['Amount']) for r in datasets['CustomerOpenings'] if r['Code']==code];ch=[num(r['Amount']) for r in datasets['ReceivedCheques'] if r['Code']==code]
    customer_summary.append([code,len(ar),money(sum((x for x in ar if x>0),D(0))),money(sum((x for x in ar if x<0),D(0))),money(sum(ar,D(0))),len(ch),money(sum(ch,D(0))),'Yes' if ar and sum(ar,D(0))==0 else 'No','Provided' if code in customer_master_codes else 'Missing'])
supplier_summary=[]
for code in sorted({r['Code'] for r in datasets['SupplierOpenings']}):
    rs=[num(r['Amount']) for r in datasets['SupplierOpenings'] if r['Code']==code];supplier_summary.append([code,len(rs),money(sum((x for x in rs if x>0),D(0))),money(sum((x for x in rs if x<0),D(0))),money(sum(rs,D(0))),'Provided' if code in supplier_master_codes else 'Missing'])
status=[]
for module in datasets:
    upload='Yes, staging only' if module.endswith('Master') else 'No: HOLD for refreshed closing export'
    post='No: mapping validation not executed' if module=='CustomerMaster' else 'No: supplier expense assignment unresolved' if module=='SupplierMaster' else 'No: stale/uncertified closing balances and unreconciled controls'
    status.append([module,len(datasets[module]),upload,post,'staging_csv' if module.endswith('Master') else 'held_csv'])
checks=[['Customer AR signed net',amount_totals['CustomerOpenings']['net'],'154029322.14',money(num(amount_totals['CustomerOpenings']['net'])-D('154029322.14')),'Different snapshots; not an approved opening'],['Supplier AP signed net',amount_totals['SupplierOpenings']['net'],'78068405.85',money(num(amount_totals['SupplierOpenings']['net'])-D('78068405.85')),'Different snapshots; supplier signs confirmed'],['Cheques in hand',amount_totals['ReceivedCheques']['net'],'14323736.00',money(num(amount_totals['ReceivedCheques']['net'])-D('14323736')),'GL less schedule = 8,995,108.00'],['Combined stock approved avar + raw',money(fg_value+rm_value),'97626087.76',money(fg_value+rm_value-D('97626087.76')),'Legacy closing-stock account coverage/split requires confirmation'],['TB debit less credit',money(tb_dr),money(tb_cr),money(tb_dr-tb_cr),'Unbalanced source; no plug added']]
SUMMARY={'opening_date':OPENING,'closing_date':CLOSING,'amount_totals':amount_totals,'tb_debit':money(tb_dr),'tb_credit':money(tb_cr),'tb_difference':money(tb_dr-tb_cr),'fg_quantity':fixed(fg_quantity,4),'rm_quantity':fixed(rm_quantity,4),'fg_value':money(fg_value),'fg_source_costval':money(fg_costval),'rm_value':money(rm_value),'combined_stock_value':money(fg_value+rm_value),'source_location_totals':{k:fixed(v,4) for k,v in loc_totals.items()},'duplicate_groups':len(duplicates),'status':status,'postcutover_supplier_rows':len(postcutover),'postcutover_supplier_amount':money(sum((num(r[-1]) for r in postcutover),D(0))),'zero_net_customers':[r[0] for r in customer_summary if r[7]=='Yes'],'stock_unmatched':sum(r['Code'] not in variants for r in datasets['Stock']),'local_project':M['MigrationProjects'],'source_comparisons':equivalence}
(WORK/'summary.json').write_text(json.dumps(SUMMARY,indent=2),encoding='utf-8')

for module,rows in datasets.items():write_csv(ROOT/('staging_csv' if module.endswith('Master') else 'held_csv')/(module+'.csv'),headers[module],rows)
report_tables={
 'Readiness':(['Module','Output rows','Ready to upload','Ready to post','CSV folder'],status),
 'Source inventory':(['Source key','Original file','Sheet','Physical rows','Data rows','Bytes','SHA256'],[[x['key'],x['filename'],s['name'],len(s['rows']),len(s['rows'])-1 if x['key']!='ReceivedCheques' else 49,x['bytes'],x['sha256']] for x in SOURCES for s in x['sheets']]),
 'Reconciliation':(['Comparison','Detail or Debit (LKR)','GL or Credit (LKR)','Difference (LKR)','Interpretation'],checks),
 'Customer totals':(['Code','Documents','Positive debt (LKR)','Negative credit (LKR)','AR net (LKR)','Cheques','Cheque amount (LKR)','Zero net with documents','Master'],customer_summary),
 'Supplier totals':(['Code','Documents','Positive debt (LKR)','Negative credit (LKR)','AP net (LKR)','Master'],supplier_summary),
 'Stock valuation':(['SKU','ProductType','Source row','Quantity','Approved unit cost (LKR)','Approved value (LKR)','Legacy costval (LKR)','Value less costval (LKR)','Original loc1','Original loc3','Existing variant ID','Existing base unit'],stock_analysis),
 'Exceptions':(['Module','Code','Reference','Source row','Field','Severity','Source value','Issue','Action'],exceptions),
 'Wizard mappings':(['Mapping type','Exact legacy code','Local target ID for reference','NexusFlow master label','Status','Reason'],maps),
 'Wizard defaults':(['UI field','Selection','Reason'],defaults),
 'Transformations':(['Module','Source column or rule','Template column','Transformation','Status'],transformations),
 'Row trace':(['Module','Output row','Source file','Source sheet','Source row','Code','Reference','Snapshot date','Snapshot basis','Transformation'],trace),
 'Stable identities':(['Module','Output row','Code','Reference','Supported identity','Identity basis'],identities),
 'Source comparisons':(['Excel file','CSV file','Rows compared','Differing cells after type/whitespace normalization','Treatment'],equivalence),
 'Cheque controls':(['Subtotal row','Face-date group','Computed subtotal (LKR)','Report subtotal (LKR)','Match'],group_checks),
 'Excluded report rows':(['Module','Source row','Reason','Original presentation text'],excluded),
 'Supplier date changes':(['Code','Reference','Source row','DocumentDate','Original paydate','Confirmed DueDate','Source crdays'],ap_due_changes),
 'Supplier October rows':(['Code','Reference','Source row','DocumentDate','Amount (LKR)'],postcutover),
 'Duplicates':(['Module','Identity','Output rows','Count'],duplicates),
 'Current accounts':(['Code','Name','Type','Active','Posting account','Local ID','Balance'],[[r['Code'],r['Name'],TYPE[r['Type']],r['IsActive'],r['IsTransactionAccount'],r['Id'],r['Balance']] for r in M['Accounts']]),
 'Current lookups':(['Type','Code','Label','Active','Local ID'],[[r['Type'],r['Code'],r['Value'],r['IsActive'],r['Id']] for r in M['SystemLookups']]),
}
for name,(head,rows) in report_tables.items():write_csv(ROOT/'reports'/(name.replace(' ','_')+'.csv'),head,rows)
books=[]
for module,rows in datasets.items():books.append({'file':'review_workbooks/'+module+'.xlsx','sheets':[{'name':module,'headers':headers[module],'rows':[[r[h] for h in headers[module]] for r in rows],'template':True}]})
books.append({'file':'review_workbooks/Migration_Review.xlsx','sheets':[{'name':k,'headers':v[0],'rows':v[1],'template':False} for k,v in report_tables.items()]})
archive_sheets=[]
for x in SOURCES:
    for s in x['sheets']:
        archive_sheets.append({'name':x['key'][:31],'headers':s['rows'][0] if x['key']!='ReceivedCheques' else ['Column '+str(i+1) for i in range(10)],'rows':s['rows'][1:] if x['key']!='ReceivedCheques' else s['rows'],'template':True,'raw':True})
books.append({'file':'archive/Legacy_Archive.xlsx','sheets':archive_sheets})
(WORK/'workbooks.json').write_text(json.dumps(books,ensure_ascii=False),encoding='utf-8')
inventory=[{k:x[k] for k in ['key','path','filename','bytes','sha256']} for x in SOURCES]
(ROOT/'archive/source_manifest.json').write_text(json.dumps(inventory,indent=2),encoding='utf-8')
archivezip=ROOT/'archive/Legacy_Originals.zip'
if not archivezip.exists():
    with zipfile.ZipFile(archivezip,'w',zipfile.ZIP_DEFLATED) as z:
        for x in SOURCES:z.write(x['path'],x['key']+'/'+x['filename'])
        z.writestr('source_manifest.json',json.dumps(inventory,indent=2))
with zipfile.ZipFile(archivezip) as z:
    for x in SOURCES:assert hashlib.sha256(z.read(x['key']+'/'+x['filename'])).hexdigest()==x['sha256']

recon=f'''# Migration reconciliation report

Prepared 11 October 2026. User-confirmed NexusFlow opening: **1 October 2026**. Required legacy closing: **30 September 2026**, Asia/Colombo. Currency: LKR.

All supplied versions were read. Values are reconciled to the supplied snapshots, not certified as 30 September balances. No imports, database changes or application code changes were executed.

## Source and output counts

{table_md(['Module','Rows','Upload status','Posting status','CSV folder'],status)}

Customers: 892 source and output rows. Suppliers: 213 source and output rows. Customer open documents: 1,150 across 331 customers, all retained. Supplier open documents: 180 across 52 suppliers, all retained. Cheques: 49 instrument rows across 17 customers, total 5,328,628.00. The cheque sheet has 110 physical rows, 88 nonblank rows: 49 instruments and 39 nonblank presentation/subtotal rows, plus 22 blanks. Every excluded presentation row is listed in the review workbook; the grand total and every group subtotal were checked.

Stock: 494 FG source rows plus 444 RM source rows become **938 combined Stock rows**. All FG warehouse quantities were summed into the approved WH_MAIN layer per SKU. loc1 = 68,206 and loc3 = 780, other locations zero. FG quantity remains 68,986. RM quantity remains 6,306,283; these mixed item quantities are diagnostic counts and not a single physical unit measure. Excel/CSV counterpart rows are compared and never appended twice. No overlapping FG/RM SKU codes and no duplicate supported identities were found.

TB: all 188 distinct legacy account rows retained, including 102 zero rows. Total Debit = {money(tb_dr)}; Credit = {money(tb_cr)}; Debit less Credit = **{money(tb_dr-tb_cr)}**. Every cfamt agrees with cfamtdr − cfamtcr, so header conversion does not solve the imbalance.

## Debts and credits

{table_md(['Module','Positive debts / instruments','Negative credits','Signed net','Positive rows','Negative rows'],[[k,v['positive'],v['negative'],v['net'],v['positive_rows'],v['negative_rows']] for k,v in amount_totals.items()])}

Customer Amount comes only from creamt. Due dates are document date +30. No cheque values are added to AR. No netting or zero-net-customer filtering is performed. Zero-net customers with retained unsettled rows: {', '.join(SUMMARY['zero_net_customers']) or 'none in this supplied snapshot'}.

Supplier Amount preserves dtfbal with user-confirmed signs. Negative balances are Credit (returns). Positive ADVANCE/OVER PAID remarks retain positive Bill amounts by explicit user instruction. These are reported balances; refreshed 30 September exports remain necessary to certify the opening.

## Stock valuation

User selected FG avar average cost: **{money(fg_value)}**. Original costval total: {money(fg_costval)}. Approved candidate less original costval: **{money(fg_value-fg_costval)}**. There are 202 FG valuation mismatches and three negative source costval rows (7, 99, 253); their source values remain archived. RM source valuation is {money(rm_value)}. Combined selected valuation: **{money(fg_value+rm_value)}**. This cost choice resolves preparation treatment, not the difference to the closing GL.

## Source schedule versus supplied GL

{table_md(['Check','Schedule / Debit','GL / Credit','Difference','Interpretation'],checks)}

Do not post a suspense/equity plug for any difference. Invoice/cheque balances stay separate. Legacy B150 closing stock is one account while NexusFlow has separate RM 1131 and FG 1132 assets; an approved allocation and compatible complete TB mapping are still required.

## Dates and unmatched masters

Customer ageing: all 958 type-1/2 age calculations identify **28 September**, with 192 returned-cheque debts not used to infer report date. Cheque title explicitly identifies 28 September. RM OpeningDate is 13 September on all 444 rows. Supplier dtfdate + grndays identifies **7 October** on all 180 rows, an inference rather than a certified report header. Supplier rows after closing: **{len(postcutover)}**, amount {SUMMARY['postcutover_supplier_amount']}. FG and TB dates are uncertified.

Missing customer masters in both source and local legacy-code bindings: GA735, TK1039, UP1038, VI1041. Missing supplier masters: SH331, WL329. The two current customers and four current suppliers have no legacy codes; name similarity is not a valid match. Stock has 857 exact existing SKU matches (413 FG +444 RM); 81 FG SKUs need genuine master/classification/unit completion. Detailed affected rows and amounts are in Exceptions and party-total sheets.

## Evidence and verification limits

Current local masters were read using SELECT only from SAKEELPC / NexusERPLocal at {MASTER['ReadAtUtc']} UTC. Project 3 (test) already exists in Preparing state. Journals are empty and account balances zero. Local http://localhost:5248 refused the browser connection. Hosted https://nexus.garnetnation.lk opened Sign In; authenticated deployed templates, masters and button behavior were not verified. Local template files are checked against current MigrationCatalog code. All template headers, workbook/CSV row equality, identities, signed amounts, ISO dates and formulas are verified in the final verification report.

The supplied source archive is complete for these ten files, but it is not the complete old-system transaction archive. Obtain a full read-only historical export and backup through 30 September, plus a separately controlled transfer of every transaction from 1 October onward.
'''
(ROOT/'Reconciliation_Report.md').write_text(recon,encoding='utf-8')

mapguide=f'''# Source mapping and Migration Centre selections

Prepared for opening {OPENING}, closing {CLOSING}, LKR. The seven workbooks use the existing v1.0 templates with exact ordered headers, including ClosingDate. Database IDs appear only as mapping target references in the review report; template Code fields always contain actual source/master codes.

## Exact source transformations

{table_md(['Module','Source','Template field','Transformation','State'],transformations)}

Unsupported source fields remain in Legacy_Archive.xlsx and the original ZIP. They are not silently discarded or forced into unrelated fields. No permanent transaction-ID column exists in these supplied schedules. Leave LegacyRecordId blank and use the supported composite identity; Stable identities lists every identity and Row trace separately lists source row locations. Stock aggregation into the approved common warehouse avoids identity collisions between loc1 and loc3 for the same SKU. Stable identities records the uploaded Stock composite with a blank LayerDate; after summarized-layer approval the validator sets LayerDate to project closing before computing the final posted identity. This is a stable project-level date, never an Excel row number.

## Exact mappings to add

Use Mapping type → Exact legacy code → NexusFlow master → Add Mapping. These selections are for the current local database and must be rechecked in the destination environment. Every existing Stock match is listed in Migration_Review.xlsx and reports/Wizard_mappings.csv.

{table_md(['Type','Source code','Local target ID','Master label','State','Reason'],[r for r in maps if r[0]!='Stock'])}

**Explicit CustomerGroup, PaymentTerm and PriceLevel mappings are mandatory.** Matching codes do not auto-resolve these lookups. Defaults apply only to blank codes. Supplier GroupCode stays A/B/C/G/S with five explicit mappings to Domestic Suppliers. PaymentTermCode is NET30 → 30 Days (local ID 27). Do not map it to CASH ON DELIVERY (COD), COD, or the saved erroneous TRADECASH ON DELIVERY (COD) alias. NET 30 DAYS (ID 9) is another current 30-day lookup, but the prepared file consistently uses NET30 and its exact mapping.

## Defaults and evidence to save

{table_md(['Field','Selection','Reason'],defaults)}

In Valuation discrepancy resolution evidence record: user confirmed 11 October 2026 to use avar, FG candidate value {money(fg_value)}, costval {money(fg_costval)}, difference {money(fg_value-fg_costval)}. Link this reconciliation and the accountant's final 30 September review. In Summarized stock layer approval record the user's summarized-layer and common-warehouse confirmation. No original layer dates have been invented. Canonical stock uses final UnitCost, so the legacy CandidateUnitCost guard is not carried into upload; external valuation evidence remains necessary.

## Account type checks

1120 AR = active Asset posting account. 2110 AP = active Liability posting account. 4110 FG sales = active Revenue posting account. 1131 RM and 1132 FG inventory = active Asset posting accounts. 1151 Cheques In Hand = active Asset posting account and configured undeposited funds. 3200 Retained Earnings = active Equity posting account, configured Account.Equity.OpeningBalance and already selected in current project 3. 5000 Cost of Goods Sold is an Expense **folder**, not an eligible posting account. 5130 Sales is typed Expense and cannot serve as customer revenue. 5110 Raw Material Consumption and 5120 Outsourced Job Work Costs are eligible Expense posting accounts, but applying either to every domestic supplier would invent supplier business treatment. Remaining supplier ExpenseAccountCode values and default expense remain unresolved pending actual supplier expense assignments or a genuine approved purchasing expense master.

The shared 1151 cheque/salary-advance configuration requires accounting review. InventoryAccountCode must agree with the actual product category and warehouse posting account. An explicit Stock row account cannot override a conflicting category. Raw CategoryCode ACCOSORIES NEW has no exact current category code; existing matched SKUs use their own verified category. A new SKU needs approved classification. Category RAW METERIAL NEW currently posts to FG inventory 1132 and must not be selected as raw inventory solely from its name.

## Complete trial-balance mapping

All 188 legacy codes, names and balances are retained. Internal spaces such as B450    -001 must be mapped exactly. Likely control-account correspondences are B201    -001 →1120, B450    -001 →2110, B525    -001 →1151, P001    -001 →4110 and B806    -001 →3200; accountant approval of account coverage is required. B150    -001 must be allocated between actual RM/FG controls before it can cover both target accounts. No automatic name-based 188-to-39 mapping or forced consolidation has been applied. The full TB must cover every active target transaction account, including genuine zero balances. Do not add invented zero rows or subtract stock/AR/AP/cheques manually: the importer computes the residual from posted migration contributions. If multiple legacy codes consolidate to one target account, approve and prepare a complete consolidated source schedule before residual posting; repeated per-row contribution subtraction must be reviewed against current importer behavior.
'''
(ROOT/'Mapping_Guide.md').write_text(mapguide,encoding='utf-8')

counts=collections.Counter(r[5] for r in exceptions)
exc=f'''# Exceptions and import readiness

Opening date confirmed as 1 October 2026; required legacy closing is 30 September. The files are populated for review. Financial CSVs are held because supplied snapshots are stale or uncertified; none is ready to post. Both master CSVs are structurally ready to stage. CustomerMaster needs saved mappings and destination validation. SupplierMaster additionally needs appropriate supplier Expense posting accounts.

## Outstanding gates

{table_md(['Module','Field','Observed value','Issue','Required action'],global_issues)}

Other row-level gates: four missing customer codes, two missing supplier codes, 81 unmatched FG SKUs with missing unit/brand and unresolved category codes, and actual product type/unit/category-account conflicts listed in reports/Exceptions.csv. Every problem row remains in its template workbook/CSV. Source rows after closing are retained in the held supplier file; removing only those rows cannot recreate an older 30 September balance snapshot.

Supplier due dates are document+30, confirmed by the user. Original paydate differences, including LH109 / GR00001448 (original 19 March before document 20 March), remain visible in Supplier date changes. Negative supplier rows are Credit returns and positive ADVANCE/OVER PAID rows stay positive by explicit instruction. BA366's invalid email was explicitly declared unknown and replaced with a blank. Genuine missing contact/tax details are permitted and flagged for later maintenance.

## Exception states

{table_md(['State','Rows'],sorted(counts.items()))}

Counts are issue records, not distinct source rows. Later completion and resolved records are retained for traceability. Blocks validation and Blocks posting require action before execution. The audit workbook and reports/Exceptions.csv contain exact source row, code, reference, value, reason and action. Final source/output comparison found no duplicate supported identities. Do not manufacture LegacyRecordId from row numbers to bypass a future duplicate.

The user authorized all supplier groups to DOM and requested appropriate expense accounts. Current existing supplier defaults include the ineligible 5000 folder and specialist 5110/5120 accounts. Their applicability to each legacy supplier cannot be established from supplied data. RE187/RS149 explicitly identify rent and use 6210 Rent Expense. EL264 explicitly identifies electricity and uses 6220 Electricity & Utilities. The remaining 210 assignments are **Needs business confirmation**; generic trading/garment supplier names do not establish expense treatment.

## Refreshed exports required

Obtain complete certified 30 September customer unsettled documents (including offsetting debt/credit items), supplier unsettled documents, physical cheques with actual closing status/reflection, raw and finished stock quantities/valuations, full balanced TB including zeros, every bank book/statement/reconciliation position and issued cheques. Rebuild the complete combined Stock revision. Keep originals and report dates unchanged. Obtain independent control schedules and full historical backup/export for the read-only archive. October transactions remain a separate transfer, with reconciliation to the opening and subsequent activity.
'''
(ROOT/'Exceptions_Report.md').write_text(exc,encoding='utf-8')

instructions=f'''# Upload and import instructions

These instructions describe future user actions. No upload, validation request, approval, import, reset or posting was executed during preparation. Actual UI button names are verified in current source; authenticated deployed UI is not verified. Use Administration → Settings & Security → Migration Centre, route /Migration, API /api/migration/projects.

1. Preserve the read-only original archive. Review Reconciliation_Report.md, Exceptions_Report.md, Mapping_Guide.md and Migration_Review.xlsx. Use opening **2026-10-01**, closing **2026-09-30**, LKR and Asia/Colombo. Obtain the refreshed complete closing reports before attempting any held financial CSV.
2. Open the existing saved project. Current local project is **test**, ID 3, Preparing, already using those opening/closing dates. The importer permits only one company migration project. Resume it; do not reset the database or create a duplicate. If no project exists in the actual destination, use New Project, enter a stable existing source-system identifier and Actual go-live opening date, then Create Project. Do not invent a new source identifier on every retry.
3. Select Download Template Pack and compare deployed headers with these files. They were checked against local v1.0 templates and current code. Create missing genuine posting accounts and their parent folders, lookups, warehouses, units, categories, brands, bank branches and missing party/stock masters through normal maintenance, with business review. Do not use database IDs as template codes.
4. For CustomerMaster and SupplierMaster choose Dataset and the corresponding staging_csv file, then Upload Dataset. The XLSX counterparts contain one template sheet with final values and may also be staged. Uploading retains a revision and changes no ledger balances. Do not upload both formats of the same dataset as if they were separate data. The two master files contain 30 September project metadata, not certified balance snapshots.
5. Add every exact mapping in Mapping_Guide.md through Mapping type, Exact legacy code, NexusFlow master and Add Mapping. Add CustomerGroup TRADE, PriceLevel STANDARD TIER, PaymentTerm NET30 explicitly. Replace the legacy COD treatment with confirmed 30-day credit. Map A/B/C/G/S to DOM and B450    -001 to the active AP account 2110. Resolve supplier expense account assignment before execution. Set the verified account defaults, summarized layer approval, average-cost discrepancy evidence and supplier days 30. Select Save Mappings and Evidence. Canonical headers require no source column mapping. If an old column profile exists, remove conflicting entries or set each template field to its identical canonical source header through Map Column before saving.
6. In Required data and zero declarations keep missing financial datasets Awaiting data. Provided is assigned when uploaded. NotApplicable requires a signed actual-zero declaration; blank/empty templates and absent schedules are not proof. Customer scopes require CustomerOpenings and ReceivedCheques; supplier scopes require SupplierOpenings and IssuedCheques; stock scopes require the complete Stock module; bank scopes require BankOpening and BankRecon. For a party with no cheque component, supply independently verified zero component evidence when reconciling that scope.
7. Select CustomerMaster first (or both master modules once all supplier expenses are resolved), leave filters blank for all valid master rows, then Validate Selected Import. Review every normalized row, warning and error. No master row is silently skipped. An existing exact legacy-code master is linked and its current contact data preserved rather than overwritten. Verify the destination's active masters and account types.
8. When validation is clean, review the complete selection, record Approval evidence, select Approve Selected Import and then Execute Approved Import. These actions are for the authorized migration operator later. Mapping, file or selection changes invalidate approval and require validation and approval again. Execution creates masters/openings and, for financial modules, journals. Do not execute from a review-only file.
9. Refresh all financial files at certified 30 September, rebuild from actual data, compare totals and keep every row including zero-net offsets. Select CustomerOpenings and ReceivedCheques in dependency order after genuine customer masters, and SupplierOpenings and IssuedCheques after genuine supplier masters. Use one complete combined Stock.csv containing both FG/RM in WH_MAIN. Uploading another Stock file replaces the current module revision rather than appending. If mappings merge locations, keep one stable summarized layer per SKU or genuine distinct original layers; do not manufacture row IDs.
10. Import Accounts only if a separate authoritative, business-approved account dataset is prepared. Stage complete bank opening/reconciliation and issued-cheque schedules. Received cheques already reduced AR; do not add them to invoice Amount or create historical receipts. InSafe cheques post only the approved safe asset opening, never another bank/customer payment. Deposited/endorsed statuses need different verified source treatment on any refreshed export.
11. Use complete party/SKU/bank scope selections. Each selection includes all rows for the code; do not cherry-pick invoices. Select Validate Selected Import, review proposed journals and source totals, approve, execute and monitor progress with Refresh Progress. If a run fails, inspect its result and Resume through the existing run control; do not blindly execute a second opening. Already imported stable records are recognized, changed posted records require controlled dated correction.
12. Under Reconcile and release scopes enter independent closing controls and row counts, then use the displayed release action. Customer/Supplier positive and negative amounts are separate control keys: CustomerOpenings:Debit, CustomerOpenings:Credit, SupplierOpenings:Debit, SupplierOpenings:Credit. Both debt and credit keys use the full document count for that party, not only the rows on one side. Cheques remain separate keys ReceivedCheques / IssuedCheques. Stock keys are Stock:WH_MAIN:Quantity and Stock:WH_MAIN:Value. Control schedules derived only from these prepared files are diagnostic, not independent approval evidence. Obtain a signed independent ControlTotals schedule before Release Selected Reconciled Scopes for bulk release.
13. After all required subledger, stock and bank scopes are complete, reconciled and released, upload the full corrected TrialBalance. Include every active transaction account and genuine zeros with approved mappings/allocations. Do not manually reduce AR/AP/inventory/bank/equity balances for prior modules; the importer subtracts its own opening journal contributions and rejects managed-control residuals. The present TB is unbalanced and cannot be executed.
14. Select Check Full Ledger. Require zero differences account by account, every current source row imported, and no AwaitingData or pending opening scopes. Record final financial sign-off evidence and select Finalize Migration only when all conditions pass. The project then becomes read-only. Retain the full historical archive independently.
15. Because opening remains 1 October, transfer all transactions recorded only in the old system from 1 October onward through controlled normal workflows after relevant scope release. Reconcile customer, supplier, stock, bank, cheque and GL effects to old-system activity through the actual operational switch. Opening imports do not recreate historical invoices/receipts or replay these transactions. Confirm the business freeze, transaction coverage and transfer method before declaring the live system reconciled.

Files in held_csv deliberately retain true older dates or blanks for unknown snapshot dates. Do not choose 30 September in the upload form to disguise those source positions. These files demonstrate the exact intended template conversion and the work remaining, and are not authorized opening balances.
'''
(ROOT/'Upload_Instructions.md').write_text(instructions,encoding='utf-8')

readme=f'''# NexusFlow migration preparation package

Opening: **1 October 2026**. Required closing: **30 September 2026**. Prepared: 11 October 2026. Currency: LKR.

Start with [Migration review workbook](review_workbooks/Migration_Review.xlsx), [readiness and exceptions](Exceptions_Report.md), [reconciliation](Reconciliation_Report.md), [mapping guide](Mapping_Guide.md), and [upload instructions](Upload_Instructions.md).

{table_md(['Module','Rows','Ready to upload','Ready to post','CSV folder'],status)}

review_workbooks contains seven populated exact-template workbooks and a comprehensive review workbook. staging_csv contains the two master files. held_csv contains five financial datasets with actual source dates/unknown date blanks and all problematic rows retained. reports contains searchable CSV versions of review sheets; those extra-column reports are not upload templates. archive contains a values-only view of every supplied file, byte-identical originals in ZIP, and SHA256/source inventory. Only original source archive files are made read-only; originals at their supplied paths are untouched.

All original source columns are retained in the archive. Unsupported fields are not imported under invented meanings. All CSVs are UTF-8, dates are ISO and numeric values use decimal points. Excel values are final, without formulas. No application source changes, database resets, imports or external uploads were performed.

Master files are structurally ready for staging, not approved for posting. Financial files are held for certified refreshed 30 September exports, independent controls and the recorded exceptions. The supplied snapshots cannot become closing-ready by changing their labels. Supplier appropriate expense assignment and new FG base units/classifications still require business confirmation. The read-only archive preserves these supplied snapshots, but full pre-cutover history must be exported separately.
'''
(ROOT/'README.md').write_text(readme,encoding='utf-8')
print(json.dumps({k:v for k,v in SUMMARY.items() if k not in ['local_project']},indent=2))
