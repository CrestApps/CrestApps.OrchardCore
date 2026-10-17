using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.ContentFields.Drivers;
using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.ContentFields.Indexes;
using CrestApps.OrchardCore.ContentFields.Migrations;
using CrestApps.OrchardCore.ContentFields.Schemas;
using CrestApps.OrchardCore.Recipes.Core;
using Fluid;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContentFields;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();

        services.AddContentField<PhoneField>()
            .UseDisplayDriver<PhoneFieldDisplayDriver>();

        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, PhoneFieldSettingsDriver>();
    }
}

[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class RecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, PhoneFieldSchemaDefinition>();
    }
}

/// <summary>
/// The address field, laid out and checked the way the chosen country writes addresses.
/// </summary>
[Feature("CrestApps.OrchardCore.ContentFields.Address")]
public sealed class AddressFieldStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddContentField<AddressField>()
            .UseDisplayDriver<AddressFieldDisplayDriver>();

        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, AddressFieldSettingsDriver>();

        services.AddDataMigration<AddressFieldIndexMigrations>();
        // Scoped, like Orchard Core's own field indexes: it reads the scoped content definitions.
        services.AddScopedIndexProvider<AddressFieldIndexProvider>();

        services.Configure<TemplateOptions>(options =>
        {
            options.MemberAccessStrategy.Register<AddressField>();
            options.MemberAccessStrategy.Register<Address>();
        });
    }
}

[Feature("CrestApps.OrchardCore.ContentFields.Address")]
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class AddressFieldRecipesSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, AddressFieldSchemaDefinition>();
    }
}

