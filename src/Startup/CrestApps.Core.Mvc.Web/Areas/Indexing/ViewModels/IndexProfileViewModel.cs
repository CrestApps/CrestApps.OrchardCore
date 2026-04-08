using CrestApps.Core.Infrastructure.Indexing;
using CrestApps.Core.Infrastructure.Indexing.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Core.Mvc.Web.Areas.Indexing.ViewModels;

public sealed class IndexProfileViewModel
{
    public string ItemId { get; set; }

    public string Name { get; set; }

    public string DisplayText { get; set; }

    public string IndexName { get; set; }

    public string ProviderName { get; set; }

    public string Type { get; set; }

    public string EmbeddingDeploymentId { get; set; }

    [BindNever]
    public IReadOnlyList<IndexProfileSourceDescriptor> Sources { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> Providers { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> Types { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> EmbeddingDeployments { get; set; } = [];

    public static IndexProfileViewModel FromProfile(SearchIndexProfile profile)
    {
        return new IndexProfileViewModel
        {
            ItemId = profile.ItemId,
            Name = profile.Name,
            DisplayText = profile.DisplayText,
            IndexName = profile.IndexName,
            ProviderName = profile.ProviderName,
            Type = profile.Type,
            EmbeddingDeploymentId = profile.EmbeddingDeploymentId,
        };
    }

    public void ApplyTo(SearchIndexProfile profile)
    {
        profile.Name = Name?.Trim();
        profile.DisplayText = DisplayText?.Trim();
        profile.IndexName = IndexName?.Trim();
        profile.ProviderName = ProviderName;
        profile.Type = Type;
        profile.EmbeddingDeploymentId = EmbeddingDeploymentId;
    }
}
