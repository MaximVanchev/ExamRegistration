using ExamRegistration.Contracts;

namespace ExamRegistration.Processor;

/// <summary>
/// Бизнес правила за записване: изпитът съществува, срокът не е изтекъл,
/// има свободни места и студентът не е записан вече. Пази заетите места в паметта.
/// </summary>
public sealed class RegistrationValidator(TimeProvider clock)
{
    private readonly Dictionary<string, HashSet<string>> _registered = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public (RegistrationStatus Status, string? Reason) TryRegister(ExamRegistrationRequested request)
    {
        var exam = ExamCatalog.Find(request.ExamId);
        if (exam is null)
            return (RegistrationStatus.Rejected, $"Изпит {request.ExamId} не съществува.");

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        if (today > exam.RegistrationDeadline)
            return (RegistrationStatus.Rejected, $"Срокът за записване изтече на {exam.RegistrationDeadline:dd.MM.yyyy}.");

        lock (_gate)
        {
            if (!_registered.TryGetValue(exam.Id, out var students))
                _registered[exam.Id] = students = new HashSet<string>();

            if (students.Contains(request.FacultyNumber))
                return (RegistrationStatus.Rejected, "Студентът вече е записан за този изпит.");

            if (students.Count >= exam.Capacity)
                return (RegistrationStatus.Rejected, $"Няма свободни места (капацитет {exam.Capacity}).");

            students.Add(request.FacultyNumber);
            return (RegistrationStatus.Accepted, null);
        }
    }
}
