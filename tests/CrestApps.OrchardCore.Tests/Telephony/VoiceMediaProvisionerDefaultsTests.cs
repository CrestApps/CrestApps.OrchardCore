using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// A provisioner written before the platform asked providers about their copies keeps compiling and keeps the copy
/// the platform recorded: it answers that it cannot say, which is the answer that uploads nothing again.
/// </summary>
public sealed class VoiceMediaProvisionerDefaultsTests
{
    [Fact]
    public async Task GetAvailabilityAsync_ByDefault_AnswersUnknown()
    {
        // Arrange
        IVoiceMediaProvisioner provisioner = new UploadOnlyProvisioner();

        // Act
        var availability = await provisioner.GetAvailabilityAsync("copy-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(VoiceMediaAvailability.Unknown, availability);
    }

    private sealed class UploadOnlyProvisioner : IVoiceMediaProvisioner
    {
        public string ProviderTechnicalName => "External";

        public Task<string> UploadAsync(Stream audio, string contentType, string namePrefix, CancellationToken cancellationToken = default)
            => Task.FromResult("copy-1");

        public Task DeleteAsync(string mediaReference, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
