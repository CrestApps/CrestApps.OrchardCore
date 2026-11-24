using CrestApps.OrchardCore.Learning.Certifications.Core.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Learning.Certifications;

/// <summary>
/// Who holds a certificate a learner earns: their staff record when Staff is on and they have one, so Workforce
/// scheduling and Dispatch (which read staff holders) see it; the user otherwise.
/// </summary>
public sealed class CertificateHolderResolverTests
{
    [Fact]
    public async Task ResolveAsync_IssuesToTheStaffRecord_WhenTheLearnerHasOne()
    {
        // Arrange
        var directory = new Mock<IStaffDirectory>();

        directory.Setup(service => service.FindByUserIdAsync("ana", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaffMember { ItemId = "staff-ana", UserId = "ana", DisplayName = "Ana Lima" });

        var resolver = Create(services => services.AddSingleton(directory.Object));

        // Act
        var (subject, name) = await resolver.ResolveAsync("ana", TestContext.Current.CancellationToken);
        var subjects = await resolver.ListSubjectsAsync("ana", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SkillSubject.Staff("staff-ana"), subject);
        Assert.Equal("Ana Lima", name);
        Assert.Equal([SkillSubject.User("ana"), SkillSubject.Staff("staff-ana")], subjects);
    }

    [Fact]
    public async Task ResolveAsync_IssuesToTheUser_WhenStaffIsOffOrTheLearnerIsNotStaff()
    {
        // Arrange
        var directory = new Mock<IStaffDirectory>();
        var withStaff = Create(services => services.AddSingleton(directory.Object));
        var withoutStaff = Create(_ => { });

        // Act
        var notStaff = await withStaff.ResolveAsync("ben", TestContext.Current.CancellationToken);
        var staffOff = await withoutStaff.ResolveAsync("ben", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(SkillSubject.User("ben"), notStaff.Subject);
        Assert.Equal("user-ben", notStaff.Name);
        Assert.Equal(SkillSubject.User("ben"), staffOff.Subject);
    }

    private static CertificateHolderResolver Create(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();

        configure(services);

        return new CertificateHolderResolver(services.BuildServiceProvider(), new NamedUsers());
    }

    private sealed class NamedUsers : LearnerUserLookup
    {
        public NamedUsers()
            : base(null, null)
        {
        }

        public override Task<IDictionary<string, string>> GetUserNamesAsync(IEnumerable<string> userIds)
            => Task.FromResult<IDictionary<string, string>>(userIds.ToDictionary(id => id, id => "user-" + id));
    }
}
