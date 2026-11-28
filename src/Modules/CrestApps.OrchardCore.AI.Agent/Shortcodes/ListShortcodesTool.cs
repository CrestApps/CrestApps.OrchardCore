using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Shortcodes;
using OrchardCore.Shortcodes.Services;

namespace CrestApps.OrchardCore.AI.Agent.Shortcodes;

/// <summary>
/// Lists the shortcodes of the site from the shortcode catalog, which is what the shortcode picker of the HTML and
/// Markdown editors shows. The picker checks no permission, so the tool asks only for a signed-in caller.
/// </summary>
public sealed class ListShortcodesTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listShortcodes";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "search": {
          "type": "string",
          "description": "Keep the shortcodes whose name, hint or category contains this text. The case is ignored."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page to return, starting at 1. The default is 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "The number of shortcodes on a page. The default is 20 and the maximum is 50."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists every shortcode available on the site (built-in and template-based) with its hint, usage, default value and categories. Use it to learn which shortcodes can be used in HTML or Markdown bodies before writing content. It does not return the Liquid content of a shortcode template.";

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

        // The catalog is shown to everyone who can open an editor; a tool still needs to know who is asking.
        if (AgentToolAuthorization.GetCaller(services) is null)
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var descriptors = await services.GetRequiredService<IShortcodeDescriptorManager>().GetShortcodeDescriptors();

        if (arguments.TryGetFirstString("search", out var search))
        {
            search = search.Trim();
            descriptors = descriptors.Where(descriptor => Matches(descriptor, search));
        }

        var matches = descriptors
            .OrderBy(descriptor => descriptor.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var (page, pageSize) = AgentToolResults.ReadPaging(arguments);

        return AgentToolResults.Json(new
        {
            items = matches
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(descriptor => new
                {
                    name = descriptor.Name,
                    hint = descriptor.Hint,
                    usage = descriptor.Usage,
                    defaultValue = descriptor.DefaultValue,
                    categories = descriptor.Categories ?? [],
                }),
            total = matches.Count,
            page,
            pageSize,
        });
    }

    private static bool Matches(ShortcodeDescriptor descriptor, string search)
    {
        return descriptor.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
            descriptor.Hint?.Contains(search, StringComparison.OrdinalIgnoreCase) == true ||
            descriptor.Categories?.Any(category => category?.Contains(search, StringComparison.OrdinalIgnoreCase) == true) == true;
    }
}
