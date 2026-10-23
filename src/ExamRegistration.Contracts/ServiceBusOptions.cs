using System.ComponentModel.DataAnnotations;

namespace ExamRegistration.Contracts;

/// <summary>Настройки за връзка с Service Bus (секция "ServiceBus" в конфигурацията).</summary>
public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    /// <summary>SAS connection string. За емулатора съдържа UseDevelopmentEmulator=true.</summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
