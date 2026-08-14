using CrestApps.OrchardCore.ContentTransfer.Models;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContentTransfer.Filters;

/// <summary>
/// Bounds the request body size of a bulk import upload to a single chunk (plus a small overhead)
/// based on <see cref="ContentImportOptions"/>. This allows very large imports to be uploaded in
/// chunks while keeping any individual request body small, which protects the server from oversized
/// request bodies. When chunked uploads are disabled the whole configured maximum file size is allowed
/// in a single request.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ContentTransferUploadSizeLimitAttribute : Attribute, IFilterFactory, IOrderedFilter
{
    private const long RequestBodyOverhead = 1_048_576;

    /// <summary>
    /// Gets or sets the order in which this authorization filter runs in the MVC filter pipeline.
    /// </summary>
    /// <remarks>
    /// Defaults to <c>900</c>. Do not change this value without understanding the filter ordering, because
    /// it is what makes the upload size limits take effect.
    /// <para>
    /// This filter sets the request body and multipart form size limits, which must be applied before
    /// anything reads the request body. It must run before <c>[ValidateAntiForgeryToken]</c> (whose order is
    /// <c>1000</c>): antiforgery validation reads the form to locate the antiforgery token, which forces the
    /// multipart body to be parsed. If this filter ran after it, the body would already have been parsed
    /// using the default limits and a normal-sized chunk would be rejected. The order must therefore stay
    /// strictly less than <c>1000</c>.
    /// </para>
    /// <para>
    /// It must also run after authentication and authorization filters so an unauthenticated or unauthorized
    /// request still returns 401/403 instead of a size error. The framework's own <c>[RequestSizeLimit]</c>
    /// and <c>[RequestFormLimits]</c> attributes default to <c>900</c> for exactly these reasons.
    /// </para>
    /// </remarks>
    public int Order { get; set; } = 900;

    /// <inheritdoc />
    public bool IsReusable => true;

    /// <inheritdoc />
    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        var options = serviceProvider.GetRequiredService<IOptions<ContentImportOptions>>().Value;
        var maxRequestBodySize = options.MaxUploadChunkSize > 0
            ? options.MaxUploadChunkSize + RequestBodyOverhead
            : Math.Max(options.MaxUploadFileSize, 0) + RequestBodyOverhead;

        return new ContentTransferUploadSizeFilter(maxRequestBodySize);
    }

    private sealed class ContentTransferUploadSizeFilter : IAuthorizationFilter, IRequestFormLimitsPolicy
    {
        private readonly long _maxRequestBodySize;

        public ContentTransferUploadSizeFilter(long maxRequestBodySize)
        {
            _maxRequestBodySize = maxRequestBodySize;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var effectiveFormPolicy = context.FindEffectivePolicy<IRequestFormLimitsPolicy>();

            if (effectiveFormPolicy == null || effectiveFormPolicy == this)
            {
                var features = context.HttpContext.Features;
                var formFeature = features.Get<IFormFeature>();

                if (formFeature?.Form == null)
                {
                    var formOptions = new FormOptions
                    {
                        MultipartBodyLengthLimit = _maxRequestBodySize,
                    };

                    features.Set<IFormFeature>(new FormFeature(context.HttpContext.Request, formOptions));
                }
            }

            if (context.FindEffectivePolicy<IRequestSizePolicy>() == null)
            {
                var maxRequestBodySizeFeature = context.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();

                if (maxRequestBodySizeFeature != null && !maxRequestBodySizeFeature.IsReadOnly)
                {
                    maxRequestBodySizeFeature.MaxRequestBodySize = _maxRequestBodySize;
                }
            }
        }
    }
}
