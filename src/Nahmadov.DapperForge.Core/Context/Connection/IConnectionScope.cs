using System.Data;

namespace Nahmadov.DapperForge.Core.Context.Connection;

/// <summary>
/// Represents a scoped database connection that ensures proper connection lifecycle management.
/// Connections are returned to the pool when the scope is disposed.
/// </summary>
/// <remarks>
/// <para><b>Problem Solved:</b></para>
/// <list type="bullet">
/// <item>Prevents connection pool exhaustion by ensuring connections are properly closed</item>
/// <item>Avoids long-running idle connections in high-traffic scenarios</item>
/// <item>Provides explicit scope-based connection management</item>
/// </list>
/// <para><b>Usage:</b></para>
/// <code>
/// using var scope = context.CreateConnectionScope();
/// var users = await context.QueryAsync&lt;User&gt;("SELECT * FROM Users", connection: scope.Connection);
/// // scope.Dispose() automatically closes and returns connection to pool
/// </code>
/// </remarks>
public interface IConnectionScope : IDisposable
{
    /// <summary>
    /// Gets the database connection for this scope.
    /// The connection is guaranteed to be open and healthy.
    /// </summary>
    IDbConnection Connection { get; }

    /// <summary>
    /// Gets whether this scope has an active transaction.
    /// </summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Gets the active transaction, if any.
    /// </summary>
    IDbTransaction? ActiveTransaction { get; }
}
