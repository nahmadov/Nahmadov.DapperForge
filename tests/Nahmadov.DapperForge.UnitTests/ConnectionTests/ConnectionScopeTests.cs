using System.Data;

using Nahmadov.DapperForge.Core.Context.Connection;
using Nahmadov.DapperForge.Core.Exceptions;
using Nahmadov.DapperForge.UnitTests.Fakes;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.ConnectionTests;

/// <summary>
/// Tests for ConnectionScope to verify proper connection lifecycle management.
/// </summary>
public class ConnectionScopeTests
{
    [Fact]
    public void ConnectionScope_Should_Create_Connection_On_First_Access()
    {
        // Arrange
        var connectionCreated = false;
        Func<IDbConnection> factory = () =>
        {
            connectionCreated = true;
            return new FakeDbConnection();
        };

        var scope = new ConnectionScope(factory, _ => { }, (_, _, _) => { });

        // Act
        Assert.False(connectionCreated, "connection should not be created until accessed");

        var connection = scope.Connection;

        // Assert
        Assert.True(connectionCreated, "connection should be created on first access");
        Assert.NotNull(connection);
        Assert.Equal(ConnectionState.Open, connection.State);
    }

    [Fact]
    public void ConnectionScope_Should_Return_Same_Connection_On_Multiple_Access()
    {
        // Arrange
        var scope = new ConnectionScope(
            () => new FakeDbConnection(),
            _ => { },
            (_, _, _) => { });

        // Act
        var connection1 = scope.Connection;
        var connection2 = scope.Connection;

        // Assert
        Assert.Same(connection1, connection2);
    }

    [Fact]
    public void ConnectionScope_Should_Close_Connection_On_Disposal()
    {
        // Arrange
        var fakeConnection = new FakeDbConnection();
        var scope = new ConnectionScope(
            () => fakeConnection,
            _ => { },
            (_, _, _) => { });

        // Access connection to trigger creation
        var connection = scope.Connection;
        Assert.Equal(ConnectionState.Open, connection.State);

        // Act
        scope.Dispose();

        // Assert
        Assert.Equal(ConnectionState.Closed, fakeConnection.State);
    }

    [Fact]
    public void ConnectionScope_Should_Throw_When_Accessed_After_Disposal()
    {
        // Arrange
        var scope = new ConnectionScope(
            () => new FakeDbConnection(),
            _ => { },
            (_, _, _) => { });

        scope.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => scope.Connection);
    }

    [Fact]
    public void ConnectionScope_Should_Handle_Broken_Connection()
    {
        // Arrange
        var connectionFactory = new FakeDbConnection();
        var callCount = 0;

        var scope = new ConnectionScope(
            () =>
            {
                callCount++;
                return callCount == 1 ? connectionFactory : new FakeDbConnection();
            },
            _ => { },
            (_, _, _) => { });

        // Act
        var connection1 = scope.Connection;
        Assert.Equal(ConnectionState.Open, connection1.State);

        // Simulate connection breaking
        connectionFactory.SimulateConnectionBreak();

        // Access again - should detect broken state and recreate
        var connection2 = scope.Connection;

        // Assert
        Assert.Equal(2, callCount);
        Assert.NotSame(connection1, connection2);
        Assert.Equal(ConnectionState.Open, connection2.State);
    }

    [Fact]
    public void ConnectionScope_Should_Warn_When_Disposing_With_Active_Transaction()
    {
        // Arrange
        var logMessages = new List<string>();
        var errorLogged = false;

        var scope = new ConnectionScope(
            () => new FakeDbConnection(),
            msg => logMessages.Add(msg),
            (_, _, msg) =>
            {
                errorLogged = true;
                logMessages.Add(msg);
            });

        var connection = scope.Connection;
        var transaction = connection.BeginTransaction();

        // Use reflection to set transaction (internal method)
        scope.GetType()
            .GetMethod("SetTransaction", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(scope, new[] { transaction });

        // Act
        scope.Dispose();

        // Assert
        Assert.True(errorLogged, "should log error when disposing with active transaction");
        Assert.Contains(logMessages, msg => msg.Contains("active transaction"));
    }

    [Fact]
    public void ConnectionScope_Should_Handle_Null_ConnectionFactory()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ConnectionScope(null!, _ => { }, (_, _, _) => { }));
    }

    [Fact]
    public void ConnectionScope_Should_Throw_When_Factory_Returns_Null()
    {
        // Arrange
        var scope = new ConnectionScope(
            () => null!,
            _ => { },
            (_, _, _) => { });

        // Act & Assert
        var ex = Assert.Throws<DapperConnectionException>(() => scope.Connection);
        Assert.Contains("ConnectionFactory returned null", ex.Message);
    }

    [Fact]
    public void ConnectionScope_Should_Handle_Connection_Opening_Failure()
    {
        // Arrange
        var fakeConnection = new FakeDbConnection();
        fakeConnection.SimulateOpenFailure(new InvalidOperationException("Simulated open failure"));

        var scope = new ConnectionScope(
            () => fakeConnection,
            _ => { },
            (_, _, _) => { });

        // Act & Assert
        var ex = Assert.Throws<DapperConnectionException>(() => scope.Connection);
        Assert.Contains("Failed to establish database connection", ex.Message);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    [Fact]
    public void ConnectionScope_HasActiveTransaction_Should_Return_False_Initially()
    {
        // Arrange
        var scope = new ConnectionScope(
            () => new FakeDbConnection(),
            _ => { },
            (_, _, _) => { });

        // Act & Assert
        Assert.False(scope.HasActiveTransaction);
        Assert.Null(scope.ActiveTransaction);
    }

    [Fact]
    public void ConnectionScope_Multiple_Dispose_Calls_Should_Be_Safe()
    {
        // Arrange
        var scope = new ConnectionScope(
            () => new FakeDbConnection(),
            _ => { },
            (_, _, _) => { });

        var connection = scope.Connection;

        // Act & Assert (should not throw)
        scope.Dispose();
        scope.Dispose();
        scope.Dispose();
    }
}
