using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.AI.Mcp.ViewModels;

public class StdioConnectionFieldsViewModel
{
    public string Command { get; set; }

    public string Arguments { get; set; }

    public string WorkingDirectory { get; set; }

    public string EnvironmentVariables { get; set; }

    [BindNever]
    public string EnvironmentVariablesSchema { get; set; }

    [BindNever]
    public string ArgumentsSchema { get; set; }
}
