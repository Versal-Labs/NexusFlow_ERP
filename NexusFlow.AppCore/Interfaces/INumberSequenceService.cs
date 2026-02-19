using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Interfaces
{
    public interface INumberSequenceService
    {
        Task<string> GenerateNextNumberAsync(string moduleName, CancellationToken ct);
    }
}
