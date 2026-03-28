using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace CrestApps.AI.Models;

/// <summary>
/// Represents a single streaming response chunk from an <see cref="ITextToSpeechClient"/>.
/// </summary>
public class TextToSpeechResponseUpdate
{
    private IList<AIContent> _contents;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextToSpeechResponseUpdate"/> class.
    /// </summary>
    [JsonConstructor]
    public TextToSpeechResponseUpdate()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TextToSpeechResponseUpdate"/> class.
    /// </summary>
    /// <param name="contents">The contents for this update.</param>
    public TextToSpeechResponseUpdate(IList<AIContent> contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        _contents = contents;
    }

    /// <summary>
    /// Gets or sets the kind of the generated audio speech update.
    /// </summary>
    public TextToSpeechResponseUpdateKind Kind { get; set; } = TextToSpeechResponseUpdateKind.AudioUpdating;

    /// <summary>
    /// Gets or sets the ID of the generated audio speech response of which this update is a part.
    /// </summary>
    public string ResponseId { get; set; }

    /// <summary>
    /// Gets or sets the model ID used in the creation of the text to speech of which this update is a part.
    /// </summary>
    public string ModelId { get; set; }

    /// <summary>
    /// Gets or sets the raw representation of the generated audio speech update from an underlying implementation.
    /// </summary>
    [JsonIgnore]
    public object RawRepresentation { get; set; }

    /// <summary>
    /// Gets or sets additional properties for the update.
    /// </summary>
    public IDictionary<string, object> AdditionalProperties { get; set; }

    /// <summary>
    /// Gets or sets the generated content items.
    /// </summary>
    public IList<AIContent> Contents
    {
        get => _contents ??= [];
        set => _contents = value;
    }
}
