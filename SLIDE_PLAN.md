# Real World Entity Framework — Slide Plan

Source deck: `RealWorldEFSQLSat.pptx` — **52 slides**. History: the original deck had 41 slides; two
missing sections (Raw SQL to DTO, DDD: Aggregates & Bounded Contexts) were built directly into it as
12 new slides, cloned from existing slide templates so fonts, layout, and background art match the
rest of the deck, bringing it to 53; the "Thank You to Our Event Sponsors" slide (originally slide 4)
was then removed, bringing it to the current 52. The numbering below reflects the deck exactly as it
stands now — re-verified against the actual file, not carried over from an earlier draft. Remaining
**[GAP]** notes are smaller demo details that exist in code but were intentionally *not* turned into
their own slides (finer grained than what a conference talk needs) — flagged here as presenter
talking points, not as slides still owed.

## 1. Title
**Real World Entity Framework**

## 2. Who am I?
Speaker bio — Brian Gorman: MCT (x6) / MVP (x5), Azure certification list, MS in Computer
Information Systems, BS in Computer Science, 10+ years teaching SQL/C#/VB.NET/Java/Office.
Author of *Practical Entity Framework Core 6* (Apress, 2nd ed.) and an AZ-204 exam companion.
Runs Major Guidance Solutions (contract training, curriculum dev, Azure consulting).
Contact: `@blgorman` / linkedin.com/in/brianlgorman

## 3. Agenda (updated)
- Project Overview
- Migration Options
- Interceptors
- Query Filters
- Bulk Update/Delete
- JSON Columns and JSON Type
- **Raw SQL to DTO** *(new)*
- LINQ Enhancements and Writing Better Queries
- **DDD: Aggregates & Bounded Contexts** *(new)*
- Q&A/War Stories

## 4. Quick Project Overview (section divider)
"Get quick familiarity with the project"

## 5. Quick Details
**Inventory Manager** domain model:
- Items
- Categories (one-to-many)
- Contributors (many-to-many)
- ItemContributor (explicit join)
- Address (JSON as NVARCHAR(MAX)) — defined in `OnModelCreating`, Contributor owns one Address
- Genres (many-to-many, implicit join)
- Tenant (multi-tenancy)

**[GAP] additional model detail not called out on this slide (talking point, not a slide):**
- `DefaultBaseModel` + `IIdentityModel` / `ISoftDeletableModel` / `IActivatableModel` interfaces
  standardize `Id`, `IsDeleted`, `IsActive` across every entity — this is *why* the query-filter
  code in slides 13–16 can apply the same three named filters uniformly to Category, Contributor,
  Genre, JunkToBulkDelete, ItemContributor, and Item.
- `JunkToBulkDelete` model exists solely to drive the Bulk Delete demo (slide 17–21).
- A second bounded context, **Ordering** (`OrderingDbContext`, `Order`/`OrderLine`/`OrderStatus`
  under `EF10_NewFeaturesModels/Ordering`), lives in its own schema (`[Ordering]`) with its own
  migrations history table — now covered in its own section, slides 43–49.

## 6. Migration Options (section divider)
"Safely changing the database"

## 7. How will you run migrations?
- Migrations Factory → run on startup
- Azure Function with ability to inject the context and run migrations
- Automated CI/CD Pipeline — tricky (no public access → GitHub private runner, ACI, Build Agent VM)
- Migration Bundle / `update-database` from command on the runner
- Advanced tooling: DbUp, Flyway, Liquibase, Atlas

**[GAP] talking point:** this slide is conceptual/discussion-only — no corresponding runnable demo —
but the repo does contain real migration artifacts worth pointing to live:
- `EF10_NewFeaturesDbLibrary/Migrations/` — the standard Inventory context migration history
  (10 migrations from initial schema through index tuning).
- `EF10_NewFeaturesDbLibrary/Migrations/Ordering/` — an **independent** migration history for the
  Ordering bounded context, now shown directly on slide 48 ("Independent Migrations Per Context").
