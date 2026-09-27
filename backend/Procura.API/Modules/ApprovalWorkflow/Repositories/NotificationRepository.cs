using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Procura.API.Modules.ApprovalWorkflow.Entities;
using Procura.API.Shared.Data;

namespace Procura.API.Modules.ApprovalWorkflow.Repositories
{
    /// <summary>
    /// EF Core implementation of INotificationRepository.
    /// </summary>
    public class NotificationRepository : INotificationRepository
    {
        private readonly ApplicationDbContext _context;

        public NotificationRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, bool? unreadOnly = null)
        {
            var query = _context.Notifications
                .Where(n => n.RecipientUserId == userId);

            if (unreadOnly == true)
            {
                query = query.Where(n => !n.IsRead);
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
        }

        public async Task<Notification?> GetByIdAsync(Guid id)
        {
            return await _context.Notifications
                .SingleOrDefaultAsync(n => n.Id == id);
        }

        public async Task<int> GetUnreadCountAsync(Guid userId)
        {
            return await _context.Notifications
                .CountAsync(n => n.RecipientUserId == userId && !n.IsRead);
        }

        public async Task AddAsync(Notification notification)
        {
            if (notification == null) throw new ArgumentNullException(nameof(notification));
            await _context.Notifications.AddAsync(notification);
        }

        public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
        {
            var notification = await _context.Notifications
                .SingleOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == userId);

            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                _context.Notifications.Update(notification);
            }
        }

        public async Task MarkAllAsReadAsync(Guid userId)
        {
            var unread = await _context.Notifications
                .Where(n => n.RecipientUserId == userId && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
            {
                n.IsRead = true;
            }

            if (unread.Any())
            {
                _context.Notifications.UpdateRange(unread);
            }
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
