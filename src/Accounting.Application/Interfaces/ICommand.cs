namespace Accounting.Application.Interfaces;

/// <summary>
/// Marker interface for commands that modify state in the system.
/// Commands represent write operations (e.g., CreateAccount, RecordCharge, RecordPayment).
/// </summary>
public interface ICommand
{
}

/// <summary>
/// Marker interface for commands that return a result value.
/// </summary>
/// <typeparam name="TResult">The type of the result returned by the command.</typeparam>
public interface ICommand<out TResult> : ICommand
{
}
