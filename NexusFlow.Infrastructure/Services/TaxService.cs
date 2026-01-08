using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Services
{
    public class TaxService : ITaxService
    {
        private readonly IErpDbContext _context;

        public TaxService(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> GetTaxRateAsync(string taxName, DateTime date)
        {
            // Find the rate active for the given date
            // Logic: Find the latest rate where EffectiveDate <= date
            var rate = await _context.TaxRates
                .Include(r => r.TaxType)
                .Where(r => r.TaxType.Name == taxName && r.EffectiveDate <= date)
                .OrderByDescending(r => r.EffectiveDate)
                .Select(r => r.Rate)
                .FirstOrDefaultAsync();

            return rate; // Returns 0 if not found
        }

        public async Task<Result<decimal>> CalculateTaxAsync(decimal amount, string taxName)
        {
            var rate = await GetTaxRateAsync(taxName, DateTime.UtcNow);
            var taxAmount = amount * (rate / 100);
            return Result<decimal>.Success(taxAmount);
        }
    }
}
