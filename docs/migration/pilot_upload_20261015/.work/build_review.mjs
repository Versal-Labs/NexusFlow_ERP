import fs from "node:fs/promises";
import path from "node:path";
import {Workbook,SpreadsheetFile} from "@oai/artifact-tool";
const root="F:/source/NexusFlow_ERP/docs/migration/pilot_upload_20261015";
const spec=JSON.parse(await fs.readFile(root+"/.work/review_spec.json","utf8"));
const wb=Workbook.create();
const numeric={
 "Stock posting":[3,4,5],"Expected GL":[2,3,4,5,6,7],
 "Reconciliation":[1,2,3,4,5,6,7]
};
for (const item of spec.sheets){
 const sh=wb.worksheets.add(item.name), n=item.headers.length;
 sh.showGridLines=false; sh.tabColor="#126782";
 const rows=item.rows.map(r=>r.map((v,i)=>numeric[item.name]?.includes(i)&&v!==""?Number(v):v));
 const all=sh.getRangeByIndexes(0,0,rows.length+5,n);
 all.format.font={name:"Arial",size:10,color:"#203445"};
 all.format.verticalAlignment="center";
 all.format.rowHeight=36;
 sh.getRange("A1").values=[[item.name]];
 sh.getRangeByIndexes(0,0,1,n).format.fill="#123047";
 sh.getRangeByIndexes(0,0,1,n).format.font={name:"Arial",size:18,color:"#FFFFFF",bold:true};
 sh.getRangeByIndexes(0,0,1,n).format.rowHeight=34;
 sh.getRange("A2").values=[["CLIENT PILOT • Same finalized local inputs • All amounts LKR"]];
 sh.getRangeByIndexes(1,0,1,n).format.font={name:"Arial",size:10,color:"#126782",bold:true};
 sh.getRange("A3").values=[["Source: successful local project 4 / version 2065; destination validation required."]];
 sh.getRangeByIndexes(2,0,1,n).format.font={name:"Arial",size:9,color:"#627383"};
 sh.getRangeByIndexes(4,0,1,n).values=[item.headers];
 sh.getRangeByIndexes(4,0,1,n).format.fill="#126782";
 sh.getRangeByIndexes(4,0,1,n).format.font={name:"Arial",size:10,color:"#FFFFFF",bold:true};
 sh.getRangeByIndexes(4,0,1,n).format.wrapText=true;
 sh.getRangeByIndexes(4,0,1,n).format.rowHeight=42;
 sh.getRangeByIndexes(5,0,rows.length,n).values=rows;
 sh.getRangeByIndexes(5,0,rows.length,n).format.wrapText=true;
 for(let i=0;i<rows.length;i+=2) sh.getRangeByIndexes(i+5,0,1,n).format.fill="#EDF4F6";
 let widths=Array(n).fill(22);
 if(item.name==="Pilot overview") widths=[30,42,76];
 if(item.name==="Mappings") widths=[20,25,25,46,43,75];
 if(item.name==="Defaults") widths=[48,25,52,85];
 if(item.name==="Stock posting") widths=[19,31,23,10,21,23,28];
 if(item.name==="Expected GL") widths=[17,43,22,22,24,25,25,18];
 if(item.name==="Reconciliation") widths=[24,21,19,24,24,26,22,34,23];
 if(item.name==="Pilot tests") widths=[9,22,46,67,75,16,20,18,42];
 if(item.name==="Assumptions and fixes") widths=[23,30,95,95];
 widths.forEach((w,i)=>sh.getRangeByIndexes(0,i,rows.length+5,1).format.columnWidth=w);
 const nums=numeric[item.name]||[];
 for(const c of nums){
  sh.getRangeByIndexes(5,c,rows.length,1).setNumberFormat((item.name==="Reconciliation"&&[1,2,6,7].includes(c))||(item.name==="Stock posting"&&c===3)?"0":"#,##0.00;(#,##0.00);0.00");
  sh.getRangeByIndexes(5,c,rows.length,1).format.horizontalAlignment="right";
 }
 if(item.name==="Stock posting") sh.getRangeByIndexes(5,4,rows.length,1).setNumberFormat("#,##0.0000");
 if(item.name==="Pilot overview"){
  for(let i=0;i<rows.length;i++)if(typeof rows[i][1]==="number")sh.getRangeByIndexes(5+i,1,1,1).setNumberFormat(i===14?"0":"#,##0.00;(#,##0.00);0.00");
 }
 const tall=["Mappings","Defaults","Pilot tests","Assumptions and fixes"].includes(item.name);
 sh.getRangeByIndexes(5,0,rows.length,n).format.rowHeight=tall?64:40;
 sh.freezePanes.freezeRows(5);sh.freezePanes.freezeColumns(1);
}
wb.recalculate();
console.log(await wb.inspect({kind:"table",range:"Pilot overview!A5:C13",include:"values,formulas",maxChars:2200,tableMaxRows:9,tableMaxCols:3}));
console.log(await wb.inspect({kind:"match",searchTerm:"#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!",options:{useRegex:true,maxResults:20},maxChars:1800}));
await fs.mkdir(root+"/.work/previews",{recursive:true});
for(const item of spec.sheets){
 const last=String.fromCharCode(64+item.headers.length);
 const preview=await wb.render({sheetName:item.name,range:"A1:"+last+"11",scale:1,format:"png"});
 await fs.writeFile(root+"/.work/previews/"+item.name.replaceAll(" ","_")+".png",new Uint8Array(await preview.arrayBuffer()));
 console.log("Rendered "+item.name);
}
await (await SpreadsheetFile.exportXlsx(wb)).save(root+"/review_workbooks/Pilot_Review.xlsx");
console.log("Created Pilot_Review.xlsx: "+spec.sheets.length+" sheets");

