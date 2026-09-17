# Pre-extraction tenant snapshot

A tenant database written by the code as it was **before** the Contact Center framework extraction,
restored by `PreExtractionUpgradeTests` to prove the current build still reads it.

`manifest.json` records what is in it: the tenant name, the table prefix, the collection, the
document counts per stored type, and the version of every CrestApps migration that had already been
applied.

## Why it is a checked-in binary

Phase 1 renames every namespace in the suite. Two things in a live tenant are keyed by those names:

- YesSql stores a document's CLR type in the `Type` column, so a renamed model stops deserializing
  unless a migration rewrites the stored name.
- Orchard Core records applied migrations under the migration class's full type name, so a renamed
  migration class looks unapplied and its `CreateAsync` runs again against tables that already exist.

Nothing else in the suite covers either. The database has to have been *written* by the old names,
so it cannot be produced by the current build.

## Regenerating it

The source commit is recorded in `manifest.json`. It is not `main` - the Contact Center modules do
not exist on `main` at all.

```bash
git worktree add ../cc-baseline <sourceCommit>
cp <this directory>/PreExtractionSnapshotGenerator.cs.txt \
   ../cc-baseline/tests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests/PreExtractionSnapshotGenerator.cs
cd ../cc-baseline
dotnet build tests/CrestApps.OrchardCore.ContactCenter.FeatureActivationTests -c Release -p:NuGetAudit=false
CRESTAPPS_SNAPSHOT_OUT=<this directory> CRESTAPPS_SNAPSHOT_COMMIT=<sourceCommit> \
  dotnet tests/.../CrestApps.OrchardCore.ContactCenter.FeatureActivationTests.dll \
  -filterVSTest "FullyQualifiedName~PreExtractionSnapshotGenerator"
```

The generator is kept here as `.txt` rather than as a compiled test, because compiling it in this
repository would let it run against the *current* code, which would record the new type names and
prove nothing.

`manifest.json` is then completed with `tablePrefix`, `collectionName` and `migrationVersions`, which
are read out of the produced database. Finally, remove the worktree:

```bash
git worktree remove ../cc-baseline
```

## Extending it

Seeding more document types widens the coverage. Add them to the generator's seeding block and to
`documentCounts`, and add a matching `CountAsync<T>` line in `PreExtractionUpgradeTests`. Keep the
database small: it is committed.
