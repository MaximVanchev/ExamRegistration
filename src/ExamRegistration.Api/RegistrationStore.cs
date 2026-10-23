using System.Collections.Concurrent;
using ExamRegistration.Contracts;

namespace ExamRegistration.Api;

public sealed record RegistrationEntry(ExamRegistrationRequested Request, string? SubmittedBy);

/// <summary>In-memory списък на подадените заявки (само за демонстрация).</summary>
public sealed class RegistrationStore
{
    private readonly ConcurrentDictionary<Guid, RegistrationEntry> _entries = new();

    public void Add(ExamRegistrationRequested request, string? submittedBy) =>
        _entries[request.RegistrationId] = new RegistrationEntry(request, submittedBy);

    public RegistrationEntry? Get(Guid id) => _entries.GetValueOrDefault(id);

    public IReadOnlyCollection<RegistrationEntry> All() => _entries.Values.ToList();
}
