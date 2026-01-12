using Microsoft.AspNetCore.SignalR;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Domain.Entities.System;
using NexusFlow.Infrastructure.Hubs;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.Infrastructure.Services
{
    public class NotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ErpDbContext _context;

        public NotificationService(IHubContext<NotificationHub> hubContext, ErpDbContext context)
        {
            _hubContext = hubContext;
            _context = context;
        }

        public async Task SendAsync(string userId, string title, string message, string type = "Info", string url = "#")
        {
            // 1. Persist to Database (So it's there when they refresh)
            var notification = new NotificationItem
            {
                UserId = userId,
                Title = title,
                Message = message,
                Type = type,
                Url = url,
                IsRead = false
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync(new CancellationToken());

            // 2. Push Real-Time to User
            // We broadcast to the specific "User Group"
            await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", new
            {
                id = notification.Id,
                title,
                message,
                type,
                url,
                created = DateTime.Now
            });
        }
    }
}
