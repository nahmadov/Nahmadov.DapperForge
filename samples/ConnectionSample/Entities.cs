using System.ComponentModel.DataAnnotations;

namespace ConnectionSample;

public class Customer
{
    public int Id { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    [MaxLength(100)]
    public string? City { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

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
