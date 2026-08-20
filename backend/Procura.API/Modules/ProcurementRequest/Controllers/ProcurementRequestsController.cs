using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procura.API.Modules.ProcurementRequest.DTOs;
using Procura.API.Modules.ProcurementRequest.Enums;
using Procura.API.Modules.ProcurementRequest.Services;

namespace Procura.API.Modules.ProcurementRequest.Controllers
{
    [ApiController]
    [Route("api/procurement-requests")]
    [Authorize]
    public class ProcurementRequestsController : ControllerBase
    {
        private readonly IProcurementRequestService _service;

        public ProcurementRequestsController(IProcurementRequestService service)
        {
            _service = service;
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
                return Forbid();

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
                return Forbid(ex.Message);
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
                await _service.DeleteAsync(id, GetUserId());
                return NoContent();
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
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
                return Forbid(ex.Message);
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
    }
}
