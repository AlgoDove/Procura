namespace Procura.API.Modules.ApprovalWorkflow.Enums
{
    /// <summary>
    /// Decisions that an authorized Manager can make when reviewing a workflow in WAITING_MANAGER_APPROVAL.
    /// </summary>
    public enum ApprovalDecisionType
    {
        APPROVED,
        REJECTED,
        REVISION_REQUESTED
    }
}
