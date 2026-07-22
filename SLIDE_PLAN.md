# Real World Entity Framework — Slide Plan

Source deck: `RealWorldEFSQLSat.pptx`. **The two missing sections identified below (Raw SQL to DTO
and DDD: Aggregates & Bounded Contexts) have been built directly into the deck** — it now has
**53 slides** (was 41; 12 new slides added, cloned from existing slide templates so fonts, layout,
and background art match the rest of the deck). Everything from slide 28 onward shifted down to make
room; the numbering below reflects the deck as it stands now. Remaining **[GAP]** notes are smaller
demo details that exist in code but were intentionally *not* turned into their own slides (finer
grained than what a conference talk needs) — flagged here as presenter talking points, not as slides
still owed.

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

## 4. Thank You to Our Event Sponsors
Sponsor acknowledgments (Global, Silver, Bronze, Other).

## 5. Quick Project Overview (section divider)
"Get quick familiarity with the project"

## 6. Quick Details
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
  code in slides 14–17 can apply the same three named filters uniformly to Category, Contributor,
  Genre, JunkToBulkDelete, ItemContributor, and Item.
- `JunkToBulkDelete` model exists solely to drive the Bulk Delete demo (slide 18–22).
- A second bounded context, **Ordering** (`OrderingDbContext`, `Order`/`OrderLine`/`OrderStatus`
  under `EF10_NewFeaturesModels/Ordering`), lives in its own schema (`[Ordering]`) with its own
  migrations history table — now covered in its own section, slides 44–50.

## 7. Migration Options (section divider)
"Safely changing the database"

## 8. How will you run migrations?
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
  Ordering bounded context, now shown directly on slide 49 ("Independent Migrations Per Context").
- `MigrationBuilderSqlResource.cs` — a helper extension (`mb.SqlResource(...)`) that embeds `.sql`
  files as assembly resources and runs them from a migration; used to create `vwGetItemDetailSummaries`
  (now shown on slide 30) and to backup/restore `Contributors`/`ItemContributors` around the
  join-table key change migration (`20250830220400_change-key-for-ic-table.cs`).

## 9. Interceptors (section divider)
"Perform uniform Operations"

## 10. Interceptors — background
- Introduced in EFCore 3: `DbCommandInterceptor`
- Introduced with EFCore 6, improved in EFCore 7
- Problems solved: logging (queries), soft-delete

## 11. Sample Code
`CommandCreating` / `NonQueryExecuting` override samples logging via `ILogger`.

## 12. Demo — Logging and Soft Delete Interceptors
**Code:** `EF10_NewFeatureDemos/NewFeatureDemos/InterceptorsDemos.cs`, backed by
`EF10_NewFeaturesDbLibrary/LoggingCommandInterceptor.cs` and
`EF10_NewFeaturesDbLibrary/SoftDeleteInterceptor.cs`.
- **LoggingInterceptor**: run with `USE_INTERCEPTORS`/`LOG_TO_CONSOLE` env vars toggled to compare
  logging with the interceptor on vs. off (`Program.cs` wires both toggles and a Serilog file
  sink at `C:\Logs\logfile_<timestamp>.txt`).
- **SoftDeleteInterceptor**: adds and then removes a `Category`; the interceptor converts the
  delete into an update that sets `IsDeleted = true` instead of removing the row.

## 13. Query Filters (section divider)

## 14. Named Query Filters
- Problem: disabled filters were all-or-nothing (tenancy, soft delete, active all toggled together)
- Solution: named query filters — toggle only the filter you choose
- Caveat: flag management

## 15. Original Code
`IgnoreQueryFilters()` with no name — all-or-nothing toggle example querying `Categories` for
"Stamps".

## 16. Named Query Filters (code)
`OnModelCreating` calling `.HasQueryFilter("SoftDelete", ...)` / `.HasQueryFilter("Active", ...)`
chained per entity, then selectively disabling via `IgnoreQueryFilters(new[] { "SoftDelete", "Active" })`.

