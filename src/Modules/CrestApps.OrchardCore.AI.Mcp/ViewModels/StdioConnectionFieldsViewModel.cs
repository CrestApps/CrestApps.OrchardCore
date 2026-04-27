using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

/// <summary>
/// Represents the view model for stdio connection fields.
/// </summary>
public class StdioConnectionFieldsViewModel
{
    /// <summary>
    /// Gets or sets the command.
    /// </summary>
    public string Command { get; set; }

    /// <summary>
    /// Gets or sets the arguments.
    /// </summary>
    public string Arguments { get; set; }

    /// <summary>
    /// Gets or sets the working directory.
    /// </summary>
    public string WorkingDirectory { get; set; }

    /// <summary>
    /// Gets or sets the environment variables.
    /// </summary>
    public string EnvironmentVariables { get; set; }

    /// <summary>
    /// Gets or sets the environment variables schema.
    /// </summary>
    [BindNever]
    public string EnvironmentVariablesSchema { get; set; }

    /// <summary>
    /// Gets or sets the arguments schema.
    /// </summary>
    [BindNever]
    public string ArgumentsSchema { get; set; }
}
