# Stage 7 — ordinary operations and acceptance checks

Record Pass / Fail, tester/date and evidence in UAT_Review.xlsx → UAT tests or reports/UAT_tests.csv. Preparation checks do not mean these application tests passed. Admin migration operators need Migration permissions. Normal users need actual sales/payment/stock/cheque permissions and released scopes. Destination role names are Needs business confirmation.

After the opening/TB tests:
- Receive a normal customer payment against a positive imported Invoice. Confirm outstanding reduces and a receipt posts once.
- Apply an imported ReturnCredit through the supported customer credit workflow. A credit is not collected as an invoice. Zero-net documents remain traceable.
- Pay a positive supplier opening Bill and apply a negative supplier Credit separately. Confirm no historical bill is recreated.
- Use normal stock issue/sale/receipt with a released imported SKU. Confirm on-hand, FIFO remaining quantity and journal accounts.
- Deposit an imported InSafe cheque. Confirm safe reduces and bank changes once, with no second AR reduction.
- Clear the synthetic issued cheque and deposit item through the supported opening-bank-item workflow. Confirm already-reflected bank book is not posted twice.
- Check /Migration access and API permission enforcement for a user without migration permissions.

| Test ID | Stage | Precondition | Steps | Expected result | Pass / Fail | Evidence / tester |
|---|---|---|---|---|---|---|
| UAT-01 | Project | Isolated UAT project | Select saved test project | Preparing; opening 2026-10-01; closing 2026-09-30 |  |  |
| UAT-02 | Staging | Preparing project | Upload Stage 1 package/CSVs | 39 accounts, 896 customers, 215 suppliers; no ledger posting |  |  |
| UAT-03 | Mappings | Masters staged | Save mappings/defaults | NET30 means 30 Days; DOM/TRADE mapped; no COD |  |  |
| UAT-04 | Masters | Clean validation | Validate, approve, execute masters | All masters imported; genuine missing-contact warnings remain |  |  |
| UAT-05 | Customer documents | Masters imported | Import CustomerOpenings | 1150 items, positive Invoice / negative ReturnCredit; net 162114985.14 |  |  |
| UAT-06 | Zero-net customer | Opening items posted | Inspect DC969, FA111, JY308, SA280, VI852 | Debt and credit documents both retained |  |  |
| UAT-07 | Supplier documents | Masters imported | Import SupplierOpenings | 180 items; positive Bill / negative Credit; net 94190250.55 |  |  |
| UAT-08 | Received cheques | Customer masters imported | Import and inspect cheque numbers | 49 InSafe instruments; zeros retained; safe 5328628.00; AR unchanged |  |  |
| UAT-09 | Issued cheque | AH143 imported | Import UAT-ISSUED-001 | 000001 / 15000 pending bank item; no second AP/bank payment |  |  |
| UAT-10 | Stock | No prior SKU activity; mappings saved | Import one complete Stock dataset | 938 WH_MAIN layers; quantities/costs preserved |  |  |
| UAT-11 | Bank | 1112 active Asset | Import bank opening/recon | 100000 =112500 +2500 -15000 |  |  |
| UAT-12 | Controls | Opening modules posted | Import ControlTotals | 5212 expected components, including master-only zero controls |  |  |
| UAT-13 | Release | Controls imported | Bulk release all scope kinds | 2050 scopes Released; zero Pending |  |  |
| UAT-14 | TB | All scopes released | Import complete test TB | 39 accounts; balanced; residual rent debit / revenue credit 1000000 |  |  |
| UAT-15 | Ledger | TB imported | Check Full Ledger | All 39 differences zero |  |  |
| UAT-16 | Retry | Already imported selection | Validate same unchanged selection again | Already imported; no duplicate openings |  |  |
| UAT-17 | Finalize | All rows imported and scopes released | Enter UAT evidence; Finalize Migration | Finalized; migration edits/execution disabled |  |  |
| UAT-18 | Customer payment | Customer scope Released | Receive payment against positive opening Invoice | Outstanding reduces and receipt posts once |  |  |
| UAT-19 | Supplier payment | Supplier scope Released | Pay positive opening Bill; apply separate Credit | Balances update without recreating historical bill |  |  |
| UAT-20 | Stock operation | Stock scope Released | Normal issue/sale/receipt for imported SKU | On-hand/layer quantities and journal accounts update |  |  |
| UAT-21 | Cheque deposit | Customer/bank scopes Released | Deposit one imported InSafe cheque | Safe reduces, bank movement once, no second AR reduction |  |  |

Ordinary workflow screen/route details: [User Manual](../../user-manual/User_Manual.md), [Admin User Manual](../../user-manual/Admin_User_Manual.md), [UAT test cases](../../uat/UAT_Test_Cases.md). Hosted authenticated UI was not accessible in this preparation; record actual deployed results and screenshots when you perform the tests.

## Finalized local UAT verification — 15 October 2026

Local project 4 is now Finalized, with all 2,050 scopes Released and all 39 ledger reconciliation differences zero. The dashboard correction includes migrated AR/AP and corrects cash/bank classification. Before executing ordinary transactions, rebuild/restart the updated application and compare [the verified baseline, next steps and mapping review](../Post_Finalization_Check_20261015.md). Transaction test rows above remain unexecuted until a human tester records their results.