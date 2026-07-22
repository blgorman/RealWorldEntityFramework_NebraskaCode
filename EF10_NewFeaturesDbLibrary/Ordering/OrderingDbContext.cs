using EF10_NewFeaturesModels.Ordering;
using Microsoft.EntityFrameworkCore;

namespace EF10_NewFeaturesDbLibrary.Ordering;

/// <summary>
/// A second BOUNDED CONTEXT living in the same database as the Inventory context.
/// - It maps ONLY the Ordering aggregate (Order + OrderLine) - it knows nothing
///   about Items, Categories, Contributors, etc.
/// - Everything it owns lives in the [Ordering] schema
/// - It keeps its own migrations in Migrations/Ordering and its own
///   __EFMigrationsHistory table in the [Ordering] schema, so the two contexts
///   can be migrated independently (see the registration in Program.cs)
/// </summary>
public class OrderingDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; }

    public OrderingDbContext(DbContextOptions<OrderingDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        //every table this context owns goes to its own schema
        modelBuilder.HasDefaultSchema("Ordering");

        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("Orders");
            entity.Property(o => o.CustomerName).IsRequired().HasMaxLength(100);
            entity.Property(o => o.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            //the Lines navigation is backed by the private _lines field so all
            //mutations have to go through the aggregate root
            //lines cannot exist without their order (required + cascade delete)
            entity.HasMany(o => o.Lines)
                .WithOne()
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(o => o.Lines)
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            //computed from the lines - never persisted
            entity.Ignore(o => o.OrderTotal);
        });

        modelBuilder.Entity<OrderLine>(entity =>
        {
            entity.ToTable("OrderLines");
            entity.Property(l => l.ItemName).IsRequired().HasMaxLength(100);
            entity.Property(l => l.UnitPrice).HasPrecision(18, 2);
            entity.Ignore(l => l.LineTotal);
        });
    }
}
