using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Commerce.Api;

/// <summary>
/// The problem responses commerce endpoints return (RFC 9457). A conflict carries a <c>code</c> a client can act on.
/// </summary>
public static class ApiProblems
{
    /// <summary>
    /// The code of a conflict: what went wrong, for a client to act on.
    /// </summary>
    public static class Codes
    {
        public const string EmptyCart = "empty-cart";

        public const string OutOfStock = "out-of-stock";

        public const string InvoiceChanged = "invoice-changed";

        public const string Concurrency = "concurrency";

        public const string NotAvailable = "not-available";

        public const string Unsupported = "unsupported";

        public const string IdempotencyInProgress = "idempotency-in-progress";
    }

    /// <summary>
    /// Something that doesn't exist, or that the caller may not see: both answer the same, so a stranger can't tell
    /// which.
    /// </summary>
    public static ProblemHttpResult NotFound(string detail = null)
        => TypedResults.Problem(detail, statusCode: StatusCodes.Status404NotFound, title: "Not found");

    public static ProblemHttpResult Forbidden(string detail = null)
        => TypedResults.Problem(detail, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");

    public static ProblemHttpResult Conflict(string code, string detail)
        => TypedResults.Problem(detail, statusCode: StatusCodes.Status409Conflict, title: "Conflict", extensions: new Dictionary<string, object> { ["code"] = code });

    public static ProblemHttpResult TooManyRequests(string detail = null)
        => TypedResults.Problem(detail, statusCode: StatusCodes.Status429TooManyRequests, title: "Too many requests");

    public static ValidationProblem Validation(IDictionary<string, string[]> errors, string detail = null)
        => TypedResults.ValidationProblem(errors, detail);

    public static ValidationProblem Validation(string field, string error)
        => Validation(new Dictionary<string, string[]> { [field] = [error] });

    /// <summary>
    /// The problems a form-shaped service recorded, keyed by the JSON names of the request's fields: <c>Email</c>
    /// becomes <c>email</c>, and <c>BillingAddress.City</c> becomes <c>billingAddress.city</c>.
    /// </summary>
    public static ValidationProblem Validation(ModelStateDictionary modelState, string detail = null)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var (key, entry) in modelState)
        {
            if (entry.Errors.Count == 0)
            {
                continue;
            }

            var name = string.Join('.', key.Split('.').Select(part => part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
            var messages = entry.Errors.Select(error => error.ErrorMessage).Where(message => !string.IsNullOrEmpty(message)).ToArray();

            // An error without a message only kept a page on its step; there's nothing in it for a client.
            if (messages.Length == 0)
            {
                continue;
            }

            errors[name] = errors.TryGetValue(name, out var existing) ? [.. existing, .. messages] : [.. messages];
        }

        return Validation(errors, detail);
    }
}