- `MigrationBuilderSqlResource.cs` — a helper extension (`mb.SqlResource(...)`) that embeds `.sql`
  files as assembly resources and runs them from a migration; used to create `vwGetItemDetailSummaries`
  (now shown on slide 29) and to backup/restore `Contributors`/`ItemContributors` around the
  join-table key change migration (`20250830220400_change-key-for-ic-table.cs`).

## 8. Interceptors (section divider)
"Perform uniform Operations"

## 9. Interceptors — background
- Introduced in EFCore 3: `DbCommandInterceptor`
- Introduced with EFCore 6, improved in EFCore 7
- Problems solved: logging (queries), soft-delete

## 10. Sample Code
`CommandCreating` / `NonQueryExecuting` override samples logging via `ILogger`.

## 11. Demo — Logging and Soft Delete Interceptors
**Code:** `EF10_NewFeatureDemos/NewFeatureDemos/InterceptorsDemos.cs`, backed by
`EF10_NewFeaturesDbLibrary/LoggingCommandInterceptor.cs` and
`EF10_NewFeaturesDbLibrary/SoftDeleteInterceptor.cs`.
- **LoggingInterceptor**: run with `USE_INTERCEPTORS`/`LOG_TO_CONSOLE` env vars toggled to compare
  logging with the interceptor on vs. off (`Program.cs` wires both toggles and a Serilog file
  sink at `C:\Logs\logfile_<timestamp>.txt`).
- **SoftDeleteInterceptor**: adds and then removes a `Category`; the interceptor converts the
  delete into an update that sets `IsDeleted = true` instead of removing the row.

## 12. Query Filters (section divider)

## 13. Named Query Filters
- Problem: disabled filters were all-or-nothing (tenancy, soft delete, active all toggled together)
- Solution: named query filters — toggle only the filter you choose
- Caveat: flag management

## 14. Original Code
`IgnoreQueryFilters()` with no name — all-or-nothing toggle example querying `Categories` for
"Stamps".

## 15. Named Query Filters (code)
`OnModelCreating` calling `.HasQueryFilter("SoftDelete", ...)` / `.HasQueryFilter("Active", ...)`
chained per entity, then selectively disabling via `IgnoreQueryFilters(new[] { "SoftDelete", "Active" })`.

## 16. Demo — Named Query Filters
**Code:** `QueryFiltersDemos.cs`
- **Original Query Operation** (`ShowNonNamedQueryFilters`): seeds an inactive "Stamps" category
  and a deleted+inactive "Coins" category, then contrasts filtered vs. full `IgnoreQueryFilters()`.
- **Query with Named Filters** (`ShowNamedQueryFilters`): walks through all four combinations —
  no filters disabled, both disabled, only `SoftDelete` disabled, only `Active` disabled — showing
  exactly which rows each combination reveals. Note: `Item` also carries a third named filter,
  `"Tenant"` (`i.TenantId == 1`), demonstrating a 3-filter entity, not just the 2-filter examples
  shown on the slide.

## 17. Bulk Update/Delete (section divider)

## 18. Bulk Operations
- Problems: update every item to mark on-sale; delete all unpaid/expired subscriptions
- Solutions: stored procedure (previous approach), Bulk Update (with/without filter), Bulk Delete
  (with/without filter)

## 19. Original Bulk Update (no SPROC)
Fetch-all → loop → mutate → `SaveChangesAsync()` (O(n) round trip pattern).

## 20. EF10 Bulk Update/Bulk Delete
`ExecuteUpdateAsync` / `ExecuteDeleteAsync` as single set-based operations, with and without a
`Where` filter.

