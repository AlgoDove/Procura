using System;
using System.Collections.Generic;

namespace Procura.API.AI.Core
{
    public class WorkflowStep
    {
        public int StepNumber { get; set; }
        public WorkflowStage Stage { get; set; }
        public string AgentName { get; set; } = string.Empty;
        public string Objective { get; set; } = string.Empty;
        public StepStatus Status { get; set; } = StepStatus.NOT_STARTED;
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? OutcomeSummary { get; set; }
    }

    public class WorkflowPlan
    {
        public List<WorkflowStep> Steps { get; set; } = new();

        public static WorkflowPlan CreateDefaultPlan()
        {
            return new WorkflowPlan
            {
                Steps = new List<WorkflowStep>
                {
                    new WorkflowStep
                    {
                        StepNumber = 1,
                        Stage = WorkflowStage.PROCUREMENT_REQUEST,
                        AgentName = "ProcurementRequestAgent",
                        Objective = "Extract, validate, and persist initial procurement requirements as a DRAFT request",
                        Status = StepStatus.PENDING
                    },
                    new WorkflowStep
                    {
                        StepNumber = 2,
                        Stage = WorkflowStage.VENDOR_SELECTION,
                        AgentName = "VendorManagementAgent",
                        Objective = "Identify and select candidate vendors matching procurement request items",
                        Status = StepStatus.NOT_STARTED
                    },
                    new WorkflowStep
                    {
                        StepNumber = 3,
                        Stage = WorkflowStage.VENDOR_EVALUATION,
                        AgentName = "VendorEvaluationAgent",
                        Objective = "Evaluate vendor quotes and provide comparison and recommendation",
                        Status = StepStatus.NOT_STARTED
                    },
                    new WorkflowStep
                    {
                        StepNumber = 4,
                        Stage = WorkflowStage.APPROVAL_WORKFLOW,
                        AgentName = "ApprovalWorkflowAgent",
                        Objective = "Facilitate formal human review and authorization by Manager/Admin",
                        Status = StepStatus.NOT_STARTED
                    }
                }
            };
        }
    }
}
