using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Procura.API.Modules.ProcurementRequest.Entities;

namespace Procura.API.Modules.ProcurementRequest.Repositories
{
    public interface IProcurementRequestRepository
    {
        Task<Entities.ProcurementRequest> GetByIdAsync(Guid id);
        Task<IEnumerable<Entities.ProcurementRequest>> GetAllByUserIdAsync(Guid userId);
        Task<IEnumerable<Entities.ProcurementRequest>> GetAllAsync();
        Task AddAsync(Entities.ProcurementRequest request);
        void Update(Entities.ProcurementRequest request);
        void Remove(Entities.ProcurementRequest request);
        Task<bool> ExistsAsync(Guid id);
        Task<string> GenerateUniqueRequestNumberAsync();
        Task SaveChangesAsync();
    }
}