## 21. Demo — Bulk Update/Bulk Delete
**Code:** `BulkUpdateDeleteDemos.cs` — five demos, one more than the two slide code samples show:
1. Bulk Update All Items On Sale — **original** loop-based logic (baseline comparison)
2. Bulk Update Items — None On Sale — `ExecuteUpdateAsync`, no filter
3. Bulk Update Movies — All On Sale — `ExecuteUpdateAsync` **with** a `Category.CategoryName` filter
4. Bulk Delete Junk Data by filter — `ExecuteDeleteAsync` filtered on `Name.Contains("BadData")`,
   seeding 10 `JunkToBulkDelete` rows (half "BadData", half "GoodData") the first time it runs
5. Bulk Delete All Junk Data — `ExecuteDeleteAsync` with no filter

## 22. JSON Columns (section divider)

## 23. JSON Columns (NVARCHAR(max))
- Introduced in SQL Server 2016; stored as NVARCHAR(max) until SQL 2025
- Problem: storing unstructured related data (Settings, Addresses) — queryable only via
  extensions/`sqlraw`
- Solution: JSON column + new JSON type — same storage, now queryable/joinable as a full object

## 24. Original Query
`FromSqlRaw("... WHERE JSON_VALUE([Address], '$.City') = {0}", cityToFind)`

## 25. Using the new JSON Type
LINQ `Where(c => c.Address != null && c.Address.City == cityToFind)` — no raw SQL needed.

## 26. Demo — JSON Columns
**Code:** `WorkingWithJSONColumnsDemos.cs`, backed by `Contributor.Address` (owned `Address` JSON
column configured in `InventoryDbContext.OnModelCreating`).
- **Original JSON Column Demo** (`ShowOriginalJSONQueryLogic`): the `FromSqlRaw` + `JSON_VALUE`
  approach from slide 24, filtering city `"Lakeside"`.
- **Get Contributors by City** (`GetContributorsByCity`): the new-type LINQ approach from slide 25.
- **[GAP]** **Get Contributors with Address having 'P.O. Box'** (`GetContributorsWithPOBoxAddress`):
  a third demo not represented on any slide — filters on `Address.AddressLine1.StartsWith("PO Box")`,
  showing a string-prefix predicate translated against the JSON column (not just equality).
- Supporting helper `RandomUpdateContributors`/`CreateRandomAddress` randomly backfills
  `Contributor.Address` (20% chance of a PO Box line) so the by-city and PO-Box demos have real
  data to find — worth mentioning live since it runs automatically before every menu choice.

---

## 27. Raw SQL to DTO (section divider) — **NEW**
**Subtitle (verbatim on slide):** Report-shaped reads that don't fit LINQ

## 28. When LINQ Isn't the Right Tool — **NEW**
Verbatim bullet text on the slide (trimmed during a later pass to fit the box — shorter than the
original draft):
- Problem:
  - Some reads are report-shaped, not entity-shaped
  - Modeling every report as LINQ gets unwieldy
- Old options:
  - Hand-roll the join in LINQ every time
  - Or drop to FromSqlRaw, mapped into a keyless entity
- Solution:
  - Create a SQL view, read it into a plain DTO

## 29. vwGetItemDetailSummaries — **NEW**
Verbatim code on the slide:
```csharp
View created via an embedded SQL migration script - not hand-edited
in SSMS:

public static OperationBuilder<SqlOperation> SqlResource(
    this MigrationBuilder mb, string relativeFileName)
{
    var assembly = Assembly.GetAssembly(
        typeof(MigrationBuilderSqlResource));
    using var stream = assembly?.GetManifestResourceStream(
        relativeFileName)
        ?? throw new FileNotFoundException("missing");
    using var ms = new MemoryStream();
    stream.CopyTo(ms);
    var text = new UTF8Encoding(false).GetString(ms.ToArray());
    return mb.Sql(text);
}
```
(The first two lines are prose, not code — rendered in the same monospace block. The exception
message was shortened to `"missing"` from the fuller `"Embedded SQL Resource missing"` in the
actual `MigrationBuilderSqlResource.cs` source, purely to keep the line short enough to fit.)

