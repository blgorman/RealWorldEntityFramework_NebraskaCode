# Real World Entity Framework — Demo Walkthrough

This guide follows `RealWorldEFSQLSat.pptx` and the console application's menu order. Use it as a presenter runbook: each section identifies the related slides, the menu action, the code change being demonstrated, and the visible proof to call out.

## Before the presentation

The application now applies pending migrations for both contexts during startup:

```csharp
await inventoryDb.Database.MigrateAsync();
await orderingDb.Database.MigrateAsync();
```

The contexts share one SQL Server database but maintain separate histories:

- `InventoryDbContext` uses `[dbo].[__EFMigrationsHistory]`.
- `OrderingDbContext` uses `[Ordering].[__EFMigrationsHistory]`.

Set the interceptor options before starting the process. They are read once at startup, so changing them requires restarting the application.

```powershell
$env:USE_INTERCEPTORS = 'true'
$env:LOG_TO_CONSOLE = 'true'
dotnet run --project EF10_NewFeatureDemos
```

For the OFF comparison:

```powershell
$env:USE_INTERCEPTORS = 'false'
$env:LOG_TO_CONSOLE = 'true'
dotnet run --project EF10_NewFeatureDemos
```

At startup, confirm the banner reports the expected interceptor and console-logging states. The application pauses after migrations and before showing the main menu.

## Presentation and demo route

| Slides | Topic | Main menu | Recommended submenu order |
|---:|---|---:|---|
| 1-3 | Intro | — | Introduction
| 4-5 | Project Overview | — | Inventory Manager System |
| 6-7 | Migrations Options | 1 | Options for getting migrations
| 8-11 | Interceptors | 2 | Logging, then Soft Delete |
| 12-16 | Named query filters | 3 | Original, then Named Filters |
| 17-21 | Bulk update/delete | 4 | Run actions 1 through 5 |
| 22-26 | JSON columns | 5 | Original JSON, City LINQ, PO Box LINQ |
| 27-31 | Raw SQL to DTO | 7 | Run the single demo |
| 32-42 | LINQ improvements | 6 | N+1 pair, fetch pair, join pair, parameter pair |
| 43-49 | DDD and bounded contexts | 8 | Run actions 1 through 4 |
| 50 | Summary | — | Recap the seven code-level takeaways |

## Slides 5–8 — Project and migration strategy

### What changed

The application has two independently configured contexts and now migrates both automatically before entering the demo menu.

```csharp
services.AddDbContext<InventoryDbContext>(options =>
    options.UseSqlServer(connectionString));

services.AddDbContext<OrderingDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
        sql.MigrationsHistoryTable("__EFMigrationsHistory", "Ordering")));
```

### Callout

The bounded contexts share a physical database without sharing models, schemas, or migration history. `MigrateAsync()` is idempotent: it applies pending migrations rather than recreating the database.

## Slides 9–12 — Command logging and soft-delete interceptors

Open main menu **2 — Interceptors and Logging**.

### Demo 1: LoggingInterceptor

#### Baseline

EF Core can normally log commands through the `Microsoft.EntityFrameworkCore.Database.Command` category, but this application suppresses its informational output:

```csharp
.MinimumLevel.Override(
    "Microsoft.EntityFrameworkCore.Database.Command",
    LogEventLevel.Warning)
```

This removes general EF informational noise.

#### Interceptor approach

`LoggingCommandInterceptor` derives from the actual EF Core command interceptor:

```csharp
public class LoggingCommandInterceptor : DbCommandInterceptor
```

It logs selected commands under the independent `EFCustomInterceptor` category:

```csharp
public override async ValueTask<InterceptionResult<DbDataReader>>
    ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
{
    _logger.LogInformation(
        "[LoggingCommandInterceptor] Executing Query (Async): {CommandText}",
        command.CommandText);

    return await base.ReaderExecutingAsync(
        command, eventData, result, cancellationToken);
}
```

The interceptor is added only when `USE_INTERCEPTORS=true`:

```csharp
options.AddInterceptors(
    serviceProvider.GetRequiredService<LoggingCommandInterceptor>(),
    serviceProvider.GetRequiredService<SoftDeleteInterceptor>());
```

#### Visible proof

With interceptors ON and console logging ON, the query area displays direct callback messages:

```text
>>> DbCommandInterceptor.CommandCreating CALLED <<<
>>> DbCommandInterceptor.ReaderExecutingAsync CALLED <<<
```

