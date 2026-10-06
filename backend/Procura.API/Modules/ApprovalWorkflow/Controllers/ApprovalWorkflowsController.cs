using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Procura.API.Modules.ApprovalWorkflow.DTOs;
using Procura.API.Modules.ApprovalWorkflow.Enums;
using Procura.API.Modules.ApprovalWorkflow.Services;

namespace Procura.API.Modules.ApprovalWorkflow.Controllers
{
    /// <summary>
    /// REST controller providing endpoints for managing procurement approval workflows,
    /// tracking lifecycle states, and executing Manager approval decisions.
    /// </summary>
    [ApiController]
    [Route("api/approval-workflows")]
    [Authorize]
    [Produces("application/json")]
    public class ApprovalWorkflowsController : ControllerBase
    {
        private readonly IApprovalWorkflowService _workflowService;
        private readonly INotificationService _notificationService;

        public ApprovalWorkflowsController(
            IApprovalWorkflowService workflowService,
            INotificationService notificationService)
        {
            _workflowService = workflowService ?? throw new ArgumentNullException(nameof(workflowService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        private Guid GetUserId()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                throw new UnauthorizedAccessException("Invalid or missing user identity in token claims.");
            }

            return userId;
        }

        private string GetUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value
                   ?? User.FindFirst("role")?.Value
                   ?? "EMPLOYEE";
        }

        /// <summary>
        /// Retrieves all approval workflows accessible to the authenticated user.
        /// Employees only receive workflows for their own procurement requests.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<ApprovalWorkflowResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetAll([FromQuery] WorkflowState? status)
        {
            var workflows = await _workflowService.GetAllWorkflowsAsync(GetUserId(), GetUserRole(), status);
            return Ok(workflows);
        }

