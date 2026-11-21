using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// A page post.
/// </summary>
public class SubmitPageRequest
{
    /// <summary>
    /// Gets or sets the id of the response.
    /// </summary>
    public string ResponseId { get; set; }

    /// <summary>
    /// Gets or sets who posts.
    /// </summary>
    public SurveyRespondentIdentity Identity { get; set; } = new();

    /// <summary>
    /// Gets or sets the id of the page the respondent posted, to catch a stale tab.
    /// </summary>
    public string PageId { get; set; }

    /// <summary>
    /// Gets or sets where the post goes.
    /// </summary>
    public SurveyPageDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the posted form, or <see langword="null"/> for JSON.
    /// </summary>
    public IFormCollection Form { get; set; }

    /// <summary>
    /// Gets or sets the posted answers by question id, or <see langword="null"/> for a form.
    /// </summary>
    public JsonObject Answers { get; set; }
}
