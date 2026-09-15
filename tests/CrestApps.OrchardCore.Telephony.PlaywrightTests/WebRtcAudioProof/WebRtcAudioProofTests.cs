using System.Text.Json;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.WebRtcAudioProof;

/// <summary>
/// Live WebRTC media proof tests that assert received audio content, not mocked call state.
/// </summary>
public sealed class WebRtcAudioProofTests
{
    [Fact(Skip = "Requires live WebRTC Asterisk+coturn+browser; enabled in Part 2-6 nightly E2E.")]
    public async Task DirectIce_RemoteInboundAudioContainsInjectedTone_ExceedsFrequencyPowerThreshold()
    {
        // Arrange
        var options = WebRtcAudioProofOptions.DirectIce();
        await using var session = await WebRtcAudioProofSession.StartAsync(options);
        var page = await session.OpenSoftPhoneAsync();

        // Act
        await session.PlaceCallAsync(page);
        var measurement = await session.MeasureRemoteToneAsync(page);

        // Assert
        Assert.True(
            measurement.PeakDecibels >= options.MinimumPeakDecibels,
            $"Expected {options.ExpectedToneFrequencyHz} Hz peak >= {options.MinimumPeakDecibels} dB, actual {measurement.PeakDecibels} dB.");
        Assert.True(
            measurement.SignalToNoiseDecibels >= options.MinimumSignalToNoiseDecibels,
            $"Expected tone to exceed the surrounding noise floor by {options.MinimumSignalToNoiseDecibels} dB, actual {measurement.SignalToNoiseDecibels} dB.");
    }

    [Fact(Skip = "Requires live WebRTC Asterisk+coturn+browser; enabled in Part 2-6 nightly E2E.")]
    public async Task ForcedTurnRelay_RemoteInboundAudioContainsInjectedTone_ExceedsFrequencyPowerThreshold()
    {
        // Arrange
        var options = WebRtcAudioProofOptions.ForcedTurnRelay();
        await using var session = await WebRtcAudioProofSession.StartAsync(options);
        var page = await session.OpenSoftPhoneAsync();

        // Act
        await session.PlaceCallAsync(page);
        var measurement = await session.MeasureRemoteToneAsync(page);

        // Assert
        Assert.True(
            measurement.PeakDecibels >= options.MinimumPeakDecibels,
            $"Expected relay-only {options.ExpectedToneFrequencyHz} Hz peak >= {options.MinimumPeakDecibels} dB, actual {measurement.PeakDecibels} dB.");
        Assert.True(
            measurement.SignalToNoiseDecibels >= options.MinimumSignalToNoiseDecibels,
            $"Expected relay-only tone to exceed the surrounding noise floor by {options.MinimumSignalToNoiseDecibels} dB, actual {measurement.SignalToNoiseDecibels} dB.");
    }

    private sealed class WebRtcAudioProofSession : IAsyncDisposable
    {
        private readonly WebRtcAudioProofOptions _options;
        private readonly IPlaywright _playwright;
        private readonly IBrowser _browser;
        private readonly IBrowserContext _context;

        private WebRtcAudioProofSession(
            WebRtcAudioProofOptions options,
            IPlaywright playwright,
            IBrowser browser,
            IBrowserContext context)
        {
            _options = options;
            _playwright = playwright;
            _browser = browser;
            _context = context;
        }

