# WHOOP Hardware Telemetry Platform

Backend RESTful API for managing WHOOP hardware devices and biometric telemetry records using ASP.NET Core, Entity Framework Core, MySQL, and a Domain-Driven Design structure.

## Current Base Status

The project currently has the shared foundation ready:

- `Shared` bounded context compiles.
- Swagger is enabled and opens automatically from the launch profile.
- `AppDbContext` is registered in `Program.cs`.
- MySQL connection is read from `appsettings.json`.
- EF Core runs `Database.Migrate()` when the app starts.
- Shared repositories, unit of work, audit interceptor, naming strategy, localization resources, middleware, and mediator abstractions are present.

Swagger is empty for now because there are no REST controllers yet.

## Important Settings To Review

Before creating the bounded contexts, review these values.

### Project Name

The exam asks for a root namespace like:

```text
Whoop.HardwareTelemetry.Platform.uYOUR_CODE
```

This project currently uses:

```text
whoop
```

If you want to match the exam strictly, rename namespaces before creating many files. Doing it later is possible, but more annoying.

### Database Name

The exam asks for a MySQL schema named:

```text
whoop
```

Current connection string:

```json
"DefaultConnection": "server=localhost;user=root;password=password;database=whoops-wa"
```

Recommended exam-aligned version:

```json
"DefaultConnection": "server=localhost;user=root;password=password;database=whoop"
```

Also update `user` and `password` if your local MySQL credentials are different.

## About The EF Core Startup Message

You may see this when running the app:

```text
The model for context 'AppDbContext' has pending changes.
Add a new migration before updating the database.
No migrations were found in assembly 'whoop'.
```

This happens because `Program.cs` calls:

```csharp
context.Database.Migrate();
```

but the project still has no migrations.

Right now it is not blocking the app. Your log shows the app still started:

```text
Now listening on: http://localhost:5191
Application started.
```

Once you create the `Hardware` and `Telemetry` entities and mappings, add a migration:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

Run those commands from:

```text
whoop/whoop
```

If `dotnet ef` is not installed, install the tool:

```bash
dotnet tool install --global dotnet-ef
```

## Recommended Bounded Context Order

Create the bounded contexts in this order.

## 1. Hardware Bounded Context

Create this structure:

```text
Hardware/
  Domain/
    Model/
      Aggregates/
      Commands/
      Queries/
      ValueObjects/
    Repositories/
  Application/
    CommandServices/
    Internal/
      CommandServices/
      QueryServices/
      EventHandlers/
    QueryServices/
  Infrastructure/
    Persistence/
      EntityFrameworkCore/
        Configuration/
          Extensions/
        Repositories/
  Interfaces/
    Rest/
      Resources/
      Transform/
```

Minimum domain objects:

- `Device`
- `DeviceStatus`
- `IDeviceRepository`

Device attributes:

- `Id`
- `SerialNumber`
- `Model`
- `Status`
- `LastSyncDate`
- `CreatedAt`
- `UpdatedAt`

Business method:

```csharp
public bool IsDataTransmissionAllowed()
{
    return Status == DeviceStatus.Active;
}
```

Persistence mapping:

- Table should become `devices`.
- `SerialNumber` must be unique.
- Audit fields are internal and should not be returned by API responses.
- Seed the 5 devices required by the exam.

Endpoint:

```text
GET /api/v1/devices
```

## 2. Telemetry Bounded Context

Create this structure:

```text
Telemetry/
  Domain/
    Model/
      Aggregates/
      Commands/
      Events/
      Queries/
      ValueObjects/
    Repositories/
  Application/
    Acl/
    CommandServices/
    Internal/
      CommandServices/
      OutboundServices/
  Infrastructure/
    Persistence/
      EntityFrameworkCore/
        Configuration/
          Extensions/
        Repositories/
  Interfaces/
    Rest/
      Resources/
      Transform/
```

Minimum domain objects:

