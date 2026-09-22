using System;
using Procura.API.Modules.VendorManagement.Enums;

namespace Procura.API.Modules.VendorManagement.Entities
{
    public class Vendor
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string ContactPerson { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? Address { get; set; }
        public string Category { get; set; } = string.Empty;
        public decimal Rating { get; set; } = 0;
        public VendorStatus Status { get; set; } = VendorStatus.ACTIVE;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}