using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Procura.API.Shared.Data
{
    /// <summary>
    /// Design-time DbContext factory used by EF Core tools (e.g. dotnet ef database update).
    /// Resolves connection strings from environment variables (DATABASE_URL, ConnectionStrings__DefaultConnection)
    /// or falls back to appsettings.json, parsing PostgreSQL URI formats when present.
    /// </summary>
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var basePath = Directory.GetCurrentDirectory();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var databaseUrl = configuration["DATABASE_URL"];

            var rawToParse = !string.IsNullOrWhiteSpace(databaseUrl) ? databaseUrl : connectionString;
            if (!string.IsNullOrWhiteSpace(rawToParse) && (rawToParse.StartsWith("postgres://") || rawToParse.StartsWith("postgresql://")))
            {
                try
                {
                    var uri = new Uri(rawToParse);
                    var userInfo = uri.UserInfo.Split(':');
                    var npgsqlBuilder = new NpgsqlConnectionStringBuilder
                    {
                        Host = uri.Host,
                        Port = uri.Port > 0 ? uri.Port : 5432,
                        Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "",
                        Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
                        Database = uri.AbsolutePath.TrimStart('/'),
                        SslMode = Npgsql.SslMode.Require,
                        TrustServerCertificate = true
                    };
                    connectionString = npgsqlBuilder.ToString();
                }
                catch
                {
                    // Fall back to original value if parsing fails
                }
            }

            connectionString ??= "Host=localhost;Port=5432;Database=procura;Username=postgres";

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
