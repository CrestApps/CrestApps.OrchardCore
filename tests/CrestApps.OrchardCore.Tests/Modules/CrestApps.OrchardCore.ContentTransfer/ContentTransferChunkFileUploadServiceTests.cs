using System.Text;
using CrestApps.OrchardCore.ContentTransfer.Models;
using CrestApps.OrchardCore.ContentTransfer.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.FileStorage;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContentTransfer;

public sealed class ContentTransferChunkFileUploadServiceTests
{
    private const string UploadIdFormKey = "__chunkedFileUploadId";

    [Fact]
    public async Task ProcessRequestAsync_WithoutContentRange_InvokesCompletedWithRequestFiles()
    {
        // Arrange
        var service = CreateService(new ContentImportOptions
        {
            MaxUploadChunkSize = 1024,
            MaxUploadFileSize = 1_000_000,
        });

        var context = CreateContext(contentRange: null, uploadId: null, CreateFormFile("hello"));

        var completedInvoked = false;
        var chunkInvoked = false;
        ContentTransferUploadError? invalidError = null;

        // Act
        await service.ProcessRequestAsync(
            context.Request,
            (_, _, _) =>
            {
                chunkInvoked = true;

                return Ok();
            },
            files =>
            {
                completedInvoked = true;
                Assert.Single(files);

                return Ok();
            },
            error =>
            {
                invalidError = error;

                return Invalid();
            });

        // Assert
        Assert.True(completedInvoked);
        Assert.False(chunkInvoked);
        Assert.Null(invalidError);
    }

    [Fact]
    public async Task ProcessRequestAsync_WhenChunkingDisabled_InvokesCompleted()
    {
        // Arrange
        var service = CreateService(new ContentImportOptions
        {
            MaxUploadChunkSize = 0,
            MaxUploadFileSize = 1_000_000,
        });

        var uploadId = Guid.NewGuid();
        var context = CreateContext("bytes 0-4/5", uploadId, CreateFormFile("hello"));

        var completedInvoked = false;
        ContentTransferUploadError? invalidError = null;

        // Act
        await service.ProcessRequestAsync(
            context.Request,
            (_, _, _) => Ok(),
            _ =>
            {
                completedInvoked = true;

                return Ok();
            },
            error =>
            {
                invalidError = error;

                return Invalid();
            });

        // Assert
        Assert.True(completedInvoked);
        Assert.Null(invalidError);
    }

    [Fact]
    public async Task ProcessRequestAsync_WhenFileExceedsMaxFileSize_InvokesInvalid()
    {
        // Arrange
        var service = CreateService(new ContentImportOptions
        {
            MaxUploadChunkSize = 1024,
            MaxUploadFileSize = 10,
        });

        var uploadId = Guid.NewGuid();
        var context = CreateContext("bytes 0-4/100", uploadId, CreateFormFile("hello"));

        ContentTransferUploadError? invalidError = null;

        // Act
        await service.ProcessRequestAsync(
            context.Request,
            (_, _, _) => Ok(),
            _ => Ok(),
            error =>
            {
                invalidError = error;

                return Invalid();
            });

        // Assert
        Assert.Equal(ContentTransferUploadError.MaxFileSizeExceeded, invalidError);
    }

    [Fact]
    public async Task ProcessRequestAsync_WhenChunkExceedsMaxChunkSize_InvokesInvalid()
    {
        // Arrange
        var service = CreateService(new ContentImportOptions
        {
            MaxUploadChunkSize = 4,
            MaxUploadFileSize = 1_000,
        });

        var uploadId = Guid.NewGuid();
        var context = CreateContext("bytes 0-9/20", uploadId, CreateFormFile("0123456789"));

        ContentTransferUploadError? invalidError = null;

        // Act
        await service.ProcessRequestAsync(
            context.Request,
            (_, _, _) => Ok(),
            _ => Ok(),
            error =>
            {
                invalidError = error;

                return Invalid();
            });

        // Assert
        Assert.Equal(ContentTransferUploadError.MaxChunkSizeExceeded, invalidError);
    }

    [Fact]
    public async Task ProcessRequestAsync_WithMultipleFiles_InvokesInvalid()
    {
        // Arrange
        var service = CreateService(new ContentImportOptions
        {
            MaxUploadChunkSize = 1024,
            MaxUploadFileSize = 1_000_000,
        });

        var uploadId = Guid.NewGuid();
        var context = CreateContext("bytes 0-4/10", uploadId, CreateFormFile("hello"), CreateFormFile("world"));

        ContentTransferUploadError? invalidError = null;

        // Act
        await service.ProcessRequestAsync(
            context.Request,
            (_, _, _) => Ok(),
            _ => Ok(),
            error =>
            {
                invalidError = error;

                return Invalid();
            });

        // Assert
        Assert.Equal(ContentTransferUploadError.InvalidRequest, invalidError);
    }

