namespace Accounting.Application.Interfaces;

/// <summary>
/// Marker interface for queries that read data from the system without modifying state.
/// Queries represent read operations (e.g., GetAccount, GetBalance, GetStatement).
/// </summary>
/// <typeparam name="TResult">The type of the result returned by the query.</typeparam>
public interface IQuery<out TResult>
{
}
