namespace Procura.API.AI.Core
{
    public enum WorkflowStage
    {
        PROCUREMENT_REQUEST,
        VENDOR_SELECTION,
        VENDOR_EVALUATION,
        APPROVAL_WORKFLOW,
        COMPLETED
    }

    public enum WorkflowStatus
    {
        NOT_STARTED,
        IN_PROGRESS,
        NEEDS_USER_INPUT,
        STAGE_COMPLETED,
        WAITING_FOR_HUMAN_APPROVAL,
        APPROVED,
        REJECTED,
        REVISION_REQUESTED,
        COMPLETED,
        FAILED
    }

    public enum AgentExecutionStatus
    {
        COMPLETED,
        NEEDS_USER_INPUT,
        FAILED
    }

    public enum StepStatus
    {
        NOT_STARTED,
        PENDING,
        IN_PROGRESS,
        COMPLETED,
        FAILED,
        SKIPPED
    }
}
