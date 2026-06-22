# UAT Roles and Permissions

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
