using CsvHelper;
using CsvHelper.Configuration;
using MediatR;
using NexusFlow.AppCore.DTOs.Inventory;
using NexusFlow.AppCore.DTOs.Master;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NexusFlow.AppCore.Features.MasterData.Products.Commands
{
    public class PreviewLegacyProductsCommand : IRequest<Result<List<ProductImportDto>>>
    {
        public Stream CsvStream { get; set; }
        public PreviewLegacyProductsCommand(Stream csvStream) => CsvStream = csvStream;
    }

    public class PreviewLegacyProductsHandler : IRequestHandler<PreviewLegacyProductsCommand, Result<List<ProductImportDto>>>
    {
        // ========================================================
        // 1. PARENT CATEGORY MAPPING ('type')
        // ========================================================
        private readonly Dictionary<string, string> _typeMapping = new()
        {
            { "01", "Gents" },
            { "02", "Ladies" },
            { "03", "Boys" },
            { "04", "Girls" },
            { "05", "L/S" },
            { "06", "S/S" },
            { "07", "S/O" },
            { "08", "Denim" } // Based on your CSV snippet showing '08' for Denim items
        };

        // ========================================================
        // 2. SUB CATEGORY MAPPING ('type1')
        // ========================================================
        private readonly Dictionary<string, string> _type1Mapping = new()
        {
            { "000", "ASSOSARIES (DO NOT ENTER)" }, { "001", "METERIAL (DO NOT ENTER)" },
            { "002", "ACCOSORIES NEW" }, { "003", "RAW METERIAL NEW" },
            { "101", "SLIM FIT" }, { "102", "REGULAR FIT" }, { "103", "SIDE OPEN" },
            { "104", "J/B,SAMPLE & FBD" }, { "105", "PATTU SHIRT" }, { "106", "BOYS SHINNY SHIRT" },
            { "107", "BOYS SHIRT / DES NO : 399" }, { "108", "GARNET CASUAL WEAR" },
            { "109", "SHIRT WITHOUT PRICE" }, { "110", "OFFICE SHIRT SLIM FIT" },
            { "111", "OFFICE SHIRT REGULAR FIT" }, { "112", "OFFICE SHIRT SIDE OPEN" },
            { "113", "TOP" }, { "114", "BLOUSE" }, { "115", "SKIRT" }, { "116", "FROCK" },
            { "201", "OFFICIAL SLIM FIT TROUSER" }, { "202", "H-DIAZ COTTON TROUSER" },
            { "203", "COTTON SHORTS" }, { "205", "ORDER ITEM" }, { "206", "JOGGER PANT" },
            { "301", "TIE" }, { "302", "BOW" }, { "303", "VESTI SET" },
            { "400", "SHIRT BOX" }, { "401", "LAFIR HAJI-CLOTHES" }, { "402", "JDM-CLOTHES" },
            { "403", "MELBORN - CLOTHES" }, { "404", "SAREE PALACE - CLOTHES" },
            { "405", "HI COM - CLOTHES" }, { "406", "SGQ GARMENT PRODUCTS" },
            { "407", "ARIF BAI" }, { "408", "NOORSONS" }, { "409", "VENTURE HOLDINGS" },
            { "410", "BUTTON" }, { "411", "BOSS ACCESSORIES" }, { "412", "DENIM METERIAL" },
            { "413", "HOUSEHOLD CLOTH" }, { "501", "COTTON TROUSER WITHOUT PRICE" },
            { "502", "SPARE PARTS" }, { "503", "T SHIRT" }, { "504", "HALEEM HAJI" },
            { "505", "PUTLAM CASUAL SHIRT" }, { "506", "TBZ TROUSER" },
            { "507", "NAJATH (ORCHID) COLOMBO" }, { "508", "TOUCH SHIRT" },
            { "509", "CASUAL TROUSER" }, { "515", "LACOAST TROUSER" },
            { "516", "GARNET CASUAL SHIRT" }, { "517", "TBZ SHIRT" },
            { "518", "TOMMY TROUSER" }, { "519", "GLAMOUR" }, { "520", "COOL KHAKIES" },
            { "521", "DENIM LADIES TROUSER" }, { "522", "DENIM SKIRT" },
            { "523", "DENIM JACKET" }, { "524", "DENIM SHORTS" }, { "525", "DENIM FROCK" },
            { "526", "DENIM GENTS TROUSER" }, { "527", "DENIM BOYS TROUSER" }
        };

        public async Task<Result<List<ProductImportDto>>> Handle(PreviewLegacyProductsCommand request, CancellationToken cancellationToken)
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null
            };

            using var reader = new StreamReader(request.CsvStream);
            using var csv = new CsvReader(reader, config);

            var records = csv.GetRecords<LegacyProductCsvRecord>().ToList();
            if (!records.Any()) return Result<List<ProductImportDto>>.Failure("The CSV file is empty.");

            var dtoList = records.Select(r =>
            {
                // Scrubber
                string cleanItemName = r.ItemName?.Replace("\r", "").Replace("\n", "").Trim() ?? "UNKNOWN ITEM";
                string cleanSku = r.ItemCode?.Replace("\r", "").Replace("\n", "").Trim() ?? "";

                // Translate Parent Category
                string rawType = r.Type?.Trim() ?? "";
                string translatedCategory = _typeMapping.TryGetValue(rawType, out var mappedParent)
                                            ? mappedParent : (string.IsNullOrWhiteSpace(rawType) ? "UNCATEGORIZED" : rawType);

                // Translate Sub Category
                string rawType1 = r.Type1?.Trim() ?? "";
                string translatedSubCategory = _type1Mapping.TryGetValue(rawType1, out var mappedSub)
                                               ? mappedSub : rawType1;

                return new ProductImportDto
                {
                    LotNo = r.LotNo?.Replace("\r", "").Replace("\n", "").Trim() ?? "",
                    Category = translatedCategory.ToUpper(),
                    SubCategory = translatedSubCategory.ToUpper(),
                    ItemName = cleanItemName,
                    SKU = cleanSku,
                    Brand = string.IsNullOrWhiteSpace(r.Brand) ? "UNBRANDED" : r.Brand.Trim().ToUpper(),
                    Color = string.IsNullOrWhiteSpace(r.Color) ? "N/A" : r.Color.Trim(),
                    Size = !string.IsNullOrWhiteSpace(r.Size) ? r.Size.Trim() : (!string.IsNullOrWhiteSpace(r.MSize) ? r.MSize.Trim() : "N/A"),
                    AverageCost = r.AverageCost,
                    SellingPrice = r.SellingPrice,
                    MinSellingPrice = r.MinSellingPrice,
                    TotalQuantity = r.TotalQuantity
                };
            }).ToList();

            return Result<List<ProductImportDto>>.Success(dtoList);
        }
    }
}
