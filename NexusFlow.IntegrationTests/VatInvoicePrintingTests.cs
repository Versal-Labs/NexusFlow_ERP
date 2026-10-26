using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NexusFlow.AppCore.DTOs.Print;
using NexusFlow.AppCore.Features.Print;
using NexusFlow.AppCore.Features.Reporting.Queries;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Finance;
using NexusFlow.Domain.Entities.HR;
using NexusFlow.Domain.Entities.Sales;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Domain.Enums;
using NexusFlow.Infrastructure;
using NexusFlow.Infrastructure.Services;
using Syncfusion.Pdf.Parsing;

namespace NexusFlow.IntegrationTests;

public class VatInvoicePrintingTests : TestBase
{
    [Fact]
    public async Task VatInvoice_LoadsSavedDetailsAndAging_WithDueDateBoundariesAndCredits()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var today = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "Asia/Colombo").Date;
        var customer = await db.Customers.SingleAsync(x => x.Id == 1);
        customer.TaxRegNo = "123456789";
        customer.Phone = "0110000000";
        var invoice = new SalesInvoice
        {
            InvoiceNumber = "26OCT_BR01_1", CustomerId = 1, InvoiceDate = today, DueDate = today.AddDays(45),
            CustomerPoNumber = "PO-123", ApplyVat = true, IsPosted = true,
            SubTotal = 310800, TotalTax = 55944, GrandTotal = 366744,
            SalesRep = new Employee { FirstName = "Test", LastName = "Representative" },
            Items = [new() { ProductVariantId = 200, Description = "Recorded description", Quantity = 24, UnitPrice = 3090, LineTotal = 74160 }]
        };
        db.SalesInvoices.Add(invoice);
        var overdue = new List<SalesInvoice>();
        foreach (var (days, amount) in new[] { (30, 100m), (31, 200m), (60, 300m), (61, 400m), (90, 500m), (91, 600m) })
        {
            var item = new SalesInvoice { CustomerId = 1, InvoiceNumber = $"AGED-{days}", InvoiceDate = today.AddDays(-150),
                DueDate = today.AddDays(-days), IsPosted = true, GrandTotal = amount };
            overdue.Add(item);
            db.SalesInvoices.Add(item);
        }
        db.SalesInvoices.AddRange(
            new SalesInvoice { CustomerId = 1, InvoiceNumber = "DRAFT", InvoiceDate = today, IsPosted = false, GrandTotal = 9999 },
            new SalesInvoice { CustomerId = 1, InvoiceNumber = "FUTURE", InvoiceDate = today.AddDays(1), IsPosted = true, GrandTotal = 9999 },
            new SalesInvoice { Customer = new Customer { Name = "Other Customer", TaxRegNo = "987654321", Phone = "", Email = "", ContactPerson = "", AddressLine1 = "", City = "", Province = "", District = "" }, InvoiceNumber = "OTHER", InvoiceDate = today, IsPosted = true, GrandTotal = 9999 });
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            CustomerId = 1, Date = today, Type = PaymentType.CustomerReceipt, ReferenceNo = "RECEIPT", Amount = 50,
            Allocations = [new PaymentAllocation { SalesInvoice = overdue.Last(), AmountAllocated = 50 }]
        });
        db.CreditNotes.Add(new CreditNote { CustomerId = 1, SalesInvoice = invoice, CreditNoteNumber = "CREDIT", Date = today, IsPosted = true, GrandTotal = 25 });
        await db.SaveChangesAsync();

        var result = await new GetPrintDocumentHandler(db).Handle(new GetPrintDocumentQuery(DocumentType.SalesInvoice, invoice.Id.ToString()), default);
        result.Succeeded.Should().BeTrue();
        var vat = result.Data.VatInvoice!;
        vat.PurchaserTin.Should().Be("123456789");
        vat.PurchaserPhone.Should().Be("0110000000");
        vat.CustomerPoNumber.Should().Be("PO-123");
        vat.SalesRepresentative.Should().Be("Test Representative");
        vat.SupplyDate.Should().Be(invoice.InvoiceDate);
        vat.PaymentTerms.Should().Be("CREDIT 45 Days");
        vat.AccountAging.AsOfDate.Should().Be(today);
        vat.AccountAging.Days0To30.Should().Be(366844);
        vat.AccountAging.Days31To60.Should().Be(500);
        vat.AccountAging.Days61To90.Should().Be(900);
        vat.AccountAging.Over90Days.Should().Be(550);
        vat.AccountAging.UnappliedCredit.Should().Be(25);
        vat.AccountAging.TotalOutstanding.Should().Be(368769);
        var report = await new GetArAgingHandler(db).Handle(new GetArAgingQuery { CustomerId = 1, AsOfDate = today }, default);
        vat.AccountAging.TotalOutstanding.Should().Be(report.Data.Single().TotalOutstanding);
        result.Data.GrandTotal.Should().Be(invoice.GrandTotal);
        result.Data.LineItems.Single().Description.Should().Be("Recorded description");
    }

    [Theory]
    [InlineData(false, 18)]
    [InlineData(true, 0)]
    public async Task LayoutSelection_UsesSavedVatFlag_NotTaxAmount(bool applyVat, int tax)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var invoice = new SalesInvoice { CustomerId = 1, InvoiceNumber = "FLAG", InvoiceDate = DateTime.Today, ApplyVat = applyVat, TotalTax = tax };
        db.SalesInvoices.Add(invoice);
        await db.SaveChangesAsync();
        var result = await new GetPrintDocumentHandler(db).Handle(new GetPrintDocumentQuery(DocumentType.SalesInvoice, invoice.Id.ToString()), default);
        (result.Data.VatInvoice != null).Should().Be(applyVat);
    }

    [Fact]
    public async Task VatRenderer_MatchesReferenceStructure_WithoutLoadingGenericWordTemplate()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        db.DocumentTemplates.Add(new DocumentTemplate { DocumentType = DocumentType.SalesInvoice, TaxProfile = TaxProfile.All,
            TemplateName = "Generic invoice", BlobUrl = "unused.docx", IsDefault = true, IsActive = true });
        await db.SaveChangesAsync();
        var storage = new Mock<IGlobalStorageCoordinator>(MockBehavior.Strict);
        var renderer = Renderer(db, storage);
        var dto = Sample();
        var bytes = await renderer.RenderDocumentToPdfAsync(DocumentType.SalesInvoice, dto);
        using var pdf = new PdfLoadedDocument(bytes);
        pdf.Pages.Count.Should().Be(1);
        var text = Text(pdf);
        // Company name must render in the letterhead, party panel and cheque instruction.
        text.Split("SAMPLE SUPPLIER (PVT) LTD").Length.Should().Be(4);
        foreach (var label in new[] { "TAX INVOICE", "Supplier's Tin", "Purchaser's Tin", "P.O. Number", "Date of Supply",
            "Disc %", "Total Ex. VAT", "Total Value of Supply", "VAT Amount 18%", "Total Amount including VAT", "Amount in Words",
            "Account aging as at", "Over 90", "Unapplied credit", "Customer Signature", "Sales Rep Signature", "Checked By" })
            text.Should().Contain(label);
        text.Should().Contain("310,800.00").And.Contain("55,944.00").And.Contain("366,744.00");
        text.Should().Contain("THREE HUNDRED AND SIXTY SIX THOUSAND SEVEN HUNDRED AND FORTY FOUR ONLY");
        text.Should().Contain("10/21/2026");
        text.Should().NotContain("Hatton National Bank").And.NotContain("2500 100 16199").And.NotContain("14 days");
        storage.VerifyNoOtherCalls();
        SavePreview("VAT_Invoice_Sample.pdf", bytes);
    }

    [Fact]
    public async Task VatRenderer_PreservesDiscountsAndCents_AndDoesNotUseCurrentTaxRate()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var dto = Sample();
        dto.LineItems = [new() { ItemCode = "DISC", Description = "Discounted goods", Quantity = 2, UnitPrice = 100, Discount = 20, LineTotal = 180 }];
        dto.SubTotal = 180;
        dto.DiscountTotal = 30;
        dto.TaxTotal = 22.5m;
        dto.GrandTotal = 172.5m;
        var bytes = await Renderer(db).RenderDocumentToPdfAsync(DocumentType.SalesInvoice, dto);
        using var pdf = new PdfLoadedDocument(bytes);
        pdf.Pages.Count.Should().Be(1);
        Text(pdf).Should().Contain("VAT Amount 15%").And.Contain("150.00").And.Contain("22.50").And.Contain("172.50")
            .And.Contain("Invoice Discount").And.Contain("10.00").And.Contain("FIFTY CENTS ONLY");
        SavePreview("VAT_Invoice_Discount.pdf", bytes);
    }

    [Fact]
    public async Task VatRenderer_PaginatesLongDescriptions_RepeatsHeaders_AndPreservesAllNotes()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var dto = Sample();
        dto.LineItems = Enumerable.Range(1, 50).Select(i => new PrintLineItemDto { ItemCode = $"ITEM-{i:000}",
            Description = $"Distinct goods {i} " + string.Join(" ", Enumerable.Repeat("Detailed description", 5)), Quantity = 1, UnitPrice = 10, LineTotal = 10 }).ToList();
        dto.LineItems[0].Description += string.Join(" ", Enumerable.Repeat("Long description", 400));
        dto.SubTotal = 500;
        dto.TaxTotal = 90;
        dto.GrandTotal = 590;
        dto.Notes = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"Business note {i:000}"));
        var bytes = await Renderer(db).RenderDocumentToPdfAsync(DocumentType.SalesInvoice, dto);
        using var pdf = new PdfLoadedDocument(bytes);
        pdf.Pages.Count.Should().BeGreaterThan(2);
        var text = Text(pdf);
        for (var i = 1; i <= 50; i++) text.Should().Contain($"ITEM-{i:000}");
        text.Should().Contain("Business note 001").And.Contain("Business note 100").And.Contain("590.00");
        for (var i = 0; i < pdf.Pages.Count; i++)
            pdf.Pages[i].ExtractText().Should().Contain("TAX INVOICE").And.Contain($"Page {i + 1} of {pdf.Pages.Count}");
        SavePreview("VAT_Invoice_Multipage.pdf", bytes);
    }

    [Fact]
    public async Task NonVatInvoice_StillUsesExistingGenericRenderer()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var dto = Sample();
        dto.VatInvoice = null;
        dto.TaxTotal = 0;
        var bytes = await Renderer(db).RenderDocumentToPdfAsync(DocumentType.SalesInvoice, dto);
        using var pdf = new PdfLoadedDocument(bytes);
        Text(pdf).Should().Contain("SALESINVOICE").And.Contain("BILL / DELIVER TO").And.NotContain("TAX INVOICE").And.NotContain("Account aging");
    }

    private static DocumentRenderingService Renderer(ErpDbContext db, Mock<IGlobalStorageCoordinator>? storage = null)
    {
        var profile = new Mock<ICompanyProfileService>();
        profile.Setup(x => x.GetProfileAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new CompanyProfile
        {
            CompanyName = "SAMPLE SUPPLIER (PVT) LTD", TaxRegistrationNumber = "111222333", PrimaryAddress = "123 Sample Street, Colombo 15",
            ContactPhone = "0110000000", ContactEmail = "accounts@example.test"
        });
        return new DocumentRenderingService(db, profile.Object, (storage ?? new Mock<IGlobalStorageCoordinator>(MockBehavior.Strict)).Object);
    }

    private static PrintDocumentDto Sample() => new()
    {
        DocumentId = "1", DocumentType = "SalesInvoice", DocumentNumber = "26OCT_BR01_00000001", DocumentDate = new DateTime(2026, 10, 21),
        CustomerOrSupplierName = "SAMPLE PURCHASER", BillingAddress = "456 Sample Road\nPiliyandala", CurrencyCode = "LKR",
        SubTotal = 310800, TaxTotal = 55944, GrandTotal = 366744,
        VatInvoice = new VatInvoicePrintDto
        {
            PurchaserTin = "444555666", PurchaserPhone = "0110000001", CustomerPoNumber = "PO-0001", SupplyDate = new DateTime(2026, 10, 21),
            PaymentTerms = "CREDIT 45 Days", SalesRepresentative = "Sample Rep",
            AccountAging = new() { AsOfDate = new DateTime(2026, 10, 26), Days0To30 = 366744, Days31To60 = 2000, Days61To90 = 3000, Over90Days = 4000, UnappliedCredit = 1000, TotalOutstanding = 374744 }
        },
        LineItems = new[] { ("DENIM 8114 ACID", "DENIM LADIES YOKE DRESS", 3090m), ("L8055", "LADIES DENIM CARGO SKIRT", 2240m),
            ("SHORT 8126 ACID", "DENIM LADIES CORD SHORT", 2390m), ("DENIM 8104", "DENIM LADIES CARGO SIDE SLIT JEANS", 2540m),
            ("TROUSER 7110", "DENIM GIRLS CARGO JEANS", 2690m) }.Select(x => new PrintLineItemDto
            { ItemCode = x.Item1, Description = x.Item2, Quantity = 24, UnitPrice = x.Item3, LineTotal = 24 * x.Item3 }).ToList()
    };

    private static string Text(PdfLoadedDocument pdf) => string.Join("\n", Enumerable.Range(0, pdf.Pages.Count).Select(i => pdf.Pages[i].ExtractText()));
    private static void SavePreview(string name, byte[] bytes)
    {
        if (Environment.GetEnvironmentVariable("NEXUSFLOW_VAT_PREVIEW_DIR") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name), bytes);
    }
}
