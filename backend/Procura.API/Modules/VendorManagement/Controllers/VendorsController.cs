using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Procura.API.Modules.VendorManagement.DTOs;
using Procura.API.Modules.VendorManagement.Services;

namespace Procura.API.Modules.VendorManagement.Controllers
{
    [ApiController]
    [Route("api/vendors")]
    [Authorize]
    public class VendorsController : ControllerBase
    {
        private readonly IVendorService _service;

        public VendorsController(IVendorService service)
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
        [Authorize(Roles = "PROCUREMENT_OFFICER,MANAGER,ADMIN")]
        public async Task<IActionResult> Create([FromBody] CreateVendorDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var vendor = await _service.CreateAsync(dto);
                return CreatedAtAction(nameof(GetById), new { id = vendor.Id }, vendor);
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

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? status, [FromQuery] string? category)
        {
            var vendors = await _service.GetAllAsync(status, category);
            return Ok(vendors);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var vendor = await _service.GetByIdAsync(id);
            if (vendor == null) return NotFound();
            return Ok(vendor);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "PROCUREMENT_OFFICER,MANAGER,ADMIN")]
        public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVendorDto dto)
        {
            try
            {
                if (!ModelState.IsValid) return BadRequest(ModelState);
                var vendor = await _service.UpdateAsync(id, dto);
                return Ok(vendor);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
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

        [HttpPost("{id}/deactivate")]
        [Authorize(Roles = "PROCUREMENT_OFFICER,MANAGER,ADMIN")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            try
            {
                var vendor = await _service.DeactivateAsync(id);
                return Ok(vendor);
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

        [HttpPost("{id}/activate")]
        [Authorize(Roles = "PROCUREMENT_OFFICER,MANAGER,ADMIN")]
        public async Task<IActionResult> Activate(Guid id)
        {
            try
            {
                var vendor = await _service.ActivateAsync(id);
                return Ok(vendor);
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