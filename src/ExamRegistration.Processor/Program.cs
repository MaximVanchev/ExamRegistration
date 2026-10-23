using Azure.Messaging.ServiceBus;
using ExamRegistration.Contracts;
using ExamRegistration.Processor;
using Microsoft.Extensions.Azure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<ServiceBusOptions>()
    .Bind(builder.Configuration.GetSection(ServiceBusOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddAzureClients(clients =>
{
    clients.AddServiceBusClient(builder.Configuration[$"{ServiceBusOptions.SectionName}:ConnectionString"]);
    // sender към topic-а, в който публикуваме резултата от обработката
    clients.AddClient<ServiceBusSender, ServiceBusClientOptions>((_, _, provider) =>
            provider.GetRequiredService<ServiceBusClient>().CreateSender(ServiceBusNames.RegistrationEventsTopic))
        .WithName(ServiceBusNames.RegistrationEventsTopic);
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<RegistrationValidator>();
builder.Services.AddHostedService<RegistrationProcessorWorker>();

builder.Build().Run();
