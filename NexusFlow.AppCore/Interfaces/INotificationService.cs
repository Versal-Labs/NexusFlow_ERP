using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Interfaces
{
    public interface INotificationService
    {
        Task SendAsync(string userId, string title, string message, string type = "Info", string url = "#");
    }
}
