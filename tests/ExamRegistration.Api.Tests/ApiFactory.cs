using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ExamRegistration.Api;
using ExamRegistration.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace ExamRegistration.Api.Tests;

/// <summary>Стартира Api в паметта; Service Bus е заменен с fake, JWT се валидира с тестов ключ.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private const string Issuer = "dotnet-user-jwts";
    private const string Audience = "http://localhost:5080";
    private static readonly byte[] SigningKey = Encoding.UTF8.GetBytes("test-signing-key-test-signing-key!!");

    public FakePublisher Publisher { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Authentication:Schemes:Bearer:ValidIssuer", Issuer);
        builder.UseSetting("Authentication:Schemes:Bearer:ValidAudiences:0", Audience);
        builder.UseSetting("Authentication:Schemes:Bearer:SigningKeys:0:Issuer", Issuer);
        builder.UseSetting("Authentication:Schemes:Bearer:SigningKeys:0:Value", Convert.ToBase64String(SigningKey));
        builder.ConfigureServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IRegistrationPublisher>(Publisher)));
    }

    public static string CreateToken(string role, params string[] scopes)
    {
        var claims = new List<Claim> { new(ClaimTypes.Name, "test-user"), new(ClaimTypes.Role, role) };
        claims.AddRange(scopes.Select(s => new Claim("scope", s)));

        var token = new JwtSecurityToken(Issuer, Audience, claims,
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(SigningKey), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed class FakePublisher : IRegistrationPublisher
{
    public ConcurrentQueue<ExamRegistrationRequested> Sent { get; } = new();

    public Task PublishAsync(ExamRegistrationRequested message, CancellationToken ct)
    {
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
