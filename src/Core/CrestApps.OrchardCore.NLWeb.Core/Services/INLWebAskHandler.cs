namespace CrestApps.OrchardCore.NLWeb.Services;

/// <summary>
/// Is told about every question the site answered, for example to record analytics.
/// </summary>
public interface INLWebAskHandler
{
    /// <summary>
    /// Called after a question is answered, failed or needed more information.
    /// </summary>
    /// <param name="context">The ask context.</param>
    /// <returns>A task that completes when the handler is done.</returns>
    Task AskedAsync(NLWebAskContext context);
}
