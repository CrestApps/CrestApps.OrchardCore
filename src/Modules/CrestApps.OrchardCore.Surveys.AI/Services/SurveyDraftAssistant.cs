using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Surveys.Core.Schemas;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Json.Schema;
using Microsoft.AspNetCore.Authorization;

namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// Drafts a new survey from an author's instructions or a document: the model writes a definition that follows the
/// definition JSON schema, the draft is checked against the schema and saved as a draft. It is never published.
/// </summary>
public sealed class SurveyDraftAssistant
{
    /// <summary>
    /// No AI model is set up.
    /// </summary>
    public const string NoModel = "No AI model is set up for this site.";

    /// <summary>
    /// The model's answer was not a valid survey definition.
    /// </summary>
    public const string InvalidDraft = "The AI model did not write a valid survey definition. Try again with clearer instructions.";

    /// <summary>
    /// The user may not create surveys.
    /// </summary>
    public const string NotAllowed = "The user isn't allowed to create surveys.";

    private const int MaxInstructionsLength = 20_000;

    private static readonly JsonSerializerOptions _definitionOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly ISurveysAICompleter _completer;
    private readonly SurveyDefinitionSchemaBuilder _schemaBuilder;
    private readonly ISurveyControlRegistry _registry;
    private readonly ISurveyManager _surveys;
    private readonly ISurveyDefinitionValidator _validator;
    private readonly IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyDraftAssistant"/> class.
    /// </summary>
    /// <param name="completer">The AI completer.</param>
    /// <param name="schemaBuilder">The definition schema builder.</param>
    /// <param name="registry">The question type registry.</param>
    /// <param name="surveys">The survey manager.</param>
    /// <param name="validator">The definition validator.</param>
    /// <param name="authorizationService">The authorization service.</param>
    public SurveyDraftAssistant(
        ISurveysAICompleter completer,
        SurveyDefinitionSchemaBuilder schemaBuilder,
        ISurveyControlRegistry registry,
        ISurveyManager surveys,
        ISurveyDefinitionValidator validator,
        IAuthorizationService authorizationService)
    {
        _completer = completer;
        _schemaBuilder = schemaBuilder;
        _registry = registry;
        _surveys = surveys;
        _validator = validator;
        _authorizationService = authorizationService;
    }

    /// <summary>
    /// Drafts a survey.
    /// </summary>
    /// <param name="user">The author.</param>
    /// <param name="title">The title, or <see langword="null"/> to take the model's.</param>
    /// <param name="instructions">The instructions or document text.</param>
    /// <param name="kind">The kind of survey, or <see langword="null"/> for a survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The draft or the error.</returns>
    public async Task<SurveyDraftResult> DraftAsync(ClaimsPrincipal user, string title, string instructions, string kind, CancellationToken cancellationToken)
    {
        if (!await _authorizationService.AuthorizeAsync(user, SurveysPermissions.ManageOwnSurveys))
        {
            return new SurveyDraftResult { Error = NotAllowed };
        }

        if (string.IsNullOrWhiteSpace(instructions))
        {
            return new SurveyDraftResult { Error = InvalidDraft };
        }

        instructions = instructions.Length > MaxInstructionsLength ? instructions.Substring(0, MaxInstructionsLength) : instructions;

        var schema = _schemaBuilder.BuildDefinition().Build();
        var output = await _completer.CompleteAsync(
            SurveysAIConstants.Templates.DraftSurvey,
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                ["Schema"] = JsonSerializer.Serialize(schema),
                ["ControlTypes"] = string.Join(", ", _registry.GetControls().Select(control => control.Name)),
            },
            instructions,
            cancellationToken);

        if (output is null)
        {
            return new SurveyDraftResult { Error = NoModel };
        }

        var definition = Read(output, schema);

        if (definition is null)
        {
            return new SurveyDraftResult { Error = InvalidDraft };
        }

        var survey = await _surveys.CreateSurveyAsync(new SurveyCreateRequest
        {
            Title = string.IsNullOrWhiteSpace(title) ? definition.Title : title.Trim(),
            Kind = DraftKind(kind),
            Definition = definition,
        }, cancellationToken);

        var validation = await _validator.ValidateAsync(survey, survey.Draft, cancellationToken);

        return new SurveyDraftResult
        {
            Survey = survey,
            Messages = validation.Messages,
        };
    }

    /// <summary>
    /// Gets the kind a drafted survey takes. The tool's schema offers the built-in kinds only, and the model's value is
    /// checked here rather than trusted: anything else drafts a survey.
    /// </summary>
    /// <param name="kind">The kind the tool was given.</param>
    /// <returns>A built-in kind.</returns>
    public static string DraftKind(string kind)
    {
        var trimmed = kind?.Trim();

        return SurveyKinds.IsBuiltIn(trimmed) ? trimmed : SurveyKinds.Survey;
    }

    /// <summary>
    /// Reads a definition from a model's answer when it follows the schema.
    /// </summary>
    /// <param name="output">The model's answer.</param>
    /// <param name="schema">The definition schema.</param>
    /// <returns>The definition, or <see langword="null"/>.</returns>
    public static SurveyDefinition Read(string output, JsonSchema schema)
    {
        ArgumentNullException.ThrowIfNull(schema);

        var json = SurveysAIJson.ReadObject(output);

        if (json is null)
        {
            return null;
        }

        try
        {
            var element = JsonSerializer.SerializeToElement(json);

            if (!schema.Evaluate(element, new EvaluationOptions { OutputFormat = OutputFormat.Flag }).IsValid)
            {
                return null;
            }

            var definition = json.Deserialize<SurveyDefinition>(_definitionOptions);

            return definition?.Pages is { Count: > 0 } ? definition : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