- `TelemetryDataRecord`
- `VitalSignData`
- `DeviceHealthData`
- `TelemetryProcessedEvent`
- `ITelemetryDataRecordRepository`

Telemetry attributes:

- `Id`
- `DeviceId`
- `VitalSignData`
- `DeviceHealthData`
- `RecordedAt`
- `CreatedAt`
- `UpdatedAt`

Value objects:

```csharp
public record VitalSignData(int HeartRate, int RespiratoryRate);
public record DeviceHealthData(int BatteryLevel, string DeviceStatus);
```

Business validations:

- `RecordedAt` cannot be greater than `DateTime.UtcNow`.
- Request JSON receives `RecordedAt` in `yyyy-MM-dd HH:mm:ss` format.
- `DeviceStatus` is a string.

Endpoint:

```text
POST /api/v1/telemetry-data-records
```

Expected success response:

```text
201 Created
```

Do not include audit fields in the response.

## 3. Anti-Corruption Layer

The `Telemetry` context must not directly depend on Hardware internals.

Create an ACL/facade so Telemetry can ask:

```text
Is this device authorized for data transmission?
```

If the device does not exist or is inactive, return:

```text
403 Forbidden
Device authorization failed
```

## 4. Asynchronous Event Handling

After a valid telemetry record is saved:

1. Emit `TelemetryProcessedEvent`.
2. Hardware handler receives the event.
3. Handler verifies idempotency.
4. Handler updates `Device.LastSyncDate`.

Suggested event data:

```csharp
public class TelemetryProcessedEvent(long RecordId, long DeviceId, DateTime RecordedAt) : IEvent;
```

For idempotency, store processed telemetry record ids in Hardware, for example:

```text
ProcessedTelemetryRecords
```

or another simple internal mechanism.

## AppDbContext Changes After Creating BCs

Once these methods exist:

```csharp
builder.ApplyHardwareConfiguration();
builder.ApplyTelemetryConfiguration();
```

update `Shared/Infrastructure/Persistence/EntityFrameworkCore/Configuration/AppDbContext.cs`:

```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);

    builder.ApplyHardwareConfiguration();
    builder.ApplyTelemetryConfiguration();

    builder.UseSnakeCaseNamingConvention();
}
```

Do not add these calls before the extension methods exist, or the project will stop compiling.

## Program.cs Items To Add Later

When each service/repository exists, register it in `Program.cs`.

Shared:

```csharp
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
```

Hardware example:

```csharp
builder.Services.AddScoped<IDeviceRepository, DeviceRepository>();
builder.Services.AddScoped<IDeviceQueryService, DeviceQueryService>();
```

Telemetry example:

```csharp
builder.Services.AddScoped<ITelemetryDataRecordRepository, TelemetryDataRecordRepository>();
builder.Services.AddScoped<ITelemetryDataRecordCommandService, TelemetryDataRecordCommandService>();
```

Mediator:

```csharp
builder.Services.AddScoped(typeof(ICommandPipelineBehavior<>), typeof(LoggingCommandBehavior<>));
builder.Services.AddCortexMediator([typeof(Program)]);
```

Localization:

```csharp
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
```

Global exception handling:

```csharp
app.UseGlobalExceptionHandler();
```

## Migration Workflow

After creating entities and Fluent API mappings:

```bash
dotnet build
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run
```

If the app is currently running, stop it before building. Otherwise Windows may lock:

```text
bin/Debug/net10.0/whoop.exe
bin/Debug/net10.0/whoop.dll
```

## Final Exam Checklist

- Swagger opens at `/swagger`.
- `GET /api/v1/devices` works.
- `POST /api/v1/telemetry-data-records` works.
- Invalid or inactive device returns `403 Forbidden`.
- `RecordedAt` cannot be in the future.
- `TelemetryProcessedEvent` updates `Device.LastSyncDate`.
- Same telemetry `RecordId` is not processed twice by the handler.
- Responses support English and Spanish through `Accept-Language`.
- Responses do not expose audit fields.
- Database objects use plural snake case names.
- README final version includes app description and author.
