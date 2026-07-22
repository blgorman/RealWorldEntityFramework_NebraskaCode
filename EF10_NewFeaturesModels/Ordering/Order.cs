namespace EF10_NewFeaturesModels.Ordering;

/// <summary>
/// The Order AGGREGATE ROOT for the Ordering bounded context.
/// - All changes to the aggregate (order lines, status) MUST go through the root
/// - Invariants are enforced here, not in the database or the UI
/// - Note there are no public setters and no way to touch the lines collection directly
/// </summary>
public class Order
{
    private readonly List<OrderLine> _lines = new();

    public int Id { get; private set; }
    public string CustomerName { get; private set; } = null!;
    public DateTime OrderDate { get; private set; }
    public OrderStatus Status { get; private set; }

    //The only view of the children the outside world gets is read-only:
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    //Computed from the children - never stored, never out of sync:
    public decimal OrderTotal => _lines.Sum(l => l.LineTotal);

    // EF Core needs this; keeping it private means consumers can't
    // create an Order in an invalid state
    private Order() { }

    public Order(string customerName)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            throw new ArgumentException("An order requires a customer.", nameof(customerName));
        }

        CustomerName = customerName;
        OrderDate = DateTime.UtcNow;
        Status = OrderStatus.Draft;
    }

    public void AddLine(string itemName, decimal unitPrice, int quantity)
    {
        EnsureDraft();

        if (quantity <= 0)
        {
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));
        }

        //INVARIANT: one line per item - adding the same item merges quantities
        var existing = _lines.FirstOrDefault(l => l.ItemName == itemName);
        if (existing is not null)
        {
            existing.IncreaseQuantity(quantity);
            return;
        }

        _lines.Add(new OrderLine(itemName, unitPrice, quantity));
    }

    public void RemoveLine(string itemName)
    {
        EnsureDraft();
        _lines.RemoveAll(l => l.ItemName == itemName);
    }

    public void Submit()
    {
        EnsureDraft();

        //INVARIANT: an empty order can never be submitted
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("Cannot submit an order with no lines.");
        }

        Status = OrderStatus.Submitted;
    }

    private void EnsureDraft()
    {
        if (Status != OrderStatus.Draft)
        {
            throw new InvalidOperationException($"Order {Id} is {Status} and can no longer be modified.");
        }
    }
}
