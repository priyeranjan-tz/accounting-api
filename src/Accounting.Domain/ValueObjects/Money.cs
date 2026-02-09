namespace Accounting.Domain.ValueObjects;

/// <summary>
/// Represents a monetary amount with fixed-point decimal precision (19,4).
/// Immutable value object that ensures accurate financial calculations without floating-point errors.
/// </summary>
public readonly record struct Money
{
    private const int DecimalPlaces = 4; // Matches PostgreSQL NUMERIC(19,4)

    /// <summary>
    /// The underlying decimal value, rounded to 4 decimal places.
    /// </summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// Creates a new Money instance with the specified amount.
    /// </summary>
    /// <param name="amount">The monetary amount.</param>
    public Money(decimal amount)
    {
        Amount = decimal.Round(amount, DecimalPlaces, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Gets a Money instance representing zero.
    /// </summary>
    public static Money Zero => new(0m);

    /// <summary>
    /// Creates a Money instance from a dollar amount.
    /// </summary>
    /// <param name="dollars">The amount in dollars.</param>
    /// <returns>A new Money instance.</returns>
    public static Money FromDollars(decimal dollars) => new(dollars);

    /// <summary>
    /// Gets a value indicating whether this amount is positive (greater than zero).
    /// </summary>
    public bool IsPositive => Amount > 0;

    /// <summary>
    /// Gets a value indicating whether this amount is negative (less than zero).
    /// </summary>
    public bool IsNegative => Amount < 0;

    /// <summary>
    /// Gets a value indicating whether this amount is zero.
    /// </summary>
    public bool IsZero => Amount == 0;

    /// <summary>
    /// Returns the absolute value of this Money instance.
    /// </summary>
    /// <returns>A new Money instance with the absolute value.</returns>
    public Money Abs() => new(Math.Abs(Amount));

    /// <summary>
    /// Adds two Money values.
    /// </summary>
    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);

    /// <summary>
    /// Subtracts one Money value from another.
    /// </summary>
    public static Money operator -(Money a, Money b) => new(a.Amount - b.Amount);

    /// <summary>
    /// Negates a Money value.
    /// </summary>
    public static Money operator -(Money a) => new(-a.Amount);

    /// <summary>
    /// Multiplies a Money value by a scalar.
    /// </summary>
    public static Money operator *(Money a, decimal scalar) => new(a.Amount * scalar);

    /// <summary>
    /// Multiplies a scalar by a Money value.
    /// </summary>
    public static Money operator *(decimal scalar, Money a) => new(scalar * a.Amount);

    /// <summary>
    /// Divides a Money value by a scalar.
    /// </summary>
    public static Money operator /(Money a, decimal divisor)
    {
        if (divisor == 0)
        {
            throw new DivideByZeroException("Cannot divide money by zero.");
        }

        return new Money(a.Amount / divisor);
    }

    /// <summary>
    /// Compares two Money values for greater than.
    /// </summary>
    public static bool operator >(Money a, Money b) => a.Amount > b.Amount;

    /// <summary>
    /// Compares two Money values for less than.
    /// </summary>
    public static bool operator <(Money a, Money b) => a.Amount < b.Amount;

    /// <summary>
    /// Compares two Money values for greater than or equal.
    /// </summary>
    public static bool operator >=(Money a, Money b) => a.Amount >= b.Amount;

    /// <summary>
    /// Compares two Money values for less than or equal.
    /// </summary>
    public static bool operator <=(Money a, Money b) => a.Amount <= b.Amount;

    /// <summary>
    /// Returns the string representation of this Money instance.
    /// </summary>
    /// <returns>A formatted string showing the amount with 2 decimal places.</returns>
    public override string ToString() => $"${Amount:F2}";
}
