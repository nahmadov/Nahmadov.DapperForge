using Nahmadov.DapperForge.Core.Abstractions;
using Nahmadov.DapperForge.Oracle;
using Nahmadov.DapperForge.SqlServer;

using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Bulk;

public class BulkSqlDialectTests
{
    #region SQL Server Bulk Insert Tests

    [Fact]
    public void SqlServer_BuildBulkInsert_SingleRow_GeneratesCorrectSql()
    {
        var dialect = SqlServerDialect.Instance;
        var columns = new List<string> { "Name", "Email" };

        var sql = dialect.BuildBulkInsert(
            "[dbo].[Users]",
            columns,
            rowCount: 1,
            (rowIndex, columnName) => $"{columnName}{rowIndex}");

        Assert.Contains("INSERT INTO [dbo].[Users]", sql);
        Assert.Contains("([Name], [Email])", sql);
        Assert.Contains("VALUES (@Name0, @Email0)", sql);
    }

    [Fact]
    public void SqlServer_BuildBulkInsert_MultipleRows_GeneratesCorrectSql()
    {
        var dialect = SqlServerDialect.Instance;
        var columns = new List<string> { "Name", "Email" };

        var sql = dialect.BuildBulkInsert(
            "[dbo].[Users]",
            columns,
            rowCount: 3,
            (rowIndex, columnName) => $"{columnName}{rowIndex}");

        Assert.Contains("INSERT INTO [dbo].[Users]", sql);
        Assert.Contains("([Name], [Email])", sql);
        Assert.Contains("(@Name0, @Email0)", sql);
        Assert.Contains("(@Name1, @Email1)", sql);
        Assert.Contains("(@Name2, @Email2)", sql);
    }

    [Fact]
    public void SqlServer_BuildBulkInsert_ZeroRows_ReturnsEmpty()
    {
        var dialect = SqlServerDialect.Instance;
        var columns = new List<string> { "Name" };

        var sql = dialect.BuildBulkInsert("[dbo].[Users]", columns, rowCount: 0, (i, c) => $"{c}{i}");

        Assert.Equal(string.Empty, sql);
    }

    [Fact]
    public void SqlServer_MaxParametersPerStatement_Returns2100()
    {
        var dialect = SqlServerDialect.Instance;

        Assert.Equal(2100, dialect.MaxParametersPerStatement);
    }

    #endregion

    #region SQL Server Merge Tests

    [Fact]
    public void SqlServer_BuildMerge_InsertOrUpdate_GeneratesCorrectSql()
    {
        var dialect = SqlServerDialect.Instance;
        var keyColumns = new List<string> { "Id" };
        var insertColumns = new List<string> { "Id", "Name", "Email" };
        var updateColumns = new List<string> { "Name", "Email" };

        var sql = dialect.BuildMerge(
            "[dbo].[Users]",
            keyColumns,
            insertColumns,
            updateColumns,
            rowCount: 2,
            (rowIndex, columnName) => $"{columnName}{rowIndex}",
            MergeMode.InsertOrUpdate);

        Assert.Contains("MERGE INTO [dbo].[Users] AS target", sql);
        Assert.Contains("USING (VALUES", sql);
        Assert.Contains("ON target.[Id] = source.[Id]", sql);
        Assert.Contains("WHEN NOT MATCHED THEN", sql);
        Assert.Contains("INSERT ([Id], [Name], [Email])", sql);
        Assert.Contains("WHEN MATCHED THEN", sql);
        Assert.Contains("UPDATE SET target.[Name] = source.[Name]", sql);
    }

    [Fact]
    public void SqlServer_BuildMerge_InsertOnly_OmitsUpdateClause()
    {
        var dialect = SqlServerDialect.Instance;
        var keyColumns = new List<string> { "Id" };
        var insertColumns = new List<string> { "Id", "Name" };
        var updateColumns = new List<string> { "Name" };

        var sql = dialect.BuildMerge(
            "[dbo].[Users]",
            keyColumns,
            insertColumns,
            updateColumns,
            rowCount: 1,
            (rowIndex, columnName) => $"{columnName}{rowIndex}",
            MergeMode.InsertOnly);

        Assert.Contains("WHEN NOT MATCHED THEN", sql);
        Assert.DoesNotContain("WHEN MATCHED THEN", sql);
    }

    [Fact]
    public void SqlServer_BuildMerge_UpdateOnly_OmitsInsertClause()
    {
        var dialect = SqlServerDialect.Instance;
        var keyColumns = new List<string> { "Id" };
        var insertColumns = new List<string> { "Id", "Name" };
        var updateColumns = new List<string> { "Name" };

        var sql = dialect.BuildMerge(
            "[dbo].[Users]",
            keyColumns,
            insertColumns,
            updateColumns,
            rowCount: 1,
            (rowIndex, columnName) => $"{columnName}{rowIndex}",
            MergeMode.UpdateOnly);

        Assert.DoesNotContain("WHEN NOT MATCHED THEN", sql);
        Assert.Contains("WHEN MATCHED THEN", sql);
    }

