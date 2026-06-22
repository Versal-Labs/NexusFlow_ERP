# NexusFlow ERP UAT Test Plan

## Objective

Validate financial-period controls, number-sequence health, cheque lifecycle accounting, controlled document output, supplier-bill payment routing, and job-work production before release.

## Environment

- Application: hosted UAT/staging system
- Local verification route: `/Inventory/ProductionOrders`
- Database: restored UAT copy with migration `FinancialControlsDocumentsAndProduction` applied
- Browsers: current Chrome and Edge, desktop and mobile-width checks
- Roles: `SuperAdmin`, `Admin`, `Accountant`, `StoreKeeper`, `SalesRep`

## Entry Criteria

- A current open financial period and one closed period exist.
- Required account mappings and number sequences pass `/api/config/health`.
- UAT users have only the permissions listed in `UAT_Roles_And_Permissions.md`.
- Test products, warehouses, contractor, customers, supplier bills, and cheques are loaded.
- No test uses production customer, banking, or payroll data.

## Execution Order

1. Financial periods and number-sequence health
2. Cheque receipt, endorsement, clearance, and dishonor
3. Print, PDF, history, and supplier-payment deep link
4. BOM revisions and production orders
5. Full regression: PO to GRN to bill, SO to invoice, inventory, treasury, and reporting

## Evidence

For each case retain the document number, screenshots, generated PDF hash, journal reference, and pass/fail result. Accounting cases must include debit and credit totals.

## Exit Criteria

- All critical and high-risk cases pass.
- No unbalanced journal is created.
- No transaction posts outside an open financial period.
- No issued WIP cost is capitalized more than once.
- Any item marked **Needs business confirmation** has a signed business decision.

## Automated Baseline

`dotnet test NexusFlow_ERP.sln -c Release` passes 30 tests. Authenticated browser smoke testing still requires a UAT login; the unauthenticated route and return URL were verified.