The SQL log entry is prefixed with `[LoggingCommandInterceptor]`. The same messages are written to the startup banner's `C:\Logs\...` file.

`CommandText` contains the SQL and parameter placeholders. The current interceptor does **not** enumerate `command.Parameters`, so parameter values are not logged.

### Demo 2: SoftDeleteInterceptor

#### Original delete behavior

Without the interceptor, this produces a physical `DELETE`:

```csharp
_db.Categories.Remove(category);
await _db.SaveChangesAsync();
```

#### Interceptor behavior

`SoftDeleteInterceptor` derives from `SaveChangesInterceptor`. Before EF generates SQL, it turns deleted soft-deletable entities into modified entities:

```csharp
if (entry.State == EntityState.Deleted)
{
    entry.State = EntityState.Modified;
    entry.CurrentValues[nameof(ISoftDeletableModel.IsDeleted)] = true;
}
```

EF consequently sends an `UPDATE` that sets `IsDeleted`, not a physical `DELETE`.

#### Visible proof

The demo remembers the inserted category ID, clears the change tracker, and reads all stored category rows for verification:

```csharp
_db.ChangeTracker.Clear();

var categoriesAfterDelete = await _db.Categories
    .IgnoreQueryFilters()
    .OrderBy(c => c.Id)
    .ToListAsync();
```

- Interceptors OFF: the test category is absent and the demo reports `PHYSICAL DELETE`.
- Interceptors ON: the category remains in the list with `IsDeleted: True` and the demo reports `SOFT DELETE`.

The verification bypasses the normal soft-delete filter only so the stored row can be inspected. It does not change the filter configuration.

## Slides 13–17 — Original filters versus named query filters

Open main menu **3 — Query Filters**.

The demo creates two recognizable records:

- `Stamps`: inactive, not deleted.
- `Coins`: inactive and deleted.

### Demo 1: Original Query Operation

Before named filters, disabling query filters was all-or-nothing:

```csharp
var categories = await _db.Categories
    .IgnoreQueryFilters()
    .ToListAsync();
```

This reveals both inactive and soft-deleted rows. The query cannot say “show deleted rows but continue hiding inactive rows.”

### Demo 2: Query with Named Filters

EF10 assigns an independent name to each filter:

```csharp
modelBuilder.Entity<Category>()
    .HasQueryFilter("SoftDelete", c => !c.IsDeleted)
    .HasQueryFilter("Active", c => c.IsActive);
```

The query can then disable exactly the required filter:

```csharp
// Disable both filters.
_db.Categories.IgnoreQueryFilters(new[] { "SoftDelete", "Active" });

// Include soft-deleted rows, but continue hiding inactive rows.
_db.Categories.IgnoreQueryFilters(new[] { "SoftDelete" });

// Include inactive rows, but continue hiding soft-deleted rows.
_db.Categories.IgnoreQueryFilters(new[] { "Active" });
```

### Visible proof

Run the named-filter action and stop at each result set:

1. All filters active: neither `Stamps` nor `Coins` appears.
2. Both disabled: both appear.
3. Only `SoftDelete` disabled: inactive rows remain hidden, so neither appears.
4. Only `Active` disabled: `Stamps` appears; deleted `Coins` remains hidden.

`Item` demonstrates that the pattern scales beyond two filters by also defining a named `Tenant` filter.

## Slides 18–22 — Fetch-and-loop versus set-based bulk operations

Open main menu **4 — Bulk Update/Delete**.

### Demo 1: Original bulk update

The original approach retrieves every matching entity, changes each tracked object, and then saves:

```csharp
var items = await _db.Items.ToListAsync();

foreach (var item in items)
{
    item.IsOnSale = true;
}

await _db.SaveChangesAsync();
```

Call out the unnecessary entity materialization, tracking, loop, and generated per-row updates.

### Demos 2 and 3: ExecuteUpdateAsync

The new approach expresses the update as one set-based database operation:

```csharp
var count = await _db.Items.ExecuteUpdateAsync(setters =>
    setters.SetProperty(i => i.IsOnSale, i => false));
```

The same API supports a filter without changing the update shape:

```csharp
var count = await _db.Items
    .Where(i => i.Category.CategoryName == "Movie")
    .ExecuteUpdateAsync(setters =>
        setters.SetProperty(i => i.IsOnSale, i => true));
```

### Demos 4 and 5: ExecuteDeleteAsync

Filtered delete:

```csharp
var count = await _db.JunkToBulkDeletes
    .Where(j => j.Name.Contains("BadData"))
    .ExecuteDeleteAsync();
```

