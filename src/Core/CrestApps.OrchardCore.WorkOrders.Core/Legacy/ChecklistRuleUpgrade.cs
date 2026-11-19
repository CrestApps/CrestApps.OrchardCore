using CrestApps.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.WorkOrders.Core.Legacy;

/// <summary>
/// Rewrites the conditions of checklist templates saved before the shared rules (<see cref="ChecklistCondition"/>) as
/// <see cref="ChecklistQuestion.Rule"/>. Templates already converted are left alone, so a second run changes nothing.
/// </summary>
internal static class ChecklistRuleUpgrade
{
    /// <summary>
    /// Converts the templates.
    /// </summary>
    /// <param name="templates">The checklist templates.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>How many templates were converted.</returns>
    public static async Task<int> ConvertAsync(ICatalog<ChecklistTemplate> templates, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(templates);

        var converted = 0;

        foreach (var template in await templates.GetAllAsync())
        {
            if (!template.Questions.Any(question => question.ShowWhen is not null))
            {
                continue;
            }

            foreach (var question in template.Questions)
            {
                question.Rule = question.GetRule();
                question.ShowWhen = null;
            }

            await templates.UpdateAsync(template);
            converted++;
        }

        if (converted > 0 && logger?.IsEnabled(LogLevel.Information) == true)
        {
            logger.LogInformation("Kept the question conditions of {Count} checklist templates as shared rules.", converted);
        }

        return converted;
    }
}