## 17. Demo — Named Query Filters
**Code:** `QueryFiltersDemos.cs`
- **Original Query Operation** (`ShowNonNamedQueryFilters`): seeds an inactive "Stamps" category
  and a deleted+inactive "Coins" category, then contrasts filtered vs. full `IgnoreQueryFilters()`.
- **Query with Named Filters** (`ShowNamedQueryFilters`): walks through all four combinations —
  no filters disabled, both disabled, only `SoftDelete` disabled, only `Active` disabled — showing
  exactly which rows each combination reveals. Note: `Item` also carries a third named filter,
  `"Tenant"` (`i.TenantId == 1`), demonstrating a 3-filter entity, not just the 2-filter examples
  shown on the slide.

## 18. Bulk Update/Delete (section divider)

## 19. Bulk Operations
- Problems: update every item to mark on-sale; delete all unpaid/expired subscriptions
- Solutions: stored procedure (previous approach), Bulk Update (with/without filter), Bulk Delete
  (with/without filter)

## 20. Original Bulk Update (no SPROC)
Fetch-all → loop → mutate → `SaveChangesAsync()` (O(n) round trip pattern).

## 21. EF10 Bulk Update/Bulk Delete
`ExecuteUpdateAsync` / `ExecuteDeleteAsync` as single set-based operations, with and without a
`Where` filter.

## 22. Demo — Bulk Update/Bulk Delete
**Code:** `BulkUpdateDeleteDemos.cs` — five demos, one more than the two slide code samples show:
1. Bulk Update All Items On Sale — **original** loop-based logic (baseline comparison)
2. Bulk Update Items — None On Sale — `ExecuteUpdateAsync`, no filter
3. Bulk Update Movies — All On Sale — `ExecuteUpdateAsync` **with** a `Category.CategoryName` filter
4. Bulk Delete Junk Data by filter — `ExecuteDeleteAsync` filtered on `Name.Contains("BadData")`,
   seeding 10 `JunkToBulkDelete` rows (half "BadData", half "GoodData") the first time it runs
5. Bulk Delete All Junk Data — `ExecuteDeleteAsync` with no filter

## 23. JSON Columns (section divider)

## 24. JSON Columns (NVARCHAR(max))
- Introduced in SQL Server 2016; stored as NVARCHAR(max) until SQL 2025
- Problem: storing unstructured related data (Settings, Addresses) — queryable only via
  extensions/`sqlraw`
- Solution: JSON column + new JSON type — same storage, now queryable/joinable as a full object

## 25. Original Query
`FromSqlRaw("... WHERE JSON_VALUE([Address], '$.City') = {0}", cityToFind)`

## 26. Using the new JSON Type
LINQ `Where(c => c.Address != null && c.Address.City == cityToFind)` — no raw SQL needed.

## 27. Demo — JSON Columns
**Code:** `WorkingWithJSONColumnsDemos.cs`, backed by `Contributor.Address` (owned `Address` JSON
column configured in `InventoryDbContext.OnModelCreating`).
- **Original JSON Column Demo** (`ShowOriginalJSONQueryLogic`): the `FromSqlRaw` + `JSON_VALUE`
  approach from slide 25, filtering city `"Lakeside"`.
- **Get Contributors by City** (`GetContributorsByCity`): the new-type LINQ approach from slide 26.
- **[GAP]** **Get Contributors with Address having 'P.O. Box'** (`GetContributorsWithPOBoxAddress`):
  a third demo not represented on any slide — filters on `Address.AddressLine1.StartsWith("PO Box")`,
  showing a string-prefix predicate translated against the JSON column (not just equality).
- Supporting helper `RandomUpdateContributors`/`CreateRandomAddress` randomly backfills
  `Contributor.Address` (20% chance of a PO Box line) so the by-city and PO-Box demos have real
  data to find — worth mentioning live since it runs automatically before every menu choice.

---

## 28. Raw SQL to DTO (section divider) — **NEW**
**Subtitle:** Report-shaped reads that don't fit LINQ

## 29. When LINQ Isn't the Right Tool — **NEW**
- Problem:
  - Some reads are report-shaped, not entity-shaped — flattened joins across Item / Category /
    Genre / Contributor
  - Modeling every report as a LINQ projection gets unwieldy and hard to tune
