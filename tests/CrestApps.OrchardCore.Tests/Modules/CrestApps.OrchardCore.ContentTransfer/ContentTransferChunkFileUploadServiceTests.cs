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

    private static ContentTransferChunkFileUploadService CreateService(ContentImportOptions options)
        => new(
            new ShellSettings { Name = "Default" },
            Mock.Of<IClock>(),
            NullLogger<ContentTransferChunkFileUploadService>.Instance,
            Options.Create(options));

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
