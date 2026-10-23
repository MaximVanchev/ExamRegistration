namespace ExamRegistration.Contracts;

/// <summary>Имена на опашки, topic-и и абонаменти (трябва да съвпадат с emulator/Config.json).</summary>
public static class ServiceBusNames
{
    public const string RegistrationsQueue = "exam-registrations";
    public const string RegistrationEventsTopic = "registration-events";
    public const string NotificationsSubscription = "notifications";
    public const string AuditSubscription = "audit";
}
