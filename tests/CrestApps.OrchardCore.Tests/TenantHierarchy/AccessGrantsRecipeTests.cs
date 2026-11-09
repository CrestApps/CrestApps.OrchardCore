using System.Text.Json.Nodes;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Recipes;

namespace CrestApps.OrchardCore.Tests.TenantHierarchy;

/// <summary>
/// An access grant is written by the deployment step in the shape the TenantHierarchyAccessGrants recipe step reads:
/// the user or role by name, the child tenant by entry id and slug, and nothing the parent site owns, such as the
/// user's id or who made the grant.
/// </summary>
public sealed class AccessGrantsRecipeTests
{
    [Fact]
    public void AGrantForOneChild_ReadsBackAsTheSameRequest()
    {
        // Arrange
        var grant = new AccessGrant
        {
            Id = 9,
            GrantId = "grant-1",
            PrincipalType = AccessGrantPrincipalType.User,
            PrincipalId = "parent-user-id",
            PrincipalName = "ana",
            ChildEntryId = "entry-north",
            ChildRoles = ["Editor", "Author"],
            CreatedByName = "admin",
        };
        var entry = new ChildTenantEntry { EntryId = "entry-north", Slug = "north" };

        // Act
        var exported = JsonNode.Parse(AccessGrantsStep.Export(grant, entry).ToJsonString()).AsObject();
        var request = AccessGrantsStep.Read(exported);

        // Assert
        var json = exported.ToJsonString();

        Assert.DoesNotContain("parent-user-id", json);
        Assert.DoesNotContain("admin", json);
        Assert.DoesNotContain("grant-1", json);
        Assert.Equal(AccessGrantPrincipalType.User, request.PrincipalType);
        Assert.Equal("ana", request.Principal);
        Assert.Equal("entry-north", request.ChildEntryId);
        Assert.Equal("north", request.ChildSlug);
        Assert.Equal(["Editor", "Author"], request.ChildRoles);
    }

    [Fact]
    public void AGrantForEveryChild_HasNoChildTenant()
    {
        // Arrange
        var grant = new AccessGrant { PrincipalType = AccessGrantPrincipalType.Role, PrincipalId = "Administrator", PrincipalName = "Administrator", ChildRoles = ["Administrator"] };

        // Act
        var request = AccessGrantsStep.Read(AccessGrantsStep.Export(grant, null));

        // Assert
        Assert.Equal(AccessGrantPrincipalType.Role, request.PrincipalType);
        Assert.Equal("Administrator", request.Principal);
        Assert.Null(request.ChildEntryId);
        Assert.Null(request.ChildSlug);
    }

    [Fact]
    public void AGrantWithoutAKnownPrincipalType_IsNotRead()
    {
        // Arrange
        var node = JsonNode.Parse("""{ "PrincipalType": "Group", "Principal": "Sales", "ChildRoles": [ "Editor" ] }""").AsObject();

        // Act
        var request = AccessGrantsStep.Read(node);

        // Assert
        Assert.Null(request);
    }
}
