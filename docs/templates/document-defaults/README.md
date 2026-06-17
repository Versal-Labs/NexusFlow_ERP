# NexusFlow Default Document Templates

This folder contains editable A4 portrait `.docx` templates for the NexusFlow reusable print preview engine.

## How to use

1. Open **Company Settings**.
2. Go to the **Document Templates** tab.
3. Choose the document type and tax profile.
4. Upload the matching `.docx` file from this folder.
5. Mark the template as default when it should be used for generated PDFs.

## Common merge fields

- Company fields: `CompanyName`, `CompanyTaxRegistrationNumber`, `CompanyBusinessRegistrationNumber`, `CompanyAddress`, `CompanyEmail`, `CompanyPhone`, `Image:CompanyLogo`
- Header fields: `DocumentNumber`, `DocumentDate`, `CustomerOrSupplierName`, `BillingAddress`, `ShippingAddress`, `Notes`, `CurrencyCode`
- Totals: `SubTotal`, `TaxTotal`, `DiscountTotal`, `GrandTotal`

## Repeating groups by document type

- `SalesOrder`: `SalesLines` with `ItemCode`, `Description`, `Quantity`, `Unit`, `UnitPrice`, `Discount`, `TaxAmount`, `LineTotal`.
- `SalesQuotation`: `SalesLines` with `ItemCode`, `Description`, `Quantity`, `Unit`, `UnitPrice`, `Discount`, `TaxAmount`, `LineTotal`.
- `SalesInvoice`: `SalesLines` with `ItemCode`, `Description`, `Quantity`, `Unit`, `UnitPrice`, `Discount`, `TaxAmount`, `LineTotal`.
- `CreditNote`: `CreditNoteLines` with `ItemCode`, `Description`, `Quantity`, `UnitPrice`, `TaxAmount`, `LineTotal`.
- `PurchaseOrder`: `PurchaseLines` with `ItemCode`, `Description`, `Quantity`, `Unit`, `UnitPrice`, `LineTotal`.
- `GRN`: `ReceivedLines` with `ItemCode`, `Description`, `Quantity`, `Unit`, `UnitPrice`, `LineTotal`.
- `SupplierBill`: `SupplierBillLines` with `ItemCode`, `Description`, `Quantity`, `Unit`, `UnitPrice`, `Discount`, `TaxAmount`, `LineTotal`.
- `DebitNote`: `DebitNoteLines` with `ItemCode`, `Description`, `Quantity`, `UnitPrice`, `TaxAmount`, `LineTotal`.
- `CustomerReceipt`: `PaymentAllocations` with `ReferenceNumber`, `Description`, `Amount`.
- `SupplierPaymentRemittance`: `PaymentAllocations` with `ReferenceNumber`, `Description`, `Amount`.
- `StockTransferDeliveryNote`: `TransferLines` with `ItemCode`, `Description`, `Quantity`, `Unit`, `UnitPrice`, `LineTotal`.
- `Payslip`: `PayslipEarnings` and `PayslipDeductions` with `Description`, `Amount`.

Payment and bank-detail text is intentionally editable static placeholder content because NexusFlow does not yet expose dynamic bank-detail fields in `PrintDocumentDto`.
Paysheet is not generated here because the current application enum contains `Payslip` but not a separate `Paysheet` document type.
