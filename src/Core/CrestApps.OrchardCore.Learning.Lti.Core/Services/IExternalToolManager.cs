using CrestApps.Core.Services;
using CrestApps.OrchardCore.Learning.Lti.Models;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Services;

/// <summary>
/// Manages external tools, so their handlers run.
/// </summary>
public interface IExternalToolManager : ICatalogManager<ExternalTool>
{
}
