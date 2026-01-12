using MediatR;
using Microsoft.EntityFrameworkCore;
using NexusFlow.AppCore.Interfaces;
using NexusFlow.Shared.Wrapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace NexusFlow.AppCore.Features.Notifications.Queries
{
    public class NotificationDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Type { get; set; } = "Info";
        public DateTime Created { get; set; }
        public bool IsRead { get; set; }
    }

    public class GetUnreadNotificationsQuery : IRequest<Result<List<NotificationDto>>>
    {
        public string UserId { get; set; } = string.Empty;
    }

    public class GetUnreadNotificationsHandler : IRequestHandler<GetUnreadNotificationsQuery, Result<List<NotificationDto>>>
    {
        private readonly IErpDbContext _context;

        public GetUnreadNotificationsHandler(IErpDbContext context)
        {
            _context = context;
        }

        public async Task<Result<List<NotificationDto>>> Handle(GetUnreadNotificationsQuery request, CancellationToken cancellationToken)
        {
            var notifications = await _context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == request.UserId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .Take(10) // Limit to top 10 for the dropdown
                .Select(n => new NotificationDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Message = n.Message,
                    Url = n.Url,
                    Type = n.Type,
                    Created = n.CreatedAt,
                    IsRead = n.IsRead
                })
                .ToListAsync(cancellationToken);

            return Result<List<NotificationDto>>.Success(notifications);
        }
    }
}
