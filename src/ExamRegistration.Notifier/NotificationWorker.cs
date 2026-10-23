using Azure.Messaging.ServiceBus;
using ExamRegistration.Contracts;

namespace ExamRegistration.Notifier;

/// <summary>E: абонат на topic registration-events (subscription "notifications"); симулира изпращане на имейл.</summary>
public sealed class NotificationWorker(ServiceBusClient client, ILogger<NotificationWorker> logger) : BackgroundService
{
    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = client.CreateProcessor(
            ServiceBusNames.RegistrationEventsTopic,
            ServiceBusNames.NotificationsSubscription,
            new ServiceBusProcessorOptions { AutoCompleteMessages = false, MaxConcurrentCalls = 4 });
        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += args =>
        {
            logger.LogError(args.Exception, "Service Bus error ({Source}) on {Entity}", args.ErrorSource, args.EntityPath);
            return Task.CompletedTask;
        };

        await _processor.StartProcessingAsync(stoppingToken);
        logger.LogInformation("Listening on {Topic}/{Subscription}",
            ServiceBusNames.RegistrationEventsTopic, ServiceBusNames.NotificationsSubscription);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        var result = args.Message.Body.ToObjectFromJson<ExamRegistrationProcessed>();
        if (result is null)
        {
            await args.DeadLetterMessageAsync(args.Message, "InvalidPayload", cancellationToken: args.CancellationToken);
            return;
        }

        var body = result.Status == RegistrationStatus.Accepted
            ? $"Успешно сте записан(а) за изпит {result.ExamId}."
            : $"Записването за изпит {result.ExamId} е отказано: {result.Reason}";

        // Симулация на имейл - в реална система тук би стоял SMTP / SendGrid клиент
        logger.LogInformation("EMAIL to {Email} | Subject: Записване за изпит | {Body}", result.StudentEmail, body);

        await args.CompleteMessageAsync(args.Message, args.CancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor is not null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}
