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

public sealed class RemoveContentTypeDefinitionsTool : AIFunction
{
    public const string TheName = "removeContentTypeDefinition";

    private readonly IContentDefinitionManager _contentDefinitionManager;
    private readonly IEnumerable<IContentDefinitionEventHandler> _contentDefinitionEventHandlers;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;
    private readonly IAuthorizationService _authorizationService;

    public RemoveContentTypeDefinitionsTool(
        IContentDefinitionManager contentDefinitionManager,
        IEnumerable<IContentDefinitionEventHandler> contentDefinitionEventHandlers,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RemoveContentTypeDefinitionsTool> logger,
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
                  "description": "The name of the content type for which to remove the definitions."
                }
              },
              "required": ["name"],
              "additionalProperties": false
            }
            """, JsonSerializerOptions);
    }

    public override string Name => TheName;

    public override string Description => "Removes the content type definition for a given content type.";

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

        var typeDefinition = await _contentDefinitionManager.LoadTypeDefinitionAsync(name);

        if (typeDefinition is null)
        {
            return
                $"""
                Unable to find a type definition that match the name: {name}.
                Here are the available content types that can be removed:
                {JsonSerializer.Serialize((await _contentDefinitionManager.ListTypeDefinitionsAsync()).Select(x => x.Name), JsonHelpers.ContentDefinitionSerializerOptions)}
                """;
        }

        var settings = typeDefinition.GetSettings<ContentSettings>();

        if (settings.IsSystemDefined)
        {
            throw new InvalidOperationException("Unable to remove system-defined type.");
        }

        var partDefinitions = typeDefinition.Parts.ToList();
        foreach (var partDefinition in partDefinitions)
        {
            await RemovePartFromTypeAsync(partDefinition.PartDefinition.Name, name);

            // Delete the part if it's its own part.
            if (partDefinition.PartDefinition.Name == name)
            {
                await RemovePartAsync(name);
            }
        }

        await _contentDefinitionManager.DeleteTypeDefinitionAsync(name);

        var context = new ContentTypeRemovedContext
        {
            ContentTypeDefinition = typeDefinition,
        };

        _contentDefinitionEventHandlers.Invoke((handler, ctx) => handler.ContentTypeRemoved(ctx), context, _logger);

        return $"The content type {name} was removed successfully";
    }

    private async Task RemovePartFromTypeAsync(string partName, string typeName)
    {
        var typeDefinition = await _contentDefinitionManager.LoadTypeDefinitionAsync(typeName);

        if (typeDefinition == null)
        {
            return;
        }

        var partDefinition = typeDefinition.Parts.FirstOrDefault(p => string.Equals(p.Name, partName, StringComparison.OrdinalIgnoreCase));

        if (partDefinition == null)
        {
            return;
        }

        var settings = partDefinition.GetSettings<ContentSettings>();

        if (settings.IsSystemDefined)
        {
            throw new InvalidOperationException("Unable to remove system-defined part.");
        }

        await _contentDefinitionManager.AlterTypeDefinitionAsync(typeName, typeBuilder => typeBuilder.RemovePart(partName));

        var context = new ContentPartDetachedContext
        {
            ContentTypeName = typeName,
            ContentPartName = partName,
        };

        _contentDefinitionEventHandlers.Invoke((handler, ctx) => handler.ContentPartDetached(ctx), context, _logger);
    }

    private async Task RemovePartAsync(string name)
    {
        var partDefinition = await _contentDefinitionManager.LoadPartDefinitionAsync(name);

        if (partDefinition == null)
        {
            // Couldn't find this named part, ignore it.
            return;
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
