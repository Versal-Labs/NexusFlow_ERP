using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace NexusFlow.Infrastructure.Hubs
{
    [Authorize] // Only logged-in users can connect
    public class NotificationHub : Hub
    {
        // Frontend calls this to identify itself (though User.Identity.Name is auto-handled)
        public async Task JoinGroup(string userId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        }
    }
}
