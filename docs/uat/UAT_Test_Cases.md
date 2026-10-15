# UAT Test Cases

> Current opening-import workflow: [Migration Centre](../migration/Migration_Centre_Runbook.md), `/Migration`. The earlier separate raw-material/customer-import sections describe historical releases; their page routes redirect to Migration Centre and their posting APIs retire once a central project exists.

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
| BOM-02 | Active finished goods and raw materials exist | Target FG and RM component records | Open **Create BOM**; type fewer than two characters, then two or more characters in each lookup | No catalog-wide request before two characters; target returns only active Finished Goods and component search returns only active Raw Materials | |
| BOM-03 | One active FG and one active RM; item of each wrong type | Attempt wrong-type selections via UI/API | Save BOM | Wrong target/component type is rejected server-side; no BOM revision is created | |
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

## Raw Material Import Acceptance Cases

Hosted execution and visual checks are pending deployment. Leave pass/fail blank until an authorized tester executes them in UAT.

| ID | Preconditions | Test data | Test steps | Expected results | Pass/Fail |
| --- | --- | --- | --- | --- | --- |
| RM-01 | User has both required permissions; new screen deployed | Synthetic two-row fixture | Open **Inventory & Mfg → Raw Material Import**; choose CSV; select open date and defaults; click **Validate and Preview** | 2 rows, 11 PCS, 26.00; no catalog/stock/journal writes; account names/codes visible | |
| RM-02 | RM-01 preview valid | Extra columns with different category/date/UOM | Review setup and warnings | Screen settings apply to all rows; ignored columns warning is explicit | |
| RM-03 | Valid preview; confirmation unchecked | Valid fixture | Inspect **Import and Post Opening Stock**; tick confirmation; post once | Disabled until confirmation; success reference appears; 2 Raw Material items with zero selling price, 2 FIFO layers/stock entries, Dr RM inventory 26.00 / Cr opening equity 26.00 | |
| RM-04 | Successful import | Same file | Preview/post again | Existing SKU errors; no additional stock or journal | |
| RM-05 | Valid preview | Change quantity, file, date, brand or warehouse | Edit then try posting | Preview invalidated; revalidation required; tampered/stale preview hash rejected server-side | |
| RM-06 | Operator has only one or neither required permission | Read-only, product-only, stock-only users | Open page and call preview/execute API | Access denied; no changes | |
| RM-07 | Cookie-authenticated user | Omit/alter antiforgery token | Submit multipart request | Rejected; no writes | |
| RM-08 | Any existing catalog | Duplicate/existing SKUs and invalid quantities/costs | Preview each negative file | Row-specific errors; entire import blocked; no silent value replacement or row skipping | |
| RM-09 | Shared category uses FG inventory | Select that category | Preview | Mapping conflict; category unchanged | |
| RM-10 | Inactive/missing account or warehouse, missing brand/PCS, closed/missing period | Negative setup | Preview/post | Blocked before committing catalog or accounting changes | |
| RM-11 | Local or disposable test DB only | Inject journal failure after stock staging | Execute import; query all affected tables | Product/category/variant/layer/ledger/journal writes rolled back; no account balance change | |
| RM-12 | Valid CSV names contain quotes, comma, line break, HTML-like text | Special-character fixture | Preview in desktop and mobile widths | Correct names shown as text; no HTML execution; controls/totals readable without clipped content | |
| RM-13 | Request is posting | Double-click or interrupted response | Attempt duplicate submission; then check **Opening Stock** | Controls disabled while busy; verify reference before retry; replay cannot duplicate SKU stock | |
| RM-14 | Valid input | Cost with >2 decimals, >2 MB upload, >2,000 rows | Preview | Clear rejection without rounding or partial import | |

## Customer opening migration acceptance cases

Shared preconditions: application and both new schema migrations deployed to disposable UAT; source closing 2026-09-30/cutover 2026-10-01; valid active LKR masters, verified 7010-001 branch, open period, zero control balances and both migration permissions. Use synthetic C001/C002 data from UAT_Test_Data.md. Each independent posting/reversal case starts on clean test data. Automated evidence is in Customer_Migration_Verification.md; the Result column below is for authenticated hosted business UAT and must be completed by the tester.