Same technique also runs `BackupContributorsAndItemContributors_v0.sql` /
`RestoreContributorsAndItemContributors_v0.sql` around the join-table key change migration
(`20250830220400_change-key-for-ic-table.cs`) — worth a callout as a real-world advanced migration
technique alongside slide 7's DbUp/Flyway/Liquibase/Atlas bullet.

## 30. SqlQuery<T> — Typed, Not Tracked — **NEW**
Verbatim code on the slide:
```csharp
public class ItemDetailSummaryDTO
{
    public int ItemId { get; set; }
    public string ItemName { get; set; }
    public string CategoryName { get; set; }
    public string Genres { get; set; }
    public string Contributors { get; set; }
}

var items = await _db.Database
    .SqlQuery<ItemDetailSummaryDTO>(
        $"SELECT * FROM vwGetItemDetailSummaries")
    .ToListAsync();
```
- `SqlQuery<T>` maps straight into a plain DTO — no `DbSet`, no keyless entity type registration,
  no change tracking overhead
- Distinct from `FromSqlRaw`, which requires the target type to already be mapped in the model
- Best fit for dashboards/reports: read-only, shaped for display, easy to tune by editing the view

## 31. Demo — Raw SQL to DTO — **NEW**
**Code:** `RawSqlToDtoDemo.cs`
- Single menu action: queries `vwGetItemDetailSummaries` via `SqlQuery<ItemDetailSummaryDTO>` and
  prints `ItemId | ItemName | CategoryName | Genres | Contributors` for every item
- Talking point: show the view definition next to the DTO class so the audience sees the 1:1 shape
  match that makes this pattern low-friction

---

## 32. LINQ Enhancements and Writing Better Queries (section divider)

## 33. N+1 Queries
Lazy-load-per-row pattern; sneaks into grid logic; acceptable only when incremental load-on-demand
is genuinely wanted.

## 34. Use Includes or Projections
`Include(x => x.Category)` eager-loads to avoid the extra round trip per row.

