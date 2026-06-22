# UAT Test Data

| Data | Example | Purpose |
|---|---|---|
| Open period | 2026-06-01 to 2026-06-30 | Successful postings |
| Closed period | 2026-05-01 to 2026-05-31 | Period rejection |
| Missing-period date | 2026-07-15 | Missing-period rejection |
| RM variant | `FABRIC-BLUE`, 100 metres at LKR 500 FIFO cost | Material issue/return |
| RM variant | `BUTTON-7`, 1,000 pieces at LKR 8 FIFO cost | Decimal and whole UOM checks |
| FG variant | `SHIRT-BLUE-M`, whole-number quantity | Production receipt and sale |
| BOM | Shirt revision 1, basis 1, 1.8m fabric and 7 buttons | Snapshot and requirements |
| Contractor | Active supplier with payable account | Job work and sewing bill |
| Warehouses | Main RM Store and Finished Goods Store | Issue and receipt locations |
| Sewing charge | LKR 250 per receipt batch | Service accrual matching |
| Customer cheque | LKR 50,000 in Cheque Vault | Endorsement and dishonor |
| Supplier bills | Two posted unpaid bills for same supplier | Allocation controls |

Create a second FIFO fabric layer at a different cost to prove issue valuation order. Keep document dates and expected journal amounts in the UAT evidence sheet.
