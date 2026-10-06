/*
 * Voice isolation: a neural noise suppressor and a noise gate in front of the encoder.
 *
 * The browser's own noise suppression only removes steady noise -- fans, hum, hiss. A call centre's noise is other
 * people talking, and that passes straight through it; automatic gain control then makes it worse, raising the room
 * every time the agent pauses. An agent on a busy floor sounded to the caller like they were in the middle of a
 * crowd, while the same headset on a desk phone app with "voice isolation" sounded quiet. This is the soft phone's
 * answer: the raw capture runs through a speech-enhancement model, then through a gate tuned for a close-talk
 * headset, then through an automatic voice level and the optional boost (mic-boost.js), and that is what the
 * call sends.
 *
 *   microphone -> denoiser (GTCRN or RNNoise) -+-> noise gate -+-> auto level -> [boost gain] -> limiter -> send track
 *                                              +-> floor gain -+
 *                                              +-> level meter (decides the auto level; see auto-level.js)
 *
 * The automatic voice level stands in for the browser's automatic gain control, which this chain has to turn
 * off (it raises the room in every pause). Without it a headset delivering -35 to -25 dBFS was sent at that level,
 * and callers said the agent was very quiet. The level only moves while the denoised signal is above the gate's
 * open threshold -- while the agent is speaking -- and holds through every pause, so it lifts the voice without
 * ever lifting the room on its own. A limiter always follows it.
 *
 * The gate stays ahead of the level, on purpose. Its job is to tell the agent from the room by how loud each is
 * at a close-talk microphone, and that gap is a fact of the acoustics. Levelled first, a quiet headset lifted by
 * 15 dB would carry the room 15 dB nearer every fixed threshold, and the strengths would mean something different
 * on every microphone. Ahead of the level, the gate and the level also agree on what speech is: the level
 * measures the same denoised signal against the same open threshold the gate uses. A voice so quiet that the
 * gate cuts its edges at Medium or High needs the Low strength (or the microphone moved closer) either way --
 * no gain placed after the gate could restore what it cut.
 *
 * The denoiser removes what it recognizes as not-speech, including much of the babble of nearby voices. The gate
 * handles what is left: a close-talk headset hears its wearer 20 dB or more above anyone else in the room, so
 * when the agent is not speaking the level drops under the gate's threshold and the room is cut. The floor is a
 * copy of the denoised signal far below the gate, so a closed gate attenuates the room instead of putting the
 * line into dead digital silence -- which callers hear as a dropped call.
 *
 * Two engines are offered. GTCRN (the default) is a current speech-enhancement network trained on the DNS
 * challenge's mixed noise, and it handles non-stationary noise and babble markedly better than RNNoise, whose
 * band-gain model is closer to a smarter spectral subtractor. It runs at 16 kHz inside (exactly telephony's
 * wideband) and, measured in Chrome, renders at about a tenth of real time, so it fits the audio thread with room
 * to spare on an ordinary desktop. RNNoise renders at about a twenty-fifth of real time and is the lighter choice
 * for an older machine; it is also the automatic fallback when GTCRN cannot load.
 *
 * Everything here is best effort. A browser without AudioWorklet or WebAssembly, a model that fails to load, or an
 * audio engine the browser will not start without a click all reject the returned promise with a reason; the
 * caller sends the raw capture instead and says so in the diagnostics. A call never goes out silent because this
 * failed.
 *
 * The model and gate files are vendored from @sapphi-red/web-noise-suppressor (MIT; RNNoise BSD-3-Clause, GTCRN
 * MIT) by the Resources module and loaded from the URL the widget configuration passes in, so tenant prefixes and
 * virtual directories keep working.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a
 * shared namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 * Every browser API is injectable so the graph wiring can be tested without a browser.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // The engines an agent can choose, in order of preference. The first is the default.
    var VOICE_ISOLATION_ENGINES = ['gtcrn', 'rnnoise'];

    // The gate strengths an agent can choose. The middle one is the default.
    var VOICE_ISOLATION_STRENGTHS = ['low', 'medium', 'high'];

    // The gate settings per strength. Thresholds are the RMS level, in dBFS, of the denoised signal over one
    // 128-sample block. A close-talk headset with automatic gain control off puts conversational speech around
    // -30 to -20 dBFS; voices across the room arrive 20 dB or more below that, and the denoiser has already taken
    // most of them down further. The gate opens on the agent's voice, stays open through the gaps inside a phrase
    // for the hold time, and closes only once the level has stayed under the (lower) close threshold that long.
    // The floor is how far the room is turned down while the gate is closed.
    //   low    - for a quiet voice or a microphone that sits further from the mouth: opens easily, cuts gently.
    //   medium - a headset microphone at the corner of the mouth.
    //   high   - a loud floor: needs a clearer voice to open and cuts the room hardest.
    var GATE_SETTINGS = {
        low: { openThreshold: -50, closeThreshold: -58, holdMs: 300, floorDb: -24 },
        medium: { openThreshold: -40, closeThreshold: -50, holdMs: 250, floorDb: -30 },
        high: { openThreshold: -36, closeThreshold: -45, holdMs: 200, floorDb: -40 }
    };

    // RNNoise is trained at 48 kHz and GTCRN accepts 48 kHz; asking for it avoids a resampler in the worklet.
    var SAMPLE_RATE = 48000;

    // The vendored files, relative to the configured base URL. Their names match the package's dist folder.
    var WORKLET_PATHS = {
        gtcrn: 'gtcrn/workletProcessor.js',
        rnnoise: 'rnnoise/workletProcessor.js',
        gate: 'noiseGate/workletProcessor.js'
    };

    // The processor names the vendored worklets register.
    var PROCESSOR_NAMES = {
        gtcrn: '@sapphi-red/web-noise-suppressor/gtcrn',
        rnnoise: '@sapphi-red/web-noise-suppressor/rnnoise',
        gate: '@sapphi-red/web-noise-suppressor/noise-gate'
    };

    // The wasm binaries. RNNoise ships a SIMD build that is used wherever the browser validates SIMD.
    var WASM_PATHS = {
        gtcrn: { url: 'gtcrn.wasm' },
        rnnoise: { url: 'rnnoise.wasm', simdUrl: 'rnnoise_simd.wasm' }
    };

    // The smallest module using a SIMD instruction, to ask the browser whether it can run the SIMD build. The same
    // probe the package's own loader uses (wasm-feature-detect).
    var SIMD_PROBE = [0, 97, 115, 109, 1, 0, 0, 0, 1, 5, 1, 96, 0, 1, 123, 3, 2, 1, 0, 10, 10, 1, 8, 0, 65, 0, 253, 15, 253, 98, 11];

    // How long the whole build may take before the capture is sent raw instead. Registration waits on it, so it
    // has to be short; the files are small and cached after the first load.
    var BUILD_TIMEOUT_MS = 6000;

    // How long to wait for a suspended audio engine to start. Started inside a click it is immediate; refused, the
    // promise never settles, so it is raced against this.
    var RESUME_TIMEOUT_MS = 400;

    // How often the raw microphone level is read for the dead-microphone check.
    var SOURCE_METER_INTERVAL_MS = 200;

    // How often the automatic voice level measures the denoised signal and decides its gain, and how many samples
    // each measurement covers (2048 at 48 kHz is about 43 ms, so consecutive reads cover nearly all the audio).
    var AUTO_LEVEL_INTERVAL_MS = 50;
    var AUTO_LEVEL_FFT_SIZE = 2048;

    // How quickly the gain node follows a new decision (the time constant of setTargetAtTime): short enough to
    // track the decisions, long enough that a step is not heard as a click.
    var AUTO_LEVEL_SMOOTHING_S = 0.03;

    // The peak level of the RAW capture (0..1 RMS) at or under which a window is digital silence. With the gate
    // in the chain the send track is legitimately near-silent whenever the agent listens, so the dead-microphone
    // check reads the capture before processing instead; any working microphone, even in a quiet room, sits far
    // above this, while a dead or hardware-muted one delivers zeros.
    var RAW_CAPTURE_SILENT_LEVEL = 0.0001;

    // Loaded wasm binaries, per URL, shared by every graph built on this page: a device switch or a settings
    // change rebuilds the graph, and the binary does not need fetching again. A failed load is forgotten so the
    // next build retries it.
    var wasmCache = {};

    function clampVoiceIsolationEngine(value) {
        return VOICE_ISOLATION_ENGINES.indexOf(value) === -1 ? VOICE_ISOLATION_ENGINES[0] : value;
    }

    function clampVoiceIsolationStrength(value) {
        return VOICE_ISOLATION_STRENGTHS.indexOf(value) === -1 ? 'medium' : value;
    }

    // The gate settings for a strength (a copy, so a caller cannot change the table).
    function noiseGateSettingsFor(strength) {
        var settings = GATE_SETTINGS[clampVoiceIsolationStrength(strength)];

        return {
            openThreshold: settings.openThreshold,
            closeThreshold: settings.closeThreshold,
            holdMs: settings.holdMs,
            floorDb: settings.floorDb
        };
    }

    /*
     * A short token for the capture readout and the call quality report:
     *   state 'off'     -> 'vi=off'
     *   state 'active'  -> 'vi=gtcrn+gate' (the engine actually running, which may be the fallback), with
     *                      '+level(+9dB)' when the automatic voice level is on, showing the gain it applies now
     *   state 'pending' -> 'vi=pending' (waiting for a click to start the audio engine)
     *   state 'failed'  -> 'vi=failed'
     */
    function describeVoiceIsolation(state, engine, autoLevelGainDb) {
        if (state === 'active') {
            var label = 'vi=' + (engine || '?') + '+gate';

            if (typeof autoLevelGainDb === 'number' && isFinite(autoLevelGainDb)) {
                var rounded = Math.round(autoLevelGainDb);

                label += '+level(' + (rounded >= 0 ? '+' : '') + rounded + 'dB)';
            }

            return label;
        }

        if (state === 'pending' || state === 'failed') {
            return 'vi=' + state;
        }

        return 'vi=off';
    }

    // Joins a base URL and a relative path with exactly one slash between them.
    function joinUrl(base, path) {
        return String(base || '').replace(/\/+$/, '') + '/' + path;
    }

    // Whether this browser has everything the chain needs. Checked before anything is built, so an unsupported
    // browser costs nothing.
    function isVoiceIsolationSupported(options) {
        var settings = options || {};
        var AudioCtx = settings.audioContext || root.AudioContext || root.webkitAudioContext;
        var WorkletNode = settings.audioWorkletNode || root.AudioWorkletNode;
        var wasm = settings.webAssembly || root.WebAssembly;

        return !!AudioCtx && !!WorkletNode && !!wasm && typeof wasm.validate === 'function';
    }

    function reasonError(reason, message, cause) {
        var error = new Error(message);

        error.voiceIsolationReason = reason;

        if (cause) {
            error.cause = cause;
        }

        return error;
    }

    // Fetches one engine's wasm binary as an ArrayBuffer, picking RNNoise's SIMD build where the browser supports
    // it. The worklet receives it through processorOptions (copied, not transferred), so one cached buffer serves
    // every graph.
    function loadWasm(engine, baseUrl, fetchFn, wasm) {
        var paths = WASM_PATHS[engine];
        var useSimd = false;

        if (paths.simdUrl) {
            try {
                useSimd = !!wasm.validate(new Uint8Array(SIMD_PROBE));
            } catch (error) {
                useSimd = false;
            }
        }

        var url = joinUrl(baseUrl, useSimd ? paths.simdUrl : paths.url);

        if (!wasmCache[url]) {
            wasmCache[url] = Promise.resolve(fetchFn(url, { credentials: 'same-origin' })).then(function (response) {
                if (!response || (typeof response.ok === 'boolean' && !response.ok)) {
                    throw new Error('Loading ' + url + ' failed' + (response && response.status ? ' (' + response.status + ')' : '') + '.');
                }

                return response.arrayBuffer();
            });

            wasmCache[url].catch(function () {
                delete wasmCache[url];
            });
        }

        return wasmCache[url];
    }

    // Resolves once the context is running, or with false once the wait runs out.
    function waitForRunning(context, timers, timeoutMs) {
        if (context.state === 'running') {
            return Promise.resolve(true);
        }

        return new Promise(function (resolve) {
            var settled = false;
            var timer = timers.setTimeout(function () {
                if (!settled) {
                    settled = true;
                    resolve(context.state === 'running');
                }
            }, timeoutMs);

            var resumed;

            try {
                resumed = typeof context.resume === 'function' ? context.resume() : null;
            } catch (error) {
                resumed = null;
            }

            Promise.resolve(resumed).then(function () {
                if (!settled && context.state === 'running') {
                    settled = true;
                    timers.clearTimeout(timer);
                    resolve(true);
                }
            }, function () { /* refused; the timer decides */ });
        });
    }

    /*
     * Builds the voice isolation chain for a captured microphone stream.
     *
     * options.engine            - 'gtcrn' or 'rnnoise' (anything else is the default). GTCRN falls back to RNNoise
     *                             when it cannot load.
     * options.strength          - 'low', 'medium' or 'high': the gate settings.
     * options.autoLevel         - the automatic voice level (on unless false; see auto-level.js).
     * options.autoLevelInitialDb- the gain the automatic level starts from, for example the one it had reached
     *                             on this microphone before the chain was rebuilt (unity when absent).
     * options.boostDb           - the microphone boost, added after the auto level as a manual trim (see
     *                             mic-boost.js).
     * options.assetBaseUrl      - where the vendored worklets and wasm files are served from.
     * options.audioContext      - AudioContext constructor (defaults to the browser's).
     * options.audioWorkletNode  - AudioWorkletNode constructor (defaults to the browser's).
     * options.fetch, options.webAssembly, options.timers, options.now - injectable for the tests.
     *
     * Resolves to { stream, boosted, isolated: true, autoLevel, engine, requestedEngine, strength, label, state(),
     * readSourcePeak(), readAutoLevelGainDb(), dispose() }. Rejects with an Error whose voiceIsolationReason is
     * one of:
     *   'unsupported' - the browser lacks AudioWorklet, WebAssembly, or the asset URL is not configured.
     *   'load-failed' - no engine's worklet or wasm file could be loaded.
     *   'suspended'   - the browser would not start the audio engine without a click on the page.
     *   'graph-failed'- the browser refused the graph (for example a capture rate it cannot connect).
     *   'timeout'     - the build took longer than it may hold up the capture.
     * Nothing is left running after a rejection.
     */
    function createVoiceIsolationPipeline(sourceStream, options) {
        var settings = options || {};
        var AudioCtx = settings.audioContext || root.AudioContext || root.webkitAudioContext;
        var WorkletNode = settings.audioWorkletNode || root.AudioWorkletNode;
        var wasm = settings.webAssembly || root.WebAssembly;
        var fetchFn = settings.fetch || (typeof root.fetch === 'function' ? root.fetch.bind(root) : null);
        var timers = settings.timers || root;
        var baseUrl = settings.assetBaseUrl;
        var requestedEngine = clampVoiceIsolationEngine(settings.engine);
        var strength = clampVoiceIsolationStrength(settings.strength);
        var gateSettings = noiseGateSettingsFor(strength);
        var connectBoostStage = softPhone.connectBoostStage;
        var clampBoostDb = softPhone.clampBoostDb;
        var nextAutoLevelGainDb = softPhone.nextAutoLevelGainDb;
        var clampAutoLevelGainDb = softPhone.clampAutoLevelGainDb;
        var amplitudeToDb = softPhone.amplitudeToDb;
        var dbToGain = softPhone.dbToGain;
        var now = typeof settings.now === 'function' ? settings.now : function () { return Date.now(); };
        var autoLevelOn = settings.autoLevel !== false && typeof nextAutoLevelGainDb === 'function';
        // The level decides on the same threshold the gate opens at, so it moves exactly when the agent is let
        // through (see the header).
        var autoLevelOptions = { speechThresholdDb: gateSettings.openThreshold };

        if (!sourceStream || !baseUrl || !fetchFn || !isVoiceIsolationSupported(settings)) {
            return Promise.reject(reasonError('unsupported',
                !baseUrl ? 'No voice isolation asset URL is configured.' : 'This browser does not support AudioWorklet and WebAssembly.'));
        }

        var context;

        try {
            context = new AudioCtx({ sampleRate: SAMPLE_RATE, latencyHint: 'interactive' });
        } catch (error) {
            return Promise.reject(reasonError('graph-failed', 'The audio engine could not be created: ' + ((error && error.message) || error), error));
        }

        if (!context.audioWorklet || typeof context.audioWorklet.addModule !== 'function') {
            closeQuietly(context);

            return Promise.reject(reasonError('unsupported', 'This browser does not support AudioWorklet.'));
        }

        // Asked for at once, while any click that led here still counts as one.
        var running = waitForRunning(context, timers, RESUME_TIMEOUT_MS);

        var nodes = [];
        var meterTimer = null;
        var levelTimer = null;
        var autoLevelGainDb = autoLevelOn ? clampAutoLevelGainDb(settings.autoLevelInitialDb) : null;
        var disposed = false;
        var timedOut = false;
        var engine = null;
        var denoiser = null;
        var analyser = null;
        var meterData = null;
        var sourcePeak = 0;
        var sourceReads = 0;

        function dispose() {
            if (disposed) {
                return;
            }

            disposed = true;

            if (meterTimer !== null) {
                timers.clearInterval(meterTimer);
                meterTimer = null;
            }

            if (levelTimer !== null) {
                timers.clearInterval(levelTimer);
                levelTimer = null;
            }

            // Frees the model's memory in the worklet. The context closing below ends the worklet scope anyway;
            // this is the polite half.
            try {
                if (denoiser && denoiser.port && typeof denoiser.port.postMessage === 'function') {
                    denoiser.port.postMessage('destroy');
                }
            } catch (error) { /* best effort */ }

            nodes.forEach(function (node) {
                try {
                    node.disconnect();
                } catch (error) { /* best effort */ }
            });

            closeQuietly(context);
        }

        // The automatic voice level: meters the denoised signal (ahead of the gate, the floor and the level
        // itself) and steers the level gain with nextAutoLevelGainDb. Main-thread polling is plenty here -- the
        // decisions are slow by design, and each one is bounded by the time that actually passed, so a timer the
        // browser delays only slows the level down; it can never make it jump.
        function startAutoLevel(input, level, destination) {
            var levelAnalyser = context.createAnalyser();
            levelAnalyser.fftSize = AUTO_LEVEL_FFT_SIZE;
            nodes.push(levelAnalyser);

            var levelSink = context.createGain();
            levelSink.gain.value = 0;
            nodes.push(levelSink);

            input.connect(levelAnalyser);
            levelAnalyser.connect(levelSink);
            levelSink.connect(destination);

            var levelData = new Float32Array(levelAnalyser.fftSize);
            var lastDecision = now();

            levelTimer = timers.setInterval(function () {
                if (disposed) {
                    return;
                }

                var at = now();
                var elapsed = at - lastDecision;

                lastDecision = at;

                try {
                    levelAnalyser.getFloatTimeDomainData(levelData);
                } catch (error) {
                    return;
                }

                var next = nextAutoLevelGainDb(autoLevelGainDb, amplitudeToDb(frameRms(levelData)), autoLevelOptions, elapsed);

                if (next === autoLevelGainDb) {
                    return;
                }

                autoLevelGainDb = next;

                try {
                    if (typeof level.gain.setTargetAtTime === 'function') {
                        level.gain.setTargetAtTime(dbToGain(next), context.currentTime, AUTO_LEVEL_SMOOTHING_S);
                    } else {
                        level.gain.value = dbToGain(next);
                    }
                } catch (error) { /* best effort: the previous gain stays */ }
            }, AUTO_LEVEL_INTERVAL_MS);
        }

        // Loads one engine: its worklet module and its wasm binary, in parallel.
        function loadEngine(name) {
            return Promise.all([
                context.audioWorklet.addModule(joinUrl(baseUrl, WORKLET_PATHS[name])),
                loadWasm(name, baseUrl, fetchFn, wasm)
            ]).then(function (results) {
                return { name: name, wasmBinary: results[1] };
            });
        }

        // The requested engine, then the lighter one if it is different and the first could not load.
        function loadAnyEngine() {
            return loadEngine(requestedEngine).catch(function (firstError) {
                var fallback = requestedEngine === 'rnnoise' ? null : 'rnnoise';

                if (!fallback) {
                    throw firstError;
                }

                return loadEngine(fallback).catch(function () {
                    throw firstError;
                });
            });
        }

        var build = Promise.all([
            loadAnyEngine().catch(function (error) {
                throw reasonError('load-failed', 'The voice isolation model could not be loaded: ' + ((error && error.message) || error), error);
            }),
            context.audioWorklet.addModule(joinUrl(baseUrl, WORKLET_PATHS.gate)).catch(function (error) {
                throw reasonError('load-failed', 'The noise gate could not be loaded: ' + ((error && error.message) || error), error);
            }),
            running
        ]).then(function (results) {
            if (timedOut || disposed) {
                throw reasonError('timeout', 'Voice isolation took too long to start.');
            }

            if (!results[2]) {
                throw reasonError('suspended', 'The browser has not allowed the audio engine to start yet; it needs a click on the page.');
            }

            var loaded = results[0];
            var mono = { channelCount: 1, channelCountMode: 'explicit', channelInterpretation: 'speakers', numberOfInputs: 1, numberOfOutputs: 1, outputChannelCount: [1] };

            engine = loaded.name;

            try {
                var source = context.createMediaStreamSource(sourceStream);
                nodes.push(source);

                denoiser = new WorkletNode(context, PROCESSOR_NAMES[engine], Object.assign({
                    processorOptions: { maxChannels: 1, wasmBinary: loaded.wasmBinary }
                }, mono));
                nodes.push(denoiser);

                var gate = new WorkletNode(context, PROCESSOR_NAMES.gate, Object.assign({
                    processorOptions: {
                        openThreshold: gateSettings.openThreshold,
                        closeThreshold: gateSettings.closeThreshold,
                        holdMs: gateSettings.holdMs,
                        maxChannels: 1
                    }
                }, mono));
                nodes.push(gate);

                var floor = context.createGain();
                floor.gain.value = Math.pow(10, gateSettings.floorDb / 20);
                nodes.push(floor);

                var mix = context.createGain();
                mix.gain.value = 1;
                nodes.push(mix);

                source.connect(denoiser);
                denoiser.connect(gate);
                denoiser.connect(floor);
                gate.connect(mix);
                floor.connect(mix);

                var output = mix;
                var level = null;

                if (autoLevelOn) {
                    level = context.createGain();
                    level.gain.value = dbToGain(autoLevelGainDb);
                    nodes.push(level);
                    mix.connect(level);
                    output = level;
                }

                // The boost is the agent's manual trim on top of the auto level; the limiter after it is always
                // there while the auto level is, so neither can clip.
                var boost = typeof connectBoostStage === 'function'
                    ? connectBoostStage(context, output, settings.boostDb, { alwaysLimit: autoLevelOn })
                    : null;

                if (boost) {
                    boost.nodes.forEach(function (node) { nodes.push(node); });
                    output = boost.output;
                }

                var destination = context.createMediaStreamDestination();
                output.connect(destination);

                if (level) {
                    startAutoLevel(denoiser, level, destination);
                }

                // The raw-capture meter for the dead-microphone check (see RAW_CAPTURE_SILENT_LEVEL). Routed into
                // the destination through a silent gain so every browser pulls it; it adds nothing to the call.
                analyser = context.createAnalyser();
                analyser.fftSize = 512;
                nodes.push(analyser);

                var meterSink = context.createGain();
                meterSink.gain.value = 0;
                nodes.push(meterSink);

                source.connect(analyser);
                analyser.connect(meterSink);
                meterSink.connect(destination);
                meterData = new Float32Array(analyser.fftSize);

                meterTimer = timers.setInterval(function () {
                    if (disposed) {
                        return;
                    }

                    try {
                        analyser.getFloatTimeDomainData(meterData);
                    } catch (error) {
                        return;
                    }

                    sourcePeak = Math.max(sourcePeak, frameRms(meterData));
                    sourceReads++;
                }, SOURCE_METER_INTERVAL_MS);

                return destination.stream;
            } catch (error) {
                throw reasonError('graph-failed', 'The browser refused the voice isolation graph: ' + ((error && error.message) || error), error);
            }
        }).then(function (stream) {
            var boosted = typeof clampBoostDb === 'function' && clampBoostDb(settings.boostDb) > 0;

            return {
                stream: stream,
                boosted: boosted,
                isolated: true,
                autoLevel: autoLevelOn,
                engine: engine,
                requestedEngine: requestedEngine,
                strength: strength,
                label: describeVoiceIsolation('active', engine, autoLevelGainDb),
                // The gain the automatic voice level applies now, in dB, or null when it is off.
                readAutoLevelGainDb: function () {
                    return autoLevelGainDb;
                },
                // The audio engine's state: anything but 'running' means the far end hears silence.
                state: function () {
                    return disposed ? 'closed' : context.state;
                },
                // The loudest raw-capture level since the previous read (0..1 RMS), or -1 when nothing was
                // measured in the window. Starts a new window.
                readSourcePeak: function () {
                    if (!sourceReads) {
                        return -1;
                    }

                    var peak = sourcePeak;

                    sourcePeak = 0;
                    sourceReads = 0;

                    return peak;
                },
                dispose: dispose
            };
        });

        var guarded = new Promise(function (resolve, reject) {
            var timer = timers.setTimeout(function () {
                timedOut = true;
                dispose();
                reject(reasonError('timeout', 'Voice isolation took longer than ' + BUILD_TIMEOUT_MS + ' ms to start.'));
            }, typeof settings.timeoutMs === 'number' ? settings.timeoutMs : BUILD_TIMEOUT_MS);

            build.then(function (pipeline) {
                timers.clearTimeout(timer);

                if (timedOut) {
                    pipeline.dispose();

                    return;
                }

                resolve(pipeline);
            }, function (error) {
                timers.clearTimeout(timer);
                dispose();
                reject(error);
            });
        });

        return guarded;
    }

    // Root-mean-square amplitude of one waveform read.
    function frameRms(samples) {
        var total = 0;

        for (var i = 0; i < samples.length; i++) {
            total += samples[i] * samples[i];
        }

        return samples.length ? Math.sqrt(total / samples.length) : 0;
    }

    function closeQuietly(context) {
        try {
            if (context && typeof context.close === 'function') {
                Promise.resolve(context.close()).catch(function () { });
            }
        } catch (error) { /* best effort */ }
    }

    // Forgets every cached wasm binary. For the tests.
    function resetVoiceIsolationCache() {
        wasmCache = {};
    }

    softPhone.VOICE_ISOLATION_ENGINES = VOICE_ISOLATION_ENGINES;
    softPhone.VOICE_ISOLATION_STRENGTHS = VOICE_ISOLATION_STRENGTHS;
    softPhone.RAW_CAPTURE_SILENT_LEVEL = RAW_CAPTURE_SILENT_LEVEL;
    softPhone.clampVoiceIsolationEngine = clampVoiceIsolationEngine;
    softPhone.clampVoiceIsolationStrength = clampVoiceIsolationStrength;
    softPhone.noiseGateSettingsFor = noiseGateSettingsFor;
    softPhone.describeVoiceIsolation = describeVoiceIsolation;
    softPhone.isVoiceIsolationSupported = isVoiceIsolationSupported;
    softPhone.createVoiceIsolationPipeline = createVoiceIsolationPipeline;
    softPhone.resetVoiceIsolationCache = resetVoiceIsolationCache;
}(typeof globalThis !== 'undefined' ? globalThis : window));
