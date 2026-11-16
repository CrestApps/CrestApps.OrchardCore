using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Lti.Services;
using CrestApps.OrchardCore.Learning.Lti.ViewModels;
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

    public ExternalToolDisplayDriver(
        ExternalToolClients clients,
        ILtiPlatform platform)
    {
        _clients = clients;
        _platform = platform;
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
        => Initialize<ExternalToolViewModel>("ExternalToolFields_Edit", async model =>
        {
            var (redirectUris, keySet) = await _clients.ReadAsync(tool.ClientId);
            var platform = await _platform.GetAsync();

            model.IsNew = context.IsNew;
            model.Name = tool.Name;
            model.Description = tool.Description;
            model.ClientId = tool.ClientId;
            model.DeploymentId = string.IsNullOrEmpty(tool.DeploymentId) && context.IsNew ? IdGenerator.GenerateId() : tool.DeploymentId;
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
            model.Enabled = context.IsNew || tool.Enabled;
            model.Issuer = platform.Issuer;
            model.AuthorizationEndpoint = platform.AuthorizationEndpoint;
            model.TokenEndpoint = platform.TokenEndpoint;
            model.PlatformJwksUrl = platform.JwksUrl;
            model.DeepLinkingReturnUrl = platform.DeepLinkingReturnUrl;
            model.SupportedVariables = LtiCustomParameters.SupportedVariables;
        }).Location("Content:1");

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

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
