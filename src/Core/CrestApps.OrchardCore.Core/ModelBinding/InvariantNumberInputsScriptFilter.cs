using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Filters;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Core.ModelBinding;

/// <summary>
/// Writes, at the foot of every page, the script that names each number, date and time input of a form in an <c>__Invariant</c> field as
/// the form is sent.
/// </summary>
internal sealed class InvariantNumberInputsScriptFilter : IAsyncResultFilter
{
    internal const string ScriptResourceName = "CrestApps.OrchardCore.Core.invariant-number-inputs.js";

    private static readonly Lazy<HtmlString> _script = new(ReadScript);

    private readonly IResourceManager _resourceManager;

    public InvariantNumberInputsScriptFilter(IResourceManager resourceManager)
    {
        _resourceManager = resourceManager;
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // A partial or JSON response is added to a page that has the script already.
        if (context.IsViewOrPageResult())
        {
            _resourceManager.RegisterFootScript(_script.Value);
        }

        await next();
    }

    internal static HtmlString Script => _script.Value;

    private static HtmlString ReadScript()
    {
        using var stream = typeof(InvariantNumberInputsScriptFilter).Assembly.GetManifestResourceStream(ScriptResourceName)
            ?? throw new InvalidOperationException($"The embedded script '{ScriptResourceName}' is missing.");
        using var reader = new StreamReader(stream);

        return new HtmlString("<script>" + Compact(reader.ReadToEnd()) + "</script>");
    }

    /// <summary>
    /// Leaves out the blank lines and the lines that hold only a comment, since every page carries the script inline.
    /// </summary>
    internal static string Compact(string source)
        => string.Join('\n', source
            .ReplaceLineEndings("\n")
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal)));
}
