using Nahmadov.DapperForge.Core.Infrastructure.Exceptions;
using Nahmadov.DapperForge.Core.Modeling.Mapping;

namespace Nahmadov.DapperForge.Core.Mutations.Execution;
/// <summary>
/// Validates mutation operations and affected row counts.
/// </summary>
internal sealed class MutationValidator<TEntity>(EntityMapping mapping) where TEntity : class
{
    private readonly EntityMapping _mapping = mapping;

    public void EnsureCanInsert()
    {
        if (_mapping.IsReadOnly)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked as ReadOnly and cannot be modified.");
        }
    }

    public void EnsureCanMutate()
    {
        if (_mapping.IsReadOnly)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked as ReadOnly and cannot be modified.");
        }

        if (!_mapping.HasPrimaryKey && !_mapping.HasAlternateKey)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' has no Primary Key or Alternate Key and cannot be updated/deleted.");
        }
    }

    public static void ValidateAffectedRows(int affected, bool allowMultiple, int? expectedRows, OperationType operationType)
    {
        if (expectedRows.HasValue)
        {
            if (affected != expectedRows.Value)
            {
                throw new DapperOperationException(
                    operationType,
                    typeof(TEntity).Name,
                    $"Expected {expectedRows.Value} row(s) to be affected but {affected} row(s) were affected.");
            }
            return;
        }

        if (!allowMultiple && affected != 1)
        {
            if (affected == 0)
            {
                throw new DapperConcurrencyException(operationType, typeof(TEntity).Name);
            }
            else
            {
                throw new DapperOperationException(
                    operationType,
                    typeof(TEntity).Name,
                    $"Expected 1 row to be affected but {affected} rows were affected. " +
                    "Set allowMultiple=true to allow multiple rows to be affected.");
            }
        }
    }

    public static void ValidateRowCountMismatch(long actualCount, int expectedRows, OperationType operationType)
    {
        if (actualCount != expectedRows)
        {
            throw new DapperOperationException(
                operationType,
                typeof(TEntity).Name,
                $"Expected {expectedRows} row(s) to be affected but found {actualCount} matching row(s) before {operationType.ToString().ToLowerInvariant()}.");
        }
    }
}