| ID | Preconditions / test data | Test steps | Expected result | Hosted pass/fail |
|---|---|---|---|---|
| MIG-CUST-01 | Valid complete master CSV, Id 0/C003; existing customer explicit Id | Download Customer CSV Template, Preview Customers, confirm, Import Customers; repeat with explicit update Id | Unique normalized legacy code retained; complete masters saved atomically; no financial rows | Pending |
| MIG-CUST-02 | Duplicate code; missing master ID; unknown update Id; changed master after preview | Preview/import each invalid CSV | Clear validation error; no partial import; stale preview rejected | Pending |
| MIG-CUST-03 | Synthetic reports with GT00011762 original debit 128,880 and remaining 18,751.50 | Validate and Preview; inspect item/customer totals | 4 items, 2 customers, net AR 19,051.50 and 2 cheques totaling 750.00; only remaining amount used; no finance posting | Pending |
| MIG-CUST-04 | 28 September sample; corrupt subtotal; unknown type; malformed or formula XLSX | Preview each against 30 September closing | Invalid/date-inconsistent source blocked; original supplied binary XLS readable; cheque text zeros and A suffix retained | Pending |
| MIG-CUST-05 | Unmapped code/branch; inactive customer; invalid or nonzero control account; closed period | Preview; fix mappings; Revalidate Mappings | All errors shown; no placeholder master; correct verified mapping succeeds | Pending |
| MIG-CUST-06 | Clean draft, posting disabled or physical checkbox clear | Attempt post; enable without UAT checkbox; then approved activation and revalidate | Disabled/UAT/physical gates enforced on server and screen | Pending |
| MIG-CUST-07 | Approved valid draft | Change customer mapping/name or bank selection after preview; attempt old review post | Stale result rejected; Revalidate Mappings required | Pending |
| MIG-CUST-08 | Valid physically verified draft | Post Customer Opening; repeat; submit concurrently from second session | Exactly 4 opening items, 2 opening cheques and one balanced journal; zero sales invoices/receipts; no duplicate | Pending |
| MIG-CUST-09 | Deliberately failing opening journal in isolated automated fixture | Post batch | Entire transaction rolled back; no opening rows, cheques or GL residue | Pending |
| MIG-CUST-10 | Posted batch | Reconcile Batch, download totals and sources; sign off; attempt re-enable or reverse | Source totals/counts/journal match; retained source downloads; sign-off audit retained and lock enforced | Pending |
| MIG-CUST-11 | Posted C001 return credit 300 | In Receipts allocate return credit 300 to GT; reject excess/cross-customer; Reverse Application on later date | Target debt/available credit change equally; net AR unchanged; dated earlier reports preserved | Pending |
| MIG-CUST-12 | Posted C001 | Receive 1,000 against GT; view receipt; void three days later; inspect Statement and dated AR Ageing | Allocation references opening item; net AR decreases then restores on void date; no duplicate revenue/tax/stock; net/gross/credits reconcile | Pending |
| MIG-CUST-13 | Opening InSafe cheque | Deposit after cheque date; clear exact deposit through bank reconciliation; separately endorse another cheque then bounce | Safe/bank/AP movements correct; no historical receipt required; exact journal cleared; full collectible bounce memo due immediately | Pending |
| MIG-CUST-14 | Opening cheque and separately a new-system receipt cheque allocated to opening debt | Bounce opening cheque; collect resulting memo; reject repeated bounce; bounce new-system cheque | Opening creates memo, new receipt cheque voids receipt/restores allocated debt; correct dated AR and no double debt | Pending |
| MIG-CUST-15 | Separate unused unsigned batch; finance-only, treasury-only and read-only users | Reverse unused batch with reason/date; try used/signed-off reversal and later-date backdated replacement; visit route/API with restricted users and omit cookie CSRF | Eligible reversal retains audit/opposite journal; prohibited cases blocked; both migration permissions required; spoofed Authorization does not bypass CSRF | Pending |

## Migration Centre acceptance cases

Current entry: **Administration → Settings & Security → Migration Centre**, `/Migration`. Earlier separate-import cases are historical; their posting routes are retired once a central project exists. Leave hosted Pass/Fail blank until executed by an authorized UAT tester.

