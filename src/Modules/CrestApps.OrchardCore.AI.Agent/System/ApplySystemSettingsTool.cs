using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Agent.System;

/// <summary>
/// Represents the apply system settings tool.
/// </summary>
public sealed class ApplySystemSettingsTool : ImportRecipeBaseTool
{
    public const string TheName = "applySiteSettings";

    public override string Name => TheName;

    public override string Description => "Applies site settings or configurations to the system.";

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var logger = arguments.Services.GetRequiredService<ILogger<ApplySystemSettingsTool>>();

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' invoked.", Name);
        }

        if (!arguments.TryGetFirstString("recipe", out var recipe))
        {
            logger.LogWarning("AI tool '{ToolName}': missing 'recipe' argument.", Name);

            return MissingArgument();
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("AI tool '{ToolName}' completed.", Name);
        }

        return await ProcessRecipeAsync(arguments.Services, recipe, logger, cancellationToken);
    }
}
