using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Procura.API.Modules.ApprovalWorkflow.Entities;

namespace Procura.API.Modules.ApprovalWorkflow.Repositories
{
    /// <summary>
    /// Repository interface for persisting and querying workflow notification records.
    /// </summary>
    public interface INotificationRepository
    {
        /// <summary>
        /// Retrieves notifications for a specific recipient, with optional unread filter.
        /// </summary>
        Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, bool? unreadOnly = null);

        /// <summary>
        /// Retrieves a notification by its unique ID.
        /// </summary>
        Task<Notification?> GetByIdAsync(Guid id);

        /// <summary>
        /// Gets the count of unread notifications for a user.
        /// </summary>
        Task<int> GetUnreadCountAsync(Guid userId);

        /// <summary>
        /// Adds a new notification to the database.
        /// </summary>
        Task AddAsync(Notification notification);

        /// <summary>
        /// Marks a single notification as read if owned by the user.
        /// </summary>
        Task MarkAsReadAsync(Guid notificationId, Guid userId);

        /// <summary>
        /// Marks all unread notifications for a user as read.
        /// </summary>
        Task MarkAllAsReadAsync(Guid userId);

        /// <summary>
        /// Persists all pending database changes.
        /// </summary>
        Task SaveChangesAsync();
    }
}
