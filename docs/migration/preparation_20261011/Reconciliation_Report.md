# Migration reconciliation report

Prepared 11 October 2026. User-confirmed NexusFlow opening: **1 October 2026**. Required legacy closing: **30 September 2026**, Asia/Colombo. Currency: LKR.

All supplied versions were read. Values are reconciled to the supplied snapshots, not certified as 30 September balances. No imports, database changes or application code changes were executed.

## Source and output counts

| Module | Rows | Upload status | Posting status | CSV folder |
|---|---|---|---|---|
| CustomerMaster | 892 | Yes, staging only | No: mapping validation not executed | staging_csv |
| SupplierMaster | 213 | Yes, staging only | No: supplier expense assignment unresolved | staging_csv |
| CustomerOpenings | 1150 | No: HOLD for refreshed closing export | No: stale/uncertified closing balances and unreconciled controls | held_csv |
| SupplierOpenings | 180 | No: HOLD for refreshed closing export | No: stale/uncertified closing balances and unreconciled controls | held_csv |
| ReceivedCheques | 49 | No: HOLD for refreshed closing export | No: stale/uncertified closing balances and unreconciled controls | held_csv |
| Stock | 938 | No: HOLD for refreshed closing export | No: stale/uncertified closing balances and unreconciled controls | held_csv |
| TrialBalance | 188 | No: HOLD for refreshed closing export | No: stale/uncertified closing balances and unreconciled controls | held_csv |

Customers: 892 source and output rows. Suppliers: 213 source and output rows. Customer open documents: 1,150 across 331 customers, all retained. Supplier open documents: 180 across 52 suppliers, all retained. Cheques: 49 instrument rows across 17 customers, total 5,328,628.00. The cheque sheet has 110 physical rows, 88 nonblank rows: 49 instruments and 39 nonblank presentation/subtotal rows, plus 22 blanks. Every excluded presentation row is listed in the review workbook; the grand total and every group subtotal were checked.

Stock: 494 FG source rows plus 444 RM source rows become **938 combined Stock rows**. All FG warehouse quantities were summed into the approved WH_MAIN layer per SKU. loc1 = 68,206 and loc3 = 780, other locations zero. FG quantity remains 68,986. RM quantity remains 6,306,283; these mixed item quantities are diagnostic counts and not a single physical unit measure. Excel/CSV counterpart rows are compared and never appended twice. No overlapping FG/RM SKU codes and no duplicate supported identities were found.

TB: all 188 distinct legacy account rows retained, including 102 zero rows. Total Debit = 686897710.78; Credit = 687510830.78; Debit less Credit = **-613120.00**. Every cfamt agrees with cfamtdr − cfamtcr, so header conversion does not solve the imbalance.

## Debts and credits

| Module | Positive debts / instruments | Negative credits | Signed net | Positive rows | Negative rows |
|---|---|---|---|---|---|
| CustomerOpenings | 162436582.24 | -321597.10 | 162114985.14 | 1130 | 20 |
| SupplierOpenings | 96073265.55 | -1883015.00 | 94190250.55 | 173 | 7 |
| ReceivedCheques | 5328628.00 | 0.00 | 5328628.00 | 49 | 0 |

Customer Amount comes only from creamt. Due dates are document date +30. No cheque values are added to AR. No netting or zero-net-customer filtering is performed. Zero-net customers with retained unsettled rows: DC969, FA111, JY308, SA280, VI852.

Supplier Amount preserves dtfbal with user-confirmed signs. Negative balances are Credit (returns). Positive ADVANCE/OVER PAID remarks retain positive Bill amounts by explicit user instruction. These are reported balances; refreshed 30 September exports remain necessary to certify the opening.

## Stock valuation

User selected FG avar average cost: **84689502.04**. Original costval total: 87863320.46. Approved candidate less original costval: **-3173818.42**. There are 202 FG valuation mismatches and three negative source costval rows (7, 99, 253); their source values remain archived. RM source valuation is 29727743.70. Combined selected valuation: **114417245.74**. This cost choice resolves preparation treatment, not the difference to the closing GL.

## Source schedule versus supplied GL

| Check | Schedule / Debit | GL / Credit | Difference | Interpretation |
|---|---|---|---|---|
| Customer AR signed net | 162114985.14 | 154029322.14 | 8085663.00 | Different snapshots; not an approved opening |
| Supplier AP signed net | 94190250.55 | 78068405.85 | 16121844.70 | Different snapshots; supplier signs confirmed |
| Cheques in hand | 5328628.00 | 14323736.00 | -8995108.00 | GL less schedule = 8,995,108.00 |
| Combined stock approved avar + raw | 114417245.74 | 97626087.76 | 16791157.98 | Legacy closing-stock account coverage/split requires confirmation |
| TB debit less credit | 686897710.78 | 687510830.78 | -613120.00 | Unbalanced source; no plug added |

Do not post a suspense/equity plug for any difference. Invoice/cheque balances stay separate. Legacy B150 closing stock is one account while NexusFlow has separate RM 1131 and FG 1132 assets; an approved allocation and compatible complete TB mapping are still required.

## Dates and unmatched masters

Customer ageing: all 958 type-1/2 age calculations identify **28 September**, with 192 returned-cheque debts not used to infer report date. Cheque title explicitly identifies 28 September. RM OpeningDate is 13 September on all 444 rows. Supplier dtfdate + grndays identifies **7 October** on all 180 rows, an inference rather than a certified report header. Supplier rows after closing: **4**, amount 3563629.09. FG and TB dates are uncertified.

Missing customer masters in both source and local legacy-code bindings: GA735, TK1039, UP1038, VI1041. Missing supplier masters: SH331, WL329. The two current customers and four current suppliers have no legacy codes; name similarity is not a valid match. Stock has 857 exact existing SKU matches (413 FG +444 RM); 81 FG SKUs need genuine master/classification/unit completion. Detailed affected rows and amounts are in Exceptions and party-total sheets.

## Evidence and verification limits

Current local masters were read using SELECT only from SAKEELPC / NexusERPLocal at 2026-10-11T06:58:31 UTC. Project 3 (test) already exists in Preparing state. Journals are empty and account balances zero. Local http://localhost:5248 refused the browser connection. Hosted https://nexus.garnetnation.lk opened Sign In; authenticated deployed templates, masters and button behavior were not verified. Local template files are checked against current MigrationCatalog code. All template headers, workbook/CSV row equality, identities, signed amounts, ISO dates and formulas are verified in the final verification report.

The supplied source archive is complete for these ten files, but it is not the complete old-system transaction archive. Obtain a full read-only historical export and backup through 30 September, plus a separately controlled transfer of every transaction from 1 October onward.
