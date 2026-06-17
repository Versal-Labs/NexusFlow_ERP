using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.DTOs.Print;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Entities.Purchasing;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Enums;
using NexusFlow.Shared.Wrapper;

namespace NexusFlow.AppCore.Features.Print
{
    public record GetPrintDocumentQuery(DocumentType DocumentType, string DocumentId) : IRequest<Result<PrintDocumentDto>>;

    public class GetPrintDocumentHandler : IRequestHandler<GetPrintDocumentQuery, Result<PrintDocumentDto>>
    {
        private readonly IErpDbContext _context;

        public GetPrintDocumentHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<PrintDocumentDto>> Handle(GetPrintDocumentQuery request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.DocumentId))
                return Result<PrintDocumentDto>.Failure("Document id is required.");

            return request.DocumentType switch
            {
                DocumentType.SalesOrder or DocumentType.SalesQuotation => await BuildSalesOrderAsync(request.DocumentType, request.DocumentId, cancellationToken),
                DocumentType.SalesInvoice => await BuildSalesInvoiceAsync(request.DocumentId, cancellationToken),
                DocumentType.CreditNote => await BuildCreditNoteAsync(request.DocumentId, cancellationToken),
                DocumentType.PurchaseOrder => await BuildPurchaseOrderAsync(request.DocumentId, cancellationToken),
                DocumentType.GRN => await BuildGrnAsync(request.DocumentId, cancellationToken),
                DocumentType.SupplierBill => await BuildSupplierBillAsync(request.DocumentId, DocumentType.SupplierBill, cancellationToken),
                DocumentType.DebitNote => await BuildSupplierBillAsync(request.DocumentId, DocumentType.DebitNote, cancellationToken),
                DocumentType.CustomerReceipt => await BuildPaymentAsync(request.DocumentId, DocumentType.CustomerReceipt, cancellationToken),
                DocumentType.SupplierPaymentRemittance => await BuildPaymentAsync(request.DocumentId, DocumentType.SupplierPaymentRemittance, cancellationToken),
                DocumentType.StockTransferDeliveryNote => await BuildStockTransferAsync(request.DocumentId, cancellationToken),
                DocumentType.Payslip => await BuildPayslipAsync(request.DocumentId, cancellationToken),
                _ => Result<PrintDocumentDto>.Failure("Unsupported document type.")
            };
        }

        private async Task<Result<PrintDocumentDto>> BuildSalesOrderAsync(DocumentType requestedType, string documentId, CancellationToken cancellationToken)
        {
            if (!TryParseId(documentId, out var id))
                return InvalidId();

            var order = await _context.SalesOrders
                .Include(x => x.Customer)
                .Include(x => x.Items).ThenInclude(x => x.ProductVariant)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (order == null)
                return Result<PrintDocumentDto>.Failure("Sales order not found.");

            var documentType = requestedType == DocumentType.SalesQuotation || order.Status == SalesOrderStatus.Draft
                ? DocumentType.SalesQuotation
                : DocumentType.SalesOrder;

            var subTotal = order.Items.Sum(x => x.Quantity * x.UnitPrice);
            var discount = order.Items.Sum(x => x.Discount);

            return Success(new PrintDocumentDto
            {
                DocumentId = order.Id.ToString(),
                DocumentType = documentType.ToString(),
                DocumentNumber = order.OrderNumber,
                DocumentDate = order.OrderDate,
                CustomerOrSupplierName = order.Customer?.Name ?? string.Empty,
                BillingAddress = CustomerAddress(order.Customer),
                Notes = order.Notes,
                CurrencyCode = order.Customer?.CurrencyCode ?? "LKR",
                SubTotal = subTotal,
                DiscountTotal = discount,
                GrandTotal = order.TotalAmount,
                LineItems = order.Items.Select(x => Line(
                    x.ProductVariant,
                    x.Quantity,
                    x.UnitPrice,
                    x.Discount,
                    0,
                    x.LineTotal)).ToList()
            });
        }

        private async Task<Result<PrintDocumentDto>> BuildSalesInvoiceAsync(string documentId, CancellationToken cancellationToken)
        {
            if (!TryParseId(documentId, out var id))
                return InvalidId();

            var invoice = await _context.SalesInvoices
                .Include(x => x.Customer)
                .Include(x => x.Items).ThenInclude(x => x.ProductVariant)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (invoice == null)
                return Result<PrintDocumentDto>.Failure("Sales invoice not found.");

            return Success(new PrintDocumentDto
            {
                DocumentId = invoice.Id.ToString(),
                DocumentType = DocumentType.SalesInvoice.ToString(),
                DocumentNumber = invoice.InvoiceNumber,
                DocumentDate = invoice.InvoiceDate,
                CustomerOrSupplierName = invoice.Customer?.Name ?? string.Empty,
                BillingAddress = CustomerAddress(invoice.Customer),
                Notes = invoice.Notes,
                CurrencyCode = invoice.Customer?.CurrencyCode ?? "LKR",
                SubTotal = invoice.SubTotal,
                TaxTotal = invoice.TotalTax,
                DiscountTotal = invoice.TotalDiscount,
                GrandTotal = invoice.GrandTotal,
                LineItems = invoice.Items.Select(x => Line(
                    x.ProductVariant,
                    x.Quantity,
                    x.UnitPrice,
                    x.Discount,
                    0,
                    x.LineTotal,
                    x.Description)).ToList()
            });
        }

        private async Task<Result<PrintDocumentDto>> BuildCreditNoteAsync(string documentId, CancellationToken cancellationToken)
        {
            if (!TryParseId(documentId, out var id))
                return InvalidId();

            var creditNote = await _context.CreditNotes
                .Include(x => x.Customer)
                .Include(x => x.SalesInvoice)
                .Include(x => x.Items).ThenInclude(x => x.ProductVariant)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (creditNote == null)
                return Result<PrintDocumentDto>.Failure("Credit note not found.");

            return Success(new PrintDocumentDto
            {
                DocumentId = creditNote.Id.ToString(),
                DocumentType = DocumentType.CreditNote.ToString(),
                DocumentNumber = creditNote.CreditNoteNumber,
                DocumentDate = creditNote.Date,
                CustomerOrSupplierName = creditNote.Customer?.Name ?? string.Empty,
                BillingAddress = CustomerAddress(creditNote.Customer),
                Notes = creditNote.Reason,
                CurrencyCode = creditNote.Customer?.CurrencyCode ?? "LKR",
                SubTotal = creditNote.SubTotal,
                TaxTotal = creditNote.TotalTax,
                GrandTotal = creditNote.GrandTotal,
                LineItems = creditNote.Items.Select(x => Line(
                    x.ProductVariant,
                    x.ReturnedQuantity,
                    x.UnitPrice,
                    0,
                    0,
                    x.LineTotal)).ToList()
            });
        }

        private async Task<Result<PrintDocumentDto>> BuildPurchaseOrderAsync(string documentId, CancellationToken cancellationToken)
        {
            if (!TryParseId(documentId, out var id))
                return InvalidId();

            var purchaseOrder = await _context.PurchaseOrders
                .Include(x => x.Supplier)
                .Include(x => x.Items).ThenInclude(x => x.ProductVariant)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (purchaseOrder == null)
                return Result<PrintDocumentDto>.Failure("Purchase order not found.");

            return Success(new PrintDocumentDto
            {
                DocumentId = purchaseOrder.Id.ToString(),
                DocumentType = DocumentType.PurchaseOrder.ToString(),
                DocumentNumber = purchaseOrder.PoNumber,
                DocumentDate = purchaseOrder.Date,
                CustomerOrSupplierName = purchaseOrder.Supplier?.Name ?? string.Empty,
                BillingAddress = SupplierAddress(purchaseOrder.Supplier),
                Notes = purchaseOrder.Note ?? string.Empty,
                CurrencyCode = purchaseOrder.Supplier?.CurrencyCode ?? "LKR",
                SubTotal = purchaseOrder.Items.Sum(x => x.QuantityOrdered * x.UnitCost),
                GrandTotal = purchaseOrder.TotalAmount,
                LineItems = purchaseOrder.Items.Select(x => Line(
                    x.ProductVariant,
                    x.QuantityOrdered,
                    x.UnitCost,
                    0,
                    0,
                    x.QuantityOrdered * x.UnitCost)).ToList()
            });
        }

        private async Task<Result<PrintDocumentDto>> BuildGrnAsync(string documentId, CancellationToken cancellationToken)
        {
            if (!TryParseId(documentId, out var id))
                return InvalidId();

            var grn = await _context.GRNs
                .Include(x => x.PurchaseOrder).ThenInclude(x => x.Supplier)
                .Include(x => x.Warehouse)
                .Include(x => x.Items).ThenInclude(x => x.ProductVariant)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (grn == null)
                return Result<PrintDocumentDto>.Failure("Goods receipt note not found.");

            return Success(new PrintDocumentDto
            {
                DocumentId = grn.Id.ToString(),
                DocumentType = DocumentType.GRN.ToString(),
                DocumentNumber = grn.GrnNumber,
                DocumentDate = grn.ReceivedDate,
                CustomerOrSupplierName = grn.PurchaseOrder?.Supplier?.Name ?? string.Empty,
                BillingAddress = SupplierAddress(grn.PurchaseOrder?.Supplier),
                ShippingAddress = grn.Warehouse?.Name ?? string.Empty,
                Notes = grn.SupplierInvoiceNo,
                CurrencyCode = grn.PurchaseOrder?.Supplier?.CurrencyCode ?? "LKR",
                SubTotal = grn.Items.Sum(x => x.LineTotal),
                GrandTotal = grn.TotalAmount,
                LineItems = grn.Items.Select(x => Line(
                    x.ProductVariant,
                    x.QuantityReceived,
                    x.UnitCost,
                    0,
                    0,
                    x.LineTotal)).ToList()
            });
        }

        private async Task<Result<PrintDocumentDto>> BuildSupplierBillAsync(string documentId, DocumentType documentType, CancellationToken cancellationToken)
        {
            if (!TryParseId(documentId, out var id))
                return InvalidId();

            var bill = await _context.SupplierBills
                .Include(x => x.Supplier)
                .Include(x => x.Items).ThenInclude(x => x.ProductVariant)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (bill == null)
                return Result<PrintDocumentDto>.Failure(documentType == DocumentType.DebitNote ? "Debit note not found." : "Supplier bill not found.");

            return Success(new PrintDocumentDto
            {
                DocumentId = bill.Id.ToString(),
                DocumentType = documentType.ToString(),
                DocumentNumber = bill.BillNumber,
                DocumentDate = bill.BillDate,
                CustomerOrSupplierName = bill.Supplier?.Name ?? string.Empty,
                BillingAddress = SupplierAddress(bill.Supplier),
                Notes = string.IsNullOrWhiteSpace(bill.SupplierInvoiceNo) ? bill.Remarks : $"{bill.Remarks}\nSupplier invoice: {bill.SupplierInvoiceNo}".Trim(),
                CurrencyCode = bill.Supplier?.CurrencyCode ?? "LKR",
                SubTotal = bill.SubTotal,
                TaxTotal = bill.TaxAmount,
                GrandTotal = bill.GrandTotal,
                LineItems = bill.Items.Select(x => Line(
                    x.ProductVariant,
                    x.Quantity,
                    x.UnitPrice,
                    0,
                    0,
                    x.LineTotal,
                    string.IsNullOrWhiteSpace(x.Description) ? null : x.Description)).ToList()
            });
        }

        private async Task<Result<PrintDocumentDto>> BuildPaymentAsync(string documentId, DocumentType documentType, CancellationToken cancellationToken)
        {
            if (!TryParseId(documentId, out var id))
                return InvalidId();

            var payment = await _context.PaymentTransactions
                .Include(x => x.Customer)
                .Include(x => x.Supplier)
                .Include(x => x.Allocations).ThenInclude(x => x.SalesInvoice)
                .Include(x => x.Allocations).ThenInclude(x => x.SupplierBill)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (payment == null)
                return Result<PrintDocumentDto>.Failure("Payment transaction not found.");

            if (documentType == DocumentType.CustomerReceipt && payment.Type != PaymentType.CustomerReceipt)
                return Result<PrintDocumentDto>.Failure("The selected transaction is not a customer receipt.");

            if (documentType == DocumentType.SupplierPaymentRemittance && payment.Type != PaymentType.SupplierPayment)
                return Result<PrintDocumentDto>.Failure("The selected transaction is not a supplier payment.");

            var isCustomerReceipt = documentType == DocumentType.CustomerReceipt;
            var name = isCustomerReceipt ? payment.Customer?.Name : payment.Supplier?.Name;
            var address = isCustomerReceipt ? CustomerAddress(payment.Customer) : SupplierAddress(payment.Supplier);
            var currency = isCustomerReceipt ? payment.Customer?.CurrencyCode : payment.Supplier?.CurrencyCode;

            return Success(new PrintDocumentDto
            {
                DocumentId = payment.Id.ToString(),
                DocumentType = documentType.ToString(),
                DocumentNumber = payment.ReferenceNo,
                DocumentDate = payment.Date,
                CustomerOrSupplierName = name ?? string.Empty,
                BillingAddress = address,
                Notes = payment.Remarks ?? string.Empty,
                CurrencyCode = currency ?? "LKR",
                SubTotal = payment.Amount,
                GrandTotal = payment.Amount,
                LineItems = payment.Allocations.Select(x => new PrintLineItemDto
                {
                    ItemCode = isCustomerReceipt ? x.SalesInvoice?.InvoiceNumber ?? string.Empty : x.SupplierBill?.BillNumber ?? string.Empty,
                    Description = isCustomerReceipt ? "Invoice Allocation" : "Bill Allocation",
                    Quantity = 1,
                    Unit = "Doc",
                    UnitPrice = x.AmountAllocated,
                    LineTotal = x.AmountAllocated
                }).ToList()
            });
        }

        private async Task<Result<PrintDocumentDto>> BuildStockTransferAsync(string documentId, CancellationToken cancellationToken)
        {
            var transactions = await _context.StockTransactions
                .Include(x => x.ProductVariant)
                .Include(x => x.Warehouse)
                .Where(x => x.ReferenceDocNo == documentId &&
                            (x.Type == StockTransactionType.TransferOut || x.Type == StockTransactionType.TransferIn))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (transactions.Count == 0)
                return Result<PrintDocumentDto>.Failure("Stock transfer not found.");

            var outWarehouses = string.Join(", ", transactions.Where(x => x.Type == StockTransactionType.TransferOut).Select(x => x.Warehouse.Name).Distinct());
            var inWarehouses = string.Join(", ", transactions.Where(x => x.Type == StockTransactionType.TransferIn).Select(x => x.Warehouse.Name).Distinct());
            var lines = transactions
                .Where(x => x.Type == StockTransactionType.TransferOut)
                .Select(x => Line(x.ProductVariant, Math.Abs(x.Qty), x.UnitCost, 0, 0, Math.Abs(x.TotalValue)))
                .ToList();

            return Success(new PrintDocumentDto
            {
                DocumentId = documentId,
                DocumentType = DocumentType.StockTransferDeliveryNote.ToString(),
                DocumentNumber = documentId,
                DocumentDate = transactions.Min(x => x.Date),
                CustomerOrSupplierName = "Stock Transfer",
                BillingAddress = $"From: {outWarehouses}",
                ShippingAddress = $"To: {inWarehouses}",
                Notes = transactions.Select(x => x.Notes).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty,
                CurrencyCode = "LKR",
                SubTotal = lines.Sum(x => x.LineTotal),
                GrandTotal = lines.Sum(x => x.LineTotal),
                LineItems = lines
            });
        }

        private async Task<Result<PrintDocumentDto>> BuildPayslipAsync(string documentId, CancellationToken cancellationToken)
        {
            if (!TryParseId(documentId, out var id))
                return InvalidId();

            var slip = await _context.PayrollSlips
                .Include(x => x.Employee)
                .Include(x => x.PayrollPeriod)
                .Include(x => x.LineItems)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (slip == null)
                return Result<PrintDocumentDto>.Failure("Payslip not found.");

            var basicSalaryLine = new PrintLineItemDto
            {
                ItemCode = "BASIC",
                Description = "Basic Salary",
                Quantity = 1,
                Unit = "Month",
                UnitPrice = slip.GrossBasic,
                LineTotal = slip.GrossBasic
            };
            var earnings = new List<PrintTableRowDto>
            {
                new()
                {
                    Description = basicSalaryLine.Description,
                    Amount = basicSalaryLine.LineTotal,
                    Earnings = basicSalaryLine.LineTotal
                }
            };
            var deductions = new List<PrintTableRowDto>();
            var lines = new List<PrintLineItemDto> { basicSalaryLine };

            foreach (var item in slip.LineItems)
            {
                var signedAmount = item.Type == 2 ? -Math.Abs(item.Amount) : item.Amount;
                lines.Add(new PrintLineItemDto
                {
                    ItemCode = item.Type == 2 ? "DED" : "ADD",
                    Description = item.Description,
                    Quantity = 1,
                    Unit = item.Type == 2 ? "Deduction" : "Addition",
                    UnitPrice = signedAmount,
                    LineTotal = signedAmount
                });

                var row = new PrintTableRowDto
                {
                    Description = item.Description,
                    Amount = Math.Abs(item.Amount)
                };

                if (item.Type == 2)
                {
                    row.Deductions = Math.Abs(item.Amount);
                    deductions.Add(row);
                }
                else
                {
                    row.Earnings = item.Amount;
                    earnings.Add(row);
                }
            }

            return Success(new PrintDocumentDto
            {
                DocumentId = slip.Id.ToString(),
                DocumentType = DocumentType.Payslip.ToString(),
                DocumentNumber = $"PAYSLIP-{slip.Id}",
                DocumentDate = slip.PayrollPeriod?.EndDate ?? DateTime.UtcNow,
                CustomerOrSupplierName = $"{slip.Employee?.FirstName} {slip.Employee?.LastName}".Trim(),
                BillingAddress = $"Employee Code: {slip.Employee?.EmployeeCode}",
                ShippingAddress = slip.PayrollPeriod?.MonthYear ?? string.Empty,
                Notes = slip.PayrollPeriod?.MonthYear ?? string.Empty,
                CurrencyCode = "LKR",
                SubTotal = slip.GrossBasic + slip.TotalAllowances,
                DiscountTotal = slip.TotalDeductions,
                GrandTotal = slip.NetPay,
                LineItems = lines,
                Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["EmployeeCode"] = slip.Employee?.EmployeeCode ?? string.Empty,
                    ["PayPeriod"] = slip.PayrollPeriod?.MonthYear ?? string.Empty,
                    ["GrossBasic"] = slip.GrossBasic.ToString("N2"),
                    ["TotalAllowances"] = slip.TotalAllowances.ToString("N2"),
                    ["TotalEarnings"] = (slip.GrossBasic + slip.TotalAllowances).ToString("N2"),
                    ["TotalDeductions"] = slip.TotalDeductions.ToString("N2"),
                    ["NetPay"] = slip.NetPay.ToString("N2")
                },
                Tables = new Dictionary<string, List<PrintTableRowDto>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PayslipEarnings"] = earnings,
                    ["PayslipDeductions"] = deductions
                }
            });
        }

        private static Result<PrintDocumentDto> Success(PrintDocumentDto dto)
        {
            AddDefaultMergeTables(dto);
            return Result<PrintDocumentDto>.Success(dto);
        }

        private static void AddDefaultMergeTables(PrintDocumentDto dto)
        {
            if (dto.LineItems.Count == 0)
                return;

            var rows = dto.LineItems.Select(PrintTableRowDto.FromLineItem).ToList();
            dto.Tables.TryAdd("LineItems", rows);

            if (Enum.TryParse<DocumentType>(dto.DocumentType, true, out var documentType))
            {
                var tableName = DefaultTableName(documentType);
                if (!string.IsNullOrWhiteSpace(tableName))
                {
                    dto.Tables.TryAdd(tableName, rows);
                }
            }
        }

        private static string? DefaultTableName(DocumentType documentType)
        {
            return documentType switch
            {
                DocumentType.SalesOrder or DocumentType.SalesQuotation or DocumentType.SalesInvoice => "SalesLines",
                DocumentType.CreditNote => "CreditNoteLines",
                DocumentType.PurchaseOrder => "PurchaseLines",
                DocumentType.GRN => "ReceivedLines",
                DocumentType.SupplierBill => "SupplierBillLines",
                DocumentType.DebitNote => "DebitNoteLines",
                DocumentType.CustomerReceipt or DocumentType.SupplierPaymentRemittance => "PaymentAllocations",
                DocumentType.StockTransferDeliveryNote => "TransferLines",
                _ => null
            };
        }

        private static bool TryParseId(string documentId, out int id)
            => int.TryParse(documentId, out id) && id > 0;

        private static Result<PrintDocumentDto> InvalidId()
            => Result<PrintDocumentDto>.Failure("Document id must be a positive number.");

        private static string CustomerAddress(Customer? customer)
        {
            if (customer == null) return string.Empty;
            return JoinLines(customer.AddressLine1, customer.AddressLine2, customer.City, customer.District, customer.Province, customer.Country);
        }

        private static string SupplierAddress(Supplier? supplier)
        {
            if (supplier == null) return string.Empty;
            return JoinLines(supplier.AddressLine1, supplier.AddressLine2, supplier.City, supplier.District, supplier.Province, supplier.Country);
        }

        private static string JoinLines(params string?[] values)
            => string.Join(Environment.NewLine, values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));

        private static PrintLineItemDto Line(
            ProductVariant? variant,
            decimal quantity,
            decimal unitPrice,
            decimal discount,
            decimal taxAmount,
            decimal lineTotal,
            string? descriptionOverride = null)
        {
            return new PrintLineItemDto
            {
                ItemCode = variant?.SKU ?? string.Empty,
                Description = !string.IsNullOrWhiteSpace(descriptionOverride) ? descriptionOverride : variant?.Name ?? string.Empty,
                Quantity = quantity,
                Unit = "Pcs",
                UnitPrice = unitPrice,
                Discount = discount,
                TaxAmount = taxAmount,
                LineTotal = lineTotal
            };
        }
    }
}