Unfiltered delete:

```csharp
var count = await _db.JunkToBulkDeletes.ExecuteDeleteAsync();
```

### Visible proof

- The methods return the number of affected rows.
- The filtered update changes only movies.
- The filtered delete removes `BadData-*` rows and preserves `GoodData-*` rows.
- The final delete removes the remaining rows.

`ExecuteUpdateAsync` and `ExecuteDeleteAsync` execute immediately and do not use `SaveChanges()`. Consequently, a `SaveChangesInterceptor` is not part of these bulk-operation paths.

## Slides 23–27 — Raw JSON SQL versus LINQ over mapped JSON

Open main menu **5 — Work with JSON Columns**.

`Contributor.Address` is configured as an owned JSON object:

```csharp
entity.OwnsOne(c => c.Address, address =>
{
    address.ToJson();
});
```

The demo backfills missing addresses before each action so the queries have data.

### Demo 1: Original JSON Column Demo

The original query knows SQL Server's JSON syntax and JSON path:

```csharp
var contributors = await _db.Contributors
    .FromSqlRaw(
        "SELECT * FROM Contributors " +
        "WHERE JSON_VALUE([Address], '$.City') = {0}",
        cityToFind)
    .ToListAsync();
```

### Demo 2: Get Contributors by City

The mapped approach remains in strongly typed LINQ:

```csharp
var contributors = await _db.Contributors
    .Where(c => c.Address != null && c.Address.City == cityToFind)
    .ToListAsync();
```

### Demo 3: Get Contributors with a PO Box

The same mapping translates a string operation against a JSON member:

```csharp
var contributors = await _db.Contributors
    .Where(c => c.Address != null &&
                c.Address.AddressLine1 != null &&
                c.Address.AddressLine1.StartsWith("PO Box"))
    .ToListAsync();
```

### Callout

The improvement is not merely shorter SQL. The compiler now understands the member path, refactoring is safer, and query composition remains in LINQ.

## Slides 28–32 — Raw SQL directly into a DTO

Open main menu **7 — Raw SQL to DTO**.

### Older options

A report-shaped read typically required one of these compromises:

- Repeat a large multi-table LINQ projection wherever the report is needed.
- Use `FromSqlRaw` with a type already mapped in the EF model, often as a keyless entity.

### SqlQuery&lt;T&gt; approach

The migration creates `vwGetItemDetailSummaries`, and the application maps its columns directly into a plain DTO:

```csharp
var items = await _db.Database
    .SqlQuery<ItemDetailSummaryDTO>(
        $"SELECT * FROM vwGetItemDetailSummaries")
    .ToListAsync();
```

`ItemDetailSummaryDTO` is not a `DbSet`, is not tracked, and does not need keyless-entity registration.

### Visible proof

The output is already shaped for the report:

```text
ItemId | ItemName | CategoryName | Genres | Contributors
```

Point out the one-to-one match between the view's selected columns and the DTO properties.

## Slides 33–36 — N+1 and keeping work in SQL

Open main menu **6 — LINQ Enhancements**.

### Demos 1 and 2: N+1 shape versus Include

Potential N+1 shape:

```csharp
var items = await _db.Items.ToListAsync();

foreach (var item in items)
{
    var categoryName = item.Category?.CategoryName;
}
```

Eager-loading fix:

```csharp
var items = await _db.Items
    .Include(i => i.Category)
    .ToListAsync();
```

The code change makes the related-data requirement explicit in the database query.

Presenter accuracy note: the current `Program.cs` does not call `UseLazyLoadingProxies()`. Without lazy loading enabled, the first action demonstrates missing related data rather than generating a literal extra query per item. The `Include` action still demonstrates the correct eager-loading shape.

### Demos 3 and 4: Premature client evaluation versus one translated query

The problematic version crosses into client-side processing too early:

```csharp
var items = await _db.Items
    .Include(i => i.Category)
    .GroupBy(...)
    .AsAsyncEnumerable()
    .Select(...)
    .OrderBy(...)
    .Take(10)
    .ToListAsync();
```

The corrected version keeps projection, distinctness, ordering, and limiting in the `IQueryable` pipeline until final materialization:

```csharp
var items = await _db.Items
    .Select(i => new { i.Id, i.ItemName, i.Category.CategoryName })
    .Distinct()
    .OrderBy(i => i.CategoryName)
    .Take(10)
    .ToListAsync();
```

Call out the placement of `ToListAsync()`: it is the single point where execution occurs.

