using System.Collections.Generic;

namespace Procura.API.AI.Agents.VendorManagement
{
    public class VendorIntentValidationResult
    {
        public bool IsValid => Errors.Count == 0 && MissingFields.Count == 0;
        public List<string> Errors { get; set; } = new();
        public List<string> MissingFields { get; set; } = new();
    }

    public class VendorManagementDeterministicValidator
    {
        public VendorIntentValidationResult Validate(ExtractedVendorIntent? data)
        {
            var result = new VendorIntentValidationResult();

            if (data == null)
            {
                result.Errors.Add("Extracted vendor intent cannot be null.");
                return result;
            }

            if (!data.HasSufficientInformation)
            {
                if (data.MissingInformationReasons.Count > 0)
                {
                    result.MissingFields.AddRange(data.MissingInformationReasons);
                }
                else
                {
                    result.MissingFields.Add("Vendor category could not be determined from the objective.");
                }
                return result;
            }

            if (string.IsNullOrWhiteSpace(data.Category))
            {
                result.MissingFields.Add("Vendor category is required.");
            }
            else if (data.Category.Length > 60)
            {
                result.Errors.Add("Vendor category must not exceed 60 characters.");
            }

            return result;
        }
    }
}