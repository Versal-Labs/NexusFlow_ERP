# Finalized UAT migration — verification and next steps

Verified on **15 October 2026** against the local NexusERPLocal database. All amounts below are LKR and describe the baseline before new operational UAT transactions.

## Migration outcome

Project **4 — NexusFlow UAT 2026-10-12** is **Finalized**, with opening date **2026-10-01** and closing date **2026-09-30**. Finalization was recorded at **09:32 on 15 October 2026, Asia/Colombo**. Its evidence explicitly says **UAT functional acceptance only; no production migration approval**.

- All **2,050 scopes** are Released: 896 customers, 215 suppliers, 938 stock scopes, one bank. No Pending scope remains.
- All **2,455 units** are Posted, including TrialBalance.
- Re-running the application's ledger reconciliation returned **39 accounts, zero differences**.
- Ledger debits and credits both total **377,341,210.43**; the account-balance net is zero.
- Finalized status/version **2065** remained unchanged during this work. No imports, postings, releases, resets, account remappings or operational transactions were executed in the UAT database.

## Verified opening position

| Item | Amount / count | Where to check |
| --- | ---: | --- |
| Customer positive opening debts | 162,436,582.24 | AR Aging / Customer Statement |
| Customer negative return credits | -321,597.10 | Customer Statement / opening credit details |
| Net receivables | **162,114,985.14** | AR Aging total; GL account 1120 |
| Supplier positive opening debts | 96,073,265.55 | AP Aging / Supplier Statement |
| Supplier negative credits | -1,883,015.00 | Supplier Statement / opening credits |
| Net payables | **94,190,250.55** | AP Aging total; GL account 2110 has credit balance -94,190,250.55 |
| Stock value | **114,417,245.74** | Inventory reporting; GL inventory accounts |
| Finished goods inventory | 84,689,502.04 | GL account 1132 |
| Raw materials inventory | 29,727,743.70 | GL account 1131 |
| Cash and bank | **100,000.00** | Cash/bank group 1110; bank 1112 |
| Received cheques in hand | **5,328,628.00 / 49 cheques** | Cheque Vault; GL account 1151 |
| Customer / supplier opening documents | **1,150 / 180** | Opening references in statements and allocation screens |

Cheque balances remain separate from invoice outstanding. Do not record a second customer receipt for an imported cheque already reflected in party balances.

## Dashboard issue and correction

The old dashboard read only SalesInvoices and SupplierBills. This migration intentionally stores opening documents separately, and the database currently contains no new sales invoices, supplier bills or payment transactions. That explains the incorrect A/R and A/P zeros; the balances were not missing from migration.

The old Cash & Bank calculation summed every Asset account, producing **281,960,858.88** from receivables, inventory, cheques in hand and bank together.

The corrected source code:

- Uses the same dated customer/supplier balances as aging and statements, including openings, negative credits, receipts/payments, voids and reversals.
- Calculates overdue customer debt from unsettled debit documents. Amounts due today are current, matching AR Aging. Unapplied credits remain separate until allocated.
- Calculates Cash & Bank from active transaction accounts under the actual cash/bank account hierarchy, excluding the configured undeposited-funds account and folder balances.
- Uses the standard installed group **1110** here. A custom chart can specify its group code with the optional setting **Dashboard.CashBankAccountRoot**. No database setting was added or changed in this work.
- Displays **Unavailable** with a finance-configuration warning if the cash/bank group or undeposited-funds mapping is invalid.
- Loads payable balances in bulk and avoids loading archived source JSON while calculating customer/supplier balances.

Read-only execution of the corrected handler returned:

| Dashboard card | Expected baseline |
| --- | ---: |
| A/R Balance | 162,114,985.14 |
| A/P Balance | 94,190,250.55 |
| Total Cash & Bank Balance | 100,000.00 |
| Total Stock Value | 114,417,245.74 |
| Overdue Receivables on 2026-10-15 | 156,172,922.24 |
| Revenue MTD / sales and purchase chart | Zero until new invoices/bills are entered |

The corrected AR/AP totals matched the application's aging reports exactly. Overdue matched the AR Aging overdue buckets exactly. The UAT trial-balance rent/revenue residual is an opening fixture; it is not a new sales invoice or purchasing history.

Verification: **43 tests passed, one optional private-source-file test skipped**. After strengthening the invalid-cash-mapping check, all **four focused dashboard tests passed again**. The web application built successfully with zero errors into an isolated output directory. Existing build warnings remain. The real application was not stopped, restarted or used to execute test transactions.

Live browser verification was attempted but both browser helpers failed with `failed to write kernel assets ... path specified (os error 3)`. The screenshot is the pre-change UI. Updated calculations were verified directly through a SELECT-only database guard, and the Razor view compiled; refresh the UI after rebuilding/restarting the local project.

