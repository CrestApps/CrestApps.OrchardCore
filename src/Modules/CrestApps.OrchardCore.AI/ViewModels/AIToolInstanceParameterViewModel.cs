using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Parameters;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// One editable row in the tool instance parameter editor. Mirrors the CrestApps.Core sample host so the
/// mapping to and from <see cref="AIToolInstanceParameter"/> — including the "leave a secret blank to keep
/// it" rule — is written once.
/// </summary>
public sealed class AIToolInstanceParameterViewModel
{
    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the model-facing description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the declared JSON type.
    /// </summary>
    public AIToolParameterType Type { get; set; }

    /// <summary>
    /// Gets or sets who supplies the value.
    /// </summary>
    public AIToolParameterFill Fill { get; set; }

    /// <summary>
    /// Gets or sets whether the AI model must supply the value.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Gets or sets the default value. Held as text because the editor is a form; it is converted to the
    /// declared type when the instance is saved.
    /// </summary>
    public string DefaultValue { get; set; }

    /// <summary>
    /// Gets or sets the pinned value for a fixed parameter.
    /// </summary>
    public string FixedValue { get; set; }

    /// <summary>
    /// Gets or sets the accepted values as a comma-separated list.
    /// </summary>
    public string AllowedValues { get; set; }

    /// <summary>
    /// Gets or sets the context key for a context-filled parameter.
    /// </summary>
    public string ContextKey { get; set; }

    /// <summary>
    /// Gets or sets the selected placement target, such as <c>Query</c>.
    /// </summary>
    public string BindingTarget { get; set; }

    /// <summary>
    /// Gets or sets the target name within the placement. Defaults to the parameter name when left blank.
    /// </summary>
    public string BindingName { get; set; }

    /// <summary>
    /// Gets or sets whether the pinned value is a credential.
    /// </summary>
    public bool IsSecret { get; set; }

    /// <summary>
    /// Gets or sets whether a protected value is already stored, so the editor can show a placeholder
    /// instead of the secret and treat a blank input as "keep what is stored".
    /// </summary>
    public bool HasStoredSecret { get; set; }

    /// <summary>
    /// Builds the editable rows for an instance's declared parameters.
    /// </summary>
    public static List<AIToolInstanceParameterViewModel> FromParameters(IReadOnlyList<AIToolInstanceParameter> parameters)
    {
        var rows = new List<AIToolInstanceParameterViewModel>();

        if (parameters is null)
        {
            return rows;
        }

        foreach (var parameter in parameters)
        {
            if (parameter is null)
            {
                continue;
            }

            var hasSecret = parameter.IsSecret && parameter.DefaultValue is string { Length: > 0 };

            AIToolParameterBinding.TryParse(parameter.Binding, parameter.Name, out var binding);

            rows.Add(new AIToolInstanceParameterViewModel
            {
                Name = parameter.Name,
                Description = parameter.Description,
                Type = parameter.Type,
                Fill = parameter.Fill,
                Required = parameter.Required,

                // A stored secret is never rendered back into the form.
                DefaultValue = parameter.Fill == AIToolParameterFill.Fixed || hasSecret
                    ? null
                    : AIToolParameterValueConverter.ToStringValue(parameter.DefaultValue),
                FixedValue = parameter.Fill == AIToolParameterFill.Fixed && !hasSecret
                    ? AIToolParameterValueConverter.ToStringValue(parameter.DefaultValue)
                    : null,
                AllowedValues = parameter.AllowedValues is { Length: > 0 }
                    ? string.Join(", ", parameter.AllowedValues)
                    : null,
                ContextKey = parameter.ContextKey,
                BindingTarget = binding.Target,
                BindingName = binding.Name,
                IsSecret = parameter.IsSecret,
                HasStoredSecret = hasSecret,
            });
        }

        return rows;
    }

