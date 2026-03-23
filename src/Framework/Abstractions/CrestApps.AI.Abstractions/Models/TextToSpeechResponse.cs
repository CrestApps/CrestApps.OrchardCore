using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace CrestApps.AI.Models;

/// <summary>
/// Represents the result of a text to speech request.
/// </summary>
public class TextToSpeechResponse
{
    private IList<AIContent> _contents;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextToSpeechResponse"/> class.
    /// </summary>
    [JsonConstructor]
    public TextToSpeechResponse()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TextToSpeechResponse"/> class.
    /// </summary>
    /// <param name="contents">The contents for this response.</param>
    public TextToSpeechResponse(IList<AIContent> contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        _contents = contents;
    }

    /// <summary>
    /// Gets or sets the ID of the text to speech response.
    /// </summary>
    public string ResponseId { get; set; }

    /// <summary>
    /// Gets or sets the model ID used in the creation of the text to speech response.
    /// </summary>
    public string ModelId { get; set; }

    /// <summary>
    /// Gets or sets the raw representation of the text to speech response from an underlying implementation.
    /// </summary>
    [JsonIgnore]
    public object RawRepresentation { get; set; }

    /// <summary>
    /// Gets or sets any additional properties associated with the text to speech response.
    /// </summary>
    public IDictionary<string, object> AdditionalProperties { get; set; }

    /// <summary>
    /// Creates an array of <see cref="TextToSpeechResponseUpdate"/> instances that represent this
    /// <see cref="TextToSpeechResponse"/>.
    /// </summary>
    /// <returns>An array of <see cref="TextToSpeechResponseUpdate"/> instances.</returns>
    public TextToSpeechResponseUpdate[] ToTextToSpeechResponseUpdates()
    {
        var update = new TextToSpeechResponseUpdate
        {
            Contents = Contents,
            AdditionalProperties = AdditionalProperties,
            RawRepresentation = RawRepresentation,
            Kind = TextToSpeechResponseUpdateKind.AudioUpdated,
            ResponseId = ResponseId,
            ModelId = ModelId,
        };

        return [update];
    }

    /// <summary>
    /// Gets or sets the generated content items.
    /// </summary>
    public IList<AIContent> Contents
    {
        get => _contents ??= [];
        set => _contents = value;
    }

    /// <summary>
    /// Gets or sets usage details for the text to speech response.
    /// </summary>
    public UsageDetails Usage { get; set; }
}
