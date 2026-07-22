using EF10_NewFeatureDemos.ConsoleHelpers;
using EF10_NewFeaturesDbLibrary;
using EF10_NewFeaturesDbLibrary.Ordering;
using EF10_NewFeaturesModels.Ordering;
using Microsoft.EntityFrameworkCore;

namespace EF10_NewFeatureDemos.NewFeatureDemos;

public class DDDOrderingDemos : IAsyncDemo
{
    private readonly OrderingDbContext _orderingDb;
    private readonly InventoryDbContext _inventoryDb;

    public DDDOrderingDemos(OrderingDbContext orderingDb, InventoryDbContext inventoryDb)
    {
        _orderingDb = orderingDb;
        _inventoryDb = inventoryDb;
    }

    private List<string> GetMenuOptions()
    {
        return new List<string> {
            "Create an Order through the Aggregate Root",
            "Try to break the Aggregate's invariants",
            "Show saved Orders (Ordering context)",
            "Show Bounded Context isolation",
            "Exit"
        };
    }

    private async Task<bool> HandleMenuChoiceAsync(int choice)
    {
        switch (choice)
        {
            case 1:
                await CreateOrderThroughAggregateRoot();
                _orderingDb.ChangeTracker.Clear();
                break;
            case 2:
                await TryToBreakTheInvariants();
                _orderingDb.ChangeTracker.Clear();
                break;
            case 3:
                await ShowSavedOrders();
                _orderingDb.ChangeTracker.Clear();
                break;
            case 4:
                ShowBoundedContextIsolation();
                break;
            case 5:
                return false;
            default:
                Console.WriteLine("Invalid choice. Try again.");
                break;
        }
        return true;
    }

    public async Task RunAsync()
    {
        bool shouldContinue = true;
        while (shouldContinue)
        {
            Console.Clear();

            List<string> menuOptions = GetMenuOptions();

            var menuText = MenuGenerator.GenerateMenu("DDD: Aggregates & Bounded Contexts", "Please select an operation", menuOptions, 40);

            // Show menu and get user choice
            int choice = UserInput.GetInputFromUser(menuText, shouldConfirm: true, min: 1, max: menuOptions.Count);

            try
            {
                shouldContinue = await HandleMenuChoiceAsync(choice);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }
    }

    private async Task CreateOrderThroughAggregateRoot()
    {
        Console.WriteLine("Create an Order through the Aggregate Root");
        Console.WriteLine("The Order is the AGGREGATE ROOT - the only way in.");
        Console.WriteLine("Notice there is no _orderingDb.OrderLines DbSet and no public setters anywhere.");
        Console.WriteLine();

        //the ONLY way to build up an order is through the root's methods
        var order = new Order("Ada Lovelace");
        order.AddLine("1984 Topps Baseball Card Set", 129.99m, 1);
        order.AddLine("Star Wars: A New Hope VHS", 24.50m, 2);

        //adding the same item again does NOT create a duplicate line -
        //the aggregate merges quantities (an invariant the root enforces)
        order.AddLine("Star Wars: A New Hope VHS", 24.50m, 1);

        order.Submit();

        _orderingDb.Orders.Add(order);
        await _orderingDb.SaveChangesAsync();

        Console.WriteLine(ConsolePrinter.PrintFormattedMessage($"Order {order.Id} saved for {order.CustomerName}",
            $"Status: {order.Status} | Total: {order.OrderTotal:C}"));
        Console.WriteLine(ConsolePrinter.PrintBoxedList(order.Lines.ToList(),
            l => $"{l.ItemName} | {l.Quantity} x {l.UnitPrice:C} = {l.LineTotal:C}"));
        Console.WriteLine("Note: 'Star Wars' shows quantity 3 on ONE line - the aggregate merged the duplicate add.");

        UserInput.WaitForUserInput();
    }

    private async Task TryToBreakTheInvariants()
    {
        Console.WriteLine("Try to break the Aggregate's invariants");
        Console.WriteLine("Business rules live in the aggregate root - not in the UI, not in the database.");
        Console.WriteLine();

        //1 - you cannot submit an empty order
        try
        {
            var emptyOrder = new Order("Charles Babbage");
            emptyOrder.Submit();
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"BLOCKED - Submit an empty order: {ex.Message}");
        }

        //2 - you cannot add a line with a non-positive quantity
        try
        {
            var order = new Order("Grace Hopper");
            order.AddLine("COBOL Reference Manual", 15.00m, 0);
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"BLOCKED - Add a line with quantity 0: {ex.Message}");
        }

        //3 - you cannot modify an order after it has been submitted
        var submitted = new Order("Alan Turing");
        submitted.AddLine("Enigma Machine Replica", 499.99m, 1);
        submitted.Submit();
        _orderingDb.Orders.Add(submitted);
        await _orderingDb.SaveChangesAsync();

        try
        {
            submitted.AddLine("Bombe Blueprint", 89.99m, 1);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"BLOCKED - Modify a submitted order: {ex.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("Every rule was enforced by the aggregate root - no invalid state ever reached the database.");

        UserInput.WaitForUserInput();
    }

    private async Task ShowSavedOrders()
    {
        Console.WriteLine("Orders currently in the [Ordering].[Orders] table:");

        var orders = await _orderingDb.Orders
                            .Include(o => o.Lines)
                            .AsNoTracking()
                            .ToListAsync();

        if (orders.Count == 0)
        {
            Console.WriteLine("No orders yet - run the 'Create an Order' demo first.");
        }
        else
        {
            Console.WriteLine(ConsolePrinter.PrintBoxedList(orders,
                o => $"Order {o.Id} | {o.CustomerName} | {o.Status} | {o.Lines.Count} line(s) | {o.OrderTotal:C}"));
        }

        UserInput.WaitForUserInput();
    }

    private void ShowBoundedContextIsolation()
    {
        Console.WriteLine("Bounded Context isolation - two contexts, one database");
        Console.WriteLine();

        var inventoryEntities = _inventoryDb.Model.GetEntityTypes()
                                    .Select(e => e.ClrType.Name)
                                    .OrderBy(n => n)
                                    .ToList();
        var orderingEntities = _orderingDb.Model.GetEntityTypes()
                                    .Select(e => e.ClrType.Name)
                                    .OrderBy(n => n)
                                    .ToList();

        Console.WriteLine(ConsolePrinter.PrintFormattedMessage("InventoryDbContext maps:", string.Join(", ", inventoryEntities)));
        Console.WriteLine(ConsolePrinter.PrintFormattedMessage("OrderingDbContext maps:", string.Join(", ", orderingEntities)));

        Console.WriteLine();
        Console.WriteLine("Isolation in practice:");
        Console.WriteLine(" - Neither context can see (or accidentally query/migrate) the other's entities");
        Console.WriteLine(" - Inventory tables live in [dbo]; Ordering tables live in the [Ordering] schema");
        Console.WriteLine(" - Inventory migrations:  Migrations/            -> [dbo].[__EFMigrationsHistory]");
        Console.WriteLine(" - Ordering migrations:   Migrations/Ordering/   -> [Ordering].[__EFMigrationsHistory]");
        Console.WriteLine();
        Console.WriteLine("Each context migrates independently:");
        Console.WriteLine("  dotnet ef database update --context InventoryDbContext ...");
        Console.WriteLine("  dotnet ef database update --context OrderingDbContext ...");

        UserInput.WaitForUserInput();
    }
}
