using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.Features.Barcodes;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;
using NexusFlow.Infrastructure;
using Syncfusion.Pdf.Parsing;

namespace NexusFlow.IntegrationTests
{
    public class BarcodeModuleTests : TestBase
    {
        [Fact]
        public void TemplateValidator_RejectsGridThatExceedsPage()
        {
            var template = ValidTemplateDto();
            template.StickersPerRow = 3;

            var errors = BarcodeTemplateValidator.Validate(template);

            errors.Should().Contain(x => x.Contains("exceeding the page width"));
        }

        [Fact]
        public void PayloadValidation_UsesSkuFallbackAndValidatesChecksums()
        {
            var variant = new ProductVariant { SKU = "FG-001", Barcode = "  " };

            GenerateBarcodePdfHandler.ResolvePayload(variant).Should().Be("FG-001");
            GenerateBarcodePdfHandler.IsValidPayload("4006381333931", BarcodeSymbology.EAN13).Should().BeTrue();
            GenerateBarcodePdfHandler.IsValidPayload("036000291452", BarcodeSymbology.UPC).Should().BeTrue();
            GenerateBarcodePdfHandler.IsValidPayload("4006381333932", BarcodeSymbology.EAN13).Should().BeFalse();
            GenerateBarcodePdfHandler.IsValidPayload("036000291453", BarcodeSymbology.UPC).Should().BeFalse();
        }

        [Fact]
        public async Task SaveTemplate_SwitchesTheSingleDefault()
        {
            var first = ValidTemplateDto();
            first.Name = "First";
            first.IsDefault = true;
            var firstResult = await SendAsync(new SaveBarcodeTemplateCommand { Template = first });

            var second = ValidTemplateDto();
            second.Name = "Second";
            second.IsDefault = true;
            var secondResult = await SendAsync(new SaveBarcodeTemplateCommand { Template = second });

            firstResult.Succeeded.Should().BeTrue();
            secondResult.Succeeded.Should().BeTrue();

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            (await context.BarcodeTemplates.CountAsync(x => x.IsDefault)).Should().Be(1);
            (await context.BarcodeTemplates.SingleAsync(x => x.IsDefault)).Name.Should().Be("Second");
        }

        [Fact]
        public async Task GeneratePdf_UsesExactPageSizeAndAddsPageAfterGridCapacity()
        {
            int templateId = await AddTemplateAsync(new BarcodeTemplate
            {
                Name = "Test Grid",
                PageWidthMM = 100,
                PageHeightMM = 50,
                StickerWidthMM = 50,
                StickerHeightMM = 25,
                StickersPerRow = 2,
                RowsPerPage = 2,
                Symbology = BarcodeSymbology.Code128
            });

            var result = await SendAsync(new GenerateBarcodePdfQuery
            {
                TemplateId = templateId,
                Items = [new PrintItem { ProductVariantId = 200, Quantity = 5 }]
            });

            result.Succeeded.Should().BeTrue(string.Join(", ", result.Errors ?? []));
            result.Data.Should().NotBeEmpty();

            using MemoryStream stream = new(result.Data);
            using PdfLoadedDocument document = new(stream);
            document.Pages.Count.Should().Be(2);
            document.Pages[0].Size.Width.Should().BeApproximately(283.465f, 0.5f);
            document.Pages[0].Size.Height.Should().BeApproximately(141.732f, 0.5f);
        }

        [Fact]
        public async Task GeneratePdf_RejectsInvalidSymbologyPayloadAndStickerLimit()
        {
            int templateId = await AddTemplateAsync(new BarcodeTemplate
            {
                Name = "EAN Test",
                PageWidthMM = 50,
                PageHeightMM = 25,
                StickerWidthMM = 50,
                StickerHeightMM = 25,
                StickersPerRow = 1,
                RowsPerPage = 1,
                Symbology = BarcodeSymbology.EAN13
            });

            var invalidPayload = await SendAsync(new GenerateBarcodePdfQuery
            {
                TemplateId = templateId,
                Items = [new PrintItem { ProductVariantId = 200, Quantity = 1 }]
            });
            var overLimit = await SendAsync(new GenerateBarcodePdfQuery
            {
                TemplateId = templateId,
                Items = [new PrintItem { ProductVariantId = 200, Quantity = GenerateBarcodePdfHandler.MaximumStickerCount + 1 }]
            });

            invalidPayload.Succeeded.Should().BeFalse();
            invalidPayload.Errors.Should().ContainSingle(x => x.Contains("FG-001"));
            overLimit.Succeeded.Should().BeFalse();
            overLimit.Errors.Should().ContainSingle(x => x.Contains("5,000"));
        }

        [Theory]
        [InlineData(BarcodeSymbology.EAN13, "4006381333931")]
        [InlineData(BarcodeSymbology.UPC, "036000291452")]
        public async Task GeneratePdf_RendersSupportedNumericSymbologies(BarcodeSymbology symbology, string payload)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
                var variant = await context.ProductVariants.SingleAsync(x => x.Id == 200);
                variant.Barcode = payload;
                await context.SaveChangesAsync();
            }

            int templateId = await AddTemplateAsync(new BarcodeTemplate
            {
                Name = $"{symbology} Test",
                PageWidthMM = 50,
                PageHeightMM = 25,
                StickerWidthMM = 50,
                StickerHeightMM = 25,
                StickersPerRow = 1,
                RowsPerPage = 1,
                Symbology = symbology
            });

            var result = await SendAsync(new GenerateBarcodePdfQuery
            {
                TemplateId = templateId,
                Items = [new PrintItem { ProductVariantId = 200, Quantity = 1 }]
            });

            result.Succeeded.Should().BeTrue(string.Join(", ", result.Errors ?? []));
            result.Data.Should().NotBeEmpty();
        }

        private async Task<int> AddTemplateAsync(BarcodeTemplate template)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            context.BarcodeTemplates.Add(template);
            await context.SaveChangesAsync();
            return template.Id;
        }

        private static BarcodeTemplateDto ValidTemplateDto()
        {
            return new BarcodeTemplateDto
            {
                Name = "Valid",
                PageWidthMM = 100,
                PageHeightMM = 50,
                StickerWidthMM = 50,
                StickerHeightMM = 25,
                StickersPerRow = 2,
                RowsPerPage = 2,
                Symbology = BarcodeSymbology.Code128
            };
        }
    }
}
