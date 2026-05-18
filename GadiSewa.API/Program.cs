using GadiSewa.Application;
using GadiSewa.Application.Common.Responses;
using GadiSewa.Infrastructure;
using GadiSewa.Infrastructure.Authentication;
using GadiSewa.Domain.Enums;
using GadiSewa.API.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var corsPolicyName = "CorsPolicy";
var configuredCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()?
    .Select(origin => origin.Trim().TrimEnd('/'))
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? [];

var allowedCorsOrigins = configuredCorsOrigins.Length > 0
    ? configuredCorsOrigins
    : ["http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:3000"];

if (!builder.Environment.IsDevelopment() && configuredCorsOrigins.Length == 0)
{
    throw new InvalidOperationException("CORS AllowedOrigins must be configured for non-development environments.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(corsPolicyName, policy =>
    {
        policy.WithOrigins(allowedCorsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});


builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Values
                .SelectMany(x => x.Errors)
                .Select(x => string.IsNullOrWhiteSpace(x.ErrorMessage) ? "One or more validation errors occurred." : x.ErrorMessage)
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            return new BadRequestObjectResult(
                ApiResponse<object?>.Failure(errors, StatusCodes.Status400BadRequest));
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GadiSewa API",
        Version = "v1",
        Description = "API documentation for the GadiSewa backend"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token in the format: Bearer {your token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    options.EnableAnnotations();
});
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddTransient<GlobalExceptionMiddleware>();

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? throw new InvalidOperationException("JWT configuration is missing.");

if (string.IsNullOrWhiteSpace(jwtOptions.Key))
{
    throw new InvalidOperationException("JWT Key is missing from configuration.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(UserRole.Admin.ToString()));
    options.AddPolicy("StaffOnly", policy => policy.RequireRole(UserRole.Staff.ToString()));
    options.AddPolicy("BackOfficeOnly", policy => policy.RequireRole(UserRole.Admin.ToString(), UserRole.Staff.ToString()));
    options.AddPolicy("CustomerOnly", policy => policy.RequireRole(UserRole.Customer.ToString()));
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseCors(corsPolicyName);

// Only use HTTPS redirection in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "GadiSewa API v1");
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
