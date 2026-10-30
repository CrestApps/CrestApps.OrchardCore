using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// The voice library page.
/// </summary>
public class AIVoiceLibraryViewModel
{
    /// <summary>Gets or sets the text-to-speech deployment.</summary>
    public string DeploymentName { get; set; }

    /// <summary>Gets or sets what to search for.</summary>
    public string Query { get; set; }

    /// <summary>Gets or sets the language.</summary>
    public string Language { get; set; }

    /// <summary>Gets or sets the page cursor.</summary>
    public string Cursor { get; set; }

    /// <summary>Gets or sets the next page's cursor.</summary>
    [BindNever]
    public string NextCursor { get; set; }

    /// <summary>Gets or sets a value indicating whether the deployment's provider has a library.</summary>
    [BindNever]
    public bool HasLibrary { get; set; }

    /// <summary>Gets or sets a value indicating whether it clones voices.</summary>
    [BindNever]
    public bool SupportsCloning { get; set; }

    /// <summary>Gets or sets the deployments.</summary>
    [BindNever]
    public IList<SelectListItem> Deployments { get; set; } = [];

    /// <summary>Gets or sets the voices.</summary>
    [BindNever]
    public AdminListContext<SpeechVoice> Voices { get; set; }
}
