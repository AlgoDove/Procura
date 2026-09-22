using System;
using System.Threading.Tasks;
using Procura.API.AI.Entities;

namespace Procura.API.AI.Persistence
{
    public interface IWorkflowRepository
    {
        Task<WorkflowInstance?> GetByIdAsync(Guid id);
        Task AddAsync(WorkflowInstance instance);
        void Update(WorkflowInstance instance);
        Task SaveChangesAsync();
    }
}
