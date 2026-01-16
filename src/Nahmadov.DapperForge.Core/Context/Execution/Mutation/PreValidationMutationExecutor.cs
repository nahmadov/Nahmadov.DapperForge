using System.Data;

using Nahmadov.DapperForge.Core.Exceptions;

namespace Nahmadov.DapperForge.Core.Context.Execution.Mutation;

/// <summary>
/// Executes mutation operations with pre-validation (count check before update/delete).
/// </summary>
internal sealed class PreValidationMutationExecutor<TEntity>(
    DapperDbContext context,
    MutationSqlBuilder<TEntity> sqlBuilder) where TEntity : class
{
    private readonly DapperDbContext _context = context;
    private readonly MutationSqlBuilder<TEntity> _sqlBuilder = sqlBuilder;

    public async Task<int> UpdateWithPreValidationAsync(
        TEntity entity,
        string whereClause,
        Dictionary<string, object?> whereParams,
        int expectedRows,
        IDbTransaction? transaction)
    {
        if (transaction is not null)
        {
            return await ExecuteUpdateWithPreValidationAsync(
                entity, whereClause, whereParams, expectedRows, transaction)
                .ConfigureAwait(false);
        }

        using var txScope = await _context.BeginTransactionScopeAsync().ConfigureAwait(false);

        try
        {
            var affected = await ExecuteUpdateWithPreValidationAsync(
                entity, whereClause, whereParams, expectedRows, txScope.Transaction)
                .ConfigureAwait(false);

            txScope.Complete();
            return affected;
        }
        catch
        {
            throw;
        }
    }

    public async Task<int> DeleteWithPreValidationAsync(
        string whereClause,
        Dictionary<string, object?> whereParams,
        int expectedRows,
        IDbTransaction? transaction)
    {
        if (transaction is not null)
        {
            return await ExecuteDeleteWithPreValidationAsync(
                whereClause, whereParams, expectedRows, transaction)
                .ConfigureAwait(false);
        }

        using var txScope = await _context.BeginTransactionScopeAsync().ConfigureAwait(false);

        try
        {
            var affected = await ExecuteDeleteWithPreValidationAsync(
                whereClause, whereParams, expectedRows, txScope.Transaction)
                .ConfigureAwait(false);

            txScope.Complete();
            return affected;
        }
        catch
        {
            throw;
        }
    }

    private async Task<int> ExecuteUpdateWithPreValidationAsync(
        TEntity entity,
        string whereClause,
        Dictionary<string, object?> whereParams,
        int expectedRows,
        IDbTransaction transaction)
    {
        var countSql = _sqlBuilder.BuildCountSql(whereClause);
        long actualCount;

        try
        {
            actualCount = await _context.QueryFirstOrDefaultAsync<long>(
                countSql, whereParams, transaction).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DapperForgeException)
        {
            throw new DapperExecutionException(OperationType.Update, typeof(TEntity).Name, countSql, ex);
        }

        MutationValidator<TEntity>.ValidateRowCountMismatch(actualCount, expectedRows, OperationType.Update);

        var updateSql = _sqlBuilder.BuildUpdateSqlWithWhere(whereClause);
        var allParams = MutationParameterHelper.MergeParameters(entity, whereParams);

        int affected;
        try
        {
            affected = await _context.ExecuteAsync(updateSql, allParams, transaction).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DapperForgeException)
        {
            throw new DapperExecutionException(OperationType.Update, typeof(TEntity).Name, updateSql, ex);
        }

        return affected;
    }

    private async Task<int> ExecuteDeleteWithPreValidationAsync(
        string whereClause,
        Dictionary<string, object?> whereParams,
        int expectedRows,
        IDbTransaction transaction)
    {
        var countSql = _sqlBuilder.BuildCountSql(whereClause);
        long actualCount;

        try
        {
            actualCount = await _context.QueryFirstOrDefaultAsync<long>(
                countSql, whereParams, transaction).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DapperForgeException)
        {
            throw new DapperExecutionException(OperationType.Delete, typeof(TEntity).Name, countSql, ex);
        }

        MutationValidator<TEntity>.ValidateRowCountMismatch(actualCount, expectedRows, OperationType.Delete);

        var deleteSql = _sqlBuilder.BuildDeleteSql(whereClause);

        int affected;
        try
        {
            affected = await _context.ExecuteAsync(deleteSql, whereParams, transaction).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not DapperForgeException)
        {
            throw new DapperExecutionException(OperationType.Delete, typeof(TEntity).Name, deleteSql, ex);
        }

        return affected;
    }
}
