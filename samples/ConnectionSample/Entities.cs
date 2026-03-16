using System.ComponentModel.DataAnnotations;

namespace ConnectionSample;

/// <summary>
/// Base entity demonstrating inheritance scenario.
/// Properties defined here should work correctly in derived entity queries.
/// </summary>
public abstract class BaseEntity
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Interface demonstrating interface implementation scenario.
/// Properties defined here should work correctly in implementing entity queries.
/// </summary>
public interface ITrackable
{
    bool IsActive { get; set; }
    DateTime? LastModified { get; set; }
}

public class Customer : BaseEntity, ITrackable
{
    // Id, Name, CreatedAt inherited from BaseEntity

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    // IsActive, LastModified from ITrackable interface
    public bool IsActive { get; set; }

    public DateTime? LastModified { get; set; }

    public DateTime? LastLogin { get; set; }

    // Navigation property for related support tickets
    public List<SupportTicket> SupportTickets { get; set; } = [];
}

public class SupportTicket
{
    [Key]
    public int TicketId { get; set; }

    // Foreign key configured via fluent API in AppDapperDbContext.OnModelCreating
    public int CustomerId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Open";

    public bool IsEscalated { get; set; }

    public DateTime OpenedOn { get; set; }

    public DateTime? ClosedOn { get; set; }

    // Navigation property for related customer
    public Customer? Customer { get; set; }
}

public class AuditLog
{
    public int Id { get; set; }

    [MaxLength(100)]
    public string Entity { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; }
}

// ──────────────────────────────────────────────────────────────────────────────
// ThenInclude sample entities
//
//  Department (A)  ──< Employee (B)  ──  EmployeeAddress (C)
//                                    ──< Assignment     (D)  ──  AssignmentCategory (F)
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>A – main table. Has a list of Employees.</summary>
public class Department : BaseEntity
{
    // Id, Name, CreatedAt – inherited from BaseEntity
    public List<Employee> Employees { get; set; } = [];
}

/// <summary>B – has one Address (C) and a list of Assignments (D).</summary>
public class Employee : BaseEntity
{
    // Id, Name, CreatedAt – inherited from BaseEntity
    public int DepartmentId { get; set; }

    [MaxLength(100)]
    public string Position { get; set; } = string.Empty;

    /// <summary>FK → EmployeeAddress (C).</summary>
    public int? AddressId { get; set; }

    // Navigation properties
    public Department? Department { get; set; }
    public EmployeeAddress? Address { get; set; }
    public List<Assignment> Assignments { get; set; } = [];
}

/// <summary>C – single address per employee (FK on Employee side).</summary>
public class EmployeeAddress
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string Street { get; set; } = string.Empty;

    [MaxLength(100)]
    public string City { get; set; } = string.Empty;
}

/// <summary>D – belongs to an Employee, has one AssignmentCategory (F).</summary>
public class Assignment
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public int CategoryId { get; set; }

    // Navigation properties
    public Employee? Employee { get; set; }
    public AssignmentCategory? Category { get; set; }
}

/// <summary>F – single category per assignment.</summary>
public class AssignmentCategory
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;
}

// ──────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Product entity demonstrating composite alternate key usage.
/// This entity has no primary key - it uses TenantId + ProductCode as a business key.
/// </summary>
public class Product
{
    /// <summary>
    /// Tenant identifier - part of composite alternate key.
    /// </summary>
    [Required]
    public int TenantId { get; set; }

    /// <summary>
    /// Product code unique within tenant - part of composite alternate key.
    /// </summary>
    [Required, MaxLength(50)]
    public string ProductCode { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
