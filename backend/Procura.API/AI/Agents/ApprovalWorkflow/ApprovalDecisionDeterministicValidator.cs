using System;
using System.Collections.Generic;

namespace Procura.API.AI.Agents.ApprovalWorkflow
{
    /// <summary>
    /// Deterministic validator for procurement decision support.
    /// Enforces mathematical and policy invariants to prevent AI hallucinations,
    /// budget overruns, and compliance violations.
    /// </summary>
    public class ApprovalDecisionDeterministicValidator
    {
        public (bool IsValid, List<string> Errors, List<string> Warnings) ValidateReadiness(ApprovalReadinessData data)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            if (data == null)
            {
                errors.Add("Procurement readiness data cannot be null.");
                return (false, errors, warnings);
            }

            if (data.ProcurementRequestId == Guid.Empty)
            {
                errors.Add("Procurement Request ID is required.");
            }

            if (data.ItemCount <= 0)
            {
                errors.Add("Procurement request must contain at least one line item.");
            }

            if (!data.HasEvaluations || data.TotalCandidatesEvaluated <= 0)
            {
                errors.Add("Vendor evaluation has not been conducted. At least one candidate must be evaluated before approval support can execute.");
            }

            if (!data.TopVendorId.HasValue || data.TopVendorId == Guid.Empty)
            {
                errors.Add("No top-ranked candidate vendor was identified in the vendor evaluation stage.");
            }

            // Budget guardrails
            if (data.TopQuotedPrice.HasValue && data.EstimatedTotal > 0)
            {
                if (data.TopQuotedPrice.Value > data.EstimatedTotal)
                {
                    var excess = data.TopQuotedPrice.Value - data.EstimatedTotal;
                    var percent = Math.Round((excess / data.EstimatedTotal) * 100, 1);
                    warnings.Add($"Budget Alert: Recommended quote (${data.TopQuotedPrice.Value:N2}) exceeds estimated budget (${data.EstimatedTotal:N2}) by ${excess:N2} (+{percent}%). Manager justification required.");
                }
            }

            // Compliance check
            if (!data.IsComplianceApproved)
            {
                errors.Add("Compliance Guardrail: Top candidate vendor has not passed compliance verification.");
            }

            return (errors.Count == 0, errors, warnings);
        }

        public (bool IsValid, List<string> Errors) ValidateExtractedAdvice(
            ExtractedDecisionSupportInput advice,
            ApprovalReadinessData data)
        {
            var errors = new List<string>();

            if (advice == null)
            {
                errors.Add("Extracted decision advice is null.");
                return (false, errors);
            }

            var action = advice.RecommendedAction?.ToUpperInvariant().Trim();
            if (action != "APPROVE" && action != "REJECT" && action != "REQUEST_REVISION")
            {
                errors.Add($"Invalid recommended action '{advice.RecommendedAction}'. Must be APPROVE, REJECT, or REQUEST_REVISION.");
            }

            // Critical enterprise invariant: AI can NEVER recommend APPROVE for a non-compliant vendor
            if (!data.IsComplianceApproved && action == "APPROVE")
            {
                errors.Add("Safety Violation: AI cannot recommend APPROVE when candidate is not compliance approved.");
            }

            if (string.IsNullOrWhiteSpace(advice.ExecutiveSummary))
            {
                errors.Add("Executive summary cannot be empty.");
            }

            if (advice.ConfidenceScore < 0 || advice.ConfidenceScore > 100)
            {
                errors.Add($"Confidence score {advice.ConfidenceScore} must be between 0 and 100.");
            }

            return (errors.Count == 0, errors);
        }
    }
}
