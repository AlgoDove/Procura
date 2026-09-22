using System;
using System.ComponentModel.DataAnnotations;
using Procura.API.Modules.VendorManagement.Enums;

namespace Procura.API.Modules.VendorManagement.DTOs
{
    public class CreateVendorDto
    {
        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string ContactPerson { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(30)]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Address { get; set; }

        [Required, StringLength(60)]
        public string Category { get; set; } = string.Empty;

        [Range(0, 5)]
        public decimal Rating { get; set; } = 0;
    }

    public class UpdateVendorDto
    {
        [Required, StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string ContactPerson { get; set; } = string.Empty;

        [Required, EmailAddress, StringLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required, StringLength(30)]
        public string PhoneNumber { get; set; } = string.Empty;

        public string? Address { get; set; }

        [Required, StringLength(60)]
        public string Category { get; set; } = string.Empty;

        [Range(0, 5)]
        public decimal Rating { get; set; } = 0;
    }

    public class VendorResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string Category { get; set; } = string.Empty;
        public decimal Rating { get; set; }
        public VendorStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}