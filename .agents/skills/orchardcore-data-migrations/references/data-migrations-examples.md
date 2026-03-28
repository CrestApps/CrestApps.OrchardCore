# Orchard Core Data Migrations Examples

## Example 1: Complete Module Migration

A migration for an Event content type with custom part, fields, and index:

```csharp
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

public sealed class Migrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public Migrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        // Define the EventPart with custom fields
        await _contentDefinitionManager.AlterPartDefinitionAsync("EventPart", part => part
            .WithField("Location", field => field
                .OfType("TextField")
                .WithDisplayName("Location")
                .WithPosition("0")
            )
            .WithField("StartDate", field => field
                .OfType("DateTimeField")
                .WithDisplayName("Start Date")
                .WithPosition("1")
            )
            .WithField("EndDate", field => field
                .OfType("DateTimeField")
                .WithDisplayName("End Date")
                .WithPosition("2")
            )
            .WithField("MaxAttendees", field => field
                .OfType("NumericField")
                .WithDisplayName("Max Attendees")
                .WithPosition("3")
            )
        );

        // Define the Event content type
        await _contentDefinitionManager.AlterTypeDefinitionAsync("Event", type => type
            .DisplayedAs("Event")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    AllowCustomPath = true,
                    Pattern = "events/{{ ContentItem | display_text | slugify }}"
                })
            )
            .WithPart("EventPart", part => part.WithPosition("2"))
            .WithPart("HtmlBodyPart", part => part.WithPosition("3"))
        );

        // Create the index table
        await SchemaBuilder.CreateMapIndexTableAsync<EventPartIndex>(table => table
            .Column<string>(nameof(EventPartIndex.ContentItemId), col => col.WithLength(26))
            .Column<string>(nameof(EventPartIndex.Location), col => col.WithLength(256))
            .Column<DateTime>(nameof(EventPartIndex.StartDate))
            .Column<DateTime>(nameof(EventPartIndex.EndDate))
            .Column<bool>(nameof(EventPartIndex.Published))
        );

        await SchemaBuilder.AlterIndexTableAsync<EventPartIndex>(table => table
            .CreateIndex("IDX_EventPartIndex_StartDate",
                nameof(EventPartIndex.StartDate),
                nameof(EventPartIndex.Published))
        );

        return 1;
    }

    public async Task<int> UpdateFrom1Async()
    {
        // Add a new field to EventPart
        await _contentDefinitionManager.AlterPartDefinitionAsync("EventPart", part => part
            .WithField("IsVirtual", field => field
                .OfType("BooleanField")
                .WithDisplayName("Virtual Event")
                .WithPosition("4")
            )
        );

        return 2;
    }
}
```

## Example 2: Startup Registration

```csharp
using OrchardCore.ContentManagement;
using OrchardCore.Data.Migration;
using YesSql.Indexes;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentPart<EventPart>();
        services.AddScoped<IDataMigration, Migrations>();
        services.AddIndexProvider<EventPartIndexProvider>();
    }
}
```
