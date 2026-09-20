using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Procura.API.Modules.VendorManagement.DTOs;
using Procura.API.Modules.VendorManagement.Entities;
using Procura.API.Modules.VendorManagement.Enums;
using Procura.API.Modules.VendorManagement.Repositories;

namespace Procura.API.Modules.VendorManagement.Services
{
    public class VendorService : IVendorService
    {
        private readonly IVendorRepository _repository;

        public VendorService(IVendorRepository repository)
        {
            _repository = repository;
        }

        public async Task<VendorResponseDto> CreateAsync(CreateVendorDto dto)
        {
            ValidateRating(dto.Rating);

            if (await _repository.EmailExistsAsync(dto.Email))
                throw new InvalidOperationException($"A vendor with email '{dto.Email}' already exists.");

            var vendor = new Vendor
            {
                Name = dto.Name,
                ContactPerson = dto.ContactPerson,
                Email = dto.Email,
                PhoneNumber = dto.PhoneNumber,
                Address = dto.Address,
                Category = dto.Category,
                Rating = dto.Rating,
                Status = VendorStatus.ACTIVE
            };

            var created = await _repository.AddAsync(vendor);
            return ToDto(created);
        }

        public async Task<VendorResponseDto?> GetByIdAsync(Guid id)
        {
            var vendor = await _repository.GetByIdAsync(id);
            return vendor == null ? null : ToDto(vendor);
        }

        public async Task<List<VendorResponseDto>> GetAllAsync(string? status, string? category)
        {
            var vendors = await _repository.GetAllAsync(status, category);
            return vendors.Select(ToDto).ToList();
        }

        public async Task<VendorResponseDto> UpdateAsync(Guid id, UpdateVendorDto dto)
        {
            ValidateRating(dto.Rating);

            var vendor = await _repository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Vendor '{id}' not found.");

            vendor.Name = dto.Name;
            vendor.ContactPerson = dto.ContactPerson;
            vendor.Email = dto.Email;
            vendor.PhoneNumber = dto.PhoneNumber;
            vendor.Address = dto.Address;
            vendor.Category = dto.Category;
            vendor.Rating = dto.Rating;

            var updated = await _repository.UpdateAsync(vendor);
            return ToDto(updated);
        }

        public async Task<VendorResponseDto> DeactivateAsync(Guid id)
        {
            var vendor = await _repository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Vendor '{id}' not found.");

            if (vendor.Status == VendorStatus.INACTIVE)
                throw new InvalidOperationException("Vendor is already inactive.");

            vendor.Status = VendorStatus.INACTIVE;
            var updated = await _repository.UpdateAsync(vendor);
            return ToDto(updated);
        }

        public async Task<VendorResponseDto> ActivateAsync(Guid id)
        {
            var vendor = await _repository.GetByIdAsync(id)
                ?? throw new KeyNotFoundException($"Vendor '{id}' not found.");

            if (vendor.Status == VendorStatus.ACTIVE)
                throw new InvalidOperationException("Vendor is already active.");

            vendor.Status = VendorStatus.ACTIVE;
            var updated = await _repository.UpdateAsync(vendor);
            return ToDto(updated);
        }

        private static void ValidateRating(decimal rating)
        {
            if (rating < 0 || rating > 5)
                throw new ArgumentException("Rating must be between 0 and 5.");
        }

        private static VendorResponseDto ToDto(Vendor vendor)
        {
            return new VendorResponseDto
            {
                Id = vendor.Id,
                Name = vendor.Name,
                ContactPerson = vendor.ContactPerson,
                Email = vendor.Email,
                PhoneNumber = vendor.PhoneNumber,
                Address = vendor.Address,
                Category = vendor.Category,
                Rating = vendor.Rating,
                Status = vendor.Status,
                CreatedAt = vendor.CreatedAt,
                UpdatedAt = vendor.UpdatedAt
            };
        }
    }
}