- Old options:
  - Hand-roll the join in LINQ every time
  - Or drop to `FromSqlRaw`, mapped into a full tracked / keyless entity type
- Solution:
  - Create a SQL view for the report shape, then read it straight into a plain DTO

## 30. vwGetItemDetailSummaries — **NEW**
View created via an embedded SQL migration script, not hand-edited in SSMS:
```csharp
public static OperationBuilder<SqlOperation> SqlResource(
    this MigrationBuilder mb, string relativeFileName)
{
    var assembly = Assembly.GetAssembly(typeof(MigrationBuilderSqlResource));
    using var stream = assembly?.GetManifestResourceStream(relativeFileName)
        ?? throw new FileNotFoundException("Embedded SQL Resource missing");
    using var ms = new MemoryStream();
    stream.CopyTo(ms);
    var text = new UTF8Encoding(false).GetString(ms.ToArray());
    return mb.Sql(text);
}
```
Same technique also runs `BackupContributorsAndItemContributors_v0.sql` /
`RestoreContributorsAndItemContributors_v0.sql` around the join-table key change migration
(`20250830220400_change-key-for-ic-table.cs`) — worth a callout as a real-world advanced migration
technique alongside slide 8's DbUp/Flyway/Liquibase/Atlas bullet.

## 31. SqlQuery<T> — Typed, Not Tracked — **NEW**
```csharp
public class ItemDetailSummaryDTO
{
    public int ItemId { get; set; }
    public string ItemName { get; set; }
    public string CategoryName { get; set; }
    public string Genres { get; set; }
    public string Contributors { get; set; }
}

List<ItemDetailSummaryDTO> itemsNewWay = await _db.Database
    .SqlQuery<ItemDetailSummaryDTO>($"SELECT * FROM vwGetItemDetailSummaries")
    .ToListAsync();
```
- `SqlQuery<T>` maps straight into a plain DTO — no `DbSet`, no keyless entity type registration,
  no change tracking overhead
- Distinct from `FromSqlRaw`, which requires the target type to already be mapped in the model
- Best fit for dashboards/reports: read-only, shaped for display, easy to tune by editing the view

## 32. Demo — Raw SQL to DTO — **NEW**
**Code:** `RawSqlToDtoDemo.cs`
- Single menu action: queries `vwGetItemDetailSummaries` via `SqlQuery<ItemDetailSummaryDTO>` and
  prints `ItemId | ItemName | CategoryName | Genres | Contributors` for every item
- Talking point: show the view definition next to the DTO class so the audience sees the 1:1 shape
  match that makes this pattern low-friction

---

## 33. LINQ Enhancements and Writing Better Queries (section divider)

## 34. N+1 Queries
Lazy-load-per-row pattern; sneaks into grid logic; acceptable only when incremental load-on-demand
is genuinely wanted.

## 35. Use Includes or Projections
`Include(x => x.Category)` eager-loads to avoid the extra round trip per row.

