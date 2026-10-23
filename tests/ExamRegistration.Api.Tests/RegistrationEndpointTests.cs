using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace ExamRegistration.Api.Tests;

public sealed class RegistrationEndpointTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly object ValidRequest = new
    {
        facultyNumber = "121221001",
        studentName = "Иван Петров",
        studentEmail = "ivan@tu-sofia.bg",
        examId = "PRS-2026-01"
    };

    private HttpClient ClientWithToken(string? token)
    {
        var client = factory.CreateClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Health_IsAnonymous_Returns200()
    {
        var response = await ClientWithToken(null).GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Exams_ReturnsCatalog()
    {
        var exams = await ClientWithToken(null).GetFromJsonAsync<List<Dictionary<string, object>>>("/api/exams");
        Assert.NotNull(exams);
        Assert.NotEmpty(exams);
    }

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var response = await ClientWithToken(null).PostAsJsonAsync("/api/registrations", ValidRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithoutRequiredScope_Returns403()
    {
        var token = ApiFactory.CreateToken("Student");
        var response = await ClientWithToken(token).PostAsJsonAsync("/api/registrations", ValidRequest);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_ValidRequest_Returns202_AndPublishesMessage()
    {
        var token = ApiFactory.CreateToken("Student", "registrations:write");
        var response = await ClientWithToken(token).PostAsJsonAsync("/api/registrations", ValidRequest);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AcceptedBody>();
        Assert.NotNull(body);
        Assert.Contains(factory.Publisher.Sent, m => m.RegistrationId == body.RegistrationId && m.ExamId == "PRS-2026-01");
        Assert.Equal($"/api/registrations/{body.RegistrationId}", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Post_InvalidRequest_Returns400_WithErrors()
    {
        var token = ApiFactory.CreateToken("Student", "registrations:write");
        var response = await ClientWithToken(token).PostAsJsonAsync("/api/registrations",
            new { facultyNumber = "12", studentName = "", studentEmail = "x", examId = "NOPE" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("FacultyNumber", text);
        Assert.Contains("ExamId", text);
    }

    [Fact]
    public async Task ListAll_RequiresAdmin()
    {
        var student = ApiFactory.CreateToken("Student", "registrations:write");
        var admin = ApiFactory.CreateToken("Admin");

        Assert.Equal(HttpStatusCode.Forbidden, (await ClientWithToken(student).GetAsync("/api/registrations")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ClientWithToken(admin).GetAsync("/api/registrations")).StatusCode);
    }

    private sealed record AcceptedBody(Guid RegistrationId, string Status);
}
