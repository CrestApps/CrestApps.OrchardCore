using System.Net;
using System.Text;
using CrestApps.OrchardCore.Learning.Packages.Core.Services;
using CrestApps.OrchardCore.Learning.Packages.Models;

namespace CrestApps.OrchardCore.Learning.Packages.Services;

/// <summary>
/// The frame page of a launch on the package content host. The player frames this page; it defines <c>window.API</c>
/// (SCORM 1.2) or <c>window.API_1484_11</c> (SCORM 2004) with the learner's data, and loads the package's launch file in
/// a child frame on the same origin, so a SCO, or Articulate's SCORM Driver in a frame of its own, finds the API by
/// walking up its parents. The API sends what the content sets to the player page by <c>postMessage</c>, to the
/// player's origin only; the player checks the content host's origin and sends it to the server, which checks every
/// value again.
/// </summary>
public static class PackageFramePage
{
    /// <summary>
    /// The name of the child frame that holds the package.
    /// </summary>
    public const string ContentFrameName = "crestapps-package-content";

    /// <summary>
    /// Builds the page.
    /// </summary>
    /// <param name="launch">The launch, with the player's origin.</param>
    /// <param name="state">The runtime state of the launch, or <see langword="null"/>.</param>
    /// <param name="standard">The standard of the package.</param>
    /// <param name="contentUrl">The URL of the launch file, on the content host.</param>
    /// <param name="title">The title of the page.</param>
    /// <returns>The page.</returns>
    public static string Build(PackageLaunch launch, PackageRuntimeState state, string standard, string contentUrl, string title)
    {
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(contentUrl);

        return new StringBuilder()
            .Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>")
            .Append(WebUtility.HtmlEncode(title ?? string.Empty))
            .Append("</title><style>html,body{margin:0;padding:0;height:100%;overflow:hidden}iframe{display:block;width:100%;height:100%;border:0}</style>")
            .Append(PackageShimScript.BuildBridge(launch, state, launch.PlayerOrigin, standard))
            .Append("</head><body><iframe name=\"")
            .Append(ContentFrameName)
            .Append("\" src=\"")
            .Append(WebUtility.HtmlEncode(contentUrl))
            .Append("\" allow=\"fullscreen; autoplay\" allowfullscreen></iframe></body></html>")
            .ToString();
    }
}
