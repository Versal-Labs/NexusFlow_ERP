from pathlib import Path
import csv,json
P=Path("F:/source/NexusFlow_ERP/docs/migration/pilot_upload_20261015")
with (P/"reports/Historical_Source_column_mappings.csv").open(encoding="utf-8-sig",newline="") as f:
 d=csv.DictReader(f);rows=list(d);headers=d.fieldnames
for r in rows:
 for k in headers:r[k]=r[k].replace("Final UAT","Final pilot").replace("Applied for UAT","Retained for pilot").replace("Explicit UAT 2026-09-30; original dates preserved.","Explicit pilot 2026-09-30 metadata; original snapshot dates archived, not relabelled as certified closing evidence.")
headers2=[h.replace("Final UAT","Final pilot") for h in headers]
with (P/"reports/Source_To_Template_Mapping.csv").open("w",encoding="utf-8",newline="") as f:
 w=csv.writer(f);w.writerow(headers2);w.writerows([[r[h] for h in headers] for r in rows])
p=P/"README_START_HERE.md"
s=p.read_text(encoding="utf-8").replace("- Preserved all twelve successful local CSVs byte-for-byte, and rebuilt all twelve matching Excel workbooks with importer-safe ISO date text. No new quantity, price, balance or identity was invented.","- Preserved all twelve successful local CSVs byte-for-byte. Rebuilt their twelve matching Excel workbooks with importer-safe ISO date text after the current reader rejected the older date-cell representation. No new quantity, price, balance or identity was invented.")
p.write_text(s,encoding="utf-8")
report="""
# Pilot package verification — 15 October 2026

## What passed

| Check | Result | Evidence |
|---|---|---|
| Successful local baseline | Finalized project 4, version 2065; 2455 Posted units; 2050 Released scopes | SELECT-only local inspection; exported private snapshot excluded from handoff |
| CSV provenance | All 12 CSV SHA256 hashes equal the current files in the finalized local project | Successful_Local_Baseline.csv |
| Exact template headers | All 12 CSVs match current MigrationCatalog field order plus ClosingDate | Importer_Reader_Verification.json |
| Current importer reading | All 12 CSVs and 12 rebuilt XLSX files parse without reader errors | Importer_Reader_Verification.json |
| CSV / Excel equivalence | Every field matches, using decimal comparison only for numeric amount/quantity fields; codes, dates, cheque numbers and IDs compared as exact strings | Importer_Reader_Verification.json |
| Identity safety | No duplicate identities under current MigrationCatalog.Identity after code normalization | Stable_Identities.csv; Importer_Reader_Verification.json |
| Dates and terms | Canonical date values yyyy-MM-dd; all party due dates document date +30 days | Independent_Data_Checks.json |
| Party references | All outstanding and cheque codes match included party masters | Independent_Data_Checks.json |
| Controls | Independently recomputed all 5212 component amounts AND row counts; zero differences | Independent_Data_Checks.json |
| Stock | 938 rows; single WH_MAIN; RM 29727743.70 + FG 84689502.04 =114417245.74 | Reconciliation.csv; Stock_Setup_Reference.csv |
| Bank | 100000.00 =112500.00 +2500.00 -15000.00 | Independent_Data_Checks.json |
| Trial balance | 39 accounts; debit and credit each 282960858.88; local posted contribution difference zero on every account | Expected_GL.csv |
| Values-only workbooks | 13 XLSX workbooks, no formulas/macros/external links/error cells; 12 template workbooks use ISO text date cells | Independent_Data_Checks.json |
| Visual review | Rendered all 8 review sheets and selected data rows on each of the 12 module worksheets; widened/wrapped long text | Preparation previews kept outside delivery |
| Stage ZIP contract | 7 manifests Version 1.0 / SourceSystem TESTTTT / ClosingDate 2026-09-30; root filenames and CSV payloads match | Importer_Reader_Verification.json |
| Original preservation | Source archive and original manifest unchanged | Package_SHA256.csv and retained original files |

## Corrections and limits

The older Excel date cells produced DateTimeOffset-style text in ExcelDataReader, which current NormalizeDate rejects. All twelve Excel workbooks were rebuilt using ISO date strings and then re-read through the actual importer. Upload CSV bytes were retained unchanged.

The old mapping guide omitted ACCESSORIES and 33 lower-case existing-SKU aliases. The new references include them, plus separate 1131/1132 posting-account setup and all 28 used categories. These references must still be saved against the destination's actual masters.

The hosted authenticated UI was not inspectable: browser tooling failed to initialize. Button labels, routes, field contracts and processing behavior were cross-checked with current source, and the successful local database was inspected read-only. Hosted account IDs, exact master presence, role assignments, financial period, application build and cleanliness are not certified by these file checks.

The package is ready to upload for pilot validation. It is ready to post only after error-free destination validation and acceptance of the disclosed pilot assumptions. It is not a signed real financial closing position.

The temporary file-reader harness built successfully with existing package vulnerability warnings from System.Security.Cryptography.Pkcs 9.0.13 in the repository dependency graph; no dependency or application source was changed for this preparation.

## Operator checks still required

1. Confirm the disposable test-data cleanup result separately; this task executed no Azure cleanup.
2. Run Target_Readiness_ReadOnly.sql and compare required master/account definitions.
3. Confirm the deployed build includes the migration performance and dashboard fixes.
4. Save destination mappings/defaults, then validate/approve/execute each stage.
5. Review all release controls and account-level differences before pilot finalization.
6. Capture the baseline before normal pilot transactions, then execute and record the acceptance tests.
"""
(P/"reports/Verification_Report.md").write_text(report.strip()+"\n",encoding="utf-8")
# Record the discovered workbook issue in the consolidated exceptions report as corrected.
f=P/"reports/Exceptions.csv"
with f.open("a",encoding="utf-8",newline="") as a:
 csv.writer(a).writerow(["XLSX-001","Corrected","Old typed date cells were rejected by current reader","Rebuilt 12 XLSX files using ISO text dates; all actual-reader comparisons passed","Importer_Reader_Verification.json"])
print("Updated source-to-template guide and verification evidence.")