## 36. Demo — N+1 Queries (Potential Lazy-Loading problems)
**Code:** `LINQEnhancementsDemos.cs`. **[GAP]** the demo menu has *four* entries here, not the two
the slides show:
1. **Show N Plus One Query** — the slide-34 problem, reproduced live
2. **Fix N Plus One Query** — the slide-35 `Include` fix
3. **Prefetch IEnumerable** — a *mis-coded* fix flagged in the code's own comment
   ("this is not linq enhancements, this is just mis-coding the query... but code reviewers are
   important"): pulls a `GroupBy` into `AsAsyncEnumerable()` and finishes ordering/paging
   client-side. Good live example of a well-intentioned rewrite that still drags data into memory
   too early.
4. **One Fetch Only** — the corrected version: projects to an anonymous type, `Distinct`, `OrderBy`,
   `Take` — all still translated to SQL, single round trip. A natural "here's the fix to the fix"
   companion to #3 that isn't called out anywhere in the deck.

## 37. Using GroupJoin with Grouping
"Old way" contributor item-count report: `GroupJoin` → `SelectMany(DefaultIfEmpty)` → `GroupBy` →
`Select` with `Count(x => x.ItemContributor != null)`.

## 38. Using Left-Join
"New way": EF Core's `LeftJoin` operator directly, same `GroupBy`/`Count` shape, far less ceremony.

## 39. Demo — Original GroupJoin vs New Left Join
**Code:** `ShowContributorDataReportOldLogic` / `ShowContributorDataReportNewLogic` in
`LINQEnhancementsDemos.cs`. **[GAP]** `EnsureContributorsWithNoItemsExist()` seeds two
zero-item contributors first, so the demo can visibly prove the `Count(x => ... != null) → 0` claim
from slide 38 instead of just asserting it.

## 40. But Brian – What about the Query Plan!?
Claim to debunk: "LINQ can't reuse a query plan." Reality: LINQ now parameterizes literals into the
generated SQL, enabling plan reuse while still writing LINQ.

## 41. Prove it!
`Contains` used to compile to `WHERE col IN (...)`; now compiles to
`WHERE col LIKE @param ESCAPE ...` — a parameterized, reusable plan.

## 42. (SQL output sample)
`sp_executesql` output showing `@itemFilter_contains` / `@categoryFilter_contains` as real
parameters inside a query that also references the three named query filters (`IsDeleted`,
`IsActive`, `TenantId`) from section 14–17 — a nice callback if presenting in order.

## 43. Demo — Query Plan Reuse
**Code:** `QueryPlanReuse` / `QueryPlanReuseWithParameters` in `LINQEnhancementsDemos.cs`.
- First demo prompts for a category filter only (item filter hard-coded to `"S"`).
- Second demo prompts for **both** item-name and category filters — the one that actually produces
  the two-parameter `sp_executesql` shown in slide 42. Presenter note: run this one, with SQL
  Profiler or console logging on, to match the slide's captured output.

---

## 44. DDD: Aggregates & Bounded Contexts (section divider) — **NEW**
**Subtitle:** Letting the domain model protect itself

## 45. Where Do Business Rules Actually Live? — **NEW**
- The problem:
  - "An order can't be empty." "A line can't have zero quantity." "Can't edit after submit."
  - Rules like these get scattered — a check in the UI, a check in a service, a forgotten constraint
  - Anyone with a `DbSet<OrderLine>` can build an invalid order by construction
- Two DDD tools fix this:
  - **Aggregate** — a root + children only ever modified through the root, so it can guarantee its
    own invariants
  - **Bounded Context** — a model boundary; each context owns its own entities, schema, and
    migration history

## 46. Order — the Aggregate Root — **NEW**
```csharp
public class Order
{
    private readonly List<OrderLine> _lines = new();
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
    public decimal OrderTotal => _lines.Sum(l => l.LineTotal);

    public void AddLine(string itemName, decimal unitPrice, int quantity)
    {
        EnsureDraft();
        if (quantity <= 0) throw new ArgumentException("Quantity must be positive.");

        // INVARIANT: one line per item - adding the same item merges quantities
        var existing = _lines.FirstOrDefault(l => l.ItemName == itemName);
        if (existing is not null) { existing.IncreaseQuantity(quantity); return; }
        _lines.Add(new OrderLine(itemName, unitPrice, quantity));
    }

    public void Submit()
    {
        EnsureDraft();
        if (_lines.Count == 0)
            throw new InvalidOperationException("Cannot submit an order with no lines.");
        Status = OrderStatus.Submitted;
    }
}
```
No public setters anywhere; no `DbSet<OrderLine>` in `OrderingDbContext` — `OrderLine`'s
constructor and mutators are `internal`, so the only way to create or change one is through `Order`.
`OrderTotal` is computed from `Lines`, never persisted — it can never drift out of sync.

## 47. Enforcing Invariants — Not the UI's Job, Not the DB's Job — **NEW**
- Submit an order with no lines
  - → `InvalidOperationException`: "Cannot submit an order with no lines."
- Add a line with quantity 0
  - → `ArgumentException`: "Quantity must be positive."
- Modify an order after `Submit()`
  - → `InvalidOperationException`: "Order {Id} is Submitted and can no longer be modified."
- Add the same item twice
  - → No error — merges into one line, quantity increases

Every rule is enforced in the domain model — no invalid state ever reaches the database.

## 48. OrderingDbContext — a Second Context, Same Database — **NEW**
```csharp
public class OrderingDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
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

## 49. Independent Migrations Per Context — **NEW**
Registered independently of `InventoryDbContext` in `Program.cs`:
```csharp
services.AddDbContext<OrderingDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
        sql.MigrationsHistoryTable("__EFMigrationsHistory", "Ordering")));

