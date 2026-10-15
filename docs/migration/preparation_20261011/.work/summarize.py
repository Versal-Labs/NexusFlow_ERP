import json, collections, datetime, re
from decimal import Decimal as D
from pathlib import Path
p=Path(__file__).parent
src=json.loads((p/'sources.json').read_text(encoding='utf-8'))
def rows(key):
    s=next(x for x in src if x['key']==key)['sheets'][0]['rows']; return [dict(zip(s[0],r)) for r in s[1:]]
def num(v):return D(str(v))
def summarize(key,field,code):
    r=rows(key); values=[num(x[field]) for x in r]; print(key,len(r),'codes',len({x[code] for x in r}),'positive',sum(x for x in values if x>0),'negative',sum(x for x in values if x<0),'net',sum(values),'zero',values.count(0))
summarize('CustomerOpenings','creamt','debcode');summarize('SupplierOpenings','dtfbal','supcode')
c=rows('CustomerOpenings');s=rows('SupplierOpenings');fg=rows('FinishedGoodsExcel');rm=rows('RawMaterialExcel');tb=rows('TrialBalance')
print('customer dates',collections.Counter((datetime.date.fromisoformat(r['tdate'])+datetime.timedelta(days=int(r['days']))).isoformat() for r in c if r['xtyp'] in ('1','2')))
print('supplier dates',collections.Counter((datetime.date.fromisoformat(r['dtfdate'])+datetime.timedelta(days=int(r['grndays']))).isoformat() for r in s))
print('customer unmatched',sorted({r['debcode'] for r in c}-{r['debcode'] for r in rows('Customers')}))
print('supplier unmatched',sorted({r['supcode'] for r in s}-{r['supcode'] for r in rows('Suppliers')}))
print('FG',len(fg),'qty',sum(num(r['totqty']) for r in fg),'average',sum(num(r['avar'])*num(r['totqty']) for r in fg),'costval',sum(num(r['costval']) for r in fg),'zero_qty',sum(num(r['totqty'])==0 for r in fg))
print('RM',len(rm),'qty',sum(num(r['Quantity']) for r in rm),'value',sum(num(r['Quantity'])*num(r['UnitCost']) for r in rm))
print('TB',len(tb),'dr',sum(num(r['cfamtdr']) for r in tb),'cr',sum(num(r['cfamtcr']) for r in tb),'zero',sum(num(r['cfamtdr'])==num(r['cfamtcr'])==0 for r in tb))
for key,cols in [('Customers',['debadd3','debmail']),('Suppliers',['supadd3','supadd4','tinno','vatno','paycode','supgroup']),('CustomerOpenings',['xtyp']),('SupplierOpenings',['dtftype'])]:
    for col in cols:
        count=collections.Counter(str(r[col]).strip() for r in rows(key));print(key,col,len(count),'nonblank',sum(v for k,v in count.items() if k),'values',list(count.items())[:20])
for key,col in [('CustomerOpenings','ref'),('SupplierOpenings','dtfref')]:
    r=rows(key); counts=collections.Counter(x[col] for x in r);print(key,'duplicate refs',[(k,v) for k,v in counts.items() if v>1])
print('TB account descriptors',json.dumps([[r['acccod'],r['accdes'],r['accsc1'],r['cfamtdr'],r['cfamtcr']] for r in tb if num(r['cfamtcr'])!=0 or re.search('stock|debtor|creditor|cheque|opening|sale|purchase',r['accdes'],re.I)]))
if (p/'local_masters.json').exists():
    m=json.loads((p/'local_masters.json').read_text(encoding='utf-8-sig'))
    for k in ['Accounts','SystemConfigs','SystemLookups','Warehouses','UnitOfMeasures','Brands','Categories','Customers','Suppliers','MigrationProjects','FinancialPeriods']:
        print('CURRENT',k,json.dumps(m['Tables'][k],default=str))
