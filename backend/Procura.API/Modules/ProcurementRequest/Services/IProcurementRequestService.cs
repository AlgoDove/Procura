using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Procura.API.Modules.ProcurementRequest.DTOs;
using Procura.API.Modules.ProcurementRequest.Enums;

namespace Procura.API.Modules.ProcurementRequest.Services
{
    public interface IProcurementRequestService
    {
        Task<ProcurementRequestResponseDto> CreateAsync(CreateProcurementRequestDto dto, Guid requesterId);
        Task<ProcurementRequestResponseDto?> GetByIdAsync(Guid id);
        Task<IEnumerable<ProcurementRequestResponseDto>> GetRequestsAsync(Guid requesterId, string role);
        Task<ProcurementRequestResponseDto> UpdateAsync(Guid id, UpdateProcurementRequestDto dto, Guid requesterId);
        Task DeleteAsync(Guid id, Guid requesterId, string? role = null);
        Task SubmitAsync(Guid id, Guid requesterId);
        Task UpdateStatusAsync(Guid id, RequestStatus newStatus, string role, Guid? callerId = null);
    }
}
