using System.Linq;

namespace ConnectionSample;

public class SampleRunner(AppDapperDbContext db)
{
    private readonly AppDapperDbContext _db = db;

    public async Task RunAsync()
    {
        Console.WriteLine("=== Nahmadov.DapperForge full-feature sample ===");
        var (adaId, graceId) = await SeedCustomersAsync();
        var ticketId = await SeedTicketAsync(adaId);

        await ShowWhereExamplesAsync();
        await RunCrudExamplesAsync(graceId, ticketId);
        await ShowReadOnlyExampleAsync();
    }

    private async Task<(int AdaId, int GraceId)> SeedCustomersAsync()
    {
        Console.WriteLine("Seeding customers (idempotent)...");

        var adaId = await EnsureCustomerAsync(
            name: "Ada Lovelace",
            email: "ada@contoso.com",
            city: "London",
            isActive: true);

        var graceId = await EnsureCustomerAsync(
            name: "Grace Hopper",
            email: "grace@contoso.com",
            city: "Arlington",
            isActive: true);

        await EnsureCustomerAsync(
            name: "Inactive Sample",
            email: null,
            city: "New York",
            isActive: false);

        return (adaId, graceId);
    }

    private async Task<int> EnsureCustomerAsync(string name, string? email, string? city, bool isActive)
    {
        var existing = await _db.Customers.FirstOrDefaultAsync(c => c.Name == name, ignoreCase: true);
        if (existing is not null)
        {
            return existing.Id;
        }

        var customer = new Customer
        {
            Name = name,
            Email = email,
            City = city,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow,
            LastLogin = null
        };

        var id = await _db.Customers.InsertAndGetIdAsync<int>(customer);
        Console.WriteLine($"Inserted customer '{name}' with id {id}");
        return id;
    }

    private async Task<int> SeedTicketAsync(int customerId)
    {
        var existing = await _db.Tickets.FirstOrDefaultAsync(t => t.Title == "Sample outage ticket", ignoreCase: true);
        if (existing is not null)
        {
            return existing.TicketId;
        }

        var ticket = new SupportTicket
        {
            CustomerId = customerId,
            Title = "Sample outage ticket",
            Description = "API requests return 500 intermittently.",
            Status = "Open",
            IsEscalated = true,
            OpenedOn = DateTime.UtcNow
        };

        var id = await _db.Tickets.InsertAndGetIdAsync<int>(ticket);
        Console.WriteLine($"Inserted ticket '{ticket.Title}' with id {id}");
        return id;
    }

    private async Task ShowWhereExamplesAsync()
    {
        Console.WriteLine("\nQuery examples using PredicateVisitor:");

        var active = await _db.Customers.WhereAsync(c => (c.IsActive || c.Id > 0) && true);
        Console.WriteLine($"Active customers (boolean projection): {active.Count()}");

        var inactiveOrMissingEmail = await _db.Customers.WhereAsync(c => !c.IsActive || c.Email == null);
        Console.WriteLine($"Inactive or missing email: {inactiveOrMissingEmail.Count()}");

        var startsWithA = await _db.Customers.WhereAsync(c => c.Name.StartsWith("a"), ignoreCase: true);
        Console.WriteLine($"Name starts with 'a' (ignore case): {startsWithA.Count()}");

        var containsYork = await _db.Customers.WhereAsync(c => c.City != null && c.City.Contains("york"), ignoreCase: true);
        Console.WriteLine($"City contains 'york': {containsYork.Count()}");

        var endsWithCom = await _db.Customers.WhereAsync(c => c.Email != null && c.Email.EndsWith(".com"));
        Console.WriteLine($"Email ends with .com: {endsWithCom.Count()}");

        var firstAda = await _db.Customers.FirstOrDefaultAsync(c => c.Name == "Ada Lovelace", ignoreCase: true);
        Console.WriteLine($"FirstOrDefault for Ada Lovelace: {(firstAda is null ? "not found" : $"found id {firstAda.Id}")}");

        var idList = active.Select(c => c.Id).Take(2).ToArray();
        var inList = await _db.Customers.WhereAsync(c => idList.Contains(c.Id));
        Console.WriteLine($"Customers with ids IN ({string.Join(", ", idList)}): {inList.Count()}");
    }

    private async Task RunCrudExamplesAsync(int customerId, int ticketId)
    {
        Console.WriteLine("\nCRUD examples:");

        // Find and update
        var customer = await _db.Customers.FindAsync(customerId);
        if (customer is not null)
        {
            var originalCity = customer.City;
            customer.City = "Seattle";
            await _db.Customers.UpdateAsync(customer);
            Console.WriteLine($"Updated customer {customer.Name} city from '{originalCity}' to '{customer.City}'.");
        }

        // Insert and delete a temporary customer using DeleteById
        var tempCustomer = new Customer
        {
            Name = "Temp To Delete",
            City = "Chicago",
            Email = "temp@contoso.com",
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };
        var tempId = await _db.Customers.InsertAndGetIdAsync<int>(tempCustomer);
        await _db.Customers.DeleteByIdAsync(tempId);
        Console.WriteLine($"Inserted then deleted customer id {tempId} via DeleteByIdAsync.");

        // Delete a ticket using DeleteAsync
        var ticket = await _db.Tickets.FindAsync(ticketId);
        if (ticket is not null)
        {
            ticket.Status = "Closed";
            ticket.ClosedOn = DateTime.UtcNow;
            await _db.Tickets.UpdateAsync(ticket);
            await _db.Tickets.DeleteAsync(ticket);
            Console.WriteLine($"Closed and deleted ticket id {ticketId}.");
        }
    }

    private async Task ShowReadOnlyExampleAsync()
    {
        Console.WriteLine("\nRead-only entity example:");
        var auditEntries = await _db.AuditLogs.GetAllAsync();
        foreach (var entry in auditEntries.Take(3))
        {
            Console.WriteLine($"[Audit] {entry.CreatedAt:u} {entry.Entity} {entry.Action} {entry.Details}");
        }

        if (!auditEntries.Any())
        {
            Console.WriteLine("No audit logs present yet. Insert rows into dbo.AuditLogs to see read-only queries in action.");
        }
    }
}
