using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.NLWeb.Services;

namespace CrestApps.OrchardCore.Tests.NLWeb;

public sealed class NLWebRankFusionTests
{
    [Fact]
    public void Fuse_RanksAnItemBothListsFoundFirst()
    {
        // Arrange
        IReadOnlyList<NLWebCandidate> keyword = [Candidate("a", "index"), Candidate("b", "index"), Candidate("c", "index")];
        IReadOnlyList<NLWebCandidate> vector = [Candidate("c", "vector"), Candidate("d", "vector")];

        // Act
        var fused = NLWebRankFusion.Fuse([keyword, vector], 10);

        // Assert
        Assert.Equal("c", fused[0].Id);
        Assert.Equal("index+vector", fused[0].Source);
        Assert.Equal(4, fused.Count);
    }

    [Fact]
    public void Fuse_WithOneList_KeepsItsOrderAndRemovesDuplicates()
    {
        // Arrange
        IReadOnlyList<NLWebCandidate> list = [Candidate("a", "index"), Candidate("b", "index"), Candidate("a", "index"), Candidate("c", "index")];

        // Act
        var fused = NLWebRankFusion.Fuse([list], 2);

        // Assert
        Assert.Equal(["a", "b"], fused.Select(candidate => candidate.Id));
    }

    private static NLWebCandidate Candidate(string id, string source)
        => new(id, null, source);
}
