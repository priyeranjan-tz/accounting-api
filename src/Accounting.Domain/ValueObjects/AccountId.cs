namespace Accounting.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for Account aggregate root.
/// Prevents mixing Account IDs with other GUIDs (type safety).
/// </summary>
public readonly record struct AccountId
{
    /// <summary>
    /// The underlying unique identifier.
    /// </summary>
    public Guid Value { get; init; }

    /// <summary>
    /// Creates a new AccountId with the specified GUID value.
    /// </summary>
    /// <param name="value">The GUID value for the account ID.</param>
    public AccountId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Account ID cannot be empty.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Creates a new AccountId with a new GUID.
    /// </summary>
    /// <returns>A new AccountId instance with a generated GUID.</returns>
    public static AccountId NewId() => new(Guid.NewGuid());

    /// <summary>
    /// Creates an AccountId from a string representation of a GUID.
    /// </summary>
    /// <param name="value">The string representation of a GUID.</param>
    /// <returns>A new AccountId instance.</returns>
    public static AccountId FromString(string value)
    {
        if (!Guid.TryParse(value, out var guid))
        {
            throw new ArgumentException($"Invalid Account ID format: {value}", nameof(value));
        }

        return new AccountId(guid);
    }

    /// <summary>
    /// Returns the string representation of this AccountId.
    /// </summary>
    /// <returns>The GUID as a string.</returns>
    public override string ToString() => Value.ToString();

    /// <summary>
    /// Implicitly converts an AccountId to a Guid.
    /// </summary>
    public static implicit operator Guid(AccountId accountId) => accountId.Value;

    /// <summary>
    /// Explicitly converts a Guid to an AccountId.
    /// </summary>
    public static explicit operator AccountId(Guid guid) => new(guid);
}
