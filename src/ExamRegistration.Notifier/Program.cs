using ExamRegistration.Contracts;
using ExamRegistration.Notifier;
using Microsoft.Extensions.Azure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<ServiceBusOptions>()
    .Bind(builder.Configuration.GetSection(ServiceBusOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddAzureClients(clients =>
    clients.AddServiceBusClient(builder.Configuration[$"{ServiceBusOptions.SectionName}:ConnectionString"]));

builder.Services.AddHostedService<NotificationWorker>();

builder.Build().Run();
