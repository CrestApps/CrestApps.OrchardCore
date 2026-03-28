using Microsoft.Extensions.AI;
using TextToSpeechResponse = CrestApps.AI.Models.TextToSpeechResponse;
using TextToSpeechResponseUpdate = CrestApps.AI.Models.TextToSpeechResponseUpdate;

namespace CrestApps.AI;

/// <summary>
/// Provides extension methods for working with <see cref="TextToSpeechResponseUpdate"/> instances.
/// </summary>
public static class TextToSpeechResponseUpdateExtensions
{
    /// <summary>
    /// Combines <see cref="TextToSpeechResponseUpdate"/> instances into a single <see cref="TextToSpeechResponse"/>.
    /// </summary>
    /// <param name="updates">The updates to be combined.</param>
    /// <returns>The combined <see cref="TextToSpeechResponse"/>.</returns>
    public static TextToSpeechResponse ToTextToSpeechResponse(
        this IEnumerable<TextToSpeechResponseUpdate> updates)
    {
        ArgumentNullException.ThrowIfNull(updates);

        var response = new TextToSpeechResponse();

        foreach (var update in updates)
        {
            ProcessUpdate(update, response);
        }

        return response;
    }

    /// <summary>
    /// Combines <see cref="TextToSpeechResponseUpdate"/> instances into a single <see cref="TextToSpeechResponse"/>.
    /// </summary>
    /// <param name="updates">The updates to be combined.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> to monitor for cancellation requests.
    /// </param>
    /// <returns>The combined <see cref="TextToSpeechResponse"/>.</returns>
    public static async Task<TextToSpeechResponse> ToTextToSpeechResponseAsync(
        this IAsyncEnumerable<TextToSpeechResponseUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(updates);

        var response = new TextToSpeechResponse();

        await foreach (var update in updates.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            ProcessUpdate(update, response);
        }

        return response;
    }

    private static void ProcessUpdate(TextToSpeechResponseUpdate update, TextToSpeechResponse response)
    {
        if (update.ResponseId is not null)
        {
            response.ResponseId = update.ResponseId;
        }

        if (update.ModelId is not null)
        {
            response.ModelId = update.ModelId;
        }

        foreach (var content in update.Contents)
        {
            if (content is UsageContent usage)
            {
                (response.Usage ??= new()).Add(usage.Details);
            }
            else
            {
                response.Contents.Add(content);
            }
        }

        if (update.AdditionalProperties is not null)
        {
            if (response.AdditionalProperties is null)
            {
                response.AdditionalProperties = new Dictionary<string, object>(update.AdditionalProperties);
            }
            else
            {
                foreach (var entry in update.AdditionalProperties)
                {
                    response.AdditionalProperties[entry.Key] = entry.Value;
                }
            }
        }
    }
}
