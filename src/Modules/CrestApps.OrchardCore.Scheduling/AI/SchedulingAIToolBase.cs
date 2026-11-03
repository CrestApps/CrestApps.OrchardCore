using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Scheduling.AI;

/// <summary>
/// A scheduling AI tool. It can book appointment types customers can book themselves, and any type for someone who may manage
/// appointments. Moving or cancelling an appointment needs its manage code, the customer's email or phone on it, or that permission.
/// </summary>
public abstract class SchedulingAIToolBase : AIFunction
{
    /// <summary>
    /// The options the tools write their results with.
    /// </summary>
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object> { ["Strict"] = false };

    /// <summary>
    /// Reads a string argument.
    /// </summary>
    /// <param name="arguments">The arguments.</param>
    /// <param name="name">The argument's name.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    protected static string Read(AIFunctionArguments arguments, string name)
    {
        if (arguments is null || !arguments.TryGetValue(name, out var value) || value is null)
        {
            return null;
        }

        var text = value is JsonElement element ? (element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString()) : value.ToString();

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Gets whether the person using the AI may manage appointments.
    /// </summary>
    /// <param name="services">The request's services.</param>
    /// <returns><see langword="true"/> when they may.</returns>
    protected static async Task<bool> CanManageAsync(IServiceProvider services)
    {
        var user = services.GetService<IHttpContextAccessor>()?.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true && await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, SchedulingPermissions.ManageAppointments);
    }

    /// <summary>
    /// Finds an appointment type the AI may use.
    /// </summary>
    /// <param name="services">The request's services.</param>
    /// <param name="id">The type's id.</param>
    /// <returns>The type, or <see langword="null"/>.</returns>
    protected static async Task<AppointmentType> FindTypeAsync(IServiceProvider services, string id)
    {
        var type = string.IsNullOrEmpty(id) ? null : await services.GetRequiredService<AppointmentTypeStore>().FindByIdAsync(id);

        return type is { Enabled: true } && (type.AllowSelfService || await CanManageAsync(services)) ? type : null;
    }

    /// <summary>
    /// Formats a time in the site's time zone, for the AI to read back.
    /// </summary>
    /// <param name="services">The request's services.</param>
    /// <param name="utc">The time.</param>
    /// <returns>The time, ISO 8601 with its offset.</returns>
    protected static async Task<string> LocalAsync(IServiceProvider services, DateTime utc)
    {
        var zone = CalendarTime.NormalizeZoneId((await services.GetRequiredService<ISiteService>().GetSiteSettingsAsync()).TimeZoneId) ?? "UTC";
        var local = CalendarTime.ToLocal(utc, zone);
        var offset = local - DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);

        return new DateTimeOffset(local, offset).ToString("yyyy-MM-dd'T'HH:mmzzz", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Parses a JSON schema.
    /// </summary>
    /// <param name="schema">The schema text.</param>
    /// <returns>The schema.</returns>
    protected static JsonElement Schema(string schema)
    {
        return JsonSerializer.Deserialize<JsonElement>(schema);
    }
}
