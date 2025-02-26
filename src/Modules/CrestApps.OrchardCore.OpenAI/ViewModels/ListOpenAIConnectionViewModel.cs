using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.OpenAI.ViewModels;

public sealed class ListOpenAIConnectionViewModel
{
    [BindNever]
    public int Count { get; set; }

    public OpenAIConnectionOptions Options { get; set; }

    public IShape Pager { get; set; }

    public IList<DisplayOpenAIConnectionViewModel> Records { get; set; }
}

public sealed class OpenAIConnectionOptions
{
    public string Search { get; set; }

    public OpenAIConnectionAction BulkAction { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> BulkActions { get; set; }

}

public enum OpenAIConnectionAction
{
    None,
    Remove,
}

public sealed class DisplayOpenAIConnectionViewModel
{
    public string Id { get; set; }

    public string DisplayText { get; set; }

    public string Name { get; set; }

    public string DefaultDeploymentName { get; set; }

    public Uri Endpoint { get; set; }
}

public sealed class OpenAIConnectionViewModel
{
    public string Name { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string DisplayText { get; set; }

    [Required]
    public Uri Endpoint { get; set; }

    public string ApiKey { get; set; }

    [Required(AllowEmptyStrings = false)]
    public string DefaultDeploymentName { get; set; }

    public bool IsDefaultConnection { get; set; }
}
