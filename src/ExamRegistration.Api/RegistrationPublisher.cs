using Azure.Messaging.ServiceBus;
using ExamRegistration.Contracts;
using Microsoft.Extensions.Azure;

namespace ExamRegistration.Api;

public interface IRegistrationPublisher
{
    Task PublishAsync(ExamRegistrationRequested message, CancellationToken ct);
}

/// <summary>D: логика за изпращане на съобщения в опашката exam-registrations.</summary>
public sealed class ServiceBusRegistrationPublisher(
    IAzureClientFactory<ServiceBusSender> senderFactory,
    ILogger<ServiceBusRegistrationPublisher> logger) : IRegistrationPublisher
{
    private readonly ServiceBusSender _sender = senderFactory.CreateClient(ServiceBusNames.RegistrationsQueue);

    public async Task PublishAsync(ExamRegistrationRequested message, CancellationToken ct)
    {
        var sbMessage = new ServiceBusMessage(BinaryData.FromObjectAsJson(message))
        {
            // MessageId е уникален -> опашката отхвърля дубликати (RequiresDuplicateDetection=true)
            MessageId = message.RegistrationId.ToString(),
            CorrelationId = message.RegistrationId.ToString(),
            ContentType = "application/json",
            Subject = nameof(ExamRegistrationRequested)
        };
        sbMessage.ApplicationProperties["examId"] = message.ExamId;
        sbMessage.ApplicationProperties["facultyNumber"] = message.FacultyNumber;

        await _sender.SendMessageAsync(sbMessage, ct);

        logger.LogInformation("Sent registration {RegistrationId} for exam {ExamId} to queue {Queue}",
            message.RegistrationId, message.ExamId, ServiceBusNames.RegistrationsQueue);
    }
}
