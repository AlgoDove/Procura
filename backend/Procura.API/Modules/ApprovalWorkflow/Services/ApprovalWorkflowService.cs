using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ApprovalWorkflowEntity = Procura.API.Modules.ApprovalWorkflow.Entities.ApprovalWorkflow;
using ProcurementRequestEntity = Procura.API.Modules.ProcurementRequest.Entities.ProcurementRequest;
using Procura.API.Modules.ApprovalWorkflow.DTOs;
using Procura.API.Modules.ApprovalWorkflow.Entities;
using Procura.API.Modules.ApprovalWorkflow.Enums;
using Procura.API.Modules.ApprovalWorkflow.Repositories;
using Procura.API.Modules.ProcurementRequest.Enums;
using Procura.API.Modules.ProcurementRequest.Repositories;
using Procura.API.Modules.VendorEvaluation.Services;
using Procura.API.Integrations.Email;

namespace Procura.API.Modules.ApprovalWorkflow.Services
{
    /// <summary>
    /// Implements core approval workflow business logic, enforcing state invariants,
    /// manager-only approval authorities, notification creation, and vendor evaluation integration.
    /// </summary>
    public class ApprovalWorkflowService : IApprovalWorkflowService
    {
        private readonly IApprovalWorkflowRepository _workflowRepository;
        private readonly IProcurementRequestRepository _procurementRequestRepository;
        private readonly IWorkflowStateTransitionEngine _transitionEngine;
        private readonly IVendorEvaluationService? _vendorEvaluationService;
        private readonly IEmailService? _emailService;
        private readonly ILogger<ApprovalWorkflowService> _logger;

        public ApprovalWorkflowService(
            IApprovalWorkflowRepository workflowRepository,
            IProcurementRequestRepository procurementRequestRepository,
            IWorkflowStateTransitionEngine transitionEngine,
            ILogger<ApprovalWorkflowService> logger,
            IVendorEvaluationService? vendorEvaluationService = null,
            IEmailService? emailService = null)
        {
            _workflowRepository = workflowRepository ?? throw new ArgumentNullException(nameof(workflowRepository));
            _procurementRequestRepository = procurementRequestRepository ?? throw new ArgumentNullException(nameof(procurementRequestRepository));
            _transitionEngine = transitionEngine ?? throw new ArgumentNullException(nameof(transitionEngine));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _vendorEvaluationService = vendorEvaluationService;
            _emailService = emailService;
        }

        public async Task<ApprovalWorkflowResponseDto?> GetWorkflowByIdAsync(Guid workflowId, Guid userId, string role)
        {
            if (workflowId == Guid.Empty) throw new ArgumentException("Workflow ID cannot be empty.");

            var workflow = await _workflowRepository.GetByIdAsync(workflowId);
            if (workflow == null) return null;

            ValidateViewPermission(workflow, userId, role);
            return await MapToDtoAsync(workflow);
        }

        public async Task<ApprovalWorkflowResponseDto?> GetWorkflowByProcurementRequestIdAsync(Guid procurementRequestId, Guid userId, string role)
        {
            if (procurementRequestId == Guid.Empty) throw new ArgumentException("Procurement request ID cannot be empty.");

            var workflow = await _workflowRepository.GetByProcurementRequestIdAsync(procurementRequestId);
            if (workflow == null) return null;

            ValidateViewPermission(workflow, userId, role);
            return await MapToDtoAsync(workflow);
        }

        public async Task<IEnumerable<ApprovalWorkflowResponseDto>> GetPendingWorkflowsAsync(Guid userId, string role)
        {
            var userRole = role.ToUpperInvariant();
            if (userRole != "MANAGER" && userRole != "ADMIN")
            {
                throw new UnauthorizedAccessException("Only Managers and Administrators can view pending approval workflows.");
            }

            var workflows = await _workflowRepository.GetPendingApprovalsAsync();
            var dtos = new List<ApprovalWorkflowResponseDto>();

            foreach (var w in workflows)
            {
                dtos.Add(await MapToDtoAsync(w));
            }

            return dtos;
        }

