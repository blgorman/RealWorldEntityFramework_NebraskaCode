using EF10_NewFeatureDemos.ConsoleHelpers;
using EF10_NewFeaturesDbLibrary;
using EF10_NewFeaturesDbLibrary.Ordering;

namespace EF10_NewFeatureDemos;

public class Application
{
    private readonly MainMenu _menu;

    public Application(InventoryDbContext db, OrderingDbContext orderingDb)
    {
        _menu = new MainMenu(db, orderingDb);
    }

    public async Task DoWork()
    {
        Console.WriteLine("Press any key to continue...");
        Console.ReadKey();

        Console.WriteLine("Welcome to the New Feature Demos");

        await _menu.ShowAsync();

        Console.WriteLine("Thank you for using the New Feature Demos!");
    }
}
