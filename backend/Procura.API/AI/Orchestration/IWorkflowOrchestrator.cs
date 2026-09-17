using System;
using System.Threading;
using System.Threading.Tasks;
using Procura.API.AI.Core;

namespace Procura.API.AI.Orchestration
{
    public interface IWorkflowOrchestrator
    {
        Task<WorkflowContext> ProcessWorkflowAsync(
            string objective,
            Guid requesterId,
            string requesterRole,
            Guid? existingRequestId = null,
            Guid? workflowId = null,
            CancellationToken ct = default);

        Task<WorkflowContext?> GetWorkflowAsync(Guid workflowId, Guid requesterId, string requesterRole);
    }
}
