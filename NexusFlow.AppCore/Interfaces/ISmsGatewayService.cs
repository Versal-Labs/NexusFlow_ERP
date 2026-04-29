using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Interfaces
{
    public interface ISmsGatewayService
    {
        Task<bool> SendSmsAsync(string phoneNumber, string message);
    }
}
