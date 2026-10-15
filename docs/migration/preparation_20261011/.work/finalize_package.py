import csv,hashlib,json,os,stat,zipfile
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
REPO=ROOT.parents[2]
codefiles=[
 'NexusFlow.AppCore/Features/Migration/MigrationModels.cs',
 'NexusFlow.AppCore/Features/Migration/MigrationValidation.cs',
 'NexusFlow.AppCore/Features/Migration/MigrationCentreService.cs',
 'NexusFlow.AppCore/Features/Migration/MigrationPosting.cs',
 'NexusFlow.AppCore/Features/Migration/MigrationReconciliation.cs',
 'NexusFlow.Infrastructure/Services/MigrationFileReader.cs',
 'NexusFlow.Infrastructure/Services/CustomerMigrationFileReader.cs',
 'NexusFlow.Web/Controllers/Api/MigrationProjectsController.cs',
 'NexusFlow.Web/Controllers/MigrationController.cs',
 'NexusFlow.Web/Views/Migration/Index.cshtml',
 'NexusFlow.Web/wwwroot/js/migration/centre.js',
 'NexusFlow.AppCore/Constants/Permissions.cs',
]
codefiles += [str(x.relative_to(REPO)).replace('\\','/') for x in (REPO/'docs/migration/templates').glob('*.csv')]
with (ROOT/'reports/Application_source_evidence.csv').open('w',encoding='utf-8',newline='') as f:
    w=csv.writer(f);w.writerow(['File relative to repository','Bytes','SHA256','Checked date'])
    for name in codefiles:
        data=(REPO/name).read_bytes();w.writerow([name,len(data),hashlib.sha256(data).hexdigest(),'2026-10-11'])
modules=['CustomerMaster','SupplierMaster','CustomerOpenings','SupplierOpenings','ReceivedCheques','Stock','TrialBalance']
readme=(ROOT/'README.md').read_text(encoding='utf-8')
if '## Download and individual files' not in readme:
    readme+='\n## Download and individual files\n\nThe NexusFlow_Migration_Review_20261011.zip file is a review/download bundle. It is not a Migration Centre bulk-upload package. Extract it and use only the correct dataset CSV after resolving its readiness gates.\n\n'
    readme+='| Dataset | Review workbook | Matching UTF-8 CSV | Status |\n|---|---|---|---|\n'
    for module in modules:
        folder='staging_csv' if module.endswith('Master') else 'held_csv'
        readme+='| '+module+' | [Excel](review_workbooks/'+module+'.xlsx) | [CSV]('+folder+'/'+module+'.csv) | '+('Staging only' if module.endswith('Master') else 'HOLD')+' |\n'
    readme+='\nSee [file verification](Verification_Report.md) and [application verification](Source_Verification.md). The read-only [archive workbook](archive/Legacy_Archive.xlsx) and [original-files ZIP](archive/Legacy_Originals.zip) preserve the supplied source evidence. Every source file and row is accounted for in the review workbook.\n'
    (ROOT/'README.md').write_text(readme,encoding='utf-8')
verify=ROOT/'Verification_Report.md'
text=verify.read_text(encoding='utf-8')
text+='\nOriginal ZIP copies preserve exact bytes. Text line endings in the values-only archive workbook are normalized to LF by Excel XML; content comparison accounts for this normalization. Worksheet previews were visually inspected for all nine workbooks. The CSV and XLSX values were checked independently of these previews.\n'
verify.write_text(text,encoding='utf-8')
for folder in ['review_workbooks','archive']:
    for source in (ROOT/folder).glob('*.inspect.ndjson'):
        target=ROOT/'.work'/source.name
        if target.exists():raise RuntimeError('Support file already exists: '+str(target))
        source.rename(target)
for name in ['Legacy_Originals.zip','Legacy_Archive.xlsx','source_manifest.json']:
    os.chmod(ROOT/'archive'/name,stat.S_IREAD)
files=sorted([p for p in ROOT.iterdir() if p.is_file() and p.suffix in {'.md','.json'}])
for folder in ['review_workbooks','staging_csv','held_csv','reports','archive']:
    files+=sorted(p for p in (ROOT/folder).iterdir() if p.is_file() and not p.name.endswith('.inspect.ndjson'))
manifest=ROOT/'Package_Manifest.csv'
manifestrows=[]
for p in files:
    rel=str(p.relative_to(ROOT)).replace('\\','/')
    status='Staging only' if rel.startswith('staging_csv/') else 'HOLD: review only' if rel.startswith('held_csv/') else 'Read-only supplied source archive' if rel.startswith('archive/') else 'Review/instructions: not an upload template'
    manifestrows.append([rel,p.stat().st_size,hashlib.sha256(p.read_bytes()).hexdigest(),status])
with manifest.open('w',encoding='utf-8',newline='') as f:
    w=csv.writer(f);w.writerow(['File','Bytes','SHA256','Readiness']);w.writerows(manifestrows)
files.append(manifest)
package=ROOT/'NexusFlow_Migration_Review_20261011.zip'
with zipfile.ZipFile(package,'w',zipfile.ZIP_DEFLATED) as z:
    for p in files:z.write(p,str(p.relative_to(ROOT)).replace('\\','/'))
with zipfile.ZipFile(package) as z:
    assert z.testzip() is None
    assert len(z.namelist())==len(files)
    for rel,size,digest,status in manifestrows:
        raw=z.read(rel);assert len(raw)==size and hashlib.sha256(raw).hexdigest()==digest,rel
    assert not any('.work' in p or p.endswith('.inspect.ndjson') for p in z.namelist())
    assert 'manifest.json' not in z.namelist()
assert len([p for p in files if p.suffix=='.xlsx'])==9
assert len([p for p in files if p.parent.name in ['staging_csv','held_csv'] and p.suffix=='.csv'])==7
print(json.dumps({'package':str(package),'bytes':package.stat().st_size,'files':len(files),'workbooks':9,'dataset_csvs':7,'archive_read_only':all(not os.stat(ROOT/'archive'/n).st_mode & stat.S_IWRITE for n in ['Legacy_Originals.zip','Legacy_Archive.xlsx','source_manifest.json']),'package_hashes_verified':True},indent=2))