using System.Data;

using Nahmadov.DapperForge.Core.Context.Connection;
using Nahmadov.DapperForge.UnitTests.Fakes;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Context;

/// <summary>
/// Tests for TransactionScope to verify proper transaction lifecycle management.
/// </summary>
public class TransactionScopeTests
{
    [Fact]
    public void TransactionScope_Should_Begin_Transaction_On_Creation()
    {
        // Arrange
        var connection = new FakeDbConnection();
        connection.Open();

        // Act
        var scope = new TransactionScope(
            connection,
            IsolationLevel.ReadCommitted,
            _ => { },
            (_, _, _) => { },
            () => { });

        // Assert
        Assert.NotNull(scope.Transaction);
        Assert.Equal(IsolationLevel.ReadCommitted, scope.IsolationLevel);
        Assert.False(scope.IsCompleted);
    }

    [Fact]
    public void TransactionScope_Should_Commit_When_Complete_And_Disposed()
    {
        // Arrange
        var connection = new FakeDbConnection();
        connection.Open();
        var committed = false;
        var rolledBack = false;

        var transaction = new FakeDbTransaction(connection);
        transaction.OnCommit = () => committed = true;
        transaction.OnRollback = () => rolledBack = true;

        var scope = CreateScope(connection, transaction);

        // Act
        scope.Complete();
        scope.Dispose();

        // Assert
        Assert.True(scope.IsCompleted);
        Assert.True(committed, "Transaction should be committed");
        Assert.False(rolledBack, "Transaction should not be rolled back");
    }

    [Fact]
    public void TransactionScope_Should_Rollback_When_Not_Complete_And_Disposed()
    {
        // Arrange
        var connection = new FakeDbConnection();
        connection.Open();
        var committed = false;
        var rolledBack = false;

        var transaction = new FakeDbTransaction(connection);
        transaction.OnCommit = () => committed = true;
        transaction.OnRollback = () => rolledBack = true;

        var scope = CreateScope(connection, transaction);

        // Act
        // Do NOT call Complete()
        scope.Dispose();

        // Assert
        Assert.False(scope.IsCompleted);
        Assert.False(committed, "Transaction should not be committed");
        Assert.True(rolledBack, "Transaction should be rolled back");
    }

    [Fact]
    public void TransactionScope_Should_Support_Manual_Commit()
    {
        // Arrange
        var connection = new FakeDbConnection();
        connection.Open();
        var committed = false;

        var transaction = new FakeDbTransaction(connection);
        transaction.OnCommit = () => committed = true;

        var scope = CreateScope(connection, transaction);

        // Act
        scope.Commit();

        // Assert
        Assert.True(committed);
        Assert.True(scope.IsCompleted);
    }

    [Fact]
    public void TransactionScope_Should_Support_Manual_Rollback()
    {
        // Arrange
        var connection = new FakeDbConnection();
        connection.Open();
        var rolledBack = false;

        var transaction = new FakeDbTransaction(connection);
        transaction.OnRollback = () => rolledBack = true;

        var scope = CreateScope(connection, transaction);

        // Act
        scope.Rollback();

        // Assert
        Assert.True(rolledBack);
        Assert.False(scope.IsCompleted);
    }

    [Fact]
    public void TransactionScope_Should_Throw_When_Commit_After_Rollback()
    {
        // Arrange
        var connection = new FakeDbConnection();
        connection.Open();

        var scope = new TransactionScope(
            connection,
            IsolationLevel.ReadCommitted,
            _ => { },
            (_, _, _) => { },
            () => { });

        scope.Rollback();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => scope.Commit());
    }

    [Fact]
    public void TransactionScope_Should_Throw_When_Complete_After_Commit()
    {
        // Arrange
        var connection = new FakeDbConnection();
        connection.Open();

        var scope = new TransactionScope(
            connection,
            IsolationLevel.ReadCommitted,
            _ => { },
            (_, _, _) => { },
            () => { });

        scope.Commit();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => scope.Complete());
    }

    [Fact]
    public void TransactionScope_Should_Handle_Rollback_Failure_Gracefully()
    {
        // Arrange
        var connection = new FakeDbConnection();
        connection.Open();

        var transaction = new FakeDbTransaction(connection);
        transaction.OnRollback = () => throw new InvalidOperationException("Rollback failed");

        var errorLogged = false;
        var scope = new TransactionScope(
            connection,
            IsolationLevel.ReadCommitted,
            _ => { },
            (_, _, _) => { errorLogged = true; },
            () => { });

        // Replace internal transaction with our fake that throws on rollback
        typeof(TransactionScope)
            .GetField("_transaction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(scope, transaction);

        // Act - should not throw
        scope.Dispose();

        // Assert
        Assert.True(errorLogged, "Error should be logged");
        // Connection should be closed to cleanup orphaned transaction
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public void TransactionScope_Should_Throw_When_Accessed_After_Disposal()
    {
        // Arrange
        var connection = new FakeDbConnection();
        connection.Open();

        var scope = new TransactionScope(
            connection,
            IsolationLevel.ReadCommitted,
            _ => { },
            (_, _, _) => { },
            () => { });

        scope.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => scope.Transaction);
        Assert.Throws<ObjectDisposedException>(() => scope.Complete());
        Assert.Throws<ObjectDisposedException>(() => scope.Commit());
        Assert.Throws<ObjectDisposedException>(() => scope.Rollback());
    }

    [Fact]
    public void TransactionScope_Multiple_Dispose_Should_Be_Safe()
    {
        // Arrange
        var connection = new FakeDbConnection();
        connection.Open();

        var scope = new TransactionScope(
            connection,
            IsolationLevel.ReadCommitted,
            _ => { },
            (_, _, _) => { },
            () => { });

        // Act & Assert (should not throw)
        scope.Dispose();
        scope.Dispose();
        scope.Dispose();
    }

    [Fact]
    public void TransactionScope_Should_Call_Unregister_Callback_On_Disposal()
    {
        // Arrange
        var connection = new FakeDbConnection();
        connection.Open();
        var unregisterCalled = false;

        var scope = new TransactionScope(
            connection,
            IsolationLevel.ReadCommitted,
            _ => { },
            (_, _, _) => { },
            () => { unregisterCalled = true; });

        // Act
        scope.Dispose();

        // Assert
        Assert.True(unregisterCalled, "Unregister callback should be called");
    }

    [Fact]
    public void TransactionScope_Should_Support_Different_Isolation_Levels()
    {
        // Arrange
        var connection = new FakeDbConnection();
        connection.Open();

        // Act & Assert
        var scope1 = new TransactionScope(
            connection,
            IsolationLevel.Serializable,
            _ => { },
            (_, _, _) => { },
            () => { });
        Assert.Equal(IsolationLevel.Serializable, scope1.IsolationLevel);
        scope1.Dispose();

        var scope2 = new TransactionScope(
            connection,
            IsolationLevel.RepeatableRead,
            _ => { },
            (_, _, _) => { },
            () => { });
        Assert.Equal(IsolationLevel.RepeatableRead, scope2.IsolationLevel);
        scope2.Dispose();
    }

    // Helper method to create scope with custom transaction
    private static TransactionScope CreateScope(
        FakeDbConnection connection,
        FakeDbTransaction transaction)
    {
        var scope = new TransactionScope(
            connection,
            IsolationLevel.ReadCommitted,
            _ => { },
            (_, _, _) => { },
            () => { });

        // Replace internal transaction using reflection
        typeof(TransactionScope)
            .GetField("_transaction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.SetValue(scope, transaction);

        return scope;
    }
}