## 35. Demo — N+1 Queries (Potential Lazy-Loading problems)
**Code:** `LINQEnhancementsDemos.cs`. **[GAP]** the demo menu has *four* entries here, not the two
the slides show:
1. **Show N Plus One Query** — the slide-33 problem, reproduced live
2. **Fix N Plus One Query** — the slide-34 `Include` fix
3. **Prefetch IEnumerable** — a *mis-coded* fix flagged in the code's own comment
   ("this is not linq enhancements, this is just mis-coding the query... but code reviewers are
   important"): pulls a `GroupBy` into `AsAsyncEnumerable()` and finishes ordering/paging
   client-side. Good live example of a well-intentioned rewrite that still drags data into memory
   too early.
4. **One Fetch Only** — the corrected version: projects to an anonymous type, `Distinct`, `OrderBy`,
   `Take` — all still translated to SQL, single round trip. A natural "here's the fix to the fix"
   companion to #3 that isn't called out anywhere in the deck.

## 36. Using GroupJoin with Grouping
"Old way" contributor item-count report: `GroupJoin` → `SelectMany(DefaultIfEmpty)` → `GroupBy` →
`Select` with `Count(x => x.ItemContributor != null)`.

## 37. Using Left-Join
"New way": EF Core's `LeftJoin` operator directly, same `GroupBy`/`Count` shape, far less ceremony.

## 38. Demo — Original GroupJoin vs New Left Join
**Code:** `ShowContributorDataReportOldLogic` / `ShowContributorDataReportNewLogic` in
`LINQEnhancementsDemos.cs`. **[GAP]** `EnsureContributorsWithNoItemsExist()` seeds two
zero-item contributors first, so the demo can visibly prove the `Count(x => ... != null) → 0` claim
from slide 37 instead of just asserting it.

## 39. But Brian – What about the Query Plan!?
Claim to debunk: "LINQ can't reuse a query plan." Reality: LINQ now parameterizes literals into the
generated SQL, enabling plan reuse while still writing LINQ.

## 40. Prove it!
`Contains` used to compile to `WHERE col IN (...)`; now compiles to
`WHERE col LIKE @param ESCAPE ...` — a parameterized, reusable plan.

## 41. (SQL output sample)
`sp_executesql` output showing `@itemFilter_contains` / `@categoryFilter_contains` as real
parameters inside a query that also references the three named query filters (`IsDeleted`,
`IsActive`, `TenantId`) from section 13–16 — a nice callback if presenting in order.

## 42. Demo — Query Plan Reuse
**Code:** `QueryPlanReuse` / `QueryPlanReuseWithParameters` in `LINQEnhancementsDemos.cs`.
- First demo prompts for a category filter only (item filter hard-coded to `"S"`).
- Second demo prompts for **both** item-name and category filters — the one that actually produces
  the two-parameter `sp_executesql` shown in slide 41. Presenter note: run this one, with SQL
  Profiler or console logging on, to match the slide's captured output.

---

## 43. DDD: Aggregates & Bounded Contexts (section divider) — **NEW**
**Subtitle (verbatim on slide):** Letting the domain model protect itself

## 44. Where Do Business Rules Actually Live? — **NEW**
Verbatim bullet text on the slide (trimmed from the original draft to fit the box):
- The problem:
  - "An order can't be empty." "Can't edit after submit."
  - Rules get scattered across UI, service, DB checks
  - Anyone with a DbSet<OrderLine> can build an invalid order
- Two DDD tools fix this:
  - Aggregate - root + children only modified through the root
  - Bounded Context - own entities, schema, migrations

## 45. Order — the Aggregate Root — **NEW**
Verbatim code on the slide (trimmed from the original draft — dropped the `Lines`/`OrderTotal`
properties, the private/public constructors, and the `EnsureDraft()` calls to fit the box; the
full versions of all of these are in the real `Order.cs`, this is a deliberately abbreviated
excerpt for the slide):
```csharp
public class Order
{
    private readonly List<OrderLine> _lines = new();
    public void AddLine(string itemName, decimal unitPrice, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.");
        var existing = _lines.FirstOrDefault(l => l.ItemName == itemName);
        if (existing is not null)
        { existing.IncreaseQuantity(quantity); return; }
        _lines.Add(new OrderLine(itemName, unitPrice, quantity));
    }

    public void Submit()
    {
        if (_lines.Count == 0)
            throw new InvalidOperationException("Cannot submit an empty order.");
        Status = OrderStatus.Submitted;
    }
}
```
No public setters anywhere; no `DbSet<OrderLine>` in `OrderingDbContext` — `OrderLine`'s
constructor and mutators are `internal`, so the only way to create or change one is through `Order`.
`OrderTotal` is computed from `Lines`, never persisted — it can never drift out of sync (true of the
real class; not shown in the trimmed slide excerpt above).

## 46. Enforcing Invariants — Not the UI's Job — **NEW**
(Title itself was shortened from the original draft's "...Not the UI's Job, Not the DB's Job" to
fit on one line.) Verbatim bullet text on the slide:
- Submit an order with no lines
  - → InvalidOperationException: "Cannot submit an empty order."
- Add a line with quantity 0
  - → ArgumentException: "Quantity must be positive."
- Modify an order after Submit()
  - → InvalidOperationException: order can no longer be modified
- Add the same item twice
  - → No error - merges into one line, quantity increases

(The exception message here and the one shown in slide 45's code were made consistent — both now
read `"Cannot submit an empty order."` — after an earlier pass had accidentally left them
mismatched.)

## 47. A Second Bounded Context — **NEW**
(Title shortened from the original draft's "OrderingDbContext — a Second Context, Same Database" —
that version wrapped to two lines and visually collided with the code below it.) Verbatim code on
the slide:
```csharp
public class OrderingDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("Ordering");

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasMany(o => o.Lines).WithOne()
                  .IsRequired().OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(o => o.Lines)
                  .UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Ignore(o => o.OrderTotal);
        });
    }
}
```
`OrderingDbContext` maps *only* `Order`/`OrderLine` — it has never heard of `Item`, `Category`, or
`Contributor`. Everything it owns lives in the `[Ordering]` schema.

## 48. Independent Migrations Per Context — **NEW**
Verbatim code on the slide:
```csharp
Registered independently of InventoryDbContext
in Program.cs:

services.AddDbContext<OrderingDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
        sql.MigrationsHistoryTable(
            "__EFMigrationsHistory", "Ordering")));

dotnet ef database update --context InventoryDbContext ...
dotnet ef database update --context OrderingDbContext ...
```
Direct callback to slide 7 (Migration Options): this *is* the "each bounded context migrates
independently" pattern, running live in this same solution.

## 49. Demo — DDD: Aggregates & Bounded Contexts — **NEW**
**Code:** `DDDOrderingDemos.cs`
1. **Create an Order through the Aggregate Root** — builds an order with two items, adds
   "Star Wars: A New Hope VHS" twice, and shows it collapsed into one line at quantity 3
2. **Try to break the Aggregate's invariants** — runs all three blocked operations from slide 46
   live and prints the caught exception message for each
3. **Show saved Orders** — `OrderingDbContext.Orders.Include(o => o.Lines).AsNoTracking()`
4. **Show Bounded Context isolation** — reflects over `InventoryDbContext.Model.GetEntityTypes()`
   vs. `OrderingDbContext.Model.GetEntityTypes()` side by side, then prints the schema and
   migrations-history split described in slides 47–48

---

## 50. Summary
Make it private · Sleep better · Be compliant · Be resilient · Don't sweat the small stuff.
*(Speaker note on the slide is a leftover Copilot-generated summary about "security and resiliency
on Azure" that doesn't match this talk's content — likely copied from a different deck and worth
fixing before presenting.)*

## 51. Questions/War Stories?
Contact: brian@majorguidancesolutions.com, blgorman@gmail.com, `@blgorman`,
linkedin.com/in/brianlgorman.
*(Speaker note references a different repo, `ServerlessMessagingDemystified`, and the slide itself
still says "Code: TBD" — needs updating to point at this repo,
`RealWorldEntityFramework_NebraskaCode`, before presenting.)*

## 52. Conclusion
Thanks/closing, same contact info, Spotify plug, PEF6/AZ-204 book links.
*(Speaker note also says "Repo: TBD" — same fix needed as slide 51.)*

---

## How the new slides were built

The 12 new slides (27–31 and 43–49) were generated with `python-pptx` by cloning existing slides
from elsewhere in the deck (same layout, same background art/decorations, same fonts and sizes),
then rewriting only the text/code content — rather than building shapes from scratch — so they're
visually indistinguishable from originals:
- Dividers (27, 43) cloned from the "Bulk Update/Delete" and "Interceptors" section-header slides.
- Bullet slides (28, 44, 46) cloned from the "Bulk Operations" Problems:/Solution: slide.
- Code slides (29, 30, 45, 47, 48) cloned from the pure-code "Original Bulk Update" slide, sized
  16–24pt depending on line count (this deck does not use PowerPoint autofit — sizes are set
  explicitly per slide, matching the rest of the deck's convention).
- Demo slides (31, 49) cloned from the "JSON Columns" and "GroupJoin vs Left Join" Demo (Name Card)
  slides, including their decorative artwork.
- The agenda (slide 3) had two new bullets inserted in place: "Raw SQL to DTO" after "JSON Columns
  and JSON Type", and "DDD: Aggregates & Bounded Contexts" after "LINQ Enhancements and Writing
  Better Queries".

All picture relationships were verified to resolve correctly after every edit. As of the current
52-slide file (after the Sponsors slide was removed), the deck contains 50 pictures including nested
group pictures, all confirmed resolvable. The original file is gitignored (not tracked by git), so a
pre-edit backup was kept during the session rather than relying on `git checkout` for recovery.
