using System;
using System.Collections.Generic;

namespace Procura.API.AI.Agents.VendorManagement
{
    public class ExtractedVendorIntent
    {
        public string Category { get; set; } = string.Empty;
        public string? MentionedRequestNumber { get; set; }
        public bool HasSufficientInformation { get; set; } = true;
        public List<string> MissingInformationReasons { get; set; } = new();
        public string? ClarificationPrompt { get; set; }
    }

    public class VendorCandidate
    {
        public Guid VendorId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Rating { get; set; }
    }

    public class VendorSelectionOutput
    {
        public List<VendorCandidate> Candidates { get; set; } = new();
        public Guid? SelectedVendorId { get; set; }
        public string? SelectedVendorName { get; set; }
    }
}