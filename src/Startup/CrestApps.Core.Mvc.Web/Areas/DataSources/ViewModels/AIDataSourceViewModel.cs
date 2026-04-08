using CrestApps.Core.AI.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.Core.Mvc.Web.Areas.DataSources.ViewModels;

public sealed class AIDataSourceViewModel
{
    public string ItemId { get; set; }

    public string DisplayText { get; set; }

    public string SourceIndexProfileName { get; set; }

    public string AIKnowledgeBaseIndexProfileName { get; set; }

    public string KeyFieldName { get; set; }

    public string TitleFieldName { get; set; }

    public string ContentFieldName { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> SourceIndexProfiles { get; set; } = [];

    [BindNever]
    public IEnumerable<SelectListItem> KnowledgeBaseIndexProfiles { get; set; } = [];

    public static AIDataSourceViewModel FromDataSource(AIDataSource ds)
    {
        return new AIDataSourceViewModel
        {
            ItemId = ds.ItemId,
            DisplayText = ds.DisplayText,
            SourceIndexProfileName = ds.SourceIndexProfileName,
            AIKnowledgeBaseIndexProfileName = ds.AIKnowledgeBaseIndexProfileName,
            KeyFieldName = ds.KeyFieldName,
            TitleFieldName = ds.TitleFieldName,
            ContentFieldName = ds.ContentFieldName,
        };
    }

    public void ApplyTo(AIDataSource ds)
    {
        ds.DisplayText = DisplayText?.Trim();
        ds.SourceIndexProfileName = SourceIndexProfileName;
        ds.AIKnowledgeBaseIndexProfileName = AIKnowledgeBaseIndexProfileName;
        ds.KeyFieldName = KeyFieldName?.Trim();
        ds.TitleFieldName = TitleFieldName?.Trim();
        ds.ContentFieldName = ContentFieldName?.Trim();
    }
}
