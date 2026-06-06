using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal sealed class OmnichannelContactDefinitionTenantEvents : ModularTenantEvents
{
    private readonly OmnichannelContactDefinitionService _definitionService;
    private readonly ShellSettings _shellSettings;

    public OmnichannelContactDefinitionTenantEvents(
        OmnichannelContactDefinitionService definitionService,
        ShellSettings shellSettings)
    {
        _definitionService = definitionService;
        _shellSettings = shellSettings;
    }

    public override async Task ActivatingAsync()
    {
        if (_shellSettings.IsUninitialized())
        {
            return;
        }

        await _definitionService.RepairOmnichannelContactContentTypesAsync();
    }
}
