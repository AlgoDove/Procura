using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Procura.API.AI.Agents.ProcurementRequest;
using Procura.API.AI.Agents.ProcurementRequest.Tools;
using Procura.API.AI.Core;
using Procura.API.AI.Gemini;
using Procura.API.Modules.ProcurementRequest.DTOs;
using Procura.API.Modules.ProcurementRequest.Enums;
using Procura.API.Modules.ProcurementRequest.Services;
using Xunit;

namespace Procura.API.Tests.AI
{
    public class ProcurementRequestAgentTests
    {
        private readonly Mock<IGeminiClient> _geminiClientMock;
        private readonly Mock<IProcurementRequestService> _serviceMock;
        private readonly Mock<ILogger<ProcurementRequestAgent>> _loggerMock;
        private readonly ProcurementRequestDeterministicValidator _validator;
        private readonly ToolRegistry _toolRegistry;
        private readonly ProcurementRequestAgent _agent;

        public ProcurementRequestAgentTests()
        {
            _geminiClientMock = new Mock<IGeminiClient>();
            _serviceMock = new Mock<IProcurementRequestService>();
            _loggerMock = new Mock<ILogger<ProcurementRequestAgent>>();
            _validator = new ProcurementRequestDeterministicValidator();

            var tools = new List<IAgentTool>
            {
                new ValidateDraftDataTool(_validator),
                new CreateDraftRequestTool(_serviceMock.Object),
                new GetProcurementRequestTool(_serviceMock.Object),
                new UpdateDraftRequestTool(_serviceMock.Object)
            };

            _toolRegistry = new ToolRegistry(tools);
            _agent = new ProcurementRequestAgent(_geminiClientMock.Object, _toolRegistry, _loggerMock.Object);
        }

        [Fact]
        public async Task ExecuteAsync_ValidPrompt_ExtractsAndCreatesDraftSuccessfully()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = requesterId,
                RequesterRole = "EMPLOYEE",
                Objective = "We need 20 laptops for new interns. Budget is Rs. 4,000,000."
            };

