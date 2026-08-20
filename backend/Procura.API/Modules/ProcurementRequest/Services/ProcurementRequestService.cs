using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Procura.API.Modules.ProcurementRequest.DTOs;
using Procura.API.Modules.ProcurementRequest.Entities;
using Procura.API.Modules.ProcurementRequest.Enums;
using Procura.API.Modules.ProcurementRequest.Repositories;

namespace Procura.API.Modules.ProcurementRequest.Services
{
    public class ProcurementRequestService : IProcurementRequestService
    {
        private readonly IProcurementRequestRepository _repository;

        public ProcurementRequestService(IProcurementRequestRepository repository)
        {
            _repository = repository;
        }

        public async Task<ProcurementRequestResponseDto> CreateAsync(CreateProcurementRequestDto dto, Guid requesterId)
        {
            if (dto.Items == null || !dto.Items.Any())
                throw new ArgumentException("A procurement request must contain at least one item before submission.");

            var request = new Entities.ProcurementRequest
            {
                RequesterId = requesterId,
                RequestNumber = await _repository.GenerateUniqueRequestNumberAsync(),
                Title = dto.Title,
                Description = dto.Description,
                Justification = dto.Justification,
                Priority = dto.Priority,
                RequiredByDate = dto.RequiredByDate,
                Status = RequestStatus.DRAFT,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                EstimatedTotal = dto.Items.Sum(i => i.Quantity * i.EstimatedUnitPrice),
                Items = dto.Items.Select(i => new ProcurementRequestItem
                {
                    ItemName = i.ItemName,
                    Description = i.Description,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    EstimatedUnitPrice = i.EstimatedUnitPrice
                }).ToList()
            };

            await _repository.AddAsync(request);
            await _repository.SaveChangesAsync();

            return MapToDto(request);
        }

        public async Task<ProcurementRequestResponseDto> GetByIdAsync(Guid id)
        {
            var request = await _repository.GetByIdAsync(id);
            if (request == null) return null;
            return MapToDto(request);
        }

        public async Task<IEnumerable<ProcurementRequestResponseDto>> GetRequestsAsync(Guid requesterId, string role)
        {
            IEnumerable<Entities.ProcurementRequest> requests;
            if (role == "EMPLOYEE")
            {
                requests = await _repository.GetAllByUserIdAsync(requesterId);
            }
            else
            {
                requests = await _repository.GetAllAsync();
            }

            return requests.Select(MapToDto);
        }

        public async Task<ProcurementRequestResponseDto> UpdateAsync(Guid id, UpdateProcurementRequestDto dto, Guid requesterId)
        {
            var request = await _repository.GetByIdAsync(id);
            if (request == null) throw new KeyNotFoundException("Request not found.");
            if (request.RequesterId != requesterId) throw new UnauthorizedAccessException("Not authorized to update this request.");
            if (request.Status != RequestStatus.DRAFT) throw new InvalidOperationException("Only DRAFT requests can be modified.");
            if (dto.Items == null || !dto.Items.Any()) throw new ArgumentException("A procurement request must contain at least one item.");

            request.Title = dto.Title;
            request.Description = dto.Description;
            request.Justification = dto.Justification;
            request.Priority = dto.Priority;
            request.RequiredByDate = dto.RequiredByDate;
            request.UpdatedAt = DateTime.UtcNow;

            request.Items.Clear();
            foreach (var i in dto.Items)
            {
                request.Items.Add(new ProcurementRequestItem
                {
                    Id = Guid.Empty,
                    ItemName = i.ItemName,
                    Description = i.Description,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    EstimatedUnitPrice = i.EstimatedUnitPrice
                });
            }
            
            request.EstimatedTotal = request.Items.Sum(i => i.Quantity * i.EstimatedUnitPrice);

            await _repository.SaveChangesAsync();

            return MapToDto(request);
        }

        public async Task DeleteAsync(Guid id, Guid requesterId)
        {
            var request = await _repository.GetByIdAsync(id);
            if (request == null) throw new KeyNotFoundException("Request not found.");
            if (request.RequesterId != requesterId) throw new UnauthorizedAccessException("Not authorized to delete this request.");
            if (request.Status != RequestStatus.DRAFT) throw new InvalidOperationException("Only DRAFT requests can be deleted.");

            _repository.Remove(request);
            await _repository.SaveChangesAsync();
        }

