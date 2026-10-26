# VAT invoice layout and account aging

Prepared 26 October 2026. Scope: the supplied VAT invoice structure for **Sales Invoices** (`/Sales/Invoices`), including account aging. This is a presentation change; invoice posting, tax calculations and number sequences are not changed. No database upgrade is required for this change.

## Agreed business behavior

- Use the saved **Apply VAT** flag to select the VAT layout, including VAT-enabled invoices with zero VAT. Normal invoices retain the existing renderer.
- **Date of Supply** uses **Invoice Date**, as confirmed by the user. Dates print as `MM/DD/YYYY`.
- Read the supplier's name, TIN, address, phone, email and logo from Company Settings. Read the purchaser's name, TIN, address and telephone from the customer record; use the saved customer P.O. number and invoice sales representative.
- Display the reference's five columns: **Description of Goods or Services**, **Qty**, **Disc %**, **Unit Price**, **Total Ex. VAT**. Disc % is `100 x saved line discount / (quantity x unit price)` (zero when the gross value is zero). Saved line totals already include line discounts.
- Show an invoice-level discount separately when present. **Total Value of Supply** is subtotal less that discount, with a minimum of zero, following existing posting calculations. VAT and grand total are the saved amounts. The displayed VAT percentage is derived from those saved amounts, not today's tax setup.
- Positive payment terms are shown as **CREDIT N Days** from the saved due date. No cash/cheque payment method is invented for invoices without a credit period.
- Account aging uses the existing AR Aging calculation for this customer, as of the current date in Asia/Colombo. It includes posted invoices, posted opening balances and debit memos, with dated receipts, allocations, reversals and credits handled by `CustomerBalanceService`. Draft and future documents are excluded by that service.
- **0 - 30** combines the report's **Current** and **1 - 30** overdue bands. Other bands are **31 - 60**, **61 - 90**, **Over 90**. **Total** is the net account balance. **Unapplied credit** is shown separately and deducted in Total. Therefore the four debit bands less unapplied credit reconcile to Total for normally settled accounts. The caption states the aging date and due-date basis.
- A fresh preview or final output reloads current aging. Previously generated history PDFs retain the balances that were printed at that time.
- Keep **Bank**, **Account No** and **Return Clause** contents blank, as requested. Do not copy the reference PDF's bank account, balances or return policy into other invoices.
- Repeat invoice identification, party panels and column headings when goods continue onto another page. Preserve long descriptions and additional notes.

## User workflow

1. Open **Sales (O2C) > AR Invoices** (`/Sales/Invoices`).
2. Select the invoice's **Print** or **Download PDF** action. The document viewer also exposes these actions.
3. In **Document Preview**, check the party details, goods, VAT totals and account-aging date.
4. Use **Refresh Preview** after permitted presentation edits. The server reloads financial figures and aging; they cannot be edited through Print Details.
5. Select **Print** or **Download PDF**. Both use the same VAT renderer. **Generated History** opens the already generated file.

Existing authorization remains `Permissions.Sales.ViewInvoices` (or `Permissions.SuperAdmin`). No new role or permission is introduced. Admins should maintain the supplier profile in **Company Settings**, customer tax/address details, and the invoice number sequence using their existing permissions.

## UAT cases

Preconditions: deploy the updated application to a designated UAT system; use an account allowed to view invoices. Use synthetic customers and invoices for financial test setup.

