using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Sitemaps;
using OrchardCore.Sitemaps.Models;
using OrchardCore.Sitemaps.Services;

namespace CrestApps.OrchardCore.AI.Agent.Sitemaps;

/// <summary>
/// Creates a sitemap or updates a sitemap or a sitemap index, through the sitemap manager the Sitemaps admin pages use.
/// Everything is checked before anything is changed, so a refused call leaves the sitemap as it was.
/// </summary>
public sealed class SaveSitemapTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "saveSitemap";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "sitemapId": {
          "type": "string",
          "description": "The ID of the sitemap or sitemap index to update, from listSitemaps. Leave it out to create a sitemap."
        },
        "name": {
          "type": "string",
          "description": "The name of the sitemap. Required to create one."
        },
        "path": {
          "type": "string",
          "description": "The address of the sitemap on the site, for example blog-sitemap.xml. It must end in .xml and no other sitemap may use it. When a sitemap is created without one, it is made from the name."
        },
        "enabled": {
          "type": "boolean",
          "description": "Whether the sitemap is served. A new sitemap is enabled unless this is false."
        },
        "sources": {
          "type": "array",
          "description": "The sources of the sitemap. The list replaces the stored one: a stored source that is not listed with its sourceId is removed. Leave it out to keep the sources as they are. Not for a sitemap index.",
          "items": {
            "type": "object",
            "properties": {
              "sourceId": {
                "type": "string",
                "description": "The ID of a stored source to keep and update, from listSitemaps. Leave it out to add a new source."
              },
              "type": {
                "type": "string",
                "enum": [
                  "contentTypes",
                  "customPath"
                ],
                "description": "contentTypes lists the published content items of content types; customPath is one address of the site."
              },
              "indexAll": {
                "type": "boolean",
                "description": "For contentTypes: whether every content type that has an address is listed. A new source lists all unless contentTypes or limitedContentType is given."
              },
              "limitItems": {
                "type": "boolean",
                "description": "For contentTypes: whether the source lists a part of the items of one content type, given in limitedContentType."
              },
              "changeFrequency": {
                "type": "string",
                "enum": [
                  "always",
                  "hourly",
                  "daily",
                  "weekly",
                  "monthly",
                  "yearly",
                  "never"
                ],
                "description": "How often the pages change. For contentTypes it applies when every content type is listed."
              },
              "priority": {
                "type": "integer",
                "minimum": 0,
                "maximum": 10,
                "description": "The priority from 0 to 10, written to the sitemap as 0.0 to 1.0. For contentTypes it applies when every content type is listed."
              },
              "contentTypes": {
                "type": "array",
                "description": "For contentTypes: the content types to list, each with its own frequency and priority. Giving it turns indexAll off.",
                "items": {
                  "type": "object",
                  "properties": {
                    "contentType": {
                      "type": "string",
                      "description": "The technical name of a content type that has an address."
                    },
                    "changeFrequency": {
                      "type": "string",
                      "enum": [
                        "always",
                        "hourly",
                        "daily",
                        "weekly",
                        "monthly",
                        "yearly",
                        "never"
                      ],
                      "description": "How often the items change. The default is daily."
                    },
                    "priority": {
                      "type": "integer",
                      "minimum": 0,
                      "maximum": 10,
                      "description": "The priority from 0 to 10. The default is 5."
                    }
                  },
                  "required": [
                    "contentType"
                  ],
                  "additionalProperties": false
                }
              },
              "limitedContentType": {
                "type": "object",
                "description": "For contentTypes: the one content type whose items are listed in part, to split a large sitemap. Giving it turns limitItems on and indexAll off.",
                "properties": {
                  "contentType": {
                    "type": "string",
                    "description": "The technical name of a content type that has an address."
                  },
                  "changeFrequency": {
                    "type": "string",
                    "enum": [
                      "always",
                      "hourly",
                      "daily",
                      "weekly",
                      "monthly",
                      "yearly",
                      "never"
                    ],
                    "description": "How often the items change. The default is daily."
                  },
                  "priority": {
                    "type": "integer",
                    "minimum": 0,
                    "maximum": 10,
                    "description": "The priority from 0 to 10. The default is 5."
                  },
                  "skip": {
                    "type": "integer",
                    "minimum": 0,
                    "description": "The number of items to skip. The default is 0."
                  },
                  "take": {
                    "type": "integer",
                    "minimum": 1,
                    "description": "The number of items to list. The default is 50000."
                  }
                },
                "required": [
                  "contentType"
                ],
                "additionalProperties": false
              },
              "path": {
                "type": "string",
                "description": "For customPath: the address on the site, for example pricing or docs/getting-started."
              }
            },
            "required": [
              "type"
            ],
            "additionalProperties": false
          }
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Creates a sitemap, or updates a sitemap or a sitemap index: name, path, enabled, and the sources of a sitemap (content type sources and custom path sources). The given source list replaces the existing one; the path must be unique and end in .xml. It cannot create a sitemap index or change the sitemaps an index contains.";

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

        var services = arguments.Services;

        if (!await AgentToolAuthorization.IsAuthorizedAsync(services, SitemapsPermissions.ManageSitemaps))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        if (!AgentToolArguments.TryReadJsonArray(arguments, "sources", out var sources))
        {
            return "Nothing was saved: sources must be a JSON array.";
        }

        var hasName = arguments.TryGetFirstString("name", out var name);
        var hasPath = arguments.TryGetFirstString("path", out var path);
        var hasEnabled = arguments.TryGetFirst<bool>("enabled", out var enabled);
        var sitemapManager = services.GetRequiredService<ISitemapManager>();
        var created = !arguments.TryGetFirstString("sitemapId", out var sitemapId);

        SitemapType sitemap;

        if (created)
        {
            if (!hasName)
            {
                return "Nothing was saved: give name to create a sitemap, or sitemapId to update one.";
            }

            sitemap = new Sitemap
            {
                SitemapId = services.GetRequiredService<ISitemapIdGenerator>().GenerateUniqueId(),
            };
        }
        else
        {
            sitemap = await sitemapManager.LoadSitemapAsync(sitemapId.Trim());

            if (sitemap is null)
            {
                return $"Nothing was saved: there is no sitemap with the ID '{sitemapId}'. Use listSitemaps to get the IDs.";
            }

            if (!hasName && !hasPath && !hasEnabled && sources is null)
            {
                return "Nothing was saved: give name, path, enabled or sources to change the sitemap.";
            }
        }

        if (sources is not null && sitemap is not Sitemap)
        {
            return "Nothing was saved: this is a sitemap index. The sitemaps it contains are changed on the Sitemap Indexes page; this tool changes its name, path and enabled state only.";
        }

        name = hasName
            ? name.Trim()
            : sitemap.Name;

        if (string.IsNullOrEmpty(name))
        {
            return "Nothing was saved: the name of a sitemap cannot be empty.";
        }

        var sitemapHelperService = services.GetRequiredService<ISitemapHelperService>();

        if (created && !hasPath)
        {
            path = sitemapHelperService.GetSitemapSlug(name);
            hasPath = true;
        }

        if (hasPath)
        {
            path = path.Trim();

            // The check of the Sitemaps admin pages: allowed characters, the .xml ending, and no other sitemap at the path.
            var updater = new NullModelUpdater();
            var ownSitemapId = created
                ? null
                : sitemap.SitemapId;

            await sitemapHelperService.ValidatePathAsync(path, updater, ownSitemapId);

            if (!updater.ModelState.IsValid)
            {
                var errors = updater.ModelState.Values
                    .SelectMany(entry => entry.Errors)
                    .Select(error => error.ErrorMessage);

                return $"Nothing was saved: the path '{path}' cannot be used. {string.Join(" ", errors)}";
            }
        }

        List<Func<SitemapSource>> sourcePlans = null;

        if (sources is not null)
        {
            (sourcePlans, var error) = await SitemapSourcePlanner.PlanAsync(services, sitemap, sources);

            if (error is not null)
            {
                return $"Nothing was saved: {error}";
            }
        }

        sitemap.Name = name;

        if (hasPath)
        {
            sitemap.Path = path;
        }

        if (hasEnabled)
        {
            sitemap.Enabled = enabled;
        }

        if (sourcePlans is not null)
        {
            sitemap.SitemapSources = sourcePlans.Select(plan => plan()).ToList();
        }

        await sitemapManager.UpdateSitemapAsync(sitemap);

        return AgentToolResults.Json(new
        {
            saved = true,
            created,
            sitemap = SitemapContent.Describe(sitemap),
            note = sitemap is Sitemap
                ? "The sitemap is built again, with these settings, the next time it is requested."
                : "The sitemaps a sitemap index contains are changed on the Sitemap Indexes page.",
        });
    }
}
