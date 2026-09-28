using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// An in-memory stand-in for the Contact Center's soft-phone transfer endpoints: it answers the transfer directory,
/// records every blind transfer and consult command, and lets a test move a consult along as the destination would by
/// answering or hanging up.
/// </summary>
public sealed class TestTransferService
{
    public const string TargetsUrl = "/cc/transfer/targets";
    public const string TransferUrl = "/cc/transfer";
    public const string ConsultUrl = "/cc/transfer/consult";
    public const string ConsultCompleteUrl = "/cc/transfer/consult/complete";
    public const string ConsultCancelUrl = "/cc/transfer/consult/cancel";
    public const string AntiForgeryToken = "test-antiforgery-token";

    private readonly object _lock = new();
    private readonly List<JsonObject> _commands = [];
    private string _consultStatus;

    /// <summary>
    /// Gets the directory the targets endpoint answers.
    /// </summary>
    public static JsonObject Directory => new()
    {
        ["agents"] = new JsonArray(
            new JsonObject { ["id"] = "agent-bea", ["name"] = "Bea Baker", ["extension"] = "201", ["presence"] = "Available", ["available"] = true },
            new JsonObject { ["id"] = "agent-cal", ["name"] = "Cal Cole", ["extension"] = null, ["presence"] = "Busy", ["available"] = false }),
        ["queues"] = new JsonArray(
            new JsonObject { ["id"] = "queue-sales", ["name"] = "Sales", ["waiting"] = 3 }),
        ["externalDestinations"] = new JsonArray(
            new JsonObject { ["id"] = "dest-billing", ["name"] = "Billing partner", ["number"] = "+15559990000" }),
        ["canTransferExternally"] = true,
        ["allowExternalNumbers"] = false,
        ["supportsConsult"] = true,
    };

    /// <summary>
    /// Maps the endpoints, plus the test-only ones that move a consult along.
    /// </summary>
    public void Map(IEndpointRouteBuilder routes)
    {
        routes.MapGet(TargetsUrl, (string interactionId) =>
        {
            Record("targets", new JsonObject { ["interactionId"] = interactionId });

            return Results.Json(Directory);
        });

        routes.MapPost(TransferUrl, async (HttpContext context) =>
        {
            var body = await ReadCommandAsync(context, "transfer");

            return body is null
                ? Results.StatusCode(StatusCodes.Status400BadRequest)
                : Results.Json(new { succeeded = true, message = "The call is ringing for Bea Baker." });
        });

        routes.MapPost(ConsultUrl, async (HttpContext context) =>
        {
            var body = await ReadCommandAsync(context, "consult-start");

            if (body is null)
            {
                return Results.StatusCode(StatusCodes.Status400BadRequest);
            }

            lock (_lock)
            {
                _consultStatus = "ringing";
            }

            return Results.Json(new { succeeded = true, consult = Consult() });
        });

        routes.MapGet(ConsultUrl, (string interactionId, string consultId) =>
        {
            Record("consult-status", new JsonObject { ["interactionId"] = interactionId, ["consultId"] = consultId });

            return Results.Json(new { succeeded = true, consult = Consult() });
        });

        routes.MapPost(ConsultCompleteUrl, async (HttpContext context) =>
        {
            await ReadCommandAsync(context, "consult-complete");

            lock (_lock)
            {
                if (_consultStatus != "connected")
                {
                    return Results.Json(new { succeeded = false, error = "The transfer can be completed once the destination has answered." });
                }

                _consultStatus = "completed";
            }

            return Results.Json(new { succeeded = true, consult = Consult() });
        });

        routes.MapPost(ConsultCancelUrl, async (HttpContext context) =>
        {
            await ReadCommandAsync(context, "consult-cancel");

            lock (_lock)
            {
                _consultStatus = "cancelled";
            }

            return Results.Json(new { succeeded = true, consult = Consult() });
        });

        routes.MapPost("/test/consult/{status}", (string status) =>
        {
            lock (_lock)
            {
                _consultStatus = status;
            }

            return Results.Ok();
        });

        routes.MapGet("/test/transfer-commands", () =>
        {
            lock (_lock)
            {
                return Results.Json(new JsonArray(_commands.Select(command => (JsonNode)command.DeepClone()).ToArray()));
            }
        });
    }

    private object Consult()
    {
        lock (_lock)
        {
            return new
            {
                id = "consult-1",
                status = _consultStatus ?? "cancelled",
                live = _consultStatus is "ringing" or "connected",
                targetType = "agent",
                targetId = "agent-bea",
            };
        }
    }

    private async Task<JsonObject> ReadCommandAsync(HttpContext context, string kind)
    {
        // A command without the phone's antiforgery token is one another site could have made the browser send.
        if (!string.Equals(context.Request.Headers["RequestVerificationToken"], AntiForgeryToken, StringComparison.Ordinal))
        {
            Record(kind + "-refused", []);

            return null;
        }

        var body = await JsonSerializer.DeserializeAsync<JsonObject>(context.Request.Body) ?? [];

        Record(kind, body);

        return body;
    }

    private void Record(string kind, JsonObject body)
    {
        body["kind"] = kind;

        lock (_lock)
        {
            _commands.Add(body);
        }
    }
}
