# Stage 4 — documents, cheques, stock and banks

Precondition: masters imported; all mappings saved; an open financial period covers 1 October 2026; no existing operational party/SKU activity or live stock layers. The checked local test database has none.

1. Upload 02_Openings_and_Controls.zip with Upload Package, or stage its eight individual CSVs with Upload Dataset. This includes all opening schedules and Control Totals. Trial Balance is saved for Stage 6.
2. Uncheck automatically selected modules. Select Customer Openings and Supplier Openings only, with all code filters blank. Validate Selected Import; review signs/dates/amounts; enter UAT evidence; Approve Selected Import; Execute Approved Import; Refresh Progress until complete.
3. Select Received Cheques and Issued Cheques only and repeat the validation/approval/execution cycle. The 49 received instruments are InSafe, PartyReflected=true, BankReflected=false, total 5328628.00. They add the safe opening without another AR receipt or bank movement. Cheque numbers, including leading zeros, remain text.
4. The issued fixture is AH143 / UAT-ISSUED-001 / cheque 000001 / 15000.00 / bank 1112. Both reflection flags are true. It creates a pending bank item already reflected in party and bank book; it does not pay AP again.
5. Select Stock only. Validate, approve and execute the complete 938-row Stock file: 494 FG plus 444 RM, all in WH_MAIN. Existing exact SKU masters are used. The 81 new FG SKUs use temporary MSH / UNBRANDED / pcs classification. LayerDate 2026-09-30 is the UAT summarized date. Another Stock file replaces this complete revision.
6. Select Bank Opening and Bank Recon only. Validate, approve and execute. Bank 1112: book 100000.00; statement 112500.00; deposit pending +2500.00; issued cheque pending -15000.00. Statement bridge: 112500 +2500 -15000 =100000.
7. Import Control Totals in Stage 5. Keep Trial Balance unselected until all opening scopes are released.

## Expected amounts

| Dataset | Positive LKR | Negative LKR | Net LKR |
|---|---|---|---|
| CustomerOpenings | 162436582.24 | -321597.10 | 162114985.14 |
| SupplierOpenings | 96073265.55 | -1883015.00 | 94190250.55 |
| ReceivedCheques | 5328628.00 | 0.00 | 5328628.00 |
| IssuedCheques | 15000.00 | 0.00 | 15000.00 |

Stock value 114417245.74 LKR. FG quantity 68986.0000; RM quantity 6306283.0000. These are different item populations, not a single physical unit measure. All legacy document amounts are retained. Five zero-net customer cases retain both debt and credit rows. Four post-closing supplier documents have test date 2026-09-30 and due date 2026-10-30; original dates are logged.

Do not bypass date/type/mapping/duplicate/live-activity errors. Review the exact cause. Warnings for genuine blanks and composite identities are expected; they are not errors.