        public async Task<IEnumerable<ApprovalWorkflowResponseDto>> GetAllWorkflowsAsync(Guid userId, string role, WorkflowState? status = null)
        {
            var userRole = role.ToUpperInvariant();
            var workflows = await _workflowRepository.GetAllAsync(status);

            // Employee isolation: Only view own workflows
            if (userRole == "EMPLOYEE")
            {
                workflows = workflows.Where(w => w.ProcurementRequest?.RequesterId == userId);
            }

            var dtos = new List<ApprovalWorkflowResponseDto>();
            foreach (var w in workflows)
            {
                dtos.Add(await MapToDtoAsync(w));
            }

            return dtos;
        }

        public async Task<ApprovalWorkflowResponseDto> InitializeWorkflowAsync(Guid procurementRequestId, Guid userId, string role)
        {
            if (procurementRequestId == Guid.Empty) throw new ArgumentException("Procurement request ID cannot be empty.");

            var existing = await _workflowRepository.GetByProcurementRequestIdAsync(procurementRequestId);
            if (existing != null)
            {
                throw new InvalidOperationException($"An approval workflow already exists for Procurement Request {procurementRequestId}.");
            }

            var request = await _procurementRequestRepository.GetByIdAsync(procurementRequestId);
            if (request == null)
            {
                throw new KeyNotFoundException($"Procurement request with ID {procurementRequestId} not found.");
            }

            var userRole = role.ToUpperInvariant();
            if (userRole == "EMPLOYEE" && request.RequesterId != userId)
            {
                throw new UnauthorizedAccessException("Not authorized to initialize an approval workflow for another user's request.");
            }

            var workflow = new ApprovalWorkflowEntity
            {
                Id = Guid.NewGuid(),
                ProcurementRequestId = procurementRequestId,
                CurrentStatus = WorkflowState.SUBMITTED,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ProcurementRequest = request
            };

            // Synchronize request status to SUBMITTED
            request.Status = RequestStatus.SUBMITTED;
            request.UpdatedAt = DateTime.UtcNow;

            // Generate notification for submission
            workflow.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(),
                ApprovalWorkflowId = workflow.Id,
                RecipientUserId = request.RequesterId,
                Title = "Procurement Request Submitted",
                Message = $"Procurement Request {request.RequestNumber} has been successfully submitted and entered the approval workflow.",
                Type = NotificationType.REQUEST_SUBMITTED,
                CreatedAt = DateTime.UtcNow
            });

            await _workflowRepository.AddAsync(workflow);
            await _workflowRepository.SaveChangesAsync();

