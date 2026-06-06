using System.Reflection;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public sealed class ContactIndexProviderCollectionTests
{
    [Fact]
    public void OmnichannelContactIndexProvider_ShouldUseDefaultCollection()
    {
        // Arrange
        var provider = CreateProvider(
            typeof(CrestApps.OrchardCore.Omnichannel.Managements.Startup).Assembly,
            "CrestApps.OrchardCore.Omnichannel.Managements.Indexes.OmnichannelContactIndexProvider",
            [new CrestApps.OrchardCore.PhoneNumbers.DefaultPhoneNumberService()]);

        // Act
        var collectionName = GetCollectionName(provider);

        // Assert
        Assert.True(string.IsNullOrEmpty(collectionName));
    }

    [Fact]
    public void OmnichannelContactCommunicationPreferenceIndexProvider_ShouldUseDefaultCollection()
    {
        // Arrange
        var provider = CreateProvider(
            typeof(CrestApps.OrchardCore.Omnichannel.Startup).Assembly,
            "CrestApps.OrchardCore.Omnichannel.Indexes.OmnichannelContactCommunicationPreferenceIndexProvider",
            []);

        // Act
        var collectionName = GetCollectionName(provider);

        // Assert
        Assert.True(string.IsNullOrEmpty(collectionName));
    }

    private static object CreateProvider(
        Assembly assembly,
        string typeName,
        object[] arguments)
    {
        var providerType = assembly.GetType(typeName, throwOnError: true);

        return Activator.CreateInstance(providerType!, arguments)!;
    }

    private static string GetCollectionName(object provider)
    {
        var property = provider.GetType().GetProperty(
            "CollectionName",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Assert.NotNull(property);

        return property?.GetValue(provider) as string;
    }
}
