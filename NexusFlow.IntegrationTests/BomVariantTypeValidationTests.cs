using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.AppCore.Features.MasterData.Boms.Commands;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using NexusFlow.Domain.Enums;

namespace NexusFlow.IntegrationTests;

public sealed class BomVariantTypeValidationTests : TestBase
{
    private async Task<int> AddFinishedGoodAsync(string sku)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IErpDbContext>();
        var brand = await db.Brands.FirstAsync();
        var unit = await db.UnitOfMeasures.FirstAsync();
        var category = await db.Categories.SingleAsync(x => x.Code == "FG");
        var product = new Product { Name = sku, Brand = brand, UnitOfMeasure = unit, Category = category, Type = ProductType.FinishedGood };
        product.Variants.Add(new ProductVariant { Name = sku, SKU = sku, IsActive = true });
        db.Products.Add(product);
        await db.SaveChangesAsync(CancellationToken.None);
        return product.Variants.Single().Id;
    }

    private static SaveBomCommand Command(int targetId, int materialId) => new(new BomDto
    {
        Name = "Type validation test", ProductVariantId = targetId, BasisQuantity = 1,
        EffectiveFrom = new DateTime(2026, 9, 15), Components = [new() { MaterialVariantId = materialId, Quantity = 1 }]
    });

    [Fact]
    public async Task SaveBom_RejectsRawMaterialAsTheFinishedGoodTarget()
    {
        var result = await SendAsync(Command(100, 200));
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Finished Good");
    }

    [Fact]
    public async Task SaveBom_RejectsFinishedGoodAsAMaterialComponent()
    {
        var targetId = await AddFinishedGoodAsync("FG-TYPE-TARGET");
        var result = await SendAsync(Command(targetId, 200));
        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("Raw Material");
    }

    [Fact]
    public async Task SaveBom_AcceptsFinishedGoodTargetAndRawMaterialComponent()
    {
        var targetId = await AddFinishedGoodAsync("FG-TYPE-VALID");
        var result = await SendAsync(Command(targetId, 100));
        result.Succeeded.Should().BeTrue(result.Message);
    }
}
