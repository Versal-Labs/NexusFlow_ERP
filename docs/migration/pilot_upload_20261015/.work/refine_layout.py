from pathlib import Path
p=Path("F:/source/NexusFlow_ERP/docs/migration/pilot_upload_20261015/.work/build_review.mjs")
s=p.read_text(encoding="utf-8")
s=s.replace('sh.getRangeByIndexes(1,0,rows.length,n).format.rowHeight=22;','sh.getRangeByIndexes(1,0,rows.length,n).format.rowHeight=44;\n sh.getRangeByIndexes(1,0,rows.length,n).format.wrapText=true;\n sh.getRangeByIndexes(1,0,rows.length,n).format.verticalAlignment="center";')
s=s.replace('h==="Name"?48:h==="LegacyRecordId"?24:h.includes("Account")?24:20;','h==="Name"?60:h==="LegacyRecordId"?42:h==="DocumentReferences"?44:h==="Component"?36:h==="Reference"?28:h==="Email"?35:h.startsWith("Address")?44:h.includes("Account")?26:20;')
p.write_text(s,encoding="utf-8")
