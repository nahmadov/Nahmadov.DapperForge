using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;

using DapperToolkit.Core.Builders;
using DapperToolkit.Core.Mapping;
using DapperToolkit.Oracle;
using DapperToolkit.SqlServer;
using Xunit;

namespace DapperToolkit.UnitTests.Builders;

public class PredicateVisitorTests
{
    [Fact]
    public void Translates_Boolean_Member_To_TrueLiteral()
    {
        var (sql, parameters) = Translate(u => u.IsActive);

        Assert.Equal("[IsActive] = 1", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Translates_Not_Boolean_Member_To_FalseLiteral()
    {
        var (sql, parameters) = Translate(u => !u.IsActive);

        Assert.Equal("[IsActive] = 0", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Translates_StringContains_To_Like_With_Wildcards()
    {
        var (sql, parameters) = Translate(u => u.Name.Contains("abc"));

        Assert.Equal("[username] LIKE @p0 ESCAPE '\\'", sql);
        Assert.Single(parameters);
        Assert.Equal("%abc%", parameters["p0"]);
    }

    [Fact]
    public void Translates_StringContains_Escapes_Wildcards()
    {
        var (sql, parameters) = Translate(u => u.Name.Contains("a%b_c"));

        Assert.Equal("[username] LIKE @p0 ESCAPE '\\'", sql);
        Assert.Single(parameters);
        Assert.Equal("%a\\%b\\_c%", parameters["p0"]);
    }

    [Fact]
    public void Translates_StartsWith_And_EndsWith()
    {
        var (startsSql, startsParams) = Translate(u => u.Name.StartsWith("abc"));
        Assert.Equal("[username] LIKE @p0 ESCAPE '\\'", startsSql);
        var startsDict = Assert.IsType<Dictionary<string, object>>(startsParams);
        Assert.Equal("abc%", startsDict["p0"]);

        var (endsSql, endsParams) = Translate(u => u.Name.EndsWith("xyz"));
        Assert.Equal("[username] LIKE @p0 ESCAPE '\\'", endsSql);
        var endsDict = Assert.IsType<Dictionary<string, object>>(endsParams);
        Assert.Equal("%xyz", endsDict["p0"]);
    }

    [Fact]
    public void CaseInsensitive_StartsWith_And_EndsWith()
    {
        var mapping = EntityMappingCache<UserEntity>.Mapping;
        var visitor = new PredicateVisitor<UserEntity>(mapping, SqlServerDialect.Instance);

        var (startsSql, startsParams) = visitor.Translate(u => u.Name.StartsWith("Ab"), ignoreCase: true);
        Assert.Equal("LOWER([username]) LIKE @p0 ESCAPE '\\'", startsSql);
        var startsDict = Assert.IsType<Dictionary<string, object>>(startsParams);
        Assert.Equal("ab%", startsDict["p0"]);

        var (endsSql, endsParams) = visitor.Translate(u => u.Name.EndsWith("Cd"), ignoreCase: true);
        Assert.Equal("LOWER([username]) LIKE @p0 ESCAPE '\\'", endsSql);
        var endsDict = Assert.IsType<Dictionary<string, object>>(endsParams);
        Assert.Equal("%cd", endsDict["p0"]);
    }

    [Fact]
    public void CaseInsensitive_Like_Uses_Lower()
    {
        var mapping = EntityMappingCache<UserEntity>.Mapping;
        var visitor = new PredicateVisitor<UserEntity>(mapping, SqlServerDialect.Instance);

        var (sql, parameters) = visitor.Translate(u => u.Name.Contains("ABC"), ignoreCase: true);
        Assert.Equal("LOWER([username]) LIKE @p0 ESCAPE '\\'", sql);
        var dict = Assert.IsType<Dictionary<string, object>>(parameters);
        Assert.Equal("%abc%", dict["p0"]);
    }

    [Fact]
    public void CaseInsensitive_Like_With_Boolean_Combines_Correctly()
    {
        var mapping = EntityMappingCache<UserEntity>.Mapping;
        var visitor = new PredicateVisitor<UserEntity>(mapping, SqlServerDialect.Instance);

        var (sql, parameters) = visitor.Translate(u => u.Name.StartsWith("Ab") && u.IsActive, ignoreCase: true);

        Assert.Equal("(LOWER([username]) LIKE @p0 ESCAPE '\\' AND [IsActive] = 1)", sql);
        var dict = Assert.IsType<Dictionary<string, object>>(parameters);
        Assert.Equal("ab%", dict["p0"]);
    }

    [Fact]
    public void Translates_Comparison_With_Closure_Value()
    {
        var threshold = 10;

        var (sql, parameters) = Translate(u => u.Id > threshold);

        Assert.Equal("([Id] > @p0)", sql);
        Assert.Single(parameters);
        Assert.Equal(threshold, parameters["p0"]);
    }

    [Fact]
    public void Translates_Null_Comparison()
    {
        var (sql, parameters) = Translate(u => u.Name == null);

        Assert.Equal("([username] IS NULL)", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Translates_Empty_String_Equals_To_Parameter()
    {
        var (sql, parameters) = Translate(u => u.Name == string.Empty);

        Assert.Equal("([username] = @p0)", sql);
        Assert.Single(parameters);
        Assert.Equal(string.Empty, parameters["p0"]);
    }

    [Fact]
    public void Oracle_Contains_Uses_Escape_And_Colon_Params()
    {
        var mapping = EntityMappingCache<UserEntity>.Mapping;
        var visitor = new PredicateVisitor<UserEntity>(mapping, OracleDialect.Instance);

        var (sql, parameters) = visitor.Translate(u => u.Name.Contains("a%b"));

        Assert.Equal("LOWER(\"username\") LIKE :p0 ESCAPE '\\'", sql);
        var dict = Assert.IsType<Dictionary<string, object>>(parameters);
        Assert.Single(dict);
        Assert.Equal("%a\\%b%", dict["p0"]);
    }

    [Fact]
    public void Translates_ListContains_To_In_Clause()
    {
        var ids = new[] { 1, 3, 5 };

        var (sql, parameters) = Translate(u => ids.Contains(u.Id));

        Assert.Equal("[Id] IN @p0", sql);
        var dict = Assert.IsType<Dictionary<string, object>>(parameters);
        Assert.True(dict.TryGetValue("p0", out var value));
        var list = Assert.IsAssignableFrom<IEnumerable<int>>(value);
        Assert.Equal(ids, list);
    }

    private static (string Sql, IDictionary<string, object> Parameters) Translate(Expression<Func<UserEntity, bool>> predicate)
    {
        var mapping = EntityMappingCache<UserEntity>.Mapping;
        var visitor = new PredicateVisitor<UserEntity>(mapping, SqlServerDialect.Instance);

        var (sql, parameters) = visitor.Translate(predicate);
        var dict = Assert.IsType<Dictionary<string, object>>(parameters);

        return (sql, dict);
    }

    [Table("Users", Schema = "dbo")]
    private class UserEntity
    {
        [Key]
        public int Id { get; set; }

        [Column("username")]
        public string Name { get; set; } = string.Empty;

        public bool IsActive { get; set; }
    }
}
