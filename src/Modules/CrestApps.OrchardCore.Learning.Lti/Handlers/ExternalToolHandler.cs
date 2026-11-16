using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using CrestApps.OrchardCore.Learning.Lti.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Models;
using CrestApps.OrchardCore.Learning.Lti.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Lti.Handlers;

/// <summary>
/// Stamps external tools, gives each one its own subject salt, reads them from recipes, checks them, and removes the
/// tool's OpenID application with the tool.
/// </summary>
internal sealed class ExternalToolHandler : CatalogEntryHandlerBase<ExternalTool>
{
    private readonly IExternalToolStore _store;
    private readonly ExternalToolClients _clients;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public ExternalToolHandler(
        IExternalToolStore store,
        ExternalToolClients clients,
        IClock clock,
        IStringLocalizer<ExternalToolHandler> stringLocalizer)
    {
        _store = store;
        _clients = clients;
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

        if (string.IsNullOrWhiteSpace(tool.DeploymentId))
        {
            context.Result.Fail(new ValidationResult(S["Deployment ID is required."], [nameof(ExternalTool.DeploymentId)]));
        }

        RequireUrl(context, tool.LoginInitiationUrl, nameof(ExternalTool.LoginInitiationUrl), required: true);
        RequireUrl(context, tool.TargetLinkUri, nameof(ExternalTool.TargetLinkUri), required: true);
        RequireUrl(context, tool.DeepLinkingUrl, nameof(ExternalTool.DeepLinkingUrl), required: false);
        RequireUrl(context, tool.JwksUrl, nameof(ExternalTool.JwksUrl), required: false);
    }

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
