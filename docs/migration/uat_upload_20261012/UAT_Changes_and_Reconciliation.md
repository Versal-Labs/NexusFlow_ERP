# UAT changes and reconciliation

The user requested functional UAT data on 12 October 2026. This separate package adapts values explicitly for testing. Original exports and the previous production preparation package are unchanged.

## Intentional fixture choices

- Financial ClosingDate is UAT metadata 2026-09-30. Original customer/cheque 28 September snapshots, RM 13 September snapshot and uncertified FG/supplier dates remain archived. They have not become certified closing balances.
- All 1150 customer and 180 supplier unsettled document amounts/signs remain. Customer Amount uses creamt only. Cheques remain separate. Four post-closing supplier documents use test date 2026-09-30 and due date 2026-10-30; original dates are logged.
- Four missing customers and two missing suppliers have explicit UAT names with retained legacy codes and blank contacts. BA366 email remains the confirmed blank.
- Suppliers use DOM, NET30 and AP 2110. Expense 5110 is temporary for unresolved suppliers and the two added fixtures. RE187/RS149 retain 6210, EL264 retains 6220.
- All 938 stock quantities and final approved unit costs remain. WH_MAIN, test LayerDate 2026-09-30 and ConversionFactor 1 are fixture choices. Existing SKU fields use their actual masters. The 81 new FG SKUs use MSH / UNBRANDED / pcs.
- 14 cheque branches use documented substitute UAT mappings. Three exact source branch matches remain exact. InSafe / PartyReflected=true / BankReflected=false remain the user's confirmed received-cheque treatment.
- Synthetic bank book 100000, statement 112500, issued cheque 15000 and deposit 2500 exercise bank reconciliation; no real bank source was supplied.
- Accounts mirrors all 39 active posting accounts. The synthetic TB derives from opening journals plus rent Debit / sales Credit 1000000 each; all 39 current accounts covered, 30 test zeros. Original 188 TB rows and -613120 imbalance remain unchanged.
- ControlTotals contains 5212 fixed expected UAT controls, including master-only zeros. It is not independent production closing evidence.
- Named synthetic fixtures have explicit permanent fixture IDs. Legacy rows use supported stable code/composite identities. No identity is based on an Excel row number.

| Module | Rows | Data basis | Upload status | Execution status |
|---|---|---|---|---|
| Accounts | 39 | Current verified masters | Ready for UAT staging | Target validation/approval required |
| CustomerMaster | 896 | Legacy-derived with recorded UAT changes | Ready for UAT staging | Target validation/approval required |
| SupplierMaster | 215 | Legacy-derived with recorded UAT changes | Ready for UAT staging | Target validation/approval required |
| CustomerOpenings | 1150 | Legacy-derived with recorded UAT changes | Ready for UAT staging | Target validation/approval required |
| SupplierOpenings | 180 | Legacy-derived with recorded UAT changes | Ready for UAT staging | Target validation/approval required |
| ReceivedCheques | 49 | Legacy-derived with recorded UAT changes | Ready for UAT staging | Target validation/approval required |
| IssuedCheques | 1 | Synthetic fixture/expected controls | Ready for UAT staging | Target validation/approval required |
| Stock | 938 | Legacy-derived with recorded UAT changes | Ready for UAT staging | Target validation/approval required |
| BankOpening | 1 | Synthetic fixture/expected controls | Ready for UAT staging | Target validation/approval required |
| BankRecon | 1 | Synthetic fixture/expected controls | Ready for UAT staging | Target validation/approval required |
| ControlTotals | 5212 | Synthetic fixture/expected controls | Ready for UAT staging | Target validation/approval required |
| TrialBalance | 39 | Synthetic fixture/expected controls | Ready for UAT staging | Target validation/approval required |

| Module | Positive LKR | Negative LKR | Signed net LKR |
|---|---|---|---|
| CustomerOpenings | 162436582.24 | -321597.10 | 162114985.14 |
| SupplierOpenings | 96073265.55 | -1883015.00 | 94190250.55 |
| ReceivedCheques | 5328628.00 | 0.00 | 5328628.00 |
| IssuedCheques | 15000.00 | 0.00 | 15000.00 |

Stock value 114417245.74 LKR. TB Debit = Credit = 282960858.88 LKR. Expected TB residual Debit = Credit = 1000000.00. Supported duplicate identities: 0. All five zero-net customer cases retain original debt/credit rows.

reports/UAT_changes.csv records 6899 field/fixture changes with original value, UAT value and reason. Reports/Expected_GL.csv and Expected_journals.csv show account-by-account expected import behavior. The UAT review workbook includes the same records. All ten source hashes were rechecked before creation.

Certified production closing exports, real expense/branch/new-SKU classification, complete historical archive and October activity transfer remain separate production work. No application imports, posting, finalization or database reset were executed during file preparation.