        public static async Task<WebRtcAudioProofSession> StartAsync(WebRtcAudioProofOptions options)
        {
            var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Args =
                [
                    "--use-fake-ui-for-media-stream",
                    "--autoplay-policy=no-user-gesture-required",
                ],
                Headless = true,
            });
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                Permissions = ["microphone"],
            });

            var proofConfigJson = JsonSerializer.Serialize(new
            {
                expectedToneFrequencyHz = options.ExpectedToneFrequencyHz,
                iceTransportPolicy = options.IceTransportPolicy,
            });
            await context.AddInitScriptAsync(
                $$"""
                () => {
                    window.__crestAppsWebRtcAudioProof = {{proofConfigJson}};
                }
                """);

            return new WebRtcAudioProofSession(options, playwright, browser, context);
        }

        public async Task<IPage> OpenSoftPhoneAsync()
        {
            var page = await _context.NewPageAsync();
            await page.GotoAsync(_options.SoftPhoneUrl);
            await page.WaitForSelectorAsync(_options.SoftPhoneRootSelector);

            return page;
        }

        public async Task PlaceCallAsync(IPage page)
        {
            await page.ClickAsync("[data-telephony-toggle]");
            await page.FillAsync("[data-telephony-number]", _options.DialDestination);
            await page.ClickAsync("[data-telephony-dial]");
            await page.WaitForFunctionAsync(
                """
                () => {
                    const audio = document.querySelector('[data-telephony-remote-audio]');
                    const stream = audio && audio.srcObject;
                    return stream && stream.getAudioTracks && stream.getAudioTracks().some(track => track.readyState === 'live');
                }
                """);
        }

        public async Task<ToneMeasurement> MeasureRemoteToneAsync(IPage page)
        {
            return await page.EvaluateAsync<ToneMeasurement>(
                """
                async options => {
                    const audio = document.querySelector('[data-telephony-remote-audio]');
                    const stream = audio && audio.srcObject;

                    if (!stream) {
                        throw new Error('Remote audio element does not have a MediaStream srcObject.');
                    }

                    const tracks = stream.getAudioTracks();

                    if (!tracks.length || tracks[0].readyState !== 'live') {
                        throw new Error('Remote inbound audio MediaStreamTrack is not live.');
                    }

                    const AudioContextType = window.AudioContext || window.webkitAudioContext;
                    const audioContext = new AudioContextType();
                    const source = audioContext.createMediaStreamSource(stream);
                    const analyser = audioContext.createAnalyser();
                    analyser.fftSize = options.fftSize;
                    analyser.smoothingTimeConstant = 0;
                    source.connect(analyser);

                    await new Promise(resolve => setTimeout(resolve, options.analysisWindowMilliseconds));

                    const bins = new Float32Array(analyser.frequencyBinCount);
                    analyser.getFloatFrequencyData(bins);

                    const binWidth = audioContext.sampleRate / analyser.fftSize;
                    const expectedBin = Math.round(options.expectedToneFrequencyHz / binWidth);
                    const firstBin = Math.max(0, expectedBin - options.binTolerance);
                    const lastBin = Math.min(bins.length - 1, expectedBin + options.binTolerance);
                    let peakDecibels = Number.NEGATIVE_INFINITY;
                    let peakBin = expectedBin;

                    for (let index = firstBin; index <= lastBin; index++) {
                        if (bins[index] > peakDecibels) {
                            peakDecibels = bins[index];
                            peakBin = index;
                        }
                    }

                    let noiseTotal = 0;
                    let noiseCount = 0;

                    for (let index = 0; index < bins.length; index++) {
                        if (Math.abs(index - peakBin) > options.noiseExclusionBins && Number.isFinite(bins[index])) {
                            noiseTotal += bins[index];
                            noiseCount++;
                        }
                    }

                    await audioContext.close();
                    const noiseFloorDecibels = noiseCount > 0 ? noiseTotal / noiseCount : Number.NEGATIVE_INFINITY;

                    return {
                        frequencyHz: peakBin * binWidth,
                        peakDecibels,
                        noiseFloorDecibels,
                        signalToNoiseDecibels: peakDecibels - noiseFloorDecibels
                    };
                }
                """,
                new
                {
                    analysisWindowMilliseconds = _options.AnalysisWindowMilliseconds,
                    binTolerance = _options.BinTolerance,
                    expectedToneFrequencyHz = _options.ExpectedToneFrequencyHz,
                    fftSize = _options.FftSize,
                    noiseExclusionBins = _options.NoiseExclusionBins,
                });
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
            await _browser.DisposeAsync();
            _playwright.Dispose();
        }
    }

    private sealed record WebRtcAudioProofOptions(
        string SoftPhoneUrl,
        string SoftPhoneRootSelector,
        string DialDestination,
        string IceTransportPolicy,
        int ExpectedToneFrequencyHz,
        int FftSize,
        int BinTolerance,
        int NoiseExclusionBins,
        int AnalysisWindowMilliseconds,
        double MinimumPeakDecibels,
        double MinimumSignalToNoiseDecibels)
    {
        public static WebRtcAudioProofOptions DirectIce()
        {
            return Create("all", "webrtc-tone-440", 440);
        }

        public static WebRtcAudioProofOptions ForcedTurnRelay()
        {
            return Create("relay", "webrtc-tone-523", 523);
        }

        private static WebRtcAudioProofOptions Create(
            string iceTransportPolicy,
            string dialDestination,
            int expectedToneFrequencyHz)
        {
            return new WebRtcAudioProofOptions(
                Environment.GetEnvironmentVariable("CRESTAPPS_WEBRTC_E2E_SOFTPHONE_URL") ?? "https://localhost:5001/Admin/Telephony/SoftPhone",
                "#telephony-soft-phone",
                dialDestination,
                iceTransportPolicy,
                expectedToneFrequencyHz,
                4096,
                2,
                8,
                1500,
                -55,
                12);
        }
    }

    private sealed record ToneMeasurement(
        double FrequencyHz,
        double PeakDecibels,
        double NoiseFloorDecibels,
        double SignalToNoiseDecibels);
}