    /// <summary>
    /// Converts the editor rows back into declared parameters, carrying forward any stored secret the user
    /// left blank.
    /// </summary>
    public static List<AIToolInstanceParameter> ToParameters(
        IReadOnlyList<AIToolInstanceParameterViewModel> rows,
        IReadOnlyList<AIToolInstanceParameter> existing,
        Func<string, string> protect = null)
    {
        var parameters = new List<AIToolInstanceParameter>();

        if (rows is null)
        {
            return parameters;
        }

        foreach (var row in rows)
        {
            if (row is null || string.IsNullOrWhiteSpace(row.Name))
            {
                continue;
            }

            var name = row.Name.Trim();
            var isFixed = row.Fill == AIToolParameterFill.Fixed;

            var parameter = new AIToolInstanceParameter
            {
                Name = name,
                Description = row.Description?.Trim(),
                Type = row.Type,
                Fill = row.Fill,
                Required = row.Fill == AIToolParameterFill.Model && row.Required,
                ContextKey = row.Fill == AIToolParameterFill.Context
                    ? row.ContextKey?.Trim()
                    : null,
                Binding = BuildBinding(row, name),
                IsSecret = isFixed && row.IsSecret,
                AllowedValues = SplitAllowedValues(row.AllowedValues),
            };

            parameter.DefaultValue = ResolveDefaultValue(row, name, existing, protect);

            parameters.Add(parameter);
        }

        return parameters;
    }

    private static string BuildBinding(AIToolInstanceParameterViewModel row, string name)
    {
        if (string.IsNullOrWhiteSpace(row.BindingTarget))
        {
            return null;
        }

        var target = row.BindingTarget.Trim();
        var bindingName = row.BindingName?.Trim();

        return string.IsNullOrEmpty(bindingName) || string.Equals(bindingName, name, StringComparison.Ordinal)
            ? target
            : $"{target}:{bindingName}";
    }

    private static object ResolveDefaultValue(
        AIToolInstanceParameterViewModel row,
        string name,
        IReadOnlyList<AIToolInstanceParameter> existing,
        Func<string, string> protect)
    {
        var isFixed = row.Fill == AIToolParameterFill.Fixed;

        if (row.Fill == AIToolParameterFill.Context)
        {
            return null;
        }

        if (isFixed && row.IsSecret)
        {
            // A blank secret input means "keep the value already stored", matching how the rest of the
            // instance editor treats credentials.
            if (string.IsNullOrEmpty(row.FixedValue))
            {
                return FindExisting(existing, name)?.DefaultValue;
            }

            return protect is null
                ? row.FixedValue
                : protect(row.FixedValue);
        }

        var raw = isFixed
            ? row.FixedValue
            : row.DefaultValue;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return AIToolParameterValueConverter.TryConvert(raw.Trim(), row.Type, out var converted)
            ? converted
            : raw.Trim();
    }

    private static AIToolInstanceParameter FindExisting(IReadOnlyList<AIToolInstanceParameter> existing, string name)
    {
        if (existing is null)
        {
            return null;
        }

        foreach (var parameter in existing)
        {
            if (parameter is not null && string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return parameter;
            }
        }

        return null;
    }

    private static string[] SplitAllowedValues(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return parts.Length > 0
            ? parts
            : null;
    }
}

/// <summary>
/// Backs the tool instance parameters editor shape.
/// </summary>
public class EditToolInstanceParametersViewModel
{
    /// <summary>
    /// Gets or sets the editable parameter rows.
    /// </summary>
    public List<AIToolInstanceParameterViewModel> Parameters { get; set; } = [];

    /// <summary>
    /// Gets or sets the capability metadata keyed by tool instance source. Only sources that support
    /// parameters are included.
    /// </summary>
    public IDictionary<string, AIToolInstanceParameterCapabilities> ParameterCapabilities { get; set; }
        = new Dictionary<string, AIToolInstanceParameterCapabilities>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the context keys available for context-filled parameters.
    /// </summary>
    public IReadOnlyList<AIToolParameterContextKey> ContextKeys { get; set; } = [];
}
