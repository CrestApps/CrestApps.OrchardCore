using CrestApps.OrchardCore.Downloads.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Downloads.ViewModels;

// Not sealed: Orchard Core derives a proxy from a view model it initializes, and a sealed one renders nothing.
public class DigitalFilesPartViewModel
{
    public List<DigitalFileViewModel> Files { get; set; } = [];

    public int? DownloadLimit { get; set; }

    public int? LinkLifetimeDays { get; set; }

    [BindNever]
    public string UploadUrl { get; set; }

    [BindNever]
    public string FileUrlFormat { get; set; }

    [BindNever]
    public DownloadSettings Defaults { get; set; }
}

public class DigitalFileViewModel
{
    public string FileId { get; set; }

    public string FileName { get; set; }

    public long Size { get; set; }

    public bool Remove { get; set; }
}

public class OrderDownloadsViewModel
{
    public Order Order { get; set; }

    public List<OrderDownloadViewModel> Downloads { get; set; } = [];

    public bool CanManage { get; set; }
}

public class OrderDownloadViewModel
{
    public DownloadEntitlement Entitlement { get; set; }

    public DownloadProblem Problem { get; set; }

    public string Url { get; set; }
}

public class AccountDownloadsViewModel
{
    public List<AccountDownloadViewModel> Downloads { get; set; } = [];
}

public class AccountDownloadViewModel
{
    public Order Order { get; set; }

    public DownloadEntitlement Entitlement { get; set; }

    public DownloadProblem Problem { get; set; }

    public string Url { get; set; }
}

public class DownloadSettingsViewModel
{
    public int DownloadLimit { get; set; }

    public int LinkLifetimeDays { get; set; }
}

public class DownloadPageViewModel
{
    public string Token { get; set; }

    public DownloadEntitlement Entitlement { get; set; }

    public DownloadProblem Problem { get; set; }
}
