using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq.Expressions;

using Nahmadov.DapperForge.Core.Modeling.Builders;
using Nahmadov.DapperForge.Core.Querying.Predicates;
using Nahmadov.DapperForge.Core.Modeling.Mapping;
using Nahmadov.DapperForge.SqlServer;
using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Builders;

/// <summary>
/// Comprehensive tests for PredicateVisitor<T> covering various expression types.
/// Tests expression-to-SQL translation for WHERE clauses.
/// </summary>
public class PredicateVisitorComprehensiveTests
{
    [Table("Users", Schema = "dbo")]
    private class User
    {
        [Key]
        public int Id { get; set; }

        [Column("username")]
        public string Name { get; set; } = string.Empty;

        public string? Email { get; set; }

        public bool IsActive { get; set; }

        public int Age { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    private static EntityMapping BuildMapping()
    {
        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<User>(b =>
        {
            b.ToTable("Users", "dbo");
            b.Property(u => u.Name).HasColumnName("username");
        });

        var model = builder.Build();
        return model[typeof(User)];
    }

    private static (string sql, Dictionary<string, object?> parameters) Translate(
        Expression<Func<User, bool>> predicate,
        bool? ignoreCase = null)
    {
        var mapping = BuildMapping();
        var visitor = new PredicateVisitor<User>(mapping, SqlServerDialect.Instance);
        var (sqlText, paramObj) = visitor.Translate(predicate, ignoreCase);
        return (sqlText, (Dictionary<string, object?>)paramObj);
    }

    #region Boolean Expressions

    [Fact]
    public void Translates_BooleanProperty_True()
    {
        var (sql, parameters) = Translate(u => u.IsActive);

        Assert.Equal("a.[IsActive] = 1", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Translates_BooleanProperty_False()
    {
        var (sql, parameters) = Translate(u => !u.IsActive);

        Assert.Equal("a.[IsActive] = 0", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Translates_BooleanProperty_EqualTrue()
    {
        var (sql, parameters) = Translate(u => u.IsActive == true);

        Assert.Contains("[IsActive]", sql);
        Assert.Contains("1", sql);
    }

    [Fact]
    public void Translates_BooleanProperty_EqualFalse()
    {
        var (sql, parameters) = Translate(u => u.IsActive == false);

        Assert.Contains("[IsActive]", sql);
        Assert.Contains("0", sql);
    }

    #endregion

    #region String Operations

    [Fact]
    public void Translates_StringContains_WithWildcards()
    {
        var (sql, parameters) = Translate(u => u.Name.Contains("john"));

        Assert.Contains("LIKE", sql);
        Assert.Contains("ESCAPE", sql);
        Assert.Single(parameters);
        Assert.Equal("%john%", parameters["p0"]);
    }

    [Fact]
    public void Translates_StringContains_EscapesWildcardChars()
    {
        var (sql, parameters) = Translate(u => u.Name.Contains("a%b_c"));

        Assert.Contains("LIKE", sql);
        Assert.Single(parameters);
        // % and _ should be escaped
        Assert.Equal("%a\\%b\\_c%", parameters["p0"]);
    }

    [Fact]
    public void Translates_StringStartsWith()
    {
        var (sql, parameters) = Translate(u => u.Name.StartsWith("john"));

        Assert.Contains("LIKE", sql);
        Assert.Single(parameters);
        Assert.Equal("john%", parameters["p0"]);
    }

    [Fact]
    public void Translates_StringEndsWith()
    {
        var (sql, parameters) = Translate(u => u.Name.EndsWith("doe"));

        Assert.Contains("LIKE", sql);
        Assert.Single(parameters);
        Assert.Equal("%doe", parameters["p0"]);
    }

    [Fact]
    public void Translates_StringStartsWith_CaseInsensitive()
    {
        var (sql, parameters) = Translate(u => u.Name.StartsWith("john"), ignoreCase: true);

        Assert.Contains("LIKE", sql);
        Assert.Single(parameters);
        Assert.Equal("john%", parameters["p0"]);
    }

    #endregion

    #region Comparison Operations

    [Fact]
    public void Translates_IntegerEqual()
    {
        var (sql, parameters) = Translate(u => u.Id == 5);

        Assert.Contains("[Id]", sql);
        Assert.Contains("=", sql);
        Assert.Contains("@", sql);
        Assert.Single(parameters);
        Assert.Equal(5, parameters["p0"]);
    }

    [Fact]
    public void Translates_IntegerNotEqual()
    {
        var (sql, parameters) = Translate(u => u.Id != 5);

        Assert.Contains("[Id]", sql);
        Assert.Contains("<>", sql);
        Assert.Equal(5, parameters["p0"]);
    }

    [Fact]
    public void Translates_IntegerGreaterThan()
    {
        var (sql, parameters) = Translate(u => u.Age > 18);

        Assert.Contains("[Age]", sql);
        Assert.Contains(">", sql);
        Assert.Equal(18, parameters["p0"]);
    }

    [Fact]
    public void Translates_IntegerGreaterThanOrEqual()
    {
        var (sql, parameters) = Translate(u => u.Age >= 18);

        Assert.Contains("[Age]", sql);
        Assert.Contains(">=", sql);
    }

    [Fact]
    public void Translates_IntegerLessThan()
    {
        var (sql, parameters) = Translate(u => u.Age < 30);

        Assert.Contains("[Age]", sql);
        Assert.Contains("<", sql);
    }

    [Fact]
    public void Translates_IntegerLessThanOrEqual()
    {
        var (sql, parameters) = Translate(u => u.Age <= 30);

        Assert.Contains("[Age]", sql);
        Assert.Contains("<=", sql);
    }

    #endregion

    #region Null Checks

    [Fact]
    public void Translates_NullCheck_IsNull()
    {
        var (sql, parameters) = Translate(u => u.Email == null);

        Assert.Contains("[Email]", sql);
        Assert.Contains("IS NULL", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Translates_NullCheck_IsNotNull()
    {
        var (sql, parameters) = Translate(u => u.Email != null);

        Assert.Contains("[Email]", sql);
        Assert.Contains("IS NOT NULL", sql);
        Assert.Empty(parameters);
    }

    [Fact]
    public void Translates_StringNullCheck()
    {
        var (sql, parameters) = Translate(u => u.Name != null);

        Assert.Contains("[username]", sql);
        Assert.Contains("IS NOT NULL", sql);
    }

    #endregion

    #region Logical Operations

    [Fact]
    public void Translates_AND_Operation()
    {
        var (sql, parameters) = Translate(u => u.IsActive && u.Age > 18);

        Assert.Contains("AND", sql);
        Assert.Contains("[IsActive]", sql);
        Assert.Contains("[Age]", sql);
        Assert.NotEmpty(parameters);
    }

    [Fact]
    public void Translates_OR_Operation()
    {
        var (sql, parameters) = Translate(u => u.Id == 1 || u.Id == 2);

        Assert.Contains("OR", sql);
        Assert.Contains("[Id]", sql);
    }

    [Fact]
    public void Translates_ComplexAND_OR_Expression()
    {
        var (sql, parameters) = Translate(u => 
            (u.IsActive && u.Age > 18) || (u.Name.Contains("admin") && u.Id < 100));

        Assert.Contains("AND", sql);
        Assert.Contains("OR", sql);
        Assert.True(parameters.Count > 0);
    }

    #endregion

    #region Member Access

    [Fact]
    public void Translates_DirectProperty_Access()
    {
        var (sql, parameters) = Translate(u => u.Name == "john");

        Assert.Contains("[username]", sql);
        Assert.Contains("=", sql);
    }

    [Fact]
    public void Translates_ColumnName_Alias()
    {
        // The Name property is mapped to "username" column
        var (sql, parameters) = Translate(u => u.Name == "test");

        Assert.Contains("[username]", sql);
        Assert.DoesNotContain("[Name]", sql);
    }

    #endregion

    #region Parameter Naming

    [Fact]
    public void Translates_MultipleParameters_WithCorrectNames()
    {
        var (sql, parameters) = Translate(u => u.Id == 1 && u.Age == 25);

        Assert.True(parameters.Count >= 2);
        Assert.Contains("p0", parameters.Keys.First());
    }

    [Fact]
    public void Translates_Parameters_AreEscaped()
    {
        var (sql, parameters) = Translate(u => u.Name.Contains("50% off"));

        var paramValue = parameters["p0"];
        Assert.Contains("%50\\% off%", paramValue?.ToString() ?? "");
    }

    #endregion

    #region DateTime Operations

    [Fact]
    public void Translates_DateTimeComparison()
    {
        var date = new DateTime(2024, 1, 1);
        var (sql, parameters) = Translate(u => u.CreatedAt > date);

        Assert.Contains("[CreatedAt]", sql);
        Assert.Contains(">", sql);
        Assert.Single(parameters);
        Assert.Equal(date, parameters["p0"]);
    }

    [Fact]
    public void Translates_DateTimeEqual()
    {
        var date = new DateTime(2024, 1, 1);
        var (sql, parameters) = Translate(u => u.CreatedAt == date);

        Assert.Contains("[CreatedAt]", sql);
        Assert.Contains("=", sql);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Translates_NegatedComparison()
    {
        var (sql, parameters) = Translate(u => !(u.IsActive));

        Assert.Contains("[IsActive]", sql);
        Assert.Contains("0", sql);
    }

    [Fact]
    public void Translates_EmptyStringCheck()
    {
        var (sql, parameters) = Translate(u => u.Name == "");

        Assert.Contains("[username]", sql);
        Assert.Contains("@", sql);
        Assert.Equal("", parameters["p0"]);
    }

    [Fact]
    public void Translates_WhitespaceStringCheck()
    {
        var (sql, parameters) = Translate(u => u.Name == "   ");

        Assert.Contains("[username]", sql);
        Assert.Equal("   ", parameters["p0"]);
    }

    #endregion
}


