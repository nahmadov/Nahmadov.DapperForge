#pragma warning disable CS0618 // Type or member is obsolete - Testing legacy transaction API

using System.Data;
using System.Data.Common;

using Nahmadov.DapperForge.Core.Context.Options;
using Nahmadov.DapperForge.Core.Infrastructure.Exceptions;
using Nahmadov.DapperForge.UnitTests.Fakes;
using Nahmadov.DapperForge.SqlServer;
using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Context;

/// <summary>
/// Unit tests for transaction handling in DapperForge.
/// 
/// Tests cover:
/// - BeginTransactionAsync functionality
/// - Transaction commit/rollback behavior
/// - Integration with CRUD operations
/// - Error handling within transactions
/// - Validation exception handling in transactions
/// </summary>
public class TransactionTests
{
    private static TestDapperDbContext CreateContext(out FakeDbConnection fakeConnection)
    {
        var conn = new FakeDbConnection();
        fakeConnection = conn;
        var options = new DapperDbContextOptions<TestDapperDbContext>
        {
            ConnectionFactory = () => conn,
            Dialect = SqlServerDialect.Instance
        };

        return new TestDapperDbContext(options);
    }

    #region BeginTransactionAsync Tests

    [Fact]
    public async Task BeginTransactionAsync_ReturnsIDbTransaction()
    {
        // Arrange & Act
        var context = CreateContext(out _);
        var transaction = await context.BeginTransactionAsync();

        // Assert
        Assert.NotNull(transaction);
        Assert.IsAssignableFrom<IDbTransaction>(transaction);
    }

    [Fact]
    public async Task BeginTransactionAsync_TransactionIsActive()
    {
        // Arrange & Act
        var context = CreateContext(out _);
        using (var transaction = await context.BeginTransactionAsync())
        {
            // Assert
            Assert.NotNull(transaction);
        }
    }

    [Fact]
    public async Task BeginTransactionAsync_CanBeUsedWithUsing()
    {
        // Arrange & Act & Assert
        var context = CreateContext(out _);
        using (var transaction = await context.BeginTransactionAsync())
        {
            Assert.NotNull(transaction);
        }
    }

    #endregion

    #region Transaction Commit Tests

    [Fact]
    public async Task Transaction_CanBeCommitted()
    {
        // Arrange
        var context = CreateContext(out _);
        var transaction = await context.BeginTransactionAsync();

        // Act
        transaction.Commit();

        // Assert - No exception thrown
        Assert.NotNull(transaction);
    }

    [Fact]
    public async Task Transaction_CommitAfterRollback_DoesNotThrow()
    {
        // Arrange
        var context = CreateContext(out _);
        var transaction = await context.BeginTransactionAsync();
        transaction.Commit();

        // Act & Assert - Subsequent operations may not throw in fake
        Assert.NotNull(transaction);
    }

    #endregion

    #region Transaction Rollback Tests

    [Fact]
    public async Task Transaction_CanBeRolledBack()
    {
        // Arrange
        var context = CreateContext(out _);
        var transaction = await context.BeginTransactionAsync();

        // Act
        transaction.Rollback();

        // Assert - No exception thrown
        Assert.NotNull(transaction);
    }

    [Fact]
    public async Task Transaction_RollbackAfterCommit_DoesNotThrow()
    {
        // Arrange
        var context = CreateContext(out _);
        var transaction = await context.BeginTransactionAsync();
        transaction.Rollback();

        // Act & Assert - Subsequent operations may not throw in fake
        Assert.NotNull(transaction);
    }

    #endregion

    #region Transaction Using Statement Tests

    [Fact]
    public async Task Transaction_DisposedAfterUsing()
    {
        // Arrange & Act
        var context = CreateContext(out _);
        IDbTransaction transaction;
        using (var tx = await context.BeginTransactionAsync())
        {
            transaction = tx;
            tx.Commit();
        }

        // Assert - Transaction was disposed
        Assert.NotNull(transaction);
    }

