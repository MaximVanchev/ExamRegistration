using Azure.Messaging.ServiceBus;
using ExamRegistration.Contracts;
using ExamRegistration.Processor;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExamRegistration.IntegrationTests;

/// <summary>
/// Свързва се с работещия Azure Service Bus Emulator (docker compose up -d в emulator/)
/// и стартира RegistrationProcessorWorker в процеса на теста.
/// </summary>
public sealed class EmulatorFixture : IAsyncLifetime
{
    public const string ConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    private IHost? _processorHost;

    public ServiceBusClient Client { get; } = new(ConnectionString);

    public async Task InitializeAsync()
    {
        await DrainAsync(Client.CreateReceiver(ServiceBusNames.RegistrationEventsTopic, ServiceBusNames.AuditSubscription));

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddAzureClients(clients =>
        {
            clients.AddServiceBusClient(ConnectionString);
            clients.AddClient<ServiceBusSender, ServiceBusClientOptions>((_, _, provider) =>
                    provider.GetRequiredService<ServiceBusClient>().CreateSender(ServiceBusNames.RegistrationEventsTopic))
                .WithName(ServiceBusNames.RegistrationEventsTopic);
        });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<RegistrationValidator>();
        builder.Services.AddHostedService<RegistrationProcessorWorker>();

        _processorHost = builder.Build();
        await _processorHost.StartAsync();
    }

    /// <summary>Чака резултата за дадена заявка в subscription "audit".</summary>
    public async Task<ExamRegistrationProcessed> WaitForResultAsync(Guid registrationId, TimeSpan timeout)
    {
        await using var receiver = Client.CreateReceiver(ServiceBusNames.RegistrationEventsTopic, ServiceBusNames.AuditSubscription);
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var message = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(2));
            if (message is null)
                continue;

            await receiver.CompleteMessageAsync(message);
            var result = message.Body.ToObjectFromJson<ExamRegistrationProcessed>();
            if (result?.RegistrationId == registrationId)
                return result;
        }
        throw new TimeoutException($"Няма резултат за {registrationId} в рамките на {timeout}.");
    }

    private static async Task DrainAsync(ServiceBusReceiver receiver)
    {
        await using (receiver)
        {
            while (await receiver.ReceiveMessagesAsync(50, TimeSpan.FromSeconds(1)) is { Count: > 0 } batch)
                foreach (var m in batch)
                    await receiver.CompleteMessageAsync(m);
        }
    }

    public async Task DisposeAsync()
    {
        if (_processorHost is not null)
        {
            await _processorHost.StopAsync();
            _processorHost.Dispose();
        }
        await Client.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class EmulatorCollection : ICollectionFixture<EmulatorFixture>
{
    public const string Name = "Service Bus Emulator";
}
