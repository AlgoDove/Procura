using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Procura.API.Modules.VendorManagement.Entities;
using Procura.API.Modules.VendorManagement.Enums;
using Procura.API.Shared.Data;

namespace Procura.API.Modules.VendorManagement.Repositories
{
    public class VendorRepository : IVendorRepository
    {
        private readonly ApplicationDbContext _context;

        public VendorRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Vendor> AddAsync(Vendor vendor)
        {
            _context.Vendors.Add(vendor);
            await _context.SaveChangesAsync();
            return vendor;
        }

        public async Task<Vendor?> GetByIdAsync(Guid id)
        {
            return await _context.Vendors.FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<List<Vendor>> GetAllAsync(string? status, string? category)
        {
            var query = _context.Vendors.AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<VendorStatus>(status, true, out var statusEnum))
            {
                query = query.Where(v => v.Status == statusEnum);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(v => v.Category == category);
            }

            return await query.OrderByDescending(v => v.CreatedAt).ToListAsync();
        }

        public async Task<Vendor> UpdateAsync(Vendor vendor)
        {
            vendor.UpdatedAt = DateTime.UtcNow;
            _context.Vendors.Update(vendor);
            await _context.SaveChangesAsync();
            return vendor;
        }

        public async Task<bool> EmailExistsAsync(string email)
        {
            return await _context.Vendors.AnyAsync(v => v.Email == email);
        }
    }
}