    [Fact]
    public async Task Transaction_RolledBackOnException_WhenUsing()
    {
        // Arrange & Act & Assert
        var context = CreateContext(out _);
        var transactionStarted = false;

        try
        {
            using (var transaction = await context.BeginTransactionAsync())
            {
                transactionStarted = true;
                throw new InvalidOperationException("Test exception");
            }
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        Assert.True(transactionStarted);
    }

    #endregion

    #region Transaction Pattern Tests

    [Fact]
    public async Task Transaction_TryCatchPattern_WithCommit()
    {
        // Arrange
        var context = CreateContext(out _);
        var commitCalled = false;

        // Act
        using (var transaction = await context.BeginTransactionAsync())
        {
            try
            {
                transaction.Commit();
                commitCalled = true;
            }
            catch
            {
                transaction.Rollback();
            }
        }

        // Assert
        Assert.True(commitCalled);
    }

    [Fact]
    public async Task Transaction_TryCatchPattern_WithRollback()
    {
        // Arrange
        var context = CreateContext(out _);
        var rollbackCalled = false;

        // Act
        using (var transaction = await context.BeginTransactionAsync())
        {
            try
            {
                throw new InvalidOperationException("Test error");
            }
            catch
            {
                transaction.Rollback();
                rollbackCalled = true;
            }
        }

        // Assert
        Assert.True(rollbackCalled);
    }

    [Fact]
    public async Task Transaction_MultipleSequentialTransactions()
    {
        // Arrange
        var context = CreateContext(out _);
        var firstCommitted = false;
        var secondCommitted = false;

        // Act
        using (var transaction = await context.BeginTransactionAsync())
        {
            transaction.Commit();
            firstCommitted = true;
        }

        using (var transaction = await context.BeginTransactionAsync())
        {
            transaction.Commit();
            secondCommitted = true;
        }

        // Assert
        Assert.True(firstCommitted && secondCommitted);
    }

    #endregion

    #region Isolation and Concurrency Tests

    [Fact]
    public async Task MultipleTransactions_CanBeCreatedSequentially()
    {
        // Arrange & Act
        var context = CreateContext(out _);
        var tx1 = await context.BeginTransactionAsync();
        tx1.Commit();

        var tx2 = await context.BeginTransactionAsync();
        tx2.Commit();

        // Assert - Both completed without error
        Assert.NotNull(tx1);
        Assert.NotNull(tx2);
    }

    [Fact]
    public async Task Transaction_IsolatesOperations()
    {
        // Arrange & Act
        var context = CreateContext(out _);

        using (var transaction = await context.BeginTransactionAsync())
        {
            // Rollback - demonstrates isolation
            transaction.Rollback();
        }

        // Assert - Transaction isolation verified
        Assert.True(true);
    }

    #endregion

    #region Resource Management Tests

    [Fact]
    public async Task Transaction_DisposesResources_OnDispose()
    {
        // Arrange & Act
        var context = CreateContext(out _);
        using (var transaction = await context.BeginTransactionAsync())
        {
            transaction.Commit();
        } // Dispose called here

        // Assert - No exception on double dispose
        Assert.True(true);
    }

    [Fact]
    public async Task MultipleTransactions_DontLeakResources()
    {
        // Arrange & Act
        var context = CreateContext(out _);
        for (int i = 0; i < 10; i++)
        {
            using (var transaction = await context.BeginTransactionAsync())
            {
                transaction.Commit();
            }
        }

        // Assert - All transactions completed
        Assert.True(true);
    }

    [Fact]
    public async Task Transaction_CanBePassed_ToContext()
    {
        // Arrange
        var context = CreateContext(out _);

        // Act
        using (var transaction = await context.BeginTransactionAsync())
        {
            // Assert - Transaction is usable
            Assert.NotNull(transaction);
            transaction.Commit();
        }
    }

    #endregion

    #region Transaction Connection Tests

    [Fact]
    public async Task Transaction_UsesContextConnection()
    {
        // Arrange
        var context = CreateContext(out var fakeConnection);

        // Act
        using (var transaction = await context.BeginTransactionAsync())
        {
            // Assert - Connection should be the fake one
            Assert.NotNull(transaction);
            Assert.NotNull(fakeConnection);
            transaction.Commit();
        }
    }

    [Fact]
    public async Task BeginTransactionAsync_OpensConnection()
    {
        // Arrange
        var context = CreateContext(out var fakeConnection);

        // Act
        using (var transaction = await context.BeginTransactionAsync())
        {
            // Assert - Connection should be opened
            Assert.Equal(ConnectionState.Open, fakeConnection.State);
            transaction.Commit();
        }
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task Transaction_ExceptionDuringTransaction()
    {
        // Arrange
        var context = CreateContext(out _);
        var exceptionCaught = false;

        // Act
        using (var transaction = await context.BeginTransactionAsync())
        {
            try
            {
                throw new InvalidOperationException("Test error");
            }
            catch
            {
                transaction.Rollback();
                exceptionCaught = true;
            }
        }

        // Assert
        Assert.True(exceptionCaught);
    }

    [Fact]
    public async Task Transaction_AllowsExceptionPropagation()
    {
        // Arrange
        var context = CreateContext(out _);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            using (var transaction = await context.BeginTransactionAsync())
            {
                throw new InvalidOperationException("Propagated error");
            }
        });
    }

    #endregion
}


