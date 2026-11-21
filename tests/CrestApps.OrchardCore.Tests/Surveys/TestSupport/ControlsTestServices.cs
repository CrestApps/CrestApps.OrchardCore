using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Surveys.TestSupport;

/// <summary>
/// Builds the built-in question types the way the module registers them, with test doubles for their dependencies.
/// </summary>
public static class ControlsTestServices
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddOptions();
        services.AddSingleton(typeof(IStringLocalizer<>), typeof(ControlsTestLocalizer<>));
        services.AddScoped<IAttachmentService, ControlsFakeAttachmentService>();
        services.AddSurveysStorageFreeRegistry();
        services.AddSurveysControls();
        services.AddSurveysFileUploadControl();

        return services.BuildServiceProvider();
    }

    public static ISurveyControlRegistry Registry(IServiceProvider services)
        => services.GetRequiredService<ISurveyControlRegistry>();

    private static IServiceCollection AddSurveysStorageFreeRegistry(this IServiceCollection services)
    {
        services.AddScoped<ISurveyControlRegistry, CrestApps.OrchardCore.Surveys.Core.Services.SurveyControlRegistry>();

        return services;
    }

    private sealed class ControlsFakeAttachmentService : IAttachmentService
    {
        public Task<AttachmentResult> AddAsync(IReadOnlyList<AttachmentUpload> uploads, CancellationToken cancellationToken = default)
            => Task.FromResult(new AttachmentResult
            {
                Attachments = uploads.Select((upload, index) => new Attachment { ItemId = $"att{index}", Owner = upload.Owner, FileName = upload.FileName }).ToList(),
            });

        public Task<Attachment> FindAsync(string attachmentId, CancellationToken cancellationToken = default)
            => Task.FromResult<Attachment>(null);

        public Task<IReadOnlyList<Attachment>> GetAsync(IEnumerable<string> attachmentIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Attachment>>([]);

        public Task<IReadOnlyList<Attachment>> ListAsync(RecordRef owner, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Attachment>>([]);

        public Task<byte[]> ReadAsync(Attachment attachment, CancellationToken cancellationToken = default)
            => Task.FromResult(Array.Empty<byte>());

        public Task<bool> DeleteAsync(string attachmentId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public string CreateSignedPath(string attachmentId, TimeSpan lifetime)
            => $"/attachments/{attachmentId}";
    }
}
