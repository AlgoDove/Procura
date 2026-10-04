namespace Procura.API.Modules.ApprovalWorkflow.Enums
{
    /// <summary>
    /// Event types that trigger internal notifications across the approval workflow lifecycle.
    /// </summary>
    public enum NotificationType
    {
        REQUEST_SUBMITTED,
        APPROVAL_REQUIRED,
        APPROVED,
        REJECTED,
        REVISION_REQUESTED,
        WORKFLOW_COMPLETED
    }
}
