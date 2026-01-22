using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Interfaces
{
    public interface IConfigService
    {
        Task<string> GetString(string key);
        Task<decimal> GetDecimal(string key);
        Task<bool> GetBool(string key);
    }
}
