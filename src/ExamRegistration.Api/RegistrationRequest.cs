using System.Net.Mail;
using ExamRegistration.Contracts;

namespace ExamRegistration.Api;

public sealed record RegistrationRequest(string FacultyNumber, string StudentName, string StudentEmail, string ExamId)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(FacultyNumber) || FacultyNumber.Length != 9 || !FacultyNumber.All(char.IsDigit))
            errors[nameof(FacultyNumber)] = ["Факултетният номер трябва да е 9 цифри."];

        if (string.IsNullOrWhiteSpace(StudentName))
            errors[nameof(StudentName)] = ["Името е задължително."];

        if (string.IsNullOrWhiteSpace(StudentEmail) || !MailAddress.TryCreate(StudentEmail, out _))
            errors[nameof(StudentEmail)] = ["Невалиден имейл адрес."];

        if (string.IsNullOrWhiteSpace(ExamId) || ExamCatalog.Find(ExamId) is null)
            errors[nameof(ExamId)] = ["Няма изпит с такъв идентификатор."];

        return errors;
    }
}
