using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procura.API.Shared.Data;
using Procura.API.Shared.Enums;
using Procura.API.Shared.Users.DTOs;

namespace Procura.API.Shared.Users
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = "ADMIN")]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _context.Users
                .OrderBy(u => u.CreatedAt)
                .Select(u => new UserSummaryDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Role = u.Role.ToString(),
                    IsActive = u.IsActive,
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPatch("{id}/role")]
        public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateUserRoleDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Role))
            {
                return BadRequest("Role is required.");
            }

            var normalizedRole = dto.Role.Trim().ToUpperInvariant();
            if (normalizedRole != "EMPLOYEE" && normalizedRole != "PROCUREMENT_OFFICER" && normalizedRole != "ADMIN")
            {
                return BadRequest("Invalid role. Allowed roles are: EMPLOYEE, PROCUREMENT_OFFICER, ADMIN.");
            }

            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound("User not found.");
            }

            if (!Enum.TryParse<SystemRole>(normalizedRole, out var newRole))
            {
                return BadRequest("Invalid system role.");
            }

            // Prevent self-lockout if the only active admin demotes themselves
            var currentUserIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (Guid.TryParse(currentUserIdClaim, out var currentUserId) && currentUserId == id && newRole != SystemRole.ADMIN)
            {
                var otherAdminExists = await _context.Users.AnyAsync(u => u.Id != id && u.Role == SystemRole.ADMIN && u.IsActive);
                if (!otherAdminExists)
                {
                    return BadRequest("Cannot change your own role from ADMIN because you are the only active administrator.");
                }
            }

            user.Role = newRole;
            user.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new UserSummaryDto
            {
                Id = user.Id,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = user.Role.ToString(),
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt
            });
        }
    }
}