    [Fact]
    public async Task ProcessRequestAsync_WithSingleChunk_AssemblesAndCompletes()
    {
        // Arrange
        var service = CreateService(new ContentImportOptions
        {
            MaxUploadChunkSize = 1024,
            MaxUploadFileSize = 1_000_000,
        });

        var uploadId = Guid.NewGuid();
        var context = CreateContext("bytes 0-4/5", uploadId, CreateFormFile("hello"));

        string completedContent = null;

        // Act
        await service.ProcessRequestAsync(
            context.Request,
            (_, _, _) => Ok(),
            files =>
            {
                completedContent = ReadContent(files.Single());

                return Ok();
            },
            _ => Invalid());

        // Assert
        Assert.Equal("hello", completedContent);
    }

    [Fact]
    public async Task ProcessRequestAsync_WithMultipleChunks_AssemblesFullFile()
    {
        // Arrange
        var service = CreateService(new ContentImportOptions
        {
            MaxUploadChunkSize = 5,
            MaxUploadFileSize = 1_000_000,
        });

        var uploadId = Guid.NewGuid();
        const string fileName = "data.csv";

        var firstContext = CreateContext("bytes 0-4/10", uploadId, CreateFormFile("01234", fileName));
        var chunkInvoked = false;

        // Act - first chunk.
        await service.ProcessRequestAsync(
            firstContext.Request,
            (_, _, _) =>
            {
                chunkInvoked = true;

                return Ok();
            },
            _ => Ok(),
            _ => Invalid());

        var secondContext = CreateContext("bytes 5-9/10", uploadId, CreateFormFile("56789", fileName));
        string completedContent = null;

        // Act - last chunk.
        await service.ProcessRequestAsync(
            secondContext.Request,
            (_, _, _) => Ok(),
            files =>
            {
                completedContent = ReadContent(files.Single());

                return Ok();
            },
            _ => Invalid());

        // Assert
        Assert.True(chunkInvoked);
        Assert.Equal("0123456789", completedContent);
    }

    // SEC-A17: each upload id used to preallocate the size the client declared, up to the upload limit, before a byte
    // arrived; a chunk that starts past the end did the same by seeking. Chunks must now arrive in order.
    [Fact]
    public async Task ProcessRequestAsync_FirstChunk_DoesNotPreallocateTheDeclaredSize()
    {
        var service = CreateService(new ContentImportOptions { MaxUploadChunkSize = 1024, MaxUploadFileSize = 1_000_000 });
        var uploadId = Guid.NewGuid();
        var file = CreateFormFile("hello", "prealloc.csv");
        var context = CreateContext("bytes 0-4/900000", uploadId, file);

        await service.ProcessRequestAsync(context.Request, (_, _, _) => Ok(), _ => Ok(), _ => Invalid());

        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', uploadId.ToString("N"), file.FileName, file.Name))));
        var tempFile = new FileInfo(Path.Combine(Path.GetTempPath(), "CrestAppsContentTransferUploads", "Default_" + hash));

        try
        {
            Assert.Equal(5, tempFile.Length);
        }
        finally
        {
            tempFile.Delete();
        }
    }

    [Fact]
    public async Task ProcessRequestAsync_ChunkThatStartsPastTheEnd_IsRefused()
    {
        var service = CreateService(new ContentImportOptions { MaxUploadChunkSize = 1024, MaxUploadFileSize = 1_000_000 });
        var context = CreateContext("bytes 899995-899999/900000", Guid.NewGuid(), CreateFormFile("hello", "gap.csv"));
        ContentTransferUploadError? invalidError = null;

        await service.ProcessRequestAsync(context.Request, (_, _, _) => Ok(), _ => Ok(), error =>
        {
            invalidError = error;

            return Invalid();
        });

        Assert.Equal(ContentTransferUploadError.InvalidRequest, invalidError);
    }

    // Review 10 (B7): the declared range was checked, then the whole file was copied, so a chunk larger than its range
    // reached temporary storage past the chunk size limit.
    [Fact]
    public async Task ProcessRequestAsync_AChunkLargerThanItsRange_IsRefused_BeforeAnythingIsWritten()
    {
        // Arrange
        var service = CreateService(new ContentImportOptions { MaxUploadChunkSize = 5, MaxUploadFileSize = 1_000 });
        var uploadId = Guid.NewGuid();
        var file = CreateFormFile("0123456789ABCDEF", "oversized.csv");
        var context = CreateContext("bytes 0-4/10", uploadId, file);
        var chunkInvoked = false;
        ContentTransferUploadError? invalidError = null;

        // Act
        await service.ProcessRequestAsync(
            context.Request,
            (_, _, _) =>
            {
                chunkInvoked = true;

                return Ok();
            },
            _ => Ok(),
            error =>
            {
                invalidError = error;

                return Invalid();
            });

        // Assert
        var tempFile = TempFile(uploadId, file);

        try
        {
            Assert.False(chunkInvoked);
            Assert.Equal(ContentTransferUploadError.InvalidRequest, invalidError);
            Assert.False(tempFile.Exists && tempFile.Length > 0, "Nothing of the chunk may reach temporary storage.");
        }
        finally
        {
            tempFile.Delete();
        }
    }

