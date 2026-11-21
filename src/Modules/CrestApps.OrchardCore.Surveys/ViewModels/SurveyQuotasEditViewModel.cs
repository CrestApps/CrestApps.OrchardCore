using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The quotas of a survey definition in the designer.
/// </summary>
public class SurveyQuotasEditViewModel
{
    /// <summary>
    /// Gets or sets the quotas.
    /// </summary>
    public List<SurveyQuota> Quotas { get; set; } = [];

    /// <summary>
    /// Gets or sets the rule of each quota as JSON, in the order of <see cref="Quotas"/>.
    /// </summary>
    public List<string> QuotaRules { get; set; } = [];

    /// <summary>
    /// Gets or sets the endings a quota may screen out with.
    /// </summary>
    [BindNever]
    public List<SurveyEnding> Endings { get; set; } = [];

    /// <summary>
    /// Gets or sets the sources the rule builder offers, as JSON.
    /// </summary>
    [BindNever]
    public string Sources { get; set; }
}
