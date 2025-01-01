using System.Security.Claims;
using CrestApps.OrchardCore.OpenAI.Core.Indexes;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.OpenAI.Core.Services;

public sealed class DefaultOpenAIChatSessionManager : IOpenAIChatSessionManager
{
    private readonly IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IClientIPAddressAccessor _clientIPAddressAccessor;
    private readonly YesSql.ISession _session;

    public DefaultOpenAIChatSessionManager(
        IClock clock,
        IHttpContextAccessor httpContextAccessor,
        IClientIPAddressAccessor clientIPAddressAccessor,
        YesSql.ISession session)
    {
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        _clientIPAddressAccessor = clientIPAddressAccessor;
        _session = session;
    }

    public async Task<OpenAIChatSession> NewAsync(OpenAIChatProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var chatSession = new OpenAIChatSession
        {
            SessionId = IdGenerator.GenerateId(),
            WelcomeMessage = profile.WelcomeMessage,
            ProfileId = profile.Id,
            CreatedUtc = _clock.UtcNow,
        };

        var user = _httpContextAccessor.HttpContext?.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            chatSession.UserId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        }
        else
        {
            var clientId = await _clientIPAddressAccessor.GetClientIdAsync(_httpContextAccessor.HttpContext);

            if (string.IsNullOrEmpty(clientId))
            {
                throw new InvalidOperationException("Unable to find the clientId. Possible Robot.");
            }

            chatSession.ClientId = clientId;
        }

        return chatSession;
    }

    public async Task<OpenAIChatSession> FindAsync(string sessionId, string profileId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentException.ThrowIfNullOrEmpty(profileId);

        var user = _httpContextAccessor.HttpContext?.User;

        if (user.Identity?.IsAuthenticated == true)
        {
            var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

            return await _session.Query<OpenAIChatSession, OpenAIChatSessionIndex>(i => i.SessionId == sessionId && i.UserId == userId && i.ProfileId == profileId, collection: OpenAIConstants.CollectionName)
                .FirstOrDefaultAsync();
        }
        else
        {
            var clientId = await _clientIPAddressAccessor.GetClientIdAsync(_httpContextAccessor.HttpContext);

            if (string.IsNullOrEmpty(clientId))
            {
                throw new InvalidOperationException("Unable to find the clientId. Possible Robot.");
            }

            // It's important to make sure that the userId is null when querying using clientId.
            return await _session.Query<OpenAIChatSession, OpenAIChatSessionIndex>(i => i.SessionId == sessionId && i.UserId == null && i.ClientId == clientId && i.ProfileId == profileId, collection: OpenAIConstants.CollectionName)
                .FirstOrDefaultAsync();
        }
    }

    public Task SaveAsync(OpenAIChatSession chatSession)
    {
        ArgumentNullException.ThrowIfNull(chatSession);

        return _session.SaveAsync(chatSession, collection: OpenAIConstants.CollectionName);
    }
}
