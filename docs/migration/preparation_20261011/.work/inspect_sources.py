import sys, json, csv, hashlib, datetime, re
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent / 'deps'))
import xlrd, openpyxl

BASE = Path(r'C:\Users\user\OneDrive - Negombo Wisdom College\Documents\Versal Labs\Projects\Nexus Flow ERP')
OUT = Path(__file__).parent
FILES = [
    ('Customers', 'System uploads/New Data/customermasterfile.xls'),
    ('Suppliers', 'System uploads/New Data/supliermasterfile.xls'),
    ('CustomerOpenings', 'imports new/cusout.xls'),
    ('SupplierOpenings', 'imports new/supplier outstanding.xlsx'),
    ('ReceivedCheques', 'imports new/cheque in hand.xlsx'),
    ('FinishedGoodsExcel', 'Stock/stock.xls'),
    ('FinishedGoodsCSV', 'Stock/stock.csv'),
    ('RawMaterialExcel', 'Stock/Raw Material Stcok.xlsx'),
    ('RawMaterialCSV', 'Stock/Raw Material Stcok.csv'),
    ('TrialBalance', 'imports new/trial balance.xlsx'),
]

def val(v):
    if isinstance(v, (datetime.datetime, datetime.date)): return v.strftime('%Y-%m-%d')
    return v

def read_file(key, rel):
    p = BASE / rel
    content = p.read_bytes()
    entry = dict(key=key, path=str(p), filename=p.name, bytes=len(content), sha256=hashlib.sha256(content).hexdigest(), sheets=[])
    if p.suffix == '.csv':
        text = content.decode('utf-8-sig')
        rows = list(csv.reader(text.splitlines()))
        entry['sheets'].append(dict(name='CSV', rows=rows, formulas=[], formats={}))
    elif p.suffix == '.xls':
        w = xlrd.open_workbook(str(p), formatting_info=True)
        for s in w.sheets():
            rows=[]; formats={}
            for rn in range(s.nrows):
                row=[]
                for cn in range(s.ncols):
                    c=s.cell(rn,cn); v=c.value
                    fmt=w.format_map[w.xf_list[c.xf_index].format_key].format_str
                    if c.ctype == xlrd.XL_CELL_DATE: v=xlrd.xldate.xldate_as_datetime(v,w.datemode).strftime('%Y-%m-%d')
                    elif c.ctype == xlrd.XL_CELL_NUMBER and re.fullmatch('0+',fmt or ''): v=f'{int(v):0{len(fmt)}d}'
                    if c.ctype == xlrd.XL_CELL_ERROR: v={'excel_error':xlrd.error_text_from_code.get(v,str(v))}
                    if fmt not in ('General','general',''): formats[f'{rn+1}:{cn+1}']=fmt
                    row.append(val(v))
                rows.append(row)
            entry['sheets'].append(dict(name=s.name, rows=rows, formulas=[], formats=formats))
    else:
        w=openpyxl.load_workbook(p,read_only=True,data_only=True)
        wf=openpyxl.load_workbook(p,read_only=True,data_only=False)
        for s in w:
            rows=[]; formats={}
            for rr in s:
                row=[]
                for c in rr:
                    v=c.value
                    if isinstance(v,(int,float)) and re.fullmatch('0+',c.number_format or ''): v=f'{int(v):0{len(c.number_format)}d}'
                    row.append(val(v) if v is not None else '')
                    if getattr(c, 'coordinate', None) and c.number_format not in ('General','general',''): formats[c.coordinate]=c.number_format
                rows.append(row)
            formulas=[dict(cell=c.coordinate,formula=c.value,cached=s[c.coordinate].value) for rr in wf[s.title] for c in rr if c.data_type=='f']
            entry['sheets'].append(dict(name=s.title,rows=rows,formulas=formulas,formats=formats))
    return entry

all_data=[read_file(k,p) for k,p in FILES]
(OUT/'sources.json').write_text(json.dumps(all_data,ensure_ascii=False,default=str),encoding='utf-8')
for e in all_data:
    print('\nFILE',e['key'],e['filename'],e['sha256'])
    for s in e['sheets']:
        rows=s['rows']; nonempty=[(i+1,r) for i,r in enumerate(rows) if any(x != '' and x is not None for x in r)]
        print('SHEET',s['name'],'rows',len(rows),'nonempty',len(nonempty),'cols',max(map(len,rows),default=0),'formulas',len(s['formulas']))
        print('FIRST',json.dumps(nonempty[:7],ensure_ascii=False))
        print('LAST',json.dumps(nonempty[-4:],ensure_ascii=False))
