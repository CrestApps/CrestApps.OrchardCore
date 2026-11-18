using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Lti.Services;
using CrestApps.OrchardCore.Learning.Lti.ViewModels;
using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Lti.Handlers;

/// <summary>
/// Stamps external tools, gives each one its own subject salt, reads them from recipes, checks them, and removes the
/// tool's OpenID application with the tool.
/// </summary>
internal sealed class ExternalToolHandler : CatalogEntryHandlerBase<ExternalTool>
{
    // The editor fields of the tool's OpenID application; the errors land on them.
    private const string RedirectUrisMember = nameof(ExternalToolViewModel.RedirectUris);
    private const string KeySetMember = nameof(ExternalToolViewModel.KeySet);

    private readonly IExternalToolStore _store;
    private readonly ExternalToolClients _clients;
    private readonly ILtiToolDirectory _tools;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public ExternalToolHandler(
        IExternalToolStore store,
        ExternalToolClients clients,
        ILtiToolDirectory tools,
        IClock clock,
        IStringLocalizer<ExternalToolHandler> stringLocalizer)
    {
        _store = store;
        _clients = clients;
        _tools = tools;
        _clock = clock;
        S = stringLocalizer;
    }

    public override Task InitializingAsync(InitializingContext<ExternalTool> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;
        Populate(context.Model, context.Data);

        if (string.IsNullOrEmpty(context.Model.SubjectSalt))
        {
            context.Model.SubjectSalt = LtiSubjects.NewSalt();
        }

        return Task.CompletedTask;
    }

    public override Task UpdatingAsync(UpdatingContext<ExternalTool> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;
        Populate(context.Model, context.Data);

        if (string.IsNullOrEmpty(context.Model.SubjectSalt))
        {
            context.Model.SubjectSalt = LtiSubjects.NewSalt();
        }

        return Task.CompletedTask;
    }

