namespace ExamRegistration.Contracts;

/// <summary>Съобщение, което Api изпраща в опашката exam-registrations.</summary>
public sealed record ExamRegistrationRequested(
    Guid RegistrationId,
    string FacultyNumber,
    string StudentName,
    string StudentEmail,
    string ExamId,
    DateTimeOffset RequestedAt);

public enum RegistrationStatus
{
    Accepted,
    Rejected
}

/// <summary>Резултат, който Processor публикува в topic registration-events.</summary>
public sealed record ExamRegistrationProcessed(
    Guid RegistrationId,
    string FacultyNumber,
    string StudentEmail,
    string ExamId,
    RegistrationStatus Status,
    string? Reason,
    DateTimeOffset ProcessedAt);