    [Fact]
    public void SqlServer_BuildMerge_CompositeKey_GeneratesCorrectOnClause()
    {
        var dialect = SqlServerDialect.Instance;
        var keyColumns = new List<string> { "TenantId", "Code" };
        var insertColumns = new List<string> { "TenantId", "Code", "Name" };
        var updateColumns = new List<string> { "Name" };

        var sql = dialect.BuildMerge(
            "[dbo].[Products]",
            keyColumns,
            insertColumns,
            updateColumns,
            rowCount: 1,
            (rowIndex, columnName) => $"{columnName}{rowIndex}",
            MergeMode.InsertOrUpdate);

        Assert.Contains("ON target.[TenantId] = source.[TenantId] AND target.[Code] = source.[Code]", sql);
    }

    #endregion

    #region Oracle Bulk Insert Tests

    [Fact]
    public void Oracle_BuildBulkInsert_SingleRow_GeneratesInsertAll()
    {
        var dialect = OracleDialect.Instance;
        var columns = new List<string> { "Name", "Email" };

        var sql = dialect.BuildBulkInsert(
            "\"Users\"",
            columns,
            rowCount: 1,
            (rowIndex, columnName) => $"{columnName}{rowIndex}");

        Assert.Contains("INSERT ALL", sql);
        Assert.Contains("INTO \"Users\" (\"Name\", \"Email\") VALUES (:Name0, :Email0)", sql);
        Assert.Contains("SELECT 1 FROM DUAL", sql);
    }

    [Fact]
    public void Oracle_BuildBulkInsert_MultipleRows_GeneratesMultipleInto()
    {
        var dialect = OracleDialect.Instance;
        var columns = new List<string> { "Name", "Email" };

        var sql = dialect.BuildBulkInsert(
            "\"Users\"",
            columns,
            rowCount: 3,
            (rowIndex, columnName) => $"{columnName}{rowIndex}");

        Assert.Contains("INSERT ALL", sql);
        Assert.Contains("INTO \"Users\" (\"Name\", \"Email\") VALUES (:Name0, :Email0)", sql);
        Assert.Contains("INTO \"Users\" (\"Name\", \"Email\") VALUES (:Name1, :Email1)", sql);
        Assert.Contains("INTO \"Users\" (\"Name\", \"Email\") VALUES (:Name2, :Email2)", sql);
        Assert.Contains("SELECT 1 FROM DUAL", sql);
    }

    [Fact]
    public void Oracle_MaxParametersPerStatement_Returns32767()
    {
        var dialect = OracleDialect.Instance;

        Assert.Equal(32767, dialect.MaxParametersPerStatement);
    }

    #endregion

    #region Oracle Merge Tests

    [Fact]
    public void Oracle_BuildMerge_InsertOrUpdate_GeneratesCorrectSql()
    {
        var dialect = OracleDialect.Instance;
        var keyColumns = new List<string> { "Id" };
        var insertColumns = new List<string> { "Id", "Name", "Email" };
        var updateColumns = new List<string> { "Name", "Email" };

        var sql = dialect.BuildMerge(
            "\"Users\"",
            keyColumns,
            insertColumns,
            updateColumns,
            rowCount: 2,
            (rowIndex, columnName) => $"{columnName}{rowIndex}",
            MergeMode.InsertOrUpdate);

        Assert.Contains("MERGE INTO \"Users\" target", sql);
        Assert.Contains("USING (", sql);
        Assert.Contains("SELECT :Id0 AS \"Id\", :Name0 AS \"Name\", :Email0 AS \"Email\" FROM DUAL", sql);
        Assert.Contains("UNION ALL", sql);
        Assert.Contains("SELECT :Id1 AS \"Id\", :Name1 AS \"Name\", :Email1 AS \"Email\" FROM DUAL", sql);
        Assert.Contains("ON (target.\"Id\" = source.\"Id\")", sql);
        Assert.Contains("WHEN NOT MATCHED THEN", sql);
        Assert.Contains("WHEN MATCHED THEN", sql);
    }

    [Fact]
    public void Oracle_BuildMerge_InsertOnly_OmitsUpdateClause()
    {
        var dialect = OracleDialect.Instance;
        var keyColumns = new List<string> { "Id" };
        var insertColumns = new List<string> { "Id", "Name" };
        var updateColumns = new List<string> { "Name" };

        var sql = dialect.BuildMerge(
            "\"Users\"",
            keyColumns,
            insertColumns,
            updateColumns,
            rowCount: 1,
            (rowIndex, columnName) => $"{columnName}{rowIndex}",
            MergeMode.InsertOnly);

        Assert.Contains("WHEN NOT MATCHED THEN", sql);
        Assert.DoesNotContain("WHEN MATCHED THEN", sql);
    }

    [Fact]
    public void Oracle_BuildMerge_UpdateOnly_OmitsInsertClause()
    {
        var dialect = OracleDialect.Instance;
        var keyColumns = new List<string> { "Id" };
        var insertColumns = new List<string> { "Id", "Name" };
        var updateColumns = new List<string> { "Name" };

        var sql = dialect.BuildMerge(
            "\"Users\"",
            keyColumns,
            insertColumns,
            updateColumns,
            rowCount: 1,
            (rowIndex, columnName) => $"{columnName}{rowIndex}",
            MergeMode.UpdateOnly);

        Assert.DoesNotContain("WHEN NOT MATCHED THEN", sql);
        Assert.Contains("WHEN MATCHED THEN", sql);
    }

    #endregion
}
