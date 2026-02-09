namespace Accounting.Domain.Enums;

/// <summary>
/// Represents the type of account (corporate or individual).
/// </summary>
public enum AccountType
{
    /// <summary>
    /// Corporate or institutional account (e.g., rehab centers, hospitals).
    /// </summary>
    Organization = 1,

    /// <summary>
    /// Personal account (e.g., passengers, guardians).
    /// </summary>
    Individual = 2
}
