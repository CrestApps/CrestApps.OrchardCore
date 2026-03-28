# Module Examples

## Example 1: Basic Module with Content Part

### Manifest.cs

```csharp
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Name = "CrestApps.Testimonials",
    Author = "CrestApps",
    Website = "https://crestapps.com",
    Version = "1.0.0",
    Description = "Provides a testimonial content type and part.",
    Category = "Content"
)]

[assembly: Feature(
    Id = "CrestApps.Testimonials",
    Name = "Testimonials",
    Description = "Adds testimonial support with a dedicated content part.",
    Dependencies = new[]
    {
        "OrchardCore.ContentManagement",
        "OrchardCore.ContentTypes"
    },
    Category = "Content"
)]
```

### Startup.cs

```csharp
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using CrestApps.Testimonials.Drivers;
using CrestApps.Testimonials.Models;

namespace CrestApps.Testimonials
{
    public sealed class Startup : StartupBase
    {
        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddContentPart<TestimonialPart>()
                .UseDisplayDriver<TestimonialPartDisplayDriver>();

            services.AddScoped<IDataMigration, Migrations>();
        }
    }
}
```

### Models/TestimonialPart.cs

```csharp
using OrchardCore.ContentManagement;

namespace CrestApps.Testimonials.Models
{
    public sealed class TestimonialPart : ContentPart
    {
        public string AuthorName { get; set; }
        public string Quote { get; set; }
        public int Rating { get; set; }
    }
}
```
