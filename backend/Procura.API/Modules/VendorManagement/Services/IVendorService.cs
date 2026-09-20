using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Procura.API.Modules.VendorManagement.DTOs;

namespace Procura.API.Modules.VendorManagement.Services
{
    public interface IVendorService
    {
        Task<VendorResponseDto> CreateAsync(CreateVendorDto dto);
        Task<VendorResponseDto?> GetByIdAsync(Guid id);
        Task<List<VendorResponseDto>> GetAllAsync(string? status, string? category);
        Task<VendorResponseDto> UpdateAsync(Guid id, UpdateVendorDto dto);
        Task<VendorResponseDto> DeactivateAsync(Guid id);
        Task<VendorResponseDto> ActivateAsync(Guid id);
    }
}