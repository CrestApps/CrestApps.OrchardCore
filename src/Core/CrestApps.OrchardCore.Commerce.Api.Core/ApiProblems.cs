using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

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
}
