
using EF10_NewFeatureDemos.ConsoleHelpers;
using EF10_NewFeaturesDbLibrary;
using EF10_NewFeaturesModels;
using Microsoft.EntityFrameworkCore;

namespace EF10_NewFeatureDemos.NewFeatureDemos;

public class InterceptorsDemos : IAsyncDemo
{
    private InventoryDbContext _db;

    public InterceptorsDemos(InventoryDbContext context)
    {
        _db = context;
    }

    private List<string> GetMenuOptions()
    {
        return new List<string> {
            "LoggingInterceptor",
            "SoftDeleteInterceptor",
            "Exit"
        };
    }

    private async Task<bool> HandleMenuChoiceAsync(int choice)
    {

        switch (choice)
        {
            case 1:
                await ShowLoggingInterceptor();
                _db.ChangeTracker.Clear(); // Detach all tracked entities
                break;
            case 2:
                await ShowSoftDeleteInterceptor();
                _db.ChangeTracker.Clear(); // Detach all tracked entities
                break;
            case 3:
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

            var menuText = MenuGenerator.GenerateMenu("Interceptors Demos", "Please select an operation", menuOptions, 40);

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

    private async Task ShowLoggingInterceptor()
    {
        Console.WriteLine("Logging Interceptor");
        Console.WriteLine("Run the program without the interceptors turned on and check the logs.");
        Console.WriteLine("Then run the program with the interceptors turned on and check the logs again.");
        Console.WriteLine("With the interceptor on, you should see SQL command messages prefixed with [LoggingCommandInterceptor].");
        Console.WriteLine("The normal EF command logs are suppressed so the custom interceptor output is easy to identify.");

        Console.WriteLine("Executing query...");
        Console.WriteLine("----- INTERCEPTOR LOG OUTPUT START -----");

        var items = await _db.Items
                        .Where(i => EF.Functions.Like(i.ItemName, "%lord%"))
                        .OrderBy(i => i.ItemName)
                        .Select(i => new { i.Id, i.ItemName })
                        .ToListAsync();

        Console.WriteLine("----- INTERCEPTOR LOG OUTPUT END -------");
        Console.WriteLine("Query results:");
        Console.WriteLine(ConsolePrinter.PrintBoxedList(items, j => $"{j.Id}: {j.ItemName}"));

        Console.WriteLine("Logging Interceptor Completed");

        UserInput.WaitForUserInput();
    }

    private async Task ShowSoftDeleteInterceptor()
    {
        Console.WriteLine("Soft Delete Interceptor");
        Console.WriteLine("When the interceptors are on, all delete operations will be converted to modification (update) and set the IsDeleted flag to '1'");
        Console.WriteLine("With interceptors OFF, the test row will be physically deleted.");
        Console.WriteLine("With interceptors ON, the test row will remain with IsDeleted = true.");


        Console.WriteLine(ConsolePrinter.PrintFormattedMessage("All Categories with filter status", "All Categories"));
        var categories = await _db.Categories.ToListAsync();
        Console.WriteLine(ConsolePrinter.PrintBoxedList(categories
            , c => $"{c.Id}: {c.CategoryName} [IS Deleted: {c.IsDeleted}] - [Is Active {c.IsActive}]"));
        UserInput.WaitForUserInput();

        var ts = DateTime.Now.ToString("yyyyMMddHHmmss");
        var cat = new Category()
        {
            CategoryName = $"Test Category [{ts}]",
            IsActive = true
        };

        //add it
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        var categoryId = cat.Id;

        //now delete it (should be a soft delete)
        //(put a breakpoint on SaveChangesAsync in the interceptor to see it hit)
        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();

        // Ensure the verification comes from the database rather than a tracked entity.
        _db.ChangeTracker.Clear();

        // The normal Category query hides soft-deleted rows, so bypass its filters to
        // distinguish a physical delete from a soft delete.
        var categoriesAfterDelete = await _db.Categories
            .IgnoreQueryFilters()
            .OrderBy(c => c.Id)
            .ToListAsync();

        var deletedCategory = categoriesAfterDelete.SingleOrDefault(c => c.Id == categoryId);

        Console.WriteLine(ConsolePrinter.PrintFormattedMessage(
            $"All database rows after deleting category ID {categoryId}",
            "All Categories After Delete"));
        Console.WriteLine(ConsolePrinter.PrintBoxedList(categoriesAfterDelete,
            c => $"{c.Id}: {c.CategoryName} [IS Deleted: {c.IsDeleted}] - [Is Active {c.IsActive}]"));

        if (deletedCategory is null)
        {
            Console.WriteLine("PHYSICAL DELETE: The row no longer exists in the database (interceptors OFF).");
        }
        else if (deletedCategory.IsDeleted)
        {
            Console.WriteLine(
                $"SOFT DELETE: The row still exists with IsDeleted = {deletedCategory.IsDeleted} (interceptors ON).");
            Console.WriteLine(
                $"{deletedCategory.Id}: {deletedCategory.CategoryName} [IsDeleted: {deletedCategory.IsDeleted}] - [IsActive: {deletedCategory.IsActive}]");

            var visibleWithFilters = await _db.Categories.AnyAsync(c => c.Id == categoryId);
            Console.WriteLine($"Visible through the normal filtered query: {visibleWithFilters}");
        }
        else
        {
            Console.WriteLine(
                $"UNEXPECTED RESULT: The row still exists, but IsDeleted = {deletedCategory.IsDeleted}.");
        }

        Console.WriteLine("Soft Delete Interceptor Completed");

        UserInput.WaitForUserInput();
    }
}
