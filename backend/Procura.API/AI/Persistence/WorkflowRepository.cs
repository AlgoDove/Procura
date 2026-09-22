using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Procura.API.AI.Entities;
using Procura.API.Shared.Data;

namespace Procura.API.AI.Persistence
{
    public class WorkflowRepository : IWorkflowRepository
    {
        private readonly ApplicationDbContext _context;

        public WorkflowRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<WorkflowInstance?> GetByIdAsync(Guid id)
        {
            return await _context.WorkflowInstances.SingleOrDefaultAsync(w => w.Id == id);
        }

        public async Task AddAsync(WorkflowInstance instance)
        {
            await _context.WorkflowInstances.AddAsync(instance);
        }

        public void Update(WorkflowInstance instance)
        {
            _context.WorkflowInstances.Update(instance);
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
