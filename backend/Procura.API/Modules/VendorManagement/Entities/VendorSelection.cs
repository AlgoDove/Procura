using System;

namespace Procura.API.Modules.VendorManagement.Entities
{
    public class VendorSelection
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProcurementRequestId { get; set; }
        public Guid VendorId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}