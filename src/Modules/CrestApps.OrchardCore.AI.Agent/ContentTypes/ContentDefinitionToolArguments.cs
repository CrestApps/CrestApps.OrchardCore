using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using OrchardCore.ContentManagement.Utilities;

namespace CrestApps.OrchardCore.AI.Agent.ContentTypes;

/// <summary>
/// Reads and checks the arguments the content definition tools share.
/// </summary>
internal static class ContentDefinitionToolArguments
{
    /// <summary>
    /// Reads the <c>settings</c> argument: a JSON object keyed by settings class name, the shape a content definition
    /// recipe uses.
    /// </summary>
    /// <param name="arguments">The arguments of the tool invocation.</param>
    /// <param name="settings">The settings, or <see langword="null"/> when none are given.</param>
    /// <returns><see langword="false"/> when the argument is given but is not an object of settings objects.</returns>
    public static bool TryReadSettings(AIFunctionArguments arguments, out JsonObject settings)
    {
        if (!AgentToolArguments.TryReadJsonObject(arguments, "settings", out settings))
        {
            return false;
        }

        if (settings is not null && settings.Any(setting => setting.Value is not JsonObject))
        {
            settings = null;

            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks a technical name with the rules the content definition editor applies.
    /// </summary>
    /// <param name="name">The technical name.</param>
    /// <param name="rejectReservedNames">Whether a name reserved for internal use is refused, as for a named part.</param>
    /// <returns>What is wrong with the name, or <see langword="null"/> when it is valid.</returns>
    public static string ValidateTechnicalName(string name, bool rejectReservedNames)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "The technical name can't be empty.";
        }

        if (!char.IsLetter(name[0]))
        {
            return "The technical name must start with a letter.";
        }

        if (!string.Equals(name, name.ToSafeName(), StringComparison.OrdinalIgnoreCase))
        {
            return "The technical name contains invalid characters. Use letters and digits only.";
        }

        if (rejectReservedNames && name.IsReservedContentName())
        {
            return "The technical name is reserved for internal use.";
        }

        return null;
    }
}
