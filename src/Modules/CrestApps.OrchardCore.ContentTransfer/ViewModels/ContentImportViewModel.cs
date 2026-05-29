using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.ContentTransfer.ViewModels;

public class ContentImportViewModel
{
    [Required]
    [DataType(DataType.Upload)]
    public IFormFile File { get; set; }
}
