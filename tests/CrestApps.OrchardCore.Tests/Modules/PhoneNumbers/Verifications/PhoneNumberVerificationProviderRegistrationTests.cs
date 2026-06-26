using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumbers.Verifications;

public sealed class PhoneNumberVerificationProviderRegistrationTests
{
    [Fact]
    public void AddPhoneNumberVerificationProvider_RegistersDescriptorAndKeyedService()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPhoneNumberVerificationProvider<FakeProvider>("Fake", options =>
        {
            options.DisplayName = new LocalizedString("Fake Provider", "Fake Provider");
            options.Description = new LocalizedString("A fake provider used for testing.", "A fake provider used for testing.");
        });

        // Assert
        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<PhoneNumberVerificationProviderOptions>>().Value;

        Assert.True(options.Providers.ContainsKey("Fake"));
        Assert.Equal("Fake Provider", options.Providers["Fake"].DisplayName.Value);
        Assert.Equal("A fake provider used for testing.", options.Providers["Fake"].Description.Value);

        var provider = serviceProvider.GetKeyedService<IPhoneNumberVerificationProvider>("Fake");

        Assert.IsType<FakeProvider>(provider);
    }

    [Fact]
    public void AddPhoneNumberVerificationProvider_IsCaseInsensitiveForKeyLookup()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddPhoneNumberVerificationProvider<FakeProvider>("Fake");

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
