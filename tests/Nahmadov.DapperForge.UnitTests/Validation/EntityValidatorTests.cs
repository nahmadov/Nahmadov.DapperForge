using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Nahmadov.DapperForge.Core.Attributes;
using Nahmadov.DapperForge.Core.Builders;
using Nahmadov.DapperForge.Core.Common;
using Nahmadov.DapperForge.Core.Context;
using Nahmadov.DapperForge.Core.Mapping;
using Nahmadov.DapperForge.Core.Validation;
using Nahmadov.DapperForge.SqlServer;
using Xunit;

namespace Nahmadov.DapperForge.UnitTests.Validation;

/// <summary>
/// Comprehensive tests for EntityValidator covering required fields, string length, and read-only entities.
/// </summary>
public class EntityValidatorTests
{
    [Table("Users", Schema = "dbo")]
    private class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        [StringLength(50, MinimumLength = 3)]
        public string? Username { get; set; }

        public bool IsActive { get; set; } = true;
    }

    [Table("ReadOnlyCustomer", Schema = "dbo")]
    [ReadOnlyEntity]
    private class ReadOnlyCustomer
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private static EntityMapping BuildUserMapping()
    {
        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<User>(b =>
        {
            b.ToTable("Users", "dbo");
            b.Property(u => u.Name).IsRequired().HasMaxLength(100);
            b.Property(u => u.Email);
            b.Property(u => u.Username).HasMaxLength(50);
        });

        var model = builder.Build();
        return model[typeof(User)];
    }

    private static EntityMapping BuildReadOnlyMapping()
    {
        var builder = new DapperModelBuilder(SqlServerDialect.Instance);
        builder.Entity<ReadOnlyCustomer>(b =>
        {
            b.ToTable("ReadOnlyCustomer", "dbo");
            b.IsReadOnly();
        });

        var model = builder.Build();
        return model[typeof(ReadOnlyCustomer)];
    }

    #region Required Field Validation

    [Fact]
    public void ValidateForInsert_ThrowsWhenRequiredStringIsNull()
    {
        var mapping = BuildUserMapping();
        var user = new User { Name = null!, Email = "test@example.com" };

        var exception = Assert.Throws<Core.Exceptions.DapperValidationException>(() =>
        {
            EntityValidator<User>.ValidateForInsert(user, mapping);
        });

        Assert.Contains("required", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Name", exception.Message);
    }

    [Fact]
    public void ValidateForInsert_ThrowsWhenRequiredStringIsEmpty()
    {
        var mapping = BuildUserMapping();
        var user = new User { Name = "", Email = "test@example.com" };

        var exception = Assert.Throws<Nahmadov.DapperForge.Core.Exceptions.DapperValidationException>(() =>
        {
            EntityValidator<User>.ValidateForInsert(user, mapping);
        });

        Assert.Contains("required", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cannot be empty", exception.Message);
    }

    [Fact]
    public void ValidateForInsert_ThrowsWhenRequiredStringIsWhitespace()
    {
        var mapping = BuildUserMapping();
        var user = new User { Name = "   ", Email = "test@example.com" };

        var exception = Assert.Throws<Nahmadov.DapperForge.Core.Exceptions.DapperValidationException>(() =>
        {
            EntityValidator<User>.ValidateForInsert(user, mapping);
        });

        Assert.Contains("required", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateForInsert_PassesWhenRequiredFieldIsValid()
    {
        var mapping = BuildUserMapping();
        var user = new User { Name = "John Doe", Email = "john@example.com" };

        // Should not throw
        EntityValidator<User>.ValidateForInsert(user, mapping);
    }

    #endregion

    #region String Length Validation

    [Fact]
    public void ValidateForInsert_ThrowsWhenStringExceedsMaxLength()
    {
        var mapping = BuildUserMapping();
        var longName = new string('a', 101);
        var user = new User { Name = longName };

        var exception = Assert.Throws<Nahmadov.DapperForge.Core.Exceptions.DapperValidationException>(() =>
        {
            EntityValidator<User>.ValidateForInsert(user, mapping);
        });

        Assert.Contains("exceeds maximum length", exception.Message);
        Assert.Contains("100", exception.Message);
    }

    [Fact]
    public void ValidateForInsert_ThrowsWhenStringBelowMinLength()
    {
        var mapping = BuildUserMapping();
        var user = new User { Name = "John", Username = "ab" }; // Username has MinLength=3

        var exception = Assert.Throws<Nahmadov.DapperForge.Core.Exceptions.DapperValidationException>(() =>
        {
            EntityValidator<User>.ValidateForInsert(user, mapping);
        });

        Assert.Contains("minimum length", exception.Message);
        Assert.Contains("3", exception.Message);
    }

    [Fact]
    public void ValidateForInsert_PassesWhenStringWithinLength()
    {
        var mapping = BuildUserMapping();
        var user = new User { Name = "John", Username = "john_doe" };

        // Should not throw
        EntityValidator<User>.ValidateForInsert(user, mapping);
    }

    [Fact]
    public void ValidateForInsert_PassesWhenStringAtMaxLength()
    {
        var mapping = BuildUserMapping();
        var maxName = new string('a', 100);
        var user = new User { Name = maxName };

        // Should not throw
        EntityValidator<User>.ValidateForInsert(user, mapping);
    }

    #endregion

    #region Email Validation

    [Fact]
    public void ValidateForInsert_PassesWhenEmailIsValid()
    {
        var mapping = BuildUserMapping();
        var user = new User { Name = "John", Email = "john.doe@example.com" };

        // Should not throw (email attribute is not enforced in current implementation)
        EntityValidator<User>.ValidateForInsert(user, mapping);
    }

    [Fact]
    public void ValidateForInsert_PassesWhenEmailIsNull()
    {
        var mapping = BuildUserMapping();
        var user = new User { Name = "John", Email = null };

        // Should not throw (email is optional)
        EntityValidator<User>.ValidateForInsert(user, mapping);
    }

    #endregion

    #region Update Validation

    [Fact]
    public void ValidateForUpdate_ThrowsWhenRequiredFieldBecomesNull()
    {
        var mapping = BuildUserMapping();
        var user = new User { Id = 1, Name = null!, Email = "test@example.com" };

        var exception = Assert.Throws<Nahmadov.DapperForge.Core.Exceptions.DapperValidationException>(() =>
        {
            EntityValidator<User>.ValidateForUpdate(user, mapping);
        });

        Assert.Contains("required", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateForUpdate_ThrowsWhenStringExceedsLength()
    {
        var mapping = BuildUserMapping();
        var longName = new string('a', 101);
        var user = new User { Id = 1, Name = longName };

        var exception = Assert.Throws<Nahmadov.DapperForge.Core.Exceptions.DapperValidationException>(() =>
        {
            EntityValidator<User>.ValidateForUpdate(user, mapping);
        });

        Assert.Contains("exceeds maximum length", exception.Message);
    }

    [Fact]
    public void ValidateForUpdate_PassesWhenEntityIsValid()
    {
        var mapping = BuildUserMapping();
        var user = new User { Id = 1, Name = "Jane Doe", Email = "jane@example.com" };

        // Should not throw
        EntityValidator<User>.ValidateForUpdate(user, mapping);
    }

    #endregion

    #region Read-Only Entity Validation

    [Fact]
    public void ValidateForInsert_ThrowsWhenEntityIsReadOnly()
    {
        var mapping = BuildReadOnlyMapping();
        var customer = new ReadOnlyCustomer { Name = "Test" };

        var exception = Assert.Throws<Nahmadov.DapperForge.Core.Exceptions.DapperReadOnlyException>(() =>
        {
            EntityValidator<ReadOnlyCustomer>.ValidateForInsert(customer, mapping);
        });

        Assert.Contains("ReadOnly", exception.Message);
    }

    [Fact]
    public void ValidateForUpdate_ThrowsWhenEntityIsReadOnly()
    {
        var mapping = BuildReadOnlyMapping();
        var customer = new ReadOnlyCustomer { Id = 1, Name = "Test" };

        var exception = Assert.Throws<Nahmadov.DapperForge.Core.Exceptions.DapperReadOnlyException>(() =>
        {
            EntityValidator<ReadOnlyCustomer>.ValidateForUpdate(customer, mapping);
        });

        Assert.Contains("ReadOnly", exception.Message);
    }

    #endregion

    #region Null Entity Validation

    [Fact]
    public void ValidateForInsert_ThrowsWhenEntityIsNull()
    {
        var mapping = BuildUserMapping();

        Assert.Throws<ArgumentNullException>(() =>
        {
            EntityValidator<User>.ValidateForInsert(null!, mapping);
        });
    }

    [Fact]
    public void ValidateForUpdate_ThrowsWhenEntityIsNull()
    {
        var mapping = BuildUserMapping();

        Assert.Throws<ArgumentNullException>(() =>
        {
            EntityValidator<User>.ValidateForUpdate(null!, mapping);
        });
    }

    [Fact]
    public void ValidateForInsert_ThrowsWhenMappingIsNull()
    {
        var user = new User { Name = "John" };

        Assert.Throws<ArgumentNullException>(() =>
        {
            EntityValidator<User>.ValidateForInsert(user, null!);
        });
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ValidateForInsert_PassesWhenAllOptionalFieldsAreNull()
    {
        var mapping = BuildUserMapping();
        var user = new User { Name = "John", Email = null, Username = null };

        // Should not throw
        EntityValidator<User>.ValidateForInsert(user, mapping);
    }

    [Fact]
    public void ValidateForInsert_PassesWhenBooleanDefaultValue()
    {
        var mapping = BuildUserMapping();
        var user = new User { Name = "John", IsActive = false };

        // Should not throw
        EntityValidator<User>.ValidateForInsert(user, mapping);
    }

    #endregion
}
