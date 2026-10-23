namespace ExamRegistration.Contracts;

public sealed record Exam(string Id, string Subject, DateOnly Date, int Capacity, DateOnly RegistrationDeadline);

/// <summary>Демонстрационен каталог с изпити (in-memory, общ за Api и Processor).</summary>
public static class ExamCatalog
{
    public static readonly IReadOnlyList<Exam> All =
    [
        new("PRS-2026-01", "Програмиране в разпределени среди", new DateOnly(2027, 1, 20), 30, new DateOnly(2027, 1, 15)),
        new("BD-2026-01", "Бази от данни", new DateOnly(2027, 1, 25), 25, new DateOnly(2027, 1, 20)),
        new("OOP-2026-01", "Обектно-ориентирано програмиране", new DateOnly(2027, 2, 2), 2, new DateOnly(2027, 1, 28)),
        new("MATH-2025-09", "Математика 1 (поправка)", new DateOnly(2025, 9, 10), 40, new DateOnly(2025, 9, 1))
    ];

    public static Exam? Find(string examId) =>
        All.FirstOrDefault(e => string.Equals(e.Id, examId, StringComparison.OrdinalIgnoreCase));
}
