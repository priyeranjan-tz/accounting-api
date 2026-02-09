namespace Accounting.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for external ride reference (owned by Ride Management service).
/// Clearly distinguishes ride references from internal identifiers and enables idempotency checks.
/// 
/// IMPORTANT: Value must be unique per ride (e.g., UUID), not a description.
/// Each ride should have its own unique identifier.
/// </summary>
public readonly record struct RideId
{
    /// <summary>
    /// The external ride identifier string.
    /// </summary>
    public string Value { get; init; }

    /// <summary>
    /// Creates a new RideId with the specified value.
    /// </summary>
    /// <param name="value">The external ride identifier.</param>
    public RideId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Ride ID cannot be null or whitespace.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// Creates a RideId from a string value.
    /// </summary>
    /// <param name="value">The ride identifier string.</param>
    /// <returns>A new RideId instance.</returns>
    public static RideId FromString(string value) => new(value);

    /// <summary>
    /// Returns the string representation of this RideId.
    /// </summary>
    /// <returns>The ride ID as a string.</returns>
    public override string ToString() => Value;

    /// <summary>
    /// Implicitly converts a RideId to a string.
    /// </summary>
    public static implicit operator string(RideId rideId) => rideId.Value;

    /// <summary>
    /// Explicitly converts a string to a RideId.
    /// </summary>
    public static explicit operator RideId(string value) => new(value);
}
