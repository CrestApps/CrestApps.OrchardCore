namespace CrestApps.OrchardCore.Resources.ViewComponents;

internal sealed class DateRangePickerViewModel
{
    public string PickerId { get; set; }

    public string GroupName { get; set; }

    public string Label { get; set; }

    public string LabelCssClass { get; set; }

    public string Placeholder { get; set; }

    public string WrapperCssClass { get; set; }

    public string ToggleCssClass { get; set; }

    public string FromName { get; set; }

    public string FromId { get; set; }

    public string ToName { get; set; }

    public string ToId { get; set; }

    public string FromValue { get; set; }

    public string ToValue { get; set; }

    public string DatePattern { get; set; }

    public string TimePattern { get; set; }

    public int WeekStart { get; set; }

    public bool HasInitialValue { get; set; }
}
