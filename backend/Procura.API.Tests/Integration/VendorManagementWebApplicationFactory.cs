using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Procura.API.Shared.Data;

namespace Procura.API.Tests.Integration
{
    public class VendorManagementWebApplicationFactory : WebApplicationFactory<Program>
    {
        // Generated ONCE per factory instance, not per DbContext resolution -
        // otherwise every request would silently get its own throwaway empty database.
        private readonly string _databaseName = Guid.NewGuid().ToString();

        static VendorManagementWebApplicationFactory()
        {
            Environment.SetEnvironmentVariable("JWT_SECRET", "integration-test-secret-key-32-chars-long!");
            Environment.SetEnvironmentVariable("Jwt__Key", "integration-test-secret-key-32-chars-long!");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("JWT_SECRET", "integration-test-secret-key-32-chars-long!");
            builder.UseSetting("Jwt:Key", "integration-test-secret-key-32-chars-long!");

            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(_databaseName);
                });
            });
        }
    }
}