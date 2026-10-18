using System.Text.Json;
using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Addresses.Core.Services;

/// <summary>
/// The country formats shipped with the package, read once from the embedded data.
/// </summary>
internal sealed class AddressFormatCatalog
{
    internal const string ResourceName = "CrestApps.OrchardCore.Addresses.Core.address-formats.json";

    // Countries the data does not know get every common part, so nothing an address needs is impossible to enter.
    private const string GenericLayout = "%N%n%O%n%A%n%C %S %Z";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly Dictionary<string, AddressFormat> _formats;

    private AddressFormatCatalog(AddressFormatSource source, AddressFormat generic, Dictionary<string, AddressFormat> formats)
    {
        Source = source;
        Generic = generic;
        _formats = formats;
    }

    /// <summary>
    /// Gets where the data was taken from.
    /// </summary>
    public AddressFormatSource Source { get; }

    /// <summary>
    /// Gets the format for a country the data does not know.
    /// </summary>
    public AddressFormat Generic { get; }

    public IReadOnlyCollection<string> CountryCodes
        => _formats.Keys;

    public AddressFormat Get(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return Generic;
        }

        return _formats.TryGetValue(countryCode.Trim(), out var format) ? format : Generic;
    }

    public static AddressFormatCatalog Load()
    {
        using var stream = typeof(AddressFormatCatalog).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The address formats resource '{ResourceName}' is missing.");

        var document = JsonSerializer.Deserialize<AddressFormatDocument>(stream, _serializerOptions)
            ?? throw new InvalidOperationException("The address formats resource is empty.");

        var defaults = document.Default ?? new AddressFormatRecord();
        var formats = new Dictionary<string, AddressFormat>(StringComparer.OrdinalIgnoreCase);

        foreach (var (code, record) in document.Countries ?? [])
        {
            formats[code] = Build(code, record, defaults);
        }

        var generic = Build(null, new AddressFormatRecord { Format = GenericLayout }, defaults);

        return new AddressFormatCatalog(document.Source ?? new AddressFormatSource(), generic, formats);
    }

    private static AddressFormat Build(string countryCode, AddressFormatRecord record, AddressFormatRecord defaults)
    {
        // A country leaves out what it shares with the default ("ZZ") record.
        var latinFormat = record.LatinFormat;

        return new AddressFormat
        {
            CountryCode = countryCode,
            Layout = AddressFormatParser.ParseLayout(record.Format ?? defaults.Format),
            LatinLayout = string.IsNullOrEmpty(latinFormat) ? null : AddressFormatParser.ParseLayout(latinFormat),
            Required = AddressFormatParser.ParseComponents(record.Required ?? defaults.Required),
            Uppercase = AddressFormatParser.ParseComponents(record.Upper ?? defaults.Upper),
            PostalCodePattern = record.PostalCodePattern,
            PostalCodeExamples = string.IsNullOrEmpty(record.PostalCodeExamples)
                ? []
                : record.PostalCodeExamples.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            RegionLabel = record.RegionLabel ?? defaults.RegionLabel ?? AddressLabels.Province,
            CityLabel = record.CityLabel ?? defaults.CityLabel ?? AddressLabels.City,
            DependentLocalityLabel = record.DependentLocalityLabel ?? defaults.DependentLocalityLabel ?? AddressLabels.Suburb,
            PostalCodeLabel = record.PostalCodeLabel ?? defaults.PostalCodeLabel ?? AddressLabels.Postal,
            Regions = (record.Regions ?? [])
                .Select(region => new AddressRegion
                {
                    Code = region.IsoCode ?? region.Key,
                    Key = region.Key,
                    Name = region.Name ?? region.Key,
                    LatinName = region.LatinName,
                    PostalCodePrefix = region.PostalCodePrefix,
                })
                .ToArray(),
        };
    }

    private sealed class AddressFormatDocument
    {
        public AddressFormatSource Source { get; set; }

        public AddressFormatRecord Default { get; set; }

        public Dictionary<string, AddressFormatRecord> Countries { get; set; }
    }

    private sealed class AddressFormatRecord
    {
        public string Format { get; set; }

        public string LatinFormat { get; set; }

        public string Required { get; set; }

        public string Upper { get; set; }

        public string PostalCodePattern { get; set; }

        public string PostalCodeExamples { get; set; }

        public string RegionLabel { get; set; }

        public string CityLabel { get; set; }

        public string DependentLocalityLabel { get; set; }

        public string PostalCodeLabel { get; set; }

        public List<AddressRegionRecord> Regions { get; set; }
    }

    private sealed class AddressRegionRecord
    {
        public string Key { get; set; }

        public string Name { get; set; }

        public string LatinName { get; set; }

        public string IsoCode { get; set; }

        public string PostalCodePrefix { get; set; }
    }
}

/// <summary>
/// Where the shipped address data came from.
/// </summary>
internal sealed class AddressFormatSource
{
    public string Service { get; set; }

    public string Project { get; set; }

    public string License { get; set; }

    /// <summary>
    /// Gets or sets the day the snapshot was taken, as <c>yyyy-MM-dd</c>.
    /// </summary>
    public string Retrieved { get; set; }
}
