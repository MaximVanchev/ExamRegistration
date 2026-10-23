using ExamRegistration.Api;
using ExamRegistration.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<RegistrationStore>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

// ---- B: HTTP endpoint-и ----
app.MapGet("/api/health", () => Results.Ok(new { status = "Healthy", service = "ExamRegistration.Api" }));

app.MapGet("/api/exams", () => Results.Ok(ExamCatalog.All));

app.MapPost("/api/registrations", (
        RegistrationRequest request,
        RegistrationStore store,
        TimeProvider clock) =>
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
