using System;

namespace Procura.API.Modules.ProcurementRequest.Entities
{
    public class ProcurementRequestItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProcurementRequestId { get; set; }
        public ProcurementRequest ProcurementRequest { get; set; } = null!;
        public string ItemName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal EstimatedUnitPrice { get; set; }
    }
}
