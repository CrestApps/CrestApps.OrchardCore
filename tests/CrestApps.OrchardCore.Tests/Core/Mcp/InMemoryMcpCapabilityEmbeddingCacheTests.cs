using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Core.Mcp;

public sealed class InMemoryMcpCapabilityEmbeddingCacheTests
{
    private readonly InMemoryMcpCapabilityEmbeddingCacheProvider _cache = new(NullLogger<InMemoryMcpCapabilityEmbeddingCacheProvider>.Instance);

    [Fact]
    public async Task GetOrCreateEmbeddingsAsync_WithEmptyCapabilities_ReturnsEmpty()
    {
        var generator = new FakeEmbeddingGenerator([]);
        var result = await _cache.GetOrCreateEmbeddingsAsync([], generator, TestContext.Current.CancellationToken);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetOrCreateEmbeddingsAsync_GeneratesEmbeddings_ForAllCapabilities()
    {
        var capabilities = new List<McpServerCapabilities>
        {
            CreateCapabilities("conn1", "Server A",
                tools: [new McpServerCapability { Name = "tool1", Description = "A tool" }],
                prompts: [new McpServerCapability { Name = "prompt1", Description = "A prompt" }]),
        };

        var generator = new FakeEmbeddingGenerator(new float[] { 0.1f, 0.2f, 0.3f });
        var result = await _cache.GetOrCreateEmbeddingsAsync(capabilities, generator, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Count);
        Assert.All(result, entry =>
        {
            Assert.Equal("conn1", entry.ConnectionId);
            Assert.Equal("Server A", entry.ConnectionDisplayText);
            Assert.Equal(3, entry.Embedding.Length);
        });
    }

    [Fact]
    public async Task GetOrCreateEmbeddingsAsync_CachesResults_OnSecondCall()
    {
        var capabilities = new List<McpServerCapabilities>
        {
            CreateCapabilities("conn1", "Server A",
                tools: [new McpServerCapability { Name = "tool1", Description = "A tool" }]),
        };

        var callCount = 0;
        var generator = new FakeEmbeddingGenerator(new float[] { 1f, 2f }, () => callCount++);

        var result1 = await _cache.GetOrCreateEmbeddingsAsync(capabilities, generator, TestContext.Current.CancellationToken);
        var result2 = await _cache.GetOrCreateEmbeddingsAsync(capabilities, generator, TestContext.Current.CancellationToken);

        Assert.Single(result1);
        Assert.Single(result2);
        Assert.Equal(1, callCount); // Only called once
    }

    [Fact]
    public async Task Invalidate_ClearsCache_ForConnection()
    {
        var capabilities = new List<McpServerCapabilities>
        {
            CreateCapabilities("conn1", "Server A",
                tools: [new McpServerCapability { Name = "tool1", Description = "A tool" }]),
        };

        var callCount = 0;
        var generator = new FakeEmbeddingGenerator([1f], () => callCount++);

        await _cache.GetOrCreateEmbeddingsAsync(capabilities, generator, TestContext.Current.CancellationToken);
        _cache.Invalidate("conn1");
        await _cache.GetOrCreateEmbeddingsAsync(capabilities, generator, TestContext.Current.CancellationToken);

        Assert.Equal(2, callCount); // Called twice due to invalidation
    }

    [Fact]
    public async Task GetOrCreateEmbeddingsAsync_SkipsCapabilities_WithoutName()
    {
        var capabilities = new List<McpServerCapabilities>
        {
            CreateCapabilities("conn1", "Server A",
                tools:
                [
                    new McpServerCapability { Name = null, Description = "No name" },
                    new McpServerCapability { Name = "  ", Description = "Whitespace name" },
                    new McpServerCapability { Name = "valid_tool", Description = "Valid" },
                ]),
        };

        var generator = new FakeEmbeddingGenerator(new float[] { 1f });
        var result = await _cache.GetOrCreateEmbeddingsAsync(capabilities, generator, TestContext.Current.CancellationToken);

        Assert.Single(result);
        Assert.Equal("valid_tool", result[0].CapabilityName);
    }

    [Fact]
    public async Task GetOrCreateEmbeddingsAsync_SetsCorrectCapabilityTypes()
    {
        var capabilities = new List<McpServerCapabilities>
        {
            CreateCapabilities("conn1", "Server A",
                tools: [new McpServerCapability { Name = "t1" }],
                prompts: [new McpServerCapability { Name = "p1" }],
                resources: [new McpServerCapability { Name = "r1" }]),
        };

        var generator = new FakeEmbeddingGenerator(new float[] { 1f });
        var result = await _cache.GetOrCreateEmbeddingsAsync(capabilities, generator, TestContext.Current.CancellationToken);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, e => e.CapabilityType == McpCapabilityType.Tool && e.CapabilityName == "t1");
        Assert.Contains(result, e => e.CapabilityType == McpCapabilityType.Prompt && e.CapabilityName == "p1");
        Assert.Contains(result, e => e.CapabilityType == McpCapabilityType.Resource && e.CapabilityName == "r1");
    }

    private static McpServerCapabilities CreateCapabilities(
        string connectionId,
        string displayText,
        IReadOnlyList<McpServerCapability> tools = null,
        IReadOnlyList<McpServerCapability> prompts = null,
        IReadOnlyList<McpServerCapability> resources = null)
    {
        return new McpServerCapabilities
        {
            ConnectionId = connectionId,
            ConnectionDisplayText = displayText,
            Tools = tools ?? [],
            Prompts = prompts ?? [],
            Resources = resources ?? [],
            IsHealthy = true,
            FetchedUtc = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// A fake embedding generator that returns a fixed embedding vector for each input.
    /// </summary>
    private sealed class FakeEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly float[] _fixedVector;
        private readonly Action _onGenerate;

        public FakeEmbeddingGenerator(float[] fixedVector, Action onGenerate = null)
        {
            _fixedVector = fixedVector;
            _onGenerate = onGenerate;
        }

        public EmbeddingGeneratorMetadata Metadata { get; } = new("fake");

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions options = null,
            CancellationToken cancellationToken = default)
        {
            _onGenerate?.Invoke();

            var inputs = values.ToList();
            var embeddings = new GeneratedEmbeddings<Embedding<float>>();

            foreach (var _ in inputs)
            {
                embeddings.Add(new Embedding<float>(_fixedVector));
            }

            return Task.FromResult(embeddings);
        }

        public object GetService(Type serviceType, object serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
