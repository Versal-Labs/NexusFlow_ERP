using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Interfaces
{
    public interface IFinancialAccountResolver
    {
        /// <summary>
        /// Reads a SystemConfig Key, finds the configured AccountCode, and resolves its Database ID.
        /// </summary>
        Task<int> ResolveAccountIdAsync(string systemConfigKey, CancellationToken cancellationToken = default);
    }
}
