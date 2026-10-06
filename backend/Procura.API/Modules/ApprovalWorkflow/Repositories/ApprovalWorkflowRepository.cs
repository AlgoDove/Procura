using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ApprovalWorkflowEntity = Procura.API.Modules.ApprovalWorkflow.Entities.ApprovalWorkflow;
using Procura.API.Modules.ApprovalWorkflow.Entities;
using Procura.API.Modules.ApprovalWorkflow.Enums;
using Procura.API.Shared.Data;

namespace Procura.API.Modules.ApprovalWorkflow.Repositories
{
    /// <summary>
    /// Implementation of IApprovalWorkflowRepository using Entity Framework Core and ApplicationDbContext.
    /// </summary>
    public class ApprovalWorkflowRepository : IApprovalWorkflowRepository
    {
        private readonly ApplicationDbContext _context;

        public ApprovalWorkflowRepository(ApplicationDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<ApprovalWorkflowEntity?> GetByIdAsync(Guid id)
        {
            return await _context.ApprovalWorkflows
                .Include(w => w.ProcurementRequest)
                    .ThenInclude(pr => pr.Items)
                .Include(w => w.ProcurementRequest)
                    .ThenInclude(pr => pr.Requester)
                .Include(w => w.Decisions.OrderByDescending(d => d.CreatedAt))
                    .ThenInclude(d => d.Manager)
                .Include(w => w.AIAgentExecutions.OrderBy(a => a.ExecutionOrder))
                .Include(w => w.Notifications.OrderByDescending(n => n.CreatedAt))
                .SingleOrDefaultAsync(w => w.Id == id);
        }

        public async Task<ApprovalWorkflowEntity?> GetByProcurementRequestIdAsync(Guid procurementRequestId)
        {
            return await _context.ApprovalWorkflows
                .Include(w => w.ProcurementRequest)
                    .ThenInclude(pr => pr.Items)
                .Include(w => w.ProcurementRequest)
                    .ThenInclude(pr => pr.Requester)
                .Include(w => w.Decisions.OrderByDescending(d => d.CreatedAt))
                    .ThenInclude(d => d.Manager)
                .Include(w => w.AIAgentExecutions.OrderBy(a => a.ExecutionOrder))
                .Include(w => w.Notifications.OrderByDescending(n => n.CreatedAt))
                .SingleOrDefaultAsync(w => w.ProcurementRequestId == procurementRequestId);
        }

        public async Task<IEnumerable<ApprovalWorkflowEntity>> GetPendingApprovalsAsync()
        {
            return await _context.ApprovalWorkflows
                .Include(w => w.ProcurementRequest)
                    .ThenInclude(pr => pr.Items)
                .Include(w => w.ProcurementRequest)
                    .ThenInclude(pr => pr.Requester)
                .Include(w => w.Decisions)
                    .ThenInclude(d => d.Manager)
                .Where(w => w.CurrentStatus == WorkflowState.WAITING_MANAGER_APPROVAL)
                .OrderByDescending(w => w.UpdatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<ApprovalWorkflowEntity>> GetAllAsync(WorkflowState? status = null)
        {
            var query = _context.ApprovalWorkflows
                .Include(w => w.ProcurementRequest)
                    .ThenInclude(pr => pr.Items)
                .Include(w => w.ProcurementRequest)
                    .ThenInclude(pr => pr.Requester)
                .Include(w => w.Decisions)
                    .ThenInclude(d => d.Manager)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(w => w.CurrentStatus == status.Value);
            }

            return await query.OrderByDescending(w => w.UpdatedAt).ToListAsync();
        }

        public async Task AddAsync(ApprovalWorkflowEntity workflow)
        {
            if (workflow == null) throw new ArgumentNullException(nameof(workflow));
            await _context.ApprovalWorkflows.AddAsync(workflow);
        }

        public async Task AddDecisionAsync(ApprovalDecision decision)
        {
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            await _context.ApprovalDecisions.AddAsync(decision);
        }

        public async Task AddNotificationAsync(Notification notification)
        {
            if (notification == null) throw new ArgumentNullException(nameof(notification));
            await _context.Notifications.AddAsync(notification);
        }

        public async Task AddAgentExecutionAsync(AIAgentExecution execution)
        {
            if (execution == null) throw new ArgumentNullException(nameof(execution));
            await _context.AIAgentExecutions.AddAsync(execution);
        }

        public void Update(ApprovalWorkflowEntity workflow)
        {
            if (workflow == null) throw new ArgumentNullException(nameof(workflow));
            _context.ApprovalWorkflows.Update(workflow);
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            return await _context.ApprovalWorkflows.AnyAsync(w => w.Id == id);
        }

        public async Task<Procura.API.Shared.Entities.User?> GetUserByIdAsync(Guid userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
