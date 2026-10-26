# UAT Test Data

> Current opening-import workflow: [Migration Centre](../migration/Migration_Centre_Runbook.md), `/Migration`. The earlier separate raw-material/customer-import sections describe historical releases; their page routes redirect to Migration Centre and their posting APIs retire once a central project exists.

| Data | Example | Purpose |
|---|---|---|
| Open period | 2026-06-01 to 2026-06-30 | Successful postings |
| Closed period | 2026-05-01 to 2026-05-31 | Period rejection |
| Missing-period date | 2026-07-15 | Missing-period rejection |
| RM variant | `FABRIC-BLUE`, 100 metres at LKR 500 FIFO cost | Material issue/return |
| RM variant | `BUTTON-7`, 1,000 pieces at LKR 8 FIFO cost | Decimal and whole UOM checks |
| FG variant | `SHIRT-BLUE-M`, whole-number quantity | Production receipt and sale |
| BOM | Shirt revision 1, basis 1, 1.8m fabric and 7 buttons | Snapshot and requirements |
| Contractor | Active supplier with payable account | Job work and sewing bill |
| Warehouses | Main RM Store and Finished Goods Store | Issue and receipt locations |
| Sewing charge | LKR 250 per receipt batch | Service accrual matching |
| Customer cheque | LKR 50,000 in Cheque Vault | Endorsement and dishonor |
| Supplier bills | Two posted unpaid bills for same supplier | Allocation controls |

Create a second FIFO fabric layer at a different cost to prove issue valuation order. Keep document dates and expected journal amounts in the UAT evidence sheet.

## Raw Material Import Data

Synthetic CSV: `NexusFlow.IntegrationTests/Fixtures/raw-material-import.csv`. It contains two new SKUs, `SMOKE-RM-001` (10 PCS × 2.50) and `SMOKE-RM-002` (1 PCS × 1), total 11 PCS and 26.00. The quoted product name tests embedded commas. Its extra CSV settings deliberately differ from the screen to test override behavior.

Setup: existing active `WH-MAIN`, unique `UNBRANDED`, `PCS`; category `Accessories` missing or mapped to the configured raw-material accounts; open selected date; `OpeningStock` and `JOURNAL` sequences. SQL tests use synthetic account codes 1131/3200/5110/4110 with database-generated IDs, proving codes are not treated as IDs.

Negative files: duplicate SKU (including case changes), existing SKU, missing headers, unbalanced quoting, extra cells, empty name/code, zero/negative/fractional quantity, zero/negative/non-numeric cost, cost with three decimal places, oversized file and more than 2,000 rows. Test warehouse/account deactivation and period closure between preview and posting.

User-file control totals from read-only local preview: 444 rows; quantity 6,306,283; value 29,727,743.70. The real CSV is not checked into source control or automatically posted by tests.

## Customer opening migration data

Synthetic fixture: customers C001 and C002, LKR, cutover 2026-10-01, source 2026-09-30, open financial period, active transaction accounts AR/SAFE/BANK/OBE/REV/AP and bank branch 7010-001.

| Code | Opening reference/type | Signed LKR |
|---|---|---:|
| C001 | GT00011762 / invoice debt | 18,751.50 |
| C001 | DE1 / debit adjustment | 400.00 |
| C001 | RETURN1 / goods-return credit | -300.00 |
| C002 | RT1 / returned-cheque debt | 200.00 |

Opening AR 19,051.50; cheques 500.00 and 250.00, total 750.00; balanced journal debit 19,801.50 and credit 19,801.50. Cheque text identifiers include `001946A` and `000012`. Deposit/endorse/bounce tests use disposable SQL databases and do not transact real funds.

Private attached sample baseline: 1,150 items across 331 customers, net AR 162,114,985.14; 49 cheques across 17 customers, 5,328,628.00. Both reports represent 28 September and must fail the 30 September closing-date check. They are read-only parser evidence, not approved production balances. Final refreshed closing exports and physical custody are **Needs business confirmation**.

## Migration Centre synthetic data

Use a disposable SQL database, LKR, source ID `SMOKE`, opening date 2026-10-01 and closing 2026-09-30. These are test dates; production requires the actual agreed go-live. Create active posting accounts AR/SAFE/BANK/OBE/REV/AP, real test branch `7010-001`, customer C001, supplier S001, two warehouses, base UOM and open period. The smoke host does this only in its own GUID-named database.

