
using Microsoft.AspNetCore.Http;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.ContentTransfer.Models;

public sealed class ImportContent : Entity
{
    public string ContentTypeName { get; set; }

    public string ContentTypeId { get; set; }

    public IFormFile File { get; set; }
}
