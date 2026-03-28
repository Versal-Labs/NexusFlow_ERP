using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using Syncfusion.Drawing;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Grid;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Sales.Orders.Queries
{
    public class GetSalesOrderPdfQuery : IRequest<Result<byte[]>>
    {
        public int OrderId { get; set; }
    }

    public class GetSalesOrderPdfHandler : IRequestHandler<GetSalesOrderPdfQuery, Result<byte[]>>
    {
        private readonly IErpDbContext _context;

        public GetSalesOrderPdfHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<byte[]>> Handle(GetSalesOrderPdfQuery request, CancellationToken cancellationToken)
        {
            // 1. Fetch Deep Graph
            var order = await _context.SalesOrders
                .Include(o => o.Customer)
                .Include(o => o.SalesRep)
                .Include(o => o.Items)
                    .ThenInclude(i => i.ProductVariant)
                        .ThenInclude(v => v.Product)
                .FirstOrDefaultAsync(o => o.Id == request.OrderId, cancellationToken);

            if (order == null) return Result<byte[]>.Failure("Order not found.");

            // 2. Initialize Syncfusion PDF Document
            using PdfDocument document = new PdfDocument();
            document.PageSettings.Orientation = PdfPageOrientation.Portrait;
            document.PageSettings.Margins.All = 40;
            PdfPage page = document.Pages.Add();
            PdfGraphics graphics = page.Graphics;

            // Fonts & Colors
            PdfFont headerFont = new PdfStandardFont(PdfFontFamily.Helvetica, 24, PdfFontStyle.Bold);
            PdfFont subHeaderFont = new PdfStandardFont(PdfFontFamily.Helvetica, 12, PdfFontStyle.Bold);
            PdfFont regularFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
            PdfBrush primaryColor = new PdfSolidBrush(new PdfColor(13, 110, 253)); // Bootstrap Primary Blue
            PdfBrush darkBrush = PdfBrushes.Black;

            // 3. Draw Header (Document Type & Company Details)
            string docType = order.Status == Domain.Enums.SalesOrderStatus.Draft ? "QUOTATION" : "SALES ORDER";
            graphics.DrawString(docType, headerFont, primaryColor, new PointF(0, 0));

            graphics.DrawString("NexusFlow Enterprise Ltd.", subHeaderFont, darkBrush, new PointF(0, 35));
            graphics.DrawString("123 Business Avenue, Colombo, LK\nPhone: +94 112 345 678\nEmail: sales@nexusflow.com", regularFont, darkBrush, new PointF(0, 55));

            // 4. Draw Customer & Order Meta
            float rightAlign = page.GetClientSize().Width - 200;
            graphics.DrawString($"No: {order.OrderNumber}", subHeaderFont, darkBrush, new PointF(rightAlign, 0));
            graphics.DrawString($"Date: {order.OrderDate:dd MMM yyyy}", regularFont, darkBrush, new PointF(rightAlign, 20));
            graphics.DrawString($"Rep: {order.SalesRep?.FirstName ?? "Internal"}", regularFont, darkBrush, new PointF(rightAlign, 35));
            graphics.DrawString($"Status: {order.Status}", regularFont, darkBrush, new PointF(rightAlign, 50));

            // Customer Bill To
            graphics.DrawString("BILL TO:", subHeaderFont, primaryColor, new PointF(0, 110));
            graphics.DrawString($"{order.Customer.Name}\n{order.Customer.AddressLine1}\n{order.Customer.City}, {order.Customer.Country}\nAttn: {order.Customer.ContactPerson}", regularFont, darkBrush, new PointF(0, 130));

            // 5. Draw Enterprise Line Items Table (PdfGrid)
            PdfGrid grid = new PdfGrid();
            grid.Columns.Add(5);
            grid.Columns[0].Width = 250; // Description
            grid.Columns[1].Width = 50;  // Qty
            grid.Columns[2].Width = 75;  // Price
            grid.Columns[3].Width = 60;  // Discount
            grid.Columns[4].Width = 80;  // Total

            PdfGridRow header = grid.Headers.Add(1)[0];
            header.Cells[0].Value = "Product Description";
            header.Cells[1].Value = "Qty";
            header.Cells[2].Value = "Unit Price";
            header.Cells[3].Value = "Discount";
            header.Cells[4].Value = "Line Total";
            header.ApplyStyle(new PdfGridCellStyle { BackgroundBrush = primaryColor, TextBrush = PdfBrushes.White, Font = subHeaderFont });

            // Format Columns (Align Rights)
            PdfStringFormat alignRight = new PdfStringFormat { Alignment = PdfTextAlignment.Right, LineAlignment = PdfVerticalAlignment.Middle };
            PdfStringFormat alignMiddle = new PdfStringFormat { LineAlignment = PdfVerticalAlignment.Middle };

            for (int i = 1; i < grid.Columns.Count; i++) grid.Columns[i].Format = alignRight;
            grid.Columns[0].Format = alignMiddle;

            // Populate Rows
            foreach (var item in order.Items)
            {
                PdfGridRow row = grid.Rows.Add();
                string sku = item.ProductVariant?.SKU ?? "N/A";
                string prodName = item.ProductVariant?.Product?.Name ?? "Unknown Product";

                row.Cells[0].Value = $"[{sku}] {prodName}";
                row.Cells[1].Value = item.Quantity.ToString("0.##");
                row.Cells[2].Value = item.UnitPrice.ToString("N2");
                row.Cells[3].Value = item.Discount > 0 ? item.Discount.ToString("N2") : "-";
                row.Cells[4].Value = item.LineTotal.ToString("N2");
            }

            // Draw Table onto Page
            PdfGridLayoutFormat layoutFormat = new PdfGridLayoutFormat { Layout = PdfLayoutType.Paginate };
            PdfGridLayoutResult gridResult = grid.Draw(page, new PointF(0, 200), layoutFormat);

            // 6. Draw Grand Total at the bottom of the table
            float finalY = gridResult.Bounds.Bottom + 20;
            graphics = gridResult.Page.Graphics; // Re-grab graphics in case it paginated to a new page

            graphics.DrawString("GRAND TOTAL:", subHeaderFont, darkBrush, new PointF(rightAlign - 50, finalY));
            graphics.DrawString($"{order.Customer.CurrencyCode} {order.TotalAmount:N2}", headerFont, primaryColor, new PointF(rightAlign + 40, finalY - 5));

            if (!string.IsNullOrWhiteSpace(order.Notes))
            {
                graphics.DrawString("Notes:", subHeaderFont, darkBrush, new PointF(0, finalY));
                graphics.DrawString(order.Notes, regularFont, darkBrush, new RectangleF(0, finalY + 15, rightAlign - 60, 50));
            }

            // 7. Export to MemoryStream
            using MemoryStream stream = new MemoryStream();
            document.Save(stream);
            document.Close(true);

            return Result<byte[]>.Success(stream.ToArray());
        }
    }
}
