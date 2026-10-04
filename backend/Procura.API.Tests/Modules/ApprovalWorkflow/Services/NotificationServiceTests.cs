using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Procura.API.Modules.ApprovalWorkflow.Entities;
using Procura.API.Modules.ApprovalWorkflow.Enums;
using Procura.API.Modules.ApprovalWorkflow.Repositories;
using Procura.API.Modules.ApprovalWorkflow.Services;
using Xunit;

namespace Procura.API.Tests.Modules.ApprovalWorkflow.Services
{
    public class NotificationServiceTests
    {
        private readonly Mock<INotificationRepository> _notificationRepoMock;
        private readonly Mock<IEmailNotificationService> _emailNotificationServiceMock;
        private readonly Mock<ILogger<NotificationService>> _loggerMock;
        private readonly NotificationService _service;

        public NotificationServiceTests()
        {
            _notificationRepoMock = new Mock<INotificationRepository>();
            _emailNotificationServiceMock = new Mock<IEmailNotificationService>();
            _loggerMock = new Mock<ILogger<NotificationService>>();

            _service = new NotificationService(
                _notificationRepoMock.Object,
                _emailNotificationServiceMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task CreateNotificationAsync_CreatesRecordAndDispatchesEmail()
        {
            var workflowId = Guid.NewGuid();
            var recipientUserId = Guid.NewGuid();
            var email = "alice@example.com";
            var name = "Alice";

            var result = await _service.CreateNotificationAsync(
                workflowId,
                recipientUserId,
                "Request Approved",
                "Your request has been approved.",
                NotificationType.APPROVED,
                email,
                name);

            Assert.NotNull(result);
            Assert.Equal("Request Approved", result.Title);
            Assert.Equal("APPROVED", result.Type);
            Assert.False(result.IsRead);

            _notificationRepoMock.Verify(r => r.AddAsync(It.Is<Notification>(n =>
                n.ApprovalWorkflowId == workflowId &&
                n.RecipientUserId == recipientUserId &&
                n.Type == NotificationType.APPROVED)), Times.Once);

            _notificationRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);

            _emailNotificationServiceMock.Verify(e => e.SendEmailAsync(
                email, name, "Request Approved", "Your request has been approved.", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateNotificationAsync_WithoutEmail_DoesNotCallEmailService()
        {
            var workflowId = Guid.NewGuid();
            var recipientUserId = Guid.NewGuid();

            var result = await _service.CreateNotificationAsync(
                workflowId,
                recipientUserId,
                "Request Submitted",
                "Submitted for evaluation.",
                NotificationType.REQUEST_SUBMITTED);

            Assert.NotNull(result);
            _emailNotificationServiceMock.Verify(e => e.SendEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task GetUserNotificationsAsync_ReturnsMappedNotifications()
        {
            var userId = Guid.NewGuid();
            var notifications = new List<Notification>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    RecipientUserId = userId,
                    Title = "Notice 1",
                    Message = "Body 1",
                    Type = NotificationType.APPROVAL_REQUIRED,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                }
            };

            _notificationRepoMock.Setup(r => r.GetByUserIdAsync(userId, false))
                .ReturnsAsync(notifications);

            var result = await _service.GetUserNotificationsAsync(userId, false);

            Assert.Single(result);
            Assert.Equal("Notice 1", result.First().Title);
        }

        [Fact]
        public async Task GetUnreadCountAsync_CallsRepository()
        {
            var userId = Guid.NewGuid();
            _notificationRepoMock.Setup(r => r.GetUnreadCountAsync(userId))
                .ReturnsAsync(5);

            var count = await _service.GetUnreadCountAsync(userId);
            Assert.Equal(5, count);
        }

        [Fact]
        public async Task MarkAsReadAsync_CallsRepositoryAndSaves()
        {
            var notifId = Guid.NewGuid();
            var userId = Guid.NewGuid();

            await _service.MarkAsReadAsync(notifId, userId);

            _notificationRepoMock.Verify(r => r.MarkAsReadAsync(notifId, userId), Times.Once);
            _notificationRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task MarkAllAsReadAsync_CallsRepositoryAndSaves()
        {
            var userId = Guid.NewGuid();

            await _service.MarkAllAsReadAsync(userId);

            _notificationRepoMock.Verify(r => r.MarkAllAsReadAsync(userId), Times.Once);
            _notificationRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }
    }
}
