namespace EF10_NewFeaturesModels.Ordering;

/// <summary>
/// A child ENTITY inside the Order aggregate.
/// It has identity (Id) but no meaning outside its Order, so the
/// constructors and mutators are internal - only the aggregate root can use them.
/// </summary>
public class OrderLine
{
    public int Id { get; private set; }
    public string ItemName { get; private set; } = null!;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    // EF Core needs this
    private OrderLine() { }

    internal OrderLine(string itemName, decimal unitPrice, int quantity)
    {
        if (string.IsNullOrWhiteSpace(itemName))
        {
            throw new ArgumentException("A line requires an item name.", nameof(itemName));
        }
        if (unitPrice < 0)
        {
            throw new ArgumentException("Unit price cannot be negative.", nameof(unitPrice));
        }

        ItemName = itemName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    internal void IncreaseQuantity(int quantity)
    {
        Quantity += quantity;
    }
}
