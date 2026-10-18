namespace CrestApps.OrchardCore.PayLater.ViewModels;

/// <summary>
/// The Bank transfer settings editor.
/// </summary>
public class BankTransferSettingsViewModel
{
    public bool Enabled { get; set; }

    public string Instructions { get; set; }

    public int NetTermDays { get; set; }
}

/// <summary>
/// The Cash on delivery settings editor.
/// </summary>
public class CashOnDeliverySettingsViewModel
{
    public bool Enabled { get; set; }

    public string Instructions { get; set; }
}
