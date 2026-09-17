using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Procura.API.AI.Agents.ProcurementRequest;
using Procura.API.AI.Core;
using Procura.API.AI.Entities;
using Procura.API.AI.Orchestration;
using Procura.API.AI.Persistence;
using Xunit;

namespace Procura.API.Tests.AI
{
    public class CentralOrchestratorTests
    {
        private readonly Mock<IWorkflowRepository> _workflowRepoMock;
        private readonly Mock<IProcurementRequestAgent> _agentMock;
        private readonly Mock<ILogger<CentralOrchestrator>> _loggerMock;
        private readonly CentralOrchestrator _orchestrator;

        public CentralOrchestratorTests()
        {
            _workflowRepoMock = new Mock<IWorkflowRepository>();
            _agentMock = new Mock<IProcurementRequestAgent>();
            _loggerMock = new Mock<ILogger<CentralOrchestrator>>();

            _agentMock.Setup(a => a.AgentName).Returns("ProcurementRequestAgent");
            _agentMock.Setup(a => a.Stage).Returns(WorkflowStage.PROCUREMENT_REQUEST);

            _orchestrator = new CentralOrchestrator(_workflowRepoMock.Object, _agentMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task ProcessWorkflowAsync_InitializesWorkflowId_CreatesPlan_AndDelegatesToAgent()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var objective = "Need 10 chairs for conference room";

            var agentResult = new AgentResult
            {
                Status = AgentExecutionStatus.COMPLETED,
                ProcurementRequestId = Guid.NewGuid(),
                RequestNumber = "PR-2026-00005",
                EstimatedTotal = 150000,
                ExecutionSummary = "Created draft successfully."
            };

            _agentMock
                .Setup(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(agentResult);

            // Act
            var context = await _orchestrator.ProcessWorkflowAsync(objective, requesterId, "EMPLOYEE");

            // Assert
            Assert.NotEqual(Guid.Empty, context.WorkflowId);
            Assert.Equal(objective, context.Objective);
            Assert.Equal(requesterId, context.RequesterId);

            // Verify 4-step plan exists
            Assert.NotNull(context.Plan);
            Assert.Equal(4, context.Plan.Steps.Count);
            Assert.Equal(StepStatus.COMPLETED, context.Plan.Steps[0].Status);
            Assert.Equal(StepStatus.NOT_STARTED, context.Plan.Steps[1].Status);
            Assert.Equal(StepStatus.NOT_STARTED, context.Plan.Steps[2].Status);
            Assert.Equal(StepStatus.NOT_STARTED, context.Plan.Steps[3].Status);

            // Verify stage completion & handoff
            Assert.Equal(WorkflowStatus.STAGE_COMPLETED, context.Status);
            Assert.Equal(WorkflowStage.VENDOR_SELECTION, context.CurrentStage);
            Assert.Equal("PR-2026-00005", context.RequestNumber);

            // Verify delegation occurred
            _agentMock.Verify(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()), Times.Once);

            // Verify state persisted
            _workflowRepoMock.Verify(r => r.AddAsync(It.IsAny<WorkflowInstance>()), Times.Once);
            _workflowRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);

            // Verify audit trajectory
            Assert.Contains(context.AuditTrail, a => a.Action == "WORKFLOW_CREATED");
            Assert.Contains(context.AuditTrail, a => a.Action == "PLAN_CREATED");
            Assert.Contains(context.AuditTrail, a => a.Action == "AGENT_DELEGATED");
            Assert.Contains(context.AuditTrail, a => a.Action == "WORKFLOW_COMPLETED");
        }

        [Fact]
        public async Task ProcessWorkflowAsync_WhenAgentRequiresUserInput_PausesWorkflowSafely()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var objective = "I need some items";

            var agentResult = new AgentResult
            {
                Status = AgentExecutionStatus.NEEDS_USER_INPUT,
                ClarificationPrompt = "Please specify item names and quantities.",
                ExecutionSummary = "Details missing.",
                MissingFields = new List<string> { "Items", "Quantities" }
            };

            _agentMock
                .Setup(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(agentResult);

            // Act
            var context = await _orchestrator.ProcessWorkflowAsync(objective, requesterId, "EMPLOYEE");

            // Assert
            Assert.Equal(WorkflowStatus.NEEDS_USER_INPUT, context.Status);
            Assert.Equal("Please specify item names and quantities.", context.ClarificationPrompt);
            Assert.Equal(StepStatus.PENDING, context.Plan.Steps[0].Status);

            // Audit records pause
            Assert.Contains(context.AuditTrail, a => a.Action == "WORKFLOW_PAUSED");

            // State persisted with paused status
            _workflowRepoMock.Verify(r => r.AddAsync(It.Is<WorkflowInstance>(w => w.Status == "NEEDS_USER_INPUT")), Times.Once);
            _workflowRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task ProcessWorkflowAsync_WhenAgentFails_RecordsFailureAndHalts()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var objective = "Invalid request";

            var agentResult = new AgentResult
            {
                Status = AgentExecutionStatus.FAILED,
                ExecutionSummary = "Validation failed for price.",
                ErrorMessages = new List<string> { "Price cannot be negative." }
            };

            _agentMock
                .Setup(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(agentResult);

            // Act
            var context = await _orchestrator.ProcessWorkflowAsync(objective, requesterId, "EMPLOYEE");

            // Assert
            Assert.Equal(WorkflowStatus.FAILED, context.Status);
            Assert.Equal(StepStatus.FAILED, context.Plan.Steps[0].Status);
            Assert.Contains("Price cannot be negative.", context.Errors);
            Assert.Contains(context.AuditTrail, a => a.Action == "WORKFLOW_FAILED");

            _workflowRepoMock.Verify(r => r.AddAsync(It.Is<WorkflowInstance>(w => w.Status == "FAILED")), Times.Once);
        }

        [Fact]
        public async Task ProcessWorkflowAsync_UnauthorizedUserAccessingWorkflow_ThrowsUnauthorizedAccessException()
        {
            // Arrange
            var workflowId = Guid.NewGuid();
            var ownerId = Guid.NewGuid();
            var attackerId = Guid.NewGuid();

            var existingInstance = new WorkflowInstance
            {
                Id = workflowId,
                RequesterId = ownerId,
                RequesterRole = "EMPLOYEE",
                Objective = "Confidential items"
            };

            _workflowRepoMock.Setup(r => r.GetByIdAsync(workflowId)).ReturnsAsync(existingInstance);

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                _orchestrator.ProcessWorkflowAsync("Hacked", attackerId, "EMPLOYEE", workflowId: workflowId));
        }

        [Fact]
        public async Task AuditTrail_CapturesTrajectoryWithTimestampsAndActors()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var objective = "Need laptops";

            var agentResult = new AgentResult
            {
                Status = AgentExecutionStatus.COMPLETED,
                ProcurementRequestId = Guid.NewGuid(),
                RequestNumber = "PR-2026-00009",
                ExecutionSummary = "Completed."
            };

            _agentMock
                .Setup(a => a.ExecuteAsync(It.IsAny<WorkflowContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(agentResult);

            // Act
            var context = await _orchestrator.ProcessWorkflowAsync(objective, requesterId, "EMPLOYEE");

            // Assert
            Assert.NotEmpty(context.AuditTrail);
            Assert.All(context.AuditTrail, entry =>
            {
                Assert.NotEqual(default, entry.Timestamp);
                Assert.False(string.IsNullOrWhiteSpace(entry.Actor));
                Assert.False(string.IsNullOrWhiteSpace(entry.Action));
            });
        }
    }
}
