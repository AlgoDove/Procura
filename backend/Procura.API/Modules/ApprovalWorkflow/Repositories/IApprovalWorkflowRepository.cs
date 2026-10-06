using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ApprovalWorkflowEntity = Procura.API.Modules.ApprovalWorkflow.Entities.ApprovalWorkflow;
using Procura.API.Modules.ApprovalWorkflow.Entities;
using Procura.API.Modules.ApprovalWorkflow.Enums;

namespace Procura.API.Modules.ApprovalWorkflow.Repositories
{
    /// <summary>
    /// Repository interface for managing ApprovalWorkflow persistence and relational queries.
    /// </summary>
    public interface IApprovalWorkflowRepository
    {
        /// <summary>
        /// Retrieves an ApprovalWorkflow by its unique ID including related decisions, notifications, and procurement request.
        /// </summary>
        Task<ApprovalWorkflowEntity?> GetByIdAsync(Guid id);

        /// <summary>
        /// Retrieves an ApprovalWorkflow by the associated ProcurementRequest ID.
        /// </summary>
        Task<ApprovalWorkflowEntity?> GetByProcurementRequestIdAsync(Guid procurementRequestId);

        /// <summary>
        /// Retrieves all workflows currently in WAITING_MANAGER_APPROVAL.
        /// </summary>
        Task<IEnumerable<ApprovalWorkflowEntity>> GetPendingApprovalsAsync();

        /// <summary>
        /// Retrieves all workflows with optional status filtering.
        /// </summary>
        Task<IEnumerable<ApprovalWorkflowEntity>> GetAllAsync(WorkflowState? status = null);

        /// <summary>
        /// Adds a new ApprovalWorkflow entity to the change tracker.
        /// </summary>
        Task AddAsync(ApprovalWorkflowEntity workflow);

        /// <summary>
        /// Explicitly adds an ApprovalDecision entity to the change tracker.
        /// </summary>
        Task AddDecisionAsync(ApprovalDecision decision);

        /// <summary>
        /// Explicitly adds a Notification entity to the change tracker.
        /// </summary>
        Task AddNotificationAsync(Notification notification);

        /// <summary>
        /// Explicitly adds an AIAgentExecution entity to the change tracker.
        /// </summary>
        Task AddAgentExecutionAsync(AIAgentExecution execution);

        /// <summary>
        /// Updates an existing ApprovalWorkflow entity in the change tracker.
        /// </summary>
        void Update(ApprovalWorkflowEntity workflow);

        /// <summary>
        /// Checks if an ApprovalWorkflow exists with the specified ID.
        /// </summary>
        Task<bool> ExistsAsync(Guid id);

        /// <summary>
        /// Retrieves a user entity by ID.
        /// </summary>
        Task<Procura.API.Shared.Entities.User?> GetUserByIdAsync(Guid userId);

        /// <summary>
        /// Persists all pending database changes.
        /// </summary>
        Task SaveChangesAsync();
    }
}