| ID | Preconditions | Test data | Test steps | Expected results | Pass/Fail |
|---|---|---|---|---|---|
| MC-01 | Clean disposable target, open period, authorized administrator | Synthetic source `SMOKE`, opening 2026-10-01 | Select **New Project**, enter details, **Create Project**, leave/reopen using **Saved migration project** | One retained company project; closing date 2026-09-30; a second project is rejected | |
| MC-02 | Accounts exist with unrelated financial journal or unexplained balance | Separate dirty-ledger fixture | Attempt **Create Project** | Blocked; no second opening position added | |
| MC-03 | View-only, prepare-only and unrelated test users | Permission matrix | Open `/Migration`; call create/approve/execute/finalize; prepare-only attempts NoLegacyOpening scope | Each action requires its policy; no-legacy classification also requires Approve; forged cookie requests without CSRF fail | |
| MC-04 | Project selected | Template pack and one valid ZIP | **Download Template Pack**; edit manifest; **Upload Package**; inspect files/originals | Twelve exact CSV headers; version/source/closing date enforced; upload stages only, retains originals | |
| MC-05 | Project selected | September report, malformed file, formula XLSX, duplicate ZIP entry/path | Upload each | Clear rejection, no financial writes; stale dates are not relabelled or silently accepted | |
| MC-06 | Master defaults configured | Real names/unique codes, genuinely blank contacts/TIN | Upload masters; validate, approve, execute; open Customers/Suppliers | Masters created with exact legacy codes and **Details incomplete**; no invented contact data; later valid normal edit recomputes flag | |
| MC-07 | Canonical/legacy rows staged | Alternate source headers and exact mapping | **Change source column mapping**, **Add Mapping**, **Save Mappings and Evidence**, validate | Normalized rows/scopes reflect mapped codes; absent nonblank codes block; similar names never auto-match | |
| MC-08 | Some datasets missing | Customer schedule now, supplier/TB later | Leave modules **Awaiting Data**; import selected customer; upload later files with original closing date | Same project resumes; missing does not mean zero; dependent scopes stay pending; finalization blocked | |
| MC-09 | Approval recorded | Changed source revision/default warehouse/UOM/mapping/filter | Change settings or upload revision, attempt execution | Stale approval rejected; resolved targets and source are covered by approval; revalidation/approval needed | |
| MC-10 | Valid independent position | C001 debt 1,000, return -150, safe cheque 200 | Select complete C001; validate/approve/execute; inspect journal and unpaid items | Net AR 850; separate safe asset 200; customer due dates original date +30; return is reusable credit; no historical invoice/sale/receipt created | |
| MC-11 | C001 valid; another customer invalid | Whole-party selection and zero-net offsetting debts | Filter C001 then validate/import; inspect other scopes | Selected whole party imports without skipping its rows; unrelated invalid party stays pending; zero-net documents are retained | |
| MC-12 | Controlled failure injection only in local disposable host/test | Valid multi-component party group | Fail journal posting; inspect tables; resolve cause and **Resume Run** | Failed group fully rolls back; prior completed groups stay recorded; resume commits unfinished group once | |
| MC-13 | Approved selection | Concurrent execute and duplicate job delivery | Execute concurrently; replay approved source | One active run; atomic source origins/checkpoints prevent duplicate journals/stock/cheques | |
| MC-14 | Supplier mapped; source due date or approved terms | S001 bill 400, credit -100 | Validate without terms, then approved terms; execute/release; settle/apply credit in **Payments** | Missing due treatment blocks; AP opening debt/credit supported without fake bill/payment; credit allocation posts no second GL | |
| MC-15 | Banks/parties mapped, explicit reflection flags | Safe, deposited, endorsed, issued PDC; two cheques for one receipt | Import; inspect Cheque Vault and **Opening uncleared bank items**; clear after face date; cancel issued cheque with reason | Independent cheque identities; safe asset only for InSafe; deposited/issued create pending items without duplicate bank/payment GL; early clearing blocked; issued cancellation restores bank/AP once | |
| MC-16 | Released bank/customer scope, open date | Deposited opening cheque | Dishonour through existing cheque workflow; reconcile original bank position | Customer debt reopened once; matching pending item resolved; closing bridge unchanged by subsequent activity | |
| MC-17 | Inventory accounts/UOM/warehouses mapped | Two warehouses, fractional quantities, six-decimal cost, original FIFO dates | Upload **Stock**; filter one SKU; execute; release; consume FIFO | Complete SKU layers across warehouses; base conversion/precision retained; original FIFO order; inventory money rounded to cents consistently | |
| MC-18 | Stock valuation evidence absent/then supplied | Missing FIFO dates, FG average-cost mismatch, negative qty/cost | Validate before/after summarized-layer/valuation evidence | Missing approval or invalid costs block; evidence never hides a control/TB difference | |
| MC-19 | Posted pending scopes and independent reports | Controls in test-data guide; one intentionally wrong count | **Reconcile**, **Release Reconciled Scope**; then posted **ControlTotals** and **Release Selected Reconciled Scopes** | Every component/count independently checked; wrong/missing controls stay pending; passing selected scopes release; pending financial writes rejected, released scopes may trade | |
| MC-20 | Unused unreleased unit; separate consumed/settled/released unit | Corrected AP or stock source | **Review Reversal**, date/reason, **Reverse Unused Unit**, corrected reimport | Opposite dated journal and originals retained; corrected current origin imported once; used/released/shared master/bank/TB units cannot use this reversal | |
| MC-21 | Every subledger/stock/bank scope reconciled; subsequent trade recorded | Full balanced closing TB, including zeros | Validate/approve/execute **TrialBalance**; **Check Full Ledger** | Residual subtracts opening contributions only; subsequent trade excluded; AR/AP/inventory/safe/bank differences cannot be plugged into GL | |
| MC-22 | Complete imported current schedules, released/classified scopes, GL differences zero | Final evidence; alternate missing TB/file scenario | Inspect provisional UI/PDF/XLSX; **Finalize Migration**; attempt edits | Missing data or differences block; successful finalization removes provisional notice and makes project read-only; old posting endpoints remain retired; pre-go-live transactions stay blocked after finalization, while new masters may trade normally | |

Business confirmation: actual go-live date, current closing exports, warehouse/unit dictionary, legacy cost meaning, due terms, cheque reflection/custody and hosted role claims. Do not mark those accepted solely from automated synthetic tests.

## Post-finalization dashboard and normal-operation checks

Use the test cases, preconditions, baseline data, expected results, Pass/Fail and evidence columns in [Post_Finalization_Check_20261015.md](../migration/Post_Finalization_Check_20261015.md). Migration verification passed through read-only database checks; the normal-user transaction cases remain for a human tester to execute and record. Automated regression results are separate from live UAT acceptance.