    // Review 10 (B7): the range decided completion, so a final chunk shorter than its range completed a short file.
    [Fact]
    public async Task ProcessRequestAsync_AFinalChunkShorterThanItsRange_DoesNotComplete()
    {
        // Arrange
        var service = CreateService(new ContentImportOptions { MaxUploadChunkSize = 5, MaxUploadFileSize = 1_000 });
        var uploadId = Guid.NewGuid();
        var first = CreateFormFile("01234", "short.csv");

        await service.ProcessRequestAsync(CreateContext("bytes 0-4/10", uploadId, first).Request, (_, _, _) => Ok(), _ => Ok(), _ => Invalid());

        var completedInvoked = false;
        ContentTransferUploadError? invalidError = null;

        // Act
        await service.ProcessRequestAsync(
            CreateContext("bytes 5-9/10", uploadId, CreateFormFile("5", "short.csv")).Request,
            (_, _, _) => Ok(),
            _ =>
            {
                completedInvoked = true;

                return Ok();
            },
            error =>
            {
                invalidError = error;

                return Invalid();
            });

        // Assert
        TempFile(uploadId, first).Delete();
        Assert.False(completedInvoked);
        Assert.Equal(ContentTransferUploadError.InvalidRequest, invalidError);
    }

    // Review 10 (B7): an upload whose assembled file isn't the length its final chunk declares isn't completed, such as
    // a file grown by an earlier chunk past the length declared at the end.
    [Fact]
    public async Task ProcessRequestAsync_AnAssembledFileLongerThanItsDeclaredLength_DoesNotComplete()
    {
        // Arrange
        var service = CreateService(new ContentImportOptions { MaxUploadChunkSize = 10, MaxUploadFileSize = 1_000 });
        var uploadId = Guid.NewGuid();
        var first = CreateFormFile("0123456789", "assembled.csv");

        await service.ProcessRequestAsync(CreateContext("bytes 0-9/20", uploadId, first).Request, (_, _, _) => Ok(), _ => Ok(), _ => Invalid());

        var completedInvoked = false;
        ContentTransferUploadError? invalidError = null;

        // Act: a final chunk that declares the file five bytes long.
        await service.ProcessRequestAsync(
            CreateContext("bytes 0-4/5", uploadId, CreateFormFile("01234", "assembled.csv")).Request,
            (_, _, _) => Ok(),
            _ =>
            {
                completedInvoked = true;

                return Ok();
            },
            error =>
            {
                invalidError = error;

                return Invalid();
            });

        // Assert
        var tempFile = TempFile(uploadId, first);

        try
        {
            Assert.False(completedInvoked);
            Assert.Equal(ContentTransferUploadError.InvalidRequest, invalidError);
            Assert.False(tempFile.Exists, "A refused upload's temporary file is removed.");
        }
        finally
        {
            tempFile.Delete();
        }
    }

    private static FileInfo TempFile(Guid uploadId, FormFile file)
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|', uploadId.ToString("N"), file.FileName, file.Name))));

        return new FileInfo(Path.Combine(Path.GetTempPath(), "CrestAppsContentTransferUploads", "Default_" + hash));
    }

    private static ContentTransferChunkFileUploadService CreateService(ContentImportOptions options)
        => new(
            new ShellSettings { Name = "Default" },
            Mock.Of<IClock>(),
            NullLogger<ContentTransferChunkFileUploadService>.Instance,
            Options.Create(options),
            Mock.Of<ITempDirectoryProvider>(provider => provider.GetRootDirectory() == Path.GetTempPath()));

    private static DefaultHttpContext CreateContext(string contentRange, Guid? uploadId, params IFormFile[] files)
    {
        var context = new DefaultHttpContext();
        var fields = new Dictionary<string, StringValues>();

        if (uploadId.HasValue)
        {
            fields[UploadIdFormKey] = uploadId.Value.ToString();
        }

        var formFiles = new FormFileCollection();
        formFiles.AddRange(files);
        context.Request.Form = new FormCollection(fields, formFiles);

        if (contentRange != null)
        {
            context.Request.Headers["Content-Range"] = contentRange;
        }

        return context;
    }

    private static FormFile CreateFormFile(string content, string fileName = "test.csv")
    {
        var bytes = Encoding.UTF8.GetBytes(content);

        return new FormFile(new MemoryStream(bytes), 0, bytes.Length, "ImportContentFile.File", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv",
        };
    }

    private static string ReadContent(IFormFile file)
    {
        using var memoryStream = new MemoryStream();
        file.OpenReadStream().CopyTo(memoryStream);

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }

    private static Task<IActionResult> Ok()
        => Task.FromResult<IActionResult>(new OkResult());

    private static Task<IActionResult> Invalid()
        => Task.FromResult<IActionResult>(new BadRequestResult());
}
