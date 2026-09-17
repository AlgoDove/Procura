using System;

namespace Procura.API.AI.Agents.ProcurementRequest
{
    public class ProcurementRequestDeterministicValidator
    {
        public ProcurementValidationResult Validate(ExtractedProcurementData? data)
        {
            var result = new ProcurementValidationResult();

            if (data == null)
            {
                result.Errors.Add("Extracted procurement data cannot be null.");
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
                    result.MissingFields.Add("Essential procurement details are missing.");
                }
                return result;
            }

            // Validate Title
            if (string.IsNullOrWhiteSpace(data.Title))
            {
                result.Errors.Add("Title is required.");
            }
            else if (data.Title.Length > 150)
            {
                result.Errors.Add("Title must not exceed 150 characters.");
            }

            // Validate Description
            if (string.IsNullOrWhiteSpace(data.Description))
            {
                result.Errors.Add("Description is required.");
            }

            // Validate Justification
            if (string.IsNullOrWhiteSpace(data.Justification))
            {
                result.Errors.Add("Justification is required.");
            }

            // Validate RequiredByDate
            if (!data.RequiredByDate.HasValue)
            {
                result.MissingFields.Add("RequiredByDate is required.");
            }
            else if (data.RequiredByDate.Value.Date < DateTime.UtcNow.Date)
            {
                result.Errors.Add("RequiredByDate cannot be in the past.");
            }

            // Validate Items
            if (data.Items == null || data.Items.Count == 0)
            {
                result.MissingFields.Add("At least one procurement item is required.");
                return result;
            }

            for (int i = 0; i < data.Items.Count; i++)
            {
                var item = data.Items[i];
                var itemPrefix = $"Item {i + 1}";

                if (string.IsNullOrWhiteSpace(item.ItemName))
                {
                    result.Errors.Add($"{itemPrefix}: ItemName is required.");
                }
                else if (item.ItemName.Length > 150)
                {
                    result.Errors.Add($"{itemPrefix}: ItemName must not exceed 150 characters.");
                }

                if (string.IsNullOrWhiteSpace(item.Description))
                {
                    result.Errors.Add($"{itemPrefix}: Description is required.");
                }

                if (item.Quantity < 1)
                {
                    result.Errors.Add($"{itemPrefix}: Quantity must be at least 1 (received {item.Quantity}).");
                }

                if (string.IsNullOrWhiteSpace(item.Unit))
                {
                    result.Errors.Add($"{itemPrefix}: Unit is required.");
                }
                else if (item.Unit.Length > 30)
                {
                    result.Errors.Add($"{itemPrefix}: Unit must not exceed 30 characters.");
                }

                if (item.EstimatedUnitPrice < 0)
                {
                    result.Errors.Add($"{itemPrefix}: Price cannot be negative (received {item.EstimatedUnitPrice}).");
                }
            }

            return result;
        }
    }
}
