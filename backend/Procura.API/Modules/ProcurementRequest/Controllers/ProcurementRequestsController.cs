using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procura.API.Modules.ProcurementRequest.DTOs;
using Procura.API.Modules.ProcurementRequest.Enums;
using Procura.API.Modules.ProcurementRequest.Services;
using Procura.API.AI.Orchestration;
using Procura.API.AI.DTOs;

namespace Procura.API.Modules.ProcurementRequest.Controllers
{
    [ApiController]
    [Route("api/procurement-requests")]
    [Authorize]
    public class ProcurementRequestsController : ControllerBase
    {
        private readonly IProcurementRequestService _service;
        private readonly IWorkflowOrchestrator _orchestrator;

        public ProcurementRequestsController(
            IProcurementRequestService service,
            IWorkflowOrchestrator orchestrator)
        {
            _service = service;
            _orchestrator = orchestrator;
        }

        private Guid GetUserId()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                               ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
                throw new UnauthorizedAccessException("Invalid token claims.");
            return userId;
        }

        private string GetUserRole()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "EMPLOYEE";
        }

        [HttpPost]
        [Authorize(Roles = "EMPLOYEE,PROCUREMENT_OFFICER,MANAGER,ADMIN")]
        public async Task<IActionResult> Create([FromBody] CreateProcurementRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var request = await _service.CreateAsync(dto, GetUserId());
                return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var requests = await _service.GetRequestsAsync(GetUserId(), GetUserRole());
            return Ok(requests);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var request = await _service.GetByIdAsync(id);
            if (request == null) return NotFound();
            
            // basic isolation: Employee can only view their own
            if (GetUserRole() == "EMPLOYEE" && request.RequesterId != GetUserId())
                return Problem(detail: "Not authorized to view this request.", statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");

            return Ok(request);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "EMPLOYEE")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProcurementRequestDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var request = await _service.UpdateAsync(id, dto, GetUserId());
                return Ok(request);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "EMPLOYEE,ADMIN")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                await _service.DeleteAsync(id, GetUserId(), GetUserRole());
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPost("{id}/submit")]
        [Authorize(Roles = "EMPLOYEE")]
        public async Task<IActionResult> Submit(Guid id)
        {
            try
            {
                await _service.SubmitAsync(id, GetUserId());
                return Ok(new { Message = "Request submitted successfully." });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPost("{id}/status")]
        [Authorize(Roles = "PROCUREMENT_OFFICER,MANAGER,ADMIN,EMPLOYEE")]
        public async Task<IActionResult> ChangeStatus(Guid id, [FromQuery] RequestStatus newStatus)
        {
            try
            {
                await _service.UpdateStatusAsync(id, newStatus, GetUserRole());
                return Ok(new { Message = "Status updated successfully." });
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPost("ai/process")]
        [Authorize(Roles = "EMPLOYEE,PROCUREMENT_OFFICER,MANAGER,ADMIN")]
        public async Task<IActionResult> ProcessAiWorkflow([FromBody] ProcessProcurementAiRequestDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var context = await _orchestrator.ProcessWorkflowAsync(
                dto.Objective,
                GetUserId(),
                GetUserRole(),
                dto.ExistingRequestId,
                dto.WorkflowId);

            var response = new WorkflowProcessResponseDto
            {
                WorkflowId = context.WorkflowId,
                Status = context.Status.ToString(),
                CurrentStage = context.CurrentStage.ToString(),
                ProcurementRequestId = context.ProcurementRequestId,
                RequestNumber = context.RequestNumber,
                EstimatedTotal = context.EstimatedTotal,
                ExecutionSummary = context.ExecutionSummary,
                ClarificationPrompt = context.ClarificationPrompt,
                Errors = context.Errors,
                Plan = context.Plan,
                AuditTrail = context.AuditTrail,
                UpdatedAt = context.UpdatedAt
            };

            return Ok(response);
        }

        [HttpGet("ai/workflows/{id}")]
        public async Task<IActionResult> GetWorkflowStatus(Guid id)
        {
            try
            {
                var context = await _orchestrator.GetWorkflowAsync(id, GetUserId(), GetUserRole());
                if (context == null) return NotFound("Workflow not found.");

                var response = new WorkflowProcessResponseDto
                {
                    WorkflowId = context.WorkflowId,
                    Status = context.Status.ToString(),
                    CurrentStage = context.CurrentStage.ToString(),
                    ProcurementRequestId = context.ProcurementRequestId,
                    RequestNumber = context.RequestNumber,
                    EstimatedTotal = context.EstimatedTotal,
                    ExecutionSummary = context.ExecutionSummary,
                    ClarificationPrompt = context.ClarificationPrompt,
                    Errors = context.Errors,
                    Plan = context.Plan,
                    AuditTrail = context.AuditTrail,
                    UpdatedAt = context.UpdatedAt
                };

                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Problem(detail: ex.Message, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");
            }
        }
    }
}
