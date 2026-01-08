using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Interfaces
{
    public interface ITaxService
    {
        Task<decimal> GetTaxRateAsync(string taxName, DateTime date);
        Task<Result<decimal>> CalculateTaxAsync(decimal amount, string taxName);
    }
}
