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

    public int Order { get; set; } = 900;

    public bool IsReusable => true;

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
