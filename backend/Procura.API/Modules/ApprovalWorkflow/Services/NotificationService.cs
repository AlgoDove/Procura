using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Procura.API.Modules.ApprovalWorkflow.DTOs;
using Procura.API.Modules.ApprovalWorkflow.Entities;
using Procura.API.Modules.ApprovalWorkflow.Enums;
using Procura.API.Modules.ApprovalWorkflow.Repositories;

namespace Procura.API.Modules.ApprovalWorkflow.Services
{
    /// <summary>
    /// Service implementation managing workflow notifications and third-party email notification dispatch.
    /// </summary>
    public class NotificationService : INotificationService
    {
        private readonly INotificationRepository _notificationRepository;
        private readonly IEmailNotificationService _emailNotificationService;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            INotificationRepository notificationRepository,
            IEmailNotificationService emailNotificationService,
            ILogger<NotificationService> logger)
        {
            _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
            _emailNotificationService = emailNotificationService ?? throw new ArgumentNullException(nameof(emailNotificationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<NotificationResponseDto> CreateNotificationAsync(
            Guid workflowId,
            Guid recipientUserId,
            string title,
            string message,
            NotificationType type,
            string? recipientEmail = null,
            string? recipientName = null)
        {
            var notification = new Notification
            {
                Id = Guid.NewGuid(),
                ApprovalWorkflowId = workflowId,
                RecipientUserId = recipientUserId,
                Title = title,
                Message = message,
                Type = type,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _notificationRepository.AddAsync(notification);
            await _notificationRepository.SaveChangesAsync();

            _logger.LogInformation("Notification {NotificationId} created for user {UserId} (Type: {Type})",
                notification.Id, recipientUserId, type);

            // Dispatch third-party email notification if recipient email is available
            if (!string.IsNullOrWhiteSpace(recipientEmail))
            {
                try
                {
                    await _emailNotificationService.SendEmailAsync(
                        recipientEmail,
                        recipientName ?? "User",
                        title,
                        message);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send email notification to {Email}", recipientEmail);
                }
            }

            return new NotificationResponseDto
            {
                Id = notification.Id,
                RecipientUserId = notification.RecipientUserId,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type.ToString(),
                IsRead = notification.IsRead,
                CreatedAt = notification.CreatedAt
            };
        }

        public async Task<IEnumerable<NotificationResponseDto>> GetUserNotificationsAsync(Guid userId, bool? unreadOnly = null)
        {
            var notifications = await _notificationRepository.GetByUserIdAsync(userId, unreadOnly);
            return notifications.Select(n => new NotificationResponseDto
            {
                Id = n.Id,
                RecipientUserId = n.RecipientUserId,
                Title = n.Title,
                Message = n.Message,
                Type = n.Type.ToString(),
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            });
        }

        public async Task<int> GetUnreadCountAsync(Guid userId)
        {
            return await _notificationRepository.GetUnreadCountAsync(userId);
        }

        public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
        {
            await _notificationRepository.MarkAsReadAsync(notificationId, userId);
            await _notificationRepository.SaveChangesAsync();
        }

        public async Task MarkAllAsReadAsync(Guid userId)
        {
            await _notificationRepository.MarkAllAsReadAsync(userId);
            await _notificationRepository.SaveChangesAsync();
        }
    }
}
