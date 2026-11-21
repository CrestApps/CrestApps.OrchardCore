using CrestApps.OrchardCore.Surveys.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DataSources.Expressions;

namespace CrestApps.OrchardCore.Surveys.Core.Formulas;

/// <summary>
/// The functions survey formulas may call: the survey functions first, then the tenant's formula functions (or the
/// built-in ones without Data Pipelines). Survey functions are not added to the tenant catalog, so pipeline editors do
/// not list them.
/// </summary>
public sealed class SurveyExpressionFunctionCatalog : IExpressionFunctionCatalog
{
    private readonly Dictionary<string, ExpressionFunction> _functions = new(StringComparer.OrdinalIgnoreCase);
    private readonly IExpressionFunctionCatalog _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyExpressionFunctionCatalog"/> class.
    /// </summary>
    /// <param name="providers">The providers of survey functions.</param>
    /// <param name="serviceProvider">The services, to find the tenant's catalog when Data Pipelines is on.</param>
    public SurveyExpressionFunctionCatalog(
        IEnumerable<ISurveyExpressionFunctionProvider> providers,
        IServiceProvider serviceProvider)
    {
        _inner = serviceProvider.GetService<IExpressionFunctionCatalog>() ?? ExpressionFunctionCatalog.BuiltIn;

        foreach (var provider in providers)
        {
            foreach (var function in provider.GetFunctions())
            {
                if (!string.IsNullOrEmpty(function?.Name))
                {
                    _functions[function.Name] = function;
                }
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ExpressionFunction> Functions
        => _functions.Values.Concat(_inner.Functions.Where(function => !_functions.ContainsKey(function.Name))).ToList();

    /// <inheritdoc />
    public ExpressionFunction Find(string name)
        => !string.IsNullOrEmpty(name) && _functions.TryGetValue(name, out var function) ? function : _inner.Find(name);
}
