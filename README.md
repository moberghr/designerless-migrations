# Moberg.EntityFrameworkCore.DesignerlessMigrations

Single-file EF Core migrations: `dotnet ef migrations add` writes one file with `[DbContext]` and `[Migration]` on the class, and no `.Designer.cs` (a full model copy per migration, tens of thousands of lines each).

Planned packages:

- `Moberg.EntityFrameworkCore.DesignerlessMigrations`: design-time scaffolder, picked up by `dotnet ef` automatically; safe `migrations remove` (snapshot restored from git, with guards); convention checks for tests (no Designer files, attributes present, optionally no `Down()`).
- `Moberg.EntityFrameworkCore.DesignerlessMigrations.Npgsql`: runtime SQL generator fix. Without a Designer model EF ignores `oldType`, so a nullability-only `AlterColumn` would also emit `ALTER COLUMN ... TYPE <same type>`.

Work in progress.
