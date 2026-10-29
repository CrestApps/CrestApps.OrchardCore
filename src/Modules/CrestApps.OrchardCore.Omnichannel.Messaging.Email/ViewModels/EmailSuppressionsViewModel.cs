using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.ViewModels;

public class EmailSuppressionsViewModel
{
    public string Search { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }

    public int Total { get; set; }

    public IReadOnlyList<EmailSuppression> Items { get; set; } = [];

    public int PageCount => PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling((double)Total / PageSize));
}
