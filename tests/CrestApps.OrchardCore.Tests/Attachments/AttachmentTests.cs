using System.Security.Claims;
using System.Text;
using CrestApps.OrchardCore.Attachments;
using CrestApps.OrchardCore.Attachments.Core.Indexes;
using CrestApps.OrchardCore.Attachments.Core.Services;
using CrestApps.OrchardCore.Attachments.Endpoints;
using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Attachments;

/// <summary>
/// Record attachments: a file is matched by its own bytes, every file of an upload is checked before any is stored,
/// scanners can refuse a file, the bytes are kept apart from the record and read back, a record's files are listed
/// newest first, deleting removes both, downloads need the owner's permission or a genuine signed link, and only
/// pictures are shown in the page.
/// </summary>
public sealed class AttachmentTests
{
    private static readonly RecordRef _order = new("Order", "o1");
    private static readonly PartyRef _agent = new(PartyKinds.User, "agent-1");
    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] _pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n");

    [Fact]
    public async Task AnAcceptedFile_IsStoredWithTheTypeItsBytesShow_AndReadBack()
    {
        // Arrange
        var files = new InMemoryAttachmentFileStore();
        await using var harness = await CreateAsync(files);

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().AddAsync(
            [Upload("scan.bin", _pdf, "application/octet-stream")]));

        var (found, bytes) = await harness.RunAsync(async services =>
        {
            var attachments = services.GetRequiredService<IAttachmentService>();
            var attachment = await attachments.FindAsync(result.Attachments[0].ItemId);

            return (attachment, await attachments.ReadAsync(attachment));
        }, commit: false);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("application/pdf", found.ContentType);
        Assert.Equal("scan.bin", found.FileName);
        Assert.False(found.IsImage);
        Assert.Equal(_order, found.Owner);
        Assert.Equal(_agent, found.UploadedBy);
        Assert.Equal(CalendarTestHarness.Now, found.UploadedUtc);
        Assert.Equal(_pdf, bytes);
    }

    [Fact]
    public async Task AFileWhoseBytesMatchNoFormat_IsRefused_EvenWithAPictureName()
    {
        // Arrange
        var files = new InMemoryAttachmentFileStore();
        await using var harness = await CreateAsync(files);
        var svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>");

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().AddAsync(
            [Upload("logo.png", svg, "image/png")]));

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("logo.png", Assert.Single(result.Errors));
        Assert.Empty(files.Files);
    }

    [Fact]
    public async Task OneRefusedFile_KeepsTheWholeUploadOut()
    {
        // Arrange
        var files = new InMemoryAttachmentFileStore();
        await using var harness = await CreateAsync(files, options => options.MaxFileBytes = 64);

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().AddAsync(
            [Upload("ok.pdf", _pdf), Upload("big.pdf", [.. _pdf, .. new byte[100]])]));

        var listed = await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().ListAsync(_order), commit: false);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("big.pdf", Assert.Single(result.Errors));
        Assert.Empty(files.Files);
        Assert.Empty(listed);
    }

    [Fact]
    public async Task TooManyFiles_AreRefused()
    {
        // Arrange
        await using var harness = await CreateAsync(new InMemoryAttachmentFileStore(), options => options.MaxFilesPerUpload = 1);

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().AddAsync(
            [Upload("a.pdf", _pdf), Upload("b.pdf", _pdf)]));

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AFormatTheSiteDoesNotAllow_IsRefused()
    {
        // Arrange
        await using var harness = await CreateAsync(new InMemoryAttachmentFileStore(), options => options.AllowedFormats = ["PNG"]);

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().AddAsync([Upload("a.pdf", _pdf)]));

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("PNG", Assert.Single(result.Errors));
    }

    [Fact]
    public async Task AScannerCanRefuseAFile()
    {
        // Arrange
        var files = new InMemoryAttachmentFileStore();
        await using var harness = await CreateAsync(files, configure: services => services.AddAttachmentScanner<RefusePdfScanner>());

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().AddAsync([Upload("a.pdf", _pdf)]));

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("infected", Assert.Single(result.Errors));
        Assert.Empty(files.Files);
    }

    [Fact]
    public async Task ARecordsFiles_AreListedNewestFirst_AndDeletingRemovesTheBytes()
    {
        // Arrange
        var files = new InMemoryAttachmentFileStore();
        var now = CalendarTestHarness.Now;
        var clock = new Mock<global::OrchardCore.Modules.IClock>();
        var times = new Queue<DateTime>([now.AddMinutes(-10), now]);

        clock.SetupGet(value => value.UtcNow).Returns(() => times.Count > 1 ? times.Dequeue() : times.Peek());

        await using var harness = await CreateAsync(files, configure: services => services.AddSingleton(clock.Object));

        await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().AddAsync([Upload("first.pdf", _pdf)]));
        await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().AddAsync([Upload("second.png", _png)]));
        await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().AddAsync([new AttachmentUpload { Owner = new RecordRef("Order", "o2"), FileName = "other.pdf", Content = _pdf }]));

        // Act
        var listed = await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().ListAsync(_order), commit: false);
        var deleted = await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().DeleteAsync(listed[1].ItemId));
        var afterDelete = await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().ListAsync(_order), commit: false);

        // Assert
        Assert.Equal(["second.png", "first.pdf"], listed.Select(attachment => attachment.FileName));
        Assert.True(listed[0].IsImage);
        Assert.True(deleted);
        Assert.Equal("second.png", Assert.Single(afterDelete).FileName);
        Assert.Equal(2, files.Files.Count);
        Assert.DoesNotContain(listed[1].ItemId, files.Files.Keys);
    }

    [Fact]
    public async Task AccessComesFromTheOwnersKind_AndAKindWithoutAProviderIsRefused()
    {
        // Arrange
        await using var harness = await CreateAsync(new InMemoryAttachmentFileStore(), configure: services => services.AddAttachmentAccessProvider<OrdersForAgents>());
        var agent = CalendarTestHarness.User("agent-1", "role:Agent");
        var customer = CalendarTestHarness.User("customer-1");

        // Act
        var (agentViews, customerViews, agentManages, unknownKind) = await harness.RunAsync(async services =>
        {
            var access = services.GetRequiredService<IAttachmentAccessService>();

            return (
                await access.CanViewAsync(agent, _order),
                await access.CanViewAsync(customer, _order),
                await access.CanManageAsync(agent, _order),
                await access.CanViewAsync(agent, new RecordRef("Invoice", "i1")));
        }, commit: false);

        // Assert
        Assert.True(agentViews);
        Assert.False(customerViews);
        Assert.True(agentManages);
        Assert.False(unknownKind);
    }

    [Fact]
    public async Task ADownload_NeedsTheOwnersPermission_OrAGenuineSignedLink()
    {
        // Arrange
        await using var harness = await CreateAsync(new InMemoryAttachmentFileStore(), configure: services => services.AddAttachmentAccessProvider<OrdersForAgents>());
        var result = await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().AddAsync([Upload("a.pdf", _pdf)]));
        var id = result.Attachments[0].ItemId;

        // Act
        var (agent, guest, stranger, signed, forged, otherFile) = await harness.RunAsync(async services =>
        {
            // The harness's clock stands still in the past, and the data protector checks the expiry on the real
            // clock as well, so the links live long enough for both.
            var attachments = services.GetRequiredService<IAttachmentService>();
            var token = Token(attachments.CreateSignedPath(id, TimeSpan.FromDays(3650)));
            var otherToken = services.GetRequiredService<SignedTokenService>().Protect(DefaultAttachmentService.SignedLinkPurpose, "someone-else", TimeSpan.FromDays(3650));

            return (
                await Download(services, id, null, CalendarTestHarness.User("agent-1", "role:Agent")),
                await Download(services, id, null, new ClaimsPrincipal(new ClaimsIdentity())),
                await Download(services, id, null, CalendarTestHarness.User("customer-1")),
                await Download(services, id, token, new ClaimsPrincipal(new ClaimsIdentity())),
                await Download(services, id, token + "x", new ClaimsPrincipal(new ClaimsIdentity())),
                await Download(services, id, otherToken, new ClaimsPrincipal(new ClaimsIdentity())));
        }, commit: false);

        // Assert
        Assert.Equal(_pdf, Assert.IsType<FileContentHttpResult>(agent.Result).FileContents.ToArray());
        Assert.IsType<ChallengeHttpResult>(guest.Result);
        Assert.IsType<NotFound>(stranger.Result);
        Assert.Equal("a.pdf", Assert.IsType<FileContentHttpResult>(signed.Result).FileDownloadName);
        Assert.IsType<ChallengeHttpResult>(forged.Result);
        Assert.IsType<ChallengeHttpResult>(otherFile.Result);
        Assert.Equal("nosniff", agent.Context.Response.Headers.XContentTypeOptions);
    }

    [Fact]
    public async Task OnlyPictures_AreShownInThePage()
    {
        // Arrange
        await using var harness = await CreateAsync(new InMemoryAttachmentFileStore(), configure: services => services.AddAttachmentAccessProvider<OrdersForAgents>());
        var result = await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().AddAsync([Upload("photo.png", _png), Upload("a.pdf", _pdf)]));
        var agent = CalendarTestHarness.User("agent-1", "role:Agent");

        // Act
        var (picture, pictureAsDownload, document) = await harness.RunAsync(async services => (
            await Download(services, result.Attachments[0].ItemId, null, agent),
            await Download(services, result.Attachments[0].ItemId, null, agent, download: true),
            await Download(services, result.Attachments[1].ItemId, null, agent)), commit: false);

        // Assert
        Assert.Null(Assert.IsType<FileContentHttpResult>(picture.Result).FileDownloadName);
        Assert.StartsWith("inline", picture.Context.Response.Headers.ContentDisposition.ToString());
        Assert.Equal("photo.png", Assert.IsType<FileContentHttpResult>(pictureAsDownload.Result).FileDownloadName);
        Assert.Equal("a.pdf", Assert.IsType<FileContentHttpResult>(document.Result).FileDownloadName);
    }

    [Fact]
    public async Task AGuest_GetsTheSameAnswerForAnUnknownIdAsForAFileTheyMayNotRead()
    {
        // Arrange
        await using var harness = await CreateAsync(new InMemoryAttachmentFileStore(), configure: services => services.AddAttachmentAccessProvider<OrdersForAgents>());
        var result = await harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().AddAsync([Upload("a.pdf", _pdf)]));
        var guest = new ClaimsPrincipal(new ClaimsIdentity());

        // Act
        var (known, unknown, signedUnknown) = await harness.RunAsync(async services => (
            await Download(services, result.Attachments[0].ItemId, null, guest),
            await Download(services, "4unknownattachment00000000", null, guest),
            await Download(services, "4unknownattachment00000000", "anything", guest)), commit: false);

        // Assert
        Assert.IsType<ChallengeHttpResult>(known.Result);
        Assert.IsType<ChallengeHttpResult>(unknown.Result);
        Assert.IsType<ChallengeHttpResult>(signedUnknown.Result);
    }

    [Fact]
    public async Task ASignedLink_CarriesTheTenantsPathBase()
    {
        // Arrange
        await using var harness = await CreateAsync(new InMemoryAttachmentFileStore());

        // Act
        var path = await harness.RunAsync(services =>
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { Request = { PathBase = "/tenant-a" } };

            return Task.FromResult(services.GetRequiredService<IAttachmentService>().CreateSignedPath("4someattachment00000000000", TimeSpan.FromDays(1)));
        }, commit: false);

        // Assert
        Assert.StartsWith("/tenant-a/attachments/4someattachment00000000000?token=", path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AWriteThatFailsPartWay_LeavesNoFileBehind()
    {
        // Arrange: the second file can't be written.
        var files = new InMemoryAttachmentFileStore { FailOnWrite = 2 };
        await using var harness = await CreateAsync(files);

        // Act
        var exception = await Record.ExceptionAsync(() => harness.RunAsync(services => services.GetRequiredService<IAttachmentService>().AddAsync(
            [Upload("a.pdf", _pdf), Upload("b.png", _png)])));

        // Assert
        Assert.IsType<IOException>(exception);
        Assert.Empty(files.Files);
    }

    private static async Task<(IResult Result, HttpContext Context)> Download(IServiceProvider services, string id, string token, ClaimsPrincipal user, bool? download = null)
    {
        var context = new DefaultHttpContext { User = user, RequestServices = services };
        var result = await AttachmentDownloadEndpoint.HandleAsync(
            id,
            token,
            download,
            context,
            services.GetRequiredService<IAttachmentService>(),
            services.GetRequiredService<IAttachmentAccessService>(),
            services.GetRequiredService<SignedTokenService>());

        return (result, context);
    }

    private static string Token(string signedPath)
        => Uri.UnescapeDataString(signedPath[(signedPath.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..]);

    private static AttachmentUpload Upload(string fileName, byte[] content, string declaredContentType = null)
        => new()
        {
            Owner = _order,
            FileName = fileName,
            DeclaredContentType = declaredContentType,
            Content = content,
            UploadedBy = _agent,
        };

    private static Task<CalendarTestHarness> CreateAsync(InMemoryAttachmentFileStore files, Action<AttachmentOptions> options = null, Action<IServiceCollection> configure = null)
        => CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddAttachmentsCore();
                services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
                services.AddSingleton<IAttachmentFileStore>(files);

                if (options is not null)
                {
                    services.Configure(options);
                }

                configure?.Invoke(services);
            },
            AttachmentsIndexSchema.CreateAsync,
            [new AttachmentIndexProvider()],
            [AttachmentsConstants.CollectionName]);

    private sealed class InMemoryAttachmentFileStore : IAttachmentFileStore
    {
        private int _writes;

        public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

        public int FailOnWrite { get; init; }

        public Task WriteAsync(string attachmentId, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
        {
            if (++_writes == FailOnWrite)
            {
                throw new IOException("The disk is full.");
            }

            Files[attachmentId] = content.ToArray();

            return Task.CompletedTask;
        }

        public Task<byte[]> ReadAsync(string attachmentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Files.GetValueOrDefault(attachmentId));

        public Task<bool> DeleteAsync(string attachmentId, CancellationToken cancellationToken = default)
            => Task.FromResult(Files.Remove(attachmentId));
    }

    private sealed class RefusePdfScanner : IAttachmentScanner
    {
        public Task<string> ScanAsync(AttachmentUpload upload, string contentType, CancellationToken cancellationToken = default)
            => Task.FromResult(contentType == "application/pdf" ? "infected" : null);
    }

    private sealed class OrdersForAgents : IAttachmentAccessProvider
    {
        public string OwnerKind => "Order";

        public Task<bool> CanViewAsync(ClaimsPrincipal user, RecordRef owner, CancellationToken cancellationToken = default)
            => Task.FromResult(user.IsInRole("Agent"));

        public Task<bool> CanManageAsync(ClaimsPrincipal user, RecordRef owner, CancellationToken cancellationToken = default)
            => Task.FromResult(user.IsInRole("Agent"));
    }
}
