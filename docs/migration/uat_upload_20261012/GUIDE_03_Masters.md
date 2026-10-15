# Stage 3 — validate and import masters

Precondition: Stage 1 staged and Stage 2 saved. Prepare permission permits validation; Approve permits approval; Execute permits execution. Actual role names must be checked in the destination.

1. In Select this import select only Accounts, Customer Master and Supplier Master. Uncheck financial modules and Control Totals.
2. Leave Customer codes, Supplier codes, SKU codes and Bank account codes blank to include every record.
3. Select Validate Selected Import. Expected rows: 39 accounts, 896 customers, 215 suppliers. Missing-contact and supported-composite-identity warnings are expected. Mapping, account-type, duplicate, invalid-email and field-length errors must be resolved.
4. Review missing-master UAT fixtures GA735, TK1039, UP1038, VI1041, SH331 and WL329. Names begin UAT; original codes remain text. No contact data is fabricated.
5. In Approval evidence enter: UAT fixture import only, package 2026-10-12. Temporary masters and expense defaults reviewed for functional testing; no production financial sign-off.
6. Select Approve Selected Import, then Execute Approved Import. Select Refresh Progress until completion. If a run fails, inspect the error; fix its cause and use Resume Run for unfinished work. Do not blindly post a second opening.
7. Confirm NET30, TRADE/DOM and STANDARD TIER. Accounts links existing current posting accounts; it does not import folders. Current customers/suppliers without legacy bindings are not matched by similar name.

Expected: all selected masters imported; no opening financial journal yet. Genuine blanks remain blank. Continue to Stage 4. Changes to files/mappings/selection require validation and approval again.
