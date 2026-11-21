using System.Text.Json.Serialization;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning.Assessments;

/// <summary>
/// Describes a question type: its content type and the name shown.
/// </summary>
public sealed class QuestionTypeDescriptor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuestionTypeDescriptor"/> class.
    /// </summary>
    /// <param name="name">The question type.</param>
    public QuestionTypeDescriptor(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Gets the question type.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets or sets the content type of its questions.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the name shown to people.
    /// </summary>
    [JsonIgnore]
    public LocalizationSource DisplayName { get; set; }

    /// <summary>
    /// Gets or sets whether a grader marks it.
    /// </summary>
    public bool ManuallyGraded { get; set; }

    /// <summary>
    /// Gets or sets whether a question of the type has nothing to answer and is worth no points, such as a description.
    /// </summary>
    public bool Informational { get; set; }

    /// <summary>
    /// Gets or sets whether a question of the type may be worth zero points, so it is ungraded.
    /// </summary>
    public bool MayBeUngraded { get; set; }
}

/// <summary>
/// The registered question types.
/// </summary>
public sealed class QuestionTypeOptions
{
    private readonly Dictionary<string, QuestionTypeDescriptor> _types = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the question types.
    /// </summary>
    public IReadOnlyCollection<QuestionTypeDescriptor> Types => _types.Values;

    /// <summary>
    /// Adds or replaces a question type.
    /// </summary>
    /// <param name="descriptor">The question type.</param>
    public void Add(QuestionTypeDescriptor descriptor)
        => _types[descriptor.Name] = descriptor;

    /// <summary>
    /// Finds the question type of a content type.
    /// </summary>
    /// <param name="contentType">The content type.</param>
    /// <returns>The question type, or <see langword="null"/>.</returns>
    public QuestionTypeDescriptor FindByContentType(string contentType)
        => _types.Values.FirstOrDefault(type => string.Equals(type.ContentType, contentType, StringComparison.Ordinal));

    /// <summary>
    /// Gets whether a content type is a question.
    /// </summary>
    /// <param name="contentType">The content type.</param>
    /// <returns><see langword="true"/> for a question type.</returns>
    public bool IsQuestion(string contentType)
        => FindByContentType(contentType) is not null;
}
