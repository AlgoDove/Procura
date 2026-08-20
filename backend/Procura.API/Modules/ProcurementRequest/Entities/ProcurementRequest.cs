using System;
using System.Collections.Generic;
using Procura.API.Modules.ProcurementRequest.Enums;
using Procura.API.Shared.Entities;

namespace Procura.API.Modules.ProcurementRequest.Entities
{
    public class ProcurementRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string RequestNumber { get; set; } = string.Empty;
        public Guid RequesterId { get; set; }
        public User Requester { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Justification { get; set; } = string.Empty;
        public Priority Priority { get; set; }
        public DateTime RequiredByDate { get; set; }
        public decimal EstimatedTotal { get; set; }
        public RequestStatus Status { get; set; } = RequestStatus.DRAFT;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<ProcurementRequestItem> Items { get; set; } = new List<ProcurementRequestItem>();
    }
}
