using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Azure.Messaging.ServiceBus;
using ExamRegistration.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace ExamRegistration.IntegrationTests;

/// <summary>H: end-to-end тестове през Azure Service Bus Emulator (без интернет и без Azure акаунт).</summary>
[Collection(EmulatorCollection.Name)]
public sealed class MessagingFlowTests(EmulatorFixture emulator)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static ExamRegistrationRequested NewRequest(string examId, string? facultyNumber = null) => new(
        Guid.NewGuid(),
        facultyNumber ?? Random.Shared.NextInt64(100_000_000, 999_999_999).ToString(),
        "Тестов Студент",
        "test@tu-sofia.bg",
        examId,
        DateTimeOffset.UtcNow);

    private async Task SendToQueueAsync(ExamRegistrationRequested request)
    {
        await using var sender = emulator.Client.CreateSender(ServiceBusNames.RegistrationsQueue);
        await sender.SendMessageAsync(new ServiceBusMessage(BinaryData.FromObjectAsJson(request))
        {
            MessageId = request.RegistrationId.ToString(),
            CorrelationId = request.RegistrationId.ToString()
        });
    }

    [Fact]
    public async Task ApiPost_FlowsThroughQueueAndTopic_Accepted()
    {
        await using var api = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ServiceBus:ConnectionString", EmulatorFixture.ConnectionString);
            TestJwt.Configure(b);
        });
        var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.StudentToken());

        var response = await client.PostAsJsonAsync("/api/registrations", new
        {
            facultyNumber = Random.Shared.NextInt64(100_000_000, 999_999_999).ToString(),
            studentName = "Иван Петров",
            studentEmail = "ivan@tu-sofia.bg",
            examId = "PRS-2026-01"
        });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AcceptedBody>();

        var result = await emulator.WaitForResultAsync(body!.RegistrationId, Timeout);

        Assert.Equal(RegistrationStatus.Accepted, result.Status);
        Assert.Equal("PRS-2026-01", result.ExamId);
    }

    [Fact]
    public async Task ExpiredDeadline_IsRejected()
    {
        var request = NewRequest("MATH-2025-09");
        await SendToQueueAsync(request);

        var result = await emulator.WaitForResultAsync(request.RegistrationId, Timeout);

        Assert.Equal(RegistrationStatus.Rejected, result.Status);
        Assert.Contains("Срокът", result.Reason);
    }

    [Fact]
    public async Task DuplicateStudent_SecondRegistrationIsRejected()
    {
        var first = NewRequest("BD-2026-01");
        var second = NewRequest("BD-2026-01", first.FacultyNumber);

        await SendToQueueAsync(first);
        Assert.Equal(RegistrationStatus.Accepted, (await emulator.WaitForResultAsync(first.RegistrationId, Timeout)).Status);

        await SendToQueueAsync(second);
        var result = await emulator.WaitForResultAsync(second.RegistrationId, Timeout);
        Assert.Equal(RegistrationStatus.Rejected, result.Status);
        Assert.Contains("вече е записан", result.Reason);
    }

    [Fact]
    public async Task PoisonMessage_GoesToDeadLetterQueue()
    {
        var messageId = $"poison-{Guid.NewGuid()}";
        await using (var sender = emulator.Client.CreateSender(ServiceBusNames.RegistrationsQueue))
            await sender.SendMessageAsync(new ServiceBusMessage("това не е JSON") { MessageId = messageId });

        await using var dlq = emulator.Client.CreateReceiver(ServiceBusNames.RegistrationsQueue,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        var deadline = DateTime.UtcNow + Timeout;
        ServiceBusReceivedMessage? found = null;
        while (found is null && DateTime.UtcNow < deadline)
        {
            var message = await dlq.ReceiveMessageAsync(TimeSpan.FromSeconds(2));
            if (message is null)
                continue;
            await dlq.CompleteMessageAsync(message);
            if (message.MessageId == messageId)
                found = message;
        }

        Assert.NotNull(found);
        Assert.Equal("InvalidPayload", found.DeadLetterReason);
    }

    private sealed record AcceptedBody(Guid RegistrationId, string Status);
}

/// <summary>Локални JWT токени със симетричен тестов ключ (същата схема като dotnet user-jwts).</summary>
internal static class TestJwt
{
    private const string Issuer = "dotnet-user-jwts";
    private const string Audience = "http://localhost:5080";
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("integration-test-signing-key-32b!!");

    public static void Configure(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Authentication:Schemes:Bearer:ValidIssuer", Issuer);
        builder.UseSetting("Authentication:Schemes:Bearer:ValidAudiences:0", Audience);
        builder.UseSetting("Authentication:Schemes:Bearer:SigningKeys:0:Issuer", Issuer);
        builder.UseSetting("Authentication:Schemes:Bearer:SigningKeys:0:Value", Convert.ToBase64String(Key));
    }

    public static string StudentToken()
    {
        var token = new JwtSecurityToken(Issuer, Audience,
            [new Claim(ClaimTypes.Name, "it-student"), new Claim(ClaimTypes.Role, "Student"), new Claim("scope", "registrations:write")],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Key), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
