# Application and template verification

Verified against the files in the current local checkout on 11 October 2026. This is preparation evidence; no application validation, import, approval, ledger posting or reset was executed. The source-file hashes are recorded in reports/Application_source_evidence.csv so these findings can be rechecked if the application changes.

## Evidence checked

| Source | What it establishes |
|---|---|
| NexusFlow.AppCore/Features/Migration/MigrationModels.cs | MigrationCatalog v1.0, supported fields, financial modules, scope types and stable identities. |
| docs/migration/templates/*.csv | Exact ordered headers for the seven populated datasets, including ClosingDate. |
| NexusFlow.Infrastructure/Services/MigrationFileReader.cs | Canonical CSV/XLSX parsing, legacy adapters, maximum 20 MB and 20,000 normalized rows per dataset. |
| NexusFlow.Infrastructure/Services/CustomerMigrationFileReader.cs | Legacy customer outstanding and cheque-report interpretation. |
| NexusFlow.AppCore/Features/Migration/MigrationCentreService.cs | Project dates, explicit mappings/defaults, date checks, dataset replacement revisions and scope requirements. |
| NexusFlow.AppCore/Features/Migration/MigrationValidation.cs | Field validation, account types, proposed journals, summarized layers, party/SKU checks and residual trial-balance rules. |
| NexusFlow.AppCore/Features/Migration/MigrationPosting.cs | Master linking/creation and financial opening execution. |
| NexusFlow.AppCore/Features/Migration/MigrationReconciliation.cs | Independent reconciliation controls, scope release and full-ledger completion. |
| NexusFlow.Web/Controllers/Api/MigrationProjectsController.cs | API routes, upload/package rules and permissions. |
| NexusFlow.Web/Views/Migration/Index.cshtml and wwwroot/js/migration/centre.js | Actual page controls, dropdown/default names and action buttons. |

## Validation rules affecting these files

- Every row needs a nonblank actual Code of up to 100 characters. Party master codes are limited to 50 characters and require an actual Name of up to 200. A master dataset must have one row per code. The importer trims and uppercases Code; prepared values retain their original codes as text.
- Master Contact is limited to 100 characters, Email to 150, Address1/Address2 to 200 each, and Phone/Mobile/TaxNumber to 50 each. Nonblank email must be valid. Genuine blank contact details produce completion warnings. Supplier tax-number duplicates are rejected; missing numbers are not fabricated.
- CustomerGroup, SupplierGroup, PaymentTerm and PriceLevel require explicit saved mappings when the source value is nonblank. Defaults apply to blank fields. Account resolution can use a matching active posting-account code. AR must be Asset, AP Liability, customer revenue Revenue, supplier expense Expense, and opening equity Equity. Ineligible account folders cannot be selected as posting accounts.
- Outstanding Amount must be nonzero, with at most two decimal places and absolute value below 10^16. Reference is required and limited to 100 characters. DocumentDate must be on or before project closing. DueDate cannot precede DocumentDate. Invoice/Bill amounts are positive; ReturnCredit/Credit/Advance amounts are negative. No party-document netting is performed in preparation.
- Cheques need positive Amount, a text ChequeNumber of up to 50 characters, actual ChequeDate, and explicit true/false reflection flags. ReceivedDate must be on or before closing. PartyReflected must be true for an opening cheque. InSafe requires BankReflected=false and an Asset safe account. It adds the safe asset opening without another customer or bank movement. BankBranch requires an explicit exact mapping.
- Stock Quantity and UnitCost are nonnegative and exactly representable to four and six decimal places. Zero quantity or valuation needs review. ConversionFactor, when provided, must be positive and yield exactly representable base quantities/costs. Warehouse and Unit require mappings; new SKUs also require Brand, Category and Name. Existing product type/base unit must agree. InventoryAccountCode must agree with the category or warehouse posting account.
- Stock supports RawMaterial and FinishedGood. A blank LayerDate requires summarized-layer approval; the validator normalizes it to project closing. Stable_identities.csv records the uploaded composite; the final Stock identity uses this normalized date before posting. These rows use a stable project-level date and never a source row number.
- LegacyRecordId/composite identities are limited to 300 characters and duplicates are rejected. Missing permanent IDs produce a composite-identity warning. Source row numbers are recorded only for traceability. Previously imported changed records require controlled correction.
- Upload closing metadata and any explicit row/report snapshot dates must match project closing. A blank unknown date in a held review file does not certify that the source is current. Uploading another file replaces the current revision for that module; it does not append a second Stock dataset.
- TrialBalance Debit/Credit must be nonnegative, have at most two decimal places, and use only one nonzero side per row. Genuine zero accounts are retained. Complete active posting-account coverage, released opening scopes and a balanced residual journal are required. The importer subtracts its own opening contributions. Managed-control differences must be corrected in the owning module, not hidden in GL or equity.
- Validation previews proposed journals. Approval depends on the project, selection, mappings, file content and preview. Execution changes masters/openings and journals. None of these application actions was performed during this task.

## Access and verification limits

The page is Migration Centre at /Migration. Permission constants are Permissions.Migration.View, Prepare, Approve, Execute and Finalize. View permits inspection; Prepare permits creation, staging, mappings and validation; Approve permits approval and scope release; Execute permits execution/resume/reversal; Finalize permits finalization. These are permission names, not assumed role names. Use an operator whose actual assigned role grants the required permissions.

The current local masters and configuration were inspected using SELECT only against SAKEELPC / NexusERPLocal. Their relevant values appear in the mapping guide and audit workbook. Local http://localhost:5248 refused the browser connection. The hosted https://nexus.garnetnation.lk opened the Sign In page. Authenticated hosted workflows, deployed template versions, role grants and hosted account/master configuration remain unverified. Recheck them in the destination before staging or execution.

Excel/CSV output values and source-derived matrices were independently checked with a separate reader. Original files and original ZIP members were checked byte-for-byte using SHA256. The values-only archive workbook normalizes CRLF/CR text line endings to LF, as Excel XML does; the ZIP retains the exact original bytes. No values or problem rows were omitted to obtain a passing result.
