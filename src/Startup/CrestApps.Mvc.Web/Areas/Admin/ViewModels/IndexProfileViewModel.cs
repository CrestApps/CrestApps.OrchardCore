using CrestApps.AI;
using CrestApps.AI.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Mvc.Web.Areas.Admin.ViewModels;

public sealed class IndexProfileViewModel
{
    public string ItemId { get; set; }

    public string Name { get; set; }

    public string DisplayText { get; set; }

    public string IndexName { get; set; }

    public string ProviderName { get; set; }

    public string Type { get; set; }

    public string EmbeddingDeploymentId { get; set; }

    public List<SelectListItem> Providers { get; set; } = [];

    public List<SelectListItem> Types { get; set; } = [];

    public List<SelectListItem> EmbeddingDeployments { get; set; } = [];

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
        profile.IndexFullName = IndexName?.Trim();
    }
}
