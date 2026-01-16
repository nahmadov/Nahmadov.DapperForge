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
        await ShowDapperQueryableExamplesAsync();
        await ShowIncludeExamplesAsync();
        await ShowAlternateKeyExamplesAsync();
        await ShowTransactionExamplesAsync();
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

        // var idList = active.Select(c => c.Id).Take(2).ToArray();
        // var inList = await _db.Customers.WhereAsync(c => idList.Contains(c.Id));
        // Console.WriteLine($"Customers with ids IN ({string.Join(", ", idList)}): {inList.Count()}");
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

    private async Task ShowDapperQueryableExamplesAsync()
    {
        Console.WriteLine("\n=== DapperQueryable Examples ===");

        // Example 1: Using Where with OrderBy
        Console.WriteLine("\nExample 1: Where with OrderBy");
        var activeOrderedByName = await _db.Customers
            .Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
        Console.WriteLine($"Active customers ordered by name: {activeOrderedByName.Count()} found");
        foreach (var customer in activeOrderedByName)
        {
            Console.WriteLine($"  - {customer.Name} ({customer.Id})");
        }

        // Example 2: Using Where with OrderByDescending
        Console.WriteLine("\nExample 2: Where with OrderByDescending");
        var sortedByCreatedDesc = await _db.Customers
            .Query()
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        Console.WriteLine($"Active customers ordered by creation date (descending): {sortedByCreatedDesc.Count()} found");

        // Example 3: Using Skip and Take for pagination
        Console.WriteLine("\nExample 3: Skip and Take for Pagination");
        var page1 = await _db.Customers
            .Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .Skip(0)
            .Take(2)
            .ToListAsync();
        Console.WriteLine($"Page 1 (skip 0, take 2): {page1.Count()} customers");
        foreach (var customer in page1)
        {
            Console.WriteLine($"  - {customer.Name} (ID: {customer.Id})");
        }

        var page2 = await _db.Customers
            .Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Id)
            .Skip(2)
            .Take(2)
            .ToListAsync();
        Console.WriteLine($"Page 2 (skip 2, take 2): {page2.Count()} customers");

        // Example 4: Complex query with multiple chains
        Console.WriteLine("\nExample 4: Complex Query Chain");
        var complexQuery = await _db.Customers
            .Query()
            .Where(c => c.IsActive && c.City != null)
            .OrderByDescending(c => c.Name)
            .Skip(0)
            .Take(5)
            .ToListAsync();
        Console.WriteLine($"Complex query result: {complexQuery.Count()} customers found");

        // Example 5: Using FirstOrDefaultAsync with DapperQueryable
        Console.WriteLine("\nExample 5: FirstOrDefaultAsync with Queryable");
        var firstActiveByCity = await _db.Customers
            .Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.City)
            .FirstOrDefaultAsync();
        if (firstActiveByCity is not null)
        {
            Console.WriteLine($"First active customer by city: {firstActiveByCity.Name} from {firstActiveByCity.City}");
        }

        // Example 6: Combining with LINQ operations
        Console.WriteLine("\nExample 6: DapperQueryable with LINQ Post-Processing");
        var allActiveCustomers = await _db.Customers
            .Query()
            .Where(c => c.IsActive)
            .ToListAsync();
        var emailDomains = allActiveCustomers
            .Where(c => c.Email != null)
            .Select(c => c.Email!.Split('@')[1])
            .Distinct()
            .ToList();
        Console.WriteLine($"Email domains of active customers: {string.Join(", ", emailDomains)}");
    }

    private async Task ShowIncludeExamplesAsync()
    {
        Console.WriteLine("\n=== Include (Eager Loading) Examples ===\n");

        // Example 1: Include related tickets with customers
        Console.WriteLine("Example 1: Include support tickets with customers");
        var customersWithTickets = await _db.Customers
            .Query()
            .Include(c => c.SupportTickets)
            .Where(c => c.IsActive)
            .ToListAsync();

        foreach (var customer in customersWithTickets.Where(x => x.SupportTickets.Count != 0))
        {
            Console.WriteLine($"  Customer: {customer.Name} - {customer.SupportTickets.Count} ticket(s)");
            foreach (var ticket in customer.SupportTickets)
            {
                Console.WriteLine($"    - Ticket #{ticket.TicketId}: {ticket.Title} ({ticket.Status})");
            }
        }

        // Example 2: Include related customer with tickets
        Console.WriteLine("\nExample 2: Include related customer with tickets");
        var ticketsWithCustomers = await _db.Tickets
            .Query()
            .Include(t => t.Customer)
            .ToListAsync();

        foreach (var ticket in ticketsWithCustomers)
        {
            var customerName = ticket.Customer?.Name ?? "Unknown";
            Console.WriteLine($"  Ticket #{ticket.TicketId}: {ticket.Title}");
            Console.WriteLine($"    - Customer: {customerName}");
            Console.WriteLine($"    - Status: {ticket.Status}");
        }
    }

    private async Task ShowAlternateKeyExamplesAsync()
    {
        Console.WriteLine("\n=== Composite Alternate Key Examples (Product Entity) ===\n");

        const int tenantId = 1;

        // 1. INSERT - Create products with composite alternate key
        Console.WriteLine("1. INSERT - Creating products with composite alternate key...");

        var product1 = new Product
        {
            TenantId = tenantId,
            ProductCode = "LAPTOP-001",
            Name = "Business Laptop Pro",
            Description = "High-performance laptop for business use",
            Price = 1299.99m,
            StockQuantity = 50,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var product2 = new Product
        {
            TenantId = tenantId,
            ProductCode = "MOUSE-001",
            Name = "Wireless Mouse",
            Description = "Ergonomic wireless mouse",
            Price = 49.99m,
            StockQuantity = 200,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Check if products already exist
        var existingProduct1 = await _db.Products.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.ProductCode == "LAPTOP-001");

        if (existingProduct1 is null)
        {
            await _db.Products.InsertAsync(product1);
            Console.WriteLine($"  ✓ Inserted: {product1.Name} (TenantId={product1.TenantId}, Code={product1.ProductCode})");
        }
        else
        {
            Console.WriteLine($"  - Product already exists: {existingProduct1.Name}");
        }

        var existingProduct2 = await _db.Products.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.ProductCode == "MOUSE-001");

        if (existingProduct2 is null)
        {
            await _db.Products.InsertAsync(product2);
            Console.WriteLine($"  ✓ Inserted: {product2.Name} (TenantId={product2.TenantId}, Code={product2.ProductCode})");
        }
        else
        {
            Console.WriteLine($"  - Product already exists: {existingProduct2.Name}");
        }

        // 2. SELECT - Query products
        Console.WriteLine("\n2. SELECT - Querying products...");

        var allProducts = await _db.Products.WhereAsync(p => p.TenantId == tenantId);
        Console.WriteLine($"  Found {allProducts.Count()} products for Tenant {tenantId}:");
        foreach (var p in allProducts)
        {
            Console.WriteLine($"    - [{p.ProductCode}] {p.Name}: ${p.Price} (Stock: {p.StockQuantity})");
        }

        // 3. UPDATE - Update product using composite alternate key
        Console.WriteLine("\n3. UPDATE - Updating product using composite alternate key...");

        var productToUpdate = await _db.Products.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.ProductCode == "LAPTOP-001");

        if (productToUpdate is not null)
        {
            var oldPrice = productToUpdate.Price;
            productToUpdate.Price = 1199.99m;
            productToUpdate.StockQuantity = 45;
            productToUpdate.UpdatedAt = DateTime.UtcNow;

            // Update using WHERE with composite key
            await _db.Products.UpdateAsync(
                productToUpdate,
                new { productToUpdate.TenantId, productToUpdate.ProductCode });

            Console.WriteLine($"  ✓ Updated {productToUpdate.Name}: Price ${oldPrice} -> ${productToUpdate.Price}");
        }

        // 4. DELETE - Delete product using composite alternate key
        Console.WriteLine("\n4. DELETE - Deleting product using composite alternate key...");

        // Create a temporary product to delete
        var tempProduct = new Product
        {
            TenantId = tenantId,
            ProductCode = "TEMP-DELETE-001",
            Name = "Temporary Product",
            Price = 9.99m,
            StockQuantity = 1,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };

        await _db.Products.InsertAsync(tempProduct);
        Console.WriteLine($"  - Created temp product: {tempProduct.ProductCode}");

        // Delete using WHERE with composite key
        await _db.Products.DeleteAsync(
            new { tempProduct.TenantId, tempProduct.ProductCode });

        Console.WriteLine($"  ✓ Deleted product with composite key: TenantId={tempProduct.TenantId}, Code={tempProduct.ProductCode}");

        // 5. Query with DapperQueryable
        Console.WriteLine("\n5. QUERY - Using DapperQueryable with alternate key entity...");

        var activeProducts = await _db.Products
            .Query()
            .Where(p => p.TenantId == tenantId && p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

        Console.WriteLine($"  Active products for Tenant {tenantId}:");
        foreach (var p in activeProducts)
        {
            Console.WriteLine($"    - {p.Name} (${p.Price})");
        }

        Console.WriteLine("\n  ✓ Composite alternate key CRUD operations completed successfully!");
    }

    private async Task ShowTransactionExamplesAsync()
    {
        Console.WriteLine("\n=== Transaction Examples ===\n");

        // Example 1: Simple insert with transaction
        await Example1_SimpleTransactionAsync();

        // Example 2: Multiple operations in transaction
        await Example2_MultipleOperationsAsync();

        // Example 3: Transaction rollback on validation error
        await Example3_RollbackOnValidationAsync();

        // Example 4: Transaction rollback on constraint error
        await Example4_RollbackOnErrorAsync();
    }

    private async Task Example1_SimpleTransactionAsync()
    {
        Console.WriteLine("Example 1: Simple Transaction");
        Console.WriteLine("Creating a new customer in a transaction...");

        using var txScope = await _db.BeginTransactionScopeAsync();
        try
        {
            var newCustomer = new Customer
            {
                Name = "Transaction Test Customer",
                Email = "tx-test@contoso.com",
                City = "San Francisco",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var customerId = await _db.Customers.InsertAndGetIdAsync<int>(newCustomer, txScope.Transaction);

            // Mark transaction as successful (will commit on dispose)
            txScope.Complete();
            Console.WriteLine($"✓ Customer inserted with ID {customerId} and committed.");
        }
        catch (Exception ex)
        {
            // Transaction automatically rolls back if Complete() not called
            Console.WriteLine($"✗ Error: {ex.Message}. Transaction automatically rolled back.");
        }
        // Connection automatically returned to pool on dispose
    }

    private async Task Example2_MultipleOperationsAsync()
    {
        Console.WriteLine("\nExample 2: Multiple Operations in Single Transaction");
        Console.WriteLine("Creating customer with ticket in one transaction...");

        using var txScope = await _db.BeginTransactionScopeAsync();
        try
        {
            // Insert customer
            var customer = new Customer
            {
                Name = "Multi-Op Transaction Customer",
                Email = "multi-op@contoso.com",
                City = "Boston",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var customerId = await _db.Customers.InsertAndGetIdAsync<int>(customer, txScope.Transaction);
            Console.WriteLine($"  - Inserted customer ID {customerId}");

            // Insert related ticket
            var ticket = new SupportTicket
            {
                CustomerId = customerId,
                Title = "Transaction Demo Ticket",
                Description = "Created as part of multi-operation transaction example",
                Status = "Open",
                OpenedOn = DateTime.UtcNow
            };

            var ticketId = await _db.Tickets.InsertAndGetIdAsync<int>(ticket, txScope.Transaction);
            Console.WriteLine($"  - Inserted ticket ID {ticketId}");

            // Mark as successful - both operations will commit together
            txScope.Complete();
            Console.WriteLine("✓ Both operations committed in single transaction.");
        }
        catch (Exception ex)
        {
            // Automatic rollback if Complete() not called
            Console.WriteLine($"✗ Error during multi-op: {ex.Message}. Both operations automatically rolled back.");
        }
    }

    private async Task Example3_RollbackOnValidationAsync()
    {
        Console.WriteLine("\nExample 3: Rollback on Validation Error");
        Console.WriteLine("Attempting to insert customer with invalid data...");

        using var txScope = await _db.BeginTransactionScopeAsync();
        try
        {
            // Create customer with empty name (will fail validation)
            var invalidCustomer = new Customer
            {
                Name = string.Empty, // Invalid - Name is required
                Email = "invalid@contoso.com",
                City = "Portland",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            var customerId = await _db.Customers.InsertAndGetIdAsync<int>(invalidCustomer, txScope.Transaction);
            txScope.Complete();
            Console.WriteLine("✗ Should not reach here - validation should have failed.");
        }
        catch (Nahmadov.DapperForge.Core.Infrastructure.Exceptions.DapperValidationException ex)
        {
            // Automatic rollback (Complete() not called)
            Console.WriteLine("✓ Validation error caught and transaction automatically rolled back.");
            Console.WriteLine($"  Validation errors: {string.Join(", ", ex.Errors)}");
        }
        catch (Exception ex)
        {
            // Automatic rollback on any exception
            Console.WriteLine($"✗ Unexpected error: {ex.Message}. Transaction automatically rolled back.");
        }
    }

    private async Task Example4_RollbackOnErrorAsync()
    {
        Console.WriteLine("\nExample 4: Rollback on Duplicate Data Error");
        Console.WriteLine("Attempting to insert duplicate customer in transaction...");

        // First, ensure a customer exists
        var existingCustomer = await _db.Customers.FirstOrDefaultAsync(c => c.Name == "Ada Lovelace", ignoreCase: true);

        if (existingCustomer is not null)
        {
            using var txScope = await _db.BeginTransactionScopeAsync();
            try
            {
                // Try to insert customer with same email (will fail on insert due to data constraints)
                var duplicateCustomer = new Customer
                {
                    Name = "Different Name",
                    Email = existingCustomer.Email, // Same email as existing
                    City = "New York",
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var customerId = await _db.Customers.InsertAndGetIdAsync<int>(duplicateCustomer, txScope.Transaction);
                txScope.Complete();
                Console.WriteLine("✗ Should not reach here - duplicate constraint should have failed.");
            }
            catch (Exception ex)
            {
                // Automatic rollback (Complete() not called)
                Console.WriteLine("✓ Error caught and transaction automatically rolled back.");
                Console.WriteLine($"  Error: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("Skipped: Ada Lovelace customer not found.");
        }
    }
}

