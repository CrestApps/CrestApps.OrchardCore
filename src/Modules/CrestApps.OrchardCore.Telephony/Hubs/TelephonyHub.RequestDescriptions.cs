using CrestApps.Core.Support;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony.Hubs;

/// <summary>
/// How a hub action's request is described in the log: phone numbers go through the address redactor, digits are
/// counted rather than shown, and the rest is sanitized against log injection.
/// </summary>
public sealed partial class TelephonyHub
{
    private string DescribeDialRequest(DialRequest request)
    {
        return request is null
            ? "(null)"
            : $"To={_addressRedactor.Redact(request.To)}, From={_addressRedactor.Redact(request.From)}";
    }

    private static string DescribeExtensionDialRequest(ExtensionDialRequest request)
    {
        return request is null
            ? "(null)"
            : $"Extension={request.Extension.SanitizeLogValue()}, TargetUserId={request.TargetUserId.SanitizeLogValue()}";
    }

    private static string DescribeExtensionConferenceRequest(ExtensionConferenceRequest request)
    {
        return request is null
            ? "(null)"
            : $"Extension={request.Extension.SanitizeLogValue()}, TargetUserId={request.TargetUserId.SanitizeLogValue()}, ActiveCallId={request.ActiveCall?.CallId.SanitizeLogValue()}";
    }

    private string DescribeCallReference(CallReference call)
    {
        if (call is null)
        {
            return "(null)";
        }

        return $"CallId={call.CallId.SanitizeLogValue()}, Metadata={DescribeMetadata(call.Metadata)}";
    }

    private string DescribeMetadata(IDictionary<string, object> metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return "(none)";
        }

        return string.Join(", ", metadata.Select(entry => $"{entry.Key.SanitizeLogValue()}={_addressRedactor.Redact(entry.Value?.ToString())}"));
    }

    private string DescribeTransferRequest(TransferRequest request)
    {
        return request is null
            ? "(null)"
            : $"CallId={request.CallId.SanitizeLogValue()}, To={_addressRedactor.Redact(request.To)}, Mode={request.Mode}, IsExtension={request.IsExtension}";
    }

    private static string DescribeMergeRequest(MergeRequest request)
    {
        return request is null
            ? "(null)"
            : $"CallIds={string.Join(',', request.GetCallIds()).SanitizeLogValue()}";
    }

    private static string DescribeSendDigitsRequest(SendDigitsRequest request)
    {
        return request is null
            ? "(null)"
            : $"CallId={request.CallId.SanitizeLogValue()}, DigitsLength={request.Digits?.Length ?? 0}";
    }

    private static string BuildLogRequest(Func<string> requestFactory)
    {
        return requestFactory?.Invoke() ?? "(none)";
    }
}
