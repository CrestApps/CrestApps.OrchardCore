using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.AI.Agent.Contents;

/// <summary>
/// Checks the content item JSON a tool is given against its content type, the way createOrUpdateContentItem does, and
/// words what is wrong for the model.
/// </summary>
internal static class ContentItemPayloadCheck
{
    /// <summary>
    /// Says why the JSON does not fit the content type, with the structure the model should follow.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="definition">The content type the JSON is for.</param>
    /// <param name="payload">The JSON the tool was given.</param>
    /// <param name="subject">What the JSON holds, for example "nested content item".</param>
    /// <param name="argumentName">The name of the argument that carries the JSON.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The sentence for the result of the tool, or <see langword="null"/> when the JSON fits.</returns>
    public static async Task<string> GetErrorAsync(
        IServiceProvider services,
        ContentTypeDefinition definition,
        JsonObject payload,
        string subject,
        string argumentName,
        CancellationToken cancellationToken)
    {
        var payloadAssistanceService = services.GetRequiredService<IContentItemPayloadAssistanceService>();
        var result = await payloadAssistanceService.ValidateAsync(definition, payload, cancellationToken);

        if (result.IsValid)
        {
            return null;
        }

        var details = new List<string>(result.Messages);

        if (result.UnmappedPaths.Count > 0)
        {
            details.Add($"Unmapped or misplaced paths: {string.Join(", ", result.UnmappedPaths)}.");
        }

        var guidance = string.IsNullOrWhiteSpace(result.Guidance)
            ? await payloadAssistanceService.GetGuidanceAsync(definition, cancellationToken)
            : result.Guidance;

        return
        $"""
        Nothing was saved: the {subject} JSON does not match the content type '{definition.Name}'.
        {string.Join(Environment.NewLine, details)}
        Call the function again with a {argumentName} that follows the structure below.

        {guidance}
        """;
    }
}