        /// <summary>
        /// Retrieves all approval workflows currently awaiting managerial review (WAITING_MANAGER_APPROVAL).
        /// Restricted to Managers and Administrators.
        /// </summary>
        [HttpGet("pending")]
        [Authorize(Roles = "MANAGER,ADMIN")]
        [ProducesResponseType(typeof(IEnumerable<ApprovalWorkflowResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetPending()
        {
            try
            {
                var workflows = await _workflowService.GetPendingWorkflowsAsync(GetUserId(), GetUserRole());
                return Ok(workflows);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            }
        }

        /// <summary>
        /// Retrieves a specific approval workflow by its unique ID.
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(ApprovalWorkflowResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById([FromRoute] Guid id)
        {
            try
            {
                var workflow = await _workflowService.GetWorkflowByIdAsync(id, GetUserId(), GetUserRole());
                if (workflow == null) return NotFound(new { message = $"Approval workflow with ID {id} not found." });

                return Ok(workflow);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            }
        }

        /// <summary>
        /// Retrieves the approval workflow linked to a specific Procurement Request ID.
        /// </summary>
        [HttpGet("procurement-request/{procurementRequestId:guid}")]
        [ProducesResponseType(typeof(ApprovalWorkflowResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetByProcurementRequestId([FromRoute] Guid procurementRequestId)
        {
            try
            {
                var workflow = await _workflowService.GetWorkflowByProcurementRequestIdAsync(procurementRequestId, GetUserId(), GetUserRole());
                if (workflow == null) return NotFound(new { message = $"No approval workflow found for Procurement Request {procurementRequestId}." });

                return Ok(workflow);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            }
        }

        /// <summary>
        /// Initializes a new approval workflow for a procurement request.
        /// </summary>
        [HttpPost("initialize")]
        [Authorize(Roles = "EMPLOYEE,PROCUREMENT_OFFICER,MANAGER,ADMIN")]
        [ProducesResponseType(typeof(ApprovalWorkflowResponseDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Initialize([FromBody] CreateApprovalWorkflowDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var workflow = await _workflowService.InitializeWorkflowAsync(dto.ProcurementRequestId, GetUserId(), GetUserRole());
                return CreatedAtAction(nameof(GetById), new { id = workflow.Id }, workflow);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Advances the approval workflow through intermediate stages (e.g., to UNDER_VENDOR_EVALUATION or WAITING_MANAGER_APPROVAL).
        /// </summary>
        [HttpPost("{id:guid}/transition")]
        [Authorize(Roles = "PROCUREMENT_OFFICER,MANAGER,ADMIN")]
        [ProducesResponseType(typeof(ApprovalWorkflowResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Transition([FromRoute] Guid id, [FromBody] TransitionWorkflowRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var workflow = await _workflowService.TransitionWorkflowAsync(id, dto.TargetStatus, GetUserId(), GetUserRole());
                return Ok(workflow);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Approves a procurement request in WAITING_MANAGER_APPROVAL.
        /// Strictly restricted to authenticated Managers.
        /// </summary>
        [HttpPost("{id:guid}/approve")]
        [Authorize(Roles = "MANAGER")]
        [ProducesResponseType(typeof(ApprovalWorkflowResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Approve([FromRoute] Guid id, [FromBody] ApprovalDecisionRequestDto? dto)
        {
            try
            {
                var workflow = await _workflowService.ApproveAsync(id, GetUserId(), GetUserRole(), dto?.Comments);
                return Ok(workflow);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Rejects a procurement request in WAITING_MANAGER_APPROVAL. Requires justification comments.
        /// Strictly restricted to authenticated Managers.
        /// </summary>
        [HttpPost("{id:guid}/reject")]
        [Authorize(Roles = "MANAGER")]
        [ProducesResponseType(typeof(ApprovalWorkflowResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Reject([FromRoute] Guid id, [FromBody] ApprovalDecisionRequestDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Comments))
            {
                return BadRequest(new { message = "Rejection comments/justification are required." });
            }

            try
            {
                var workflow = await _workflowService.RejectAsync(id, GetUserId(), GetUserRole(), dto.Comments);
                return Ok(workflow);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Requests revisions on a procurement request in WAITING_MANAGER_APPROVAL. Requires revision comments.
        /// Strictly restricted to authenticated Managers.
        /// </summary>
        [HttpPost("{id:guid}/request-revision")]
        [Authorize(Roles = "MANAGER")]
        [ProducesResponseType(typeof(ApprovalWorkflowResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RequestRevision([FromRoute] Guid id, [FromBody] ApprovalDecisionRequestDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Comments))
            {
                return BadRequest(new { message = "Revision comments detailing required changes are required." });
            }

            try
            {
                var workflow = await _workflowService.RequestRevisionAsync(id, GetUserId(), GetUserRole(), dto.Comments);
                return Ok(workflow);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Retrieves the comprehensive audit trail for a workflow, including managerial decisions,
        /// agent executions, and notification events.
        /// </summary>
        [HttpGet("{id:guid}/audit-trail")]
        [ProducesResponseType(typeof(WorkflowAuditTrailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAuditTrail([FromRoute] Guid id)
        {
            try
            {
                var auditTrail = await _workflowService.GetAuditTrailAsync(id, GetUserId(), GetUserRole());
                if (auditTrail == null) return NotFound(new { message = $"Approval workflow with ID {id} not found." });

                return Ok(auditTrail);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            }
        }

        /// <summary>
        /// Retrieves notifications for the authenticated user, optionally filtered by unread status.
        /// </summary>
        [HttpGet("notifications")]
        [ProducesResponseType(typeof(IEnumerable<NotificationResponseDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetUserNotifications([FromQuery] bool? unreadOnly)
        {
            var notifications = await _notificationService.GetUserNotificationsAsync(GetUserId(), unreadOnly);
            return Ok(notifications);
        }

        /// <summary>
        /// Gets the count of unread notifications for the authenticated user.
        /// </summary>
        [HttpGet("notifications/unread-count")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> GetUnreadNotificationCount()
        {
            var count = await _notificationService.GetUnreadCountAsync(GetUserId());
            return Ok(new { unreadCount = count });
        }

        /// <summary>
        /// Marks a specific notification as read.
        /// </summary>
        [HttpPatch("notifications/{id:guid}/read")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> MarkNotificationAsRead([FromRoute] Guid id)
        {
            await _notificationService.MarkAsReadAsync(id, GetUserId());
            return NoContent();
        }

        /// <summary>
        /// Marks all notifications for the authenticated user as read.
        /// </summary>
        [HttpPost("notifications/mark-all-read")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<IActionResult> MarkAllNotificationsAsRead()
        {
            await _notificationService.MarkAllAsReadAsync(GetUserId());
            return NoContent();
        }
    }
}