| Dataset | Test rows/control |
|---|---|
| CustomerOpenings | C001 `OLD-INV`, Invoice, 2026-09-01, 1,000; `OLD-RET`, ReturnCredit, 2026-09-02, -150. Blank due dates become 2026-10-01 and 2026-10-02. |
| ReceivedCheques | C001 `OLD-R`, text cheque `000001`, received 2026-09-25, face 2026-10-10, amount 200, InSafe, PartyReflected true/BankReflected false. |
| Customer controls | `CustomerOpenings:Debit` 1,000/count 2; `CustomerOpenings:Credit` 150/count 2; `ReceivedCheques` 200/count 1. |
| SupplierOpenings | S001 bill 400/credit -100; original references/dates plus supplied due date or explicitly approved terms. Controls debit 400/count 2, credit 100/count 2, issued 0/count 0. |
| Stock | One SKU with original layers in two warehouses; base quantities 1.2500 and 0.7500, cost 1.123456. Layer values 1.40 and 0.84; total 2.24. |
| Bank bridge | Statement 1,000 + deposited receipt 300 - issued cheque 200 = book 1,100. Independent bridge difference zero, pending count 2; other BankRecon amount 0/count 0. Use its own fixture/TB. |
| HTTP smoke closing TB | AR Dr850, SAFE Dr200, OBE Cr1,000, REV Cr50, BANK/AP zero. First AR/safe opening credits OBE1,050; residual DrOBE50/CrREV50; total three journals. |

Use separate fixtures for AP/stock/bank or update the full TB consistently; do not mix incompatible sample controls. Every active mapped posting account needs a TB row, including zeros. Blank templates are in [templates](../migration/templates/README.txt); fill them with synthetic data and independent controls. Never use customer PII or banking exports as checked-in test fixtures.

Negative fixtures: duplicate source ID, altered source content under posted ID, stale source closing date, missing legacy code, invalid branch, false reflection flags, formula XLSX, negative stock, missing supplier due treatment, incomplete warehouse layer set, changed approved defaults, contradictory Not Applicable, NoLegacyOpening for an uploaded party, closed period, pre-go-live operational date and incomplete/managed-control TB difference.

## Finalized local UAT baseline — 15 October 2026

Project 4 is Finalized with 2,050 Released scopes and 2,455 Posted units. Before new UAT trading: AR 162,114,985.14; AP 94,190,250.55; stock 114,417,245.74; Cash & Bank 100,000.00; received cheques 5,328,628.00 across 49 instruments. All amounts are LKR. The closing schedule remains a UAT fixture. See [the complete baseline and transaction test data](../migration/Post_Finalization_Check_20261015.md).

## Background operation fixtures — 2026-10-21

Use isolated synthetic projects; never use finalized hosted project 3 for write tests. Dates: closing 2026-09-30, opening 2026-10-01, currency LKR. Retain independent control schedules and signed synthetic zero declarations.

| Fixture | Data and purpose |
|---|---|
| Two-customer import | C001 invoice 100; C002 invoice 200; required cheque components explicitly NotApplicable with evidence; independent debit/credit controls and row counts. Select C001 alone to prove operation totals do not report project completion. |
| Partial release | Same data; independent C002 debit deliberately 199 instead of 200. C001 releases, C002 remains pending with a reason. |
| Large recovery | Automated synthetic baseline: 2,050 customer scopes, complete zero-opening declarations, matching imported ControlTotals including 6,150 component rows. Interrupt exactly after 223 committed releases, reopen and resume 1,827 unfinished scopes. This tests recovery mechanics; it is not a certified business cutover. |
| Pilot-shape UAT | 896 customers, 215 suppliers, 938 stock scopes and 1 bank scope = 2,050 scopes. Supply valid mapped masters, complete datasets, independent amounts/counts, and bank/stock controls. Business source certification is **Needs business confirmation**. |
| Grouped warnings/details | 125 C001 source rows without LegacyRecordId; stable unique references. Composite-identity warning groups retain every affected source row. Detail/page sizes 25/50/100, with requests above 100 capped. |
| Job service failures | Local fake job service: enqueue unavailable, Active, Unavailable, Missing and stopped states. Never simulate these failures by changing hosted job records. |
| Upgrade | Disposable database on `20261008142956_latest`, finalized project, released scope, retained source/unit and legacy SelectionJson run. Exercise MigrationRuns temporal-on and temporal-off independently. |

Opt-in seeded smoke host: set `NEXUSFLOW_RUN_LOCALDB_CUSTOMER_TESTS=1` and `NEXUSFLOW_MIGRATION_SMOKE_SEED=1`, then run `tools/MigrationCentreSmokeHost`. It binds loopback port 5191 and owns a GUID-named disposable LocalDB database. It stages 130 synthetic customer scopes (C001/C002 mapped; others deliberately missing) and independent C001/C002 controls. `/smoke-login?mode=viewonly` and `?mode=prepareonly` provide restricted test sessions. POST `/smoke-stop` shuts down and removes only the owned synthetic database.
