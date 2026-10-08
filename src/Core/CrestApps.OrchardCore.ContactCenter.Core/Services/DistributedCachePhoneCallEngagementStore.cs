using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Caching.Distributed;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Keeps the supervisor engagements on agents' own phone calls in the distributed cache, shared across nodes wherever
/// the deployment configured it to be, so the node that receives a supervisor leg's webhook sees the engagement another
/// node started. An engagement lasts as long as its call; the entries lapse on their own well after any call has ended.
/// </summary>
public sealed class DistributedCachePhoneCallEngagementStore : IPhoneCallEngagementStore
{
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);

    private static readonly DistributedCacheEntryOptions _entryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(12),
    };

    private readonly IDistributedCache _cache;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedCachePhoneCallEngagementStore"/> class.
    /// </summary>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="shellSettings">The current tenant, which keys the entries so tenants never see each other's.</param>
    public DistributedCachePhoneCallEngagementStore(IDistributedCache cache, ShellSettings shellSettings)
    {
        _cache = cache;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PhoneCallEngagement>> ListAsync(string callId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(callId))
        {
            return [];
        }

        var value = await _cache.GetAsync(GetKey("call", callId), cancellationToken);

        if (value is null || value.Length == 0)
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<PhoneCallEngagement>>(value, _serializerOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<PhoneCallEngagement> FindAsync(string callId, string supervisorUserId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(supervisorUserId))
        {
            return null;
        }

        var engagements = await ListAsync(callId, cancellationToken);

        return engagements.FirstOrDefault(engagement => string.Equals(engagement.SupervisorUserId, supervisorUserId, StringComparison.Ordinal));
    }

    /// <inheritdoc/>
    public async Task<PhoneCallEngagement> FindBySupervisorLegAsync(string supervisorLegId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(supervisorLegId))
        {
            return null;
        }

        var pointer = await _cache.GetAsync(GetKey("leg", supervisorLegId), cancellationToken);

        if (pointer is null || pointer.Length == 0)
        {
            return null;
        }

        var engagements = await ListAsync(Encoding.UTF8.GetString(pointer), cancellationToken);

        return engagements.FirstOrDefault(engagement => string.Equals(engagement.SupervisorLegId, supervisorLegId, StringComparison.Ordinal));
    }

    /// <inheritdoc/>
    public async Task SaveAsync(PhoneCallEngagement engagement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(engagement);
        ArgumentException.ThrowIfNullOrEmpty(engagement.CallId);
        ArgumentException.ThrowIfNullOrEmpty(engagement.SupervisorUserId);

        var engagements = (await ListAsync(engagement.CallId, cancellationToken))
            .Where(existing => !string.Equals(existing.SupervisorUserId, engagement.SupervisorUserId, StringComparison.Ordinal))
            .Append(engagement)
            .ToList();

        await WriteAsync(engagement.CallId, engagements, cancellationToken);

        if (!string.IsNullOrEmpty(engagement.SupervisorLegId))
        {
            await _cache.SetAsync(GetKey("leg", engagement.SupervisorLegId), Encoding.UTF8.GetBytes(engagement.CallId), _entryOptions, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(PhoneCallEngagement engagement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(engagement);

        if (string.IsNullOrEmpty(engagement.CallId))
        {
            return;
        }

        var engagements = (await ListAsync(engagement.CallId, cancellationToken))
            .Where(existing => !string.Equals(existing.SupervisorUserId, engagement.SupervisorUserId, StringComparison.Ordinal))
            .ToList();

        await WriteAsync(engagement.CallId, engagements, cancellationToken);

        if (!string.IsNullOrEmpty(engagement.SupervisorLegId))
        {
            await _cache.RemoveAsync(GetKey("leg", engagement.SupervisorLegId), cancellationToken);
        }
    }

    private Task WriteAsync(string callId, List<PhoneCallEngagement> engagements, CancellationToken cancellationToken)
        => engagements.Count == 0
            ? _cache.RemoveAsync(GetKey("call", callId), cancellationToken)
            : _cache.SetAsync(GetKey("call", callId), JsonSerializer.SerializeToUtf8Bytes(engagements, _serializerOptions), _entryOptions, cancellationToken);

    private string GetKey(string kind, string value)
        => $"crestapps:cc:phone-call-engagement:{_tenantName}:{kind}:{value}";
}
