using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Lti.Services;
using CrestApps.OrchardCore.Learning.Lti.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Learning.Lti.Drivers;

/// <summary>
/// The rows of the external tools list, and the tool editor: Learning's settings, the redirect URIs and key set of the
/// tool's OpenID application, and the platform details the tool needs.
/// </summary>
internal sealed class ExternalToolDisplayDriver : DisplayDriver<ExternalTool>
{
    private readonly ExternalToolClients _clients;
    private readonly ILtiPlatform _platform;
    private readonly ILtiToolDirectory _tools;

    internal readonly IStringLocalizer S;

    public ExternalToolDisplayDriver(
        ExternalToolClients clients,
        ILtiPlatform platform,
        ILtiToolDirectory tools,
        IStringLocalizer<ExternalToolDisplayDriver> stringLocalizer)
    {
        _clients = clients;
        _platform = platform;
        _tools = tools;
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

        if (redirectUris.Count == 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.RedirectUris), S["Enter at least one redirect URI: where the tool receives its launches."]);
        }

        foreach (var uri in redirectUris)
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp) || !string.IsNullOrEmpty(parsed.Fragment))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.RedirectUris), S["Each redirect URI must be a full address that starts with https:// and has no fragment. Not valid: {0}", uri]);
            }
        }

        JsonWebKeySet keySet = null;

        if (!string.IsNullOrWhiteSpace(model.KeySet))
        {
            keySet = ReadKeySet(model.KeySet);

            if (keySet is null)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.KeySet), S["Enter a JSON Web Key Set with at least one RSA signing key (public keys only), such as {\"keys\":[{\"kty\":\"RSA\",\"kid\":\"…\",\"n\":\"…\",\"e\":\"AQAB\"}]}."]);
            }
        }
        else if (!string.IsNullOrWhiteSpace(tool.JwksUrl))
        {
            var keys = await _tools.GetSigningKeysAsync(tool, refresh: true);

            if (keys.Count == 0)
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.JwksUrl), S["The key set could not be read from the JWKS URL, or it has no signing key. Check that the tool is reachable from the site."]);
            }
            else
            {
                keySet = new JsonWebKeySet();

                foreach (var key in keys)
                {
                    if (key is JsonWebKey jsonWebKey)
                    {
                        keySet.Keys.Add(jsonWebKey);
                    }
                    else if (key is RsaSecurityKey rsa)
                    {
                        keySet.Keys.Add(JsonWebKeyConverter.ConvertFromRSASecurityKey(rsa));
                    }
                }
            }
        }
        else
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.JwksUrl), S["Enter the tool's JWKS URL or paste its key set: the platform checks the tool's messages and service requests with them."]);
        }

        _clients.SetPending(tool, redirectUris, keySet);

        return Edit(tool, context);
    }

    private static JsonWebKeySet ReadKeySet(string json)
    {
        try
        {
            var keySet = new JsonWebKeySet(json);

            if (keySet.Keys.Count == 0 || keySet.Keys.Any(key => key.HasPrivateKey) || keySet.GetSigningKeys().Count == 0)
            {
                return null;
            }

            return keySet;
        }
        catch (Exception exception) when (exception is ArgumentException or System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string Clean(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
