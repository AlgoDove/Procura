using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Procura.API.Modules.ApprovalWorkflow.DTOs;
using Procura.API.Modules.ApprovalWorkflow.Enums;

namespace Procura.API.Modules.ApprovalWorkflow.Services
{
    /// <summary>
    /// Service contract for creating, dispatching, and managing workflow notifications.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>
        /// Creates a persistent internal notification and optionally dispatches an email notification.
        /// </summary>
        Task<NotificationResponseDto> CreateNotificationAsync(
            Guid workflowId,
            Guid recipientUserId,
            string title,
            string message,
            NotificationType type,
            string? recipientEmail = null,
            string? recipientName = null);

        /// <summary>
        /// Retrieves notifications for a specific user, with optional unread filter.
        /// </summary>
        Task<IEnumerable<NotificationResponseDto>> GetUserNotificationsAsync(Guid userId, bool? unreadOnly = null);

        /// <summary>
        /// Gets the count of unread notifications for a user.
        /// </summary>
        Task<int> GetUnreadCountAsync(Guid userId);

        /// <summary>
        /// Marks a specific notification as read.
        /// </summary>
        Task MarkAsReadAsync(Guid notificationId, Guid userId);

        /// <summary>
        /// Marks all unread notifications for a user as read.
        /// </summary>
        Task MarkAllAsReadAsync(Guid userId);
    }
}
