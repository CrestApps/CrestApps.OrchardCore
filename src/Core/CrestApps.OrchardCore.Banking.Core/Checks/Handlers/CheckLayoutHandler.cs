using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core.Handlers;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Banking.Checks.Core.Services;
using CrestApps.OrchardCore.Banking.Checks.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Banking.Checks.Core.Handlers;

/// <summary>
/// Stamps check layouts, gives a new one the default field places, reads them from recipes and checks them. A recipe
/// never carries the signature images or the check line font.
/// </summary>
public sealed class CheckLayoutHandler : CatalogEntryHandlerBase<CheckLayout>
{
    // The stock and the form are read by name, as the recipe schema lists them, or by number.
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckLayoutHandler"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CheckLayoutHandler(IClock clock, IStringLocalizer<CheckLayoutHandler> stringLocalizer)
    {
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override Task InitializingAsync(InitializingContext<CheckLayout> context, CancellationToken cancellationToken = default)
    {
        context.Model.CreatedUtc = _clock.UtcNow;

        if (context.Model.Fields.Count == 0)
        {
            context.Model.Fields = CheckLayoutDefaults.CreateFields();
        }

        Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task UpdatingAsync(UpdatingContext<CheckLayout> context, CancellationToken cancellationToken = default)
    {
        context.Model.ModifiedUtc = _clock.UtcNow;
        Populate(context.Model, context.Data);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task ValidatingAsync(ValidatingContext<CheckLayout> context, CancellationToken cancellationToken = default)
    {
        var layout = context.Model;

        if (string.IsNullOrWhiteSpace(layout.Name))
        {
            context.Result.Fail(new ValidationResult(S["Name is required."], [nameof(CheckLayout.Name)]));
        }

        if (Math.Abs(layout.OffsetX) > 72 || Math.Abs(layout.OffsetY) > 72)
        {
            context.Result.Fail(new ValidationResult(S["Move the print by at most 72 points (one inch) in each direction."], [nameof(CheckLayout.OffsetX)]));
        }

        if (layout.MicrFontSize is < 6 or > 24)
        {
            context.Result.Fail(new ValidationResult(S["The check line font size is 6 to 24 points."], [nameof(CheckLayout.MicrFontSize)]));
        }

        if (string.IsNullOrEmpty(layout.MicrSymbolCharacters) || layout.MicrSymbolCharacters.Length != 4)
        {
            context.Result.Fail(new ValidationResult(S["Enter the four characters the font draws the transit, amount, on-us and dash symbols on, such as ABCD."], [nameof(CheckLayout.MicrSymbolCharacters)]));
        }

        if (layout.SecondSignatureAbove is < 0)
        {
            context.Result.Fail(new ValidationResult(S["The amount for a second signature can't be negative."], [nameof(CheckLayout.SecondSignatureAbove)]));
        }

        foreach (var (name, position) in layout.Fields)
        {
            if (position is null || position.X < 0 || position.Y < 0 || position.X > CheckLayoutDefaults.CheckWidth || position.Y > CheckLayoutDefaults.CheckHeight || position.FontSize is < 4 or > 36)
            {
                context.Result.Fail(new ValidationResult(S["The place of {0} is off the check.", name], [nameof(CheckLayout.Fields)]));
            }
        }

        return Task.CompletedTask;
    }

    private static void Populate(CheckLayout layout, JsonNode data)
    {
        if (data is not JsonObject json)
        {
            return;
        }

        var values = json.Deserialize<CheckLayout>(_jsonOptions);

        if (values is null)
        {
            return;
        }

        layout.Name = values.Name?.Trim() ?? layout.Name;
        layout.Description = json.ContainsKey(nameof(CheckLayout.Description)) ? values.Description : layout.Description;
        layout.Stock = json.ContainsKey(nameof(CheckLayout.Stock)) ? values.Stock : layout.Stock;
        layout.Form = json.ContainsKey(nameof(CheckLayout.Form)) ? values.Form : layout.Form;
        layout.OffsetX = json.ContainsKey(nameof(CheckLayout.OffsetX)) ? values.OffsetX : layout.OffsetX;
        layout.OffsetY = json.ContainsKey(nameof(CheckLayout.OffsetY)) ? values.OffsetY : layout.OffsetY;
        layout.PrintMicr = json.ContainsKey(nameof(CheckLayout.PrintMicr)) ? values.PrintMicr : layout.PrintMicr;
        layout.MicrCountry = values.MicrCountry ?? layout.MicrCountry;
        layout.UseAuxiliaryOnUs = json.ContainsKey(nameof(CheckLayout.UseAuxiliaryOnUs)) ? values.UseAuxiliaryOnUs : layout.UseAuxiliaryOnUs;
        layout.MicrFontSize = json.ContainsKey(nameof(CheckLayout.MicrFontSize)) ? values.MicrFontSize : layout.MicrFontSize;
        layout.MicrSymbolCharacters = values.MicrSymbolCharacters ?? layout.MicrSymbolCharacters;
        layout.PayerName = json.ContainsKey(nameof(CheckLayout.PayerName)) ? values.PayerName : layout.PayerName;
        layout.PayerAddress = json.ContainsKey(nameof(CheckLayout.PayerAddress)) ? values.PayerAddress : layout.PayerAddress;
        layout.SecondSignatureAbove = json.ContainsKey(nameof(CheckLayout.SecondSignatureAbove)) ? values.SecondSignatureAbove : layout.SecondSignatureAbove;

        if (json[nameof(CheckLayout.Fields)] is JsonObject)
        {
            foreach (var (name, position) in values.Fields ?? [])
            {
                layout.Fields[name] = position;
            }
        }
    }
}
