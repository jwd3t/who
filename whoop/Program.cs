using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.OpenApi;
using whoop.Shared.Resources;
using whoop.Shared.Resources.Errors;
using whoop.Shared.Domain.Repositories;
using whoop.Shared.Infrastructure.Interfaces.AspNetCore.Configuration;
using whoop.Shared.Infrastructure.Persistence.EntityFrameworkCore.Configuration;
using whoop.Shared.Infrastructure.Persistence.EntityFrameworkCore.Repositories;
using whoop.Shared.Infrastructure.Pipeline.Middleware.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddControllers(options => options.Conventions.Add(new KebabCaseRouteNamingConvention()));
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddSingleton<IStringLocalizer<ErrorMessages>, StringLocalizer<ErrorMessages>>();
builder.Services.AddSingleton<IStringLocalizer<CommonMessages>, StringLocalizer<CommonMessages>>();
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Database connection string is not set in the configuration.");

    options.UseMySQL(connectionString);
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WHOOP Hardware Telemetry Platform API",
        Version = "v1",
        Description = "REST API for hardware devices and biometric telemetry records."
    });
    options.EnableAnnotations();
});

// Dependency Injection
// Register each interface with the concrete class that implements it.
// Add these registrations only after the interfaces and classes exist.
//
// Shared Bounded Context
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
//
// Hardware Bounded Context
// Example:
// builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
// builder.Services.AddScoped<IDeviceQueryService, DeviceQueryService>();
//
// Telemetry Bounded Context
// Example:
// builder.Services.AddScoped<ITelemetryDataRecordRepository, TelemetryDataRecordRepository>();
// builder.Services.AddScoped<ITelemetryDataRecordCommandService, TelemetryDataRecordCommandService>();
//
// In short: if a controller or service receives an interface in its constructor,
// register that interface here with its implementation.

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.Migrate();
}

// Configure the HTTP request pipeline.
app.UseGlobalExceptionHandler();

var supportedCultures = new[] { "en", "en-US", "es", "es-PE" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