            var validJson = @"{
                ""Title"": ""Intern Laptops"",
                ""Description"": ""20 laptops for interns with 16GB RAM"",
                ""Justification"": ""New intern cohort"",
                ""Priority"": ""MEDIUM"",
                ""RequiredByDate"": """ + DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd") + @""",
                ""Items"": [
                    {
                        ""ItemName"": ""Development Laptop"",
                        ""Description"": ""16GB RAM, i5 processor"",
                        ""Quantity"": 20,
                        ""Unit"": ""Piece"",
                        ""EstimatedUnitPrice"": 200000
                    }
                ],
                ""HasSufficientInformation"": true,
                ""MissingInformationReasons"": [],
                ""ClarificationPrompt"": null
            }";

            _geminiClientMock
                .Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(validJson));

            var createdResponse = new ProcurementRequestResponseDto
            {
                Id = Guid.NewGuid(),
                RequestNumber = "PR-2026-00001",
                RequesterId = requesterId,
                Title = "Intern Laptops",
                Status = "DRAFT",
                EstimatedTotal = 4000000,
                Items = new List<ProcurementRequestItemResponseDto>
                {
                    new ProcurementRequestItemResponseDto
                    {
                        ItemName = "Development Laptop",
                        Quantity = 20,
                        Unit = "Piece",
                        EstimatedUnitPrice = 200000
                    }
                }
            };

            _serviceMock
                .Setup(s => s.CreateAsync(It.IsAny<CreateProcurementRequestDto>(), requesterId))
                .ReturnsAsync(createdResponse);

            // Act
            var result = await _agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.COMPLETED, result.Status);
            Assert.Equal(createdResponse.Id, result.ProcurementRequestId);
            Assert.Equal("PR-2026-00001", result.RequestNumber);
            Assert.Equal(4000000, result.EstimatedTotal);
            Assert.Contains("PR-2026-00001", result.ExecutionSummary);

            _serviceMock.Verify(s => s.CreateAsync(It.IsAny<CreateProcurementRequestDto>(), requesterId), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_NegativeQuantity_RejectedDeterministicallyWithoutDatabaseCall()
        {
            // Arrange
            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                Objective = "Buy -5 monitors"
            };

            var invalidJson = @"{
                ""Title"": ""Monitors"",
                ""Description"": ""Monitors for lab"",
                ""Justification"": ""Lab upgrade"",
                ""Priority"": ""LOW"",
                ""RequiredByDate"": """ + DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd") + @""",
                ""Items"": [
                    {
                        ""ItemName"": ""27-inch Monitor"",
                        ""Description"": ""4K display"",
                        ""Quantity"": -5,
                        ""Unit"": ""Piece"",
                        ""EstimatedUnitPrice"": 50000
                    }
                ],
                ""HasSufficientInformation"": true
            }";

            _geminiClientMock
                .Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(invalidJson));

            // Act
            var result = await _agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.FAILED, result.Status);
            Assert.Contains(result.ValidationErrors, e => e.Contains("Quantity must be at least 1"));
            _serviceMock.Verify(s => s.CreateAsync(It.IsAny<CreateProcurementRequestDto>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_NegativePrice_RejectedDeterministicallyWithoutDatabaseCall()
        {
            // Arrange
            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                Objective = "Buy office chairs at negative cost"
            };

            var invalidJson = @"{
                ""Title"": ""Office Chairs"",
                ""Description"": ""Chairs"",
                ""Justification"": ""Office seating"",
                ""Priority"": ""LOW"",
                ""RequiredByDate"": """ + DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd") + @""",
                ""Items"": [
                    {
                        ""ItemName"": ""Ergonomic Chair"",
                        ""Description"": ""Mesh back"",
                        ""Quantity"": 5,
                        ""Unit"": ""Piece"",
                        ""EstimatedUnitPrice"": -100
                    }
                ],
                ""HasSufficientInformation"": true
            }";

            _geminiClientMock
                .Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(invalidJson));

            // Act
            var result = await _agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.FAILED, result.Status);
            Assert.Contains(result.ValidationErrors, e => e.Contains("Price cannot be negative"));
            _serviceMock.Verify(s => s.CreateAsync(It.IsAny<CreateProcurementRequestDto>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_MissingInformation_ReturnsNeedsUserInputWithoutMutation()
        {
            // Arrange
            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                Objective = "I need some office supplies."
            };

            var missingInfoJson = @"{
                ""Title"": ""Office Supplies"",
                ""Description"": ""General supplies"",
                ""Justification"": ""Daily operations"",
                ""Priority"": ""MEDIUM"",
                ""Items"": [],
                ""HasSufficientInformation"": false,
                ""MissingInformationReasons"": [""Specific items and quantities needed are missing""],
                ""ClarificationPrompt"": ""Please specify what office supplies you need and in what quantities.""
            }";

            _geminiClientMock
                .Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(missingInfoJson));

            // Act
            var result = await _agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.NEEDS_USER_INPUT, result.Status);
            Assert.NotEmpty(result.MissingFields);
            Assert.NotNull(result.ClarificationPrompt);
            Assert.Contains("Please specify", result.ClarificationPrompt);
            _serviceMock.Verify(s => s.CreateAsync(It.IsAny<CreateProcurementRequestDto>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_ExistingDraftRequest_UpdatesDraftRatherThanCreatingDuplicate()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var existingRequestId = Guid.NewGuid();

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = requesterId,
                RequesterRole = "EMPLOYEE",
                ExistingRequestId = existingRequestId,
                Objective = "Change quantity to 25 laptops"
            };

            var updateJson = @"{
                ""Title"": ""Intern Laptops"",
                ""Description"": ""Updated to 25 laptops"",
                ""Justification"": ""Team expansion"",
                ""Priority"": ""HIGH"",
                ""RequiredByDate"": """ + DateTime.UtcNow.AddDays(20).ToString("yyyy-MM-dd") + @""",
                ""Items"": [
                    {
                        ""ItemName"": ""Development Laptop"",
                        ""Description"": ""16GB RAM"",
                        ""Quantity"": 25,
                        ""Unit"": ""Piece"",
                        ""EstimatedUnitPrice"": 200000
                    }
                ],
                ""HasSufficientInformation"": true
            }";

            _geminiClientMock
                .Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(updateJson));

            var existingRequest = new ProcurementRequestResponseDto
            {
                Id = existingRequestId,
                RequestNumber = "PR-2026-00001",
                RequesterId = requesterId,
                Status = "DRAFT",
                EstimatedTotal = 4000000
            };

            var updatedResponse = new ProcurementRequestResponseDto
            {
                Id = existingRequestId,
                RequestNumber = "PR-2026-00001",
                RequesterId = requesterId,
                Title = "Intern Laptops",
                Status = "DRAFT",
                EstimatedTotal = 5000000,
                Items = new List<ProcurementRequestItemResponseDto>
                {
                    new ProcurementRequestItemResponseDto { ItemName = "Development Laptop", Quantity = 25, EstimatedUnitPrice = 200000 }
                }
            };

            _serviceMock.Setup(s => s.GetByIdAsync(existingRequestId)).ReturnsAsync(existingRequest);
            _serviceMock.Setup(s => s.UpdateAsync(existingRequestId, It.IsAny<UpdateProcurementRequestDto>(), requesterId)).ReturnsAsync(updatedResponse);

            // Act
            var result = await _agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.COMPLETED, result.Status);
            Assert.Equal(existingRequestId, result.ProcurementRequestId);
            Assert.Equal(5000000, result.EstimatedTotal);
            _serviceMock.Verify(s => s.UpdateAsync(existingRequestId, It.IsAny<UpdateProcurementRequestDto>(), requesterId), Times.Once);
            _serviceMock.Verify(s => s.CreateAsync(It.IsAny<CreateProcurementRequestDto>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_NonDraftRequest_CannotBeUpdatedThroughAgent()
        {
            // Arrange
            var requesterId = Guid.NewGuid();
            var existingRequestId = Guid.NewGuid();

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = requesterId,
                RequesterRole = "EMPLOYEE",
                ExistingRequestId = existingRequestId,
                Objective = "Try updating submitted request"
            };

            var updateJson = @"{
                ""Title"": ""Submitted Request Update"",
                ""Description"": ""Update attempt"",
                ""Justification"": ""Reason"",
                ""Priority"": ""MEDIUM"",
                ""RequiredByDate"": """ + DateTime.UtcNow.AddDays(15).ToString("yyyy-MM-dd") + @""",
                ""Items"": [
                    {
                        ""ItemName"": ""Item 1"",
                        ""Description"": ""Desc"",
                        ""Quantity"": 1,
                        ""Unit"": ""Unit"",
                        ""EstimatedUnitPrice"": 100
                    }
                ],
                ""HasSufficientInformation"": true
            }";

            _geminiClientMock
                .Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(updateJson));

            // Request is already SUBMITTED
            var submittedRequest = new ProcurementRequestResponseDto
            {
                Id = existingRequestId,
                RequestNumber = "PR-2026-00001",
                RequesterId = requesterId,
                Status = "SUBMITTED"
            };

            _serviceMock.Setup(s => s.GetByIdAsync(existingRequestId)).ReturnsAsync(submittedRequest);

            // Act
            var result = await _agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.FAILED, result.Status);
            Assert.Contains(result.ErrorMessages, m => m.Contains("Only DRAFT requests can be updated"));
            _serviceMock.Verify(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateProcurementRequestDto>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_UnauthorizedRequestAccess_BlockedWithSecurityViolation()
        {
            // Arrange
            var actualOwnerId = Guid.NewGuid();
            var unauthorizedUserId = Guid.NewGuid();
            var existingRequestId = Guid.NewGuid();

            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = unauthorizedUserId,
                RequesterRole = "EMPLOYEE",
                ExistingRequestId = existingRequestId,
                Objective = "Modify someone else's request"
            };

            var updateJson = @"{
                ""Title"": ""Hijack Request"",
                ""Description"": ""Desc"",
                ""Justification"": ""Reason"",
                ""Priority"": ""MEDIUM"",
                ""RequiredByDate"": """ + DateTime.UtcNow.AddDays(15).ToString("yyyy-MM-dd") + @""",
                ""Items"": [
                    {
                        ""ItemName"": ""Item"",
                        ""Description"": ""Desc"",
                        ""Quantity"": 1,
                        ""Unit"": ""Unit"",
                        ""EstimatedUnitPrice"": 100
                    }
                ],
                ""HasSufficientInformation"": true
            }";

            _geminiClientMock
                .Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok(updateJson));

            var targetRequest = new ProcurementRequestResponseDto
            {
                Id = existingRequestId,
                RequestNumber = "PR-2026-00001",
                RequesterId = actualOwnerId, // Different owner
                Status = "DRAFT"
            };

            _serviceMock.Setup(s => s.GetByIdAsync(existingRequestId)).ReturnsAsync(targetRequest);

            // Act
            var result = await _agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.FAILED, result.Status);
            Assert.Contains(result.ErrorMessages, m => m.Contains("Unauthorized"));
            _serviceMock.Verify(s => s.UpdateAsync(It.IsAny<Guid>(), It.IsAny<UpdateProcurementRequestDto>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task ToolRegistry_ExcludedOperations_CannotBeInvokedByAgent()
        {
            // Verify that Submit, Delete, UpdateStatus are NOT in the ToolRegistry
            Assert.False(_toolRegistry.IsAllowed("SubmitRequest"));
            Assert.False(_toolRegistry.IsAllowed("DeleteRequest"));
            Assert.False(_toolRegistry.IsAllowed("UpdateStatus"));
            Assert.False(_toolRegistry.IsAllowed("ApproveRequest"));
            Assert.False(_toolRegistry.IsAllowed("ExecuteSql"));

            var context = new ToolExecutionContext { WorkflowId = Guid.NewGuid(), RequesterId = Guid.NewGuid() };
            var result = await _toolRegistry.ExecuteToolAsync("SubmitRequest", new object(), context);

            Assert.False(result.Success);
            Assert.True(result.IsSecurityViolation);
            Assert.Contains("blocked", result.ErrorMessage);
        }

        [Fact]
        public async Task ExecuteAsync_GeminiRateLimit429_ProducesSafeStructuredFailure()
        {
            // Arrange
            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                Objective = "Buy server hardware"
            };

            _geminiClientMock
                .Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Fail("Gemini API rate limit or quota exceeded. Please try again later.", isQuota: true));

            // Act
            var result = await _agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.FAILED, result.Status);
            Assert.Contains("quota exceeded", result.ExecutionSummary);
            _serviceMock.Verify(s => s.CreateAsync(It.IsAny<CreateProcurementRequestDto>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_MalformedGeminiOutput_SafelyRejected()
        {
            // Arrange
            var context = new WorkflowContext
            {
                WorkflowId = Guid.NewGuid(),
                RequesterId = Guid.NewGuid(),
                Objective = "Buy laptops"
            };

            _geminiClientMock
                .Setup(g => g.GenerateContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(GeminiClientResult.Ok("This is not JSON at all."));

            // Act
            var result = await _agent.ExecuteAsync(context);

            // Assert
            Assert.Equal(AgentExecutionStatus.FAILED, result.Status);
            Assert.Contains("malformed", result.ExecutionSummary);
            _serviceMock.Verify(s => s.CreateAsync(It.IsAny<CreateProcurementRequestDto>(), It.IsAny<Guid>()), Times.Never);
        }
    }
}
