using Azure.Messaging.ServiceBus;
using ExamRegistration.Api;
using ExamRegistration.Contracts;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

// ---- C: Service Bus настройки (SAS connection string от user-secrets / appsettings) ----
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

var app = builder.Build();

// ---- B: HTTP endpoint-и ----
app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy", service = "ExamRegistration.Api" }));

app.MapGet("/api/exams", () => Results.Ok(ExamCatalog.All));

app.MapPost("/api/registrations", async (
        RegistrationRequest request,
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
        store.Add(message, null);

        return Results.Accepted($"/api/registrations/{message.RegistrationId}",
            new { message.RegistrationId, status = "Pending" });
    });

app.MapGet("/api/registrations/{id:guid}", (Guid id, RegistrationStore store) =>
        store.Get(id) is { } entry ? Results.Ok(entry) : Results.NotFound());

app.MapGet("/api/registrations", (RegistrationStore store) => Results.Ok(store.All()));

app.Run();

// Нужно за WebApplicationFactory<Program> в тестовете
public partial class Program;
