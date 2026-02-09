namespace Accounting.Domain.Enums;

/// <summary>
/// Represents the status of an account.
/// </summary>
public enum AccountStatus
{
    /// <summary>
    /// Account is active and can receive new transactions.
    /// </summary>
    Active = 1,

    /// <summary>
    /// Account is inactive and cannot receive new transactions.
    /// Historical data is still accessible.
    /// </summary>
    Inactive = 2
}
