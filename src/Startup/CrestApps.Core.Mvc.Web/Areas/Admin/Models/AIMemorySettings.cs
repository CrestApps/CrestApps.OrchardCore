namespace CrestApps.Core.Mvc.Web.Areas.Admin.Models;

public sealed class AIMemorySettings
{
    public string IndexProfileName { get; set; }

    public int TopN { get; set; } = 5;
}