            _logger.LogInformation("ApprovalWorkflow {WorkflowId} initialized for ProcurementRequest {RequestId}", workflow.Id, procurementRequestId);
            return await MapToDtoAsync(workflow);
        }

        public async Task<ApprovalWorkflowResponseDto> TransitionWorkflowAsync(Guid workflowId, WorkflowState targetStatus, Guid userId, string role)
        {
            if (workflowId == Guid.Empty) throw new ArgumentException("Workflow ID cannot be empty.");

            var workflow = await _workflowRepository.GetByIdAsync(workflowId);
            if (workflow == null)
            {
                throw new KeyNotFoundException($"Approval workflow with ID {workflowId} not found.");
            }

            // Enforce deterministic transition rules and role authorization
            _transitionEngine.ValidateTransitionOrThrow(workflow.CurrentStatus, targetStatus, role);

            var previousStatus = workflow.CurrentStatus;
            workflow.CurrentStatus = targetStatus;
            workflow.UpdatedAt = DateTime.UtcNow;

            if (targetStatus == WorkflowState.COMPLETED)
            {
                workflow.CompletedAt = DateTime.UtcNow;
            }

            // Cross-module synchronization
            if (workflow.ProcurementRequest != null)
            {
                workflow.ProcurementRequest.Status = _transitionEngine.MapToProcurementRequestStatus(targetStatus);
                workflow.ProcurementRequest.UpdatedAt = DateTime.UtcNow;
            }

            // Notifications for specific milestones
            if (targetStatus == WorkflowState.WAITING_MANAGER_APPROVAL && workflow.ProcurementRequest != null)
            {
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    ApprovalWorkflowId = workflow.Id,
                    RecipientUserId = workflow.ProcurementRequest.RequesterId,
                    Title = "Approval Required",
                    Message = $"Procurement request {workflow.ProcurementRequest.RequestNumber} has concluded vendor evaluation and is now awaiting Manager approval.",
                    Type = NotificationType.APPROVAL_REQUIRED,
                    CreatedAt = DateTime.UtcNow
                };
                workflow.Notifications.Add(notification);
                await _workflowRepository.AddNotificationAsync(notification);
            }
            else if (targetStatus == WorkflowState.COMPLETED && workflow.ProcurementRequest != null)
            {
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    ApprovalWorkflowId = workflow.Id,
                    RecipientUserId = workflow.ProcurementRequest.RequesterId,
                    Title = "Procurement Request Completed",
                    Message = $"Procurement request {workflow.ProcurementRequest.RequestNumber} has been fulfilled and marked completed.",
                    Type = NotificationType.WORKFLOW_COMPLETED,
                    CreatedAt = DateTime.UtcNow
                };
                workflow.Notifications.Add(notification);
                await _workflowRepository.AddNotificationAsync(notification);
            }

            await _workflowRepository.SaveChangesAsync();

            _logger.LogInformation("ApprovalWorkflow {WorkflowId} transitioned from {Previous} to {Target} by user {UserId} ({Role})",
                workflow.Id, previousStatus, targetStatus, userId, role);

            return await MapToDtoAsync(workflow);
        }

        public async Task<ApprovalWorkflowResponseDto> ApproveAsync(Guid workflowId, Guid managerId, string role, string? comments)
        {
            if (workflowId == Guid.Empty) throw new ArgumentException("Workflow ID cannot be empty.");
            if (comments?.Length > 2000) throw new ArgumentException("Comments cannot exceed 2000 characters.");

            var userRole = role.ToUpperInvariant();
            if (userRole != "MANAGER")
            {
                throw new UnauthorizedAccessException("Only an authorized MANAGER holds approval authority. Administrator or Officer roles cannot approve.");
            }

            var workflow = await _workflowRepository.GetByIdAsync(workflowId);
            if (workflow == null)
            {
                throw new KeyNotFoundException($"Approval workflow with ID {workflowId} not found.");
            }

            // Confirm it is in WAITING_MANAGER_APPROVAL
            _transitionEngine.ValidateTransitionOrThrow(workflow.CurrentStatus, WorkflowState.APPROVED, role);

            // Record immutable decision
            var decision = new ApprovalDecision
            {
                Id = Guid.NewGuid(),
                ApprovalWorkflowId = workflow.Id,
                ManagerId = managerId,
                Decision = ApprovalDecisionType.APPROVED,
                Comments = string.IsNullOrWhiteSpace(comments) ? null : comments.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            workflow.Decisions.Add(decision);
            await _workflowRepository.AddDecisionAsync(decision);

            // Update workflow status
            workflow.CurrentStatus = WorkflowState.APPROVED;
            workflow.UpdatedAt = DateTime.UtcNow;
            workflow.CompletedAt = DateTime.UtcNow;

            // Synchronize ProcurementRequest status
            if (workflow.ProcurementRequest != null)
            {
                workflow.ProcurementRequest.Status = RequestStatus.APPROVED;
                workflow.ProcurementRequest.UpdatedAt = DateTime.UtcNow;

                // Add notification to requester
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    ApprovalWorkflowId = workflow.Id,
                    RecipientUserId = workflow.ProcurementRequest.RequesterId,
                    Title = "Procurement Request Approved",
                    Message = $"Your procurement request {workflow.ProcurementRequest.RequestNumber} has been approved by the Manager.",
                    Type = NotificationType.APPROVED,
                    CreatedAt = DateTime.UtcNow
                };
                workflow.Notifications.Add(notification);
                await _workflowRepository.AddNotificationAsync(notification);
            }

            await _workflowRepository.SaveChangesAsync();

            _logger.LogInformation("ApprovalWorkflow {WorkflowId} APPROVED by Manager {ManagerId}", workflow.Id, managerId);

            // Dispatch transactional approval email (failure must not compromise approval transaction)
            if (_emailService != null && workflow.ProcurementRequest != null)
            {
                var requester = workflow.ProcurementRequest.Requester;
                var recipientEmail = requester?.Email;
                if (!string.IsNullOrWhiteSpace(recipientEmail))
                {
                    var requesterName = requester != null
                        ? $"{requester.FirstName} {requester.LastName}".Trim()
                        : "Requester";
                    var manager = decision.Manager ?? await _workflowRepository.GetUserByIdAsync(managerId);
                    var managerName = manager != null
                        ? $"{manager.FirstName} {manager.LastName}".Trim()
                        : "Manager";

                    try
                    {
                        await _emailService.SendProcurementDecisionEmailAsync(
                            recipientEmail,
                            requesterName,
                            workflow.ProcurementRequest.RequestNumber,
                            workflow.ProcurementRequest.Title,
                            "APPROVED",
                            null,
                            managerName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to send procurement decision email for request {RequestNumber}",
                            workflow.ProcurementRequest.RequestNumber);
                    }
                }
            }

            return await MapToDtoAsync(workflow);
        }

        public async Task<ApprovalWorkflowResponseDto> RejectAsync(Guid workflowId, Guid managerId, string role, string comments)
        {
            if (workflowId == Guid.Empty) throw new ArgumentException("Workflow ID cannot be empty.");

            var userRole = role.ToUpperInvariant();
            if (userRole != "MANAGER")
            {
                throw new UnauthorizedAccessException("Only an authorized MANAGER holds approval authority. Administrator or Officer roles cannot reject.");
            }

            if (string.IsNullOrWhiteSpace(comments))
            {
                throw new ArgumentException("A reason/comment is required when rejecting a procurement request.");
            }

            if (comments.Length > 2000)
            {
                throw new ArgumentException("Comments cannot exceed 2000 characters.");
            }

            var workflow = await _workflowRepository.GetByIdAsync(workflowId);
            if (workflow == null)
            {
                throw new KeyNotFoundException($"Approval workflow with ID {workflowId} not found.");
            }

            // Confirm it is in WAITING_MANAGER_APPROVAL
            _transitionEngine.ValidateTransitionOrThrow(workflow.CurrentStatus, WorkflowState.REJECTED, role);

            // Record immutable decision
            var decision = new ApprovalDecision
            {
                Id = Guid.NewGuid(),
                ApprovalWorkflowId = workflow.Id,
                ManagerId = managerId,
                Decision = ApprovalDecisionType.REJECTED,
                Comments = comments.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            workflow.Decisions.Add(decision);
            await _workflowRepository.AddDecisionAsync(decision);

            // Update workflow status
            workflow.CurrentStatus = WorkflowState.REJECTED;
            workflow.UpdatedAt = DateTime.UtcNow;
            workflow.CompletedAt = DateTime.UtcNow;

            // Synchronize ProcurementRequest status
            if (workflow.ProcurementRequest != null)
            {
                workflow.ProcurementRequest.Status = RequestStatus.REJECTED;
                workflow.ProcurementRequest.UpdatedAt = DateTime.UtcNow;

                // Add notification to requester
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    ApprovalWorkflowId = workflow.Id,
                    RecipientUserId = workflow.ProcurementRequest.RequesterId,
                    Title = "Procurement Request Rejected",
                    Message = $"Your procurement request {workflow.ProcurementRequest.RequestNumber} was rejected. Reason: {comments.Trim()}",
                    Type = NotificationType.REJECTED,
                    CreatedAt = DateTime.UtcNow
                };
                workflow.Notifications.Add(notification);
                await _workflowRepository.AddNotificationAsync(notification);
            }

            await _workflowRepository.SaveChangesAsync();

            _logger.LogInformation("ApprovalWorkflow {WorkflowId} REJECTED by Manager {ManagerId}. Reason: {Comments}", workflow.Id, managerId, comments);

            // Dispatch transactional rejection email (failure must not compromise rejection transaction)
            if (_emailService != null && workflow.ProcurementRequest != null)
            {
                var requester = workflow.ProcurementRequest.Requester;
                var recipientEmail = requester?.Email;
                if (!string.IsNullOrWhiteSpace(recipientEmail))
                {
                    var requesterName = requester != null
                        ? $"{requester.FirstName} {requester.LastName}".Trim()
                        : "Requester";
                    var manager = decision.Manager ?? await _workflowRepository.GetUserByIdAsync(managerId);
                    var managerName = manager != null
                        ? $"{manager.FirstName} {manager.LastName}".Trim()
                        : "Manager";

                    try
                    {
                        await _emailService.SendProcurementDecisionEmailAsync(
                            recipientEmail,
                            requesterName,
                            workflow.ProcurementRequest.RequestNumber,
                            workflow.ProcurementRequest.Title,
                            "REJECTED",
                            comments.Trim(),
                            managerName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to send procurement decision email for request {RequestNumber}",
                            workflow.ProcurementRequest.RequestNumber);
                    }
                }
            }

            return await MapToDtoAsync(workflow);
        }

        public async Task<ApprovalWorkflowResponseDto> RequestRevisionAsync(Guid workflowId, Guid managerId, string role, string comments)
        {
            if (workflowId == Guid.Empty) throw new ArgumentException("Workflow ID cannot be empty.");

            var userRole = role.ToUpperInvariant();
            if (userRole != "MANAGER")
            {
                throw new UnauthorizedAccessException("Only an authorized MANAGER holds approval authority. Administrator or Officer roles cannot request revision.");
            }

            if (string.IsNullOrWhiteSpace(comments))
            {
                throw new ArgumentException("Revision comments detailing the required adjustments are required.");
            }

            if (comments.Length > 2000)
            {
                throw new ArgumentException("Comments cannot exceed 2000 characters.");
            }

            var workflow = await _workflowRepository.GetByIdAsync(workflowId);
            if (workflow == null)
            {
                throw new KeyNotFoundException($"Approval workflow with ID {workflowId} not found.");
            }

            // Confirm it is in WAITING_MANAGER_APPROVAL
            _transitionEngine.ValidateTransitionOrThrow(workflow.CurrentStatus, WorkflowState.REVISION_REQUESTED, role);

            // Record immutable decision
            var decision = new ApprovalDecision
            {
                Id = Guid.NewGuid(),
                ApprovalWorkflowId = workflow.Id,
                ManagerId = managerId,
                Decision = ApprovalDecisionType.REVISION_REQUESTED,
                Comments = comments.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            workflow.Decisions.Add(decision);
            await _workflowRepository.AddDecisionAsync(decision);

            // Update workflow status
            workflow.CurrentStatus = WorkflowState.REVISION_REQUESTED;
            workflow.UpdatedAt = DateTime.UtcNow;

            // Synchronize ProcurementRequest status
            if (workflow.ProcurementRequest != null)
            {
                workflow.ProcurementRequest.Status = RequestStatus.REVISION_REQUESTED;
                workflow.ProcurementRequest.UpdatedAt = DateTime.UtcNow;

                // Add notification to requester
                var notification = new Notification
                {
                    Id = Guid.NewGuid(),
                    ApprovalWorkflowId = workflow.Id,
                    RecipientUserId = workflow.ProcurementRequest.RequesterId,
                    Title = "Procurement Request Revision Requested",
                    Message = $"Revisions were requested for procurement request {workflow.ProcurementRequest.RequestNumber}: {comments.Trim()}",
                    Type = NotificationType.REVISION_REQUESTED,
                    CreatedAt = DateTime.UtcNow
                };
                workflow.Notifications.Add(notification);
                await _workflowRepository.AddNotificationAsync(notification);
            }

            await _workflowRepository.SaveChangesAsync();

            _logger.LogInformation("ApprovalWorkflow {WorkflowId} REVISION REQUESTED by Manager {ManagerId}. Reason: {Comments}", workflow.Id, managerId, comments);
            return await MapToDtoAsync(workflow);
        }

        public async Task<WorkflowAuditTrailDto?> GetAuditTrailAsync(Guid workflowId, Guid userId, string role)
        {
            if (workflowId == Guid.Empty) throw new ArgumentException("Workflow ID cannot be empty.");

            var workflow = await _workflowRepository.GetByIdAsync(workflowId);
            if (workflow == null) return null;

            ValidateViewPermission(workflow, userId, role);

            return new WorkflowAuditTrailDto
            {
                WorkflowId = workflow.Id,
                ProcurementRequestId = workflow.ProcurementRequestId,
                RequestNumber = workflow.ProcurementRequest?.RequestNumber ?? string.Empty,
                RequestTitle = workflow.ProcurementRequest?.Title ?? string.Empty,
                EstimatedTotal = workflow.ProcurementRequest?.EstimatedTotal ?? 0,
                RequesterId = workflow.ProcurementRequest?.RequesterId ?? Guid.Empty,
                RequesterName = workflow.ProcurementRequest?.Requester != null
                    ? $"{workflow.ProcurementRequest.Requester.FirstName} {workflow.ProcurementRequest.Requester.LastName}".Trim()
                    : string.Empty,
                CurrentStatus = workflow.CurrentStatus.ToString(),
                CreatedAt = workflow.CreatedAt,
                UpdatedAt = workflow.UpdatedAt,
                CompletedAt = workflow.CompletedAt,
                Decisions = workflow.Decisions.Select(d => new ApprovalDecisionResponseDto
                {
                    Id = d.Id,
                    ManagerId = d.ManagerId,
                    ManagerName = d.Manager != null ? $"{d.Manager.FirstName} {d.Manager.LastName}".Trim() : string.Empty,
                    Decision = d.Decision.ToString(),
                    Comments = d.Comments,
                    CreatedAt = d.CreatedAt
                }).ToList(),
                AgentExecutions = workflow.AIAgentExecutions.Select(a => new AIAgentExecutionResponseDto
                {
                    Id = a.Id,
                    AgentName = a.AgentName,
                    ExecutionOrder = a.ExecutionOrder,
                    ExecutionStatus = a.ExecutionStatus,
                    InputSummary = a.InputSummary,
                    OutputSummary = a.OutputSummary,
                    ValidationResult = a.ValidationResult,
                    ToolExecutionMetadata = a.ToolExecutionMetadata,
                    StartedAt = a.StartedAt,
                    CompletedAt = a.CompletedAt
                }).ToList(),
                Notifications = workflow.Notifications.Select(n => new NotificationResponseDto
                {
                    Id = n.Id,
                    RecipientUserId = n.RecipientUserId,
                    Title = n.Title,
                    Message = n.Message,
                    Type = n.Type.ToString(),
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                }).ToList()
            };
        }

        private static void ValidateViewPermission(ApprovalWorkflowEntity workflow, Guid userId, string role)
        {
            if (role.ToUpperInvariant() == "EMPLOYEE" && workflow.ProcurementRequest?.RequesterId != userId)
            {
                throw new UnauthorizedAccessException("Not authorized to view another user's approval workflow.");
            }
        }

        private async Task<ApprovalWorkflowResponseDto> MapToDtoAsync(ApprovalWorkflowEntity workflow)
        {
            var dto = new ApprovalWorkflowResponseDto
            {
                Id = workflow.Id,
                ProcurementRequestId = workflow.ProcurementRequestId,
                RequestNumber = workflow.ProcurementRequest?.RequestNumber ?? string.Empty,
                RequestTitle = workflow.ProcurementRequest?.Title ?? string.Empty,
                EstimatedTotal = workflow.ProcurementRequest?.EstimatedTotal ?? 0,
                RequesterId = workflow.ProcurementRequest?.RequesterId ?? Guid.Empty,
                RequesterName = workflow.ProcurementRequest?.Requester != null 
                    ? $"{workflow.ProcurementRequest.Requester.FirstName} {workflow.ProcurementRequest.Requester.LastName}".Trim()
                    : string.Empty,
                CurrentStatus = workflow.CurrentStatus.ToString(),
                CreatedAt = workflow.CreatedAt,
                UpdatedAt = workflow.UpdatedAt,
                CompletedAt = workflow.CompletedAt,
                Decisions = workflow.Decisions.Select(d => new ApprovalDecisionResponseDto
                {
                    Id = d.Id,
                    ManagerId = d.ManagerId,
                    ManagerName = d.Manager != null ? $"{d.Manager.FirstName} {d.Manager.LastName}".Trim() : string.Empty,
                    Decision = d.Decision.ToString(),
                    Comments = d.Comments,
                    CreatedAt = d.CreatedAt
                }).ToList(),
                Notifications = workflow.Notifications.Select(n => new NotificationResponseDto
                {
                    Id = n.Id,
                    RecipientUserId = n.RecipientUserId,
                    Title = n.Title,
                    Message = n.Message,
                    Type = n.Type.ToString(),
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                }).ToList()
            };

            // Integrate executive recommendation summary from Component 3 if service is available
            if (_vendorEvaluationService != null && workflow.ProcurementRequestId != Guid.Empty)
            {
                try
                {
                    dto.VendorRecommendationSummary = await _vendorEvaluationService.GetRecommendationSummaryAsync(workflow.ProcurementRequestId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load vendor recommendation summary for ProcurementRequest {RequestId}", workflow.ProcurementRequestId);
                }
            }

            return dto;
        }
    }
}
