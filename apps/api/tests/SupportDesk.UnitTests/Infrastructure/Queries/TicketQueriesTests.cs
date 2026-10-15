
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests
{
    [Fact]
    public async Task GetPagedAsync_FiltersByStatusAndPriority_BeforeCounting()
    {
        // ARRANGE: Create a real relational SQLite database in memory.
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "Acme Company",
            "support@acme.example",
            null,
            CustomerTier.Standard,
            now);

        var category = new Category(
            "Billing",
            requiresSpecialist: false,
            forcesCriticalPriority: false);

        db.Set<Customer>().Add(customer);
        db.Set<Category>().Add(category);
        await db.SaveChangesAsync();

        // Ticket A: New + High => SHOULD MATCH.
        var matchingTicket = Ticket.Raise(
            "TCK-0001",
            "Invoice problem",
            "Customer cannot download an invoice.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            now);

        // Ticket B: New + Low => SHOULD NOT MATCH.
        var wrongPriority = Ticket.Raise(
            "TCK-0002",
            "Password reset",
            "Customer needs to reset a password.",
            customer.Id,
            category.Id,
            TicketPriority.Low,
            now);

        // Ticket C: InProgress + High => SHOULD NOT MATCH.
        var wrongStatus = Ticket.Raise(
            "TCK-0003",
            "Payment issue",
            "Customer has a payment problem.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            now);

        wrongStatus.ChangeStatus(TicketStatus.InProgress, now);

        db.Set<Ticket>().AddRange(
            matchingTicket,
            wrongPriority,
            wrongStatus);

        await db.SaveChangesAsync();

        var ticketQueries = new TicketQueries(db, new FixedClock());

        var query = new TicketQuery
        {
            Status = TicketStatus.New,
            Priority = TicketPriority.High,
            Page = 1,
            PageSize = 20
        };

        // ACT: Execute the actual EF Core query.
        var result = await ticketQueries.GetPagedAsync(
            query,
            CancellationToken.None);

        // ASSERT: Only Ticket A should be returned.
        Assert.Equal(1, result.TotalCount);

        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-0001", ticket.Reference);
    }


    [Theory]
    [InlineData("invoice")]
    [InlineData(" INVOICE ")]
    [InlineData("TCK-0001")]
    [InlineData("Acme Company")]
    public async Task GetPagedAsync_SearchesTitleReferenceAndCustomerName(
        string search)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "Acme Company",
            "support@acme.example",
            null,
            CustomerTier.Standard,
            now);

        var category = new Category("Billing", false, false);

        db.Set<Customer>().Add(customer);
        db.Set<Category>().Add(category);
        await db.SaveChangesAsync();

        var ticket = Ticket.Raise(
            "TCK-0001",
            "Invoice problem",
            "Customer cannot download an invoice.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            now);

        db.Set<Ticket>().Add(ticket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new TicketQuery { Search = search },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("TCK-0001", Assert.Single(result.Items).Reference);
    }


    [Fact]
    public async Task GetPagedAsync_PaginatesFilteredResults()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "Acme Company",
            "support@acme.example",
            null,
            CustomerTier.Standard,
            now);

        var category = new Category("Billing", false, false);

        db.Set<Customer>().Add(customer);
        db.Set<Category>().Add(category);
        await db.SaveChangesAsync();

        for (var i = 1; i <= 5; i++)
        {
            var ticket = Ticket.Raise(
                $"TCK-{i:0000}",
                $"Billing issue {i}",
                "A customer billing problem.",
                customer.Id,
                category.Id,
                TicketPriority.High,
                now);

            db.Set<Ticket>().Add(ticket);
        }

        var excludedTicket = Ticket.Raise(
            "TCK-9999",
            "Low priority issue",
            "This ticket should not be counted.",
            customer.Id,
            category.Id,
            TicketPriority.Low,
            now);

        db.Set<Ticket>().Add(excludedTicket);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new TicketQuery
            {
                Priority = TicketPriority.High,
                Page = 2,
                PageSize = 2
            },
            CancellationToken.None);

        // Five matching tickets, but only two on page 2.
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Items.Count);

        Assert.All(result.Items, ticket =>
            Assert.Equal(TicketPriority.High, ticket.Priority));
    }

    [Fact]
    public async Task GetPagedAsync_UnassignedOnly_ExcludesAssignedTickets()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = FixedClock.DefaultNow;

        var customer = new Customer(
            "Acme Company",
            "support@acme.example",
            null,
            CustomerTier.Standard,
            now);

        var category = new Category("Billing", false, false);

        var agent = new SupportDesk.Domain.Aggregates.Agents.Agent(
            "Alex Smith",
            "alex@example.com",
            10,
            now);

        db.Set<Customer>().Add(customer);
        db.Set<Category>().Add(category);
        db.Set<SupportDesk.Domain.Aggregates.Agents.Agent>().Add(agent);
        await db.SaveChangesAsync();

        var unassigned = Ticket.Raise(
            "TCK-0001",
            "Unassigned issue",
            "Waiting for an agent.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            now);

        var assigned = Ticket.Raise(
            "TCK-0002",
            "Assigned issue",
            "Already being handled.",
            customer.Id,
            category.Id,
            TicketPriority.High,
            now);

        assigned.AssignTo(agent.Id, now);

        db.Set<Ticket>().AddRange(unassigned, assigned);
        await db.SaveChangesAsync();

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            new TicketQuery { UnassignedOnly = true },
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("TCK-0001", Assert.Single(result.Items).Reference);
    }


    [Theory]
    [InlineData("category")]
    [InlineData("customer")]
    [InlineData("agent")]
    public async Task GetPagedAsync_FiltersByRelatedEntityId(string filter)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new SupportDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = FixedClock.DefaultNow;

        var customerA = new Customer(
            "Customer A", "a@example.com", null,
            CustomerTier.Standard, now);

        var customerB = new Customer(
            "Customer B", "b@example.com", null,
            CustomerTier.Standard, now);

        var categoryA = new Category("Billing", false, false);
        var categoryB = new Category("Technical", false, false);

        var agentA = new SupportDesk.Domain.Aggregates.Agents.Agent(
            "Agent A", "agent-a@example.com", 10, now);

        var agentB = new SupportDesk.Domain.Aggregates.Agents.Agent(
            "Agent B", "agent-b@example.com", 10, now);

        db.Set<Customer>().AddRange(customerA, customerB);
        db.Set<Category>().AddRange(categoryA, categoryB);
        db.Set<SupportDesk.Domain.Aggregates.Agents.Agent>()
            .AddRange(agentA, agentB);

        await db.SaveChangesAsync();

        var matchingTicket = Ticket.Raise(
            "TCK-0001", "Matching ticket", "Expected result",
            customerA.Id, categoryA.Id, TicketPriority.High, now);

        matchingTicket.AssignTo(agentA.Id, now);

        var otherTicket = Ticket.Raise(
            "TCK-0002", "Other ticket", "Excluded result",
            customerB.Id, categoryB.Id, TicketPriority.Low, now);

        otherTicket.AssignTo(agentB.Id, now);

        db.Set<Ticket>().AddRange(matchingTicket, otherTicket);
        await db.SaveChangesAsync();

        var query = filter switch
        {
            "category" => new TicketQuery { CategoryId = categoryA.Id },
            "customer" => new TicketQuery { CustomerId = customerA.Id },
            "agent" => new TicketQuery { AssignedAgentId = agentA.Id },
            _ => throw new ArgumentOutOfRangeException(nameof(filter))
        };

        var queries = new TicketQueries(db, new FixedClock());

        var result = await queries.GetPagedAsync(
            query, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("TCK-0001", Assert.Single(result.Items).Reference);
    }


}