## Administrator steps now

1. Keep a backup/snapshot of this finalized UAT baseline before normal transaction tests, using your usual database backup process.
2. Stop the existing local project through its normal development controls, rebuild and start it again so it loads the dashboard correction. No database migration or opening-data re-import is required.
3. Open **Executive Dashboard** and compare the values above before entering transactions.
4. Confirm `/Migration` still shows Finalized, zero Pending scopes, and the retained source/audit evidence. Keep this project read-only as the opening reference.
5. Open **AR Aging** (`/Reporting/ArAging`), **AP Aging** (`/Reporting/ApAging`), **Trial Balance** (`/Finance/TrialBalance`) and **General Ledger** (`/Reporting/GeneralLedger`). Compare the baseline figures and sample original references.
6. Check that an open financial period covers the test date. The current period service confirmed an Open period covers 15 October 2026, and all required number sequences are present.
7. Give normal testers only their required transaction/report permissions. This change grants no new roles or permissions.

## Normal-user UAT sequence

Record the tester, date, document reference and Pass/Fail. Perform each test once; save its receipt/payment/journal evidence before starting the next one.

| Test | Preconditions / test data | Steps | Expected result | Pass / Fail | Evidence |
| --- | --- | --- | --- | --- | --- |
| Baseline dashboard and reports | Updated application; no new transactions | Open the dashboard and the four reports listed above | AR/AP/cash/stock agree with the baseline; trial balance balances | | |
| Collect an opening invoice | Positive customer opening debt of at least 1,000; Treasury receipt permission; open period | In Customer Receipts (`/Treasury/Receipts`), select the customer, allocate 1,000 to its opening reference, review the cash/bank account, then Confirm Receipt | That debt and total AR fall by 1,000; one receipt/journal; the selected cash/bank account increases once | | |
| Apply an opening return credit | Customer with an available ReturnCredit and eligible debt | Use the existing Apply return credit action against a selected debt; record the date | Available credit and debt both reduce; no new cash and no second reduction of net AR | | |
| Pay an opening supplier bill | Positive supplier opening debt of at least 1,000; Treasury payment permission; open period | In Payments (`/Treasury/Payments`), select supplier/opening debt, allocate 1,000, review account, then Post Payment | That debt and total AP fall by 1,000; one payment/journal; cash/bank decreases once | | |
| Apply supplier opening credit | Available negative supplier opening credit and an eligible debt | Use Apply Opening Credit with its target, amount and date | Debt and available credit reduce; no new supplier cash payment | | |
| Deposit an imported cheque | Imported InSafe cheque; test date is on/after its face date; cheque permission | In Cheque Vault (`/Finance/ChequeVault`), follow the existing deposit workflow into the selected bank account | Cheques in hand decrease and bank increases once; AR stays unchanged | | |
| Use imported stock | Released SKU with available stock; required sales/inventory permissions | Perform a small ordinary sale/stock issue; inspect its FIFO layers, stock transaction and journal | Quantity and stock value decrease correctly; no second opening layer is created | | |
| Clear an opening bank item | Eligible pending issued cheque/deposit; bank reconciliation permission | In Bank Reconciliation (`/Finance/BankReconciliation`), inspect Opening uncleared bank items and use the appropriate existing clearing action | Pending item resolves without posting the already-reflected bank book a second time | | |
| Role and read-only checks | Administrator and normal tester accounts | Confirm finalized migration cannot be edited; exercise only the tester's permitted transaction actions | Opening evidence remains locked; unauthorized actions remain blocked | | |

If the 1,000 receipt and 1,000 supplier payment are the only new net-balance transactions, AR becomes **162,113,985.14** and AP becomes **94,189,250.55**. Other invoices, returns, payments, voids or cheque bounces will change those expectations. Credit applications alone do not change the net party balance.

Continue the broader checklist in [Stage 7 — ordinary operations and acceptance checks](uat_upload_20261012/GUIDE_07_Operational_UAT_Tests.md). Preserve pre-cutover history in the read-only legacy archive; do not recreate it as new trading.

## Configuration review before broader UAT

These current mappings need business confirmation before testing the affected workflows. They were not changed:

| Mapping | Current target | Why to review |
| --- | --- | --- |
| Account.Purchasing.UnbilledReceipts | 2110 — Accounts Payable | GRN clearing currently shares the supplier AP control account. Confirm the intended separate clearing account before GRN/bill matching tests. |
| Account.Payroll.SalaryAdvancesReceivable | 1151 — Cheques In Hand | Salary advances currently share the cheque asset account. Confirm the intended salary-advance receivable account before payroll advance tests. |

Finalization completes this **UAT fixture**, not production migration approval. The earlier snapshot-date exceptions and synthetic bank/issued-cheque/TB fixtures still require refreshed, approved production closing exports and reconciliation for a real go-live.