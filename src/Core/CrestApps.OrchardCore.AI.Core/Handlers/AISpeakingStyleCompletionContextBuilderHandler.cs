using CrestApps.Core;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Templates.Services;
using CrestApps.OrchardCore.AI.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Adds the sections of the profile's speaking styles to the end of its system message, after the persona and
/// instructions they refine.
/// </summary>
public sealed class AISpeakingStyleCompletionContextBuilderHandler : IAICompletionContextBuilderHandler
{
    private readonly ITemplateService _templateService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AISpeakingStyleCompletionContextBuilderHandler"/> class.
    /// </summary>
    /// <param name="templateService">The template service the style sections are read from.</param>
    /// <param name="logger">The logger.</param>
    public AISpeakingStyleCompletionContextBuilderHandler(
        ITemplateService templateService,
        ILogger<AISpeakingStyleCompletionContextBuilderHandler> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task BuildingAsync(AICompletionContextBuildingContext context)
        => Task.CompletedTask;

    /// <inheritdoc/>
    public async Task BuiltAsync(AICompletionContextBuiltContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Resource is not AIProfile profile ||
            !profile.TryGetSettings<AIProfileSpeakingStyleSettings>(out var settings))
        {
            return;
        }

        var sections = new List<string>();

        foreach (var style in AISpeakingStyles.Normalize(settings.Styles))
        {
            try
            {
                var section = await _templateService.RenderAsync(AISpeakingStyles.GetTemplateId(style));

                if (!string.IsNullOrWhiteSpace(section))
                {
                    sections.Add(section.Trim());
                }
            }
            catch (Exception ex)
            {
                // A missing or broken template leaves its style out rather than failing the conversation.
                _logger.LogWarning(ex, "The speaking style '{Style}' could not be added to the system message.", style);
            }
        }

        if (sections.Count == 0)
        {
            return;
        }

        context.Context.SystemMessage = string.IsNullOrWhiteSpace(context.Context.SystemMessage)
            ? string.Join(Environment.NewLine + Environment.NewLine, sections)
            : context.Context.SystemMessage.TrimEnd() + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine + Environment.NewLine, sections);
    }
}
