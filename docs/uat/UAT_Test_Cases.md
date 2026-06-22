# UAT Test Cases

| ID | Preconditions | Test Steps | Expected Result | Pass/Fail |
|---|---|---|---|---|
| FIN-01 | Open period covers selected date | Create PO, GRN, invoice, payment, and production action | Each saves; document date is used for period validation | |
| FIN-02 | Selected date is closed | Attempt the same transactions | HTTP result contains `financial_period_not_open`; no number or database change | |
| FIN-03 | No period covers selected date | Visit dashboard and a transaction page; attempt save | Persistent warning appears; dashboard alert appears; save is rejected | |
| FIN-04 | Existing period | Create an overlapping date range | Save is rejected with overlap message | |
| SEQ-01 | Remove one UAT sequence | Open System Configuration and dashboard as Admin | Missing sequence is listed in both places | |
| SEQ-02 | Existing counters recorded | Run Repair Number Sequences twice | Missing default is created once; existing prefix/counter remains unchanged | |
| CHQ-01 | Posted invoice and customer cheque receipt | Allocate full cheque receipt | Invoice shows `PendingClearance`; commission remains pending | |
| CHQ-02 | Cheque in vault; posted supplier bill | Endorse from Treasury Payments, then repeat from Cheque Vault with new cheque | Both use the same validations and produce method `EndorsedCustomerCheque` | |
| CHQ-03 | Endorsed cheque | Mark Cleared with an open-period date | Supplier bill and customer invoice recalculate to paid where fully covered | |
| CHQ-04 | Endorsed cheque | Dishonor; enter reason, fee, fee source, and customer recovery | Receipt/payment void atomically; AR/AP reopen; reversal balances; debit memo is visible | |
| CHQ-05 | Paid commission exists | Dishonor related cheque | Dishonor succeeds and creates commission clawback instead of blocking | |
| DOC-01 | Any supported posted document | Use separate Print and Download PDF actions | Server reloads source data; PDF has branding, repeated table header, totals, notes, page numbers, timestamp | |
| DOC-02 | Print preview open | Edit party name/address/notes and finalize | Only allowed text changes; final PDF, SHA-256, user, time, action, and differences appear in history | |
| DOC-03 | User lacks document view permission | Call Print Engine endpoints directly | Request is forbidden | |
| DOC-04 | Browser developer tools available | Submit modified totals/lines to old render shape | Client totals/lines are ignored; database values appear in PDF | |
| PAY-01 | Posted unpaid supplier bill | Click **Pay Bill** | `/Treasury/Payments?billId={id}` loads supplier, bill, outstanding allocation, and opens modal; URL is cleaned | |
| PAY-02 | Draft, voided, paid, or zero-balance bill | Open payment deep link | Clear rejection appears and no payment modal allocation is created | |
| BOM-01 | Approved BOM revision 1 | Edit and save recipe | Revision 1 becomes inactive; approved revision 2 is created; old production snapshot is unchanged | |
| PROD-01 | Draft order | Create at `/Inventory/ProductionOrders`, then release | BOM component snapshot and planned quantities are frozen | |
| PROD-02 | Released order with FIFO stock | Post two material issues | RM layers reduce FIFO; Dr WIP / Cr category inventory; held balances update | |
| PROD-03 | Contractor-held material | Post partial return | RM stock layer is restored at held WIP cost; Dr RM / Cr WIP | |
| PROD-04 | In-progress order | Post two partial receipts | Accepted FG stock increases; cumulative WIP cost is allocated once only | |
| PROD-05 | Receipt has normal waste | Classify waste and post | Waste cost is absorbed into accepted FG cost | |
| PROD-06 | Receipt has abnormal loss | Classify loss and post | Loss posts to Inventory Shrinkage; it is not capitalized | |
| PROD-07 | Contractor-caused loss | Classify recoverable and post | Supplier claim is created; supplier AP is debited; claim must be settled before closure | |
| PROD-08 | Requested accepted quantity above tolerance | Post receipt | Rejected until authorized production-order revision raises target/tolerance | |
| PROD-09 | Sewing accrued on receipt | Create supplier bill and select **Link Sewing Accruals** | Service accrual clears; price difference posts to Purchase Variance | |
| PROD-10 | Output, materials, claims, and accruals reconciled | Close order | Status becomes Closed; closure PDF is available | |
| PROD-11 | Any unresolved held stock/cost, claim, or sewing bill | Attempt close | Closure is blocked with the unreconciled reason | |
| PROD-12 | Historical production transactions exist | Open **Legacy History** and old URLs | History is read-only; old pages redirect; free-text POST endpoints return 410 | |
