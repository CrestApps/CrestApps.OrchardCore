using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Lti.Services;
using CrestApps.OrchardCore.Learning.Lti.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Lti.Drivers;

/// <summary>
/// The rows of the external tools list, and the tool editor: Learning's settings, the redirect URIs and key set of the
/// tool's OpenID application, and the platform details the tool needs.
/// </summary>
internal sealed class ExternalToolDisplayDriver : DisplayDriver<ExternalTool>
{
    private readonly ExternalToolClients _clients;
    private readonly ILtiPlatform _platform;

    internal readonly IStringLocalizer S;

    public ExternalToolDisplayDriver(
        ExternalToolClients clients,
        ILtiPlatform platform,
        IStringLocalizer<ExternalToolDisplayDriver> stringLocalizer)
    {
        _clients = clients;
        _platform = platform;
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(ExternalTool tool, BuildDisplayContext context)
    {
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = tool.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("ExternalTool_Fields_SummaryAdmin", tool)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ExternalTool_Description_SummaryAdmin", tool)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrWhiteSpace(tool.Description))),
            View("ExternalTool_Tags_SummaryAdmin", tool)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Services:5"),
            View("ExternalTool_Buttons_SummaryAdmin", tool).Location("Actions:5"));
    }

    public override IDisplayResult Edit(ExternalTool tool, BuildEditorContext context)
    {
        // The cards share one read of the tool's OpenID application and of the platform details.
        var source = new Lazy<Task<ExternalToolViewModel>>(() => BuildAsync(tool, context.IsNew));

        return Combine(
            Initialize<ExternalToolViewModel>("ExternalToolFields_Edit", async model => Populate(await source.Value, model))
                .Location(l => l.Zone("Content", "1").Card(S["Tool"], "10")),
            Initialize<ExternalToolViewModel>("ExternalToolClient_Edit", async model => Populate(await source.Value, model))
                .Location(l => l.Zone("Content", "2").Card(S["Client"], "20")),
            Initialize<ExternalToolViewModel>("ExternalToolLaunch_Edit", async model => Populate(await source.Value, model))
                .Location(l => l.Zone("Content", "3").Card(S["Launch"], "30")),
            Initialize<ExternalToolViewModel>("ExternalToolPrivacy_Edit", async model => Populate(await source.Value, model))
                .Location(l => l.Zone("Content", "4").Card(S["Privacy and Services"], "40")),
            Initialize<ExternalToolViewModel>("ExternalToolPlatform_Edit", async model => Populate(await source.Value, model))
                .Location(l => l.Zone("Content", "5").Card(S["Platform Details"], "50")));
    }

    public override async Task<IDisplayResult> UpdateAsync(ExternalTool tool, UpdateEditorContext context)
    {
        var model = new ExternalToolViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        tool.Name = Clean(model.Name);
        tool.Description = model.Description?.Trim();
        tool.ClientId = Clean(model.ClientId);
        tool.DeploymentId = Clean(model.DeploymentId);
        tool.LoginInitiationUrl = Clean(model.LoginInitiationUrl);
        tool.TargetLinkUri = Clean(model.TargetLinkUri);
        tool.DeepLinkingUrl = Clean(model.DeepLinkingUrl);
        tool.JwksUrl = Clean(model.JwksUrl);
        tool.CustomParameters = model.CustomParameters?.Trim();
        tool.PrivacyLevel = Enum.IsDefined(model.PrivacyLevel) ? model.PrivacyLevel : LtiPrivacyLevel.Anonymous;
        tool.AllowNamesAndRoles = model.AllowNamesAndRoles;
        tool.AllowGrades = model.AllowGrades;
        tool.Enabled = model.Enabled;

        var redirectUris = (model.RedirectUris ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // The redirect URIs and the key set belong to the tool's OpenID application, which a recipe carries with the
        // OpenID applications step; the tool's handler checks them when the tool is saved.
        _clients.SetPending(tool, redirectUris, model.KeySet);

        return Edit(tool, context);
    }

    private async Task<ExternalToolViewModel> BuildAsync(ExternalTool tool, bool isNew)
    {
        var model = new ExternalToolViewModel();
        var (redirectUris, keySet) = await _clients.ReadAsync(tool.ClientId);
        var platform = await _platform.GetAsync();

        model.IsNew = isNew;
        model.Name = tool.Name;
        model.Description = tool.Description;
        model.ClientId = tool.ClientId;
        model.DeploymentId = string.IsNullOrEmpty(tool.DeploymentId) && isNew ? IdGenerator.GenerateId() : tool.DeploymentId;
        model.LoginInitiationUrl = tool.LoginInitiationUrl;
        model.TargetLinkUri = tool.TargetLinkUri;
        model.DeepLinkingUrl = tool.DeepLinkingUrl;
        model.JwksUrl = tool.JwksUrl;
        model.KeySet = string.IsNullOrWhiteSpace(tool.JwksUrl) ? keySet : null;
        model.RedirectUris = string.Join(Environment.NewLine, redirectUris);
        model.CustomParameters = tool.CustomParameters;
        model.PrivacyLevel = tool.PrivacyLevel;
        model.AllowNamesAndRoles = tool.AllowNamesAndRoles;
        model.AllowGrades = tool.AllowGrades;
        model.Enabled = isNew || tool.Enabled;
        model.Issuer = platform.Issuer;
        model.AuthorizationEndpoint = platform.AuthorizationEndpoint;
        model.TokenEndpoint = platform.TokenEndpoint;
        model.PlatformJwksUrl = platform.JwksUrl;
        model.DeepLinkingReturnUrl = platform.DeepLinkingReturnUrl;
        model.SupportedVariables = LtiCustomParameters.SupportedVariables;

        return model;
    }

    private static void Populate(ExternalToolViewModel source, ExternalToolViewModel model)
    {
        model.IsNew = source.IsNew;
        model.Name = source.Name;
        model.Description = source.Description;
        model.ClientId = source.ClientId;
        model.DeploymentId = source.DeploymentId;
        model.LoginInitiationUrl = source.LoginInitiationUrl;
        model.TargetLinkUri = source.TargetLinkUri;
        model.DeepLinkingUrl = source.DeepLinkingUrl;
        model.JwksUrl = source.JwksUrl;
        model.KeySet = source.KeySet;
        model.RedirectUris = source.RedirectUris;
        model.CustomParameters = source.CustomParameters;
        model.PrivacyLevel = source.PrivacyLevel;
        model.AllowNamesAndRoles = source.AllowNamesAndRoles;
        model.AllowGrades = source.AllowGrades;
        model.Enabled = source.Enabled;
        model.Issuer = source.Issuer;
        model.AuthorizationEndpoint = source.AuthorizationEndpoint;
        model.TokenEndpoint = source.TokenEndpoint;
        model.PlatformJwksUrl = source.PlatformJwksUrl;
        model.DeepLinkingReturnUrl = source.DeepLinkingReturnUrl;
        model.SupportedVariables = source.SupportedVariables;
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
