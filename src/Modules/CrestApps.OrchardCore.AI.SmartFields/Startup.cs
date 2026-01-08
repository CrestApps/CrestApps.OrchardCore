using CrestApps.OrchardCore.AI.SmartFields.Drivers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.Modules;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.AI.SmartFields;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Register the settings display driver for TextField
        services.AddScoped<IContentPartFieldDefinitionDisplayDriver, SmartTextFieldAutocompleteSettingsDisplayDriver>();

        // Register the field display driver for TextField
        services.AddContentField<TextField>()
            .ForEditor<SmartTextFieldAutocompleteDisplayDriver>(editor => editor == SmartTextFieldAutocompleteSettingsDisplayDriver.EditorName);

        // Register resource manifest
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>();
    }
}
