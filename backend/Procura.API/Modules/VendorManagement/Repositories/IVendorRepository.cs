using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Procura.API.Modules.VendorManagement.Entities;

namespace Procura.API.Modules.VendorManagement.Repositories
{
    public interface IVendorRepository
    {
        Task<Vendor> AddAsync(Vendor vendor);
        Task<Vendor?> GetByIdAsync(Guid id);
        Task<List<Vendor>> GetAllAsync(string? status, string? category);
        Task<Vendor> UpdateAsync(Vendor vendor);
        Task<bool> EmailExistsAsync(string email);
    }
}