using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.ContentTypes.Events;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

public sealed class RemoveContentPartDefinitionsTool : AIFunction
{
    public const string TheName = "removeContentPartDefinition";

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IEnumerable<IContentDefinitionEventHandler> _contentDefinitionEventHandlers;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<RemoveContentTypeDefinitionsTool> _logger;
    private readonly IAuthorizationService _authorizationService;

    public RemoveContentPartDefinitionsTool(
        IContentDefinitionManager contentDefinitionManager,
        IEnumerable<IContentDefinitionEventHandler> contentDefinitionEventHandlers,
        ILogger<RemoveContentTypeDefinitionsTool> logger,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService)
    {
        _contentDefinitionManager = contentDefinitionManager;
        _contentDefinitionEventHandlers = contentDefinitionEventHandlers;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _authorizationService = authorizationService;

        JsonSchema = JsonSerializer.Deserialize<JsonElement>(
            """
            {
              "type": "object",
              "properties": {
                "name": {
                  "type": "string",
                  "description": "The name of the content part for which to remove the definitions."
                }
              },
              "required": ["name"],
              "additionalProperties": false
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Removes the content part definition for a given content part.";

    public override JsonElement JsonSchema { get; }

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext.User, OrchardCorePermissions.EditContentTypes))
        {
            return "You do not have permission to edit content definitions.";
        }

        if (!arguments.TryGetFirstString("name", out var name))
        {
            return "Unable to find a name argument in the function arguments.";
        }

        await _contentDefinitionManager.LoadTypeDefinitionsAsync();
        await _contentDefinitionManager.LoadPartDefinitionsAsync();

        var partDefinition = await _contentDefinitionManager.LoadPartDefinitionAsync(name);

        if (partDefinition is null)
        {
            return
                $"""
                Unable to find a part definition that match the name: {name}.
                Here are the available part that can be removed:
                {JsonSerializer.Serialize((await _contentDefinitionManager.ListPartDefinitionsAsync()).Select(x => x.Name), JsonHelpers.ContentDefinitionSerializerOptions)}
                """;
        }

        var settings = partDefinition.GetSettings<ContentSettings>();

        if (settings.IsSystemDefined)
        {
            throw new InvalidOperationException("Unable to remove system-defined part.");
        }

        foreach (var fieldDefinition in partDefinition.Fields)
        {
            await RemoveFieldFromPartAsync(fieldDefinition.Name, name);
        }

        await _contentDefinitionManager.DeletePartDefinitionAsync(name);

        var context = new ContentPartRemovedContext
        {
            ContentPartDefinition = partDefinition,
        };

        _contentDefinitionEventHandlers.Invoke((handler, ctx) => handler.ContentPartRemoved(ctx), context, _logger);

        return $"The content part {name} was removed successfully";
    }

    private async Task RemoveFieldFromPartAsync(string fieldName, string partName)
    {
        var partDefinition = await _contentDefinitionManager.LoadPartDefinitionAsync(partName);

        if (partDefinition == null)
        {
            return;
        }

        var settings = partDefinition.GetSettings<ContentSettings>();

        if (settings.IsSystemDefined)
        {
            throw new InvalidOperationException("Unable to remove system-defined field.");
        }

        await _contentDefinitionManager.AlterPartDefinitionAsync(partName, typeBuilder => typeBuilder.RemoveField(fieldName));

        var context = new ContentFieldDetachedContext
        {
            ContentPartName = partName,
            ContentFieldName = fieldName,
        };

        _contentDefinitionEventHandlers.Invoke((handler, ctx) => handler.ContentFieldDetached(ctx), context, _logger);
    }
}
