import fs from 'node:fs/promises';
import path from 'node:path';
import { Workbook, SpreadsheetFile } from '@oai/artifact-tool';

const root=path.resolve(process.env.NEXUS_WORKBOOK_OUTPUT_ROOT || 'docs/migration/preparation_20261011');
const specs=JSON.parse(await fs.readFile(path.join(root,'.work/workbooks.json'),'utf8'));
const numericFields=new Set(['Amount','Quantity','UnitCost','SellingPrice','ConversionFactor','Debit','Credit','StatementBalance','RowCount']);
const dateFields=new Set(['DocumentDate','DueDate','ChequeDate','ReceivedDate','LayerDate','ClosingDate']);
const numberRx=/^-?\d+(\.\d+)?$/;
const isoRx=/^\d{4}-\d{2}-\d{2}$/;
function col(n){let x='';for(n++;n>0;n=Math.floor((n-1)/26))x=String.fromCharCode(65+(n-1)%26)+x;return x;}
function typed(value,header,spec){
  if(value===''||value==null)return null;
  if(spec.raw)return value;
  if(dateFields.has(header)&&isoRx.test(String(value)))return new Date(String(value)+'T00:00:00Z');
  const numeric=spec.template?numericFields.has(header):/LKR|Quantity|Rows$|row$|row count|^Documents$|^Cheques$|^Bytes$|^Physical rows$|^Data rows$|^Local ID$|^Output row$|^Source row$|^Subtotal row$|^Count$|^Balance$|^Original loc[13]$/i.test(header);
  if(numeric&&numberRx.test(String(value)))return Number(value);
  return value;
}
for(const spec of specs.filter(s=>process.argv.length<3 || process.argv.slice(2).includes(path.basename(s.file,'.xlsx')))){
  const wb=Workbook.create();
  for(const ss of spec.sheets){
    const sh=wb.worksheets.add(ss.name);sh.showGridLines=false;
    const width=ss.headers.length;
    const matrix=[ss.headers,...ss.rows.map(r=>ss.headers.map((h,i)=>typed(r[i],h,ss)))];
    sh.getRange(`A1:${col(width-1)}${matrix.length}`).values=matrix;
    const used=sh.getRange(`A1:${col(width-1)}${matrix.length}`);
    used.format.font={name:'Arial',size:10,color:'#17243A'};
    used.format.rowHeight=22;used.format.verticalAlignment='center';used.format.wrapText=false;
    const head=sh.getRange(`A1:${col(width-1)}1`);
    head.format.fill='#243B59';head.format.font={name:'Arial',size:10,bold:true,color:'#FFFFFF'};
    head.format.horizontalAlignment='center';head.format.rowHeight=28;
    head.format.borders={insideVertical:{style:'thin',color:'#FFFFFF'},bottom:{style:'thin',color:'#D5DFEA'}};
    sh.freezePanes.freezeRows(1);
    if(ss.name==='Exceptions'||ss.name==='Row trace'||ss.name==='Stock')sh.freezePanes.freezeColumns(2);
    for(let i=0;i<width;i++){
      const h=ss.headers[i];const rg=sh.getRange(`${col(i)}1:${col(i)}${matrix.length}`);
      const maxLen=Math.max(h.length,...ss.rows.map(r=>String(r[i]??'').length));
      rg.format.columnWidth=Math.max(15,Math.min(ss.template?70:145,maxLen+3));
      if(matrix.length>1){
        const body=sh.getRange(`${col(i)}2:${col(i)}${matrix.length}`);
        const numeric=typeof matrix.find((r,j)=>j>0&&r[i]!==null)?.[i]==='number';
        body.format.horizontalAlignment=numeric?'right':'left';
        if(dateFields.has(h)&&!ss.raw)body.setNumberFormat('yyyy-mm-dd');
        else if((ss.template&&numericFields.has(h))||(!ss.template&&numeric))body.setNumberFormat(/Quantity/i.test(h)?'0.0000':h==='UnitCost'||/unit cost/i.test(h)?'0.000000':/Rows$|row$|Documents$|Cheques$|Bytes$|Local ID$|Count$/i.test(h)?'0':'#,##0.00;[Red](#,##0.00);0.00');
        else if(!numeric)body.setNumberFormat('@');
      }
    }
    if(ss.template&&!ss.raw){
      for(let i=0;i<width;i++){
        const h=ss.headers[i];if(['Code','LegacyRecordId','Reference','ChequeNumber','BankBranchCode','PayableAccountCode','ReceivableAccountCode','RevenueAccountCode','InventoryAccountCode','GroupCode','PaymentTermCode','PriceLevelCode'].includes(h)&&matrix.length>1)sh.getRange(`${col(i)}2:${col(i)}${matrix.length}`).setNumberFormat('@');
      }
    }
  }
  wb.recalculate();
  const first=spec.sheets[0];
  const rendered=await wb.render({sheetName:first.name,range:`A1:${col(Math.min(first.headers.length,8)-1)}${Math.min(first.rows.length+1,12)}`,scale:1,format:'png'});
  await fs.writeFile(path.join(root,'.work/previews',path.basename(spec.file,'.xlsx')+'.png'),new Uint8Array(await rendered.arrayBuffer()));
  if(process.env.NEXUS_RENDER_ALL_SHEETS==='1') {
    for(const ss of spec.sheets.slice(1)) {
      const preview=await wb.render({sheetName:ss.name,range:`A1:${col(Math.min(ss.headers.length,7)-1)}${Math.min(ss.rows.length+1,8)}`,scale:1,format:'png'});
      await fs.writeFile(path.join(root,'.work/previews',path.basename(spec.file,'.xlsx')+'_'+ss.name.replaceAll(' ','_')+'.png'),new Uint8Array(await preview.arrayBuffer()));
    }
  }
  if(process.env.NEXUS_RENDER_ONLY==='1')continue;
  const inspected=await wb.inspect({kind:'region',sheetId:first.name,range:`A1:C${Math.min(first.rows.length+1,4)}`,maxChars:1200,tableMaxRows:4,tableMaxCols:3});
  await fs.writeFile(path.join(root,'.work',path.basename(spec.file,'.xlsx')+'_inspect.json'),JSON.stringify(inspected));
  const out=await SpreadsheetFile.exportXlsx(wb);await out.save(path.join(root,spec.file));
  console.log(JSON.stringify({file:spec.file,sheets:spec.sheets.length,firstRows:first.rows.length}));
}
