using System;
using System.Collections.Generic;
using System.Linq;

namespace Procura.API.AI.Agents.VendorEvaluation
{
    public class VendorEvaluationDeterministicValidator
    {
        public VendorEvaluationValidationResult Validate(ExtractedVendorEvaluationInput? input)
        {
            var result = new VendorEvaluationValidationResult();

            if (input == null)
            {
                result.MissingFields.Add("Evaluation input payload is required.");
                return result;
            }

            // 1. Required ProcurementRequestId check
            if (!input.ProcurementRequestId.HasValue || input.ProcurementRequestId.Value == Guid.Empty)
            {
                result.MissingFields.Add("ProcurementRequestId");
            }

            // 2. Optional EstimatedBudget range check
            if (input.EstimatedBudget.HasValue && input.EstimatedBudget.Value < 0)
            {
                result.Errors.Add("EstimatedBudget must be non-negative.");
            }

            // 3. Optional RequiredDeliveryDays range check
            if (input.RequiredDeliveryDays.HasValue && input.RequiredDeliveryDays.Value < 0)
            {
                result.Errors.Add("RequiredDeliveryDays must be non-negative.");
            }

            // 4. Candidate Vendors metrics check (if explicitly provided)
            if (input.CandidateVendors != null && input.CandidateVendors.Any())
            {
                for (int i = 0; i < input.CandidateVendors.Count; i++)
                {
                    var v = input.CandidateVendors[i];
                    if (v.VendorId == Guid.Empty)
                    {
                        result.Errors.Add($"CandidateVendor[{i}].VendorId must be a valid non-empty GUID.");
                    }
                    if (v.QuotedPrice < 0)
                    {
                        result.Errors.Add($"CandidateVendor[{i}].QuotedPrice must be non-negative.");
                    }
                    if (v.EstimatedDeliveryDays < 0)
                    {
                        result.Errors.Add($"CandidateVendor[{i}].EstimatedDeliveryDays must be non-negative.");
                    }
                    if (v.ReliabilityRating < 0 || v.ReliabilityRating > 100)
                    {
                        result.Errors.Add($"CandidateVendor[{i}].ReliabilityRating must be between 0 and 100.");
                    }
                }
            }

            // 5. Custom Weights check (if explicitly provided)
            if (input.CustomWeights != null && input.CustomWeights.Any())
            {
                foreach (var w in input.CustomWeights)
                {
                    if (w.Weight < 0)
                    {
                        result.Errors.Add($"Criterion weight for '{w.Criterion}' must be non-negative.");
                    }
                }

                if (input.CustomWeights.Sum(w => w.Weight) <= 0)
                {
                    result.Errors.Add("Total sum of custom criteria weights must be greater than zero.");
                }
            }

            return result;
        }
    }
}
