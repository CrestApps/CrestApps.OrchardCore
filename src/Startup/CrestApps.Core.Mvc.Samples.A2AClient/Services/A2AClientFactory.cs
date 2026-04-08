using System.Net.Http.Headers;
using System.Text.Json;
using A2A;

namespace CrestApps.Core.Mvc.Samples.A2AClient.Services;

public sealed class A2AClientFactory
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;

    public A2AClientFactory(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
    }

    public A2A.A2AClient Create(string agentUrl = null)
    {
        var url = agentUrl ?? GetEndpoint() + "/a2a";
        var httpClient = _httpClientFactory.CreateClient();
        ApplyAuthentication(httpClient);

        return new A2A.A2AClient(new Uri(url), httpClient);
    }

    public async Task<List<AgentCard>> GetAgentCardsAsync(CancellationToken cancellationToken)
    {
        var endpoint = GetEndpoint();
        var httpClient = _httpClientFactory.CreateClient();
        ApplyAuthentication(httpClient);

        var cardUrl = $"{endpoint.TrimEnd('/')}/.well-known/agent-card.json";
        var response = await httpClient.GetAsync(cardUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        // Try to parse as array first (multi-agent mode), then as single card (skill mode).
        try
        {
            var cards = JsonSerializer.Deserialize<List<AgentCard>>(json, _jsonOptions);

            if (cards is not null)
            {
                return cards;
            }
        }
        catch (JsonException)
        {
            // Not an array, try single card.
        }

        var singleCard = JsonSerializer.Deserialize<AgentCard>(json, _jsonOptions);

        return singleCard is not null ? [singleCard] : [];
    }

    private string GetEndpoint()
    {
        var endpoint = _configuration["A2A:Endpoint"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException("A2A:Endpoint is not configured.");
        }

        return endpoint;
    }

    private void ApplyAuthentication(HttpClient httpClient)
    {
        var apiKey = _configuration["A2A:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return;
        }

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }
}
