using System.Text.Json;
using CrestApps.OrchardCore.Seo.Drivers;
using CrestApps.OrchardCore.Seo.Models;
using CrestApps.OrchardCore.Seo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Seo.AI;

/// <summary>
/// Lets an AI agent read and change the site's structured data settings: who publishes the site, its logo and
/// profiles, and whether pages describe the web site and its search.
/// </summary>
public sealed class ConfigureSiteStructuredDataTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "configureSiteStructuredData";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "publisherType": {
          "type": "string",
          "enum": [ "Organization", "Person" ],
          "description": "Who publishes the site."
        },
        "publisherName": {
          "type": "string",
          "description": "The publisher's name. An empty string uses the site name."
        },
        "publisherLogoUrl": {
          "type": "string",
          "description": "The URL of the organization's logo or the person's photo, such as /media/logo.png."
        },
        "sameAs": {
          "type": "array",
          "items": { "type": "string" },
          "description": "The absolute URLs of the publisher's profiles on other sites, such as its LinkedIn page. Replaces the current list."
        },
        "renderOnPages": {
          "type": "boolean",
          "description": "Whether pages get a JSON-LD script."
        },
        "includeWebSite": {
          "type": "boolean",
          "description": "Whether pages describe the web site they belong to."
        },
        "includeSearchAction": {
          "type": "boolean",
          "description": "Whether the web site tells search engines how to search it."
        }
      }
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Reads or changes the site-wide schema.org structured data settings: the publisher (organization or person, name, logo, profiles on other sites) and whether pages describe the web site and its search. Only the arguments given are changed; call it with no arguments to read the settings.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var user = arguments.Services.GetRequiredService<IHttpContextAccessor>().HttpContext?.User;

        if (user is null || !await arguments.Services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, AdvancedSeoPermissionsProvider.ManageAdvancedSeoSettings))
        {
            return "You do not have permission to manage the advanced SEO settings.";
        }

        var siteService = arguments.Services.GetRequiredService<ISiteService>();
        if (arguments.Count == 0)
        {
            return JsonSerializer.Serialize(await siteService.GetSettingsAsync<StructuredDataSettings>(), SchemaOrgPartSettingsDisplayDriver.EditorJsonOptions);
        }

        var publisherType = SchemaOrgToolArguments.GetString(arguments, "publisherType");

        if (publisherType is not null &&
            publisherType != StructuredDataPublisherTypes.Organization &&
            publisherType != StructuredDataPublisherTypes.Person)
        {
            return "Nothing was changed: publisherType must be Organization or Person.";
        }

        if (!SchemaOrgToolArguments.TryGetJson<string[]>(arguments, "sameAs", SchemaOrgPartSettingsDisplayDriver.EditorJsonOptions, out var sameAs))
        {
            return "Nothing was changed: sameAs must be an array of URLs.";
        }

        foreach (var profile in sameAs ?? [])
        {
            if (!Uri.TryCreate(profile, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return $"Nothing was changed: '{profile}' is not an absolute URL.";
            }
        }

        var site = await siteService.LoadSiteSettingsAsync();
        var settings = site.GetOrCreate<StructuredDataSettings>();

        if (publisherType is not null)
        {
            settings.PublisherType = publisherType;
        }

        if (arguments.ContainsKey("publisherName"))
        {
            settings.PublisherName = SchemaOrgToolArguments.GetString(arguments, "publisherName");
        }

        if (arguments.ContainsKey("publisherLogoUrl"))
        {
            settings.PublisherLogoUrl = SchemaOrgToolArguments.GetString(arguments, "publisherLogoUrl");
        }

        if (sameAs is not null)
        {
            settings.SameAs = sameAs.Where(profile => !string.IsNullOrWhiteSpace(profile)).Select(profile => profile.Trim()).ToArray();
        }

        settings.RenderOnPages = SchemaOrgToolArguments.GetBoolean(arguments, "renderOnPages") ?? settings.RenderOnPages;
        settings.IncludeWebSite = SchemaOrgToolArguments.GetBoolean(arguments, "includeWebSite") ?? settings.IncludeWebSite;
        settings.IncludeSearchAction = SchemaOrgToolArguments.GetBoolean(arguments, "includeSearchAction") ?? settings.IncludeSearchAction;

        site.Put(settings);
        await siteService.UpdateSiteSettingsAsync(site);

        return JsonSerializer.Serialize(new { saved = true, settings }, SchemaOrgPartSettingsDisplayDriver.EditorJsonOptions);
    }
}
