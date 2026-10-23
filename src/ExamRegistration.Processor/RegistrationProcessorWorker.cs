using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ExamRegistration.Contracts;
using Microsoft.Extensions.Azure;

namespace ExamRegistration.Processor;

/// <summary>E: получава заявки от опашката exam-registrations и публикува резултата в topic registration-events.</summary>
public sealed class RegistrationProcessorWorker(
    ServiceBusClient client,
    IAzureClientFactory<ServiceBusSender> senderFactory,
    RegistrationValidator validator,
    TimeProvider clock,
    ILogger<RegistrationProcessorWorker> logger) : BackgroundService
{
    private readonly ServiceBusSender _eventsSender = senderFactory.CreateClient(ServiceBusNames.RegistrationEventsTopic);
    private ServiceBusProcessor? _processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = client.CreateProcessor(ServiceBusNames.RegistrationsQueue, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false, // потвърждаваме ръчно след успешна обработка
            MaxConcurrentCalls = 1,       // последователно -> коректно броене на местата
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
        _processor.ProcessMessageAsync += HandleMessageAsync;
        _processor.ProcessErrorAsync += HandleErrorAsync;

        await _processor.StartProcessingAsync(stoppingToken);
        logger.LogInformation("Listening on queue {Queue}", ServiceBusNames.RegistrationsQueue);

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
        var message = args.Message;

        ExamRegistrationRequested? request;
        try
        {
            request = message.Body.ToObjectFromJson<ExamRegistrationRequested>();
        }
        catch (JsonException ex)
        {
            request = null;
            logger.LogWarning(ex, "Message {MessageId} has invalid JSON", message.MessageId);
        }

        if (request is null || request.RegistrationId == Guid.Empty)
        {
            // poison message -> няма смисъл от повторен опит, отива директно в Dead-Letter Queue
            await args.DeadLetterMessageAsync(message, "InvalidPayload",
                "Тялото на съобщението не е валидна ExamRegistrationRequested заявка.", args.CancellationToken);
            logger.LogWarning("Dead-lettered message {MessageId}", message.MessageId);
            return;
        }

        try
        {
            var (status, reason) = validator.TryRegister(request);
            var result = new ExamRegistrationProcessed(
                request.RegistrationId, request.FacultyNumber, request.StudentEmail,
                request.ExamId, status, reason, clock.GetUtcNow());

            var eventMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(result))
            {
                MessageId = $"{request.RegistrationId}-processed",
                CorrelationId = message.CorrelationId,
                ContentType = "application/json",
                Subject = nameof(ExamRegistrationProcessed)
            };
            eventMessage.ApplicationProperties["status"] = status.ToString();
            eventMessage.ApplicationProperties["examId"] = request.ExamId;

            await _eventsSender.SendMessageAsync(eventMessage, args.CancellationToken);
            await args.CompleteMessageAsync(message, args.CancellationToken);

            logger.LogInformation("Registration {RegistrationId} ({FacultyNumber} -> {ExamId}): {Status} {Reason}",
                request.RegistrationId, request.FacultyNumber, request.ExamId, status, reason);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // временна грешка -> връщаме съобщението в опашката; след MaxDeliveryCount отива в DLQ
            logger.LogError(ex, "Failed to process {MessageId} (delivery {Count}), abandoning",
                message.MessageId, message.DeliveryCount);
            await args.AbandonMessageAsync(message, cancellationToken: args.CancellationToken);
        }
    }

    private Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        logger.LogError(args.Exception, "Service Bus error ({Source}) on {Entity}", args.ErrorSource, args.EntityPath);
        return Task.CompletedTask;
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