        public async Task SubmitAsync(Guid id, Guid requesterId)
        {
            var request = await _repository.GetByIdAsync(id);
            if (request == null) throw new KeyNotFoundException("Request not found.");
            if (request.RequesterId != requesterId) throw new UnauthorizedAccessException("Not authorized to submit this request.");
            if (request.Status != RequestStatus.DRAFT) throw new InvalidOperationException("Only DRAFT requests can be submitted.");
            if (!request.Items.Any()) throw new InvalidOperationException("A procurement request must contain at least one item before submission.");

            request.Status = RequestStatus.SUBMITTED;
            request.UpdatedAt = DateTime.UtcNow;

            _repository.Update(request);
            await _repository.SaveChangesAsync();
        }

        public async Task UpdateStatusAsync(Guid id, RequestStatus newStatus, string role)
        {
            var request = await _repository.GetByIdAsync(id);
            if (request == null) throw new KeyNotFoundException("Request not found.");

            // Transition rules validation based on the spec
            bool isValidTransition = false;

            switch (request.Status)
            {
                case RequestStatus.DRAFT:
                    if (newStatus == RequestStatus.SUBMITTED) isValidTransition = true;
                    break;
                case RequestStatus.SUBMITTED:
                    if (newStatus == RequestStatus.UNDER_EVALUATION && (role == "PROCUREMENT_OFFICER" || role == "MANAGER" || role == "ADMIN")) isValidTransition = true;
                    break;
                case RequestStatus.UNDER_EVALUATION:
                    if (newStatus == RequestStatus.PENDING_APPROVAL && (role == "PROCUREMENT_OFFICER" || role == "MANAGER" || role == "ADMIN")) isValidTransition = true;
                    break;
                case RequestStatus.PENDING_APPROVAL:
                    if (newStatus == RequestStatus.APPROVED && (role == "MANAGER" || role == "ADMIN")) isValidTransition = true;
                    if (newStatus == RequestStatus.REJECTED && (role == "MANAGER" || role == "ADMIN")) isValidTransition = true;
                    if (newStatus == RequestStatus.REVISION_REQUESTED && (role == "MANAGER" || role == "ADMIN")) isValidTransition = true;
                    break;
                case RequestStatus.APPROVED:
                    if (newStatus == RequestStatus.COMPLETED && (role == "PROCUREMENT_OFFICER" || role == "ADMIN")) isValidTransition = true;
                    break;
                case RequestStatus.REVISION_REQUESTED:
                    if (newStatus == RequestStatus.DRAFT) isValidTransition = true; // Typically triggered by employee to fix it
                    break;
            }

            if (!isValidTransition) throw new InvalidOperationException($"Invalid state transition from {request.Status} to {newStatus} for role {role}.");

            request.Status = newStatus;
            request.UpdatedAt = DateTime.UtcNow;

            _repository.Update(request);
            await _repository.SaveChangesAsync();
        }

        private static ProcurementRequestResponseDto MapToDto(Entities.ProcurementRequest request)
        {
            return new ProcurementRequestResponseDto
            {
                Id = request.Id,
                RequestNumber = request.RequestNumber,
                RequesterId = request.RequesterId,
                Title = request.Title,
                Description = request.Description,
                Justification = request.Justification,
                Priority = request.Priority.ToString(),
                RequiredByDate = request.RequiredByDate,
                EstimatedTotal = request.EstimatedTotal,
                Status = request.Status.ToString(),
                CreatedAt = request.CreatedAt,
                UpdatedAt = request.UpdatedAt,
                Items = request.Items.Select(i => new ProcurementRequestItemResponseDto
                {
                    Id = i.Id,
                    ItemName = i.ItemName,
                    Description = i.Description,
                    Quantity = i.Quantity,
                    Unit = i.Unit,
                    EstimatedUnitPrice = i.EstimatedUnitPrice
                }).ToList()
            };
        }
    }
}
