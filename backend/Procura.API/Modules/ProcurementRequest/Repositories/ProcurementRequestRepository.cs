using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Procura.API.Modules.ProcurementRequest.Entities;
using Procura.API.Shared.Data;

namespace Procura.API.Modules.ProcurementRequest.Repositories
{
    public class ProcurementRequestRepository : IProcurementRequestRepository
    {
        private readonly ApplicationDbContext _context;

        public ProcurementRequestRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Entities.ProcurementRequest?> GetByIdAsync(Guid id)
        {
            return await _context.ProcurementRequests
                .Include(pr => pr.Items)
                .SingleOrDefaultAsync(pr => pr.Id == id);
        }

        public async Task<IEnumerable<Entities.ProcurementRequest>> GetAllByUserIdAsync(Guid userId)
        {
            return await _context.ProcurementRequests
                .Include(pr => pr.Items)
                .Where(pr => pr.RequesterId == userId)
                .OrderByDescending(pr => pr.CreatedAt)
                .ToListAsync();
        }

        public async Task<IEnumerable<Entities.ProcurementRequest>> GetAllAsync()
        {
            return await _context.ProcurementRequests
                .Include(pr => pr.Items)
                .OrderByDescending(pr => pr.CreatedAt)
                .ToListAsync();
        }

        public async Task AddAsync(Entities.ProcurementRequest request)
        {
            await _context.ProcurementRequests.AddAsync(request);
        }

        public void Update(Entities.ProcurementRequest request)
        {
            _context.ProcurementRequests.Update(request);
        }

        public void Remove(Entities.ProcurementRequest request)
        {
            _context.ProcurementRequests.Remove(request);
        }

        public async Task<bool> ExistsAsync(Guid id)
        {
            return await _context.ProcurementRequests.AnyAsync(pr => pr.Id == id);
        }

        public async Task<string> GenerateUniqueRequestNumberAsync()
        {
            var year = DateTime.UtcNow.Year;
            var prefix = $"PR-{year}-";
            var existingNumbers = await _context.ProcurementRequests
                .Where(r => r.RequestNumber.StartsWith(prefix))
                .Select(r => r.RequestNumber)
                .ToListAsync();

            int maxSeq = 0;
            foreach (var num in existingNumbers)
            {
                if (num.Length > prefix.Length)
                {
                    var suffix = num.Substring(prefix.Length);
                    if (int.TryParse(suffix, out int seq) && seq > maxSeq)
                    {
                        maxSeq = seq;
                    }
                }
            }

            return $"{prefix}{maxSeq + 1:D5}";
        }

        public async Task SaveChangesAsync()
        {
            await _context.SaveChangesAsync();
        }
    }
}
