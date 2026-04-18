using System.Globalization;
using CrestApps.Core.AI.Copilot.Models;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Services;

internal static class CopilotModelDisplayTextFormatter
{
    public static string Format(CopilotModelInfo model)
    {
        if (model is null)
        {
            return string.Empty;
        }

        if (model.CostMultiplier <= 0)
        {
            return model.Name;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{model.Name} (x{model.CostMultiplier:0.##})");
    }
}
