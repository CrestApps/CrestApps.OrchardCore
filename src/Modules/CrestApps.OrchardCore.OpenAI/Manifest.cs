using CrestApps.OrchardCore.OpenAI.Core;
using OrchardCore.Modules.Manifest;

[assembly: Module(
    Id = OpenAIConstants.Feature.Area,
    Name = "OpenAI Chat",
    Author = CrestAppsManifestConstants.Author,
    Website = CrestAppsManifestConstants.Website,
    Version = CrestAppsManifestConstants.Version,
    Description = "Provides a way to manage AI chat profiles for OpenAI models.",
    Category = "OpenAI"
)]
