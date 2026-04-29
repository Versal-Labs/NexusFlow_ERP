using NexusFlow.AppCore.DTOs.Master.Locations;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.Master;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace NexusFlow.Infrastructure.Data.Seeders
{
    public static class LocationSeeder
    {
        public static async Task SeedAsync(IErpDbContext context, string jsonFilePath)
        {
            // 1. Check if we already have data. If yes, exit early.
            if (await context.Provinces.AnyAsync())
            {
                return;
            }

            // 2. Validate file existence
            if (!File.Exists(jsonFilePath))
            {
                throw new FileNotFoundException($"CRITICAL: The location seed file was not found at {jsonFilePath}");
            }

            // 3. Read and Deserialize the JSON
            var jsonContent = await File.ReadAllTextAsync(jsonFilePath);
            var seedData = JsonSerializer.Deserialize<LocationSeedRoot>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (seedData == null || !seedData.Provinces.Any())
            {
                return;
            }

            // 4. Map DTOs to Entity Framework Models
            var provincesToInsert = new List<Province>();

            foreach (var provDto in seedData.Provinces)
            {
                var province = new Province
                {
                    Name = provDto.Province
                };

                foreach (var distDto in provDto.Districts)
                {
                    var district = new District
                    {
                        Name = distDto.District
                    };

                    foreach (var cityName in distDto.Cities)
                    {
                        district.Cities.Add(new City
                        {
                            Name = cityName
                        });
                    }

                    province.Districts.Add(district);
                }

                provincesToInsert.Add(province);
            }

            // 5. Bulk Insert and Save
            await context.Provinces.AddRangeAsync(provincesToInsert);
            await context.SaveChangesAsync(CancellationToken.None);
        }
    }
}
