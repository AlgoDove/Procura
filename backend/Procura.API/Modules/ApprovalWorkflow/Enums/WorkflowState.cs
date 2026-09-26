namespace Procura.API.Modules.ApprovalWorkflow.Enums
{
    /// <summary>
    /// Represents the formal stages in the procurement approval workflow life cycle.
    /// In accordance with the documented workflow specification:
    /// DRAFT -> SUBMITTED -> UNDER_VENDOR_EVALUATION -> AI_RECOMMENDATION_GENERATED
    /// -> WAITING_MANAGER_APPROVAL -> APPROVED / REJECTED / REVISION_REQUESTED -> COMPLETED
    /// </summary>
    public enum WorkflowState
    {
        DRAFT,
        SUBMITTED,
        UNDER_VENDOR_EVALUATION,
        AI_RECOMMENDATION_GENERATED,
        WAITING_MANAGER_APPROVAL,
        APPROVED,
        REJECTED,
        REVISION_REQUESTED,
        COMPLETED
    }
}
