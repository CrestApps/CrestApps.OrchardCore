using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Geo.Models;

/// <summary>
/// What to geocode: an address, or free text such as a single-line address.
/// </summary>
public sealed class GeocodeQuery
{
    /// <summary>
    /// Gets or sets the address.
    /// </summary>
    public Address Address { get; set; }

    /// <summary>
    /// Gets or sets free text, used when there's no address.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets a two-letter country code that narrows the search, or <see langword="null"/>.
    /// </summary>
    public string CountryHint { get; set; }

    /// <summary>
    /// Writes the query as one line, for providers that take text and for logs.
    /// </summary>
    /// <returns>The line.</returns>
    public string ToSingleLine()
    {
        if (Address is null)
        {
            return Text?.Trim();
        }

        var parts = new[] { Address.AddressLine1, Address.AddressLine2, Address.City, Address.Region, Address.PostalCode, Address.Country };

        return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part.Trim()));
    }
}

/// <summary>
/// A place a geocoding provider found for a query.
/// </summary>
public sealed class GeocodeCandidate
{
    /// <summary>
    /// Gets or sets the point.
    /// </summary>
    public GeoPoint Point { get; set; }

    /// <summary>
    /// Gets or sets how sure the provider is, from 0 to 1.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the address as the provider writes it.
    /// </summary>
    public string FormattedAddress { get; set; }

    /// <summary>
    /// Gets or sets the postal code, when the provider gives one.
    /// </summary>
    public string PostalCode { get; set; }

    /// <summary>
    /// Gets or sets the two-letter country code, when the provider gives one.
    /// </summary>
    public string Country { get; set; }
}

/// <summary>
/// The answer to a geocoding query.
/// </summary>
public sealed class GeocodeResponse
{
    /// <summary>
    /// Gets or sets the provider that answered, or <see langword="null"/> when none is set up.
    /// </summary>
    public string Provider { get; set; }

    /// <summary>
    /// Gets or sets the candidates, best first.
    /// </summary>
    public IReadOnlyList<GeocodeCandidate> Candidates { get; set; } = [];
}

/// <summary>
/// The state of a background geocoding request.
/// </summary>
public enum GeocodeRequestStatus
{
    /// <summary>
    /// Waiting for its next attempt.
    /// </summary>
    Pending,

    /// <summary>
    /// Geocoded and written back.
    /// </summary>
    Done,

    /// <summary>
    /// Given up: no match, or every attempt failed.
    /// </summary>
    Failed,
}

/// <summary>
/// An address to geocode in the background and write back to the record it belongs to.
/// </summary>
public sealed class GeocodeRequest : CrestApps.Core.Models.CatalogItem
{
    /// <summary>
    /// Gets or sets the kind of record, which names the <c>IGeocodeTarget</c> that writes the result back.
    /// </summary>
    public string TargetType { get; set; }

    /// <summary>
    /// Gets or sets the record.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets what to geocode.
    /// </summary>
    public GeocodeQuery Query { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public GeocodeRequestStatus Status { get; set; }

    /// <summary>
    /// Gets or sets how many attempts failed.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets when to try next.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets why the last attempt failed.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets or sets the point found.
    /// </summary>
    public GeoPoint? Result { get; set; }

    /// <summary>
    /// Gets or sets the provider that found it.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets when it was asked for.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was done or given up.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }
}
