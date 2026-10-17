using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Products.ViewModels;

public class ProductAttributeSettingsViewModel
{
    public bool Filterable { get; set; }

    public bool Sortable { get; set; }

    public bool Searchable { get; set; }

    public bool ShowOnCard { get; set; }

    public bool ShowInSpecifications { get; set; }

    public string Label { get; set; }

    public string NumberFormat { get; set; }

    public string Suffix { get; set; }

    /// <summary>
    /// The swatch colors as "Name = color" lines.
    /// </summary>
    public string SwatchColors { get; set; }

    public string TermColorField { get; set; }

    [BindNever]
    public bool CanHaveSwatches { get; set; }

    [BindNever]
    public bool IsTaxonomy { get; set; }

    [BindNever]
    public bool CanSort { get; set; }

    [BindNever]
    public bool IsNumber { get; set; }

    /// <summary>
    /// Gets or sets whether the field is long text, which keyword search matches but nothing filters, sorts or shows.
    /// </summary>
    [BindNever]
    public bool IsRichText { get; set; }
}
