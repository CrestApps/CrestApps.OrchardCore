using System.Text.Json;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Omnichannel.Managements.AI;

/// <summary>
/// Lists the subject flows, as the Subject Flows page does, and for one subject every action per disposition, as its
/// Manage Actions page does.
/// </summary>
internal sealed class ListSubjectFlowsTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "listSubjectFlows";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "subjectContentType": {
          "type": "string",
          "description": "The technical name of one subject content type. Give it to get that subject's flow settings and every action per disposition; leave it out to list all subjects."
        },
        "page": {
          "type": "integer",
          "minimum": 1,
          "description": "The page of subjects to return, from 1."
        },
        "pageSize": {
          "type": "integer",
          "minimum": 1,
          "maximum": 50,
          "description": "How many subjects a page holds, at most 50. Defaults to 20."
        }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Lists the subject content types with their flow settings and, for one subject, every action per disposition (type, what it does, do-not-contact flags). Use it to explain or audit what happens after a disposition.";

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

        if (!await AIModuleTools.IsAuthorizedAsync(services, OmnichannelConstants.Permissions.ManageSubjectFlows))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var flowSettingsService = services.GetRequiredService<ISubjectFlowSettingsService>();
        var actions = await services.GetRequiredService<ISourceCatalog<SubjectAction>>().GetAllAsync(cancellationToken);
        var subjectContentType = AIModuleTools.ReadString(arguments, "subjectContentType");

        if (subjectContentType is not null)
        {
            return await DescribeSubjectAsync(services, flowSettingsService, actions, subjectContentType, cancellationToken);
        }

        var (page, pageSize) = AIModuleTools.ReadPaging(arguments);
        var subjects = (await flowSettingsService.GetConfiguredSubjectTypesAsync(cancellationToken))
            .OrderBy(type => type.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var items = new List<object>();

        foreach (var subject in subjects.Skip((page - 1) * pageSize).Take(pageSize))
        {
            var flow = await flowSettingsService.FindConfiguredFlowSettingsAsync(subject.Name, cancellationToken);

            items.Add(new
            {
                subjectContentType = subject.Name,
                displayName = subject.DisplayName,
                flow = SubjectFlowToolSupport.DescribeFlow(flow),
                actionCount = actions.Count(action => string.Equals(action.SubjectContentType, subject.Name, StringComparison.OrdinalIgnoreCase)),
            });
        }

        return AIModuleTools.Json(new
        {
            items,
            total = subjects.Count,
            page,
            pageSize,
            truncated = page * pageSize < subjects.Count,
        });
    }

    private static async Task<string> DescribeSubjectAsync(
        IServiceProvider services,
        ISubjectFlowSettingsService flowSettingsService,
        IEnumerable<SubjectAction> actions,
        string subjectContentType,
        CancellationToken cancellationToken)
    {
        var contentType = await services.GetRequiredService<IContentDefinitionManager>().GetTypeDefinitionAsync(subjectContentType);

        if (!OmnichannelSubjectDefinitionService.HasOmnichannelSubjectPart(contentType))
        {
            return $"No subject content type is named '{subjectContentType}'. Call listSubjectFlows without arguments to see the subjects.";
        }

        var options = services.GetRequiredService<IOptions<SubjectActionOptions>>().Value;
        var dispositions = (await services.GetRequiredService<INamedCatalog<OmnichannelDisposition>>().GetAllAsync(cancellationToken))
            .ToDictionary(disposition => disposition.ItemId, disposition => disposition.Name, StringComparer.OrdinalIgnoreCase);
        var subjectActions = actions
            .Where(action => string.Equals(action.SubjectContentType, contentType.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(action => action.DispositionId)
            .ThenBy(action => action.Source)
            .Select(action => SubjectFlowToolSupport.DescribeAction(
                action,
                dispositions.GetValueOrDefault(action.DispositionId ?? string.Empty),
                options.ActionTypes.TryGetValue(action.Source ?? string.Empty, out var type) ? type.DisplayName?.Value : null))
            .ToList();

        return AIModuleTools.Json(new
        {
            subjectContentType = contentType.Name,
            displayName = contentType.DisplayName,
            flow = SubjectFlowToolSupport.DescribeFlow(await flowSettingsService.FindConfiguredFlowSettingsAsync(contentType.Name, cancellationToken)),
            actionTypes = options.ActionTypes.Values.Select(type => new
            {
                type = type.Type,
                displayName = type.DisplayName?.Value ?? type.Type,
            }),
            actions = subjectActions,
            manageUrl = AIModuleTools.GetAdminPath(
                services,
                OmnichannelToolSupport.Area,
                "SubjectFlows",
                "ManageActions",
                new RouteValueDictionary { ["subjectContentType"] = contentType.Name }),
        });
    }
}
