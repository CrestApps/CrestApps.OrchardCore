using CrestApps.OrchardCore.Telephony.Models;
using OrchardCore.ResourceManagement;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Default <see cref="ISoftPhoneWidgetPresenter"/> implementation.
/// </summary>
public sealed class SoftPhoneWidgetPresenter : ISoftPhoneWidgetPresenter
{
    private readonly ITelephonyProviderResolver _providerResolver;
    private readonly ISiteService _siteService;
    private readonly IResourceManager _resourceManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftPhoneWidgetPresenter"/> class.
    /// </summary>
    /// <param name="providerResolver">The telephony provider resolver.</param>
    /// <param name="siteService">The site service used to read the soft phone settings.</param>
    /// <param name="resourceManager">The resource manager used to register styles and scripts.</param>
    public SoftPhoneWidgetPresenter(
        ITelephonyProviderResolver providerResolver,
        ISiteService siteService,
        IResourceManager resourceManager)
    {
        _providerResolver = providerResolver;
        _siteService = siteService;
        _resourceManager = resourceManager;
    }

    /// <inheritdoc/>
    public async Task<SoftPhoneWidget> CreateWidgetAsync()
    {
        var settings = await _siteService.GetSettingsAsync<SoftPhoneWidgetSettings>();
        var provider = await _providerResolver.GetAsync();
        var audioProvider = provider as ITelephonyAudioProvider;
        var audioCapabilities = audioProvider?.AudioCapabilities ?? TelephonyAudioCapabilities.None;
        var audioMode = audioProvider is null
            ? TelephonyAudioMode.None
            : TelephonyAudioModeResolver.Resolve(
                audioCapabilities,
                audioProvider.ConfiguredAudioMode,
                audioProvider.BrowserMediaAdapterName);

        return new SoftPhoneWidget
        {
            AccentColor = string.IsNullOrWhiteSpace(settings?.AccentColor)
                ? SoftPhoneWidgetSettings.DefaultAccentColor
                : settings.AccentColor,
            Capabilities = provider?.Capabilities ?? TelephonyCapabilities.None,
            AudioCapabilities = audioCapabilities,
            AudioMode = audioMode,
            BrowserMediaAdapterName = audioProvider?.BrowserMediaAdapterName,
            RecentCallsCount = settings?.RecentCallsCount is >= 1 and <= 200
                ? settings.RecentCallsCount
                : SoftPhoneWidgetSettings.DefaultRecentCallsCount,
            DefaultCountryCode = SoftPhoneCountries.ResolveDefaultCountryCode(settings?.DefaultCountryCode),
        };
    }

    /// <inheritdoc/>
    public void RegisterResources(SoftPhoneWidget widget)
    {
        ArgumentNullException.ThrowIfNull(widget);

        _resourceManager.RegisterResource("stylesheet", "intl-tel-input").AtHead();
        _resourceManager.RegisterResource("stylesheet", "telephony-soft-phone").AtHead();

        // The WebRTC soft phone loads a provider-specific browser audio library, chosen by the active
        // provider's BrowserMediaAdapterName. Only the library the current provider actually needs is
        // pulled, so providers without in-browser media load neither, and a SIP.js provider never downloads
        // the Telnyx SDK (or vice versa).
        if (widget.AudioMode == TelephonyAudioMode.Browser)
        {
            var adapterName = widget.BrowserMediaAdapterName;

            if (string.Equals(adapterName, "sipjs", StringComparison.OrdinalIgnoreCase))
            {
                _resourceManager.RegisterResource("script", "sip.js").AtFoot();
            }
            else if (string.Equals(adapterName, "telnyx-webrtc", StringComparison.OrdinalIgnoreCase))
            {
                _resourceManager.RegisterResource("script", "telnyx-webrtc").AtFoot();
            }
        }

        _resourceManager.RegisterResource("script", "telephony-soft-phone").AtFoot();
        _resourceManager.RegisterResource("script", "telephony-phone-field").AtFoot();
    }
}
