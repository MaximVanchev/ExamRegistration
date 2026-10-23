using System.Security.Claims;
using Azure.Messaging.ServiceBus;
using ExamRegistration.Api;
using ExamRegistration.Contracts;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

// ---- C/F: Service Bus настройки (SAS connection string от user-secrets / appsettings) ----
builder.Services.AddOptions<ServiceBusOptions>()
    .Bind(builder.Configuration.GetSection(ServiceBusOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var serviceBusConnection = builder.Configuration[$"{ServiceBusOptions.SectionName}:ConnectionString"];

// ---- C: регистриране на ServiceBusClient и sender за опашката в DI ----
builder.Services.AddAzureClients(clients =>
{
    clients.AddServiceBusClient(serviceBusConnection);
    clients.AddClient<ServiceBusSender, ServiceBusClientOptions>((_, _, provider) =>
            provider.GetRequiredService<ServiceBusClient>().CreateSender(ServiceBusNames.RegistrationsQueue))
        .WithName(ServiceBusNames.RegistrationsQueue);
});

builder.Services.AddSingleton<IRegistrationPublisher, ServiceBusRegistrationPublisher>();
builder.Services.AddSingleton<RegistrationStore>();
builder.Services.AddSingleton(TimeProvider.System);

// ---- F: Authentication (JWT Bearer, токени от `dotnet user-jwts`) и Authorization policies ----
builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.CanRegister, policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(Roles.Student)
        .RequireClaim("scope", "registrations:write"))
    .AddPolicy(Policies.CanViewAll, policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(Roles.Admin));

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// ---- B: HTTP endpoint-и ----
app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy", service = "ExamRegistration.Api" }))
    .AllowAnonymous();

app.MapGet("/api/exams", () => Results.Ok(ExamCatalog.All))
    .AllowAnonymous();

app.MapPost("/api/registrations", async (
        RegistrationRequest request,
        ClaimsPrincipal user,
        IRegistrationPublisher publisher,
        RegistrationStore store,
        TimeProvider clock,
        CancellationToken ct) =>
    {
        var errors = request.Validate();
        if (errors.Count > 0)
            return Results.ValidationProblem(errors);

        var message = new ExamRegistrationRequested(
            Guid.NewGuid(),
            request.FacultyNumber,
            request.StudentName,
            request.StudentEmail,
            request.ExamId.ToUpperInvariant(),
            clock.GetUtcNow());

        // D: изпращане на съобщението в опашката
        await publisher.PublishAsync(message, ct);
        store.Add(message, user.Identity?.Name);

        return Results.Accepted($"/api/registrations/{message.RegistrationId}",
            new { message.RegistrationId, status = "Pending" });
    })
    .RequireAuthorization(Policies.CanRegister);

app.MapGet("/api/registrations/{id:guid}", (Guid id, RegistrationStore store) =>
        store.Get(id) is { } entry ? Results.Ok(entry) : Results.NotFound())
    .RequireAuthorization(Policies.CanRegister);

app.MapGet("/api/registrations", (RegistrationStore store) => Results.Ok(store.All()))
    .RequireAuthorization(Policies.CanViewAll);

app.Run();

// Нужно за WebApplicationFactory<Program> в тестовете
public partial class Program;
