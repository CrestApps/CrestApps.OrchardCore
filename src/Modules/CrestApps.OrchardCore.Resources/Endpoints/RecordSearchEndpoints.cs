using CrestApps.OrchardCore.Records;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Resources.Endpoints;

/// <summary>
/// The search behind the record picker: records of one kind as <c>{ value, text, secondaryText }</c> items, the shape
/// the shared item selector reads. The record source of the kind decides whether the user may search it and which
/// records the user sees.
/// </summary>
internal static class RecordSearchEndpoints
{
    public const string RouteName = "CrestApps.Records.Search";

    public const string Pattern = "Admin/api/crestapps/records/search";

    private const int MaxResults = 30;

    public static IEndpointRouteBuilder AddRecordSearchEndpoint(this IEndpointRouteBuilder builder)
    {
        builder.MapGet(Pattern, HandleAsync)
            .RequireAuthorization()
            .WithName(RouteName);

        return builder;
    }

    internal static async Task<IResult> HandleAsync(
        string kind,
        string query,
        string valueType,
        HttpContext httpContext,
        IRecordDirectory directory,
        CancellationToken cancellationToken)
    {
        if (directory.GetSource(kind) is not { } source)
        {
            return Results.NotFound();
        }

        if (!await source.CanSearchAsync(httpContext.User, cancellationToken))
        {
            return Results.Forbid();
        }

        var records = await source.SearchAsync(
            new RecordSearch
            {
                Text = query?.Trim(),
                Count = MaxResults,
                User = httpContext.User,
            },
            cancellationToken);

        var asReference = string.Equals(valueType, RecordPickerValueTypes.Reference, StringComparison.OrdinalIgnoreCase);

        var items = records
            .Where(record => !string.IsNullOrEmpty(record.Id))
            .Take(MaxResults)
            .Select(record => new RecordSearchItem
            {
                Value = asReference ? new RecordRef(source.Kind, record.Id).ToString() : record.Id,
                Text = string.IsNullOrEmpty(record.Title) ? record.Id : record.Title,
                SecondaryText = record.Description,
                IsEnabled = true,
            })
            .ToArray();

        return Results.Ok<IEnumerable<RecordSearchItem>>(items);
    }

    internal sealed class RecordSearchItem
    {
        public string Value { get; set; }

        public string Text { get; set; }

        public string SecondaryText { get; set; }

        public bool IsEnabled { get; set; }
    }
}

/// <summary>
/// The forms of value the record picker posts.
/// </summary>
public static class RecordPickerValueTypes
{
    /// <summary>
    /// The record's id, for a field that stores records of one kind.
    /// </summary>
    public const string Id = "id";

    /// <summary>
    /// The record reference as <c>Kind:Id</c>, for a field that stores records of any kind.
    /// </summary>
    public const string Reference = "reference";
}
