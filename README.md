# Real World Entity Framework

A .NET 10 / EF Core 10 console application built for a conference talk on real-world Entity
Framework Core patterns. It pairs a small "Inventory Manager" domain (Items, Categories,
Contributors, Genres) with a second, independently-migrated "Ordering" bounded context, and walks
through eight feature areas via an interactive console menu — from migration strategy through
interceptors, query filters, bulk operations, JSON columns, raw SQL projections, LINQ query-plan
reuse, and DDD aggregate roots.

## Slides

[RealWorldEFSQLSat.pptx](https://talkimages.blob.core.windows.net/nebraskacode2026/RealWorldEFSQLSat.pptx)

Two companion documents in this repo go deeper than the slides:
- [`SLIDE_PLAN.md`](SLIDE_PLAN.md) — a slide-by-slide breakdown of the deck, cross-referenced
  against the actual code, including a few demo details that exist in code but didn't make it onto
  a slide.
- [`DEMO_WALKTHROUGH.md`](DEMO_WALKTHROUGH.md) — a presenter runbook mapping each slide range to the
  matching console menu action, the code change being demonstrated, and the visible proof to call
  out live.

## Project structure

| Project | Purpose |
|---|---|
| `EF10_NewFeatureDemos` | The console application: `Program.cs` wires up hosting/DI/logging, `Application`/`MainMenu` drive the menu loop, and `NewFeatureDemos/` holds one class per feature area. |
| `EF10_NewFeaturesDbLibrary` | `InventoryDbContext` (the main domain) and `OrderingDbContext` (the second bounded context), both interceptors, seed data, and both contexts' `Migrations/` folders. |
| `EF10_NewFeaturesModels` | POCOs for the Inventory domain, the `Ordering` aggregate (`Order`/`OrderLine`/`OrderStatus`), DTOs, and the shared `IIdentityModel`/`ISoftDeletableModel`/`IActivatableModel` interfaces every entity implements. |

Two `DbContext`s, one database: `InventoryDbContext` owns the `dbo` schema with its own
`__EFMigrationsHistory` table, and `OrderingDbContext` owns an `Ordering` schema with its own
`__EFMigrationsHistory` table in that schema — each migrates independently.

## Prerequisites

- .NET 10 SDK
- SQL Server reachable from your machine (local or remote)

## Setup

1. Point `EF10_NewFeatureDemos/appsettings.json` (or an `appsettings.{Environment}.json` /
   user-secret override) at your SQL Server instance:
   ```json
   {
     "ConnectionStrings": {
       "InventoryDbConnection": "Server=localhost;Database=RealWorldEFCore;User Id=sa;Password=your-password-here;TrustServerCertificate=True;MultipleActiveResultSets=False;"
     }
   }
   ```
2. Run it:
   ```powershell
   dotnet run --project EF10_NewFeatureDemos
   ```

On startup the app applies pending migrations for **both** contexts automatically
(`inventoryDb.Database.MigrateAsync()` then `orderingDb.Database.MigrateAsync()`) — there's no
separate `dotnet ef database update` step to remember. The database is created on first run if it
doesn't already exist.

### Configuration toggles

Read once at process start, so changing any of these requires restarting the app:

| Variable | Default | Effect |
|---|---|---|
| `USE_INTERCEPTORS` | `true` | `true` registers `LoggingCommandInterceptor` and `SoftDeleteInterceptor` on `InventoryDbContext`. `false` runs with neither — deletes become real `DELETE`s again, useful for the "before" half of that comparison. |
| `LOG_TO_CONSOLE` | `true` | Mirrors the interceptor's log output to the console in addition to the rolling file log at `C:\Logs\logfile_<timestamp>.txt`. Only has an effect when interceptors are on. |
| `DOTNET_ENVIRONMENT` | `Development` | Selects which `appsettings.{Environment}.json` overlay to load. |

The startup banner echoes the resolved environment, interceptor state, console-logging state, and
log file path — check it before demoing to confirm you're in the state you think you're in.

## The demos

Main menu, in order:

1. **Show the Data** — read-only projection over Items/Categories/Genres/Contributors. Safe to run
   any time, no side effects.
2. **Interceptors and Logging** — `LoggingCommandInterceptor` (logs SQL under a custom category) and
   `SoftDeleteInterceptor` (turns `DbSet.Remove` into an `IsDeleted = true` update).
3. **Query Filters** — the difference between an all-or-nothing `IgnoreQueryFilters()` and EF Core's
   named query filters (`SoftDelete`, `Active`, and — on `Item` — `Tenant`).
4. **Bulk Update/Delete** — original fetch-loop-save vs. `ExecuteUpdateAsync`/`ExecuteDeleteAsync`,
   with and without a filter.
5. **Work with JSON Columns** — `Contributor.Address` as an owned JSON column, queried via
   `FromSqlRaw`/`JSON_VALUE` (old way) vs. plain LINQ over the mapped JSON type (new way).
6. **LINQ Enhancements** — N+1 vs. `Include`, premature client-side evaluation vs. one translated
   query, `GroupJoin`+`SelectMany` vs. the new `LeftJoin` operator, and query-plan reuse via
   parameterized `Contains`.
7. **Raw SQL to DTO** — `Database.SqlQuery<T>()` against a view, mapped straight into a plain DTO
   with no tracking and no keyless-entity registration.
8. **DDD: Aggregates & Bounded Contexts** — `Order` as an aggregate root (no public setters, no
   `DbSet<OrderLine>`) enforcing its own invariants, plus `OrderingDbContext` as a second bounded
   context sharing the database but isolated by schema and migration history.

Exit is option 9.

## ⚠️ Gotcha: run the demos in menu order

The menu items aren't fully independent — a few later demos depend on state a numerically-earlier
demo creates in the same run. Notably:

- **Run 2 (Interceptors) before 3 (Query Filters).** The Named Query Filters demo says so directly
  in its own console output ("Please make sure that you have run the Soft Delete Interceptor demo
  first to create some soft-deleted records") — it needs the soft-deleted `Category` that demo 2's
  Soft Delete Interceptor action creates, or the "hide soft-deleted rows" comparison has nothing
  soft-deleted to show.
- **Within demo 8 (DDD), run "Create an Order" (action 1) before "Show saved Orders" (action 3).**
  With no orders created yet, action 3 just reports "No orders yet."
- **Within demo 4 (Bulk Update/Delete), run the original loop-based action before the
  `ExecuteUpdateAsync`/`ExecuteDeleteAsync` actions it's contrasted against**, so the audience sees
  the "before" state before it gets overwritten by the "after" state.

Demos 1, 5, and 7 are self-sufficient (JSON demo re-backfills addresses before every run; Bulk
Delete re-seeds junk data if none exists), but going through the menu top-to-bottom in a single
session is still the safest path and is what `DEMO_WALKTHROUGH.md`'s recommended run order assumes.

If you need to restart mid-demo, restarting with `USE_INTERCEPTORS=false` is only meant for the
one deliberate "physical delete vs. soft delete" comparison — flip it back to `true` (the default)
for everything else.
