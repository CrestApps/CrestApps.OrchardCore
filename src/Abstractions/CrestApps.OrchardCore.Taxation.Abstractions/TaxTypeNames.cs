namespace CrestApps.OrchardCore.Taxation;

/// <summary>
/// Provides well-known, extensible tax type identifiers.
/// </summary>
/// <remarks>
/// Tax types are represented as strings so that additional, region-specific types can be introduced
/// without changing the framework. The engine never embeds country-specific behavior in a tax type.
/// </remarks>
public static class TaxTypeNames
{
    /// <summary>
    /// A generic sales tax.
    /// </summary>
    public const string SalesTax = "SalesTax";

    /// <summary>
    /// A value-added tax.
    /// </summary>
    public const string Vat = "VAT";

    /// <summary>
    /// A goods and services tax.
    /// </summary>
    public const string Gst = "GST";

    /// <summary>
    /// A harmonized sales tax.
    /// </summary>
    public const string Hst = "HST";

    /// <summary>
    /// A provincial sales tax.
    /// </summary>
    public const string Pst = "PST";

    /// <summary>
    /// A Quebec sales tax.
    /// </summary>
    public const string Qst = "QST";

    /// <summary>
    /// An excise tax.
    /// </summary>
    public const string ExciseTax = "ExciseTax";

    /// <summary>
    /// A tax levied on alcohol.
    /// </summary>
    public const string AlcoholTax = "AlcoholTax";

    /// <summary>
    /// A tax levied on tobacco.
    /// </summary>
    public const string TobaccoTax = "TobaccoTax";

    /// <summary>
    /// A tourism tax.
    /// </summary>
    public const string TourismTax = "TourismTax";

    /// <summary>
    /// A lodging tax.
    /// </summary>
    public const string LodgingTax = "LodgingTax";

    /// <summary>
    /// An environmental tax.
    /// </summary>
    public const string EnvironmentalTax = "EnvironmentalTax";

    /// <summary>
    /// A digital services tax.
    /// </summary>
    public const string DigitalServicesTax = "DigitalServicesTax";

    /// <summary>
    /// Any other tax type not covered by the well-known values.
    /// </summary>
    public const string Other = "Other";
}
