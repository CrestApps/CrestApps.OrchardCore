using CrestApps.OrchardCore.ContentAccessControl.Services;
using CrestApps.OrchardCore.Indexing;

namespace CrestApps.OrchardCore.Tests.ContentAccessControl;

/// <summary>
/// An AI knowledge base copied every document of its source index, so a public assistant could answer from an article
/// restricted to staff roles: Content Access Control now tells it which content items aren't for everyone, and the
/// knowledge base leaves them out.
/// </summary>
public sealed class ContentAccessIndexedDocumentVisibilityTests
{
    [Theory]
    [InlineData(new string[0], true)]
    [InlineData(new[] { "Anonymous", "Authenticated" }, true)]
    [InlineData(new[] { "anonymous" }, true)]
    [InlineData(new[] { "Authenticated" }, false)]
    [InlineData(new[] { "Editor", "Administrator" }, false)]
    public void AnItem_IsForEveryone_OnlyWhenVisitorsMayReadIt(string[] allowedRoles, bool forEveryone)
        => Assert.Equal(forEveryone, ContentAccessIndexedDocumentVisibility.IsForEveryone(allowedRoles));

    [Fact]
    public async Task ADocument_IsForEveryone_UnlessAVisibilityRestrictsIt()
    {
        IIndexedDocumentVisibility[] visibilities = [new Fixed("public", true), new Fixed("staff", false)];

        Assert.True(await visibilities.IsForEveryoneAsync("public", TestContext.Current.CancellationToken));
        Assert.False(await visibilities.IsForEveryoneAsync("staff", TestContext.Current.CancellationToken));
        Assert.True(await Array.Empty<IIndexedDocumentVisibility>().IsForEveryoneAsync("staff", TestContext.Current.CancellationToken));
        Assert.True(await visibilities.IsForEveryoneAsync(null, TestContext.Current.CancellationToken));
    }

    private sealed class Fixed : IIndexedDocumentVisibility
    {
        private readonly string _id;
        private readonly bool _forEveryone;

        public Fixed(string id, bool forEveryone)
        {
            _id = id;
            _forEveryone = forEveryone;
        }

        public Task<bool> IsForEveryoneAsync(string documentId, CancellationToken cancellationToken = default)
            => Task.FromResult(documentId != _id || _forEveryone);
    }
}
