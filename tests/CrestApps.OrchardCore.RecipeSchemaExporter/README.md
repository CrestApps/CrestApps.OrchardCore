# CrestApps.OrchardCore.RecipeSchemaExporter

This temporary console project exports the authoritative runtime recipe JSON schemas from `CrestApps.OrchardCore.Recipes` into the Orchard Core AgentSkills repository.

It generates:

- one `<step>.schema.json` file for each registered `IRecipeStep`
- `recipe.schema.json` for the root recipe contract
- `index.json` for step-to-file lookup

The exporter exists so contributors can refresh the recipe schema reference files that AI skills use when generating Orchard Core recipes.

## Default output behavior

When you do not pass an output path, the exporter:

1. walks up from the running process location
2. finds the `CrestApps.OrchardCore` repository root by looking for markers such as `global.json`, `NuGet.config`, or `CrestApps.OrchardCore.slnx`
3. moves to the parent directory of that repo
4. writes to the sibling AgentSkills path:

`..\CrestApps.AgentSkills\src\CrestApps.AgentSkills\orchardcore\orchardcore-recipes\references\recipe-schemas`

This assumes `CrestApps.OrchardCore` and `CrestApps.AgentSkills` are checked out next to each other.

## How to run it

From the `CrestApps.OrchardCore` repository root:

```powershell
dotnet build .\tests\CrestApps.OrchardCore.RecipeSchemaExporter\CrestApps.OrchardCore.RecipeSchemaExporter.csproj --no-restore -warnaserror -v minimal
dotnet run --project .\tests\CrestApps.OrchardCore.RecipeSchemaExporter\CrestApps.OrchardCore.RecipeSchemaExporter.csproj --framework net10.0 --no-build
```

## Custom output path

You can also pass an explicit output directory as the first argument:

```powershell
dotnet run --project .\tests\CrestApps.OrchardCore.RecipeSchemaExporter\CrestApps.OrchardCore.RecipeSchemaExporter.csproj --framework net10.0 --no-build -- "C:\Some\Other\Path"
```

## If the default path cannot be resolved

If the exporter cannot find the sibling `CrestApps.AgentSkills` repository, clone it next to `CrestApps.OrchardCore`:

```powershell
Set-Location ..
git clone https://github.com/CrestApps/CrestApps.AgentSkills.git
```

After both repositories sit next to each other, run the exporter again.