## Slides 37–39 — GroupJoin ceremony versus LeftJoin

Continue in main menu **6 — LINQ Enhancements**, actions 5 and 6.

### Demo 5: Original GroupJoin

The original left-join pattern requires both `GroupJoin` and `SelectMany(DefaultIfEmpty())`:

```csharp
var report = _db.Contributors
    .GroupJoin(
        _db.ItemContributors,
        contributor => contributor.Id,
        itemContributor => itemContributor.ContributorId,
        (contributor, matches) => new { contributor, matches })
    .SelectMany(
        row => row.matches.DefaultIfEmpty(),
        (row, match) => new
        {
            Contributor = row.contributor,
            ItemContributor = match
        });
```

### Demo 6: LeftJoin

The new operator expresses the same intent directly:

```csharp
var report = _db.Contributors.LeftJoin(
    _db.ItemContributors,
    contributor => contributor.Id,
    itemContributor => itemContributor.ContributorId,
    (contributor, itemContributor) => new
    {
        Contributor = contributor,
        ItemContributor = itemContributor
    });
```

Both versions then group contributors and count non-null matches.

### Visible proof

The demo seeds two contributors with no items. Both query shapes return those contributors with an item count of zero, proving that the shorter `LeftJoin` retains left-join behavior.

## Slides 40–43 — Parameterized LINQ and query-plan reuse

Continue in main menu **6 — LINQ Enhancements**, actions 7 and 8.

The important query is action 8, where both values come from user input:

```csharp
.Where(i => i.ItemName.Contains(itemFilter) &&
            i.Category.CategoryName.Contains(categoryFilter))
```

### Visible proof

With interceptors and console logging enabled, point at the generated SQL parameter placeholders rather than only the result rows. Re-run with different input and show that the SQL command shape stays stable while parameter values change.

The current command interceptor logs `CommandText`, so it shows parameter placeholders but not parameter values.

## Slides 43–49 — Aggregate roots and bounded contexts

Open main menu **8 — DDD: Aggregates & Bounded Contexts**.

### Demo 1: Create through the aggregate root

The original anemic-model risk would expose setters and a separate `DbSet<OrderLine>`, allowing callers to construct invalid combinations.

The aggregate approach exposes behavior instead:

```csharp
var order = new Order("Ada Lovelace");
order.AddLine("1984 Topps Baseball Card Set", 129.99m, 1);
order.AddLine("Star Wars: A New Hope VHS", 24.50m, 2);
order.AddLine("Star Wars: A New Hope VHS", 24.50m, 1);
order.Submit();
```

`Order.Lines` is read-only, `OrderLine` mutation is internal, and adding the same item merges quantities.

### Demo 2: Try to break invariants

The aggregate blocks invalid state before persistence:

- Submitting an empty order throws.
- Adding quantity zero throws.
- Modifying a submitted order throws.
- Adding a duplicate item merges quantity rather than creating another line.

### Demo 3: Show saved orders

```csharp
var orders = await _orderingDb.Orders
    .Include(o => o.Lines)
    .AsNoTracking()
    .ToListAsync();
```

Call out that `OrderTotal` is computed from its lines and is not stored independently.

### Demo 4: Show bounded-context isolation

`InventoryDbContext` maps inventory entities in `dbo`. `OrderingDbContext` maps only `Order` and `OrderLine` in the `Ordering` schema.

```csharp
modelBuilder.HasDefaultSchema("Ordering");
```

The demo prints both contexts' model entity lists and their separate migration locations/history tables.

## Slide 50 — Summary

The updated summary slide now matches the demonstrated code:

1. Automate migrations for each `DbContext`.
2. Use interceptors for logging and soft delete.
3. Disable named query filters selectively.
4. Prefer set-based bulk operations.
5. Query JSON and report DTOs directly.
6. Write clearer LINQ and verify generated SQL.
7. Protect invariants with aggregates and bounded contexts.

## Fast rehearsal checklist

- Confirm SQL Server is reachable and the startup migrations finish.
- Confirm the startup banner says `Interceptors: ON` and `Console logging: ON` for the primary run.
- Run Soft Delete before Named Query Filters so a `Test Category [...]` row exists.
- Run the original operation before its replacement so the contrast is visible.
- For query-plan reuse, run action 8 and point at SQL parameter placeholders.
- Run “Create an Order” before “Show saved Orders.”
- Restart with `USE_INTERCEPTORS=false` only when demonstrating the physical-delete comparison.