if(process.argv.includes("--review-only"))process.exit(0);
const canonical=JSON.parse(await fs.readFile(root+"/.work/canonical_workbooks.json","utf8"));
const numericHeaders=new Set(["Amount","Quantity","UnitCost","ConversionFactor","SellingPrice","StatementBalance","Debit","Credit","RowCount"]);
for(const dataset of canonical){
 const out=Workbook.create();const sh=out.worksheets.add(dataset.module);
 const n=dataset.headers.length;const rows=dataset.rows.map(r=>r.map((v,i)=>numericHeaders.has(dataset.headers[i])&&v!==""?Number(v):v));
 sh.showGridLines=false;
 sh.getRangeByIndexes(0,0,rows.length+1,n).format.font={name:"Arial",size:10,color:"#203445"};
 sh.getRangeByIndexes(0,0,1,n).values=[dataset.headers];
 sh.getRangeByIndexes(0,0,1,n).format.fill="#126782";
 sh.getRangeByIndexes(0,0,1,n).format.font={name:"Arial",size:10,color:"#FFFFFF",bold:true};
 sh.getRangeByIndexes(0,0,1,n).format.wrapText=true;
 sh.getRangeByIndexes(0,0,1,n).format.rowHeight=38;
 sh.getRangeByIndexes(1,0,rows.length,n).values=rows;
 sh.getRangeByIndexes(1,0,rows.length,n).format.rowHeight=44;
 sh.getRangeByIndexes(1,0,rows.length,n).format.wrapText=true;
 sh.getRangeByIndexes(1,0,rows.length,n).format.verticalAlignment="center";
 for(let c=0;c<n;c++){
  const h=dataset.headers[c];const col=sh.getRangeByIndexes(0,c,rows.length+1,1);
  col.format.columnWidth=h==="Name"?60:h==="LegacyRecordId"?42:h==="DocumentReferences"?44:h==="Component"?36:h==="Reference"?28:h==="Email"?35:h.startsWith("Address")?44:h.includes("Account")?26:20;
  const cells=sh.getRangeByIndexes(1,c,rows.length,1);
  if(numericHeaders.has(h)) cells.setNumberFormat(h==="RowCount"||h==="ConversionFactor"?"0":h==="UnitCost"?"0.000000":h==="Quantity"?"0.0000":"0.00");
  else cells.setNumberFormat("@");
 }
 sh.freezePanes.freezeRows(1);sh.freezePanes.freezeColumns(2);
 out.recalculate();
 const check=await out.inspect({kind:"match",searchTerm:"#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!",options:{useRegex:true,maxResults:5},maxChars:500});
 if(check.records.some(r=>r.kind==="match"))throw new Error("Formula error in "+dataset.module);
 const preview=await out.render({sheetName:dataset.module,range:"A1:"+String.fromCharCode(64+Math.min(n,7))+"5",scale:1,format:"png"});
 await fs.writeFile(root+"/.work/previews/Data_"+dataset.module+".png",new Uint8Array(await preview.arrayBuffer()));
 await (await SpreadsheetFile.exportXlsx(out)).save(root+"/review_workbooks/"+dataset.module+".xlsx");
 console.log("Rebuilt "+dataset.module+": "+rows.length+" rows; ISO dates as text");
}
