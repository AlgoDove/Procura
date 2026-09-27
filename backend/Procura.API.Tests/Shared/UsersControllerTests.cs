using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Procura.API.Shared.Data;
using Procura.API.Shared.Entities;
using Procura.API.Shared.Enums;
using Procura.API.Shared.Users;
using Procura.API.Shared.Users.DTOs;
using Xunit;

namespace Procura.API.Tests.Shared
{
    public class UsersControllerTests
    {
        private ApplicationDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private UsersController CreateControllerWithAdminUser(ApplicationDbContext context, Guid adminId)
        {
            var controller = new UsersController(context);
            var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, adminId.ToString()),
                new Claim(ClaimTypes.Role, "ADMIN")
            }, "TestAuth"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = userPrincipal }
            };

            return controller;
        }

        [Fact]
        public async Task GetAll_ReturnsAllUsersWithSummary()
        {
            using var context = CreateInMemoryDbContext();
            var adminId = Guid.NewGuid();
            context.Users.AddRange(
                new User { Id = adminId, Email = "admin@procura.com", FirstName = "Admin", LastName = "User", Role = SystemRole.ADMIN },
                new User { Id = Guid.NewGuid(), Email = "emp@procura.com", FirstName = "John", LastName = "Doe", Role = SystemRole.EMPLOYEE }
            );
            await context.SaveChangesAsync();

            var controller = CreateControllerWithAdminUser(context, adminId);
            var result = await controller.GetAll();

            var okResult = Assert.IsType<OkObjectResult>(result);
            var users = Assert.IsAssignableFrom<IEnumerable<UserSummaryDto>>(okResult.Value);
            Assert.Equal(2, users.Count());
        }

        [Fact]
        public async Task UpdateRole_PromotesEmployeeToProcurementOfficer()
        {
            using var context = CreateInMemoryDbContext();
            var adminId = Guid.NewGuid();
            var employeeId = Guid.NewGuid();

            context.Users.AddRange(
                new User { Id = adminId, Email = "admin@procura.com", Role = SystemRole.ADMIN },
                new User { Id = employeeId, Email = "emp@procura.com", Role = SystemRole.EMPLOYEE }
            );
            await context.SaveChangesAsync();

            var controller = CreateControllerWithAdminUser(context, adminId);
            var result = await controller.UpdateRole(employeeId, new UpdateUserRoleDto { Role = "PROCUREMENT_OFFICER" });

            var okResult = Assert.IsType<OkObjectResult>(result);
            var userSummary = Assert.IsType<UserSummaryDto>(okResult.Value);
            Assert.Equal("PROCUREMENT_OFFICER", userSummary.Role);

            var dbUser = await context.Users.FindAsync(employeeId);
            Assert.Equal(SystemRole.PROCUREMENT_OFFICER, dbUser!.Role);
        }

        [Theory]
        [InlineData("MANAGER")]
        [InlineData("SUPERADMIN")]
        [InlineData("INVALID")]
        [InlineData("")]
        public async Task UpdateRole_RejectsUnsupportedRoles(string invalidRole)
        {
            using var context = CreateInMemoryDbContext();
            var adminId = Guid.NewGuid();
            var employeeId = Guid.NewGuid();

            context.Users.AddRange(
                new User { Id = adminId, Email = "admin@procura.com", Role = SystemRole.ADMIN },
                new User { Id = employeeId, Email = "emp@procura.com", Role = SystemRole.EMPLOYEE }
            );
            await context.SaveChangesAsync();

            var controller = CreateControllerWithAdminUser(context, adminId);
            var result = await controller.UpdateRole(employeeId, new UpdateUserRoleDto { Role = invalidRole });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task UpdateRole_PreventsSoleAdminFromDemotingThemselves()
        {
            using var context = CreateInMemoryDbContext();
            var adminId = Guid.NewGuid();

            context.Users.Add(new User { Id = adminId, Email = "admin@procura.com", Role = SystemRole.ADMIN, IsActive = true });
            await context.SaveChangesAsync();

            var controller = CreateControllerWithAdminUser(context, adminId);
            var result = await controller.UpdateRole(adminId, new UpdateUserRoleDto { Role = "EMPLOYEE" });

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Contains("only active administrator", badRequest.Value!.ToString(), StringComparison.OrdinalIgnoreCase);

            var dbUser = await context.Users.FindAsync(adminId);
            Assert.Equal(SystemRole.ADMIN, dbUser!.Role);
        }

        [Fact]
        public async Task UpdateRole_AllowsSelfRoleChange_WhenAnotherAdminExists()
        {
            using var context = CreateInMemoryDbContext();
            var admin1Id = Guid.NewGuid();
            var admin2Id = Guid.NewGuid();

            context.Users.AddRange(
                new User { Id = admin1Id, Email = "admin1@procura.com", Role = SystemRole.ADMIN, IsActive = true },
                new User { Id = admin2Id, Email = "admin2@procura.com", Role = SystemRole.ADMIN, IsActive = true }
            );
            await context.SaveChangesAsync();

            var controller = CreateControllerWithAdminUser(context, admin1Id);
            var result = await controller.UpdateRole(admin1Id, new UpdateUserRoleDto { Role = "EMPLOYEE" });

            var okResult = Assert.IsType<OkObjectResult>(result);
            var userSummary = Assert.IsType<UserSummaryDto>(okResult.Value);
            Assert.Equal("EMPLOYEE", userSummary.Role);
        }

        [Fact]
        public async Task UpdateRole_ReturnsNotFound_WhenUserDoesNotExist()
        {
            using var context = CreateInMemoryDbContext();
            var adminId = Guid.NewGuid();

            context.Users.Add(new User { Id = adminId, Email = "admin@procura.com", Role = SystemRole.ADMIN });
            await context.SaveChangesAsync();

            var controller = CreateControllerWithAdminUser(context, adminId);
            var result = await controller.UpdateRole(Guid.NewGuid(), new UpdateUserRoleDto { Role = "EMPLOYEE" });

            Assert.IsType<NotFoundObjectResult>(result);
        }
    }
}
