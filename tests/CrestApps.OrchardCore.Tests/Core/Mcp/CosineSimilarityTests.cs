using CrestApps.OrchardCore.AI.Mcp.Services;

namespace CrestApps.OrchardCore.Tests.Core.Mcp;

public sealed class CosineSimilarityTests
{
    [Fact]
    public void DotProduct_IdenticalNormalizedVectors_ReturnsOne()
    {
        var vector = DefaultMcpCapabilityResolver.NormalizeL2([1f, 2f, 3f]);

        var result = DefaultMcpCapabilityResolver.DotProduct(vector, vector);

        Assert.Equal(1f, result, precision: 5);
    }

    [Fact]
    public void DotProduct_OrthogonalVectors_ReturnsZero()
    {
        var vectorA = DefaultMcpCapabilityResolver.NormalizeL2([1f, 0f]);
        var vectorB = DefaultMcpCapabilityResolver.NormalizeL2([0f, 1f]);

        var result = DefaultMcpCapabilityResolver.DotProduct(vectorA, vectorB);

        Assert.Equal(0f, result, precision: 5);
    }

    [Fact]
    public void DotProduct_OppositeNormalizedVectors_ReturnsNegativeOne()
    {
        var vectorA = DefaultMcpCapabilityResolver.NormalizeL2([1f, 0f, 0f]);
        var vectorB = DefaultMcpCapabilityResolver.NormalizeL2([-1f, 0f, 0f]);

        var result = DefaultMcpCapabilityResolver.DotProduct(vectorA, vectorB);

        Assert.Equal(-1f, result, precision: 5);
    }

    [Fact]
    public void DotProduct_EmptyVectors_ReturnsZero()
    {
        var result = DefaultMcpCapabilityResolver.DotProduct([], []);

        Assert.Equal(0f, result);
    }

    [Fact]
    public void DotProduct_DifferentLengths_ReturnsZero()
    {
        var vectorA = new float[] { 1f, 2f };
        var vectorB = new float[] { 1f, 2f, 3f };

        var result = DefaultMcpCapabilityResolver.DotProduct(vectorA, vectorB);

        Assert.Equal(0f, result);
    }

    [Fact]
    public void DotProduct_ZeroVectors_ReturnsZero()
    {
        var vectorA = new float[] { 0f, 0f, 0f };
        var vectorB = new float[] { 0f, 0f, 0f };

        var result = DefaultMcpCapabilityResolver.DotProduct(vectorA, vectorB);

        Assert.Equal(0f, result);
    }

    [Fact]
    public void DotProduct_SimilarNormalizedVectors_ReturnsHighValue()
    {
        var vectorA = DefaultMcpCapabilityResolver.NormalizeL2([1f, 2f, 3f]);
        var vectorB = DefaultMcpCapabilityResolver.NormalizeL2([1.1f, 2.1f, 3.1f]);

        var result = DefaultMcpCapabilityResolver.DotProduct(vectorA, vectorB);

        Assert.True(result > 0.99f, $"Expected high similarity, got {result}");
    }

    [Fact]
    public void DotProduct_ScaledNormalizedVectors_ReturnsOne()
    {
        // Scaled vectors have the same direction, so after normalization they're identical.
        var vectorA = DefaultMcpCapabilityResolver.NormalizeL2([1f, 2f, 3f]);
        var vectorB = DefaultMcpCapabilityResolver.NormalizeL2([2f, 4f, 6f]);

        var result = DefaultMcpCapabilityResolver.DotProduct(vectorA, vectorB);

        Assert.Equal(1f, result, precision: 5);
    }

    [Fact]
    public void NormalizeL2_ProducesUnitVector()
    {
        var vector = new float[] { 3f, 4f };

        var normalized = DefaultMcpCapabilityResolver.NormalizeL2(vector);

        // Magnitude should be 1.
        var magnitude = MathF.Sqrt(normalized[0] * normalized[0] + normalized[1] * normalized[1]);
        Assert.Equal(1f, magnitude, precision: 5);

        // Direction preserved: 3/5, 4/5.
        Assert.Equal(0.6f, normalized[0], precision: 5);
        Assert.Equal(0.8f, normalized[1], precision: 5);
    }

    [Fact]
    public void NormalizeL2_ZeroVector_ReturnsZeroVector()
    {
        var vector = new float[] { 0f, 0f, 0f };

        var normalized = DefaultMcpCapabilityResolver.NormalizeL2(vector);

        Assert.All(normalized, v => Assert.Equal(0f, v));
    }
}