    public override async Task ValidatingAsync(ValidatingContext<ExternalTool> context, CancellationToken cancellationToken = default)
    {
        var tool = context.Model;

        if (string.IsNullOrWhiteSpace(tool.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(ExternalTool.Name)]));
        }

        if (string.IsNullOrWhiteSpace(tool.ClientId))
        {
            context.Result.Fail(new ValidationResult(S["Client ID is required."], [nameof(ExternalTool.ClientId)]));
        }
        else if (await _store.FindByClientIdAsync(tool.ClientId, cancellationToken) is { } other && other.ItemId != tool.ItemId)
        {
            context.Result.Fail(new ValidationResult(S["Another external tool uses this client ID."], [nameof(ExternalTool.ClientId)]));
        }
        else if (_clients.TryGetPending(tool.ItemId, out _) && await _clients.IsTakenByAnotherApplicationAsync(tool.ClientId))
        {
            // Only the editor writes the tool's OpenID application, so only the editor is refused here: a recipe
            // imports the application with the OpenID applications step and the tool with its own step, and never
            // writes the application through the tool.
            context.Result.Fail(new ValidationResult(S["An OpenID application that External Tools did not make uses this client ID. Choose another client ID."], [nameof(ExternalTool.ClientId)]));
        }

        if (string.IsNullOrWhiteSpace(tool.DeploymentId))
        {
            context.Result.Fail(new ValidationResult(S["Deployment ID is required."], [nameof(ExternalTool.DeploymentId)]));
        }

        RequireUrl(context, tool.LoginInitiationUrl, nameof(ExternalTool.LoginInitiationUrl), required: true);
        RequireUrl(context, tool.TargetLinkUri, nameof(ExternalTool.TargetLinkUri), required: true);
        RequireUrl(context, tool.DeepLinkingUrl, nameof(ExternalTool.DeepLinkingUrl), required: false);
        RequireUrl(context, tool.JwksUrl, nameof(ExternalTool.JwksUrl), required: false);

        // The redirect URIs and the key set of the tool's OpenID application, when the editor bound them. A recipe
        // carries them with the OpenID applications step, so they are checked here only for the editor.
        if (_clients.TryGetPending(tool.ItemId, out var pending))
        {
            await ValidateClientAsync(context, pending, cancellationToken);
        }
    }

    private async Task ValidateClientAsync(ValidatingContext<ExternalTool> context, ExternalToolClients.PendingClient pending, CancellationToken cancellationToken)
    {
        var tool = context.Model;

        if (pending.RedirectUris.Count == 0)
        {
            context.Result.Fail(new ValidationResult(S["Enter at least one redirect URI: where the tool receives its launches."], [RedirectUrisMember]));
        }

        foreach (var uri in pending.RedirectUris)
        {
            if (!IsAddress(uri))
            {
                context.Result.Fail(new ValidationResult(S["Each redirect URI must be a full address that starts with https:// and has no fragment. Not valid: {0}", uri], [RedirectUrisMember]));
            }
        }

        if (!string.IsNullOrWhiteSpace(pending.KeySetJson))
        {
            pending.KeySet = ReadKeySet(pending.KeySetJson);

            if (pending.KeySet is null)
            {
                context.Result.Fail(new ValidationResult(S["Enter a JSON Web Key Set with at least one RSA signing key (public keys only), such as {\"keys\":[{\"kty\":\"RSA\",\"kid\":\"…\",\"n\":\"…\",\"e\":\"AQAB\"}]}."], [KeySetMember]));
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(tool.JwksUrl))
        {
            context.Result.Fail(new ValidationResult(S["Enter the tool's JWKS URL or paste its key set: the platform checks the tool's messages and service requests with them."], [nameof(ExternalTool.JwksUrl)]));

            return;
        }

        if (!IsAddress(tool.JwksUrl))
        {
            return;
        }

        var keys = await _tools.GetSigningKeysAsync(tool, refresh: true, cancellationToken);

        if (keys.Count == 0)
        {
            context.Result.Fail(new ValidationResult(S["The key set could not be read from the JWKS URL, or it has no signing key. Check that the tool is reachable from the site."], [nameof(ExternalTool.JwksUrl)]));

            return;
        }

        var keySet = new JsonWebKeySet();

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

        pending.KeySet = keySet;
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

    private static bool IsAddress(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && string.IsNullOrEmpty(uri.Fragment);

    public override Task DeletedAsync(DeletedContext<ExternalTool> context, CancellationToken cancellationToken = default)
        => _clients.DeleteAsync(context.Model.ClientId);

    private void RequireUrl(ValidatingContext<ExternalTool> context, string value, string member, bool required)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (required)
            {
                context.Result.Fail(new ValidationResult(S["Enter the full address, starting with https://."], [member]));
            }

            return;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) || !string.IsNullOrEmpty(uri.Fragment))
        {
            context.Result.Fail(new ValidationResult(S["Enter a full address that starts with https:// and has no fragment."], [member]));
        }
    }

    private static void Populate(ExternalTool model, JsonNode data)
    {
        if (data is not JsonObject json)
        {
            return;
        }

        model.Name = Text(json, nameof(ExternalTool.Name)) ?? model.Name;
        model.Description = json[nameof(ExternalTool.Description)]?.GetValue<string>() ?? model.Description;
        model.ClientId = Text(json, nameof(ExternalTool.ClientId)) ?? model.ClientId;
        model.DeploymentId = Text(json, nameof(ExternalTool.DeploymentId)) ?? model.DeploymentId;
        model.LoginInitiationUrl = Text(json, nameof(ExternalTool.LoginInitiationUrl)) ?? model.LoginInitiationUrl;
        model.TargetLinkUri = Text(json, nameof(ExternalTool.TargetLinkUri)) ?? model.TargetLinkUri;
        model.DeepLinkingUrl = Text(json, nameof(ExternalTool.DeepLinkingUrl)) ?? model.DeepLinkingUrl;
        model.JwksUrl = Text(json, nameof(ExternalTool.JwksUrl)) ?? model.JwksUrl;
        model.CustomParameters = json[nameof(ExternalTool.CustomParameters)]?.GetValue<string>() ?? model.CustomParameters;
        model.AllowNamesAndRoles = json[nameof(ExternalTool.AllowNamesAndRoles)]?.GetValue<bool?>() ?? model.AllowNamesAndRoles;
        model.AllowGrades = json[nameof(ExternalTool.AllowGrades)]?.GetValue<bool?>() ?? model.AllowGrades;
        model.Enabled = json[nameof(ExternalTool.Enabled)]?.GetValue<bool?>() ?? model.Enabled;
        model.SubjectSalt = Text(json, nameof(ExternalTool.SubjectSalt)) ?? model.SubjectSalt;

        if (json[nameof(ExternalTool.PrivacyLevel)] is JsonValue privacy)
        {
            if (privacy.TryGetValue<int>(out var number) && Enum.IsDefined(typeof(LtiPrivacyLevel), number))
            {
                model.PrivacyLevel = (LtiPrivacyLevel)number;
            }
            else if (privacy.TryGetValue<string>(out var name) && Enum.TryParse<LtiPrivacyLevel>(name, ignoreCase: true, out var level))
            {
                model.PrivacyLevel = level;
            }
        }
    }

    private static string Text(JsonObject json, string name)
        => json[name]?.GetValue<string>()?.Trim() is { Length: > 0 } value ? value : null;
}
