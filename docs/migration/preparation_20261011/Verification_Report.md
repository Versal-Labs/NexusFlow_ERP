# File verification

Checked 11 October 2026. These checks confirm file conversion and completeness, not financial posting readiness.

- Nine populated Excel workbooks verified, with seven exact-template sheets.
- All seven UTF-8 CSVs match workbook values and source-derived output matrices exactly.
- Template headers match current MigrationCatalog and supplied v1.0 templates.
- Every code and cheque number is text, dates are typed and displayed yyyy-MM-dd, numeric values are typed, and no workbook contains formulas.
- All ten original files retain their original SHA256 hashes. Every ZIP member matches its original file.
- All rows, zero balances, debt/credit signs, zero-net customer documents and source-report subtotal exclusions are verified. No duplicate supported identities.
- Financial files remain held for refreshed closing exports and the exceptions report. No import, approval or posting was executed.

| Workbook | Rows | Header check | CSV comparison | Formulas |
|---|---:|---|---|---|
| CustomerMaster | 892 | Exact | Equal | None |
| SupplierMaster | 213 | Exact | Equal | None |
| CustomerOpenings | 1150 | Exact | Equal | None |
| SupplierOpenings | 180 | Exact | Equal | None |
| ReceivedCheques | 49 | Exact | Equal | None |
| Stock | 938 | Exact | Equal | None |
| TrialBalance | 188 | Exact | Equal | None |
| Migration_Review | 11616 | Report/archive | Compared to generated matrices | None |
| Legacy_Archive | 4609 | Report/archive | Compared to generated matrices | None |
Original ZIP copies preserve exact bytes. Text line endings in the values-only archive workbook are normalized to LF by Excel XML; content comparison accounts for this normalization. Worksheet previews were visually inspected for all nine workbooks. The CSV and XLSX values were checked independently of these previews.
