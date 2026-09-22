using System;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Procura.API.Modules.ProcurementRequest.Repositories;
using Procura.API.Modules.ProcurementRequest.Services;
using Procura.API.Modules.VendorEvaluation.Repositories;
using Procura.API.Modules.VendorEvaluation.Services;
using Procura.API.Shared.Authentication;
using Procura.API.Shared.Data;
using Procura.API.Shared.Middleware;
using Procura.API.AI.Core;
using Procura.API.AI.Gemini;
using Procura.API.AI.Persistence;
using Procura.API.AI.Agents.ProcurementRequest;
using Procura.API.AI.Agents.ProcurementRequest.Tools;
using Procura.API.AI.Orchestration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Global Exception Handling and Problem Details
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Health Checks
builder.Services.AddHealthChecks();

// Configure CORS for Frontend
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5173", "http://localhost:3000" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Controllers + JSON enum conversion
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Configure Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var databaseUrl = builder.Configuration["DATABASE_URL"];

if (!string.IsNullOrWhiteSpace(databaseUrl) && (databaseUrl.StartsWith("postgres://") || databaseUrl.StartsWith("postgresql://")))
{
    try
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');
        var npgsqlBuilder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = userInfo.Length > 0 ? userInfo[0] : "",
            Password = userInfo.Length > 1 ? userInfo[1] : "",
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = Npgsql.SslMode.Prefer
        };
        connectionString = npgsqlBuilder.ToString();
    }
    catch
    {
        // Fall back to default if parsing fails
    }
}

connectionString ??= "Host=localhost;Port=5432;Database=procura;Username=postgres";
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Dependency Injection - Backend
builder.Services.AddScoped<JwtTokenGenerator>();
builder.Services.AddScoped<IProcurementRequestRepository, ProcurementRequestRepository>();
builder.Services.AddScoped<IProcurementRequestService, ProcurementRequestService>();

// Dependency Injection - Vendor Evaluation Module
builder.Services.AddScoped<IVendorQuoteRepository, VendorQuoteRepository>();
builder.Services.AddScoped<IVendorQuoteService, VendorQuoteService>();
builder.Services.AddScoped<IVendorEvaluationRepository, VendorEvaluationRepository>();
builder.Services.AddScoped<IVendorEvaluationService, VendorEvaluationService>();
builder.Services.AddScoped<IVendorScoringEngine, VendorScoringEngine>();

// Dependency Injection - Vendor Managemnent Module
builder.Services.AddScoped<Procura.API.Modules.VendorManagement.Repositories.IVendorRepository, Procura.API.Modules.VendorManagement.Repositories.VendorRepository>();
builder.Services.AddScoped<Procura.API.Modules.VendorManagement.Services.IVendorService, Procura.API.Modules.VendorManagement.Services.VendorService>();
// Dependency Injection - AI Subsystem
builder.Services.Configure<GeminiOptions>(options =>
{
    var geminiSection = builder.Configuration.GetSection("Gemini");
    options.Model = builder.Configuration["Gemini:Model"] ?? geminiSection["Model"];
    options.ApiKey = builder.Configuration["Gemini:ApiKey"] ?? geminiSection["ApiKey"];
    if (int.TryParse(builder.Configuration["Gemini:TimeoutSeconds"] ?? geminiSection["TimeoutSeconds"], out var timeout))
        options.TimeoutSeconds = timeout;
    if (int.TryParse(builder.Configuration["Gemini:MaxRetries"] ?? geminiSection["MaxRetries"], out var retries))
        options.MaxRetries = retries;
});

builder.Services.AddHttpClient<IGeminiClient, GeminiClient>();
builder.Services.AddScoped<IWorkflowRepository, WorkflowRepository>();
builder.Services.AddSingleton<ProcurementRequestDeterministicValidator>();
builder.Services.AddScoped<IAgentTool, ValidateDraftDataTool>();
builder.Services.AddScoped<IAgentTool, CreateDraftRequestTool>();
builder.Services.AddScoped<Procura.API.AI.Core.IAgentTool, Procura.API.AI.Agents.VendorManagement.Tools.SearchVendorsTool>();
builder.Services.AddScoped<Procura.API.AI.Core.IAgentTool, Procura.API.AI.Agents.VendorManagement.Tools.SelectVendorTool>();
builder.Services.AddScoped<IAgentTool, GetProcurementRequestTool>();
builder.Services.AddScoped<IAgentTool, UpdateDraftRequestTool>();
builder.Services.AddScoped<ToolRegistry>();
builder.Services.AddScoped<IProcurementRequestAgent, ProcurementRequestAgent>();
builder.Services.AddScoped<Procura.API.AI.Agents.VendorManagement.VendorManagementDeterministicValidator>();
builder.Services.AddScoped<Procura.API.AI.Agents.VendorManagement.IVendorManagementAgent, Procura.API.AI.Agents.VendorManagement.VendorManagementAgent>();
builder.Services.AddScoped<IWorkflowOrchestrator, CentralOrchestrator>();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = builder.Configuration["JWT_SECRET"]
    ?? builder.Configuration["Jwt:Key"]
    ?? jwtSettings["Key"]
    ?? throw new InvalidOperationException("JWT signing key is not configured.");
    
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"] ?? "ProcuraAPI",
            ValidAudience = jwtSettings["Audience"] ?? "ProcuraClients",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
        };
    });

builder.Services.AddAuthorization();

// Configure Swagger with JWT support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Procura API", Version = "v1" });

    // JWT Swagger Configuration
    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Enter JWT Bearer token **_only_**",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };
    c.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {securityScheme, Array.Empty<string>()}
    });
});

var app = builder.Build();

app.UseExceptionHandler();

// Enable Swagger in Development or if explicitly enabled via configuration
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("EnableSwagger", false))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("FrontendPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
