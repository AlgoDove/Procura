using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Procura.API.Modules.ApprovalWorkflow.Controllers;
using Procura.API.Modules.ApprovalWorkflow.DTOs;
using Procura.API.Modules.ApprovalWorkflow.Services;
using Xunit;

namespace Procura.API.Tests.Modules.ApprovalWorkflow.Controllers
{
    public class ApprovalWorkflowsControllerTests
    {
        private readonly Mock<IApprovalWorkflowService> _serviceMock;
        private readonly Mock<INotificationService> _notificationServiceMock;
        private readonly ApprovalWorkflowsController _controller;

        public ApprovalWorkflowsControllerTests()
        {
            _serviceMock = new Mock<IApprovalWorkflowService>();
            _notificationServiceMock = new Mock<INotificationService>();
            _controller = new ApprovalWorkflowsController(_serviceMock.Object, _notificationServiceMock.Object);
        }

        private void SetUserContext(Guid userId, string role)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            }, "TestAuth"));

            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };
        }

        [Fact]
        public async Task Approve_WhenValid_ReturnsOkResult()
        {
            var managerId = Guid.NewGuid();
            var workflowId = Guid.NewGuid();
            SetUserContext(managerId, "MANAGER");

            var expectedResponse = new ApprovalWorkflowResponseDto
            {
                Id = workflowId,
                CurrentStatus = "APPROVED"
            };

            _serviceMock.Setup(s => s.ApproveAsync(workflowId, managerId, "MANAGER", "Budget approved."))
                .ReturnsAsync(expectedResponse);

            var result = await _controller.Approve(workflowId, new ApprovalDecisionRequestDto { Comments = "Budget approved." });

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApprovalWorkflowResponseDto>(okResult.Value);
            Assert.Equal("APPROVED", response.CurrentStatus);
        }

        [Fact]
        public async Task Approve_WhenWorkflowNotFound_ReturnsNotFound()
        {
            var managerId = Guid.NewGuid();
            var workflowId = Guid.NewGuid();
            SetUserContext(managerId, "MANAGER");

            _serviceMock.Setup(s => s.ApproveAsync(workflowId, managerId, "MANAGER", null))
                .ThrowsAsync(new KeyNotFoundException("Workflow not found"));

            var result = await _controller.Approve(workflowId, null);

            Assert.IsType<NotFoundObjectResult>(result);
        }

        [Fact]
        public async Task Approve_WhenInvalidStateTransition_ReturnsConflict()
        {
            var managerId = Guid.NewGuid();
            var workflowId = Guid.NewGuid();
            SetUserContext(managerId, "MANAGER");

            _serviceMock.Setup(s => s.ApproveAsync(workflowId, managerId, "MANAGER", null))
                .ThrowsAsync(new InvalidOperationException("Invalid state transition"));

            var result = await _controller.Approve(workflowId, null);

            Assert.IsType<ConflictObjectResult>(result);
        }

        [Fact]
        public async Task Reject_WithMissingComments_ReturnsBadRequest()
        {
            var managerId = Guid.NewGuid();
            var workflowId = Guid.NewGuid();
            SetUserContext(managerId, "MANAGER");

            var result = await _controller.Reject(workflowId, new ApprovalDecisionRequestDto { Comments = "" });

            Assert.IsType<BadRequestObjectResult>(result);
            _serviceMock.Verify(s => s.RejectAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task RequestRevision_WithValidComments_ReturnsOkResult()
        {
            var managerId = Guid.NewGuid();
            var workflowId = Guid.NewGuid();
            SetUserContext(managerId, "MANAGER");

            var expectedResponse = new ApprovalWorkflowResponseDto
            {
                Id = workflowId,
                CurrentStatus = "REVISION_REQUESTED"
            };

            _serviceMock.Setup(s => s.RequestRevisionAsync(workflowId, managerId, "MANAGER", "Need quote adjustment"))
                .ReturnsAsync(expectedResponse);

            var result = await _controller.RequestRevision(workflowId, new ApprovalDecisionRequestDto { Comments = "Need quote adjustment" });

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<ApprovalWorkflowResponseDto>(okResult.Value);
            Assert.Equal("REVISION_REQUESTED", response.CurrentStatus);
        }

        [Fact]
        public async Task GetPending_AsManager_ReturnsOkWithPendingWorkflows()
        {
            var managerId = Guid.NewGuid();
            SetUserContext(managerId, "MANAGER");

            var pending = new List<ApprovalWorkflowResponseDto>
            {
                new() { Id = Guid.NewGuid(), CurrentStatus = "WAITING_MANAGER_APPROVAL" }
            };

            _serviceMock.Setup(s => s.GetPendingWorkflowsAsync(managerId, "MANAGER"))
                .ReturnsAsync(pending);

            var result = await _controller.GetPending();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsAssignableFrom<IEnumerable<ApprovalWorkflowResponseDto>>(okResult.Value);
            Assert.Single(response);
        }

        [Fact]
        public async Task GetAuditTrail_WhenExists_ReturnsOkResult()
        {
            var userId = Guid.NewGuid();
            var workflowId = Guid.NewGuid();
            SetUserContext(userId, "MANAGER");

            var expectedAudit = new WorkflowAuditTrailDto
            {
                WorkflowId = workflowId,
                CurrentStatus = "APPROVED",
                Decisions = new List<ApprovalDecisionResponseDto>
                {
                    new() { Id = Guid.NewGuid(), Decision = "APPROVED" }
                }
            };

            _serviceMock.Setup(s => s.GetAuditTrailAsync(workflowId, userId, "MANAGER"))
                .ReturnsAsync(expectedAudit);

            var result = await _controller.GetAuditTrail(workflowId);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<WorkflowAuditTrailDto>(okResult.Value);
            Assert.Equal("APPROVED", response.CurrentStatus);
            Assert.Single(response.Decisions);
        }

        [Fact]
        public async Task GetUserNotifications_ReturnsOkResult()
        {
            var userId = Guid.NewGuid();
            SetUserContext(userId, "EMPLOYEE");

            var notifications = new List<NotificationResponseDto>
            {
                new() { Id = Guid.NewGuid(), Title = "Request Approved", IsRead = false }
            };

            _notificationServiceMock.Setup(n => n.GetUserNotificationsAsync(userId, false))
                .ReturnsAsync(notifications);

            var result = await _controller.GetUserNotifications(false);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsAssignableFrom<IEnumerable<NotificationResponseDto>>(okResult.Value);
            Assert.Single(response);
        }

        [Fact]
        public async Task MarkNotificationAsRead_ReturnsNoContent()
        {
            var userId = Guid.NewGuid();
            var notifId = Guid.NewGuid();
            SetUserContext(userId, "EMPLOYEE");

            _notificationServiceMock.Setup(n => n.MarkAsReadAsync(notifId, userId))
                .Returns(Task.CompletedTask);

            var result = await _controller.MarkNotificationAsRead(notifId);

            Assert.IsType<NoContentResult>(result);
            _notificationServiceMock.Verify(n => n.MarkAsReadAsync(notifId, userId), Times.Once);
        }
    }
}
