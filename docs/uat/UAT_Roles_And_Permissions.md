# UAT Roles and Permissions

> Current opening-import workflow: [Migration Centre](../migration/Migration_Centre_Runbook.md), `/Migration`. The earlier separate raw-material/customer-import sections describe historical releases; their page routes redirect to Migration Centre and their posting APIs retire once a central project exists.

| Role | Relevant access |
|---|---|
| `SuperAdmin` | Protected `Permissions.SuperAdmin` bypass; all functions |
| `Admin` | All declared application permissions |
| `Accountant` | Periods, journals, bank reconciliation, treasury, purchasing views, sales views, and financial reports |
| `StoreKeeper` | Stock, transfers, adjustments, stock takes, BOMs, production, GRN creation, and inventory reporting |
| `SalesRep` | Sales orders, invoices, customers, products, and own commissions |

## Workflow Permissions

| Workflow | Permission |
|---|---|
| Manage financial periods | `Permissions.Finance.ManagePeriods` |
| View/create PO | `Permissions.Purchasing.ViewPOs` / `Permissions.Purchasing.CreatePO` |
| View/create GRN | `Permissions.Purchasing.ViewGRNs` / `Permissions.Purchasing.CreateGRN` |
| View/create supplier bill | `Permissions.Purchasing.ViewBills` / `Permissions.Purchasing.CreateBill` |
| View/create receipt | `Permissions.Treasury.ViewReceipts` / `Permissions.Treasury.CreateReceipt` |
| View/create supplier payment | `Permissions.Treasury.ViewPayments` / `Permissions.Treasury.CreatePayment` |
| Manage cheque lifecycle | `Permissions.Treasury.ManageCheques` |
| BOM maintenance | `Permissions.MasterData.ManageBOMs` |
| Production orders and actions | `Permissions.Inventory.RunProduction` |
| System configuration repair | `Permissions.System.ManageConfigs` |

## Segregation Notes

- `StoreKeeper` can run production but cannot manage financial periods.
- `Accountant` can view purchasing documents but the default manifest does not grant `CreatePO`, `CreateGRN`, or `CreateBill`.
- `Admin` currently receives all permissions. Whether production-order release and closure need separate approver permissions is **Needs business confirmation**.

## Raw Material Import

Both `Permissions.MasterData.ManageProducts` **and** `Permissions.Inventory.AdjustStock` are required for the new page, setup/template downloads, preview and execution. A user holding only one must receive access denied. `Permissions.SuperAdmin` retains its existing bypass through the application's policy handler; this feature adds no role grants.

An authorized stock-import operator performs upload, review and posting. An administrator prepares warehouses, brands, units, categories, account configurations and open financial periods. Actual assignment of the two permissions to a stock-import role is **Needs business confirmation**; do not widen roles automatically.

## Customer opening migration access

| Workflow | Required permission(s) | Role assignment |
|---|---|---|
| Customer Opening Migration page, previews, source downloads, posting, reconciliation, sign-off and unused-batch reversal | `Permissions.Finance.ManageAccounts` AND `Permissions.Treasury.ManageCheques` | Needs business confirmation in hosted tenant |
| Customer CSV template, preview and import | Above two AND `Permissions.MasterData.ManageCustomers` | Needs business confirmation |
| Credit application history in Receipts | `Permissions.Treasury.ViewReceipts` | Existing receipt access |
| Apply opening return credit | `Permissions.Treasury.CreateReceipt` | Existing receipt creation access |
| Reverse Application | `Permissions.Treasury.VoidReceipt` | Existing receipt void access |

`Permissions.SuperAdmin` bypasses permission policies. A finance-only or treasury-only user cannot access migration. Page/API permissions enforce access even when navigating directly. Cookie-authenticated writes require antiforgery tokens; supplying an invalid Authorization header does not exempt them. Business approver and operator separation is an operating procedure; a distinct approver role is not enforced by this implementation.

## Migration Centre permissions — current release

Actual existing role names are `SuperAdmin`, `Admin`, `Accountant`, `StoreKeeper`, `SalesRep`. `Permissions.SuperAdmin` bypasses policies. The default `Admin` manifest includes all permissions; verify actual hosted claims. Migration rights are not automatically added to ordinary operational roles. One authorized administrator may prepare, approve and execute; separate human maker/checker identities are not enforced.

| Action | Required permission in addition to authenticated access |
|---|---|
| `/Migration`, saved projects, templates, retained sources, lookups, reconciliation | `Permissions.Migration.View` |
| Create project, upload files/ZIP, settings/defaults/column mappings, validate, configure pending scope | View + `Permissions.Migration.Prepare` |
| Declare existing scope has **No Legacy Opening** | View + Prepare + `Permissions.Migration.Approve` |
| Approve selected import, release individual/bulk scopes | View + `Permissions.Migration.Approve` |
| Execute/resume, reverse eligible unused unit | View + `Permissions.Migration.Execute` |
| Final financial sign-off | View + `Permissions.Migration.Finalize` |
| Opening pending bank-item list/clear/cancel | `Permissions.Treasury.ManageCheques` |
| Apply supplier opening credit | `Permissions.Treasury.CreatePayment` |
| Customer receipt/opening credit | Existing `Permissions.Treasury.CreateReceipt` |

Cookie-authenticated writes require antiforgery validation. Backend pending-scope locks still apply to users holding normal operational permissions; use scope reconciliation/release instead of privilege escalation. Legacy opening-import policies in earlier sections describe the older release, not the current wizard. Actual hosted assignments and any desired separate approver policy are **Needs business confirmation**.

## Post-finalization testing responsibilities

Administrators rebuild/start the corrected application, verify configuration and the read-only finalized migration. Normal users run receipts/payments/stock/cheque tests with their existing Treasury, Inventory, Sales and Reporting permissions. Dashboard calculations grant no new roles or permissions. Reporting routes retain their existing ViewArAging, ViewApAging, ViewCustomerStatement, ViewSupplierStatement and ViewGeneralLedger policies; configuration remains restricted by Permissions.System.ManageConfigs. See [the administrator and normal-user checklists](../migration/Post_Finalization_Check_20261015.md).

## Background operation permissions — 2026-10-21

Existing roles remain `SuperAdmin`, `Admin`, `Accountant`, `StoreKeeper` and `SalesRep`; no new role is created. Actual hosted assignments are **Needs business confirmation**. Normal trading users continue through their existing operational pages after release.

| Operation or read | Required permissions |
|---|---|
| Summary, progress, paginated outcomes, source details, history and audit | `Permissions.Migration.View` |
| Start/resume Validate or LedgerCheck | View + `Permissions.Migration.Prepare` |
| Start/resume Approve or Release | View + `Permissions.Migration.Approve` |
| Start/resume Import or a legacy posting run | View + `Permissions.Migration.Execute` |
| Start/resume Finalize | View + `Permissions.Migration.Finalize` |
| Change files, mappings or pending-scope configuration | Existing Prepare policy; blocked while an operation/posting run is active |

The new POST `/api/migration/projects/{id}/operations` checks its `kind`, and Resume checks the stored operation kind, preventing a caller from substituting a lower-privilege action. Background approval, posting and audit retain the initiating user's saved identity. Cookie writes continue to require antiforgery tokens. Navigation/read access remains available while work runs. Resume additionally requires confirmed job inactivity and an available worker lock; having permission alone cannot override these guards.
