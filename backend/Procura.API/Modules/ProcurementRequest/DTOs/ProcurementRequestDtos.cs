using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Procura.API.Modules.ProcurementRequest.Enums;

namespace Procura.API.Modules.ProcurementRequest.DTOs
{
    public class CreateProcurementRequestDto
    {
        [Required, MaxLength(150)]
        public string Title { get; set; } = string.Empty;
        [Required]
        public string Description { get; set; } = string.Empty;
        [Required]
        public string Justification { get; set; } = string.Empty;
        [Required]
        public Priority Priority { get; set; }
        [Required]
        public DateTime RequiredByDate { get; set; }

        [Required, MinLength(1, ErrorMessage = "At least one item is required.")]
        public List<CreateProcurementRequestItemDto> Items { get; set; } = new();
    }

    public class CreateProcurementRequestItemDto
    {
        [Required, MaxLength(150)]
        public string ItemName { get; set; } = string.Empty;
        [Required]
        public string Description { get; set; } = string.Empty;
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
        public int Quantity { get; set; }
        [Required, MaxLength(30)]
        public string Unit { get; set; } = string.Empty;
        [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative.")]
        public decimal EstimatedUnitPrice { get; set; }
    }

    public class UpdateProcurementRequestDto
    {
        [Required, MaxLength(150)]
        public string Title { get; set; } = string.Empty;
        [Required]
        public string Description { get; set; } = string.Empty;
        [Required]
        public string Justification { get; set; } = string.Empty;
        [Required]
        public Priority Priority { get; set; }
        [Required]
        public DateTime RequiredByDate { get; set; }

        [Required, MinLength(1, ErrorMessage = "At least one item is required.")]
        public List<CreateProcurementRequestItemDto> Items { get; set; } = new();
    }

    public class ProcurementRequestResponseDto
    {
        public Guid Id { get; set; }
        public string RequestNumber { get; set; } = string.Empty;
        public Guid RequesterId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Justification { get; set; } = string.Empty;
        public string Priority { get; set; } = string.Empty;
        public DateTime RequiredByDate { get; set; }
        public decimal EstimatedTotal { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public List<ProcurementRequestItemResponseDto> Items { get; set; } = new();
    }

    public class ProcurementRequestItemResponseDto
    {
        public Guid Id { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string Unit { get; set; } = string.Empty;
        public decimal EstimatedUnitPrice { get; set; }
    }
}