dotnet ef database update --context InventoryDbContext ...
dotnet ef database update --context OrderingDbContext ...
```
Direct callback to slide 8 (Migration Options): this *is* the "each bounded context migrates
independently" pattern, running live in this same solution.

## 50. Demo — DDD: Aggregates & Bounded Contexts — **NEW**
**Code:** `DDDOrderingDemos.cs`
1. **Create an Order through the Aggregate Root** — builds an order with two items, adds
   "Star Wars: A New Hope VHS" twice, and shows it collapsed into one line at quantity 3
2. **Try to break the Aggregate's invariants** — runs all three blocked operations from slide 47
   live and prints the caught exception message for each
3. **Show saved Orders** — `OrderingDbContext.Orders.Include(o => o.Lines).AsNoTracking()`
4. **Show Bounded Context isolation** — reflects over `InventoryDbContext.Model.GetEntityTypes()`
   vs. `OrderingDbContext.Model.GetEntityTypes()` side by side, then prints the schema and
   migrations-history split described in slides 48–49

---

## 51. Summary
Make it private · Sleep better · Be compliant · Be resilient · Don't sweat the small stuff.
*(Speaker note on the slide is a leftover Copilot-generated summary about "security and resiliency
on Azure" that doesn't match this talk's content — likely copied from a different deck and worth
fixing before presenting.)*

## 52. Questions/War Stories?
Contact: brian@majorguidancesolutions.com, blgorman@gmail.com, `@blgorman`,
linkedin.com/in/brianlgorman.
*(Speaker note references a different repo, `ServerlessMessagingDemystified`, and the slide itself
still says "Code: TBD" — needs updating to point at this repo,
`RealWorldEntityFramework_NebraskaCode`, before presenting.)*

## 53. Conclusion
Thanks/closing, same contact info, Spotify plug, PEF6/AZ-204 book links.
*(Speaker note also says "Repo: TBD" — same fix needed as slide 52.)*

---

## How the new slides were built

The 12 new slides (28–32 and 44–50) were generated with `python-pptx` by cloning existing slides
from elsewhere in the deck (same layout, same background art/decorations, same fonts and sizes),
then rewriting only the text/code content — rather than building shapes from scratch — so they're
visually indistinguishable from originals:
- Dividers (28, 44) cloned from the "Bulk Update/Delete" and "Interceptors" section-header slides.
- Bullet slides (29, 45, 47) cloned from the "Bulk Operations" Problems:/Solution: slide.
- Code slides (30, 31, 46, 48, 49) cloned from the pure-code "Original Bulk Update" slide, sized
  16–24pt depending on line count (this deck does not use PowerPoint autofit — sizes are set
  explicitly per slide, matching the rest of the deck's convention).
- Demo slides (32, 50) cloned from the "JSON Columns" and "GroupJoin vs Left Join" Demo (Name Card)
  slides, including their decorative artwork.
- The agenda (slide 3) had two new bullets inserted in place: "Raw SQL to DTO" after "JSON Columns
  and JSON Type", and "DDD: Aggregates & Bounded Contexts" after "LINQ Enhancements and Writing
  Better Queries".

All picture relationships across all 53 slides (56 images including nested group pictures) were
verified to resolve correctly after the edit. The original file is gitignored (not tracked by git),
so a pre-edit backup was kept during the session rather than relying on `git checkout` for recovery.
