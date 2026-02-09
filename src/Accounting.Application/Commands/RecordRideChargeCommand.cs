using Accounting.Application.Interfaces;
using Accounting.Application.Common;
using Accounting.Domain.ValueObjects;

namespace Accounting.Application.Commands;

/// <summary>
/// Command to record a ride charge to an account using double-entry accounting.
/// Creates two ledger entries: Debit AR, Credit Revenue.
/// </summary>
public record RecordRideChargeCommand : ICommand<Result<RecordRideChargeResult>>
{
    /// <summary>
    /// Account to charge for the ride.
    /// </summary>
    public required Guid AccountId { get; init; }

    /// <summary>
    /// Unique ride identifier (external reference from rides service).
    /// Used for idempotency - prevents duplicate charges for the same ride.
    /// 
    /// IMPORTANT: Must be a unique identifier (e.g., UUID/GUID), NOT a description.
    /// ✅ Correct: "550e8400-e29b-41d4-a716-446655440000" or "RIDE-2024-12345"
    /// ❌ Wrong: "home", "office to work", "trip 1"
    /// 
    /// Use the Description field for human-readable text.
    /// </summary>
    public required string RideId { get; init; }

    /// <summary>
    /// Fare amount in USD (must be > 0).
    /// </summary>
    public required decimal FareAmount { get; init; }

    /// <summary>
    /// Date the ride occurred (UTC).
    /// </summary>
    public required DateTime ServiceDate { get; init; }

    /// <summary>
    /// Optional fleet identifier (metadata).
    /// </summary>
    public string? FleetId { get; init; }

    /// <summary>
    /// Optional human-readable description.
    /// </summary>
    public string? Description { get; init; }
}

/// <summary>
/// Result of recording a ride charge.
/// </summary>
public record RecordRideChargeResult
{
    /// <summary>
    /// Unique transaction identifier (not individual ledger entry IDs).
    /// </summary>
    public required Guid TransactionId { get; init; }

    /// <summary>
    /// Account that was charged.
    /// </summary>
    public required Guid AccountId { get; init; }

    /// <summary>
    /// Ledger entries created (always 2 for double-entry).
    /// </summary>
    public required List<LedgerEntryDto> Entries { get; init; } = new();

    /// <summary>
    /// New account balance after this charge.
    /// </summary>
    public required decimal NewBalance { get; init; }

    /// <summary>
    /// UTC timestamp when transaction was recorded.
    /// </summary>
    public required DateTime CreatedAt { get; init; }
}

/// <summary>
/// DTO representing a single ledger entry for API responses.
/// </summary>
public record LedgerEntryDto
{
    public required Guid Id { get; init; }
    public required string LedgerAccount { get; init; }
    public required decimal DebitAmount { get; init; }
    public required decimal CreditAmount { get; init; }
}
