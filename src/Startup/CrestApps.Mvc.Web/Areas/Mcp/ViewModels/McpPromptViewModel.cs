using System.ComponentModel.DataAnnotations;

namespace CrestApps.Mvc.Web.Areas.Mcp.ViewModels;

public sealed class McpPromptViewModel
{
    public string ItemId { get; set; }

    [Required]
    public string Name { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public string Arguments { get; set; } = "[]";
}
