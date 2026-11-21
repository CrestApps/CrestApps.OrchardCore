using CrestApps.Core.AI.Ftp.Models;
using CrestApps.OrchardCore.AI.FileTransfer.Core;
using CrestApps.OrchardCore.AI.FileTransfer.Core.Drivers;
using CrestApps.OrchardCore.AI.FileTransfer.Core.ViewModels;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.ModelBinding;

namespace CrestApps.OrchardCore.Tests.Modules.AI.FileTransfer;

/// <summary>
/// SEC-A19: SFTP connections trusted any host key, and an FTP connection could accept any server certificate. The host
/// key check is CrestApps.Core's <c>SftpHostKeys</c>, and the fingerprint field is checked against it in
/// <see cref="SftpHostKeyFingerprintsTests"/>.
/// </summary>
public sealed class FileTransferSecurityTests
{
    [Fact]
    public void FtpEditor_RefusesAcceptingAnyCertificate_UnlessTheHostAllowsIt()
    {
        var refused = Apply(allowAnyCertificate: false);
        var allowed = Apply(allowAnyCertificate: true);

        Assert.False(refused.ModelState.IsValid);
        Assert.True(allowed.ModelState.IsValid);
    }

    private static TestUpdater Apply(bool allowAnyCertificate)
    {
        var updater = new TestUpdater();
        var driver = new TestFtpDriver(Options.Create(new FileTransferHostOptions { AllowAnyFtpCertificate = allowAnyCertificate }));
        var context = new UpdateEditorContext(Mock.Of<global::OrchardCore.DisplayManagement.IShape>(), string.Empty, false, string.Empty, Mock.Of<global::OrchardCore.DisplayManagement.IShapeFactory>(), null, updater);

        driver.ApplyForTest(context, new FtpConnectionFieldsViewModel { Host = "ftp.example.com", Port = 21, ValidateAnyCertificate = true });

        return updater;
    }

    private sealed class TestFtpDriver : FtpConnectionDisplayDriverBase<object>
    {
        public TestFtpDriver(IOptions<FileTransferHostOptions> hostOptions)
            : base(new EphemeralDataProtectionProvider(), new PassThroughLocalizer(), hostOptions)
        {
        }

        public FtpConnectionMetadata ApplyForTest(UpdateEditorContext context, FtpConnectionFieldsViewModel model)
            => ApplyConnectionFields(context, model, existing: null);
    }

    private sealed class TestUpdater : IUpdateModel
    {
        public Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary ModelState { get; } = new();

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model) where TModel : class => Task.FromResult(true);

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix) where TModel : class => Task.FromResult(true);

        public Task<bool> TryUpdateModelAsync<TModel>(TModel model, string prefix, params System.Linq.Expressions.Expression<Func<TModel, object>>[] includeExpressions) where TModel : class => Task.FromResult(true);

        public bool TryValidateModel(object model) => true;

        public bool TryValidateModel(object model, string prefix) => true;
    }

    private sealed class PassThroughLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
