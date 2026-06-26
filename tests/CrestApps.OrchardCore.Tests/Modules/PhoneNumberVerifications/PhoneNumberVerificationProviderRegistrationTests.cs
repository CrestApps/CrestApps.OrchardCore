using CrestApps.OrchardCore.PhoneNumberVerifications;
using CrestApps.OrchardCore.PhoneNumberVerifications.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumberVerifications;

public sealed class PhoneNumberVerificationProviderRegistrationTests
{
    [Fact]
    public void AddPhoneNumberVerificationProvider_RegistersDescriptorAndKeyedService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPhoneNumberVerificationProvider<FakeProvider>("Fake", "Fake Provider", "A fake provider used for testing.");

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<PhoneNumberVerificationProviderOptions>>().Value;

        Assert.True(options.Providers.ContainsKey("Fake"));
        Assert.Equal("Fake Provider", options.Providers["Fake"].DisplayName);
        Assert.Equal("A fake provider used for testing.", options.Providers["Fake"].Description);

        var provider = serviceProvider.GetKeyedService<IPhoneNumberVerificationProvider>("Fake");

        Assert.IsType<FakeProvider>(provider);
    }

    [Fact]
    public void AddPhoneNumberVerificationProvider_IsCaseInsensitiveForKeyLookup()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPhoneNumberVerificationProvider<FakeProvider>("Fake", "Fake Provider", "A fake provider.");

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<PhoneNumberVerificationProviderOptions>>().Value;

        Assert.True(options.Providers.ContainsKey("fake"));
    }

    private sealed class FakeProvider : IPhoneNumberVerificationProvider
    {
        public Task<PhoneNumberVerificationResult> VerifyAsync(
            string phoneNumber,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PhoneNumberVerificationResult
            {
                PhoneNumber = phoneNumber,
                Status = PhoneNumberVerificationStatus.Verified,
            });
        }
    }
}