| ID | Test data and preconditions | Steps | Expected result | Pass/Fail |
|---|---|---|---|---|
| VAT-01 | Posted invoice with Apply VAT enabled; five lines totaling 310,800.00 before VAT; VAT 55,944.00; total 366,744.00 | Open Print, inspect preview, download PDF and print | TAX INVOICE; supplier/purchaser panels; five reference columns; quantity 120; three correct totals; amount in words; signature lines; the same layout in both outputs | Pending hosted UAT |
| VAT-02 | Invoice with Apply VAT disabled | Preview and download | Existing normal invoice template/layout; no VAT-specific account-aging footer | Pending hosted UAT |
| VAT-03 | VAT-enabled invoice with zero saved tax | Preview | VAT structure is selected from the saved flag, not tax amount | Pending hosted UAT |
| VAT-04 | Two units at 100; line discount 20; invoice-level discount 30; saved VAT 22.50 and grand total 172.50 | Preview | Disc % 10.00; line total 180.00; invoice discount 30.00; supply value 150.00; VAT 15%; total 172.50; words include fifty cents | Pending hosted UAT |
| VAT-05 | Current balance 366,744; overdue balances 100 at day 30, 200 at 31, 300 at 60, 400 at 61, 500 at 90, 600 at 91; receipt allocated 50 against day 91; unapplied credit note 25 | Preview, then compare AR Aging for the same customer and printed as-of date | Bands 366,844 / 500 / 900 / 550; unapplied credit 25; total 368,769; no other customer's balance, draft or future invoice included | Pending hosted UAT |
| VAT-06 | Historical invoice; current tax setup differs from saved tax | Preview | Printed tax amount, percentage and totals reflect the saved invoice | Pending hosted UAT |
| VAT-07 | Fifty goods rows; a description longer than one page; long notes | Preview and download all pages | Every item and note retained; repeated headers; no overlaps; totals and signatures legible; page numbers correct | Pending hosted UAT |
| VAT-08 | Existing generated PDF; then a later customer receipt | Open history, then create fresh preview | History remains the original document; fresh preview includes current aging and its date | Pending hosted UAT |
| VAT-09 | Bank details and return policy unconfirmed | Preview | Areas retain labels with blank business contents; no sample bank account or policy is inserted | Pending hosted UAT |

## Regulatory scope requiring business confirmation

The current [IRD specification, Gazette 2481/22](https://www.ird.gov.lk/en/publications/Gazette_Documents/2026_2481-22_E.pdf) permits modifications to its specimen layout if the required specifications are met. [Gazette 2500/106](https://www.ird.gov.lk/en/publications/Gazette_Documents/2026_2500_106_E.pdf) changes the effective date to 1 October 2026.

**Needs business confirmation:** the saved invoice serial must follow the required year/month/entity/numeric sequence; existing `INV-...` invoice numbers are not relabeled or renumbered by printing. Supplier and purchaser TINs must contain the required nine digits and match registration details. Confirm the supplier profile, taxable-only contents and LKR amounts before statutory issue. This layout change does not add foreign-currency conversion, rewrite posted records or certify tax compliance. The user has confirmed the date-of-supply assumption above.

## Verification evidence

The source routes for preview (`POST /api/PrintEngine/Render`), final print/download (`POST /api/PrintEngine/Finalize`) and direct download (`GET /api/PrintEngine/File/SalesInvoice/{id}`) reload the same document and use `DocumentRenderingService`. VAT invoices bypass the generic Word template so both output paths consistently use the reference structure. Other document types retain their existing path.

On 26 October 2026, @Browser reached the authenticated hosted **Sales Invoices** page and verified the existing **View Document**, **Print** and **Download PDF** row controls. Print and View Document did not open a preview in that browser session. The local code has not been deployed, and hosted acceptance of this change is pending.

| Local check | Result |
|---|---|
| Build of the affected projects through the integration test project | Passed; existing repository warnings remain |
| `VatInvoicePrintingTests` | 7 passed, 0 failed, 0 skipped |
| Aging boundaries, customer isolation, draft/future exclusions, receipt allocation and unapplied credit | Passed |
| Saved VAT flag, zero VAT, saved tax rate, line/global discounts and cents in words | Passed |
| VAT template selection and normal invoice fallback | Passed |
| Company name in letterhead, party panel and cheque instruction | Passed after correcting the measured text height |
| Single-page reference and discount samples; 11-page long-description/notes fixture | Rendered with Poppler and visually reviewed; columns, wrapped descriptions, totals, aging and signatures fit |
| Watermark-free release PDF | Pending: the local installation has no configured Syncfusion license; its existing library adds a trial banner/watermark. The application's runtime license registration was preserved. Verify a valid license and clean PDF on the deployment target before statutory use |
| Hosted updated layout, final print/download auditing and physical print | Pending deployment/UAT |

Repeat the automated checks with:

```powershell
dotnet test NexusFlow.IntegrationTests/NexusFlow.IntegrationTests.csproj --filter FullyQualifiedName~VatInvoicePrintingTests -m:1
```

The verification machine's test host initially requested an unavailable .NET patch. The successful local run used `-p:RuntimeFrameworkVersion=10.0.13`, matching the installed runtime, without changing project files. Optional synthetic previews can be generated by setting `NEXUSFLOW_VAT_PREVIEW_DIR` to a local scratch directory for the test run. These test outputs are not issued tax invoices.
