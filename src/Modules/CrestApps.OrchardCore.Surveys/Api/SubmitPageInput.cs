using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Surveys.Api;

/// <summary>
/// The answers of a page posted through the API.
/// </summary>
public sealed class SubmitPageInput
{
    /// <summary>
    /// Gets or sets the answers by question id; each value is what the question type reads (choice ids, text, a number).
    /// </summary>
    public JsonObject Answers { get; set; } = [];

    /// <summary>
    /// Gets or sets where the post goes: <c>next</c> (default) or <c>back</c>.
    /// </summary>
    public string Direction { get; set; }
}
