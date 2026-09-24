/*
 * Provider-agnostic soft phone client.
 *
 * Connects to the Telephony SignalR hub and drives the floating soft phone UI. The widget can be
 * dragged, remembers its position and open state, reflects the provider/connection status, shows a
 * call history, and reaches the same provider implementation regardless of which telephony provider
 * is configured server-side.
 */
(function () {
    'use strict';

    // The parts of the soft phone with no DOM and no provider in them live in Assets/js/soft-phone/*.js and are
    // concatenated ahead of this file into the same bundle. They attach to a shared namespace rather than
    // exporting, so one source file runs both in the browser and under the JavaScript unit tests (`npm test`) —
    // which is the point of the split: this file is far too large to test, and those parts are the ones where a
    // silent arithmetic or mapping mistake reaches an agent as a wrong number on screen or a call that looks
    // connected when it is ringing.
    var softPhoneModules = (typeof globalThis !== 'undefined' ? globalThis : window).CrestAppsSoftPhone || {};

    var normalizeDialNumber = softPhoneModules.normalizeDialNumber;
    var formatNanpNumber = softPhoneModules.formatNanpNumber;
    var formatInternationalNumber = softPhoneModules.formatInternationalNumber;
    var formatPhoneNumber = softPhoneModules.formatPhoneNumber;

    var QUALITY_POOR_MOS = softPhoneModules.QUALITY_POOR_MOS;
    var QUALITY_POOR_LOSS_PERCENT = softPhoneModules.QUALITY_POOR_LOSS_PERCENT;
    var QUALITY_SILENT_MIC_SAMPLES = softPhoneModules.QUALITY_SILENT_MIC_SAMPLES;
    var LEVEL_UNKNOWN = softPhoneModules.LEVEL_UNKNOWN;
    var LEVEL_SILENT = softPhoneModules.LEVEL_SILENT;
    var estimateMos = softPhoneModules.estimateMos;
    var isCaptureSilent = softPhoneModules.isCaptureSilent;
    var readJitterBufferMs = softPhoneModules.readJitterBufferMs;
    var parseWebRtcStats = softPhoneModules.parseWebRtcStats;
    var createLevelProbe = softPhoneModules.createLevelProbe;
    var probeLevel = softPhoneModules.probeLevel;
    var isTrackDeliverable = softPhoneModules.isTrackDeliverable;
    var captureProbeNeedsRebuild = softPhoneModules.captureProbeNeedsRebuild;
    var windowedMicrophoneLevel = softPhoneModules.windowedMicrophoneLevel;
    var findAudioSender = softPhoneModules.findAudioSender;
    var formatCallStatus = softPhoneModules.formatCallStatus;
    var connectedAtFor = softPhoneModules.connectedAtFor;
    var isVirtualAudioDevice = softPhoneModules.isVirtualAudioDevice;
    var resolveDeviceLabel = softPhoneModules.resolveDeviceLabel;
    var durationMeta = softPhoneModules.durationMeta;
    var clampBoostDb = softPhoneModules.clampBoostDb;
    var describeBoost = softPhoneModules.describeBoost;
    var createBoostPipeline = softPhoneModules.createBoostPipeline;
    var clampPlayoutDelay = softPhoneModules.clampPlayoutDelay;
    var describePlayoutDelay = softPhoneModules.describePlayoutDelay;
    var applyPlayoutDelay = softPhoneModules.applyPlayoutDelay;
    var concealmentPercent = softPhoneModules.concealmentPercent;
    var clampSignalingRegion = softPhoneModules.clampSignalingRegion;
    var describeSignalingRegion = softPhoneModules.describeSignalingRegion;
    var resolveSignalingRegion = softPhoneModules.resolveSignalingRegion;
    var withSignalingRegion = softPhoneModules.withSignalingRegion;
    var describeProviderWarning = softPhoneModules.describeProviderWarning;
    var classifyProviderWarning = softPhoneModules.classifyProviderWarning;

    var mapTelnyxOutboundState = softPhoneModules.mapTelnyxOutboundState;
    var isTelnyxTerminalState = softPhoneModules.isTelnyxTerminalState;
    var iceServersIncludeTurn = softPhoneModules.iceServersIncludeTurn;

    var reconnectDelayMs = softPhoneModules.reconnectDelayMs;

    var createHoldAudioController = softPhoneModules.createHoldAudioController;

    var rememberAgentHold = softPhoneModules.rememberAgentHold;
    var pruneAgentHolds = softPhoneModules.pruneAgentHolds;
    var isAgentHeld = softPhoneModules.isAgentHeld;
    var reconcileAgentHold = softPhoneModules.reconcileAgentHold;
    var resolveReportedState = softPhoneModules.resolveReportedState;
    var holdCommandRoute = softPhoneModules.holdCommandRoute;

    var readOfferLegTag = softPhoneModules.readOfferLegTag;
    var classifyOfferLeg = softPhoneModules.classifyOfferLeg;
    var rememberAcceptedOffer = softPhoneModules.rememberAcceptedOffer;
    var forgetAcceptedOffer = softPhoneModules.forgetAcceptedOffer;
    var OFFER_LEG_CAPABILITY = softPhoneModules.OFFER_LEG_CAPABILITY;

    // Must match the CrestApps.OrchardCore.Telephony.Models.TelephonyCapabilities flags enum.
    var CAPABILITIES = {
        Dial: 1,
        Hangup: 1 << 1,
        Hold: 1 << 2,
        Resume: 1 << 3,
        Mute: 1 << 4,
        Transfer: 1 << 5,
        Merge: 1 << 6,
        SendDigits: 1 << 7,
        ReceiveCalls: 1 << 8,
        Voicemail: 1 << 9,
        Directory: 1 << 10
    };

    var AUDIO_MODES = {
        None: 0,
        Browser: 1,
        ExternalDevice: 2
    };

    // The widget config renders the audio mode as its numeric enum value, but the SignalR hub serializes the
    // same enum by name (for example "Browser"). Normalize both forms to the numeric value before comparing.
    function normalizeAudioMode(value) {
        if (typeof value === 'number') {
            return value;
        }

        if (typeof value === 'string') {
            if (Object.prototype.hasOwnProperty.call(AUDIO_MODES, value)) {
                return AUDIO_MODES[value];
            }

            var parsed = parseInt(value, 10);

            return isNaN(parsed) ? -1 : parsed;
        }

        return -1;
    }

    var normalizeState = window.telephonyClient.normalizeCallState;

    function isActive(stateName) {
        return stateName === 'Connecting' || stateName === 'Ringing' || stateName === 'Connected' || stateName === 'OnHold';
    }

    function parseConfig(rootElement) {
        var raw = rootElement.getAttribute('data-config');

        if (!raw) {
            return { hubUrl: '', capabilities: 0, strings: {} };
        }

        try {
            return JSON.parse(raw);
        } catch (e) {
            return { hubUrl: '', capabilities: 0, strings: {} };
        }
    }

    var escapeHtml = window.telephonyClient.escapeHtml;

    function buildRegistrationConfigUrl(config) {
        if (config.registrationConfigUrl) {
            return config.registrationConfigUrl;
        }

        var parts = window.location.pathname.split('/').filter(function (part) {
            return !!part;
        });
        var adminPrefix = parts.length ? parts[0] : 'Admin';

        return '/' + adminPrefix + '/contact-center/agent/soft-phone/registration-config';
    }

    function fetchRegistrationConfig(config) {
        return fetch(buildRegistrationConfigUrl(config), {
            method: 'GET',
            credentials: 'same-origin',
            headers: {
                Accept: 'application/json'
            }
        }).then(function (response) {
            if (!response.ok) {
                throw new Error('The browser media registration configuration is unavailable.');
            }

            return response.json();
        });
    }

    function createRemoteStreamSink(setRemoteStream) {
        var remoteStream = new MediaStream();

        return {
            stream: remoteStream,
            addTrack: function (track) {
                remoteStream.addTrack(track);
                setRemoteStream(remoteStream);
            },
            clear: function () {
                remoteStream.getTracks().forEach(function (track) {
                    remoteStream.removeTrack(track);
                    track.stop();
                });
                setRemoteStream(null);
            }
        };
    }

    function createBrowserMediaAdapterRegistry(rootElement, config) {
        var adapters = {};

        /*
         * IBrowserMediaAdapter contract:
         *   adapter(context) -> Promise/session
         *   context: { config, credentials, localStream, remoteAudioElement, setRemoteStream, showError }
         *   session: { handleCallState(call), dispose() }
         *
         * The registry is intentionally scoped to this soft-phone instance/page. Providers add server
         * contributors through shell DI; the browser does not expose a global adapter registry. A provider
         * that ships its own browser media stack registers it on the instance through
         * `registerMediaAdapter`, so one page can host adapters from different providers without a
         * process-wide registry that any script could silently overwrite.
         */
        adapters.sipjs = createSipJsBrowserMediaAdapter(rootElement, config);
        adapters['telnyx-webrtc'] = createTelnyxBrowserMediaAdapter(rootElement, config);

        return adapters;
    }

    function createSipJsBrowserMediaAdapter(rootElement, widgetConfig) {
        return function (context) {
            var sip = window.SIP;

            if (!sip || typeof sip.UserAgent !== 'function') {
                return Promise.reject(new Error('SIP.js is required for the configured browser audio adapter.'));
            }

            return fetchRegistrationConfig(widgetConfig).then(function (registrationConfig) {
                return createSipJsSession(sip, context, registrationConfig);
            });
        };
    }

    function createSipJsSession(sip, context, registrationConfig) {
        var signaling = registrationConfig.signaling || {};
        var credential = registrationConfig.credential || {};
        var ice = registrationConfig.ice || {};
        var media = registrationConfig.media || {};
        var remoteSink = createRemoteStreamSink(context.setRemoteStream);
        var peerConnection = null;
        var activeSession = null;
        var registerer = null;
        var disposed = false;

        if (!signaling.webSocketUrl || !signaling.sipUri || !signaling.authorizationUser || !credential.value) {
            return Promise.reject(new Error('The browser media registration configuration is incomplete.'));
        }

        function getSessionDescriptionHandler(session) {
            return session && session.sessionDescriptionHandler
                ? session.sessionDescriptionHandler
                : null;
        }

        function attachPeerConnection(session) {
            var handler = getSessionDescriptionHandler(session);

            if (!handler || !handler.peerConnection || peerConnection === handler.peerConnection) {
                return;
            }

            peerConnection = handler.peerConnection;
            context.localStream.getTracks().forEach(function (track) {
                var alreadyAdded = peerConnection.getSenders().some(function (sender) {
                    return sender.track === track;
                });

                if (!alreadyAdded) {
                    peerConnection.addTrack(track, context.localStream);
                }
            });

            peerConnection.getReceivers().forEach(function (receiver) {
                if (receiver.track) {
                    remoteSink.addTrack(receiver.track);
                }
            });
            peerConnection.addEventListener('track', function (event) {
                if (event.track) {
                    remoteSink.addTrack(event.track);
                }
            });
        }

        function wireSession(session) {
            activeSession = session;
            attachPeerConnection(session);

            if (session.stateChange && typeof session.stateChange.addListener === 'function') {
                session.stateChange.addListener(function () {
                    attachPeerConnection(session);
                });
            }
        }

        function setMicrophoneEnabled(enabled) {
            context.localStream.getAudioTracks().forEach(function (track) {
                track.enabled = enabled;
            });
        }

        function requestHold(hold) {
            if (!activeSession || typeof activeSession.invite !== 'function') {
                return Promise.resolve();
            }

            var modifiers = hold && sip.Web && sip.Web.holdModifier
                ? [sip.Web.holdModifier]
                : [];

            return Promise.resolve(activeSession.invite({ requestDelegate: {}, sessionDescriptionHandlerModifiers: modifiers })).catch(function () { });
        }

        function terminateSession() {
            if (!activeSession) {
                return Promise.resolve();
            }

            if (typeof activeSession.bye === 'function') {
                return Promise.resolve(activeSession.bye()).catch(function () { });
            }

            if (typeof activeSession.dispose === 'function') {
                return Promise.resolve(activeSession.dispose()).catch(function () { });
            }

            return Promise.resolve();
        }

        var userAgent = new sip.UserAgent({
            uri: sip.UserAgent.makeURI(signaling.sipUri),
            displayName: signaling.displayName || '',
            authorizationUsername: signaling.authorizationUser,
            authorizationPassword: credential.value,
            transportOptions: {
                server: signaling.webSocketUrl
            },
            sessionDescriptionHandlerFactoryOptions: {
                constraints: {
                    audio: true,
                    video: false
                },
                peerConnectionConfiguration: {
                    iceServers: ice.iceServers || [],
                    iceTransportPolicy: ice.iceTransportPolicy || 'all'
                }
            },
            delegate: {
                onInvite: function (invitation) {
                    wireSession(invitation);
                    Promise.resolve(invitation.accept({
                        sessionDescriptionHandlerOptions: {
                            constraints: {
                                audio: true,
                                video: false
                            }
                        }
                    })).then(function () {
                        attachPeerConnection(invitation);
                    }).catch(function (error) {
                        context.showError(error && error.message ? error.message : String(error));
                    });
                }
            }
        });

        registerer = new sip.Registerer(userAgent, {
            expires: Math.max(30, Math.floor((Date.parse(credential.expiresAtUtc) - Date.now()) / 1000))
        });

        return userAgent.start().then(function () {
            return registerer.register();
        }).then(function () {
            return {
                providerConfig: registrationConfig,
                mediaCodecs: media.codecs || [],
                // Whether the browser places its own outbound calls (Telnyx) instead of the server originating
                // a leg to this registered client.
                canOriginate: !!registrationConfig.clientOriginatesCalls,
                outboundCallerId: registrationConfig.outboundCallerId || '',
                // Places an outbound call from the registered browser client and returns a controller. The
                // caller id, when supplied, is presented as the SIP P-Asserted-Identity (required by Telnyx).
                // onState receives soft-phone state names: 'Ringing', 'Connected', 'Disconnected'.
                originate: function (destination, callerId, onState) {
                    var notify = typeof onState === 'function' ? onState : function () { };

                    if (disposed) {
                        notify('Disconnected');

                        return null;
                    }

                    var atIndex = (signaling.sipUri || '').indexOf('@');
                    var domain = atIndex >= 0
                        ? signaling.sipUri.substring(atIndex + 1).replace(/[;>].*$/, '')
                        : 'sip.telnyx.com';
                    var targetUri = sip.UserAgent.makeURI('sip:' + destination + '@' + domain);

                    if (!targetUri) {
                        notify('Disconnected');

                        return null;
                    }

                    var extraHeaders = [];

                    if (callerId) {
                        extraHeaders.push('P-Asserted-Identity: <sip:' + callerId + '@' + domain + '>');
                    }

                    var inviter = new sip.Inviter(userAgent, targetUri, {
                        // Early media is intentionally off: negotiating media on a provisional (183) response
                        // caused the session to terminate mid-ring. Media is set up from the 200 OK on answer.
                        earlyMedia: false,
                        sessionDescriptionHandlerOptions: {
                            constraints: { audio: true, video: false }
                        },
                        extraHeaders: extraHeaders
                    });

                    wireSession(inviter);

                    inviter.stateChange.addListener(function (state) {
                        if (state === 'Established') {
                            setMicrophoneEnabled(true);
                            notify('Connected');
                        } else if (state === 'Terminating' || state === 'Terminated') {
                            notify('Disconnected');
                        }
                    });

                    Promise.resolve(inviter.invite({
                        requestDelegate: {
                            onProgress: function () { notify('Ringing'); },
                            onReject: function () { notify('Disconnected'); }
                        }
                    })).catch(function () {
                        notify('Disconnected');
                    });

                    return {
                        terminate: function () {
                            var currentState = inviter.state;

                            // An established call is ended with BYE; an INVITE that has not been answered yet
                            // must be cancelled (CANCEL), which BYE cannot do.
                            if (currentState === 'Established') {
                                return Promise.resolve(inviter.bye()).catch(function () { });
                            }

                            if (currentState === 'Initial' || currentState === 'Establishing') {
                                return Promise.resolve(inviter.cancel()).catch(function () { });
                            }

                            if (typeof inviter.dispose === 'function') {
                                return Promise.resolve(inviter.dispose()).catch(function () { });
                            }

                            return Promise.resolve();
                        },
                        setHold: function (hold) { return requestHold(hold); },
                        setMute: function (mute) { setMicrophoneEnabled(!mute); return Promise.resolve(); }
                    };
                },
                handleCallState: function (call) {
                    var stateName = normalizeState(call && call.state);

                    if (stateName === 'Disconnected' || stateName === 'Failed' || !call) {
                        return terminateSession();
                    }

                    setMicrophoneEnabled(stateName === 'Connected' && !call.isMuted);

                    if (stateName === 'OnHold') {
                        return requestHold(true);
                    }

                    if (stateName === 'Connected') {
                        return requestHold(false);
                    }

                    return Promise.resolve();
                },
                dispose: function () {
                    if (disposed) {
                        return Promise.resolve();
                    }

                    disposed = true;
                    remoteSink.clear();

                    return terminateSession()
                        .then(function () {
                            return registerer ? registerer.unregister().catch(function () { }) : null;
                        })
                        .then(function () {
                            return userAgent.stop().catch(function () { });
                        });
                }
            };
        });
    }

    // Determines whether a Telnyx WebRTC error is a benign hang-up failure. When the local side ends a call
    // the SDK tries to send a SIP BYE; if the socket or the remote leg has already gone away that send fails
    // with BYE_SEND_FAILED ("Failed to hang up cleanly"). The call is ending regardless, so this is noise
    // rather than a fault the agent needs to see.
    function isBenignHangupError(error) {
        if (!error) {
            return false;
        }

        var code = error.code || error.name || '';
        var message = error.message || error.error || (typeof error === 'string' ? error : '');
        var haystack = (String(code) + ' ' + String(message)).toUpperCase();

        return haystack.indexOf('BYE_SEND_FAILED') !== -1 || haystack.indexOf('HANG UP CLEANLY') !== -1;
    }

    // Maps configured codec names (for example "opus", "G722", "PCMU") to the { mimeType } shape the Telnyx SDK's
    // preferred_codecs call option expects, which it applies with RTCRtpTransceiver.setCodecPreferences. This
    // only reorders the browser's SDP OFFER; the Telnyx gateway's answer still picks the final codec, so a codec
    // Telnyx does not support on the media path is not negotiated regardless. In practice Telnyx transcodes the
    // WebRTC<->SIP/PSTN/conference legs to G711/G722, so Opus is not negotiated even though the browser offers it;
    // this hook is here so an admin can influence ordering (and so Opus is used automatically if Telnyx ever
    // enables it on the path). Item 2's quality telemetry reports the codec actually negotiated.
    function buildPreferredCodecs(codecs) {
        if (!Array.isArray(codecs) || codecs.length === 0) {
            return null;
        }

        // setCodecPreferences requires full RTCRtpCodec descriptors (mimeType AND clockRate, plus channels for
        // stereo codecs); a bare { mimeType } throws "Required member is undefined" and aborts the call. So build
        // the preference from the browser's real audio codec capabilities, which carry those members. If the
        // browser cannot report capabilities, return null and let the SDK/browser default ordering stand.
        if (typeof RTCRtpSender === 'undefined' || typeof RTCRtpSender.getCapabilities !== 'function') {
            return null;
        }

        var capabilities = RTCRtpSender.getCapabilities('audio');
        var available = (capabilities && capabilities.codecs) || [];

        if (!available.length) {
            return null;
        }

        var ordered = [];

        // Add the configured codecs first, in preference order, matched case-insensitively against the real
        // capabilities (each match is a valid RTCRtpCodec).
        codecs.forEach(function (codec) {
            var name = String(codec || '').trim();

            if (!name) {
                return;
            }

            var mimeType = (/^(audio|video)\//i.test(name) ? name : 'audio/' + name).toLowerCase();

            available.forEach(function (capability) {
                if (capability.mimeType && capability.mimeType.toLowerCase() === mimeType &&
                    ordered.indexOf(capability) === -1) {
                    ordered.push(capability);
                }
            });
        });

        // Append every remaining capability so this only REORDERS the offer and never removes a codec the peer
        // needs (for example telephone-event for DTMF or comfort noise).
        available.forEach(function (capability) {
            if (ordered.indexOf(capability) === -1) {
                ordered.push(capability);
            }
        });

        return ordered.length ? ordered : null;
    }

    function createTelnyxBrowserMediaAdapter(rootElement, widgetConfig) {
        return function (context) {
            var telnyx = window.TelnyxWebRTC;

            if (!telnyx || typeof telnyx.TelnyxRTC !== 'function') {
                return Promise.reject(new Error('The Telnyx WebRTC SDK is required for the configured browser audio adapter.'));
            }

            return fetchRegistrationConfig(widgetConfig).then(function (registrationConfig) {
                return createTelnyxSession(telnyx, context, registrationConfig);
            });
        };
    }

    function createTelnyxSession(telnyx, context, registrationConfig) {
        var signaling = registrationConfig.signaling || {};
        var credential = registrationConfig.credential || {};
        var ice = registrationConfig.ice || {};
        var media = registrationConfig.media || {};
        // Codec preference applied to both outbound newCall and inbound answer (see buildPreferredCodecs).
        var preferredCodecs = buildPreferredCodecs(media.codecs);
        var remoteElement = context.remoteAudioElement;

        // Hold audio. Telnyx delivers the caller's audio to this browser, so call.hold() puts the media leg
        // inactive and the caller hears silence. Instead, keep the call up and swap the microphone track on the
        // outbound sender for a hold-audio track (a configured URL, else a quiet comfort tone) so the caller
        // keeps hearing something. The controller is built lazily from the media config; when it or the peer
        // connection is unavailable, applyHold falls back to the provider's native hold.
        var holdAudio = typeof createHoldAudioController === 'function'
            ? createHoldAudioController({ mediaUrl: media.holdMusicUrl || registrationConfig.holdMusicUrl || '' })
            : null;

        function setRemoteAudioMuted(muted) {
            if (remoteElement) {
                try { remoteElement.muted = !!muted; } catch (error) { /* best effort */ }
            }
        }

        // Put a live call on or off hold with browser-side hold audio, falling back to native hold when there is
        // no controller or no reachable peer connection. On hold the agent's remote audio is muted too, so the
        // parked caller is fully isolated (agent hears the hold silence, caller hears the hold audio).
        function applyHold(call, hold) {
            var peerConnection = call && call.peer && call.peer.instance;

            if (!holdAudio || !peerConnection) {
                return Promise.resolve(hold ? call.hold() : call.unhold()).catch(function () { });
            }

            if (hold) {
                var micTrack = context.localStream && typeof context.localStream.getAudioTracks === 'function'
                    ? context.localStream.getAudioTracks()[0]
                    : null;

                setRemoteAudioMuted(true);

                return holdAudio.engage(peerConnection, micTrack).catch(function () {
                    // Media swap failed; keep the caller from hearing the agent by falling back to native hold.
                    setRemoteAudioMuted(false);

                    return Promise.resolve(call.hold()).catch(function () { });
                });
            }

            return holdAudio.release(peerConnection).then(function () {
                setRemoteAudioMuted(false);
            }).catch(function () {
                setRemoteAudioMuted(false);

                return Promise.resolve(call.unhold()).catch(function () { });
            });
        }

        // Tear the hold audio down when a call ends. Without this, a call that ended while on hold leaves the
        // comfort-tone AudioContext running and the agent's remote audio muted, which is one way a soft phone
        // gets stuck looking held after the call is already gone. Safe to call unconditionally.
        function endHoldAudio() {
            if (holdAudio && holdAudio.isEngaged()) {
                Promise.resolve(holdAudio.release()).catch(function () { });
            }

            setRemoteAudioMuted(false);
        }

        // Telnyx logs in with the telephony-credential SIP username/password, delivered in the same
        // registration config the SIP.js adapter consumes (authorizationUser + credential.value). The SDK
        // speaks Verto to Telnyx's own WebRTC gateway, so it manages the peer connection, media, and the
        // rtcp-mux/SDP details internally; no SDP workaround is needed on this path.
        var login = signaling.authorizationUser;
        var password = credential.value;

        if (!login || !password) {
            return Promise.reject(new Error('The browser media registration configuration is incomplete.'));
        }

        var currentCall = null;
        // Whether the current call was placed by this browser rather than bridged to it by the platform. A
        // browser-originated call has no platform interaction behind it, so platform call state that mentions no
        // call says nothing about it and must not end it.
        var currentCallIsBrowserOriginated = false;
        // The active outbound call's state callback, set by originate() and cleared when that call ends.
        var outboundNotify = null;
        // An inbound leg that is ringing but has not been answered yet (a direct extension call). It is surfaced
        // to the soft-phone core as an Answer/Decline prompt; until the agent chooses it is not the currentCall.
        var inboundRingingCall = null;
        // Legs the platform rang for a Contact Center offer that is still ringing on screen, handed to the core to
        // hold (unanswered, and not rung as a call of their own) until the agent accepts or declines the offer.
        var heldOfferCalls = [];
        var disposed = false;

        // Media-quality sampler state for the active call. A getStats sample is taken every few seconds while a
        // call is active; the samples feed a rolling summary, a poor-connection signal for the degraded-state
        // UX, and periodic + end-of-call reports to the server. Cleared when the call ends or the session is
        // disposed. This is the early-warning system that would have caught the recent TURN/one-way-audio bug.
        var QUALITY_SAMPLE_INTERVAL_MS = 8000;
        // Only transmit every Nth periodic sample to the server (plus every poor-state change and the final
        // summary), so an 8s sampling cadence does not become 8s of hub traffic per call.
        var QUALITY_TRANSMIT_EVERY = 3;
        var qualityTimer = null;
        var qualityState = null;

        // Loudness probes for the two directions, measured in the browser rather than read out of getStats.
        // They cover what getStats cannot: how loud the far end actually is (no browser reports it), and the
        // capture level in browsers that do not implement RTCAudioSourceStats.
        var captureProbe = null;
        // The id of the track the capture probe was built on, kept even when the build failed so a browser
        // that refuses the graph is not asked again every sample -- only when the track changes.
        var captureProbeTrackId = null;
        var captureProbeTrack = null;
        var inboundProbe = null;
        var inboundProbeTrackId = null;

        // The audio track the soft phone is receiving: whatever the live receiver carries, else the remote
        // element's stream. Like the sender, the receiver is the authority: the provider SDK can replace the
        // stream on the remote element during a call, and a probe left on the first one read 0.000 for the rest of
        // the call while the agent could hear the caller.
        function currentReceiveTrack(peer) {
            if (peer && typeof peer.getReceivers === 'function') {
                var receiver = peer.getReceivers().filter(function (candidate) {
                    return candidate && candidate.track && candidate.track.kind === 'audio';
                })[0];

                if (receiver) {
                    return receiver.track;
                }
            }

            var remoteStream = remoteElement && remoteElement.srcObject;

            return remoteStream && typeof remoteStream.getAudioTracks === 'function'
                ? remoteStream.getAudioTracks()[0] || null
                : null;
        }

        function startInboundProbe(track) {
            if (inboundProbe) {
                inboundProbe.dispose();
                inboundProbe = null;
            }

            inboundProbeTrackId = track ? track.id : null;

            if (!track) {
                return;
            }

            inboundProbe = createLevelProbe(typeof MediaStream === 'function'
                ? new MediaStream([track])
                : remoteElement && remoteElement.srcObject);
        }

        // The audio track the far end is hearing: whatever the live sender carries, else the soft phone's own
        // send stream. The sender is the authority because the provider SDK may be sending a track other than
        // the one it was handed, and a probe on the wrong track measures a microphone nobody hears.
        function currentSendTrack(peer) {
            if (peer && typeof peer.getSenders === 'function') {
                var sender = findAudioSender(peer.getSenders());

                if (sender && sender.track) {
                    return sender.track;
                }
            }

            return context.localStream && typeof context.localStream.getAudioTracks === 'function'
                ? context.localStream.getAudioTracks()[0] || null
                : null;
        }

        // (Re)builds the capture probe on one specific track. The probe is given a stream holding that track
        // alone rather than the shared send stream, so what it binds to is exactly the track named here: a
        // MediaStreamAudioSourceNode takes its stream's track once, at construction, and a stream that briefly
        // holds both the old and the new track mid-swap would leave it to the browser which one it picked.
        function startCaptureProbe(track) {
            if (captureProbe) {
                captureProbe.dispose();
                captureProbe = null;
            }

            captureProbeTrackId = track ? track.id : null;
            captureProbeTrack = track || null;

            if (!track) {
                return;
            }

            var stream = typeof MediaStream === 'function' ? new MediaStream([track]) : context.localStream;

            captureProbe = createLevelProbe(stream);
        }

        function startLevelProbes() {
            stopLevelProbes();

            var call = qualityState && qualityState.call;

            startCaptureProbe(currentSendTrack(call && call.peer && call.peer.instance));
            startInboundProbe(currentReceiveTrack(call && call.peer && call.peer.instance));
        }

        // Points the capture probe at the track now being sent, after the soft phone swapped its microphone
        // mid-call. Called once the swap is committed -- the new track on the sender AND the old one stopped --
        // never from inside the sender swap itself: rebuilding there bound the probe to the send stream while it
        // still held the old track, which the commit then stopped, and the probe read 0.000 for the rest of the
        // call while the far end measured the agent at around 0.3. Leaves the inbound probe alone; the far end
        // did not change.
        function refreshCaptureProbe() {
            if (!qualityState) {
                return;
            }

            var call = qualityState.call;

            startCaptureProbe(currentSendTrack(call && call.peer && call.peer.instance));
        }

        function stopLevelProbes() {
            if (captureProbe) {
                captureProbe.dispose();
                captureProbe = null;
            }

            captureProbeTrackId = null;
            captureProbeTrack = null;

            if (inboundProbe) {
                inboundProbe.dispose();
                inboundProbe = null;
            }

            inboundProbeTrackId = null;
        }

        // Records what was negotiated the moment media is up: the audio m= line and codec maps of both the offer
        // Telnyx sent and the answer the browser gave (or vice versa), the direction, and the provider's leg ids.
        // Every call so far had its codec inferred after the fact from an RTP statistic; nothing ever recorded
        // which codecs the far side OFFERED, so "why G722 and not Opus" could not be answered from any log.
        function reportNegotiation(call) {
            if (typeof context.onNegotiated !== 'function') {
                return;
            }

            try {
                var peer = call && call.peer && call.peer.instance;

                if (!peer) {
                    return;
                }

                var audioLines = function (description) {
                    if (!description || !description.sdp) {
                        return '(none)';
                    }

                    var lines = description.sdp.split(/\r?\n/);
                    var kept = [];

                    for (var i = 0; i < lines.length; i++) {
                        if (lines[i].indexOf('m=audio') === 0 || /^a=(rtpmap|fmtp|ptime|maxptime):/.test(lines[i])) {
                            kept.push(lines[i]);
                        }
                    }

                    return kept.join(' ; ');
                };

                var options = call.options || {};
                var text = 'direction=' + (call.direction || '') +
                    ' callId=' + (call.id || '') +
                    ' ccid=' + (options.telnyxCallControlId || '') +
                    ' leg=' + (options.telnyxLegId || '') +
                    ' | remote(' + ((peer.remoteDescription && peer.remoteDescription.type) || '?') + '): ' + audioLines(peer.remoteDescription) +
                    ' | local(' + ((peer.localDescription && peer.localDescription.type) || '?') + '): ' + audioLines(peer.localDescription);

                context.onNegotiated(text);
            } catch (error) { /* diagnostics only */ }
        }

        // Asks the browser to hold less incoming audio before playing it, when the agent (or the operator) has
        // chosen a shorter hold. Applied when media goes live and again whenever the choice changes, since the
        // receiver is only there once the call is up.
        function applyPlayoutDelayToCall(call) {
            if (typeof context.readPlayoutDelay !== 'function') {
                return;
            }

            var seconds = context.readPlayoutDelay();
            var peer = call && call.peer && call.peer.instance;

            if (!peer) {
                return;
            }

            var applied = applyPlayoutDelay(peer, seconds);

            if (applied === 0 && seconds >= 0 && typeof context.onPlayoutDelayUnsupported === 'function') {
                context.onPlayoutDelayUnsupported();
            }
        }

        function startQualitySampler(call) {
            // Already sampling this call, or nothing to sample.
            if (!call || (qualityState && qualityState.call === call)) {
                return;
            }

            stopQualitySampler(false);

            qualityState = {
                call: call,
                callId: (call && (call.id || call.callId)) || '',
                started: Date.now(),
                samples: 0,
                transmitCounter: 0,
                mosSum: 0,
                minMos: Infinity,
                maxLoss: 0,
                lastPacketsReceived: 0,
                lastPacketsLost: 0,
                lastPoor: false,
                finalSent: false,
                // Capture-side state. minMicLevel starts at Infinity so the first sample sets it, and is
                // reported on the summary: it answers "was this agent audible for the whole call?" from the
                // server log alone, which nothing could before.
                lastAudioEnergy: 0,
                // The previous media-source counters, so the reported microphone level covers the window
                // since the last sample rather than one instant of it.
                lastMediaSource: null,
                silentSamples: 0,
                minMicLevel: Infinity,
                captureAlerted: false,
                // The previous raw inbound stats, kept so the jitter-buffer delay can be read over the window
                // since the last sample rather than averaged across the whole call, which would flatten a
                // buffer that only grew late.
                lastInbound: null,
                maxJitterBufferMs: -1,
                last: null
            };

            startLevelProbes();

            qualityTimer = window.setInterval(sampleQuality, QUALITY_SAMPLE_INTERVAL_MS);
        }

        function sampleQuality() {
            if (!qualityState) {
                return;
            }

            var call = qualityState.call;
            var peer = call && call.peer && call.peer.instance;

            if (!peer || typeof peer.getStats !== 'function') {
                return;
            }

            Promise.resolve(peer.getStats()).then(function (report) {
                if (!qualityState || qualityState.call !== call || !report || typeof report.forEach !== 'function') {
                    return;
                }

                var parsed = parseWebRtcStats(report);
                var inbound = parsed.inbound;

                if (!inbound) {
                    return;
                }

                var packetsReceived = inbound.packetsReceived || 0;
                var packetsLost = inbound.packetsLost || 0;
                var deltaReceived = Math.max(0, packetsReceived - qualityState.lastPacketsReceived);
                var deltaLost = Math.max(0, packetsLost - qualityState.lastPacketsLost);
                var lossPercent = (deltaReceived + deltaLost) > 0
                    ? (deltaLost / (deltaReceived + deltaLost)) * 100
                    : 0;
                var jitterMs = (inbound.jitter || 0) * 1000;
                var rttMs = parsed.pair && typeof parsed.pair.currentRoundTripTime === 'number'
                    ? parsed.pair.currentRoundTripTime * 1000
                    : (parsed.remoteInbound && typeof parsed.remoteInbound.roundTripTime === 'number'
                        ? parsed.remoteInbound.roundTripTime * 1000
                        : 0);
                // Which statistic the round trip came from. A candidate-pair value is a STUN round trip over the
                // media path; the remote-inbound value is RTCP-derived. When one call reported a steady 2.8 s
                // with healthy jitter and loss, nothing recorded which of the two had said so.
                var rttSource = parsed.pair && typeof parsed.pair.currentRoundTripTime === 'number'
                    ? 'candidate-pair'
                    : (parsed.remoteInbound && typeof parsed.remoteInbound.roundTripTime === 'number'
                        ? 'remote-inbound-rtp'
                        : 'none');

                // What Telnyx reports RECEIVING from this browser: the only far-end-side view of the agent's own
                // audio that the browser can see. Loss and jitter here describe the direction the caller hears.
                var remoteInbound = parsed.remoteInbound;
                var remoteFractionLostPercent = remoteInbound && typeof remoteInbound.fractionLost === 'number'
                    ? remoteInbound.fractionLost * 100
                    : -1;
                var remoteJitterMs = remoteInbound && typeof remoteInbound.jitter === 'number'
                    ? remoteInbound.jitter * 1000
                    : -1;

                // The echo canceller's own view of the capture. A wired headset has almost no acoustic echo to
                // cancel, so a canceller that is nonetheless working hard on it is suppressing the agent's voice
                // -- a classic source of a hollow, distant sound that no level measurement can show.
                var echoStatsReported = !!parsed.mediaSource && typeof parsed.mediaSource.echoReturnLoss === 'number';
                var echoReturnLossDb = echoStatsReported ? parsed.mediaSource.echoReturnLoss : 0;
                var echoReturnLossEnhancementDb = echoStatsReported && typeof parsed.mediaSource.echoReturnLossEnhancement === 'number'
                    ? parsed.mediaSource.echoReturnLossEnhancement
                    : 0;

                // Which track the peer connection is actually sending, and whether it is the stream the soft phone
                // captured. The provider SDK is free to ignore the stream it is handed on the answer path and
                // capture its own -- on the browser's default device, with default constraints -- in which case
                // everything the soft phone measures and configures about "its" microphone describes a track
                // the far end never hears. Comparing track identity here is the only direct test of that.
                var sentTrack = null;

                if (typeof peer.getSenders === 'function') {
                    var senders = peer.getSenders();

                    for (var s = 0; s < senders.length; s++) {
                        if (senders[s].track && senders[s].track.kind === 'audio') {
                            sentTrack = senders[s].track;
                            break;
                        }
                    }
                }

                var localTrack = context.localStream && typeof context.localStream.getAudioTracks === 'function'
                    ? context.localStream.getAudioTracks()[0]
                    : null;
                var sentTrackReported = !!sentTrack;
                var sentTrackLabel = sentTrack ? (sentTrack.label || '') : '';
                var sentTrackIsLocalStream = !!(sentTrack && localTrack && sentTrack.id === localTrack.id);

                // Keep the capture probe on the track being sent. The swap paths rebuild it themselves once a
                // new microphone is committed, but anything that changes the send track without passing through
                // them -- the provider SDK swapping its own track, or a swap whose rebuild was lost -- would
                // otherwise leave the probe reading a stopped track as 0.000 for the rest of the call. The
                // window right after a rebuild has no frames yet and reports unknown (-1), not silence. While a
                // call is on hold the sender carries the comfort tone, so the probe follows it there and back:
                // the level reported is always that of what the far end is being sent.
                var probeTarget = sentTrack || localTrack;

                if (captureProbeNeedsRebuild(captureProbeTrackId,
                    probeTarget ? probeTarget.id : null,
                    probeTarget ? probeTarget.readyState : null)) {
                    startCaptureProbe(probeTarget);
                }

                // The provider's own identifiers for this leg, so a browser-side observation can be joined to the
                // server's webhook and command log for the same leg instead of being lined up by timestamp.
                var callOptions = (call && call.options) || {};
                var providerCallControlId = callOptions.telnyxCallControlId || '';
                var providerLegId = callOptions.telnyxLegId || '';
                var providerSessionId = callOptions.telnyxSessionId || '';
                var bytesReceived = inbound.bytesReceived || 0;
                var mos = estimateMos(rttMs, jitterMs, lossPercent);
                var poor = lossPercent > QUALITY_POOR_LOSS_PERCENT ||
                    mos < QUALITY_POOR_MOS ||
                    (bytesReceived === 0 && packetsReceived > 0);

                // The capture side. Everything above measures the direction the agent is listening to, so a call
                // where the caller cannot hear the agent produced a clean bill of health: no loss, low jitter, a
                // good MOS, and not one number describing the microphone.
                var mediaSource = parsed.mediaSource;
                // Whether the browser reported a capture at all. Reporting an absent measurement as 0 would
                // make "this browser does not expose media-source stats" indistinguishable from "this
                // microphone is delivering silence" -- the same conflation that made a dropped round-trip time
                // read as a perfect connection. So an unmeasured capture is -1, and nothing is concluded from
                // it.
                //
                // The level is the RMS across the window since the previous sample, from the cumulative energy
                // counters, not the instantaneous audioLevel: a single reading every eight seconds landed in a
                // pause between words often enough to log Mic=0.000 for an agent the far end could hear.
                var micLevel = windowedMicrophoneLevel(mediaSource, qualityState.lastMediaSource);
                var captureReported = micLevel >= 0;
                var audioEnergy = mediaSource && typeof mediaSource.totalAudioEnergy === 'number' ? mediaSource.totalAudioEnergy : 0;
                var energyDelta = audioEnergy - qualityState.lastAudioEnergy;
                var bytesSent = parsed.outbound && parsed.outbound.bytesSent ? parsed.outbound.bytesSent : 0;
                var packetsSent = parsed.outbound && parsed.outbound.packetsSent ? parsed.outbound.packetsSent : 0;

                // How long the browser held this audio before playing it. Added to the round-trip time, this is
                // the delay the two people on the call actually experience -- and it is the only one of the two
                // that a healthy network does nothing to keep small.
                var jitterBufferMs = readJitterBufferMs(inbound, qualityState.lastInbound);

                // The share of received audio the browser had to invent because a packet arrived after its
                // moment. It is the price of a shorter playout buffer, so it is reported beside the delay: a
                // buffer setting is only worth keeping while this stays near zero.
                var concealment = concealmentPercent(inbound, qualityState.lastInbound);

                // The far end may not have been attached when the call started, and the provider SDK may replace
                // it during the call; keep the inbound probe on the track actually being received.
                var receiveTrack = currentReceiveTrack(call && call.peer && call.peer.instance);

                if (captureProbeNeedsRebuild(inboundProbeTrackId,
                    receiveTrack ? receiveTrack.id : null,
                    receiveTrack ? receiveTrack.readyState : null)) {
                    startInboundProbe(receiveTrack);
                }

                // Measured loudness in both directions, on the same scale, whatever the browser reports.
                // "The caller sounds far away" is a statement about this number and about nothing else on the
                // report, which is why a call that sounded distant rated Good on every other measurement.
                var inboundLevel = probeLevel(inboundProbe);
                var captureProbeLevel = probeLevel(captureProbe);

                // Why the outgoing level reads what it does: the probe's audio engine state, then the probed
                // track's state and whether it is muted or disabled. An outgoing level of 0.000 on a call the far
                // end could hear is otherwise impossible to explain from the log.
                var captureProbeState = (captureProbe && typeof captureProbe.state === 'function' ? captureProbe.state() : 'none') +
                    '/' + (captureProbeTrack ? captureProbeTrack.readyState : 'no-track') +
                    (captureProbeTrack && captureProbeTrack.muted ? '/muted' : '') +
                    (captureProbeTrack && captureProbeTrack.enabled === false ? '/disabled' : '');

                // The format the microphone is delivering in. Read every sample rather than once, because a
                // Bluetooth headset switches profile when a call claims its microphone -- the value at
                // registration is not the value on the call.
                var captureSettings = typeof context.readCaptureSettings === 'function'
                    ? context.readCaptureSettings()
                    : null;

                qualityState.lastInbound = {
                    jitterBufferDelay: inbound.jitterBufferDelay,
                    jitterBufferEmittedCount: inbound.jitterBufferEmittedCount,
                    concealedSamples: inbound.concealedSamples,
                    totalSamplesReceived: inbound.totalSamplesReceived
                };

                if (jitterBufferMs >= 0) {
                    qualityState.maxJitterBufferMs = Math.max(qualityState.maxJitterBufferMs, jitterBufferMs);
                }

                qualityState.lastPacketsReceived = packetsReceived;
                qualityState.lastPacketsLost = packetsLost;
                qualityState.lastAudioEnergy = audioEnergy;
                qualityState.lastMediaSource = mediaSource
                    ? { totalAudioEnergy: mediaSource.totalAudioEnergy, totalSamplesDuration: mediaSource.totalSamplesDuration }
                    : null;
                qualityState.samples++;
                qualityState.mosSum += mos;
                qualityState.minMos = Math.min(qualityState.minMos, mos);
                qualityState.maxLoss = Math.max(qualityState.maxLoss, lossPercent);
                if (captureReported) {
                    qualityState.minMicLevel = Math.min(qualityState.minMicLevel, micLevel);
                }

                // A capture that delivers nothing across several consecutive samples is a dead microphone, not a
                // pause. Tell the agent once per call: they are on a call the caller cannot hear them on, and
                // today the only way they find out is the caller saying so.
                //
                // Only when the browser actually reported a capture. Warning an agent that their microphone is
                // dead because the measurement is missing would be worse than saying nothing: they would go
                // hunting a device that is working.
                //
                // Where the browser reports no capture statistics at all -- Firefox, where this call was
                // answered -- the probe stands in, so the agent is told about a dead microphone there too
                // instead of the check quietly never firing.
                var captureSilent = captureReported
                    ? isCaptureSilent(micLevel, energyDelta)
                    : (captureProbeLevel >= 0 && captureProbeLevel <= LEVEL_SILENT);

                if (captureSilent) {
                    qualityState.silentSamples++;
                } else {
                    qualityState.silentSamples = 0;
                }

                if (qualityState.silentSamples >= QUALITY_SILENT_MIC_SAMPLES && !qualityState.captureAlerted &&
                    typeof context.onCaptureSilent === 'function') {
                    qualityState.captureAlerted = true;
                    context.onCaptureSilent();
                }

                var sample = {
                    callId: qualityState.callId,
                    direction: (call && call.direction) || '',
                    codec: parsed.codec,
                    localCandidateType: parsed.localCandidateType,
                    remoteCandidateType: parsed.remoteCandidateType,
                    packetsReceived: packetsReceived,
                    packetsLost: packetsLost,
                    lossPercent: lossPercent,
                    jitterMs: jitterMs,
                    rttMs: rttMs,
                    bytesReceived: bytesReceived,
                    micLevel: micLevel,
                    captureReported: captureReported,
                    bytesSent: bytesSent,
                    packetsSent: packetsSent,
                    jitterBufferMs: jitterBufferMs,
                    concealmentPercent: concealment,
                    inboundLevel: inboundLevel,
                    captureProbeLevel: captureProbeLevel,
                    captureProbeState: captureProbeState,
                    sendCodec: parsed.sendCodec,
                    rttSource: rttSource,
                    remoteFractionLostPercent: remoteFractionLostPercent,
                    remoteJitterMs: remoteJitterMs,
                    echoStatsReported: echoStatsReported,
                    echoReturnLossDb: echoReturnLossDb,
                    echoReturnLossEnhancementDb: echoReturnLossEnhancementDb,
                    sentTrackReported: sentTrackReported,
                    sentTrackLabel: sentTrackLabel,
                    sentTrackIsLocalStream: sentTrackIsLocalStream,
                    providerCallControlId: providerCallControlId,
                    providerLegId: providerLegId,
                    providerSessionId: providerSessionId,
                    captureSampleRate: (captureSettings && captureSettings.sampleRate) || 0,
                    captureDevice: typeof context.captureDeviceLabel === 'function' ? context.captureDeviceLabel() : '',
                    captureProcessing: [
                        captureSettings && captureSettings.echoCancellation ? 'ec' : '',
                        captureSettings && captureSettings.noiseSuppression ? 'ns' : '',
                        captureSettings && captureSettings.autoGainControl ? 'agc' : '',
                        // The soft phone's own gain stage, when set: a caller's "louder now" or "distorted now"
                        // has to be readable against the boost that was active.
                        typeof context.captureBoostLabel === 'function' ? context.captureBoostLabel() : ''
                    ].filter(Boolean).join('+'),
                    mos: mos,
                    poor: poor
                };
                qualityState.last = sample;

                var poorChanged = poor !== qualityState.lastPoor;

                if (poorChanged) {
                    qualityState.lastPoor = poor;

                    if (typeof context.onConnectionQuality === 'function') {
                        context.onConnectionQuality(poor);
                    }
                }

                qualityState.transmitCounter++;

                if ((poorChanged || (qualityState.transmitCounter % QUALITY_TRANSMIT_EVERY) === 0) &&
                    typeof context.reportCallQuality === 'function') {
                    context.reportCallQuality(buildQualityPayload(sample, false));
                }
            }).catch(function () { /* best effort: a failed sample must never disrupt the call */ });
        }

        function buildQualityPayload(sample, isFinal) {
            var payload = {
                callId: sample.callId,
                direction: sample.direction,
                codec: sample.codec,
                localCandidateType: sample.localCandidateType,
                remoteCandidateType: sample.remoteCandidateType,
                packetsReceived: sample.packetsReceived,
                packetsLost: sample.packetsLost,
                lossPercent: sample.lossPercent,
                jitterMs: sample.jitterMs,
                // The server's contract calls this RoundTripTimeMs. Sending it as "rttMs" bound to nothing, so
                // every sample arrived with a round-trip time of zero while the MOS beside it had been computed
                // from the real one -- a call logged as "Poor" with 0ms round trip and no packet loss, which
                // reads as a scoring bug and hides the actual half-second latency behind it.
                roundTripTimeMs: sample.rttMs,
                bytesReceived: sample.bytesReceived,
                // The capture side, so a call the caller could not hear is visible in the server log rather than
                // only in what the caller says afterwards. A microphone level of -1 with captureReported false
                // means the browser exposed no capture stats: unknown, not silent.
                microphoneLevel: sample.micLevel,
                captureReported: !!sample.captureReported,
                bytesSent: sample.bytesSent,
                packetsSent: sample.packetsSent,
                // The delay the browser itself adds on top of the network round trip, and the measured loudness
                // of each direction. All three are -1 when they could not be measured. Together they are what
                // separates "this call sounded bad" from "every number said the call was fine", which is the
                // gap the reports had until now.
                jitterBufferMs: sample.jitterBufferMs,
                // What a shorter playout buffer costs, if anything: audio the browser invented for packets that
                // arrived too late to play. -1 when the browser does not report it.
                concealmentPercent: sample.concealmentPercent,
                inboundLevel: sample.inboundLevel,
                captureProbeLevel: sample.captureProbeLevel,
                captureProbeState: sample.captureProbeState,
                // The capture format, so a call that measured perfectly and sounded wrong can be explained
                // from the server log instead of from a live diagnostics panel nobody had open at the time.
                captureSampleRate: sample.captureSampleRate,
                captureDevice: sample.captureDevice,
                captureProcessing: sample.captureProcessing,
                // The direction the far end hears: the codec the browser sends, what Telnyx reports receiving,
                // the echo canceller's activity, and -- decisively -- whether the track being sent is the stream
                // the soft phone captured at all. Plus the provider's leg ids as the join key to the server log.
                sendCodec: sample.sendCodec,
                rttSource: sample.rttSource,
                remoteFractionLostPercent: sample.remoteFractionLostPercent,
                remoteJitterMs: sample.remoteJitterMs,
                echoStatsReported: !!sample.echoStatsReported,
                echoReturnLossDb: sample.echoReturnLossDb,
                echoReturnLossEnhancementDb: sample.echoReturnLossEnhancementDb,
                sentTrackReported: !!sample.sentTrackReported,
                sentTrackLabel: sample.sentTrackLabel,
                sentTrackIsLocalStream: !!sample.sentTrackIsLocalStream,
                providerCallControlId: sample.providerCallControlId,
                providerLegId: sample.providerLegId,
                providerSessionId: sample.providerSessionId,
                mos: sample.mos,
                poor: sample.poor,
                final: !!isFinal,
                sampleCount: qualityState ? qualityState.samples : 0,
                minMos: qualityState && isFinite(qualityState.minMos) ? qualityState.minMos : sample.mos,
                avgMos: qualityState && qualityState.samples ? qualityState.mosSum / qualityState.samples : sample.mos,
                maxLossPercent: qualityState ? qualityState.maxLoss : sample.lossPercent,
                minMicrophoneLevel: qualityState && isFinite(qualityState.minMicLevel) ? qualityState.minMicLevel : sample.micLevel,
                // The worst the playout delay got at any point, so a call that drifted into walkie-talkie
                // territory late is visible from the summary line alone rather than only from the samples.
                maxJitterBufferMs: qualityState ? qualityState.maxJitterBufferMs : sample.jitterBufferMs,
                durationMs: qualityState ? (Date.now() - qualityState.started) : 0
            };

            return payload;
        }

        function stopQualitySampler(sendFinal) {
            if (qualityTimer) {
                window.clearInterval(qualityTimer);
                qualityTimer = null;
            }

            if (!qualityState) {
                return;
            }

            // Report the end-of-call summary once, only when at least one sample was taken (a sub-sample-interval
            // call produced no measurements worth summarizing).
            if (sendFinal && !qualityState.finalSent && qualityState.samples > 0 && qualityState.last &&
                typeof context.reportCallQuality === 'function') {
                qualityState.finalSent = true;
                context.reportCallQuality(buildQualityPayload(qualityState.last, true));
            }

            // Clear any lingering poor-connection signal on the core when the call ends.
            if (qualityState.lastPoor && typeof context.onConnectionQuality === 'function') {
                context.onConnectionQuality(false);
            }

            // Release the audio graphs the probes hold. They outlive nothing: a probe left running would keep
            // an AudioContext (and, for the capture probe, a reference to the microphone) alive after the call.
            stopLevelProbes();

            qualityState = null;
        }

        var clientOptions = {
            login: login,
            password: password
        };

        // Only override the Telnyx SDK's built-in ICE servers when the provided set includes a TURN relay.
        // Passing a STUN-only list here REPLACES the SDK's defaults -- which include Telnyx's TURN servers -- and
        // a client behind a restrictive/symmetric NAT then has no relay to receive inbound media: it can send
        // audio but receives nothing (one-way audio), while a client on a permissive NAT still works via STUN.
        // With a STUN-only config we leave the SDK defaults in place so TURN relaying stays available.
        if (Array.isArray(ice.iceServers) && ice.iceServers.length > 0 && iceServersIncludeTurn(ice.iceServers)) {
            clientOptions.iceServers = ice.iceServers;
        }

        // Which of the provider's signaling edges this browser registers on: the agent's own choice, else the
        // tenant setting, else the provider's geo-routing. It can only be set as the client is built, which is
        // why changing it re-registers rather than taking effect on the next call. See signaling-region.js.
        var signalingRegion = resolveSignalingRegion(
            signaling.region,
            typeof context.readSignalingRegion === 'function' ? context.readSignalingRegion() : '');

        withSignalingRegion(clientOptions, signalingRegion);

        var client = new telnyx.TelnyxRTC(clientOptions);

        // Say which edge this registration landed on. Until now nothing did, so an agent on a distant edge had
        // no way to know that was what they were hearing -- and neither did anyone reading the call afterwards.
        if (typeof context.onSignalingRegion === 'function') {
            context.onSignalingRegion(signalingRegion, describeSignalingRegion(signalingRegion));
        }

        function clearCall(call) {
            if (currentCall === call) {
                currentCall = null;
                outboundNotify = null;
            }
        }

        // A single notification handler drives both directions:
        //  * inbound: the server only originates a leg to this registered credential after the agent has
        //    accepted the offer over SignalR, so answering the incoming call here mirrors the SIP.js
        //    passive-answer model;
        //  * outbound: relay the SDK call state to the originate() callback that owns the active call.
        // The Telnyx SDK attaches the remote media to the audio element, but the browser's autoplay policy can
        // block playback -- most notably on an auto-answered inbound call, which happens with no user gesture --
        // leaving the call connected (DTLS up) but silent. Force playback and make sure the element is audible
        // once the call is active. Best-effort: a rejected play() only means this particular attempt was blocked.
        function ensureRemotePlayback() {
            if (!remoteElement) {
                return;
            }

            try {
                remoteElement.muted = false;

                if (typeof remoteElement.volume === 'number') {
                    remoteElement.volume = 1;
                }

                Promise.resolve(remoteElement.play()).catch(function () { });
            } catch (error) { /* best effort */ }
        }

        // Answer an inbound call with the same media options the outbound newCall path uses. Passing the already
        // acquired microphone stream (localStream) plus audio:true is what makes the SDK negotiate a two-way
        // (sendrecv) audio answer and attach the remote audio to remoteElement; answering with only remoteElement
        // left the leg connected (DTLS up) but silent -- no media flowed in either direction.
        function answerInboundCall(call) {
            try {
                var answerOptions = {
                    localStream: context.localStream,
                    remoteElement: remoteElement,
                    audio: true,
                    video: false
                };

                if (preferredCodecs) {
                    answerOptions.preferred_codecs = preferredCodecs;
                }

                // The SDK's answer() copies only customHeaders and the media elements out of these options; the
                // localStream (and preferred_codecs) it is handed here are silently discarded, and its Peer then
                // calls getUserMedia itself -- {audio:true}, on the browser's default input, with default
                // constraints. So on every inbound call the track the far end heard was whatever Windows had as
                // its default microphone at the moment of answer, while the soft phone's device picker, mono
                // constraint, level meter and telemetry all described a different track. A quality line from a
                // live call read "Capture=... Microphone Array (Intel Smart Sound)" against "Sent=OTHER/Headset
                // Microphone (Realtek)": two devices, one of them a far-field room mic, with nothing on screen to
                // say which the caller was hearing.
                //
                // The Peer reads the stream from call.options, which answer() preserves (it merges into a copy
                // of the existing options). Setting it there, before answering, is the one place the SDK will
                // honour it -- and makes inbound calls send the same stream outbound calls already do.
                if (context.localStream && call.options && typeof call.options === 'object') {
                    call.options.localStream = context.localStream;
                }

                call.answer(answerOptions);
            } catch (error) {
                context.showError(error && error.message ? error.message : String(error));
            }
        }

        // The core's handle on a held offer leg. Answering it makes it the current call, exactly as the auto-answered
        // agent leg of an accepted offer always was; the platform joins it to the caller.
        function createOfferLegController(call) {
            return {
                legId: (call.options && call.options.telnyxCallControlId) || '',
                answer: function () {
                    var index = heldOfferCalls.indexOf(call);

                    if (index >= 0) {
                        heldOfferCalls.splice(index, 1);
                    }

                    if (disposed || isTelnyxTerminalState(call.state)) {
                        return;
                    }

                    currentCall = call;
                    outboundNotify = null;
                    answerInboundCall(call);
                },
                hangup: function () {
                    var index = heldOfferCalls.indexOf(call);

                    if (index >= 0) {
                        heldOfferCalls.splice(index, 1);
                    }

                    try {
                        Promise.resolve(call.hangup()).catch(function () { });
                    } catch (error) { /* best effort: the platform also hangs it up */ }
                }
            };
        }

        // Best-effort extraction of the calling party from an inbound Telnyx call, used to label the ring
        // prompt. The SDK surfaces it under a few names depending on version; a display name is preferred over
        // a raw number, and both fall back to empty so the core can show a generic "Incoming call".
        function extractCaller(call) {
            var options = (call && call.options) || {};
            var name = call.remoteCallerName || options.remoteCallerName || options.callerName || '';
            var number = call.remoteCallerNumber || options.remoteCallerNumber || options.callerNumber || call.callerNumber || '';

            return { name: name || '', number: number || '' };
        }

        client.on('telnyx.notification', function (notification) {
            if (!notification || notification.type !== 'callUpdate' || !notification.call) {
                return;
            }

            var call = notification.call;

            // A held offer leg that ends (the platform hung it up because the offer was declined or expired, or it
            // rang out) is reported so the core stops holding it.
            var heldOfferIndex = heldOfferCalls.indexOf(call);

            if (heldOfferIndex >= 0) {
                if (isTelnyxTerminalState(call.state)) {
                    heldOfferCalls.splice(heldOfferIndex, 1);

                    if (typeof context.onOfferLegEnded === 'function') {
                        context.onOfferLegEnded((call.options && call.options.telnyxCallControlId) || '');
                    }
                }

                return;
            }

            if (call.direction === 'inbound' && call.state === 'ringing' &&
                call !== currentCall && call !== inboundRingingCall && !disposed) {
                // The leg the platform rang for an offer that is still ringing on screen. It is the core's to hold
                // or answer, depending on whether the offer has been accepted; it never rings as a call of its own.
                var takeOfferLeg = typeof context.claimOfferLeg === 'function'
                    ? context.claimOfferLeg(call.options || {})
                    : null;

                if (typeof takeOfferLeg === 'function') {
                    heldOfferCalls.push(call);
                    takeOfferLeg(createOfferLegController(call));

                    return;
                }

                // The caller's own bridged leg (an extension call this browser just placed) and a Contact Center
                // leg it just accepted are inbound calls the agent is expecting, so they are answered
                // automatically. An unsolicited inbound leg -- a colleague dialing this agent's extension -- must
                // ring with an Answer/Decline prompt instead of connecting silently.
                var autoAnswer = typeof context.shouldAutoAnswerInbound !== 'function' ||
                    context.shouldAutoAnswerInbound();

                if (autoAnswer) {
                    currentCall = call;
                    answerInboundCall(call);

                    return;
                }

                inboundRingingCall = call;

                var caller = extractCaller(call);
                var controller = {
                    callerName: caller.name,
                    callerNumber: caller.number,
                    // Answer the ringing leg and, from here on, drive its state through onState just like an
                    // originated call (so the core sees Connected/Disconnected and can clean up).
                    answer: function (onState) {
                        currentCall = call;
                        inboundRingingCall = null;
                        outboundNotify = typeof onState === 'function' ? onState : null;
                        answerInboundCall(call);
                    },
                    // Decline before answer: hang up the ringing leg. Telnyx reports the destination-leg hangup to
                    // the server, whose no-answer handling can still route the caller to voicemail.
                    decline: function () {
                        inboundRingingCall = null;

                        try {
                            call.hangup();
                        } catch (error) { /* best effort */ }
                    },
                    terminate: function () {
                        endHoldAudio();

                        try {
                            return Promise.resolve(call.hangup()).catch(function () { });
                        } catch (error) {
                            return Promise.resolve();
                        }
                    },
                    setHold: function (hold) {
                        try {
                            return applyHold(call, hold);
                        } catch (error) {
                            return Promise.resolve();
                        }
                    },
                    setMute: function (mute) {
                        try {
                            if (mute) {
                                call.muteAudio();
                            } else {
                                call.unmuteAudio();
                            }
                        } catch (error) { /* best effort */ }

                        return Promise.resolve();
                    }
                };

                if (typeof context.onInboundRing === 'function') {
                    context.onInboundRing(controller);
                } else {
                    // No ring handler is wired: never leave a call silently unanswered -- answer it.
                    controller.answer();
                }

                return;
            }

            // The agent has not answered yet and the caller hung up (or the ring timed out): tell the core to
            // dismiss the Answer/Decline prompt.
            if (call === inboundRingingCall) {
                if (isTelnyxTerminalState(call.state)) {
                    inboundRingingCall = null;

                    if (typeof context.onInboundRingCanceled === 'function') {
                        context.onInboundRingCanceled();
                    }
                }

                return;
            }

            if (call === currentCall) {
                // Once media is flowing, make sure the remote audio is actually playing (see ensureRemotePlayback)
                // and begin sampling media quality for this call.
                if (call.state === 'active') {
                    ensureRemotePlayback();
                    reportNegotiation(call);
                    applyPlayoutDelayToCall(call);
                    startQualitySampler(call);
                }

                if (outboundNotify) {
                    var mapped = mapTelnyxOutboundState(call.state);

                    if (mapped) {
                        outboundNotify(mapped);
                    }
                }

                if (isTelnyxTerminalState(call.state)) {
                    // Send the end-of-call quality summary before clearing the call.
                    stopQualitySampler(true);
                    clearCall(call);
                }
            }
        });

        client.on('telnyx.error', function (error) {
            // A failed BYE while hanging up is expected when the call is already tearing down; surfacing it as
            // an error is misleading, so swallow it and keep only a diagnostic trace.
            if (isBenignHangupError(error)) {
                if (window.console && console.debug) {
                    console.debug('[soft-phone] Ignored benign Telnyx hang-up error.', error);
                }

                return;
            }

            context.showError((error && (error.error || error.message)) || 'Telnyx WebRTC error.');
        });

        // Signaling-health signals for the degraded-state UX (item 6). The SDK owns the socket reconnect
        // (autoReconnect, which we leave enabled); we only observe it. A close/error while the session is live
        // means the SDK is reconnecting, so surface "Reconnecting..."; a re-open clears it. Intentional
        // teardown (dispose/renewal sets `disposed`) must not masquerade as a reconnect.
        function reportSignalingDegraded(active) {
            if (disposed || typeof context.onSignalingDegraded !== 'function') {
                return;
            }

            context.onSignalingDegraded(active);
        }

        client.on('telnyx.socket.close', function () {
            reportSignalingDegraded(true);
        });

        client.on('telnyx.socket.error', function () {
            reportSignalingDegraded(true);
        });

        client.on('telnyx.socket.open', function () {
            reportSignalingDegraded(false);
        });

        // Provider SDK warnings (media/quality advisories such as "Low local microphone audio detected"). Surface
        // them to the core for the Diagnostics tab and server telemetry.
        client.on('telnyx.warning', function (warning) {
            if (!disposed && typeof context.onProviderWarning === 'function') {
                context.onProviderWarning(warning);
            }
        });

        // On every (re)connect, clear any stale transient error banner -- most importantly a LOGIN_FAILED that
        // has since recovered (for example after a brief credential churn), which would otherwise linger on the
        // widget even though the client is connected again.
        client.on('telnyx.ready', function () {
            if (!disposed) {
                context.showError(null);
                reportSignalingDegraded(false);
            }
        });

        // Resolve the session only once the client has logged in (telnyx.ready); newCall/answer require a
        // live session.
        return new Promise(function (resolve, reject) {
            var settled = false;

            client.on('telnyx.ready', function () {
                if (settled) {
                    return;
                }

                settled = true;
                resolve(buildSession());
            });

            client.on('telnyx.error', function (error) {
                if (settled) {
                    return;
                }

                settled = true;
                reject(new Error((error && (error.error || error.message)) || 'Telnyx WebRTC login failed.'));
            });

            try {
                client.connect();
            } catch (error) {
                if (!settled) {
                    settled = true;
                    reject(error instanceof Error ? error : new Error(String(error)));
                }
            }
        });

        // Returns live diagnostics for the current call: the negotiated SDP and a getStats dump. Used by the
        // gated diagnostics panel to pull ICE/SDP/RTP from a real session on demand (companion tooling), and by
        // the echo/loopback audio test to assert inbound bytesReceived > 0. Best-effort and read-only.
        function getCallDiagnostics() {
            var call = currentCall;
            var peer = call && call.peer && call.peer.instance;

            if (!peer) {
                return Promise.resolve({ available: false, localSdp: '', remoteSdp: '', stats: [] });
            }

            var result = { available: true, localSdp: '', remoteSdp: '', stats: [] };

            try {
                result.localSdp = (peer.localDescription && peer.localDescription.sdp) || '';
                result.remoteSdp = (peer.remoteDescription && peer.remoteDescription.sdp) || '';
            } catch (error) { /* best effort */ }

            if (typeof peer.getStats !== 'function') {
                return Promise.resolve(result);
            }

            return Promise.resolve(peer.getStats()).then(function (report) {
                if (report && typeof report.forEach === 'function') {
                    report.forEach(function (stat) {
                        result.stats.push(stat);
                    });
                }

                return result;
            }).catch(function () {
                return result;
            });
        }

        function buildSession() {
            return {
                providerConfig: registrationConfig,
                mediaCodecs: media.codecs || [],
                // The signaling edge this registration is on, for the diagnostics readout. Empty means the
                // provider's own geo-routing chose it and cannot tell us which one it picked.
                signalingRegion: signalingRegion,
                // The browser places its own outbound calls through the Telnyx SDK.
                canOriginate: true,
                // Reported to the server once registered: this client recognizes and holds the leg the platform
                // rings for an offer that is still ringing, so the platform may ring it early.
                clientCapabilities: OFFER_LEG_CAPABILITY ? [OFFER_LEG_CAPABILITY] : [],
                outboundCallerId: registrationConfig.outboundCallerId || '',
                // Optional echo/loopback destination for the diagnostics audio test (companion tooling).
                echoTestDestination: registrationConfig.echoTestDestination || '',
                // On-demand live diagnostics (SDP + getStats) for the diagnostics panel and the echo test.
                getDiagnostics: getCallDiagnostics,
                // Re-applies the playout hint to the live call after the agent changes it.
                refreshPlayoutDelay: function () {
                    if (currentCall) {
                        applyPlayoutDelayToCall(currentCall);
                    }
                },
                // Swaps the outgoing audio track of the live call without renegotiating, for a capture that died
                // mid-call. Resolves false when there is no live sender to swap.
                replaceLocalAudioTrack: function (track) {
                    var peer = currentCall && currentCall.peer && currentCall.peer.instance;

                    if (!peer || typeof peer.getSenders !== 'function') {
                        return Promise.resolve(false);
                    }

                    var sender = findAudioSender(peer.getSenders());

                    if (!sender || typeof sender.replaceTrack !== 'function') {
                        return Promise.resolve(false);
                    }

                    // The capture probe is NOT rebuilt here. At this point the soft phone has not yet committed the
                    // swap: its send stream still holds the old track, which it stops only afterwards, so a probe
                    // built now bound to a track that was about to die and read 0.000 for the rest of the call
                    // (observed live: OutLevel=0.000 while the far end measured InLevel around 0.3). The caller
                    // rebuilds it through refreshCaptureProbe once the commit is done.
                    return Promise.resolve(sender.replaceTrack(track)).then(function () {
                        return true;
                    });
                },
                // Re-points the capture level probe at the track now being sent, once a mid-call microphone swap
                // has been committed. A no-op when no call is being sampled.
                refreshCaptureProbe: refreshCaptureProbe,
                // Places an outbound call through the Telnyx SDK and returns a controller. onState receives
                // soft-phone state names: 'Ringing', 'Connected', 'Disconnected'.
                originate: function (destination, callerId, onState) {
                    var notify = typeof onState === 'function' ? onState : function () { };

                    if (disposed) {
                        notify('Disconnected');

                        return null;
                    }

                    var call;

                    try {
                        var callOptions = {
                            destinationNumber: destination,
                            callerNumber: callerId || registrationConfig.outboundCallerId || '',
                            // Reuse the microphone stream the soft phone already acquired so the SDK does not
                            // open a second capture for outbound calls.
                            localStream: context.localStream,
                            remoteElement: remoteElement,
                            audio: true,
                            video: false
                        };

                        if (preferredCodecs) {
                            callOptions.preferred_codecs = preferredCodecs;
                        }

                        call = client.newCall(callOptions);
                    } catch (error) {
                        context.showError(error && error.message ? error.message : String(error));
                        notify('Disconnected');

                        return null;
                    }

                    currentCall = call;
                    currentCallIsBrowserOriginated = true;
                    outboundNotify = notify;

                    return {
                        terminate: function () {
                            endHoldAudio();

                            try {
                                return Promise.resolve(call.hangup()).catch(function () { });
                            } catch (error) {
                                return Promise.resolve();
                            }
                        },
                        setHold: function (hold) {
                            try {
                                return applyHold(call, hold);
                            } catch (error) {
                                return Promise.resolve();
                            }
                        },
                        setMute: function (mute) {
                            try {
                                if (mute) {
                                    call.muteAudio();
                                } else {
                                    call.unmuteAudio();
                                }
                            } catch (error) { /* best effort */ }

                            return Promise.resolve();
                        }
                    };
                },
                handleCallState: function (serverCall) {
                    var stateName = normalizeState(serverCall && serverCall.state);

                    // A call the browser placed itself has no platform interaction behind it, so the platform
                    // reporting no active call is silence, not an instruction to hang up. Manual dials were being
                    // dropped the moment the customer answered: the answer refreshed the active-call list, the
                    // list came back empty because nothing server-side had ever recorded the call, and this ran
                    // with no call at all and tore down the live session. Only an explicit terminal state from
                    // the platform ends a browser-originated call.
                    if (!serverCall && currentCallIsBrowserOriginated) {
                        return Promise.resolve();
                    }

                    if (!serverCall || stateName === 'Disconnected' || stateName === 'Failed') {
                        if (currentCall) {
                            stopQualitySampler(true);
                            endHoldAudio();

                            try {
                                currentCall.hangup();
                            } catch (error) { /* best effort */ }

                            currentCall = null;
                            currentCallIsBrowserOriginated = false;
                            outboundNotify = null;
                        }

                        return Promise.resolve();
                    }

                    if (!currentCall) {
                        return Promise.resolve();
                    }

                    if (stateName === 'OnHold') {
                        try {
                            return applyHold(currentCall, true);
                        } catch (error) {
                            return Promise.resolve();
                        }
                    }

                    if (stateName === 'Connected') {
                        try {
                            if (serverCall.isMuted) {
                                currentCall.muteAudio();
                            } else {
                                currentCall.unmuteAudio();
                            }

                            return applyHold(currentCall, false);
                        } catch (error) {
                            return Promise.resolve();
                        }
                    }

                    return Promise.resolve();
                },
                dispose: function () {
                    if (disposed) {
                        return Promise.resolve();
                    }

                    disposed = true;
                    // Flush the end-of-call quality summary if a call was still live at disposal.
                    stopQualitySampler(true);
                    endHoldAudio();

                    if (currentCall) {
                        try {
                            currentCall.hangup();
                        } catch (error) { /* best effort */ }

                        currentCall = null;
                        currentCallIsBrowserOriginated = false;
                        outboundNotify = null;
                    }

                    try {
                        return Promise.resolve(client.disconnect()).catch(function () { });
                    } catch (error) {
                        return Promise.resolve();
                    }
                }
            };
        }
    }

    function clamp(value, min, max) {
        return Math.min(Math.max(value, min), max);
    }

    function isFiniteNumber(value) {
        return typeof value === 'number' && isFinite(value);
    }

    // A self-contained inbound-call ringtone synthesized with the Web Audio API, so no audio asset needs to be
    // bundled or fetched (which would also run into the strict CSP). It plays a classic telephone double-ring
    // cadence (two short 440/480 Hz bursts, then a pause) on a loop until stopped. start() is idempotent, so it
    // can be called on every render while a call is ringing. Playback needs the page to have had a user gesture
    // (browser autoplay policy); an agent using the admin has almost always interacted, and if not the ring is
    // simply silent rather than erroring.
    function createRingtonePlayer() {
        var AudioCtx = window.AudioContext || window.webkitAudioContext;
        var ctx = null;
        var oscA = null;
        var oscB = null;
        var gain = null;
        var timer = null;
        var running = false;

        function ringOnce() {
            if (!ctx || !gain) {
                return;
            }

            var now = ctx.currentTime;
            var peak = 0.14;
            var floor = 0.0001;

            gain.gain.cancelScheduledValues(now);
            gain.gain.setValueAtTime(floor, now);
            // First burst.
            gain.gain.exponentialRampToValueAtTime(peak, now + 0.04);
            gain.gain.setValueAtTime(peak, now + 0.4);
            gain.gain.exponentialRampToValueAtTime(floor, now + 0.45);
            // Second burst (double-ring), then a ~2s silence handled by the interval.
            gain.gain.exponentialRampToValueAtTime(peak, now + 0.65);
            gain.gain.setValueAtTime(peak, now + 1.0);
            gain.gain.exponentialRampToValueAtTime(floor, now + 1.05);
        }

        return {
            start: function () {
                if (running || !AudioCtx) {
                    return;
                }

                running = true;

                try {
                    ctx = new AudioCtx();
                    oscA = ctx.createOscillator();
                    oscB = ctx.createOscillator();
                    gain = ctx.createGain();
                    oscA.type = 'sine';
                    oscB.type = 'sine';
                    oscA.frequency.value = 440;
                    oscB.frequency.value = 480;
                    gain.gain.value = 0.0001;
                    oscA.connect(gain);
                    oscB.connect(gain);
                    gain.connect(ctx.destination);
                    oscA.start();
                    oscB.start();

                    if (ctx.state === 'suspended' && typeof ctx.resume === 'function') {
                        ctx.resume().catch(function () { });
                    }

                    ringOnce();
                    timer = window.setInterval(ringOnce, 3000);
                } catch (error) {
                    running = false;
                }
            },
            stop: function () {
                if (!running) {
                    return;
                }

                running = false;

                if (timer) {
                    window.clearInterval(timer);
                    timer = null;
                }

                try {
                    if (gain && ctx) {
                        gain.gain.cancelScheduledValues(ctx.currentTime);
                        gain.gain.setValueAtTime(0.0001, ctx.currentTime);
                    }

                    if (oscA) {
                        oscA.stop();
                    }

                    if (oscB) {
                        oscB.stop();
                    }
                } catch (error) { /* best effort */ }

                try {
                    if (ctx && typeof ctx.close === 'function') {
                        ctx.close();
                    }
                } catch (error) { /* best effort */ }

                ctx = oscA = oscB = gain = null;
            }
        };
    }

    function createSoftPhone(rootElement, options) {
        options = options || {};

        var config = parseConfig(rootElement);
        var strings = config.strings || {};
        var capabilities = config.capabilities || 0;
        var storageKey = (config.storageKey || 'telephony-soft-phone') + '-layout';
        var mediaAdapters = createBrowserMediaAdapterRegistry(rootElement, config);

        // The standalone /softphone page (hosted by the CrestApps Soft Phone browser extension) wraps the phone
        // in an element flagged data-softphone-embedded, and -- when the agent answered an inbound call from an
        // OS notification while the phone window was closed -- data-softphone-answer-call-id. Embedded mode
        // renders the phone open and full-window (the page CSS pins it); an answer call id, when present, makes
        // the phone auto-answer that one offer as it arrives (see the one-shot state below).
        var embeddedHost = typeof rootElement.closest === 'function'
            ? rootElement.closest('[data-softphone-embedded]')
            : null;
        var isEmbedded = !!(embeddedHost && embeddedHost.getAttribute('data-softphone-embedded') === 'true');
        var requestedAnswerCallId = embeddedHost
            ? (embeddedHost.getAttribute('data-softphone-answer-call-id') || '').trim()
            : '';

        var signalRFactory = options.signalRFactory || (typeof signalR !== 'undefined' ? signalR : null);

        var dom = {
            toggle: rootElement.querySelector('[data-telephony-toggle]'),
            toggleIcon: rootElement.querySelector('[data-telephony-toggle-icon]'),
            panel: rootElement.querySelector('[data-telephony-panel]'),
            dragHandle: rootElement.querySelector('[data-telephony-drag-handle]'),
            disconnect: rootElement.querySelector('[data-telephony-disconnect]'),
            close: rootElement.querySelector('[data-telephony-close]'),
            status: rootElement.querySelector('[data-telephony-status]'),
            number: rootElement.querySelector('[data-telephony-number]'),
            dialModeToggle: rootElement.querySelector('[data-telephony-dial-mode-toggle]'),
            dialModeLabel: rootElement.querySelector('[data-telephony-dial-mode-label]'),
            error: rootElement.querySelector('[data-telephony-error]'),
            micRetry: rootElement.querySelector('[data-telephony-mic-retry]'),
            settingsToggle: rootElement.querySelector('[data-telephony-settings-toggle]'),
            settingsPanel: rootElement.querySelector('[data-telephony-settings-panel]'),
            settingsBack: rootElement.querySelector('[data-telephony-settings-back]'),
            inputDevice: rootElement.querySelector('[data-telephony-input-device]'),
            outputDevice: rootElement.querySelector('[data-telephony-output-device]'),
            processingEc: rootElement.querySelector('[data-telephony-processing-ec]'),
            processingNs: rootElement.querySelector('[data-telephony-processing-ns]'),
            processingAgc: rootElement.querySelector('[data-telephony-processing-agc]'),
            micBoost: rootElement.querySelector('[data-telephony-mic-boost]'),
            playoutDelay: rootElement.querySelector('[data-telephony-playout-delay]'),
            signalingRegion: rootElement.querySelector('[data-telephony-signaling-region]'),
            signalingRegionStatus: rootElement.querySelector('[data-telephony-signaling-region-status]'),
            outputDeviceRow: rootElement.querySelector('[data-telephony-output-device-row]'),
            diagnosticsTab: rootElement.querySelector('[data-telephony-diagnostics-tab]'),
            diagStatus: rootElement.querySelector('[data-telephony-diag-status]'),
            diagReadout: rootElement.querySelector('[data-telephony-diag-readout]'),
            diagOutput: rootElement.querySelector('[data-telephony-diag-output]'),
            diagEchoInput: rootElement.querySelector('[data-telephony-diag-echo-input]'),
            diagRun: rootElement.querySelector('[data-telephony-diag-run]'),
            diagDump: rootElement.querySelector('[data-telephony-diag-dump]'),
            diagWarnings: rootElement.querySelector('[data-telephony-diag-warnings]'),
            diagWarningsSection: rootElement.querySelector('[data-telephony-diag-warnings-section]'),
            micMeter: rootElement.querySelector('[data-telephony-mic-meter]'),
            micMeterFill: rootElement.querySelector('[data-telephony-mic-meter-fill]'),
            micMeterHint: rootElement.querySelector('[data-telephony-mic-meter-hint]'),
            activeCalls: rootElement.querySelector('[data-telephony-active-calls]'),
            activeCallsList: rootElement.querySelector('[data-telephony-active-calls-list]'),
            keys: Array.prototype.slice.call(rootElement.querySelectorAll('[data-telephony-key]')),
            dial: rootElement.querySelector('[data-telephony-dial]'),
            hold: rootElement.querySelector('[data-telephony-hold]'),
            resume: rootElement.querySelector('[data-telephony-resume]'),
            mute: rootElement.querySelector('[data-telephony-mute]'),
            unmute: rootElement.querySelector('[data-telephony-unmute]'),
            transfer: rootElement.querySelector('[data-telephony-transfer]'),
            transferIcon: rootElement.querySelector('[data-telephony-transfer-icon]'),
            transferLabel: rootElement.querySelector('[data-telephony-transfer-label]'),
            transferPanel: rootElement.querySelector('[data-telephony-transfer-panel]'),
            keypadPanel: rootElement.querySelector('[data-telephony-keypad-panel]'),
            transferInput: rootElement.querySelector('[data-telephony-transfer-input]'),
            transferCancel: rootElement.querySelector('[data-telephony-transfer-cancel]'),
            transferConfirm: rootElement.querySelector('[data-telephony-transfer-confirm]'),
            directory: rootElement.querySelector('[data-telephony-directory]'),
            directoryList: rootElement.querySelector('[data-telephony-directory-list]'),
            merge: rootElement.querySelector('[data-telephony-merge]'),
            hangup: rootElement.querySelector('[data-telephony-hangup]'),
            hangupAll: rootElement.querySelector('[data-telephony-hangup-all]'),
            body: rootElement.querySelector('[data-telephony-body]'),
            connectPanel: rootElement.querySelector('[data-telephony-connect-panel]'),
            connect: rootElement.querySelector('[data-telephony-connect]'),
            connectError: rootElement.querySelector('[data-telephony-connect-error]'),
            unavailable: rootElement.querySelector('[data-telephony-unavailable]'),
            unavailableText: rootElement.querySelector('[data-telephony-unavailable-text]'),
            keypadView: rootElement.querySelector('[data-telephony-view="keypad"]'),
            history: rootElement.querySelector('[data-telephony-history]'),
            historyList: rootElement.querySelector('[data-telephony-history-list]'),
            footer: rootElement.querySelector('[data-telephony-footer]'),
            tabs: Array.prototype.slice.call(rootElement.querySelectorAll('[data-telephony-tab]')),
            views: Array.prototype.slice.call(rootElement.querySelectorAll('[data-telephony-view]')),
            incoming: rootElement.querySelector('[data-telephony-incoming]'),
            incomingCaller: rootElement.querySelector('[data-telephony-incoming-caller]'),
            incomingQueue: rootElement.querySelector('[data-telephony-incoming-queue]'),
            incomingCards: rootElement.querySelector('[data-telephony-incoming-cards]'),
            incomingAnswer: rootElement.querySelector('[data-telephony-incoming-answer]'),
            incomingVoicemail: rootElement.querySelector('[data-telephony-incoming-voicemail]'),
            incomingIgnore: rootElement.querySelector('[data-telephony-incoming-ignore]'),
            remoteAudio: rootElement.querySelector('[data-telephony-remote-audio]'),
            voicemailAudio: rootElement.querySelector('[data-telephony-voicemail-audio]'),
            voicemailBadge: rootElement.querySelector('[data-telephony-voicemail-badge]'),
            voicemailList: rootElement.querySelector('[data-telephony-voicemail-list]'),
            voicemailPlayer: rootElement.querySelector('[data-telephony-voicemail-player]'),
            voicemailPlayerInfo: rootElement.querySelector('[data-telephony-voicemail-player-info]'),
            voicemailToolbar: rootElement.querySelector('[data-telephony-voicemail-toolbar]'),
            voicemailDelete: rootElement.querySelector('[data-telephony-voicemail-delete]'),
            voicemailSelectAll: rootElement.querySelector('[data-telephony-voicemail-select-all]')
        };

        var connection = null;
        var currentCall = null;
        var activeCalls = {};
        var conferenceSelections = {};
        var directoryEntries = [];
        var transferOpen = false;
        var numberIsCallDisplay = false;
        var callStateRevision = 0;
        var incomingContext = null;
        var incomingHandled = false;
        var incomingAcceptPending = false;
        // The leg the platform rang for the offer on screen, held unanswered until the agent accepts or declines:
        // { reservationId, legId, controller }. And the one answered for an accept that is still being confirmed, so
        // it can be hung up if the accept fails.
        var heldOfferLeg = null;
        var answeredOfferLeg = null;
        // Offers this agent is known to have accepted -- here, in another page, or on another device -- remembered
        // briefly so their leg is answered the moment it arrives rather than held.
        var acceptedOfferIds = {};
        // Calls the agent put on hold and has not resumed, by call id. With browser audio the hold happens here and
        // the server keeps reporting the call connected, so this, not the server, says whether the call is held.
        var agentHolds = {};
        // The calls as they stood before an active-call lookup replaced them, while that lookup is being applied.
        var callsBeforeLookup = null;
        // Audible inbound-call alert, started/stopped from renderIncoming so an away agent hears a ringing call.
        var ringtone = createRingtonePlayer();

        // Every open agent page runs its own soft phone, and each rings for the same offer. Answering or declining
        // in one page tells the others through the browser, so they fall silent at once: before this, the pages the
        // agent did not click kept ringing in the headset until the server's own "offer taken" update reached them,
        // a second or so after the call had been answered.
        var offerChannel = typeof BroadcastChannel === 'function'
            ? new BroadcastChannel('crestapps-soft-phone-offers')
            : null;

        function announceOfferHandled(answered, reservationId) {
            var id = currentCallId();

            if (!offerChannel || !id) {
                return;
            }

            try {
                offerChannel.postMessage({
                    type: 'offer-handled',
                    callId: id,
                    reservationId: reservationId || '',
                    answered: !!answered
                });
            } catch (error) { /* another page not hearing it only costs a second of ringing */ }
        }

        if (offerChannel) {
            offerChannel.onmessage = function (event) {
                var message = event && event.data;

                if (!message || message.type !== 'offer-handled') {
                    return;
                }

                // The page the agent clicked in may not be the one the platform rang: whichever page holds the
                // offer's leg answers it (or hangs it up) the moment it hears.
                if (message.reservationId) {
                    settleOfferLeg(message.reservationId, !!message.answered);
                }

                if (!currentCall || currentCall.callId !== message.callId) {
                    return;
                }

                incomingHandled = true;
                render();
            };
        }
        var incomingExpiryTimer = null;
        var requiresAuthentication = false;
        var isConnected = false;
        var isAvailable = false;
        var connectionStatusResolved = false;
        var authenticationScheme = null;
        var authActionPending = false;
        var activeTab = 'keypad';
        var activeCommand = null;
        var activeCallsRefreshTimer = null;
        var suppressToggleClick = false;
        // Indefinite SignalR reconnect state. SignalR's automatic reconnect gives up after its default
        // schedule, after which an agent silently stops receiving inbound-call offers until a manual page
        // reload. The custom policy and the manual restart loop below back off but never give up.
        var manualReconnectTimer = null;
        var manualReconnectAttempt = 0;
        var pageUnloading = false;
        var browserAudioPromise = null;
        var browserAudioSession = null;
        var browserAudioHeartbeatTimer = null;
        var localAudioStream = null;
        // Selected audio input/output device ids (item 5 device picker). Null means the browser default. The
        // input id is applied to getUserMedia; the output id is applied to the remote audio element via
        // setSinkId. Both persist per soft-phone instance in localStorage.
        var selectedInputDeviceId = null;
        var selectedOutputDeviceId = null;
        // Mic-permission recovery UX (item 9). 'denied' or 'notfound' when getUserMedia is rejected for a
        // permission/device reason; drives an actionable message plus a Retry affordance (distinct from a generic
        // transient error). Cleared on a successful capture or when the agent retries.
        var micPermissionState = null;
        // Whether the settings overlay (opened from the header gear) is showing. It holds the audio device
        // pickers off the keypad; it is an overlay, not a footer tab.
        var settingsOpen = false;
        // Controllers for calls the browser originated itself (client-originated providers such as Telnyx),
        // keyed by the synthetic call id. Server-tracked calls are not in this map.
        var browserCallControllers = {};

        // Set by the media adapter's quality sampler (item 2) when the live call's measured quality breaches the
        // poor-connection thresholds, and consumed by the degraded-state UX (item 6). Kept in the core so it
        // survives across adapter calls and can be read by render().
        var connectionQualityPoor = false;

        // Degraded-state UX (item 6). Two independent "trying to recover" signals drive a "Reconnecting..."
        // status: the SignalR hub connection (hubReconnecting) and the provider media socket (mediaReconnecting).
        // Each is toggled only on an actual state change, which debounces the status line so it does not flap.
        var hubReconnecting = false;
        var mediaReconnecting = false;

        // Gated diagnostics tab: a live getStats/SDP view with a microphone level meter, provider warnings, and
        // the echo/loopback audio test. Enabled by the EnableDiagnostics site setting (admins can turn it on at
        // runtime to troubleshoot production, then off) or ad hoc for one session with ?diag=1. lastQualitySample
        // mirrors item 2's most recent measurement for the live readout.
        var diagnosticsEnabled = !!config.enableDiagnostics ||
            /(?:^|[?&])diag=1(?:&|$)/.test(window.location.search || '');
        var lastQualitySample = null;
        var echoTestActive = false;

        // A direct browser-inbound (extension) call that is ringing and awaiting the agent's Answer/Decline
        // choice: { callId, controller }. The controller is the media adapter's handle for answering, declining,
        // and (once answered) controlling the SDK call.
        var browserInboundRing = null;

        // One-shot expectation that the next inbound provider leg belongs to a call this browser initiated (an
        // extension call it just placed), so the media adapter answers it automatically instead of ringing. It is
        // armed when placing an extension call and consumed by the adapter for the next inbound leg; a genuine
        // incoming call arriving without this armed still rings.
        var INBOUND_AUTO_ANSWER_WINDOW_MS = 20000;
        var expectInboundAutoAnswerUntil = 0;

        // One-shot auto-answer for the extension "answer from the OS notification" handoff. When the standalone
        // page is opened as /softphone?answerCallId=ID, the phone answers exactly the offer whose call id matches
        // ID, and only once, the moment it surfaces -- whether restored on (re)connect (Contact Center's
        // GetCurrentIncomingOffer) or pushed live (the hub's IncomingCall). A non-matching call is never
        // auto-answered, and if the requested offer never arrives (it expired before the window opened) the phone
        // simply falls through to the normal idle/ringing UI. The exact call-id match is the real guard against
        // answering a stale call; the deadline only disarms the one-shot so it cannot linger past its usefulness.
        var ANSWER_ON_LOAD_WINDOW_MS = 30000;
        var pendingAnswerCallId = requestedAnswerCallId || '';
        var pendingAnswerDeadline = pendingAnswerCallId ? (Date.now() + ANSWER_ON_LOAD_WINDOW_MS) : 0;

        // True from the moment a call is placed until the first real call state arrives (or the attempt
        // fails). While it is set - and no call is active yet - the status line reads "Connecting" instead
        // of sitting on "Ready" during the 1-2s the browser audio session and the server take to acknowledge
        // the dial, so the user can see the call is already in progress. A safety timer clears it if no call
        // ever materializes so the status can never get stranded on "Connecting".
        var pendingDial = false;
        var pendingDialNumber = null;
        var pendingDialTimer = null;

        function beginPendingDial(number) {
            if (pendingDialTimer) {
                window.clearTimeout(pendingDialTimer);
            }

            pendingDial = true;
            pendingDialNumber = number || null;
            pendingDialTimer = window.setTimeout(function () {
                clearPendingDial();
                render();
            }, 30000);
        }

        function clearPendingDial() {
            pendingDial = false;
            pendingDialNumber = null;

            if (pendingDialTimer) {
                window.clearTimeout(pendingDialTimer);
                pendingDialTimer = null;
            }
        }

        // The phone number input is enhanced with intl-tel-input so a national number entered on the
        // keypad is normalized to E.164 (with a country selector) before it is dialed or screened.
        // A country must always be selected, otherwise intl-tel-input cannot resolve a national number
        // to E.164 and getNumber() echoes the raw digits, which the server then rejects as not dialable.
        var telInput = null;
        var initialCountry = resolveInitialCountry();
        var extensionMode = false;

        if (dom.number && typeof window.intlTelInput === 'function') {
            var telInputOptions = {
                containerClass: 'telephony-soft-phone__number-iti',
                dropdownParent: document.body
            };

            if (initialCountry) {
                telInputOptions.initialCountry = initialCountry;
            }

            telInput = window.intlTelInput(dom.number, telInputOptions);

            preventCountryDropdownScroll();
        }

        // The country dropdown is detached to document.body so it can escape the panel's bounded,
        // scrollable area. Because that detached list sits outside the normal flow, the browser scrolls
        // the page to the search input the first time intl-tel-input focuses it. Capture the scroll
        // position when the flag is clicked and restore it before the next paint so the page does not jump.
        function preventCountryDropdownScroll() {
            var flagButton = rootElement.querySelector('.iti__selected-country');

            if (!flagButton) {
                return;
            }

            flagButton.addEventListener('click', function () {
                var scrollX = window.scrollX;
                var scrollY = window.scrollY;

                window.requestAnimationFrame(function () {
                    if (window.scrollX !== scrollX || window.scrollY !== scrollY) {
                        window.scrollTo(scrollX, scrollY);
                    }
                });
            });
        }

        function resolveInitialCountry() {
            if (config.defaultCountryCode) {
                return config.defaultCountryCode;
            }

            var candidates = (navigator.languages && navigator.languages.length)
                ? navigator.languages
                : (navigator.language ? [navigator.language] : []);

            for (var i = 0; i < candidates.length; i++) {
                var match = /[-_]([A-Za-z]{2})(?:$|[-_])/.exec(candidates[i] || '');

                if (match) {
                    return match[1].toLowerCase();
                }
            }

            return 'us';
        }

        function getDialNumber() {
            var raw = dom.number ? normalizeDialNumber(dom.number.value) : '';

            // In extension mode the destination is an internal extension, not a dialable phone number,
            // so it is sent verbatim and the country selector is ignored.
            if (extensionMode) {
                return raw;
            }

            // A number the user already entered in international form is dialed as-is.
            if (raw.charAt(0) === '+') {
                return raw;
            }

            var visibleDigits = raw.replace(/\D/g, '');

            if (telInput && typeof telInput.getNumber === 'function') {
                // Only trust the intl-tel-input E.164 output for real, valid phone numbers. Short
                // strings such as internal extensions are not valid numbers, so they are dialed
                // verbatim instead of being turned into a bogus "+1101" style destination.
                var isValid = typeof telInput.isValidNumber !== 'function' || telInput.isValidNumber();

                if (isValid) {
                    var e164 = telInput.getNumber();

                    // Guard against intl-tel-input desyncing (e.g. a keypad edit that did not update its
                    // internal state): only accept its E.164 when its digits actually contain what the user
                    // sees, so the dialed number can never differ from the visible number.
                    if (e164 && e164.charAt(0) === '+' &&
                        (visibleDigits === '' || e164.replace(/\D/g, '').indexOf(visibleDigits) !== -1)) {
                        return e164;
                    }
                }
            }

            // Fall back to the visible number, prefixed with the selected country's dial code so it stays
            // routable. This path guarantees the dialed number matches what is on screen.
            if (visibleDigits && telInput && typeof telInput.getSelectedCountryData === 'function') {
                var dialCode = (telInput.getSelectedCountryData() || {}).dialCode;

                if (dialCode) {
                    return visibleDigits.indexOf(dialCode) === 0
                        ? '+' + visibleDigits
                        : '+' + dialCode + visibleDigits;
                }
            }

            return raw;
        }

        function setNumberDisplay(value) {
            if (!dom.number) {
                return;
            }

            if (value && telInput && typeof telInput.setNumber === 'function') {
                var normalized = normalizeDialNumber(value);

                if (normalized.charAt(0) === '+') {
                    telInput.setNumber(normalized);

                    return;
                }
            }

            dom.number.value = value ? formatPhoneNumber(value) : '';
        }

        function clearNumberInput() {
            // Also drop any lingering dial/call-display state, otherwise the next render() re-fills the input
            // with the previous number (pendingDialNumber / the last call's peer) and clobbers what the user
            // is now typing -- e.g. entering "2" but dialing the previous "6183".
            clearPendingDial();
            numberIsCallDisplay = false;

            if (dom.number) {
                dom.number.value = '';
            }

            if (telInput && initialCountry && typeof telInput.setSelectedCountry === 'function') {
                telInput.setSelectedCountry(initialCountry);
            }
        }

        function setDialMode(isExtension) {
            extensionMode = !!isExtension;

            rootElement.classList.toggle('telephony-soft-phone--extension', extensionMode);

            if (dom.dialModeToggle) {
                dom.dialModeToggle.setAttribute('aria-pressed', extensionMode ? 'true' : 'false');
            }

            if (dom.dialModeLabel) {
                dom.dialModeLabel.textContent = extensionMode
                    ? (strings.dialPhoneNumber || 'Dial phone number')
                    : (strings.dialExtension || 'Dial extension');
            }

            if (dom.number) {
                dom.number.setAttribute('placeholder', extensionMode
                    ? (strings.extensionPlaceholder || 'Enter an extension')
                    : (strings.numberPlaceholder || 'Enter a number'));
                dom.number.setAttribute('aria-label', extensionMode
                    ? (strings.extensionLabel || 'Extension')
                    : (strings.numberLabel || 'Phone number'));
            }

            clearNumberInput();

            if (dom.number) {
                dom.number.focus();
            }
        }

        function toggleDialMode() {
            setDialMode(!extensionMode);
        }

        function has(capability) {
            return (capabilities & capability) === capability;
        }

        function show(element, visible) {
            if (element) {
                element.hidden = !visible;
            }
        }

        function setStatus(text) {
            if (dom.status) {
                dom.status.textContent = text;
            }
        }

        function showError(message) {
            if (!dom.error) {
                return;
            }

            if (message) {
                dom.error.textContent = message;
                dom.error.hidden = false;
                // Surface client-side errors to server telemetry (item 7), throttled/deduped. Kept at warning
                // level so routine input validations do not escalate to error alerts.
                reportDiagnostic('warning', 'client-error', message, null);
            } else {
                dom.error.textContent = '';
                dom.error.hidden = true;
            }
        }

        function isBrowserAudioEnabled() {
            return config.audioMode === AUDIO_MODES.Browser && !!config.browserMediaAdapterName;
        }

        function isOAuth2Authentication() {
            return (authenticationScheme || '').toLowerCase() === 'oauth2';
        }

        function hasLiveCall() {
            return getActiveCalls().some(function (call) {
                return isActive(normalizeState(call && call.state));
            });
        }

        function stopLocalAudioStream() {
            if (micBoostPipeline) {
                micBoostPipeline.dispose();
                micBoostPipeline = null;
            }

            if (sourceAudioStream) {
                sourceAudioStream.getTracks().forEach(function (track) {
                    track.stop();
                });
                sourceAudioStream = null;
            }

            if (!localAudioStream) {
                return;
            }

            localAudioStream.getTracks().forEach(function (track) {
                track.stop();
            });
            localAudioStream = null;
        }

        // The captured microphone outlives any single call: it is acquired once when the phone registers and
        // kept for the life of that registration, which can be an hour of idle time. Over that window the track
        // can die underneath the phone -- the machine sleeps, a Bluetooth headset powers down, the device is
        // unplugged -- and a dead track raises no error anywhere: getUserMedia already succeeded, the stream
        // object is still there, and the provider encodes and sends its silence perfectly happily. The call
        // connects, the agent hears the caller normally, and only the caller knows anything is wrong. These
        // read the track's actual state so that case is caught rather than inferred from a complaint.
        // The captured microphone track as a DEVICE: its label, whether it is still delivering, its settings.
        // That is the raw capture when a boost graph sits between it and the send stream, and the send track
        // itself otherwise -- the boosted output is always 'live' and carries no device label, so reading it
        // here would hide a dead microphone behind a healthy-looking track.
        function getLocalAudioTrack() {
            var track = getSourceAudioTrack();

            if (track) {
                return track;
            }

            if (!localAudioStream || typeof localAudioStream.getAudioTracks !== 'function') {
                return null;
            }

            return localAudioStream.getAudioTracks()[0] || null;
        }

        function localAudioTrackLabel() {
            var track = getLocalAudioTrack();

            return (track && track.label) || '';
        }

        // What the microphone is actually delivering, as opposed to what was asked for.
        //
        // The sample rate is the one to read first. A Bluetooth headset can only run its microphone in
        // hands-free mode, and Windows then delivers capture at 8 kHz (narrowband) or 16 kHz (wideband) instead
        // of 48 kHz -- while the same headset keeps playing back at full quality, so the agent hears a perfect
        // call and the far end hears a thin, hollow, distant voice. Nothing else on the call reflects this: the
        // codec is still negotiated at G722, the bitrate is unchanged, no packets are lost, and the score stays
        // high. The constraints are reported next to it because they were requested, not guaranteed -- and
        // echo cancellation and noise suppression layered on top of a headset's own processing add exactly the
        // hollowness this is trying to explain.
        function localCaptureSettings() {
            var track = getLocalAudioTrack();

            if (!track || typeof track.getSettings !== 'function') {
                return null;
            }

            try {
                return track.getSettings() || null;
            } catch (error) {
                return null;
            }
        }

        // A compact, readable rendering of the capture format for the diagnostics line.
        function describeCaptureSettings() {
            var settings = localCaptureSettings();

            if (!settings) {
                return '';
            }

            var parts = [];

            if (settings.sampleRate) {
                parts.push(Math.round(settings.sampleRate / 1000) + 'kHz');
            }

            if (settings.channelCount) {
                parts.push(settings.channelCount === 1 ? 'mono' : settings.channelCount + 'ch');
            }

            var processing = [];

            if (settings.echoCancellation) {
                processing.push('ec');
            }

            if (settings.noiseSuppression) {
                processing.push('ns');
            }

            if (settings.autoGainControl) {
                processing.push('agc');
            }

            if (processing.length) {
                parts.push(processing.join('+'));
            }

            var boost = describeBoost(micBoostDb);

            if (boost) {
                parts.push(boost);
            }

            return parts.join(' ');
        }

        function isLocalAudioTrackDead() {
            // Nothing captured yet is not a dead capture: the phone simply has not registered.
            if (!localAudioStream) {
                return false;
            }

            return !isTrackDeliverable(getLocalAudioTrack());
        }

        // Watches the captured track for the two ways it stops delivering audio. 'ended' is terminal -- the
        // device is gone and only a fresh getUserMedia can recover. 'mute' means the source stopped feeding the
        // track (a headset switching profile, a device going to sleep) and can recover on its own, so it is
        // reported and surfaced but not acted on destructively.
        function watchLocalAudioTrack() {
            var track = getLocalAudioTrack();

            if (!track) {
                return;
            }

            track.onended = function () {
                handleLocalAudioTrackLost('ended');
            };

            track.onmute = function () {
                handleLocalAudioTrackLost('muted');
            };

            track.onunmute = function () {
                reportDiagnostic('info', 'microphone-restored',
                    'The captured microphone is delivering audio again.', localAudioTrackLabel());

                if (micPermissionState === null) {
                    showError(null);
                }

                startMicMeter();
            };
        }

        // The capture is live but delivering nothing measurable, sustained long enough not to be a pause in the
        // conversation. This is the muted-headset and wrong-device case, which the track state cannot see: the
        // track is 'live' and unmuted, it is simply carrying silence. Worth interrupting the agent for -- the
        // alternative is finding out from the caller, which is how tonight went.
        function handleSilentCapture() {
            var label = localAudioTrackLabel();

            reportDiagnostic('warning', 'microphone-silent',
                'The microphone is live but has delivered no audio for several samples.', label);

            showError(strings.microphoneSilent ||
                'Your microphone is not picking up any sound, so the caller cannot hear you. Check that it is not muted and that the right device is selected.');
        }

        function handleLocalAudioTrackLost(reason) {
            reportDiagnostic('warning', 'microphone-lost',
                'The captured microphone stopped delivering audio (' + reason + ').', localAudioTrackLabel());

            // During a call, re-registering would tear down the media session, but the capture itself can be
            // replaced under the live call: acquire a fresh track from the same device selection and swap it onto
            // the sender with replaceTrack, which needs no renegotiation. Only if that fails is the agent left to
            // be told -- and they are, at that moment, on a call the caller cannot hear them on.
            if (hasLiveCall()) {
                switchLocalAudioTrack(reason).catch(function () {
                    showError(strings.microphoneLostOnCall ||
                        'Your microphone stopped working, so the caller cannot hear you. Check the device and call back.');
                });

                return;
            }

            // Idle: drop the dead capture and register again, so the next call starts from a live microphone
            // instead of inheriting this one.
            showError(strings.microphoneLostIdle ||
                'Your microphone stopped working and is being reconnected.');
            releaseBrowserAudio();
            registerBrowserAudioForInbound();
        }

        // Switches the captured microphone to the current device selection -- because the agent picked another
        // device, or because the source died -- without disturbing a call in progress.
        //
        // The registration-time stream is kept for the life of the registration, so both cases used to mean the
        // same thing: either tear the whole session down and register again (dropping any live call, and
        // re-issuing the provider credential every time), or do nothing and promise "the next call". Nothing ever
        // honoured that promise -- the old track lived on until the registration was renewed -- so an agent who
        // switched headsets mid-call watched the picker move while the far end kept hearing the old microphone,
        // and still heard it on the next call.
        //
        // A fresh getUserMedia on the selection, swapped onto the live sender with replaceTrack, changes what the
        // far end hears within a packet or two and needs no renegotiation. The new track goes into the SAME
        // MediaStream object, so the adapter (which reads that stream when the next call starts) follows without
        // being told. The call-quality level probe does NOT: a MediaStreamAudioSourceNode binds to the track it
        // was built on, so after a swap it went on reading the stopped old track as 0.000 for the rest of the call
        // while the far end heard the agent fine. It is rebuilt explicitly once the swap is committed, and the
        // meter is restarted below for the same reason. Known edge: a switch made while a call is on hold replaces
        // the hold audio too; unhold then restores the original, now stopped, track. Rare enough to note rather
        // than guard.
        function switchLocalAudioTrack(reason) {
            if (!navigator.mediaDevices || typeof navigator.mediaDevices.getUserMedia !== 'function') {
                return Promise.reject(new Error('Media capture is not available.'));
            }

            return navigator.mediaDevices.getUserMedia(buildAudioConstraints()).then(function (stream) {
                // The new send track: the capture itself, or the boost graph's output when a boost is set.
                var pipeline = createBoostPipeline(stream, micBoostDb);
                var fresh = pipeline.stream.getAudioTracks()[0];

                var abandon = function (error) {
                    pipeline.dispose();
                    stream.getTracks().forEach(function (track) { track.stop(); });
                    throw error;
                };

                if (!fresh) {
                    return abandon(new Error('No audio track was captured.'));
                }

                // Under a live call the sender must take the new track, and a failure there is a failure of the
                // whole switch: leaving the far end on the old track while the meter shows the new one would be
                // exactly the lie this replaces. Idle, the swap into the stream below is enough on its own.
                var replace;

                if (!hasLiveCall()) {
                    replace = Promise.resolve();
                } else if (browserAudioSession && typeof browserAudioSession.replaceLocalAudioTrack === 'function') {
                    replace = Promise.resolve(browserAudioSession.replaceLocalAudioTrack(fresh)).then(function (replaced) {
                        if (!replaced) {
                            throw new Error('The live call has no outgoing audio track to replace.');
                        }
                    });
                } else {
                    replace = Promise.reject(new Error('The media session cannot replace its outgoing track.'));
                }

                return replace.then(function () {
                    commitCapture(stream, pipeline);

                    // Only now -- the new track on the sender and in the send stream, the old one stopped -- can
                    // the capture probe be rebuilt onto what the far end hears. A probe does not follow a track
                    // swap on its own; see refreshCaptureProbe.
                    if (browserAudioSession && typeof browserAudioSession.refreshCaptureProbe === 'function') {
                        browserAudioSession.refreshCaptureProbe();
                    }

                    watchLocalAudioTrack();
                    reportDiagnostic('info', 'microphone-switched',
                        'The captured microphone was switched (' + reason + ').',
                        localAudioTrackLabel() + (describeBoost(micBoostDb) ? ' ' + describeBoost(micBoostDb) : ''));

                    if (micPermissionState === null) {
                        showError(null);
                    }

                    stopMicMeter();
                    startMicMeter();
                    populateDevicePickers();
                    checkForVirtualAudioDevices();
                }, abandon);
            });
        }

        // Builds the microphone capture constraints. The three processing flags (echo cancellation, noise
        // suppression, automatic gain control) are on by default so captured audio is clean without extra
        // configuration. When the agent has picked a specific input device (item 5) it is requested exactly;
        // otherwise the browser's default input is used.
        function buildAudioConstraints() {
            var audio = {
                // On by default; each can be switched off in the settings overlay, live, when a caller reports
                // the agent sounding hollow or processed -- these three are the usual suspects.
                echoCancellation: processingSettings.echoCancellation,
                noiseSuppression: processingSettings.noiseSuppression,
                autoGainControl: processingSettings.autoGainControl,
                // Ask for a single channel. A call is mono end to end, so stereo capture buys nothing and can
                // cost a great deal: headset microphones routed through a shared audio codec are often
                // presented as a stereo pair carrying the microphone on one side and silence on the other, and
                // WebRTC then downmixes (L+R)/2 -- roughly 6 dB of the agent's voice thrown away before it ever
                // reaches the encoder, with automatic gain control pumping to compensate. The far end hears
                // someone thin and distant while every transmitted measurement stays perfect. Asking for mono
                // lets the browser take the channel that carries signal instead of averaging it with one that
                // does not. It is a request, not a requirement, so a device that only offers stereo still works.
                channelCount: 1
            };

            if (selectedInputDeviceId) {
                audio.deviceId = { exact: selectedInputDeviceId };
            }

            return { audio: audio, video: false };
        }

        // Maps a getUserMedia rejection to an actionable error and records the recovery state (item 9). A denied
        // permission and a missing device are distinguished from a generic failure so the agent gets a specific,
        // localized message plus a Retry affordance instead of an opaque error. Never retries on its own.
        function categorizeMicError(error) {
            var name = (error && error.name) || '';

            if (name === 'NotAllowedError' || name === 'PermissionDeniedError' || name === 'SecurityError') {
                reportDiagnostic('error', 'mic-permission-denied', name, null);
                setMicPermissionIssue('denied');

                return new Error(strings.micPermissionDenied ||
                    'Microphone access is blocked. Allow microphone access in your browser, then retry.');
            }

            if (name === 'NotFoundError' || name === 'DevicesNotFoundError' || name === 'OverconstrainedError') {
                // A selected input device that has gone away throws OverconstrainedError; drop the stale selection
                // so a retry falls back to the browser default input.
                if (name === 'OverconstrainedError') {
                    selectedInputDeviceId = null;
                    persistDeviceSelection();
                }

                reportDiagnostic('error', 'mic-not-found', name, null);
                setMicPermissionIssue('notfound');

                return new Error(strings.micNotFound ||
                    'No microphone was found. Connect a microphone, then retry.');
            }

            reportDiagnostic('error', 'mic-error', name || (error && error.message) || 'getUserMedia failed', null);

            return error instanceof Error ? error : new Error(String(error));
        }

        function setMicPermissionIssue(kind) {
            micPermissionState = kind;

            var message = kind === 'denied'
                ? (strings.micPermissionDenied || 'Microphone access is blocked. Allow microphone access in your browser, then retry.')
                : (strings.micNotFound || 'No microphone was found. Connect a microphone, then retry.');

            // Show the guidance directly (not through showError, which would double-report the diagnostic) so it
            // appears regardless of which path hit the error: a dial surfaces it through the promise chain, but
            // the idle background registration swallows failures to a debug log.
            if (dom.error) {
                dom.error.textContent = message;
                dom.error.hidden = false;
            }

            render();
        }

        function clearMicPermissionIssue() {
            if (!micPermissionState) {
                return;
            }

            micPermissionState = null;

            if (dom.error && !dom.error.hidden) {
                dom.error.textContent = '';
                dom.error.hidden = true;
            }

            render();
        }

        // Retries microphone capture once after a permission/device failure (item 9). Not a loop: a repeated
        // failure simply re-shows the guidance.
        function retryMicrophone() {
            clearMicPermissionIssue();
            registerBrowserAudioForInbound();
        }

        // ---- Audio device selection (item 5) ----

        // The browser's capture processing (echo cancellation, noise suppression, automatic gain control). All on
        // by default; persisted with the device selection so an agent who found that one of them made them sound
        // hollow to callers does not have to rediscover it every day. Stored as explicit booleans -- a missing
        // value means "on", never "off".
        var processingSettings = { echoCancellation: true, noiseSuppression: true, autoGainControl: true };

        // Microphone boost in decibels (0 = off). When set, the raw capture is routed through a gain stage and a
        // limiter, and the limiter's output is what the call sends. See mic-boost.js for why.
        var micBoostDb = 0;

        // How long the browser should hold arriving audio before playing it, in seconds (-1 = the browser's own
        // judgement, which is the default). Half of the pause between an agent finishing a sentence and hearing
        // the reply is spent here; see playout-delay.js.
        var playoutDelaySeconds = -1;

        // Which of the provider's signaling edges this agent registers on (empty = follow the tenant setting,
        // and failing that the provider's geo-routing). Unlike every other setting here this one is fixed when
        // the provider client is built, so changing it re-registers. See signaling-region.js.
        var signalingRegion = '';

        // Set when the agent changes the region above: the next time the registration is ensured it is rebuilt
        // rather than reused, through the same path that revokes the credential it replaces.
        var reregisterOnNextEnsure = false;

        // The raw capture from the device -- what the device label, mute/ended state and capture settings are
        // read from -- as distinct from localAudioStream, which is the stream the call SENDS. Without a boost the
        // two carry the same track; with one, localAudioStream carries the boosted output.
        var sourceAudioStream = null;
        var micBoostPipeline = null;

        function getSourceAudioTrack() {
            if (!sourceAudioStream || typeof sourceAudioStream.getAudioTracks !== 'function') {
                return null;
            }

            return sourceAudioStream.getAudioTracks()[0] || null;
        }

        // Makes a fresh capture (already routed through its pipeline) the stream the call sends. localAudioStream
        // is a long-lived container that the provider adapter holds a reference to and reads when a call starts,
        // so its identity never changes: tracks are swapped inside it. The previous capture and graph are retired
        // once the new track is in place.
        function commitCapture(stream, pipeline) {
            var previousSource = sourceAudioStream;
            var previousPipeline = micBoostPipeline;
            var fresh = pipeline.stream.getAudioTracks()[0] || null;

            sourceAudioStream = stream;
            micBoostPipeline = pipeline;

            if (!localAudioStream) {
                localAudioStream = new MediaStream();
            }

            localAudioStream.getAudioTracks().forEach(function (old) {
                if (old !== fresh) {
                    localAudioStream.removeTrack(old);
                    old.stop();
                }
            });

            if (fresh && localAudioStream.getAudioTracks().indexOf(fresh) === -1) {
                localAudioStream.addTrack(fresh);
            }

            if (previousPipeline && previousPipeline !== pipeline) {
                previousPipeline.dispose();
            }

            if (previousSource && previousSource !== stream) {
                previousSource.getTracks().forEach(function (track) { track.stop(); });
            }

            return fresh;
        }

        function readProcessingFlag(value, fallback) {
            return typeof value === 'boolean' ? value : fallback;
        }

        function loadDeviceSelection() {
            var layout = loadLayout();
            selectedInputDeviceId = layout.inputDeviceId || null;
            selectedOutputDeviceId = layout.outputDeviceId || null;
            processingSettings = {
                echoCancellation: readProcessingFlag(layout.echoCancellation, true),
                noiseSuppression: readProcessingFlag(layout.noiseSuppression, true),
                autoGainControl: readProcessingFlag(layout.autoGainControl, true)
            };
            micBoostDb = clampBoostDb(layout.micBoostDb);
            playoutDelaySeconds = clampPlayoutDelay(
                Object.prototype.hasOwnProperty.call(layout, 'playoutDelaySeconds')
                    ? layout.playoutDelaySeconds
                    : config.playoutDelaySeconds);
            signalingRegion = clampSignalingRegion(layout.signalingRegion);
        }

        function persistDeviceSelection() {
            saveLayout({
                inputDeviceId: selectedInputDeviceId || '',
                outputDeviceId: selectedOutputDeviceId || '',
                echoCancellation: processingSettings.echoCancellation,
                noiseSuppression: processingSettings.noiseSuppression,
                autoGainControl: processingSettings.autoGainControl,
                micBoostDb: micBoostDb,
                playoutDelaySeconds: playoutDelaySeconds,
                signalingRegion: signalingRegion
            });
        }

        // The signaling-region choice changed. The provider fixes the edge when its client is constructed, so
        // unlike the other audio settings this cannot be applied to what is already running: the registration has
        // to be rebuilt. Doing that under a live call would drop the call, so a call in progress keeps the edge it
        // started on and the agent is told the change waits for it to end -- the same rule the credential renewal
        // already follows. Idle, it re-registers immediately, because an agent who just moved themselves to a
        // nearer edge is trying to fix the call they are about to take.
        function onSignalingRegionChange() {
            signalingRegion = clampSignalingRegion(dom.signalingRegion ? dom.signalingRegion.value : '');
            persistDeviceSelection();

            if (!browserAudioSession) {
                // Nothing registered yet; whatever is stored is read when registration happens.
                return;
            }

            // Hand the rebuild to ensureBrowserAudio rather than tearing down here: it is the path that remembers
            // the credential being replaced and revokes it once the fresh one is registered. Releasing directly
            // would leave the old provider credential live until its own expiry, and there is a cap on how many
            // an agent may hold -- reached, it refuses the login outright.
            reregisterOnNextEnsure = true;

            if (hasLiveCall()) {
                // The flag stays set, so the rebuild happens the next time the registration is ensured, which is
                // the next call or the next time this agent goes available -- both after this call has ended.
                showError(strings.signalingRegionOnNextCall ||
                    'The connection region will be used the next time you register, once this call ends.');

                return;
            }

            ensureBrowserAudio().catch(function (error) {
                reportDiagnostic('warning', 'signaling-region-register-failed',
                    String((error && error.message) || error), '');
                showError(strings.signalingRegionRegisterFailed ||
                    'The phone could not re-register on the selected connection region.');
            });
        }

        // The playout-delay choice changed. It is applied to the live call at once -- this is the one audio
        // setting whose effect the agent can hear on the call they are already on.
        function onPlayoutDelayChange() {
            playoutDelaySeconds = clampPlayoutDelay(dom.playoutDelay ? dom.playoutDelay.value : -1);
            persistDeviceSelection();

            if (browserAudioSession && typeof browserAudioSession.refreshPlayoutDelay === 'function') {
                browserAudioSession.refreshPlayoutDelay();
            }
        }

        // The boost selector changed: persist and rebuild the capture through the new gain, live if need be.
        function onBoostChange() {
            micBoostDb = clampBoostDb(dom.micBoost ? dom.micBoost.value : 0);
            persistDeviceSelection();

            if (!localAudioStream) {
                return;
            }

            switchLocalAudioTrack('boost changed').catch(function (error) {
                reportDiagnostic('warning', 'processing-switch-failed',
                    String((error && error.message) || error), localAudioTrackLabel());
                showError(strings.processingSwitchFailed ||
                    'The microphone processing change could not be applied on this call.');
            });
        }

        // Reflects the stored processing flags into the settings checkboxes.
        function syncProcessingControls() {
            if (dom.processingEc) {
                dom.processingEc.checked = processingSettings.echoCancellation;
            }

            if (dom.processingNs) {
                dom.processingNs.checked = processingSettings.noiseSuppression;
            }

            if (dom.processingAgc) {
                dom.processingAgc.checked = processingSettings.autoGainControl;
            }

            if (dom.micBoost) {
                dom.micBoost.value = String(micBoostDb);
            }

            if (dom.playoutDelay) {
                dom.playoutDelay.value = String(playoutDelaySeconds);
            }

            if (dom.signalingRegion) {
                dom.signalingRegion.value = signalingRegion;
            }
        }

        // Shows the edge the live registration is actually on. Automatic is the common case and the one worth
        // reporting: it is where the tenant setting, or the provider's own geo-routing, put this agent -- the
        // thing nobody could see before, and the reason a picker exists at all.
        function showSignalingRegion(region, label) {
            if (!dom.signalingRegionStatus) {
                return;
            }

            dom.signalingRegionStatus.textContent = region
                ? (strings.signalingRegionConnected || 'Connected via {0}.').replace('{0}', label)
                : (strings.signalingRegionAutomatic || 'Connected via the region your provider selected.');
        }

        // A processing checkbox changed. Re-capture with the new constraints and swap the track under any live
        // call -- applyConstraints cannot change these on an open capture in Chrome (it is refused or silently
        // ignored), so a fresh capture is the only way, and replaceTrack makes it seamless. This is the A/B an
        // agent needs when a caller says they sound hollow: flip one switch, ask "better?", flip it back.
        function onProcessingChange() {
            processingSettings = {
                echoCancellation: !dom.processingEc || dom.processingEc.checked,
                noiseSuppression: !dom.processingNs || dom.processingNs.checked,
                autoGainControl: !dom.processingAgc || dom.processingAgc.checked
            };
            persistDeviceSelection();

            if (!localAudioStream) {
                return;
            }

            switchLocalAudioTrack('processing changed').catch(function (error) {
                reportDiagnostic('warning', 'processing-switch-failed',
                    String((error && error.message) || error), localAudioTrackLabel());
                showError(strings.processingSwitchFailed ||
                    'The microphone processing change could not be applied on this call.');
            });
        }

        function fillDeviceSelect(select, devices, selectedId, defaultLabel) {
            if (!select) {
                return;
            }

            var html = '<option value="">' + escapeHtml(defaultLabel) + '</option>';

            devices.forEach(function (device, index) {
                var label = device.label || (defaultLabel + ' ' + (index + 1));
                html += '<option value="' + escapeHtml(device.deviceId) + '"' +
                    (device.deviceId === selectedId ? ' selected' : '') + '>' +
                    escapeHtml(label) + '</option>';
            });

            select.innerHTML = html;
            select.value = selectedId || '';
        }

        function outputDeviceSelectionSupported() {
            return !!(dom.remoteAudio && typeof dom.remoteAudio.setSinkId === 'function');
        }

        // Applies the selected output device to the shared remote audio element via setSinkId (feature-detected).
        // An empty id routes back to the system default. The sink sticks on the element across calls, so applying
        // it on selection and after (re)registration is enough.
        function applyOutputDevice() {
            if (!outputDeviceSelectionSupported()) {
                return;
            }

            // A failure here used to vanish: the picker moved, the audio stayed where it was, and nothing said
            // so. Now it is reported, and the agent is told the call keeps its current output.
            Promise.resolve(dom.remoteAudio.setSinkId(selectedOutputDeviceId || '')).then(function () {
                reportDiagnostic('info', 'output-device-applied',
                    'Remote audio routed to the selected output device.', selectedOutputDeviceId || 'default');
                checkForVirtualAudioDevices();
            }, function (error) {
                reportDiagnostic('warning', 'output-device-failed',
                    'The selected output device could not be applied: ' + String((error && error.message) || error),
                    selectedOutputDeviceId || 'default');
                showError(strings.outputDeviceFailed ||
                    'The selected speaker could not be applied. The call keeps its current output.');
            });
        }

        // Warns when the microphone or the speaker is a virtual cable, mixer or loopback rather than real hardware.
        // Such a device looks like any other in the lists and breaks a call in a way that looks like the platform's
        // fault: as the microphone it captures the computer's own playback, so the far end hears itself and nobody's
        // voice is sent; as the speaker it plays the call into the cable, so the agent hears nothing. Observed live
        // on an extension call that was silent both ways. The system default is checked too, because that is how a
        // cable is usually reached: set as the Windows default, never picked in the phone.
        function checkForVirtualAudioDevices() {
            if (typeof isVirtualAudioDevice !== 'function' || !navigator.mediaDevices ||
                typeof navigator.mediaDevices.enumerateDevices !== 'function') {
                return;
            }

            var microphone = localAudioTrackLabel();

            navigator.mediaDevices.enumerateDevices().then(function (devices) {
                var speaker = outputDeviceSelectionSupported()
                    ? resolveDeviceLabel(devices, 'audiooutput', selectedOutputDeviceId)
                    : '';

                if (isVirtualAudioDevice(microphone)) {
                    reportDiagnostic('warning', 'virtual-microphone',
                        'The captured microphone is a virtual audio device, not a microphone.', microphone);
                    showError(strings.virtualMicrophone ||
                        'Your microphone is set to a virtual audio device, not a real microphone, so the caller will not hear you and may hear themselves. Choose your headset or microphone in the phone settings.');

                    return;
                }

                if (isVirtualAudioDevice(speaker)) {
                    reportDiagnostic('warning', 'virtual-speaker',
                        'The selected speaker is a virtual audio device, not a speaker.', speaker);
                    showError(strings.virtualSpeaker ||
                        'Your speaker is set to a virtual audio device, so you will not hear the caller. Choose your headset or speakers in the phone settings.');
                }
            }).catch(function () { /* best effort */ });
        }

        // Enumerates audio devices and fills the pickers. Device labels are only exposed once microphone
        // permission has been granted, so the picker is shown only when labels are known and there is a real
        // choice to make.
        function populateDevicePickers() {
            if (!isBrowserAudioEnabled() || !navigator.mediaDevices ||
                typeof navigator.mediaDevices.enumerateDevices !== 'function') {
                return Promise.resolve();
            }

            return navigator.mediaDevices.enumerateDevices().then(function (devices) {
                // Exclude the "default" and "communications" pseudo-devices: they just alias whatever the system
                // default is (which can be a silent virtual cable), so listing them alongside the real devices
                // only adds confusing duplicates. The "Default microphone" option already covers the system
                // default, and the real, unambiguous devices are what an agent needs to pick to fix a bad default.
                function isRealDevice(device) {
                    return device.deviceId !== 'default' && device.deviceId !== 'communications';
                }

                var inputs = devices.filter(function (device) { return device.kind === 'audioinput' && isRealDevice(device); });
                var outputs = devices.filter(function (device) { return device.kind === 'audiooutput' && isRealDevice(device); });
                var sinkSupported = outputDeviceSelectionSupported();

                fillDeviceSelect(dom.inputDevice, inputs, selectedInputDeviceId, strings.defaultMicrophone || 'Default microphone');

                if (sinkSupported) {
                    fillDeviceSelect(dom.outputDevice, outputs, selectedOutputDeviceId, strings.defaultSpeaker || 'Default speaker');
                }

                if (dom.outputDeviceRow) {
                    dom.outputDeviceRow.hidden = !sinkSupported;
                }
            }).catch(function () { /* best effort */ });
        }

        // Settings overlay (opened from the header gear). It hosts the audio device pickers off the keypad. It is
        // an overlay over the widget body, not a footer tab, so it never adds a tab to the strip.
        function openSettings() {
            settingsOpen = true;
            // Refresh the device lists on open so newly attached devices appear.
            populateDevicePickers();
            render();
        }

        function closeSettings() {
            settingsOpen = false;
            render();
        }

        function toggleSettings() {
            if (settingsOpen) {
                closeSettings();
            } else {
                openSettings();
            }
        }

        function onInputDeviceChange() {
            selectedInputDeviceId = (dom.inputDevice && dom.inputDevice.value) || null;
            persistDeviceSelection();

            // Nothing captured yet: the registration that follows reads the selection when it captures.
            if (!localAudioStream) {
                return;
            }

            // Switch the capture in place, live call or not. Tearing the registration down re-issued the
            // provider credential on every change, and deferring to "the next call" was never actually carried
            // out. Only if the switch fails is the agent told -- with the truth: this call keeps the old device.
            switchLocalAudioTrack('device selected').catch(function (error) {
                reportDiagnostic('warning', 'microphone-switch-failed',
                    String((error && error.message) || error), localAudioTrackLabel());
                showError(strings.microphoneChangeOnNextCall ||
                    'The microphone could not be switched on this call. It will be used on your next call.');
            });
        }

        function onOutputDeviceChange() {
            selectedOutputDeviceId = (dom.outputDevice && dom.outputDevice.value) || null;
            persistDeviceSelection();
            applyOutputDevice();
        }

        function releaseBrowserAudio() {
            browserAudioPromise = null;

            if (browserAudioSession && typeof browserAudioSession.dispose === 'function') {
                Promise.resolve(browserAudioSession.dispose()).catch(function () { });
            }

            browserAudioSession = null;
            stopLocalAudioStream();

            if (dom.remoteAudio) {
                dom.remoteAudio.srcObject = null;
            }
        }

        function setRemoteAudioStream(stream) {
            if (!dom.remoteAudio) {
                return;
            }

            dom.remoteAudio.srcObject = stream || null;

            if (stream && typeof dom.remoteAudio.play === 'function') {
                Promise.resolve(dom.remoteAudio.play()).catch(function () { });
            }
        }

        // The registration config advertises when the provider's browser credential expires. This is a
        // generic part of the SoftPhoneRegistrationConfig contract that each provider fills with its own
        // lifetime, so the renewal below is provider-agnostic: it honors whatever expiry the active provider
        // advertises and does nothing for a provider that advertises no real expiry.
        var BROWSER_AUDIO_HEARTBEAT_MS = 60 * 1000;
        var BROWSER_AUDIO_RENEW_THRESHOLD_MS = 10 * 60 * 1000;
        // A credential whose advertised expiry predates this is treated as carrying no real expiry (an unset
        // or default timestamp) rather than being renewed on every heartbeat.
        var BROWSER_AUDIO_MIN_PLAUSIBLE_EXPIRY_MS = Date.UTC(2000, 0, 1);
        // How close to expiry an ongoing call must be before the agent is warned it may not survive (item 8).
        // The renewal is deferred during a live call because re-registering would drop media, so a call longer
        // than the credential lifetime can lose its registration at expiry; the warning lets the agent redial
        // proactively. It is well inside the renewal threshold so it only fires when renewal has actually been
        // deferred by an in-progress call.
        var BROWSER_AUDIO_EXPIRY_WARNING_MS = 3 * 60 * 1000;
        var credentialExpiryWarned = false;

        function browserAudioExpiryMs(session) {
            var providerConfig = session && session.providerConfig;
            var credential = providerConfig && providerConfig.credential;
            var parsed = credential && credential.expiresAtUtc ? Date.parse(credential.expiresAtUtc) : NaN;

            return isFinite(parsed) ? parsed : null;
        }

        function isBrowserAudioExpiring(session) {
            var expiry = browserAudioExpiryMs(session);

            // No expiry, or an implausible/unset one: the provider does not expire this credential, so it is
            // never renewed.
            if (expiry === null || expiry < BROWSER_AUDIO_MIN_PLAUSIBLE_EXPIRY_MS) {
                return false;
            }

            return (expiry - Date.now()) <= BROWSER_AUDIO_RENEW_THRESHOLD_MS;
        }

        // The provider credential id the session registered with, carried on the registration config so a
        // renewal can revoke the exact credential it supersedes.
        function browserAudioCredentialId(session) {
            var providerConfig = session && session.providerConfig;
            var sessionConfig = providerConfig && providerConfig.session;

            return sessionConfig && sessionConfig.interactionId ? sessionConfig.interactionId : null;
        }

        // Ask the server to tear down a credential this soft phone just superseded during a renewal, so renewed
        // sessions do not leave their predecessor credentials live until natural expiry. Best-effort: a failure
        // only means the old credential lingers until it expires or the per-user cap trims it.
        function revokeSupersededCredential(credentialId) {
            if (!connection || !credentialId) {
                return;
            }

            connection.invoke('RevokeSupersededCredential', credentialId).catch(function () { });
        }

        // Tell the server which credential this client is registered on. Several credentials can be live for
        // one user at once -- a renewal mints a fresh one before its predecessor expires, and a registration
        // that never completes leaves its credential live but unusable -- and only the registered one can
        // receive a call. Without this the server delivers to the newest-issued credential, which Telnyx
        // refuses with SIP 486 when no client is registered on it, so the agent's leg never rings.
        function reportCredentialRegistered(credentialId) {
            if (!connection || !credentialId) {
                return;
            }

            connection.invoke('ReportCredentialRegistered', credentialId).catch(function () { });
        }

        // Tell the server what this client can do on the credential it registered on, so it only rings this browser
        // in ways it understands. A client that reports nothing is rung the way it always was.
        function reportClientCapabilities(credentialId, capabilities) {
            if (!connection || !credentialId || !Array.isArray(capabilities) || !capabilities.length) {
                return;
            }

            connection.invoke('ReportCredentialCapabilities', credentialId, capabilities).catch(function () { });
        }

        function ensureBrowserAudio() {
            if (!isBrowserAudioEnabled()) {
                return Promise.resolve(null);
            }

            var supersededCredentialId = null;

            if (browserAudioSession) {
                // Self-heal on every entry point (placing or answering a call, or becoming available): when
                // the credential is at or near expiry, or the captured microphone has died since it was
                // acquired, re-establish first. Only do so while idle -- re-establishing tears down the
                // provider media client and would drop a live call, so during an active call the current
                // session is kept and the heartbeat renews once it ends.
                //
                // The microphone half matters as much as the credential: a session whose capture has ended is
                // still a perfectly valid session, and reusing it puts the agent on a call nobody can hear
                // them on.
                // The third reason is the agent choosing a different signaling region: the provider fixes the edge
                // when its client is constructed, so a live session cannot be moved -- it has to be rebuilt.
                if ((isBrowserAudioExpiring(browserAudioSession) || isLocalAudioTrackDead() || reregisterOnNextEnsure) &&
                    !hasLiveCall()) {
                    // Remember the credential being replaced so it can be revoked once the fresh one is live.
                    supersededCredentialId = browserAudioCredentialId(browserAudioSession);
                    reregisterOnNextEnsure = false;
                    releaseBrowserAudio();
                } else {
                    return Promise.resolve(browserAudioSession);
                }
            }

            if (browserAudioPromise) {
                return browserAudioPromise;
            }

            var adapter = mediaAdapters[config.browserMediaAdapterName];

            if (typeof adapter !== 'function') {
                return Promise.reject(new Error(strings.browserAudioUnavailable || 'The configured browser audio adapter is unavailable.'));
            }

            if (!navigator.mediaDevices || typeof navigator.mediaDevices.getUserMedia !== 'function') {
                return Promise.reject(new Error(strings.microphoneUnavailable || 'The microphone is unavailable.'));
            }

            browserAudioPromise = connection.invoke('GetCredentials').then(function (credentials) {
                if (!credentials ||
                    normalizeAudioMode(credentials.audioMode) !== AUDIO_MODES.Browser ||
                    credentials.browserMediaAdapterName !== config.browserMediaAdapterName) {
                    throw new Error(strings.browserAudioUnavailable || 'The configured browser audio adapter is unavailable.');
                }

                return navigator.mediaDevices.getUserMedia(buildAudioConstraints()).catch(function (mediaError) {
                    // Turn a permission/device rejection into an actionable, categorized error (item 9).
                    throw categorizeMicError(mediaError);
                }).then(function (stream) {
                    // The capture becomes the send stream through the boost pipeline (a no-op when boost is off).
                    commitCapture(stream, createBoostPipeline(stream, micBoostDb));
                    // This capture has to survive until the registration is replaced, so watch it for the
                    // device dying underneath it rather than discovering it on the next call.
                    watchLocalAudioTrack();
                    // Capture succeeded: clear any prior mic-permission guidance and refresh the device pickers
                    // (labels are only available now that permission has been granted).
                    clearMicPermissionIssue();
                    populateDevicePickers();
                    applyOutputDevice();
                    checkForVirtualAudioDevices();

                    return Promise.resolve(adapter({
                        credentials: credentials,
                        // The send stream, not the raw capture: through the boost when one is set, and a stable
                        // container whose track is swapped on device or processing changes.
                        localStream: localAudioStream,
                        remoteAudioElement: dom.remoteAudio,
                        setRemoteStream: setRemoteAudioStream,
                        showError: showError,
                        // Lets a client-originated provider (Telnyx) decide, per inbound leg, whether to answer
                        // automatically (the agent's own bridged leg) or ring an Answer/Decline prompt (a direct
                        // extension call from a colleague).
                        shouldAutoAnswerInbound: consumeInboundAutoAnswer,
                        // The leg rung for an offer still ringing on screen: held until the agent accepts or
                        // declines, never rung as a call of its own.
                        claimOfferLeg: claimOfferLeg,
                        onOfferLegEnded: handleOfferLegEnded,
                        onInboundRing: handleBrowserInboundRing,
                        onInboundRingCanceled: clearBrowserInboundRing,
                        // Media-quality telemetry (item 2): the adapter samples the live peer connection and
                        // reports periodic/final summaries and a poor-connection signal back through these.
                        reportCallQuality: reportCallQuality,
                        onConnectionQuality: setConnectionQualityPoor,
                        // Degraded-state UX (item 6): the adapter reports when its signaling socket drops and is
                        // reconnecting so the status line can show "Reconnecting..." for a media outage too.
                        onSignalingDegraded: setMediaReconnecting,
                        // Diagnostics: provider SDK warnings (e.g. "Low local microphone audio detected") are
                        // collected for the Diagnostics tab and forwarded to server telemetry.
                        onProviderWarning: addProviderWarning,
                        // The capture went silent mid-call: the microphone is live as far as the browser is
                        // concerned but is delivering nothing, so the caller cannot hear the agent.
                        onCaptureSilent: handleSilentCapture,
                        // The capture format, read at sample time rather than at registration: a Bluetooth
                        // headset changes it when the call takes its microphone, so reading it once up front
                        // would record the format the call is not using.
                        readCaptureSettings: localCaptureSettings,
                        captureDeviceLabel: localAudioTrackLabel,
                        captureBoostLabel: function () { return describeBoost(micBoostDb); },
                        readPlayoutDelay: function () { return playoutDelaySeconds; },
                        // This agent's signaling-edge choice. The adapter resolves it against the tenant setting
                        // it receives in the registration config.
                        readSignalingRegion: function () { return signalingRegion; },
                        // Which edge the registration actually landed on, once resolved.
                        onSignalingRegion: function (region, label) {
                            showSignalingRegion(region, label);
                            reportDiagnostic('info', 'signaling-region',
                                region ? 'Registered on the ' + label + ' signaling edge.'
                                    : 'Registered on the signaling edge chosen by the provider.', '');
                        },
                        onPlayoutDelayUnsupported: function () {
                            reportDiagnostic('info', 'playout-delay-unsupported',
                                'This browser does not support a playout delay hint; the call keeps its own buffering.', '');
                        },
                        // The negotiated SDP at connect, forwarded as a diagnostic so the codecs each side offered
                        // are on the server for the call, next to the webhook and command log for its legs.
                        onNegotiated: function (text) {
                            if (window.console && typeof window.console.info === 'function') {
                                window.console.info('[soft-phone] negotiated ' + text);
                            }

                            reportDiagnostic('info', 'sdp-negotiated', text, localAudioTrackLabel());
                        }
                    }));
                });
            }).then(function (session) {
                browserAudioSession = session || {};
                // A fresh credential is live: reset the one-time expiry warning so a subsequent long call warns
                // again against the new expiry (item 8).
                credentialExpiryWarned = false;

                // Registration completed on this credential, so it is the one the platform must deliver to.
                reportCredentialRegistered(browserAudioCredentialId(browserAudioSession));
                reportClientCapabilities(browserAudioCredentialId(browserAudioSession), browserAudioSession.clientCapabilities);

                // A renewal replaced a still-live credential; revoke that predecessor now that the fresh
                // session is registered (unless, defensively, the server handed back the same credential id).
                if (supersededCredentialId &&
                    supersededCredentialId !== browserAudioCredentialId(browserAudioSession)) {
                    revokeSupersededCredential(supersededCredentialId);
                }

                return browserAudioSession;
            }).catch(function (error) {
                releaseBrowserAudio();

                throw error;
            }).finally(function () {
                browserAudioPromise = null;
            });

            return browserAudioPromise;
        }

        function renewBrowserAudioIfNeeded() {
            if (!isBrowserAudioEnabled() || !browserAudioSession) {
                return;
            }

            // Nothing to do until the credential is near expiry.
            if (!isBrowserAudioExpiring(browserAudioSession)) {
                credentialExpiryWarned = false;

                return;
            }

            // Never renew mid-call (re-establishing would drop the active media session); the renewal is retried
            // on the next heartbeat once the line clears. If the credential is very close to expiring, warn the
            // agent once that a very long call may drop so they can redial proactively (item 8).
            if (hasLiveCall()) {
                maybeWarnCredentialExpiry();

                return;
            }

            // ensureBrowserAudio re-establishes with a freshly issued credential (its self-heal releases the
            // expiring session first). Failures are kept quiet here because this runs in the background; a
            // genuine problem surfaces the next time the agent places or answers a call.
            ensureBrowserAudio().catch(function (error) {
                if (window.console && console.debug) {
                    console.debug('[soft-phone] Browser audio credential renewal failed.', error);
                }
            });
        }

        // Warns the agent, once per credential, when an in-progress call is within the warning window of the
        // credential expiry. The soft phone cannot renew mid-call without dropping media, so the honest
        // mitigation is to tell the agent a very long call may drop and let them redial. The flag resets when a
        // fresh credential is established (ensureBrowserAudio) or the credential is no longer expiring.
        function maybeWarnCredentialExpiry() {
            if (credentialExpiryWarned) {
                return;
            }

            var expiry = browserAudioExpiryMs(browserAudioSession);

            if (expiry === null || (expiry - Date.now()) > BROWSER_AUDIO_EXPIRY_WARNING_MS) {
                return;
            }

            credentialExpiryWarned = true;
            reportDiagnostic('warning', 'credential-expiry-approaching',
                'Active call approaching browser credential expiry', null);
            showError(strings.callCredentialExpiring ||
                'This call is nearing its session limit. If it drops, redial to reconnect.');
        }

        function startBrowserAudioHeartbeat() {
            if (browserAudioHeartbeatTimer || !isBrowserAudioEnabled()) {
                return;
            }

            browserAudioHeartbeatTimer = window.setInterval(renewBrowserAudioIfNeeded, BROWSER_AUDIO_HEARTBEAT_MS);
        }

        function stopBrowserAudioHeartbeat() {
            if (browserAudioHeartbeatTimer) {
                window.clearInterval(browserAudioHeartbeatTimer);
                browserAudioHeartbeatTimer = null;
            }
        }

        // Register the browser with the provider as soon as the soft phone connects -- not lazily on the first
        // dial -- and keep it registered, so an idle agent can still RECEIVE inbound extension/queue calls.
        // Without this the browser only registers for the duration of a call it places (and disconnects right
        // after), so a colleague dialing an idle agent's extension reaches an unregistered endpoint and the
        // call is cleared before it can ring.
        function registerBrowserAudioForInbound() {
            if (!isBrowserAudioEnabled()) {
                return;
            }

            ensureBrowserAudio().catch(function (error) {
                if (window.console && console.debug) {
                    console.debug('[soft-phone] Could not pre-register browser audio for inbound calls.', error);
                }
            });
        }

        function notifyBrowserAudio(call) {
            // Browser-originated calls drive their own SIP session directly; the passive-answer bridging here
            // must not touch them (it would toggle the mic or terminate the live session).
            if (call && call.browserOriginated) {
                return;
            }

            if (!browserAudioSession || !localAudioStream) {
                return;
            }

            var stateName = normalizeState(call && call.state);
            var microphoneEnabled = stateName === 'Connected' && !call.isMuted;

            localAudioStream.getAudioTracks().forEach(function (track) {
                track.enabled = microphoneEnabled;
            });

            if (typeof browserAudioSession.handleCallState === 'function') {
                Promise.resolve(browserAudioSession.handleCallState(call || null)).catch(function (error) {
                    showError(error && error.message ? error.message : String(error));
                });
            }
        }

        function invokeWithBrowserAudio(method, payload) {
            return ensureBrowserAudio().then(function () {
                return invoke(method, payload);
            }).catch(function (error) {
                showError(error && error.message ? error.message : String(error));

                return null;
            });
        }

        // Places an outbound call. When the active provider delivers audio to this browser and expects the
        // browser to originate its own calls (Telnyx), the call is dialed directly from the registered SIP
        // client; otherwise the server places it over the hub as before.
        function placeCall(number, isExtension) {
            // Show "Connecting" immediately so the placed call is visible during the round trip, rather than
            // leaving the status on "Ready" until the browser audio session and the server catch up. The real
            // call state (browser-originated below, or the server's first CallStateChanged) takes over as soon
            // as it arrives, and a failure clears it right away.
            beginPendingDial(number);
            render();

            // A hub command that fails resolves with { succeeded: false } rather than rejecting, so the
            // optimistic "Connecting" state must be cleared here too -- otherwise the status line stays
            // stranded on "Connecting" after an error (for example an unavailable extension) until the safety
            // timer eventually fires.
            function settleDial(result) {
                if (!result || result.succeeded === false) {
                    clearPendingDial();
                    render();
                }

                return result;
            }

            function failDial(error) {
                clearPendingDial();
                render();
                showError(error && error.message ? error.message : String(error));

                return null;
            }

            // Internal extension calls are resolved and bridged server-side, but the caller's own browser must
            // be registered first: the provider rings this browser and then bridges it to the target. Ensure
            // (and renew, if the credential has lapsed) the browser audio session before asking the hub to place
            // the call, so a stale caller credential self-heals instead of failing with "you must be signed in".
            // In a non-browser audio mode ensureBrowserAudio resolves to null and the hub places the call as
            // before.
            if (isExtension) {
                // The provider rings this browser's own leg first and then bridges it to the target; that inbound
                // leg is expected, so arm the media adapter to answer it automatically rather than ring for it.
                armInboundAutoAnswer();

                return ensureBrowserAudio().then(function () {
                    return invoke('DialExtension', { extension: number });
                }).then(settleDial).catch(failDial);
            }

            return ensureBrowserAudio().then(function (session) {
                if (session && session.canOriginate && typeof session.originate === 'function') {
                    originateBrowserCall(session, number);

                    return null;
                }

                return invoke('Dial', { to: number, isExtension: isExtension }).then(settleDial);
            }).catch(failDial);
        }

        function originateBrowserCall(session, number) {
            var callId = 'browser-' + Date.now();
            var call = {
                callId: callId,
                state: 'Connecting',
                direction: 'Outbound',
                to: number,
                from: session.outboundCallerId || '',
                startedUtc: new Date().toISOString(),
                isMuted: false,
                isOnHold: false,
                browserOriginated: true,
                metadata: {}
            };

            upsertActiveCall(call, true);
            render();

            var controller = session.originate(number, session.outboundCallerId, function (stateName) {
                var existing = activeCalls[callId];

                if (!existing) {
                    return;
                }

                if (stateName === 'Connected') {
                    existing.everConnected = true;
                }

                // Preserve a local hold state (the SIP session has no distinct hold signal to the UI).
                if (!(stateName === 'Connected' && existing.isOnHold)) {
                    existing.state = stateName;
                }

                if (stateName === 'Disconnected') {
                    reportBrowserCallEnded(callId, existing.everConnected);
                    removeActiveCall(callId);
                    delete browserCallControllers[callId];
                    // The call ended; if the credential is at/near expiry (a call longer than its lifetime
                    // deferred renewal), renew now rather than waiting for the next heartbeat tick (item 8).
                    renewBrowserAudioIfNeeded();
                } else {
                    upsertActiveCall(existing, false);
                }

                render();
            });

            if (controller) {
                browserCallControllers[callId] = controller;

                // A browser-originated call is placed directly through the provider SDK and never passes through
                // a server "Dial" action, so report it to the hub to record call history -- otherwise it would be
                // missing from the Recent tab. Best-effort: history is not essential to the call itself.
                if (connection) {
                    connection.invoke('RecordBrowserCall', callId, call.to, call.from).catch(function () { });
                }
            } else {
                clearPendingDial();
                removeActiveCall(callId);
                render();
            }
        }

        function currentBrowserController() {
            return currentCall && currentCall.browserOriginated
                ? browserCallControllers[currentCall.callId] || null
                : null;
        }

        // Tell the server a browser-originated call ended, so its history interaction is settled to a final
        // outcome instead of lingering "in progress" (which would keep it out of completed history and let the
        // reconciler delete it as an orphan). Best-effort: history is not essential to the call itself.
        function reportBrowserCallEnded(callId, connected) {
            if (connection && callId) {
                connection.invoke('RecordBrowserCallEnded', callId, !!connected).catch(function () { });
            }
        }

        // Forwards a browser-measured media-quality report (item 2) to the server for observability and
        // alerting. Best-effort: telemetry must never disrupt the call.
        function reportCallQuality(payload) {
            if (payload) {
                lastQualitySample = payload;

                if (diagnosticsEnabled) {
                    updateDiagnosticsReadout();
                }
            }

            if (connection && payload) {
                connection.invoke('ReportCallQuality', payload).catch(function () { });
            }
        }

        // Records the media adapter's poor-connection signal for the degraded-state UX (item 6). A change
        // re-renders so the status line updates promptly, and it debounces internally by ignoring no-op updates.
        function setConnectionQualityPoor(poor) {
            var next = !!poor;

            if (next === connectionQualityPoor) {
                return;
            }

            connectionQualityPoor = next;
            render();
        }

        // Throttled/deduped client diagnostics (item 7). Client-side failures are reported to the server so they
        // become alertable rather than only visible in the agent's console. A given level+code+message is sent at
        // most once per dedupe window, and total volume is capped per minute, so a flapping error cannot flood
        // the hub.
        var DIAGNOSTIC_DEDUPE_MS = 60 * 1000;
        var DIAGNOSTIC_MAX_PER_MINUTE = 20;
        var diagnosticRecent = {};
        var diagnosticWindowStart = 0;
        var diagnosticWindowCount = 0;

        function reportDiagnostic(level, code, message, context) {
            if (!connection) {
                return;
            }

            var now = Date.now();
            var key = (level || '') + '|' + (code || '') + '|' + (message || '');

            if (diagnosticRecent[key] && (now - diagnosticRecent[key]) < DIAGNOSTIC_DEDUPE_MS) {
                return;
            }

            // Rolling per-minute cap across all diagnostics.
            if ((now - diagnosticWindowStart) >= 60000) {
                diagnosticWindowStart = now;
                diagnosticWindowCount = 0;
            }

            if (diagnosticWindowCount >= DIAGNOSTIC_MAX_PER_MINUTE) {
                return;
            }

            diagnosticWindowCount++;
            diagnosticRecent[key] = now;

            // Bound the dedupe map over a long session by dropping entries older than the dedupe window.
            if (Object.keys(diagnosticRecent).length > 200) {
                Object.keys(diagnosticRecent).forEach(function (existing) {
                    if ((now - diagnosticRecent[existing]) >= DIAGNOSTIC_DEDUPE_MS) {
                        delete diagnosticRecent[existing];
                    }
                });
            }

            connection.invoke('ReportClientDiagnostic', level || 'warning', code || 'client-error',
                message || '', context || '').catch(function () { });
        }

        // ---- Gated diagnostics + echo/loopback audio test (companion tooling) ----

        function setDiagStatus(text) {
            if (dom.diagStatus) {
                dom.diagStatus.textContent = text || '';
            }
        }

        // ---- Microphone level meter (Diagnostics tab) ----
        // A live meter of the microphone the soft phone is (or would be) capturing. It reads the actual input
        // level, so a flat bar while speaking immediately reveals a silent/wrong input device -- the exact class
        // of problem that is otherwise painful to diagnose (a virtual-cable default, a muted device, etc.).
        var micMeterCtx = null;
        var micMeterAnalyser = null;
        var micMeterSink = null;
        var micMeterRaf = null;
        var micMeterOwnStream = null;
        var micMeterData = null;

        function startMicMeter() {
            if (!dom.micMeter || !dom.micMeterFill || micMeterCtx) {
                return;
            }

            var AudioCtx = window.AudioContext || window.webkitAudioContext;

            if (!AudioCtx || !navigator.mediaDevices || typeof navigator.mediaDevices.getUserMedia !== 'function') {
                if (dom.micMeterHint) {
                    dom.micMeterHint.textContent = strings.micMeterUnavailable || 'Microphone metering is not available in this browser.';
                }

                return;
            }

            function attach(stream) {
                try {
                    micMeterCtx = new AudioCtx();
                    var source = micMeterCtx.createMediaStreamSource(stream);
                    micMeterAnalyser = micMeterCtx.createAnalyser();
                    micMeterAnalyser.fftSize = 512;
                    source.connect(micMeterAnalyser);

                    // Firefox only processes a graph that reaches the destination, so an analyser left hanging
                    // reads silence forever -- a meter that stays flat no matter how loudly the agent talks,
                    // which is the one reading it exists to rule out. Route it there through a silent gain so
                    // the graph runs without the agent hearing their own microphone.
                    micMeterSink = micMeterCtx.createGain();
                    micMeterSink.gain.value = 0;
                    micMeterAnalyser.connect(micMeterSink);
                    micMeterSink.connect(micMeterCtx.destination);

                    micMeterData = new Float32Array(micMeterAnalyser.fftSize);

                    // Show which device is actually being captured. A wrong/virtual default (for example
                    // "CABLE Output (VB-Audio Virtual Cable)") shows up here immediately, next to a flat meter.
                    // The raw capture carries the device label; the boosted send track does not.
                    var track = getSourceAudioTrack() || stream.getAudioTracks()[0];

                    if (dom.micMeterHint && track) {
                        // A track that has ended or been muted produces exactly the same flat bar as a
                        // microphone nobody is speaking into, so name the difference instead of leaving the
                        // agent to conclude the meter is broken.
                        if (track.readyState !== 'live') {
                            dom.micMeterHint.textContent = strings.micMeterEnded ||
                                'This microphone has stopped and needs to be reconnected.';
                        } else if (track.muted) {
                            dom.micMeterHint.textContent = strings.micMeterMuted ||
                                'This microphone is delivering no audio right now.';
                        } else {
                            dom.micMeterHint.textContent = (strings.micMeterUsing || 'Using:') + ' ' +
                                (track.label || strings.defaultMicrophone || 'Default microphone');
                        }
                    }

                    micMeterTick();
                } catch (error) { /* best effort */ }
            }

            // Prefer the soft phone's own captured mic so the meter reflects exactly what a call uses; when idle
            // and not yet registered, open a dedicated capture with the same constraints (selected device).
            if (localAudioStream) {
                attach(localAudioStream);
            } else {
                navigator.mediaDevices.getUserMedia(buildAudioConstraints()).then(function (stream) {
                    if (activeTab === 'diagnostics' && !micMeterCtx) {
                        micMeterOwnStream = stream;
                        attach(stream);
                    } else {
                        stream.getTracks().forEach(function (track) { track.stop(); });
                    }
                }).catch(function () {
                    if (dom.micMeterHint) {
                        dom.micMeterHint.textContent = strings.micMeterDenied || 'Allow microphone access to see the input level.';
                    }
                });
            }
        }

        function micMeterTick() {
            if (!micMeterAnalyser || !dom.micMeterFill) {
                return;
            }

            micMeterAnalyser.getFloatTimeDomainData(micMeterData);

            var sum = 0;

            for (var i = 0; i < micMeterData.length; i++) {
                sum += micMeterData[i] * micMeterData[i];
            }

            var rms = Math.sqrt(sum / micMeterData.length);
            // Speech RMS is roughly 0..0.3; scale to a 0..100% bar with headroom.
            var pct = Math.min(100, Math.round(rms * 400));
            dom.micMeterFill.style.width = pct + '%';
            dom.micMeterFill.classList.toggle('is-active', pct > 3);
            micMeterRaf = window.requestAnimationFrame(micMeterTick);
        }

        function stopMicMeter() {
            if (micMeterRaf) {
                window.cancelAnimationFrame(micMeterRaf);
                micMeterRaf = null;
            }

            if (micMeterCtx) {
                try {
                    micMeterCtx.close();
                } catch (error) { /* best effort */ }

                micMeterCtx = null;
            }

            micMeterAnalyser = null;
            micMeterSink = null;
            micMeterData = null;

            if (micMeterOwnStream) {
                micMeterOwnStream.getTracks().forEach(function (track) { track.stop(); });
                micMeterOwnStream = null;
            }

            if (dom.micMeterFill) {
                dom.micMeterFill.style.width = '0%';
                dom.micMeterFill.classList.remove('is-active');
            }
        }

        // ---- Provider warnings (Diagnostics tab) ----
        // Warnings the provider SDK raises (for example Telnyx "Low local microphone audio detected") are
        // collected here so they can be seen in the Diagnostics tab and are also forwarded (deduped) to server
        // telemetry, turning a console-only signal into an alertable one.
        var providerWarnings = [];

        function addProviderWarning(warning) {
            if (!warning) {
                return;
            }

            var text = describeProviderWarning(warning);
            // A pause-driven warning (the far end quiet for three seconds) is information, not a fault; it is
            // shown and logged as such so the warnings that do matter are not buried under it.
            var level = classifyProviderWarning(warning);

            providerWarnings.unshift(new Date().toLocaleTimeString() + '  ' + (level === 'info' ? '(info) ' : '') + text);

            if (providerWarnings.length > 20) {
                providerWarnings.length = 20;
            }

            // The captured device goes with it: "Low local microphone audio detected" is only actionable next to
            // the name of the microphone it was detected on.
            reportDiagnostic(level, 'provider-warning', text, localAudioTrackLabel());
            renderProviderWarnings();
        }

        function renderProviderWarnings() {
            if (!dom.diagWarnings || !dom.diagWarningsSection) {
                return;
            }

            dom.diagWarningsSection.hidden = providerWarnings.length === 0;
            dom.diagWarnings.textContent = providerWarnings.join('\n');
        }

        // Renders the most recent media-quality sample (item 2) into the diagnostics panel so live ICE/codec/RTP
        // can be watched from any real session.
        function updateDiagnosticsReadout() {
            if (!dom.diagReadout) {
                return;
            }

            var sample = lastQualitySample;

            if (!sample) {
                dom.diagReadout.textContent = strings.diagNoData || 'No active call to measure.';

                return;
            }

            // Read the payload's own key names: this is the sample as sent, so renaming rttMs to
            // roundTripTimeMs for the server quietly turned the panel's round trip into a dash.
            dom.diagReadout.textContent = [
                'MOS ' + (sample.mos ? sample.mos.toFixed(2) : '-'),
                'loss ' + (sample.lossPercent != null ? sample.lossPercent.toFixed(1) : '-') + '%',
                'jitter ' + (sample.jitterMs != null ? Math.round(sample.jitterMs) : '-') + 'ms',
                'rtt ' + (sample.roundTripTimeMs != null ? Math.round(sample.roundTripTimeMs) : '-') + 'ms',
                // The browser's own playout delay, alongside the network round trip. On a call that feels
                // delayed these two are the whole story, and only one of them was ever shown.
                'buffer ' + (sample.jitterBufferMs >= 0 ? Math.round(sample.jitterBufferMs) + 'ms' : 'n/a'),
                // Read these two together: a shorter buffer is only an improvement while concealment stays low.
                'conceal ' + (sample.concealmentPercent >= 0 ? sample.concealmentPercent.toFixed(1) + '%' : 'n/a'),
                // Measured loudness of each direction. "They sound far away" is this number, in
                // ('in' being what the agent hears) and nothing else.
                'in ' + (sample.inboundLevel >= 0 ? sample.inboundLevel.toFixed(3) : 'n/a'),
                'out ' + (sample.captureProbeLevel >= 0 ? sample.captureProbeLevel.toFixed(3) : 'n/a'),
                // The capture side, next to everything else: a healthy call the far end cannot hear looks
                // perfect on every number except this one. Unmeasured reads as "n/a" rather than as zero.
                'mic ' + (sample.captureReported && sample.microphoneLevel != null
                    ? sample.microphoneLevel.toFixed(3)
                    : 'n/a'),
                'bytesSent ' + (sample.bytesSent != null ? sample.bytesSent : '-'),
                'bytesRecv ' + (sample.bytesReceived != null ? sample.bytesReceived : '-'),
                'codec ' + (sample.codec || '-'),
                'send ' + (sample.sendCodec || '-'),
                // Whether the far end is hearing the soft phone's own capture or a track the provider SDK
                // acquired on its own. "OTHER" here means every microphone setting on this page is describing
                // a track nobody is listening to.
                'sent ' + (sample.sentTrackReported
                    ? (sample.sentTrackIsLocalStream ? 'soft-phone stream' : 'OTHER: ' + (sample.sentTrackLabel || '?'))
                    : 'n/a'),
                'echo ' + (sample.echoStatsReported
                    ? 'erl ' + sample.echoReturnLossDb.toFixed(1) + 'dB erle ' + sample.echoReturnLossEnhancementDb.toFixed(1) + 'dB'
                    : 'n/a'),
                'remote loss ' + (sample.remoteFractionLostPercent >= 0 ? sample.remoteFractionLostPercent.toFixed(1) + '%' : 'n/a'),
                'ice ' + (sample.localCandidateType || '-') + '/' + (sample.remoteCandidateType || '-'),
                // The microphone the call is actually on. An agent wearing a headset whose soft phone is
                // capturing a webcam's far-field array sounds distant and hollow to the far end while
                // everything they hear is perfect -- and nothing else on this line would show it.
                'mic device ' + (localAudioTrackLabel() || '-'),
                // The format that microphone is actually capturing in. A headset that drops to 8 or 16 kHz to
                // free its microphone sounds thin and far away to the far end while every other number here
                // stays perfect, so this is the line to read when a call measures well and sounds wrong.
                'capture ' + (describeCaptureSettings() || '-')
            ].join('  •  ');
        }

        // Dumps the live SDP + getStats for the current call into the diagnostics output on demand.
        function dumpDiagnostics() {
            if (!dom.diagOutput) {
                return;
            }

            if (!browserAudioSession || typeof browserAudioSession.getDiagnostics !== 'function') {
                dom.diagOutput.textContent = strings.diagUnavailable || 'Diagnostics are not available for this provider.';

                return;
            }

            dom.diagOutput.textContent = strings.diagCollecting || 'Collecting...';

            browserAudioSession.getDiagnostics().then(function (diag) {
                if (!diag || !diag.available) {
                    dom.diagOutput.textContent = strings.diagNoData || 'No active call to measure.';

                    return;
                }

                var lines = ['=== getStats ==='];

                (diag.stats || []).forEach(function (stat) {
                    try {
                        lines.push(JSON.stringify(stat));
                    } catch (error) { /* skip unserializable entries */ }
                });

                lines.push('', '=== local SDP ===', diag.localSdp || '(none)', '', '=== remote SDP ===', diag.remoteSdp || '(none)');
                dom.diagOutput.textContent = lines.join('\n');
            }).catch(function () {
                dom.diagOutput.textContent = strings.diagUnavailable || 'Diagnostics are not available.';
            });
        }

        // Runs the echo/loopback audio test: places a call to the configured echo destination and asserts that
        // inbound audio flows back (bytesReceived > 0), so an agent can verify audio without a second person.
        function runEchoTest() {
            if (echoTestActive) {
                return;
            }

            var dest = (browserAudioSession && browserAudioSession.echoTestDestination) || '';

            if (dom.diagEchoInput && dom.diagEchoInput.value && dom.diagEchoInput.value.trim()) {
                dest = dom.diagEchoInput.value.trim();
            }

            if (!dest) {
                setDiagStatus(strings.echoTestNoDestination || 'Configure an audio test destination in the provider settings first.');

                return;
            }

            if (hasBlockingActiveCall()) {
                setDiagStatus(strings.echoTestBusy || 'End the current call before running the audio test.');

                return;
            }

            echoTestActive = true;
            setDiagStatus(strings.echoTestRunning || 'Running audio test...');

            ensureBrowserAudio().then(function (session) {
                if (!session || !session.canOriginate || typeof session.originate !== 'function') {
                    echoTestActive = false;
                    setDiagStatus(strings.echoTestUnavailable || 'The audio test is not available for this provider.');

                    return;
                }

                startEchoTestCall(session, dest);
            }).catch(function (error) {
                echoTestActive = false;
                setDiagStatus((strings.echoTestFailed || 'Audio test failed.') + ' ' +
                    (error && error.message ? error.message : String(error)));
            });
        }

        function startEchoTestCall(session, dest) {
            var settled = false;
            var checkTimer = null;

            function finish(pass, detail) {
                if (settled) {
                    return;
                }

                settled = true;

                if (checkTimer) {
                    window.clearTimeout(checkTimer);
                }

                echoTestActive = false;

                try {
                    if (controller) {
                        controller.terminate();
                    }
                } catch (error) { /* best effort */ }

                var label = pass
                    ? (strings.echoTestPass || 'Audio test passed')
                    : (strings.echoTestFail || 'Audio test failed');
                setDiagStatus(label + (detail ? ' — ' + detail : ''));
            }

            var controller = session.originate(dest, session.outboundCallerId, function (stateName) {
                if (stateName === 'Connected' && !checkTimer) {
                    setDiagStatus(strings.echoTestMeasuring || 'Connected. Measuring round-trip audio...');
                    // Give media a few seconds to flow, then assert inbound audio is arriving.
                    checkTimer = window.setTimeout(function () {
                        checkEchoResult(session, finish);
                    }, 6000);
                } else if (stateName === 'Disconnected') {
                    finish(false, strings.echoTestEndedEarly || 'the call ended before audio was confirmed');
                }
            });

            if (!controller) {
                finish(false, strings.echoTestNoPlace || 'could not place the test call');
            }
        }

        function checkEchoResult(session, finish) {
            if (typeof session.getDiagnostics !== 'function') {
                // Connected but we cannot read stats: treat as an inconclusive pass (the call did connect).
                finish(true, strings.echoTestConnectedNoStats || 'connected (stats unavailable)');

                return;
            }

            session.getDiagnostics().then(function (diag) {
                var bytes = 0;

                (diag.stats || []).forEach(function (stat) {
                    if (stat.type === 'inbound-rtp' && (stat.kind === 'audio' || stat.mediaType === 'audio')) {
                        bytes = stat.bytesReceived || 0;
                    }
                });

                finish(bytes > 0, 'bytesReceived=' + bytes);
            }).catch(function () {
                finish(false, strings.echoTestNoStats || 'could not read audio stats');
            });
        }

        // Arm the one-shot expectation that the next inbound provider leg is one this browser is expecting -- its
        // own bridged leg for an extension call it is placing, or the Contact Center leg for an offer the agent
        // just accepted -- so the media adapter answers it automatically rather than ringing it as an
        // unsolicited incoming call and tearing it down.
        function armInboundAutoAnswer() {
            expectInboundAutoAnswerUntil = Date.now() + INBOUND_AUTO_ANSWER_WINDOW_MS;
        }

        // Called by the media adapter for each inbound provider leg to decide whether to auto-answer. A Contact
        // Center offer the agent just accepted (incomingHandled/incomingAcceptPending), an offer accepted from
        // the docked agent bar rather than from the phone (the armed window), and a freshly placed extension
        // call (also the armed window) are expected legs; all are consumed here so a later, genuine incoming
        // call still rings.
        function consumeInboundAutoAnswer() {
            if (incomingHandled || incomingAcceptPending) {
                return true;
            }

            if (Date.now() < expectInboundAutoAnswerUntil) {
                expectInboundAutoAnswerUntil = 0;

                return true;
            }

            return false;
        }

        // The offer on screen, as the offer-leg rules need it.
        function currentOfferDescriptor() {
            var properties = incomingContext && incomingContext.properties;

            if (!properties || !properties.reservationId) {
                return null;
            }

            return {
                reservationId: properties.reservationId,
                agentLegId: properties.agentLegId || '',
                accepting: incomingAcceptPending || incomingHandled
            };
        }

        // Called by the media adapter for each inbound provider leg. Returns null for a leg that is not an offer's
        // (the usual auto-answer/ring rules apply), otherwise a function the adapter hands the leg's controller to.
        function claimOfferLeg(options) {
            if (typeof readOfferLegTag !== 'function' || typeof classifyOfferLeg !== 'function') {
                return null;
            }

            var decision = classifyOfferLeg(readOfferLegTag(options), currentOfferDescriptor(), acceptedOfferIds, Date.now());

            if (!decision) {
                return null;
            }

            return function (controller) {
                if (decision.action === 'answer') {
                    answerOfferLeg(decision.reservationId, controller);

                    return;
                }

                // One offer rings at a time. A leg still held for an earlier one belongs to an offer that is over.
                if (heldOfferLeg && heldOfferLeg.reservationId !== decision.reservationId) {
                    hangupHeldOfferLeg(heldOfferLeg.reservationId);
                }

                heldOfferLeg = {
                    reservationId: decision.reservationId,
                    legId: controller.legId || '',
                    controller: controller
                };

                reportDiagnostic('info', 'offer-leg-held',
                    'Holding the leg rung for the ringing offer until it is accepted or declined.', decision.reservationId);
            };
        }

        function answerOfferLeg(reservationId, controller) {
            answeredOfferLeg = { reservationId: reservationId, legId: controller.legId || '', controller: controller };
            // The leg an accept is waiting for has arrived; nothing else should be auto-answered on its behalf.
            expectInboundAutoAnswerUntil = 0;

            try {
                controller.answer();
            } catch (error) {
                showError(error && error.message ? error.message : String(error));
            }
        }

        // Answers the held leg of an offer the agent accepted. Returns whether this page was holding it.
        function answerHeldOfferLeg(reservationId) {
            if (!heldOfferLeg || (reservationId && heldOfferLeg.reservationId !== reservationId)) {
                return false;
            }

            var held = heldOfferLeg;
            heldOfferLeg = null;
            answerOfferLeg(held.reservationId, held.controller);

            return true;
        }

        function hangupHeldOfferLeg(reservationId) {
            if (!heldOfferLeg || (reservationId && heldOfferLeg.reservationId !== reservationId)) {
                return;
            }

            var held = heldOfferLeg;
            heldOfferLeg = null;

            try {
                held.controller.hangup();
            } catch (error) { /* best effort: the platform also hangs it up */ }
        }

        // An accept that failed: the leg answered for it is on a call nobody will join.
        function hangupAnsweredOfferLeg(reservationId) {
            hangupHeldOfferLeg(reservationId);

            if (!answeredOfferLeg || (reservationId && answeredOfferLeg.reservationId !== reservationId)) {
                return;
            }

            var answered = answeredOfferLeg;
            answeredOfferLeg = null;

            try {
                answered.controller.hangup();
            } catch (error) { /* best effort */ }
        }

        function handleOfferLegEnded(legId) {
            if (heldOfferLeg && (!legId || heldOfferLeg.legId === legId)) {
                heldOfferLeg = null;
            }

            if (answeredOfferLeg && (!legId || answeredOfferLeg.legId === legId)) {
                answeredOfferLeg = null;
            }
        }

        // Something outside this page settled an offer: another page, another device, or the server. Accepted, its
        // leg is answered here if this page holds it; otherwise it is hung up. Returns whether a held leg was
        // answered.
        function settleOfferLeg(reservationId, accepted) {
            if (!reservationId) {
                return false;
            }

            if (accepted) {
                if (typeof rememberAcceptedOffer === 'function') {
                    rememberAcceptedOffer(acceptedOfferIds, reservationId, Date.now());
                }

                return answerHeldOfferLeg(reservationId);
            }

            if (typeof forgetAcceptedOffer === 'function') {
                forgetAcceptedOffer(acceptedOfferIds, reservationId);
            }

            hangupHeldOfferLeg(reservationId);

            return false;
        }

        // The server says an offer this agent was ringing for was answered -- by this page or any other. The pages
        // that did not answer stop ringing now, rather than when the durable offer update reaches them.
        function handleOfferAnswered(callId, reservationId) {
            var answeredHeldLeg = settleOfferLeg(reservationId, true);
            var ringing = !!(currentCall && isRingingInbound() && (!callId || currentCall.callId === callId));

            // The page that made the accept finishes it itself, and a page another page already told has stopped.
            if (!ringing || incomingAcceptPending || incomingHandled) {
                return;
            }

            // The accept was made elsewhere. When this page is holding no leg for it, the platform may still ring this
            // browser at accept time, and that leg is expected.
            if (!answeredHeldLeg) {
                armInboundAutoAnswer();
            }

            clearIncomingOffer();
        }

        // Surface a ringing direct extension call as an Answer/Decline prompt, reusing the incoming-call modal
        // that Contact Center offers use. There is no server-side offer here (no reservation/accept URL); the
        // agent's choice acts directly on the provider SDK through the adapter controller.
        function handleBrowserInboundRing(controller) {
            if (!controller) {
                return;
            }

            // The agent is already on a (non-ringing) call: decline the new leg rather than interrupting, matching
            // how a server offer is suppressed while a blocking call is active.
            if (hasBlockingActiveCall()) {
                try {
                    controller.decline();
                } catch (e) { /* best effort */ }

                return;
            }

            // Replace any earlier unanswered ring with this one.
            if (browserInboundRing && browserInboundRing.callId !== null) {
                removeActiveCall(browserInboundRing.callId);
            }

            var callId = 'browser-in-' + Date.now();
            browserInboundRing = { callId: callId, controller: controller };

            var call = {
                callId: callId,
                state: 'Ringing',
                direction: 'Inbound',
                from: controller.callerName || controller.callerNumber || '',
                to: '',
                startedUtc: new Date().toISOString(),
                isMuted: false,
                isOnHold: false,
                browserOriginated: true,
                browserInbound: true,
                metadata: {}
            };

            upsertActiveCall(call, true);

            // A direct extension call carries no Contact Center offer context.
            incomingContext = null;
            incomingHandled = false;
            incomingAcceptPending = false;
            setActiveTab('keypad');
            render();
        }

        // Answer the ringing browser-inbound call: hand control to the adapter and wire the SDK call's state back
        // into the active-call UI (Connected/Disconnected), then keep the controller for the in-call buttons.
        function answerBrowserInboundRing() {
            if (!browserInboundRing) {
                return;
            }

            var ring = browserInboundRing;
            browserInboundRing = null;
            incomingHandled = true;

            var existing = activeCalls[ring.callId];

            if (existing) {
                existing.state = 'Connecting';
                upsertActiveCall(existing, true);
            }

            browserCallControllers[ring.callId] = ring.controller;
            togglePanel(true);

            try {
                ring.controller.answer(function (stateName) {
                    var live = activeCalls[ring.callId];

                    if (!live) {
                        return;
                    }

                    if (!(stateName === 'Connected' && live.isOnHold)) {
                        live.state = stateName;
                    }

                    if (stateName === 'Disconnected') {
                        removeActiveCall(ring.callId);
                        delete browserCallControllers[ring.callId];
                        // Renew the credential now if this (possibly very long) answered call outlasted it (item 8).
                        renewBrowserAudioIfNeeded();
                    } else {
                        upsertActiveCall(live, false);
                    }

                    render();
                });
            } catch (error) {
                showError(error && error.message ? error.message : String(error));
                removeActiveCall(ring.callId);
                delete browserCallControllers[ring.callId];
            }

            render();
        }

        // Dismiss the ringing browser-inbound call, optionally declining it on the SDK. Used both when the agent
        // ignores/sends it to voicemail and when the caller cancels before it is answered.
        function clearBrowserInboundRing(options) {
            if (!browserInboundRing) {
                return;
            }

            options = options || {};
            var ring = browserInboundRing;
            browserInboundRing = null;

            if (options.decline) {
                try {
                    ring.controller.decline();
                } catch (e) { /* best effort */ }
            }

            removeActiveCall(ring.callId);
            incomingHandled = false;
            render();
        }

        function isBrowserInboundRinging() {
            return !!(browserInboundRing && currentCall && currentCall.callId === browserInboundRing.callId);
        }

        function showView(name) {
            dom.views.forEach(function (view) {
                show(view, view.getAttribute('data-telephony-view') === name);
            });
        }

        function setBodyVisible(visible) {
            if (dom.body) {
                dom.body.hidden = !visible;
            }
        }

        function syncViewHeight() {
            if (!dom.panel || dom.panel.hidden || !dom.keypadView) {
                return;
            }

            var previousHidden = dom.keypadView.hidden;
            var previousPosition = dom.keypadView.style.position;
            var previousVisibility = dom.keypadView.style.visibility;
            var previousPointerEvents = dom.keypadView.style.pointerEvents;
            var previousInset = dom.keypadView.style.inset;

            if (previousHidden) {
                dom.keypadView.hidden = false;
                dom.keypadView.style.position = 'absolute';
                dom.keypadView.style.inset = '0 auto auto 0';
                dom.keypadView.style.visibility = 'hidden';
                dom.keypadView.style.pointerEvents = 'none';
            }

            var height = Math.ceil(dom.keypadView.getBoundingClientRect().height || dom.keypadView.scrollHeight || 0);

            if (previousHidden) {
                dom.keypadView.hidden = previousHidden;
                dom.keypadView.style.position = previousPosition;
                dom.keypadView.style.inset = previousInset;
                dom.keypadView.style.visibility = previousVisibility;
                dom.keypadView.style.pointerEvents = previousPointerEvents;
            }

            if (height > 0) {
                rootElement.style.setProperty('--telephony-view-height', height + 'px');
            }
        }

        function activeTabExists() {
            return dom.tabs.some(function (tab) {
                return tab.getAttribute('data-telephony-tab') === activeTab;
            });
        }

        function ensureActiveTab() {
            if (activeTabExists()) {
                return;
            }

            activeTab = dom.tabs.length ? dom.tabs[0].getAttribute('data-telephony-tab') : 'keypad';
        }

        function isTelephonyTab(tab) {
            return tab === 'keypad' || tab === 'history' || tab === 'voicemail' || tab === 'diagnostics';
        }

        function hasExtensionTabs() {
            return dom.tabs.some(function (tab) {
                return !isTelephonyTab(tab.getAttribute('data-telephony-tab'));
            });
        }

        // ---- Call timer ----
        // When each active call was first seen connected (ms since epoch), by call id. Kept outside the call
        // objects because the server replaces those on every state refresh.
        var callConnectedAt = {};
        var callTimerInterval = null;

        function currentCallElapsedSeconds() {
            var connectedAt = currentCall && currentCall.callId ? callConnectedAt[currentCall.callId] : null;

            return typeof connectedAt === 'number' ? (Date.now() - connectedAt) / 1000 : null;
        }

        // Ticks the header once a second while a call has a running clock; stops the moment none does. Only the
        // status text is refreshed on each tick -- a full render every second would re-lay out the whole widget.
        function scheduleCallTimer() {
            var running = currentCallElapsedSeconds() !== null;

            if (running && !callTimerInterval) {
                callTimerInterval = window.setInterval(function () {
                    var elapsed = currentCallElapsedSeconds();

                    if (elapsed === null) {
                        scheduleCallTimer();

                        return;
                    }

                    // Reconnect and poor-connection overlays own the header while they last; the clock resumes
                    // with the next render once they clear.
                    var tickState = normalizeState(currentCall && currentCall.state);
                    var tickLive = tickState === 'Connected' || tickState === 'OnHold';

                    if (!hubReconnecting && !mediaReconnecting && !(connectionQualityPoor && tickLive)) {
                        setStatus(formatCallStatus(statusTextForCall(currentCall), elapsed));
                    }
                }, 1000);
            } else if (!running && callTimerInterval) {
                window.clearInterval(callTimerInterval);
                callTimerInterval = null;
            }
        }

        function statusTextForState(stateName) {
            var key = stateName.charAt(0).toLowerCase() + stateName.slice(1);

            return strings[key] || stateName;
        }

        function statusTextForCall(call) {
            if (metadataBoolean(call, 'isConference')) {
                return strings.inConference || 'In conference';
            }

            if (normalizeState(call && call.state) === 'Connecting' &&
                metadataBoolean(call, 'requiresActiveDialpadDevice')) {
                return strings.answerOnDialpadDevice || 'Answer on your Dialpad device...';
            }

            return statusTextForState(normalizeState(call && call.state));
        }

        function getPeerNumber(call) {
            if (!call) {
                return '';
            }

            var inbound = call.direction === 1 || call.direction === 'Inbound';

            if (inbound) {
                return call.from || call.to || '';
            }

            return call.to || call.from || '';
        }

        function metadataBoolean(call, key) {
            if (!call || !call.metadata || !Object.prototype.hasOwnProperty.call(call.metadata, key)) {
                return false;
            }

            var value = call.metadata[key];

            return value === true || value === 1 || value === 'true' || value === 'True';
        }

        function getActiveCalls() {
            return Object.keys(activeCalls).map(function (callId) {
                return activeCalls[callId];
            }).filter(function (call) {
                return call && isActive(normalizeState(call.state));
            }).sort(function (left, right) {
                return Date.parse(right.startedUtc || 0) - Date.parse(left.startedUtc || 0);
            });
        }

        function selectCurrentCall(call) {
            if (!call) {
                currentCall = null;

                return;
            }

            activeCalls[call.callId] = call;
            currentCall = call;
        }

        function removeActiveCall(callId) {
            if (!callId) {
                return;
            }

            delete activeCalls[callId];
            delete conferenceSelections[callId];
            delete callConnectedAt[callId];
            rememberAgentHold(agentHolds, callId, false);

            if (currentCall && currentCall.callId === callId) {
                currentCall = getActiveCalls()[0] || null;
            }
        }

        // Whether a hold on a platform call is performed by this browser, which the provider then cannot see.
        function holdIsPerformedHere() {
            return isBrowserAudioEnabled() && !!browserAudioSession;
        }

        // Applies the agent's own hold to a report of a platform call, so a report that the call is connected does
        // not take the caller off a hold this browser is performing (see soft-phone/agent-hold.js). Browser-placed
        // calls keep their hold on the call object itself and never pass through the server's reports.
        function applyAgentHold(call) {
            if (call.browserOriginated) {
                return;
            }

            // A call already up cannot be ringing again: a report that says so is stale (see
            // resolveReportedState), and is read as the call staying as it was.
            var reportedStateName = normalizeState(call.state);
            var previous = activeCalls[call.callId] || (callsBeforeLookup && callsBeforeLookup[call.callId]);
            var stateName = previous
                ? resolveReportedState(normalizeState(previous.state), reportedStateName)
                : reportedStateName;

            if (stateName !== reportedStateName) {
                reportDiagnostic('info', 'stale-call-state-ignored',
                    'A report that the call is ' + reportedStateName + ' was ignored; the call is already ' + stateName + '.',
                    call.callId);
                call.state = stateName;
            }

            var outcome = reconcileAgentHold(stateName, call.isOnHold, isAgentHeld(agentHolds, call.callId), holdIsPerformedHere());

            rememberAgentHold(agentHolds, call.callId, outcome.agentHeld);

            if (outcome.stateName !== stateName) {
                reportDiagnostic('info', 'agent-hold-kept',
                    'A report that the call is ' + stateName + ' did not end the hold the agent placed.', call.callId);
                call.state = outcome.stateName;
            }

            call.isOnHold = outcome.isOnHold;
        }

        function upsertActiveCall(call, select) {
            if (!call || !call.callId) {
                return;
            }

            applyAgentHold(call);

            var stateName = normalizeState(call.state);

            if (!isActive(stateName)) {
                removeActiveCall(call.callId);

                return;
            }

            activeCalls[call.callId] = call;

            if (select || !currentCall || currentCall.callId === call.callId) {
                currentCall = call;
            }
        }

        // ---- Layout persistence and dragging ----

        function loadLayout() {
            try {
                var layout = JSON.parse(localStorage.getItem(storageKey)) || {};

                if (Object.prototype.hasOwnProperty.call(layout, 'phoneNumber')) {
                    localStorage.removeItem(storageKey);

                    return {};
                }

                return layout;
            } catch (e) {
                return {};
            }
        }

        function saveLayout(patch) {
            try {
                var layout = loadLayout();
                Object.assign(layout, patch);
                localStorage.setItem(storageKey, JSON.stringify(layout));
            } catch (e) {
                // Ignore storage errors (for example private browsing).
            }
        }

        function applyRootPosition(left, top) {
            rootElement.style.left = left + 'px';
            rootElement.style.top = top + 'px';
            rootElement.style.right = 'auto';
            rootElement.style.bottom = 'auto';
        }

        function getAvailablePositionRange() {
            var toggleRect = rootElement.getBoundingClientRect();
            var toggleWidth = toggleRect.width || 56;
            var toggleHeight = toggleRect.height || 56;
            var margin = 8;

            // Keep the toggle on screen so the widget can be dragged to any edge, including the far
            // right and over other widgets such as the AI chat widget.
            var maxLeft = Math.max(margin, window.innerWidth - toggleWidth - margin);
            var maxTop = Math.max(margin, window.innerHeight - toggleHeight - margin);
            var minLeft = margin;
            var minTop = margin;

            if (dom.panel && !dom.panel.hidden) {
                var panelRect = dom.panel.getBoundingClientRect();
                var panelWidth = panelRect.width || toggleWidth;
                var panelHeight = panelRect.height || 0;

                // The panel is anchored to the right of the toggle and floats above it, so it extends
                // to the left and up. Keep the panel within the viewport so its header stays grabbable.
                minLeft = Math.min(maxLeft, Math.max(margin, panelWidth - toggleWidth + margin));
                minTop = Math.min(maxTop, panelHeight + (2.5 * margin));
            }

            return {
                minLeft: minLeft,
                minTop: minTop,
                maxLeft: maxLeft,
                maxTop: maxTop
            };
        }

        function clampPosition(left, top) {
            var range = getAvailablePositionRange();

            return {
                left: clamp(left, range.minLeft, range.maxLeft),
                top: clamp(top, range.minTop, range.maxTop)
            };
        }

        function createStoredPosition(left, top) {
            var range = getAvailablePositionRange();
            var leftSpan = Math.max(0, range.maxLeft - range.minLeft);
            var topSpan = Math.max(0, range.maxTop - range.minTop);

            return {
                left: left,
                top: top,
                leftRatio: leftSpan === 0 ? 0 : (left - range.minLeft) / leftSpan,
                topRatio: topSpan === 0 ? 0 : (top - range.minTop) / topSpan
            };
        }

        function resolveStoredPosition(storedPosition) {
            if (!storedPosition) {
                return null;
            }

            var range = getAvailablePositionRange();
            var left = Number(storedPosition.left);
            var top = Number(storedPosition.top);
            var leftRatio = Number(storedPosition.leftRatio);
            var topRatio = Number(storedPosition.topRatio);

            if (Number.isFinite(leftRatio)) {
                left = range.minLeft + Math.max(0, range.maxLeft - range.minLeft) * leftRatio;
            }

            if (Number.isFinite(topRatio)) {
                top = range.minTop + Math.max(0, range.maxTop - range.minTop) * topRatio;
            }

            if (!Number.isFinite(left) || !Number.isFinite(top)) {
                return null;
            }

            return clampPosition(left, top);
        }

        function persistPosition() {
            var rect = rootElement.getBoundingClientRect();

            saveLayout({
                position: createStoredPosition(rect.left, rect.top)
            });
        }

        function applyDefaultPosition() {
            // Place the soft phone beside the AI chat widget, when present, so they do not overlap.
            var chatToggle = document.querySelector('.ai-chat-widget-toggle');

            if (!chatToggle) {
                return;
            }

            var chatRect = chatToggle.getBoundingClientRect();
            var size = rootElement.getBoundingClientRect();
            var width = size.width || 56;
            var left = chatRect.left - width - 14;

            if (left < 8) {
                left = chatRect.right + 14;
            }

            var position = clampPosition(left, chatRect.top);
            applyRootPosition(position.left, position.top);
        }

        function restoreLayout() {
            var layout = loadLayout();

            if (typeof layout.activeTab === 'string' && layout.activeTab.length) {
                activeTab = layout.activeTab;
            }

            // Embedded in the standalone /softphone page: the phone always renders open and full-window (the page
            // CSS pins it), so ignore the saved floating open/position state -- the phone is not a draggable
            // bubble here, and applying a stored corner position would fight the page layout.
            if (isEmbedded) {
                if (dom.panel) {
                    dom.panel.hidden = false;
                }

                return;
            }

            if (layout.open && dom.panel) {
                dom.panel.hidden = false;
            }

            if (layout.position && isFiniteNumber(Number(layout.position.left))) {
                var position = resolveStoredPosition(layout.position);

                if (position) {
                    applyRootPosition(position.left, position.top);
                }
            } else {
                applyDefaultPosition();
            }
        }

        function restorePosition() {
            var layout = loadLayout();

            if (layout.position) {
                var storedPosition = resolveStoredPosition(layout.position);

                if (storedPosition) {
                    applyRootPosition(storedPosition.left, storedPosition.top);

                    return;
                }
            }

            if (rootElement.style.left) {
                var rect = rootElement.getBoundingClientRect();
                var position = clampPosition(rect.left, rect.top);
                applyRootPosition(position.left, position.top);
            }
        }

        function attachDrag(handle, dragOptions) {
            if (!handle) {
                return;
            }

            dragOptions = dragOptions || {};
            var pointerId = null;
            var startX = 0;
            var startY = 0;
            var startLeft = 0;
            var startTop = 0;
            var dragged = false;

            function onMove(event) {
                if (pointerId === null || event.pointerId !== pointerId) {
                    return;
                }

                var deltaX = event.clientX - startX;
                var deltaY = event.clientY - startY;

                if (!dragged && Math.hypot(deltaX, deltaY) < 4) {
                    return;
                }

                dragged = true;
                var position = clampPosition(startLeft + deltaX, startTop + deltaY);
                applyRootPosition(position.left, position.top);
            }

            function onUp() {
                if (pointerId === null) {
                    return;
                }

                document.removeEventListener('pointermove', onMove);
                document.removeEventListener('pointerup', onUp);
                document.removeEventListener('pointercancel', onUp);
                rootElement.classList.remove('telephony-soft-phone--dragging');
                pointerId = null;

                if (dragged) {
                    persistPosition();

                    if (dragOptions.suppressClick) {
                        suppressToggleClick = true;
                    }
                }
            }

            handle.addEventListener('pointerdown', function (event) {
                if (event.button !== 0) {
                    return;
                }

                if (dragOptions.ignoreButtons && event.target.closest('button, a, input, textarea, select')) {
                    return;
                }

                var rect = rootElement.getBoundingClientRect();
                applyRootPosition(rect.left, rect.top);
                pointerId = event.pointerId;
                dragged = false;
                startX = event.clientX;
                startY = event.clientY;
                startLeft = rect.left;
                startTop = rect.top;
                rootElement.classList.add('telephony-soft-phone--dragging');
                document.addEventListener('pointermove', onMove);
                document.addEventListener('pointerup', onUp);
                document.addEventListener('pointercancel', onUp);
            });
        }

        // ---- Rendering ----

        function updateTabs() {
            dom.tabs.forEach(function (tab) {
                var selected = tab.getAttribute('data-telephony-tab') === activeTab;
                tab.classList.toggle('is-active', selected);
                tab.setAttribute('aria-selected', selected ? 'true' : 'false');
            });
        }

        function persistActiveTab() {
            saveLayout({ activeTab: activeTab });
        }

        function setActiveTab(tab) {
            activeTab = tab;
            persistActiveTab();
            render();

            if (tab === 'history') {
                loadHistory();
            } else if (tab === 'voicemail') {
                // Voicemails stay unread until the caller opens one (clicks its body); opening the list no longer
                // marks them all read.
                loadVoicemails();
            } else if (tab === 'diagnostics') {
                // Start the live microphone level meter and refresh the readout while the tab is open.
                startMicMeter();
                updateDiagnosticsReadout();
            }

            // The mic meter only needs to run while the Diagnostics tab is visible.
            if (tab !== 'diagnostics') {
                stopMicMeter();
            }
        }

        function renderActiveCalls() {
            if (!dom.activeCalls || !dom.activeCallsList) {
                return;
            }

            var calls = getActiveCalls();
            show(dom.activeCalls, calls.length > 1);

            dom.activeCallsList.innerHTML = calls.map(function (call) {
                var callId = call.callId || '';
                var selected = !!conferenceSelections[callId];
                var current = currentCall && currentCall.callId === callId;
                var number = formatPhoneNumber(getPeerNumber(call)) || callId;
                var state = statusTextForCall(call);

                return '<div class="telephony-soft-phone__active-call' + (current ? ' is-current' : '') + '">' +
                    '<input type="checkbox" class="telephony-soft-phone__active-call-check" data-telephony-conference-call="' +
                    escapeHtml(callId) + '"' + (selected ? ' checked' : '') + ' aria-label="' +
                    escapeHtml(strings.conference || 'Conference selected calls') + '" />' +
                    '<button type="button" class="telephony-soft-phone__active-call-select" data-telephony-call-select="' +
                    escapeHtml(callId) + '">' +
                    '<span class="telephony-soft-phone__active-call-number">' + escapeHtml(number) + '</span>' +
                    '<span class="telephony-soft-phone__active-call-state">' + escapeHtml(state) + '</span>' +
                    '</button></div>';
            }).join('');

            Array.prototype.forEach.call(dom.activeCallsList.querySelectorAll('[data-telephony-call-select]'), function (button) {
                button.addEventListener('click', function () {
                    var callId = button.getAttribute('data-telephony-call-select');

                    if (activeCalls[callId]) {
                        selectCurrentCall(activeCalls[callId]);
                        render();
                    }
                });
            });

            Array.prototype.forEach.call(dom.activeCallsList.querySelectorAll('[data-telephony-conference-call]'), function (checkbox) {
                checkbox.addEventListener('change', function () {
                    var callId = checkbox.getAttribute('data-telephony-conference-call');

                    if (checkbox.checked) {
                        conferenceSelections[callId] = true;
                        selectCurrentCall(activeCalls[callId]);
                    } else {
                        delete conferenceSelections[callId];
                    }

                    render();
                });
            });
        }

        function renderDirectory() {
            if (!dom.directory || !dom.directoryList) {
                return;
            }

            show(dom.directory, transferOpen && has(CAPABILITIES.Directory));

            if (!directoryEntries.length) {
                dom.directoryList.innerHTML = '<div class="telephony-soft-phone__directory-empty">' +
                    escapeHtml(strings.directoryEmpty || 'No directory entries are available.') + '</div>';

                return;
            }

            dom.directoryList.innerHTML = directoryEntries.map(function (entry) {
                var destination = entry.destination || entry.extension || entry.phoneNumber || '';
                var detail = entry.extension || entry.phoneNumber || entry.detail || destination;

                return '<button type="button" class="telephony-soft-phone__directory-entry" data-telephony-directory-destination="' +
                    escapeHtml(destination) + '">' +
                    '<span class="telephony-soft-phone__directory-name">' +
                    escapeHtml(entry.displayName || destination) + '</span>' +
                    '<span class="telephony-soft-phone__directory-destination">' + escapeHtml(detail) + '</span></button>';
            }).join('');

            Array.prototype.forEach.call(dom.directoryList.querySelectorAll('[data-telephony-directory-destination]'), function (button) {
                button.addEventListener('click', function () {
                    if (dom.transferInput) {
                        dom.transferInput.value = button.getAttribute('data-telephony-directory-destination') || '';
                        dom.transferInput.focus();
                    }
                });
            });
        }

        function render() {
            renderIncoming();
            ensureActiveTab();

            // The mic-permission Retry affordance (item 9) is shown whenever a permission/device issue is
            // outstanding, independent of the connection state below.
            if (dom.micRetry) {
                show(dom.micRetry, !!micPermissionState);
            }

            // The gated Diagnostics tab is shown only when diagnostics are enabled (site setting or ?diag=1).
            if (dom.diagnosticsTab) {
                show(dom.diagnosticsTab, diagnosticsEnabled);
            }

            // The header gear opens the settings overlay, offered only when the soft phone uses browser audio
            // (the only setting today is audio device selection).
            if (dom.settingsToggle) {
                show(dom.settingsToggle, isBrowserAudioEnabled());
            }

            // The settings overlay replaces the body/footer while open (like the connect panel). The header
            // stays visible so the gear, back, and close controls remain reachable.
            if (settingsOpen && isBrowserAudioEnabled()) {
                show(dom.settingsPanel, true);
                show(dom.connectPanel, false);
                show(dom.unavailable, false);
                setBodyVisible(false);
                show(dom.footer, false);

                return;
            }

            if (dom.settingsPanel) {
                show(dom.settingsPanel, false);
            }

            var stateName = currentCall ? normalizeState(currentCall.state) : 'Idle';
            var active = isActive(stateName);
            var connected = stateName === 'Connected';
            var liveMedia = connected || stateName === 'OnHold';
            var calls = getActiveCalls();
            var canDial = !active || stateName === 'OnHold';
            var selectedConferenceCallIds = Object.keys(conferenceSelections).filter(function (callId) {
                return !!activeCalls[callId];
            });
            var currentIsConference = metadataBoolean(currentCall, 'isConference');

            if (transferOpen && !liveMedia) {
                transferOpen = false;
                directoryEntries = [];
            }

            renderActiveCalls();
            renderDirectory();
            show(dom.transferPanel, transferOpen && liveMedia);
            show(dom.keypadPanel, !transferOpen);

            if (dom.transfer) {
                var transferButtonText = transferOpen
                    ? (strings.keypad || 'Keypad')
                    : (strings.transfer || 'Transfer');

                dom.transfer.title = transferButtonText;
                dom.transfer.setAttribute('aria-label', transferButtonText);
            }

            if (dom.transferIcon) {
                dom.transferIcon.className = transferOpen
                    ? 'fa-solid fa-grip'
                    : 'fa-solid fa-arrow-right-arrow-left';
            }

            if (dom.transferLabel) {
                dom.transferLabel.textContent = transferOpen
                    ? (strings.keypad || 'Keypad')
                    : (strings.transfer || 'Transfer');
            }

            if (dom.toggleIcon) {
                dom.toggleIcon.className = 'fa-solid fa-phone';
            }

            var canDisconnectProvider = connectionStatusResolved &&
                requiresAuthentication &&
                isConnected &&
                isOAuth2Authentication();

            show(dom.disconnect, canDisconnectProvider);

            if (dom.disconnect) {
                dom.disconnect.disabled = !!authActionPending;
            }

            if (dom.connect) {
                dom.connect.disabled = !!authActionPending;
            }

            var notAvailable = connectionStatusResolved && !isAvailable;
            var needsConnect = connectionStatusResolved && isAvailable && requiresAuthentication && !isConnected;

            // Pending: the provider and connection status have not resolved yet. Keep the keypad and the
            // status messages hidden so the widget never briefly flashes the keypad before the real state
            // (unavailable, connect, or operating) is known.
            if (!connectionStatusResolved && !active) {
                show(dom.unavailable, false);
                show(dom.connectPanel, false);
                showView(null);
                setBodyVisible(false);
                show(dom.footer, hasExtensionTabs());
                updateTabs();

                return;
            }

            // Unavailable: no provider configured. Keep contributed tabs reachable.
            if (notAvailable && !active) {
                var showUnavailable = isTelephonyTab(activeTab);

                if (dom.unavailableText) {
                    dom.unavailableText.textContent = strings.notConfigured || 'No telephony provider is configured.';
                }

                setBodyVisible(true);
                show(dom.unavailable, showUnavailable);
                show(dom.connectPanel, false);
                showView(showUnavailable ? null : activeTab);
                show(dom.footer, hasExtensionTabs());
                updateTabs();
                setStatus(strings.notReady || 'Not Ready');
                syncViewHeight();

                return;
            }

            show(dom.unavailable, false);

            // Needs a per-user connection (for example OAuth). Keep contributed tabs reachable. The body is
            // collapsed while the connect panel is shown so the widget keeps its normal height instead of
            // stacking the connect panel above an empty, height-reserving body.
            if (needsConnect && !active) {
                var showConnect = isTelephonyTab(activeTab);

                show(dom.connectPanel, showConnect);
                setBodyVisible(!showConnect);
                showView(showConnect ? null : activeTab);
                show(dom.footer, hasExtensionTabs());
                updateTabs();
                setStatus(strings.notConnected || 'Not connected');

                if (!showConnect) {
                    syncViewHeight();
                }

                return;
            }

            show(dom.connectPanel, false);
            setBodyVisible(true);

            // Operating state: show the footer tabs and the selected view (keypad or recent calls).
            show(dom.footer, true);
            updateTabs();
            showView(activeTab);

            if (currentCall) {
                clearPendingDial();
            }

            var baseStatus = currentCall
                ? statusTextForCall(currentCall)
                : (pendingDial ? (strings.connecting || 'Connecting') : (strings.idle || 'Ready'));

            // Degraded-state overlays (item 6). A hub or media reconnect takes precedence so the agent sees the
            // soft phone is trying to recover; a measured poor connection is surfaced while media is live. Both
            // signals are toggled only on a real state change, so the status does not flap.
            if (hubReconnecting || mediaReconnecting) {
                baseStatus = strings.reconnecting || 'Reconnecting...';
            } else if (connectionQualityPoor && liveMedia) {
                baseStatus = strings.poorConnection || 'Poor connection';
            }

            // The elapsed time of the current call, from the first moment it was seen connected. Recorded here,
            // in the one place every call state passes through -- server-tracked and browser-originated alike --
            // so the clock is the same however the call was placed.
            if (currentCall && currentCall.callId) {
                callConnectedAt[currentCall.callId] = connectedAtFor(
                    stateName === 'Connected' || stateName === 'OnHold',
                    callConnectedAt[currentCall.callId],
                    Date.now());
            }

            setStatus(formatCallStatus(baseStatus, currentCallElapsedSeconds()));
            scheduleCallTimer();

            if (dom.number && currentCall && (active || stateName === 'OnHold')) {
                // The dial has materialized into a real call, so the pending-dial state is done. Clear it now
                // rather than letting its 30s timer keep it alive -- otherwise, once this call ends, render()
                // would fall back into the pendingDial branch and re-show the old number over a fresh entry.
                clearPendingDial();

                var peerNumber = getPeerNumber(currentCall);

                if (peerNumber) {
                    setNumberDisplay(peerNumber);
                    numberIsCallDisplay = true;
                }
            } else if (dom.number && pendingDial && !currentCall) {
                // Mirror the active-call look while the dial is in flight: show the number being connected
                // in the input so the widget does not visibly change when the first real status update
                // arrives. The input is held read-only by the disabled logic below, exactly as during a call.
                if (pendingDialNumber) {
                    setNumberDisplay(pendingDialNumber);
                    numberIsCallDisplay = true;
                }
            } else if (dom.number && canDial && numberIsCallDisplay) {
                clearNumberInput();
                numberIsCallDisplay = false;
            }

            show(dom.dial, canDial && has(CAPABILITIES.Dial));
            // Allow hanging up (cancelling) while the call is still connecting or ringing, not only once media
            // is live, so an outbound call that has not been answered yet can still be ended. A ringing inbound
            // offer is the exception: it is answered or declined through the incoming panel, so the hangup control
            // stays hidden until that call actually connects rather than sitting beside the answer and decline
            // actions.
            var incomingOfferRinging = isRingingInbound() && !incomingHandled;
            show(dom.hangup, active && !incomingOfferRinging && has(CAPABILITIES.Hangup));
            show(dom.hangupAll, calls.length > 1 && has(CAPABILITIES.Hangup));
            show(dom.hold, active && stateName === 'Connected' && has(CAPABILITIES.Hold));
            show(dom.resume, active && stateName === 'OnHold' && has(CAPABILITIES.Resume));

            var muted = currentCall && currentCall.isMuted;
            show(dom.mute, connected && !muted && has(CAPABILITIES.Mute));
            show(dom.unmute, connected && muted && has(CAPABILITIES.Mute));

            show(
                dom.transfer,
                liveMedia &&
                has(CAPABILITIES.Transfer) &&
                (!currentIsConference || selectedConferenceCallIds.length === 1));
            show(dom.merge, selectedConferenceCallIds.length >= 2 && has(CAPABILITIES.Merge));

            if (dom.number) {
                var numberDisabled = !canDial || !!activeCommand || (pendingDial && !currentCall);

                if (telInput && typeof telInput.setDisabled === 'function') {
                    telInput.setDisabled(numberDisabled);
                } else {
                    dom.number.disabled = numberDisabled;
                }

                if (dom.dialModeToggle) {
                    dom.dialModeToggle.disabled = numberDisabled;
                }
            }

            [
                dom.dial,
                dom.hangup,
                dom.hold,
                dom.resume,
                dom.mute,
                dom.unmute,
                dom.transfer,
                dom.merge,
                dom.hangupAll
            ].forEach(function (button) {
                if (button) {
                    button.disabled = !!activeCommand;
                }
            });

            if (dom.merge) {
                dom.merge.disabled = !!activeCommand || selectedConferenceCallIds.length < 2;
            }

            dom.keys.forEach(function (button) {
                button.disabled = (active && stateName !== 'Connected' && stateName !== 'OnHold') || !!activeCommand;
            });

            syncViewHeight();
        }

        // ---- Call operations ----

        function applyCommandResult(result) {
            if (!result) {
                return false;
            }

            if (result.succeeded === false) {
                showError(result.error || (strings.failed || 'Call failed'));

                return false;
            }

            showError(null);

            if (result.call) {
                upsertActiveCall(result.call, true);
                render();
                notifyBrowserAudio(currentCall);
                scheduleActiveCallsRefresh();
            }

            return true;
        }

        function applyActiveCallsLookup(result, expectedRevision) {
            if (!result || result.succeeded === false) {
                return null;
            }

            if (expectedRevision !== callStateRevision) {
                return currentCall;
            }

            var calls = result.calls || [];
            var previousCallId = currentCall ? currentCall.callId : null;

            // The server does not track browser-originated calls, so a server active-calls lookup must not
            // erase them; carry them over so a poll during a live browser call cannot blank the UI.
            var preservedBrowserCalls = Object.keys(activeCalls)
                .map(function (id) { return activeCalls[id]; })
                .filter(function (call) { return call && call.browserOriginated; });

            // The list replaces the calls wholesale, but what the phone already knew about each call still decides how
            // a stale report about it is read.
            callsBeforeLookup = activeCalls;
            activeCalls = {};

            try {
                calls.forEach(function (call) {
                    upsertActiveCall(call, false);
                });

                preservedBrowserCalls.forEach(function (call) {
                    upsertActiveCall(call, false);
                });
            } finally {
                callsBeforeLookup = null;
            }

            // A held call the server no longer reports is over; its hold goes with it.
            pruneAgentHolds(agentHolds, Object.keys(activeCalls));

            currentCall = previousCallId && activeCalls[previousCallId]
                ? activeCalls[previousCallId]
                : getActiveCalls()[0] || null;

            if (!currentCall) {
                incomingHandled = false;
            }

            render();
            notifyBrowserAudio(currentCall);

            // Do NOT tear down the browser registration when the last call ends: the soft phone stays registered
            // with the provider so it can receive the next inbound extension/queue call while idle. The
            // registration is released only on an explicit disconnect/sign-out or when the hub connection closes.

            scheduleActiveCallsRefresh();

            return currentCall;
        }

        function refreshActiveCalls() {
            if (!connection) {
                return Promise.resolve(null);
            }

            var expectedRevision = callStateRevision;

            return connection.invoke('GetActiveCalls').then(function (result) {
                return applyActiveCallsLookup(result, expectedRevision);
            });
        }

        function clearActiveCallsRefresh() {
            if (!activeCallsRefreshTimer) {
                return;
            }

            window.clearTimeout(activeCallsRefreshTimer);
            activeCallsRefreshTimer = null;
        }

        function scheduleActiveCallsRefresh() {
            clearActiveCallsRefresh();

            if (!connection || !getActiveCalls().length) {
                return;
            }

            activeCallsRefreshTimer = window.setTimeout(function () {
                activeCallsRefreshTimer = null;

                refreshActiveCalls().catch(function (error) {
                    showError(error && error.message ? error.message : String(error));
                    scheduleActiveCallsRefresh();
                });
            }, config.activeCallRefreshInterval || 5000);
        }

        function invoke(method, payload) {
            if (!connection) {
                return Promise.reject(new Error('Not connected.'));
            }

            if (activeCommand) {
                return Promise.resolve(null);
            }

            activeCommand = method;
            render();

            return connection.invoke(method, payload).then(function (result) {
                applyCommandResult(result);

                return result;
            }).catch(function (error) {
                showError(error && error.message ? error.message : String(error));

                throw error;
            }).finally(function () {
                activeCommand = null;
                render();
            });
        }

        function currentCallId() {
            return currentCall ? currentCall.callId : null;
        }

        function currentCallReference() {
            var id = currentCallId();

            if (!id) {
                return null;
            }

            return {
                callId: id,
                metadata: currentCall && currentCall.metadata ? currentCall.metadata : null
            };
        }

        function dial() {
            var number = getDialNumber();

            if (extensionMode) {
                // An internal extension call requires an extension to be entered.
                if (!number) {
                    showError(strings.extensionRequired || 'Enter an extension to call.');

                    return;
                }
            } else {
                // A phone call requires a complete, valid number. intl-tel-input validates completeness, so an
                // incomplete entry (for example "702499") is rejected here instead of being dialed as raw digits.
                var canValidate = telInput && typeof telInput.isValidNumber === 'function';

                if ((canValidate && !telInput.isValidNumber()) || !number) {
                    showError(strings.invalidNumber || 'Enter a valid phone number to call.');

                    return;
                }
            }

            clearNumberInput();
            numberIsCallDisplay = false;

            placeCall(number, extensionMode);
        }

        function dialNumber(number, isExtension) {
            if (!number) {
                return;
            }

            setActiveTab('keypad');
            togglePanel(true);

            // Align the dial mode with the entry being called back (setDialMode also clears the input), so an
            // extension entry redials in extension mode and a phone entry in phone mode.
            setDialMode(!!isExtension);
            numberIsCallDisplay = false;

            placeCall(isExtension ? number : normalizeDialNumber(number), !!isExtension);
        }

        function hangup() {
            var controller = currentBrowserController();

            if (controller) {
                Promise.resolve(controller.terminate()).catch(function () { });

                return;
            }

            var call = currentCallReference();

            if (!call) {
                return;
            }

            var callId = call.callId;

            // If the provider cannot end the call -- for example a stale call restored from the server that the
            // provider no longer knows about -- clear it locally so the soft phone recovers instead of staying
            // stuck "in a call", which blocks the keypad (it sends DTMF) and the dialer.
            invoke('Hangup', call).then(function (result) {
                if (result && result.succeeded === false) {
                    removeActiveCall(callId);
                    render();
                }
            }).catch(function () {
                removeActiveCall(callId);
                render();
            });
        }

        function hangupAll() {
            var calls = getActiveCalls();

            // End browser-originated calls directly on their SIP sessions; they have no server-side call.
            calls.filter(function (call) {
                return call.browserOriginated;
            }).forEach(function (call) {
                var controller = browserCallControllers[call.callId];

                if (controller) {
                    Promise.resolve(controller.terminate()).catch(function () { });
                }
            });

            var serverCalls = calls.filter(function (call) {
                return !call.browserOriginated;
            });

            if (!connection || !serverCalls.length || activeCommand) {
                return Promise.resolve(null);
            }

            activeCommand = 'HangupAll';
            render();

            return Promise.all(serverCalls.map(function (call) {
                return connection.invoke('Hangup', {
                    callId: call.callId,
                    metadata: call.metadata || null
                });
            })).then(function (results) {
                results.forEach(applyCommandResult);

                return results;
            }).catch(function (error) {
                showError(error && error.message ? error.message : String(error));

                throw error;
            }).finally(function () {
                activeCommand = null;
                render();
            });
        }

        // Holds or resumes the current call. A call the server tracks always goes through the hub -- the server
        // records the hold time, and its answer drives the hold audio on the leg carrying the call -- and a call this
        // browser placed itself is held on its own session. Hold and resume share this path, so the two can never
        // take different routes for the same call (see holdCommandRoute in soft-phone/agent-hold.js).
        function setCurrentCallHold(onHold) {
            var controller = currentBrowserController();
            var route = holdCommandRoute(currentCall, !!controller);

            if (route === 'local') {
                Promise.resolve(controller.setHold(onHold)).catch(function () { });
                currentCall.isOnHold = onHold;
                currentCall.state = onHold ? 'OnHold' : 'Connected';
                upsertActiveCall(currentCall, true);
                render();

                return;
            }

            var call = route === 'hub' ? currentCallReference() : null;

            if (!call) {
                return;
            }

            if (onHold) {
                // Remembered before the round trip, so a report that crosses it cannot take the caller off hold; a
                // hold the server refused is forgotten again.
                rememberAgentHold(agentHolds, call.callId, true);
                settleHoldCommand(invoke('Hold', call), call.callId, false);

                return;
            }

            // Only the agent ends their own hold. A resume the server refused leaves the call held.
            var wasHeld = isAgentHeld(agentHolds, call.callId);

            rememberAgentHold(agentHolds, call.callId, false);
            settleHoldCommand(invoke('Resume', call), call.callId, wasHeld);
        }

        function hold() {
            setCurrentCallHold(true);
        }

        // Undoes the remembered hold change when the command did not go through.
        function settleHoldCommand(pending, callId, heldIfRefused) {
            Promise.resolve(pending).then(function (result) {
                if (!result || result.succeeded === false) {
                    rememberAgentHold(agentHolds, callId, heldIfRefused);
                }
            }, function () {
                rememberAgentHold(agentHolds, callId, heldIfRefused);
            });
        }

        function resume() {
            setCurrentCallHold(false);
        }

        function mute() {
            var controller = currentBrowserController();

            if (controller) {
                controller.setMute(true);
                currentCall.isMuted = true;
                render();

                return;
            }

            var call = currentCallReference();

            if (call) {
                invoke('Mute', call);
            }
        }

        function unmute() {
            var controller = currentBrowserController();

            if (controller) {
                controller.setMute(false);
                currentCall.isMuted = false;
                render();

                return;
            }

            var call = currentCallReference();

            if (call) {
                invoke('Unmute', call);
            }
        }

        function transfer() {
            var id = currentCallId();

            if (!id) {
                return;
            }

            if (transferOpen) {
                cancelTransfer();

                return;
            }

            if (!has(CAPABILITIES.Directory) || !dom.transferPanel) {
                var destination = window.prompt(strings.transferPrompt || 'Transfer to number');

                if (destination) {
                    invoke('Transfer', { callId: id, to: destination, mode: 0 });
                }

                return;
            }

            transferOpen = true;
            directoryEntries = [];

            if (dom.transferInput) {
                dom.transferInput.value = '';
            }

            render();

            connection.invoke('GetDirectory').then(function (result) {
                if (!result || result.succeeded === false) {
                    showError(result && result.error ? result.error : 'Unable to load the provider directory.');

                    return;
                }

                directoryEntries = result.entries || [];
                render();
            }).catch(function (error) {
                showError(error && error.message ? error.message : String(error));
            });
        }

        function cancelTransfer() {
            transferOpen = false;
            directoryEntries = [];
            render();
        }

        function confirmTransfer() {
            var id = currentCallId();
            var destination = dom.transferInput ? String(dom.transferInput.value || '').trim() : '';

            if (!id || !destination) {
                showError(strings.invalidNumber || 'Enter a phone number to call.');

                return;
            }

            invoke('Transfer', { callId: id, to: destination, mode: 0 }).then(function (result) {
                if (result && result.succeeded !== false) {
                    cancelTransfer();
                }
            });
        }

        function merge() {
            var callIds = Object.keys(conferenceSelections).filter(function (callId) {
                return !!activeCalls[callId];
            });

            if (callIds.length < 2) {
                showError(strings.selectCallsToMerge || 'Select at least two calls to conference.');

                return;
            }

            invoke('Merge', {
                callIds: callIds
            }).then(function (result) {
                if (result && result.succeeded !== false) {
                    callIds.forEach(function (callId) {
                        var call = activeCalls[callId];

                        if (!call) {
                            return;
                        }

                        call.state = 'Connected';
                        call.isOnHold = false;
                        // Joining the conference is the agent taking these calls off hold.
                        rememberAgentHold(agentHolds, callId, false);
                        call.metadata = call.metadata || {};
                        call.metadata.isConference = true;
                        call.metadata.participantCount = callIds.length;
                    });

                    conferenceSelections = {};
                    render();
                }
            });
        }

        function pressKey(value) {
            var stateName = currentCall ? normalizeState(currentCall.state) : 'Idle';

            if (stateName === 'Connected' && has(CAPABILITIES.SendDigits)) {
                invoke('SendDigits', { callId: currentCallId(), digits: value });
            } else if ((!isActive(stateName) || stateName === 'OnHold') && dom.number) {
                // If the field is currently showing a call/pending number (not something the user typed),
                // start a fresh entry so the keypad digit is not appended to -- or immediately overwritten by
                // a background render with -- the previous number. clearNumberInput also drops that state.
                if (numberIsCallDisplay) {
                    clearNumberInput();
                }

                dom.number.value = dom.number.value + value;

                // The number field is enhanced by intl-tel-input, which tracks the number/country from 'input'
                // events. Setting .value directly (a keypad press) bypasses that, leaving its internal state
                // stale so getNumber() returns a wrong E.164 -- the dialed number then differs from what is
                // visible. Fire an input event so intl-tel-input re-reads the current value.
                dom.number.dispatchEvent(new Event('input', { bubbles: true }));
            }
        }

        function togglePanel(open) {
            if (!dom.panel) {
                return;
            }

            var shouldOpen = typeof open === 'boolean' ? open : dom.panel.hidden;
            dom.panel.hidden = !shouldOpen;
            saveLayout({ open: shouldOpen });
            restorePosition();
            render();
        }

        // ---- Incoming call modal ----

        function isRingingInbound() {
            if (!currentCall) {
                return false;
            }

            var inbound = currentCall.direction === 1 || currentCall.direction === 'Inbound';

            return normalizeState(currentCall.state) === 'Ringing' && inbound;
        }

        function hasBlockingActiveCall() {
            return getActiveCalls().some(function (call) {
                var inbound = call.direction === 1 || call.direction === 'Inbound';
                var stateName = normalizeState(call.state);

                return isActive(stateName) && !(stateName === 'Ringing' && inbound);
            });
        }

        function getIncomingReservationId(context) {
            return context && context.properties
                ? context.properties.reservationId || null
                : null;
        }

        function isSameIncomingOffer(call, context) {
            if (!currentCall || !call) {
                return false;
            }

            var currentReservationId = getIncomingReservationId(incomingContext);
            var nextReservationId = getIncomingReservationId(context);

            return currentCall.callId === call.callId &&
                (!currentReservationId || !nextReservationId || currentReservationId === nextReservationId);
        }

        function renderIncoming() {
            var visible = isRingingInbound() && !incomingHandled;

            show(dom.incoming, visible);
            rootElement.classList.toggle('telephony-soft-phone--incoming', visible);

            // Ring while an inbound offer is pending answer (covers both Contact Center offers and direct
            // extension rings, since both surface as a ringing inbound call). start()/stop() are idempotent, so
            // calling them on every render is safe; the ring stops the moment the call is answered, declined,
            // ignored, times out, or the caller hangs up. Answering a Contact Center offer waits on the server
            // to accept it before the call connects, and the agent heard the ringtone carry on through that
            // round trip after clicking Answer, so a pending accept silences it too.
            if (visible && !incomingAcceptPending) {
                ringtone.start();
            } else {
                ringtone.stop();
            }

            if (!visible) {
                clearIncomingExpiryTimer();

                if (!isRingingInbound()) {
                    incomingContext = null;
                }

                return;
            }

            if (dom.incomingCaller) {
                dom.incomingCaller.textContent = getPeerNumber(currentCall) || (strings.incomingCall || 'Incoming call');
            }

            var queueText = incomingContext && incomingContext.properties ? incomingContext.properties.queue : '';

            if (dom.incomingQueue) {
                dom.incomingQueue.textContent = queueText || '';
                dom.incomingQueue.hidden = !queueText;
            }

            show(dom.incomingVoicemail, has(CAPABILITIES.Voicemail));

            // While a Contact Center offer is being accepted the accept is a server round-trip; disable the
            // offer controls so the agent gets instant feedback and cannot act on the offer again mid-flight.
            setIncomingControlsBusy(incomingAcceptPending);

            renderIncomingCards();
            scheduleIncomingExpiry();
        }

        function setIncomingControlsBusy(busy) {
            [dom.incomingAnswer, dom.incomingVoicemail, dom.incomingIgnore].forEach(function (button) {
                if (button) {
                    button.disabled = busy;
                    button.classList.toggle('is-busy', busy);
                }
            });
        }

        function clearIncomingExpiryTimer() {
            if (incomingExpiryTimer) {
                window.clearTimeout(incomingExpiryTimer);
                incomingExpiryTimer = null;
            }
        }

        function scheduleIncomingExpiry() {
            clearIncomingExpiryTimer();

            if (!incomingContext || !incomingContext.properties || !incomingContext.properties.expiresUtc) {
                return;
            }

            var expiresAt = Date.parse(incomingContext.properties.expiresUtc);

            if (!isFinite(expiresAt)) {
                return;
            }

            var remainingMs = expiresAt - Date.now();

            if (remainingMs <= 0) {
                clearIncomingOffer();

                return;
            }

            incomingExpiryTimer = window.setTimeout(function () {
                clearIncomingOffer();
            }, remainingMs + 250);
        }

        function renderIncomingCards() {
            if (!dom.incomingCards) {
                return;
            }

            var cards = incomingContext && incomingContext.cards ? incomingContext.cards : [];

            if (!cards.length) {
                dom.incomingCards.innerHTML = '';
                dom.incomingCards.hidden = true;

                return;
            }

            var html = '';
            var heading = (incomingContext && incomingContext.heading) || strings.matchedRecords;

            if (heading) {
                html += '<div class="telephony-incoming__cards-heading">' + escapeHtml(heading) + '</div>';
            }

            cards.forEach(function (card) {
                html += buildIncomingCard(card);
            });

            dom.incomingCards.innerHTML = html;
            dom.incomingCards.hidden = false;

            Array.prototype.forEach.call(dom.incomingCards.querySelectorAll('[data-telephony-card-answer]'), function (button) {
                button.addEventListener('click', function () {
                    answerIncoming(button.getAttribute('data-url'));
                });
            });
        }

        function buildIncomingCard(card) {
            var icon = card.icon ? '<span class="telephony-incoming__card-icon"><i class="' + escapeHtml(card.icon) + '"></i></span>' : '';
            var body = '<div class="telephony-incoming__card-title">' + escapeHtml(card.title || '') + '</div>';

            if (card.subtitle) {
                body += '<div class="telephony-incoming__card-subtitle">' + escapeHtml(card.subtitle) + '</div>';
            }

            if (card.description) {
                body += '<div class="telephony-incoming__card-desc">' + escapeHtml(card.description) + '</div>';
            }

            if (card.badges && card.badges.length) {
                body += '<div class="telephony-incoming__card-badges">';

                card.badges.forEach(function (badge) {
                    body += '<span class="badge bg-secondary">' + escapeHtml(badge) + '</span>';
                });

                body += '</div>';
            }

            if (card.links && card.links.length) {
                body += '<div class="telephony-incoming__card-links">';

                card.links.forEach(function (link) {
                    if (link && link.url) {
                        var linkIcon = link.icon ? '<i class="' + escapeHtml(link.icon) + '"></i> ' : '';
                        var target = link.openInNewTab ? ' target="_blank" rel="noopener"' : '';
                        body += '<a href="' + escapeHtml(link.url) + '"' + target + '>' + linkIcon + escapeHtml(link.text || link.url) + '</a>';
                    }
                });

                body += '</div>';
            }

            var actions = '';

            if (card.url) {
                var openTarget = card.openInNewTab ? ' target="_blank" rel="noopener"' : '';
                var answerBusy = incomingAcceptPending ? ' disabled' : '';
                actions += '<button type="button" class="btn btn-sm btn-success" data-telephony-card-answer data-url="' + escapeHtml(card.url) + '"' + answerBusy + '><i class="fa-solid fa-phone"></i> ' + escapeHtml(strings.answerAndOpen || 'Answer & open') + '</button>';
                actions += '<a class="btn btn-sm btn-outline-secondary" href="' + escapeHtml(card.url) + '"' + openTarget + '><i class="fa-solid fa-up-right-from-square"></i> ' + escapeHtml(strings.open || 'Open') + '</a>';
            }

            return '<div class="telephony-incoming__card">' + icon +
                '<div class="telephony-incoming__card-body">' + body + '</div>' +
                (actions ? '<div class="telephony-incoming__card-actions">' + actions + '</div>' : '') +
                '</div>';
        }

        function postLifecycle(key) {
            if (!incomingContext || !incomingContext.properties) {
                return Promise.resolve(null);
            }

            var url = incomingContext.properties[key];

            if (!url) {
                return Promise.resolve(null);
            }

            var headers = { 'Content-Type': 'application/json' };

            if (config.antiForgeryToken) {
                headers['RequestVerificationToken'] = config.antiForgeryToken;
            }

            try {
                return fetch(url, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: headers,
                    body: JSON.stringify({ callId: currentCallId() })
                }).then(function (response) {
                    if (!response.ok) {
                        return { succeeded: false };
                    }

                    return response.json().catch(function () { return { succeeded: true }; });
                }).catch(function () {
                    return { succeeded: false };
                });
            } catch (e) {
                return Promise.resolve({ succeeded: false });
            }
        }

        function answerIncoming(openUrl) {
            var id = currentCallId();

            // Accepting a Contact Center offer is a server round-trip; ignore repeat clicks while one is in
            // flight so the reservation is never accepted twice.
            if (incomingAcceptPending) {
                return;
            }

            // A direct browser-inbound (extension) call has no server-side offer; it is answered on the provider
            // SDK through the adapter controller, not via a server "Answer" command.
            if (isBrowserInboundRinging()) {
                answerBrowserInboundRing();

                return;
            }

            if (isBrowserAudioEnabled() && !browserAudioSession) {
                ensureBrowserAudio().then(function () {
                    answerIncoming(openUrl);
                }).catch(function (error) {
                    showError(error && error.message ? error.message : String(error));
                });

                return;
            }

            if (openUrl) {
                window.open(openUrl, '_blank', 'noopener');
            }

            var hasOffer = incomingContext && incomingContext.properties && incomingContext.properties.acceptUrl;

            // A plain telephony call with no Contact Center offer: answer the device directly.
            if (!hasOffer) {
                if (id) {
                    togglePanel(true);
                    invokeWithBrowserAudio('Answer', { callId: id });
                }

                return;
            }

            // A Contact Center offer: the server-side accept must succeed (accept the reservation and
            // connect the media) before the device answers, so the same live call is never answered here
            // while it is being re-offered to another agent.
            //
            // The one exception is the leg the platform already rang for this offer, held since the offer appeared.
            // It is answered now, alongside the accept rather than after it: answering it proves only that this
            // browser picked up, and the platform joins it to the caller only once the accept has succeeded.
            var offerReservationId = incomingContext.properties.reservationId || '';

            if (offerReservationId && typeof rememberAcceptedOffer === 'function') {
                rememberAcceptedOffer(acceptedOfferIds, offerReservationId, Date.now());
            }

            answerHeldOfferLeg(offerReservationId);
            incomingAcceptPending = true;
            announceOfferHandled(true, offerReservationId);

            // Reflect the pending accept immediately so the offer controls disable while the accept round-trips,
            // instead of appearing clickable until the next server status update arrives.
            render();

            postLifecycle('acceptUrl').then(function (result) {
                if (!result || result.succeeded === false) {
                    if (typeof forgetAcceptedOffer === 'function') {
                        forgetAcceptedOffer(acceptedOfferIds, offerReservationId);
                    }

                    hangupAnsweredOfferLeg(offerReservationId);
                    showError(strings.offerUnavailable || 'This call is no longer available.');
                    incomingContext = null;
                    removeActiveCall(id);
                    render();

                    return;
                }

                incomingHandled = true;
                incomingContext = null;
                togglePanel(true);
                render();

                // Only answer on the device when the provider delivers media to the agent's device
                // (agent-device-native). For server-side ACD the provider bridges the call, so no device
                // answer is required.
                if (result.requiresDeviceAnswer !== false && id) {
                    invokeWithBrowserAudio('Answer', { callId: id });
                }
            }).finally(function () {
                incomingAcceptPending = false;
            });
        }

        function voicemailIncoming() {
            // A direct extension call: decline the local leg. The server's no-answer handling routes the caller to
            // the extension owner's voicemail from the destination-leg hangup, so there is nothing more to do here.
            if (isBrowserInboundRinging()) {
                clearBrowserInboundRing({ decline: true });

                return;
            }

            var call = currentCallReference();
            var declinedReservationId = incomingContext && incomingContext.properties
                ? incomingContext.properties.reservationId || ''
                : '';

            settleOfferLeg(declinedReservationId, false);
            announceOfferHandled(false, declinedReservationId);
            postLifecycle('declineUrl');

            if (call) {
                invoke('Voicemail', call);
            }
        }

        function ignoreIncoming() {
            // A direct extension call has no server offer to decline; hang up the ringing leg on the SDK.
            if (isBrowserInboundRinging()) {
                clearBrowserInboundRing({ decline: true });

                return;
            }

            var call = currentCallReference();
            var hasOffer = incomingContext && incomingContext.properties && incomingContext.properties.declineUrl;

            if (hasOffer) {
                var ignoredReservationId = incomingContext.properties.reservationId || '';

                settleOfferLeg(ignoredReservationId, false);
                announceOfferHandled(false, ignoredReservationId);
                postLifecycle('declineUrl').then(function (result) {
                    if (!result || result.succeeded === false) {
                        showError(strings.offerUnavailable || 'This call is no longer available.');

                        return;
                    }

                    clearIncomingOffer();
                });

                return;
            }

            if (call) {
                invoke('Reject', call);
            }
            else {
                clearIncomingOffer();
            }
        }

        function setIncomingOffer(call, context) {
            if (!call) {
                return;
            }

            if (hasBlockingActiveCall() && currentCallId() !== call.callId) {
                return;
            }

            if (incomingHandled && isSameIncomingOffer(call, context)) {
                return;
            }

            upsertActiveCall(call, true);
            incomingContext = context || null;
            incomingHandled = false;
            incomingAcceptPending = false;
            setActiveTab('keypad');
            render();
            maybeAutoAnswerRequestedOffer(call);
        }

        // Fires the extension's one-shot "answer from the OS notification" handoff: when the just-surfaced offer
        // is the exact call the agent already chose to answer (its call id matches the ?answerCallId the page was
        // opened with), answer it automatically through the same path the incoming modal's Answer button uses.
        // Consumed once regardless of outcome so a later, unrelated call is never auto-answered.
        function maybeAutoAnswerRequestedOffer(call) {
            if (!pendingAnswerCallId || !call || call.callId !== pendingAnswerCallId) {
                return;
            }

            // Match found: burn the one-shot now so nothing else can retrigger it.
            pendingAnswerCallId = '';

            // Too long has passed since the page was opened to still trust this as the same ring the agent
            // accepted; leave it ringing for the agent to answer manually instead of connecting on its own.
            if (pendingAnswerDeadline && Date.now() > pendingAnswerDeadline) {
                return;
            }

            answerIncoming(null);
        }

        function clearIncomingOffer(options) {
            options = options || {};
            clearIncomingExpiryTimer();
            incomingContext = null;
            incomingHandled = false;
 
            if (!options.preservePendingAccept) {
                incomingAcceptPending = false;
            }

            if (!options.preserveCurrentCall && currentCall && isRingingInbound()) {
                removeActiveCall(currentCall.callId);
            }

            render();
        }

        // ---- Connection status and authentication ----

        function refreshConnectionStatus() {
            if (!connection) {
                return Promise.resolve();
            }

            return connection.invoke('GetConnectionStatus').then(function (status) {
                if (status) {
                    isAvailable = !!status.isAvailable;
                    requiresAuthentication = !!status.requiresAuthentication;
                    isConnected = !!status.isConnected;
                    authenticationScheme = status.authenticationScheme || 'oauth2';
                    connectionStatusResolved = true;
                    render();
                }
            }).catch(function () {
                // Leave the default unavailable state when the hub call fails.
            });
        }

        function refreshCapabilities() {
            if (!connection) {
                return Promise.resolve();
            }

            return connection.invoke('GetCapabilities').then(function (value) {
                if (typeof value === 'number') {
                    capabilities = value;
                    render();
                }
            }).catch(function () {
                // Keep the capabilities provided in the configuration when the hub call fails.
            });
        }

        function showConnectError(message) {
            if (!dom.connectError) {
                return;
            }

            if (message) {
                dom.connectError.textContent = message;
                dom.connectError.hidden = false;
            } else {
                dom.connectError.textContent = '';
                dom.connectError.hidden = true;
            }
        }

        function startOAuth() {
            if (!config.connectUrl) {
                showConnectError(strings.connectUnavailable || 'The connection could not be started.');

                return;
            }

            showConnectError(null);

            var separator = config.connectUrl.indexOf('?') >= 0 ? '&' : '?';
            var url = config.connectUrl + separator + 'returnUrl=' + encodeURIComponent(window.location.pathname + window.location.search);
            var popup = window.open(url, 'telephony-oauth');

            if (popup) {
                popup.focus();

                return;
            }

            // The pop-up was blocked. Rather than silently failing, navigate the current window so the
            // user can still complete the authorization, and surface guidance to allow pop-ups.
            showConnectError(strings.connectPopupBlocked || 'Your browser blocked the connection window.');
            window.location.href = url;
        }

        function postToProviderUrl(url) {
            if (!url) {
                return Promise.resolve({ succeeded: false });
            }

            var headers = {};

            if (config.antiForgeryToken) {
                headers.RequestVerificationToken = config.antiForgeryToken;
            }

            try {
                return fetch(url, {
                    method: 'POST',
                    credentials: 'same-origin',
                    headers: headers
                }).then(function (response) {
                    if (!response.ok) {
                        return { succeeded: false };
                    }

                    return response.json().catch(function () { return { succeeded: true }; });
                }).catch(function () {
                    return { succeeded: false };
                });
            } catch (e) {
                return Promise.resolve({ succeeded: false });
            }
        }

        function handleConnect() {
            var handlers = window.telephonySoftPhone && window.telephonySoftPhone.authHandlers;
            var handler = handlers && (handlers[authenticationScheme] || handlers.oauth2);

            var context = {
                scheme: authenticationScheme,
                connectUrl: config.connectUrl,
                startOAuth: startOAuth,
                refreshStatus: refreshConnectionStatus
            };

            if (typeof handler === 'function') {
                handler(context);
            } else {
                startOAuth();
            }
        }

        function handleDisconnect() {
            if (authActionPending) {
                return;
            }

            if (hasLiveCall()) {
                showError(strings.disconnectActiveCalls || 'End active calls before disconnecting from the provider.');

                return;
            }

            if (window.confirm && !window.confirm(strings.disconnectConfirm || 'Disconnect your provider account from the soft phone?')) {
                return;
            }

            authActionPending = true;
            showError(null);
            showConnectError(null);
            render();

            postToProviderUrl(config.disconnectUrl).then(function (result) {
                if (!result || result.succeeded === false) {
                    showError(strings.disconnectFailed || 'The provider could not be disconnected. Please try again.');

                    return null;
                }

                releaseBrowserAudio();

                return refreshConnectionStatus().then(function () {
                    showConnectError(result.message || null);
                });
            }).catch(function () {
                showError(strings.disconnectFailed || 'The provider could not be disconnected. Please try again.');
            }).finally(function () {
                authActionPending = false;
                render();
            });
        }

        function onOAuthMessage(event) {
            if (event.origin !== window.location.origin || !event.data || event.data.type !== 'telephony-oauth') {
                return;
            }

            if (event.data.success) {
                showConnectError(null);
                refreshConnectionStatus();
            } else {
                showConnectError(event.data.error || strings.connectFailed || 'The connection could not be completed. Please try again.');
            }
        }

        // ---- History ----

        // Reloads whichever list tab is currently open so a just-ended call (a new recent call, or a new voicemail)
        // appears without a manual refresh.
        function reloadActiveListTab() {
            if (activeTab === 'history') {
                loadHistory();
            } else if (activeTab === 'voicemail') {
                loadVoicemails();
            }
        }

        function loadHistory() {
            if (!connection) {
                renderHistory([]);

                return;
            }

            connection.invoke('GetInteractions', config.recentCallsCount || 30).then(function (items) {
                renderHistory(items || []);
            }).catch(function () {
                renderHistory([]);
            });

            refreshVoicemailBadge();
        }

        function isInbound(interaction) {
            return interaction.direction === 1 || interaction.direction === 'Inbound';
        }

        function isMissed(interaction) {
            return interaction.outcome === 2 || interaction.outcome === 'Missed' ||
                interaction.outcome === 3 || interaction.outcome === 'Rejected';
        }

        function isVoicemail(interaction) {
            return interaction.isVoicemail === true;
        }

        function voicemailPlaybackEnabled() {
            return config.voicemailPlaybackEnabled === true && !!config.voicemailMediaUrlTemplate && !!dom.voicemailAudio;
        }

        function buildVoicemailUrl(interactionId) {
            if (!interactionId || !config.voicemailMediaUrlTemplate) {
                return null;
            }

            return config.voicemailMediaUrlTemplate.replace('__INTERACTION_ID__', encodeURIComponent(interactionId));
        }

        var playingVoicemailButton = null;
        var playingVoicemailId = null;

        function stopVoicemailPlayback() {
            if (dom.voicemailAudio) {
                try {
                    dom.voicemailAudio.pause();
                } catch (e) { /* ignore */ }

                // Detach the clip so a deleted or gone voicemail cannot stay loaded in the player.
                dom.voicemailAudio.removeAttribute('src');

                try {
                    dom.voicemailAudio.load();
                } catch (e) { /* ignore */ }
            }

            if (playingVoicemailButton) {
                playingVoicemailButton.classList.remove('is-playing');
                playingVoicemailButton = null;
            }

            playingVoicemailId = null;

            // The player bar only makes sense while a voicemail is loaded; remove it once playback is torn down.
            if (dom.voicemailPlayer) {
                dom.voicemailPlayer.hidden = true;
            }
        }

        function playVoicemail(interactionId, callId, button) {
            if (!voicemailPlaybackEnabled()) {
                return;
            }

            var url = buildVoicemailUrl(interactionId);

            if (!url) {
                return;
            }

            // Clicking the currently-playing voicemail toggles it off.
            if (playingVoicemailButton === button && dom.voicemailAudio && !dom.voicemailAudio.paused) {
                dom.voicemailAudio.pause();

                return;
            }

            stopVoicemailPlayback();
            showError(null);

            if (dom.voicemailPlayer) {
                dom.voicemailPlayer.hidden = false;
            }

            // Surface which voicemail is playing in the player bar, so the caller's number and time stay visible
            // while the native audio controls drive playback (the audio element itself shows neither).
            if (dom.voicemailPlayerInfo) {
                var row = button ? button.closest('.telephony-soft-phone__voicemail-item') : null;
                var numberText = row ? row.querySelector('.telephony-soft-phone__history-number') : null;
                var metaText = row ? row.querySelector('.telephony-soft-phone__history-meta') : null;

                dom.voicemailPlayerInfo.innerHTML =
                    '<span class="telephony-soft-phone__history-number">' +
                    escapeHtml(numberText ? numberText.textContent : (strings.voicemailLabel || 'Voicemail')) + '</span>' +
                    '<span class="telephony-soft-phone__history-meta">' +
                    escapeHtml(metaText ? metaText.textContent : '') + '</span>';
            }

            dom.voicemailAudio.src = url;
            playingVoicemailButton = button;
            playingVoicemailId = interactionId;

            if (button) {
                button.classList.add('is-playing');
            }

            // The native <audio controls> element drives playback (scrub/rewind). A load failure surfaces through
            // its 'error' event; play() may still reject on some browsers, which is handled the same way.
            Promise.resolve(dom.voicemailAudio.play()).catch(function () { });
        }

        function handleVoicemailAudioError() {
            var processing = dom.voicemailAudio &&
                dom.voicemailAudio.error &&
                dom.voicemailAudio.error.code === 4;

            stopVoicemailPlayback();

            showError(processing
                ? (strings.voicemailUnavailable || 'This voicemail is still processing. Try again in a moment.')
                : (strings.voicemailPlaybackFailed || 'The voicemail could not be played.'));
        }

        function loadVoicemails() {
            if (!connection || !dom.voicemailList) {
                renderVoicemails([]);

                return;
            }

            connection.invoke('GetInteractions', config.recentCallsCount || 30).then(function (items) {
                renderVoicemails((items || []).filter(isVoicemail));
            }).catch(function () {
                renderVoicemails([]);
            });
        }

        function voicemailDeleteEnabled() {
            return config.voicemailDeleteEnabled === true && !!config.voicemailDeleteUrlTemplate && !!dom.voicemailDelete;
        }

        function buildVoicemailDeleteUrl(interactionId) {
            if (!interactionId || !config.voicemailDeleteUrlTemplate) {
                return null;
            }

            return config.voicemailDeleteUrlTemplate.replace('__INTERACTION_ID__', encodeURIComponent(interactionId));
        }

        function selectedVoicemailIds() {
            if (!dom.voicemailList) {
                return [];
            }

            return Array.prototype.slice.call(dom.voicemailList.querySelectorAll('[data-telephony-voicemail-select]:checked'))
                .map(function (checkbox) { return checkbox.getAttribute('data-telephony-voicemail-id'); })
                .filter(function (id) { return !!id; });
        }

        function updateVoicemailDeleteButton() {
            var checkboxes = dom.voicemailList
                ? dom.voicemailList.querySelectorAll('[data-telephony-voicemail-select]')
                : [];
            var selectedCount = selectedVoicemailIds().length;

            if (dom.voicemailDelete) {
                dom.voicemailDelete.disabled = selectedCount === 0;
            }

            // Keep the header "select all" box in sync: checked when every row is selected, a dash (indeterminate)
            // when only some are.
            if (dom.voicemailSelectAll) {
                dom.voicemailSelectAll.checked = checkboxes.length > 0 && selectedCount === checkboxes.length;
                dom.voicemailSelectAll.indeterminate = selectedCount > 0 && selectedCount < checkboxes.length;
            }
        }

        function toggleSelectAllVoicemails() {
            if (!dom.voicemailList || !dom.voicemailSelectAll) {
                return;
            }

            var checked = dom.voicemailSelectAll.checked;

            Array.prototype.forEach.call(dom.voicemailList.querySelectorAll('[data-telephony-voicemail-select]'), function (checkbox) {
                checkbox.checked = checked;
            });

            updateVoicemailDeleteButton();
        }

        function renderVoicemails(items) {
            if (!dom.voicemailList) {
                return;
            }

            // If the voicemail currently loaded in the player is no longer in the list (deleted or otherwise
            // gone), tear the player down so it cannot strand an orphaned clip over an empty or changed list.
            if (playingVoicemailId && !items.some(function (interaction) {
                return interaction.interactionId === playingVoicemailId;
            })) {
                stopVoicemailPlayback();
            }

            var canDelete = voicemailDeleteEnabled();

            if (dom.voicemailToolbar) {
                dom.voicemailToolbar.hidden = !(canDelete && items.length);
            }

            updateVoicemailDeleteButton();

            if (!items.length) {
                dom.voicemailList.innerHTML = '<div class="telephony-soft-phone__history-empty">' +
                    escapeHtml(strings.noVoicemails || 'No voicemails.') + '</div>';

                return;
            }

            dom.voicemailList.innerHTML = items.map(function (interaction) {
                var number = interaction.from || '';
                var formattedNumber = formatPhoneNumber(number);
                var time = formatTime(interaction.startedUtc);
                var unread = !interaction.voicemailReadUtc;
                var cls = 'telephony-soft-phone__voicemail-item' +
                    (unread ? ' telephony-soft-phone__voicemail-item--unread' : '');

                var selectMarkup = canDelete
                    ? '<input type="checkbox" class="telephony-soft-phone__voicemail-select" data-telephony-voicemail-select ' +
                        'data-telephony-voicemail-id="' + escapeHtml(interaction.interactionId || '') + '" ' +
                        'aria-label="' + escapeHtml(strings.selectVoicemail || 'Select voicemail') + '">'
                    : '';

                return '<div class="' + cls + '">' +
                    selectMarkup +
                    '<button type="button" class="telephony-soft-phone__voicemail-play" data-telephony-voicemail-play ' +
                    'data-telephony-voicemail-id="' + escapeHtml(interaction.interactionId || '') + '" ' +
                    'data-telephony-voicemail-call="' + escapeHtml(interaction.callId || '') + '" ' +
                    'title="' + escapeHtml(strings.playVoicemail || 'Play voicemail') + '" ' +
                    'aria-label="' + escapeHtml(strings.playVoicemail || 'Play voicemail') + '"><i class="fa-solid fa-play"></i></button>' +
                    '<div class="telephony-soft-phone__voicemail-body" data-telephony-voicemail-body ' +
                    'data-telephony-voicemail-call="' + escapeHtml(interaction.callId || '') + '" ' +
                    'role="button" tabindex="0">' +
                    '<span class="telephony-soft-phone__history-number">' + escapeHtml(formattedNumber || number || (strings.voicemailLabel || 'Voicemail')) + '</span>' +
                    '<span class="telephony-soft-phone__history-meta">' + escapeHtml(time) + '</span>' +
                    '</div>' +
                    '<button type="button" class="telephony-soft-phone__call-btn" data-telephony-history-number="' + escapeHtml(number) + '" ' +
                    'title="' + escapeHtml(strings.callBack || 'Call back') + '" aria-label="' + escapeHtml(strings.callBack || 'Call back') + '"><i class="fa-solid fa-phone"></i></button>' +
                    '</div>';
            }).join('');

            Array.prototype.forEach.call(dom.voicemailList.querySelectorAll('[data-telephony-voicemail-play]'), function (item) {
                item.addEventListener('click', function () {
                    playVoicemail(
                        item.getAttribute('data-telephony-voicemail-id'),
                        item.getAttribute('data-telephony-voicemail-call'),
                        item);
                });
            });

            Array.prototype.forEach.call(dom.voicemailList.querySelectorAll('[data-telephony-voicemail-select]'), function (checkbox) {
                checkbox.addEventListener('change', updateVoicemailDeleteButton);
            });

            // Clicking (or keyboard-activating) the voicemail body -- the caller/time area, distinct from the
            // play and call-back icons -- marks that voicemail as read.
            Array.prototype.forEach.call(dom.voicemailList.querySelectorAll('[data-telephony-voicemail-body]'), function (body) {
                function activate() {
                    markVoicemailRead(
                        body.getAttribute('data-telephony-voicemail-call'),
                        body.closest('.telephony-soft-phone__voicemail-item'));
                }

                body.addEventListener('click', activate);
                body.addEventListener('keydown', function (event) {
                    if (event.key === 'Enter' || event.key === ' ') {
                        event.preventDefault();
                        activate();
                    }
                });
            });

            Array.prototype.forEach.call(dom.voicemailList.querySelectorAll('[data-telephony-history-number]'), function (item) {
                item.addEventListener('click', function () {
                    var number = item.getAttribute('data-telephony-history-number');

                    if (number) {
                        dialNumber(number);
                    }
                });
            });
        }

        function deleteSelectedVoicemails() {
            if (!voicemailDeleteEnabled()) {
                return;
            }

            var ids = selectedVoicemailIds();

            if (!ids.length) {
                return;
            }

            if (dom.voicemailDelete) {
                dom.voicemailDelete.disabled = true;
            }

            // Stop any playback of a voicemail that is about to be deleted.
            stopVoicemailPlayback();

            var headers = {};

            if (config.antiForgeryToken) {
                headers['RequestVerificationToken'] = config.antiForgeryToken;
            }

            var deletions = ids.map(function (id) {
                var url = buildVoicemailDeleteUrl(id);

                if (!url) {
                    return Promise.resolve();
                }

                return fetch(url, { method: 'POST', headers: headers });
            });

            Promise.all(deletions).then(function () {
                loadVoicemails();
                refreshVoicemailBadge();
            }).catch(function () {
                showError(strings.voicemailDeleteFailed || 'The voicemail could not be deleted.');
                loadVoicemails();
                refreshVoicemailBadge();
            });
        }

        function markAllVoicemailsRead() {
            if (!connection) {
                return;
            }

            connection.invoke('MarkAllVoicemailsRead').then(function (remaining) {
                updateVoicemailBadge(remaining);
            }).catch(function () { });
        }

        function markVoicemailRead(callId, itemElement) {
            // Reflect the read state immediately so the row stops showing as unread even before the round trip.
            if (itemElement) {
                itemElement.classList.remove('telephony-soft-phone__voicemail-item--unread');
            }

            if (!connection || !callId) {
                return;
            }

            connection.invoke('MarkVoicemailRead', callId).then(function (remaining) {
                updateVoicemailBadge(remaining);
            }).catch(function () { });
        }

        function updateVoicemailBadge(count) {
            if (!dom.voicemailBadge) {
                return;
            }

            var value = typeof count === 'number' && count > 0 ? count : 0;

            if (value > 0) {
                dom.voicemailBadge.textContent = value > 99 ? '99+' : String(value);
                dom.voicemailBadge.hidden = false;
            } else {
                dom.voicemailBadge.textContent = '';
                dom.voicemailBadge.hidden = true;
            }
        }

        function refreshVoicemailBadge() {
            if (!connection || !dom.voicemailBadge) {
                return;
            }

            connection.invoke('GetUnreadVoicemailCount').then(function (count) {
                updateVoicemailBadge(count);
            }).catch(function () { });
        }

        function isInProgress(interaction) {
            return interaction.outcome === 0 || interaction.outcome === 'InProgress';
        }

        function restoreActiveCall() {
            if (!connection) {
                return Promise.resolve();
            }

            return refreshActiveCalls().catch(function () { });
        }

        function formatTime(value) {
            try {
                var date = new Date(value);
                return isNaN(date.getTime()) ? '' : date.toLocaleString();
            } catch (e) {
                return '';
            }
        }

        function renderHistory(items) {
            if (!dom.historyList) {
                return;
            }

            if (!items.length) {
                dom.historyList.innerHTML = '<div class="telephony-soft-phone__history-empty">' +
                    escapeHtml(strings.noInteractions || 'No recent calls.') + '</div>';

                return;
            }

            // Voicemails live in their own tab; the Recent list is calls only.
            dom.historyList.innerHTML = items.filter(function (interaction) {
                return !isVoicemail(interaction);
            }).map(function (interaction) {
                var inbound = isInbound(interaction);
                var missed = isMissed(interaction);
                var inProgress = isInProgress(interaction);
                var isExtension = !!interaction.isExtension;
                var directionGlyph = inbound ? '\u2199' : '\u2197';
                var number = inbound ? (interaction.from || '') : (interaction.to || '');
                // An extension entry stores the target's display name in `to`; the dialable value is the stored
                // extension number, so the call-back button redials that (in extension mode) rather than the name.
                var dialTarget = isExtension ? (interaction.extensionNumber || '') : number;
                var label = missed ? (strings.missed || 'Missed') : (inbound ? (strings.incoming || 'Incoming') : (strings.outgoing || 'Outgoing'));
                var time = formatTime(interaction.startedUtc);
                var extensionTag = isExtension ? escapeHtml(strings.extensionLabel || 'Extension') + ' \u2022 ' : '';
                var meta = extensionTag + escapeHtml(label) + (time ? ' \u2022 ' + escapeHtml(time) : '');
                // The call's total length, once it has one. A call that never connected or is still going
                // shows none rather than a misleading zero.
                var duration = durationMeta(interaction.durationSeconds, inProgress);

                if (duration) {
                    meta += ' \u2022 ' + escapeHtml(duration);
                }
                // Extension targets are display names, not numbers, so they are shown verbatim; phone numbers are
                // run through the display formatter.
                var displayNumber = escapeHtml(isExtension ? (number || dialTarget || label) : (formatPhoneNumber(number) || number || label));
                var cls = 'telephony-soft-phone__history-item' +
                    (missed ? ' telephony-soft-phone__history-item--missed' : '') +
                    (inProgress ? ' telephony-soft-phone__history-item--active' : '');

                // The row itself no longer dials; a dedicated Call button on the right places the call.
                return '<div class="' + cls + '">' +
                    '<span class="telephony-soft-phone__history-dir" aria-hidden="true">' + directionGlyph + '</span>' +
                    '<div class="telephony-soft-phone__history-body">' +
                    '<span class="telephony-soft-phone__history-number">' + displayNumber + '</span>' +
                    '<span class="telephony-soft-phone__history-meta">' + meta + '</span>' +
                    '</div>' +
                    (dialTarget
                        ? '<button type="button" class="telephony-soft-phone__call-btn" data-telephony-history-number="' + escapeHtml(dialTarget) + '"' +
                            (isExtension ? ' data-telephony-history-extension="true"' : '') + ' ' +
                            'title="' + escapeHtml(strings.call || 'Call') + '" aria-label="' + escapeHtml(strings.call || 'Call') + '"><i class="fa-solid fa-phone"></i></button>'
                        : '') +
                    '</div>';
            }).join('');

            Array.prototype.forEach.call(dom.historyList.querySelectorAll('[data-telephony-history-number]'), function (item) {
                item.addEventListener('click', function () {
                    var number = item.getAttribute('data-telephony-history-number');
                    var isExtension = item.getAttribute('data-telephony-history-extension') === 'true';

                    if (number) {
                        dialNumber(number, isExtension);
                    }
                });
            });
        }

        // ---- SignalR ----

        function registerClientCallbacks() {
            if (!connection) {
                return;
            }

            connection.on('CallStateChanged', function (call) {
                callStateRevision++;

                var isTerminal = !call ||
                    normalizeState(call.state) === 'Disconnected' ||
                    normalizeState(call.state) === 'Failed';

                if (isTerminal) {
                    // A terminal state from the server for a call THIS browser placed is bookkeeping, not the call
                    // ending: the platform never saw the call and cannot know when it ends -- the provider SDK
                    // reports that through the media adapter, which is the only authority for these calls. The
                    // one thing the server can say about a browser-originated call is that it stopped tracking
                    // its history entry, and acting on that as a hang-up is how a reconciliation sweep dropped
                    // every keypad call that outlived the next minute: the entry was removed, this ran, the call
                    // was taken out of the active list, and with nothing left in it the session was torn down
                    // under a live conversation.
                    var terminatedBrowserCall = call && call.callId ? activeCalls[call.callId] : null;

                    if (terminatedBrowserCall && terminatedBrowserCall.browserOriginated) {
                        reportDiagnostic('info', 'browser-call-server-terminal-ignored',
                            'The server reported a terminal state for a browser-originated call that is still live; ignored.',
                            call.callId);

                        return;
                    }

                    if (!call || !call.callId) {
                        // Keep browser-originated calls; this server signal is about server-tracked calls only.
                        var keptBrowserCalls = Object.keys(activeCalls)
                            .map(function (id) { return activeCalls[id]; })
                            .filter(function (existing) { return existing && existing.browserOriginated; });

                        activeCalls = {};
                        conferenceSelections = {};

                        keptBrowserCalls.forEach(function (existing) {
                            activeCalls[existing.callId] = existing;
                        });

                        pruneAgentHolds(agentHolds, Object.keys(activeCalls));

                        currentCall = getActiveCalls()[0] || null;
                        incomingHandled = false;
                    } else {
                        var tracked = !!activeCalls[call.callId];
                        removeActiveCall(call.callId);

                        if (!currentCall) {
                            incomingHandled = false;
                        }

                        if (!tracked) {
                            refreshActiveCalls().catch(function (error) {
                                showError(error && error.message ? error.message : String(error));
                            });
                        }
                    }

                    render();
                    notifyBrowserAudio(call);

                    // A call that just ended updates the recent calls and may have been sent to voicemail, so refresh
                    // the unread badge and reload whichever list tab is open (Recent or Voicemail) so the new entry
                    // appears without a manual refresh. The projection that creates the entry can land a moment after
                    // this terminal signal, so reload once now and once shortly after to catch that case.
                    refreshVoicemailBadge();
                    reloadActiveListTab();
                    setTimeout(function () {
                        reloadActiveListTab();
                        refreshVoicemailBadge();
                    }, 2000);

                    if (!getActiveCalls().length) {
                        releaseBrowserAudio();
                        clearActiveCallsRefresh();
                    } else {
                        scheduleActiveCallsRefresh();
                    }

                    return;
                }

                upsertActiveCall(call, true);
                render();
                notifyBrowserAudio(call);
                scheduleActiveCallsRefresh();
            });

            connection.on('IncomingCall', function (call, context) {
                setIncomingOffer(call, context || null);
            });

            connection.on('IncomingCallAnswered', function (notification) {
                if (notification) {
                    handleOfferAnswered(notification.callId || '', notification.offerId || '');
                }
            });

            connection.on('ReceiveError', function (message) {
                showError(message);
            });

            connection.on('DialRequested', function (request) {
                // A call was started from outside the phone (for example, the "call" button next to a phone-number
                // field). Place it on this soft phone. dialNumber registers first when needed and holds an active
                // call, so presence is left untouched here.
                var number = request && request.number ? String(request.number).trim() : '';

                if (number) {
                    dialNumber(number);
                }
            });

            connection.on('CredentialsIssued', function () { });

            if (typeof connection.onreconnecting === 'function') {
                connection.onreconnecting(function () {
                    // SignalR is transparently retrying a dropped connection. Surface the degraded state (item 6
                    // layers a debounced degraded-state manager over this same status line) instead of leaving a
                    // stale "Ready" while calls cannot be placed or received.
                    setHubReconnecting(true);
                });
            }

            connection.onclose(function () {
                clearActiveCallsRefresh();
                stopBrowserAudioHeartbeat();
                // Do NOT release browser audio here: the provider media session (and any live call) is a
                // separate connection to the provider and must survive a transient hub outage -- tearing it down
                // would drop an in-progress call and de-register the agent for inbound. The manual restart loop
                // below brings the hub back and re-runs setup; browser audio is only released on sign-out/unload.
                setHubReconnecting(true);
                scheduleManualReconnect();
            });

            if (typeof connection.onreconnected === 'function') {
                connection.onreconnected(function () {
                    manualReconnectAttempt = 0;

                    return afterConnected();
                });
            }
        }

        // Degraded-state setters (item 6). render() derives the "Reconnecting..." status from these; toggling
        // only on a real change keeps the status line from flapping.
        function setHubReconnecting(active) {
            if (hubReconnecting === !!active) {
                return;
            }

            hubReconnecting = !!active;
            render();
        }

        function setMediaReconnecting(active) {
            if (mediaReconnecting === !!active) {
                return;
            }

            mediaReconnecting = !!active;
            render();
        }

        // Re-runs the full post-connect setup after the hub connection is (re-)established, whether by the
        // initial connect, SignalR's automatic reconnect, or the manual restart loop. Each step is idempotent:
        // startBrowserAudioHeartbeat no-ops if already running, and registerBrowserAudioForInbound dedupes
        // through ensureBrowserAudio, so running it more than once cannot double-register or double-start.
        function afterConnected() {
            showError(null);
            // The hub is back: clear the reconnecting overlay (render() below restores the real status).
            setHubReconnecting(false);
            startBrowserAudioHeartbeat();
            // Register with the provider now that the soft phone is connected, so the agent is reachable for
            // inbound calls while idle -- not only while placing a call.
            registerBrowserAudioForInbound();

            return Promise.all([refreshCapabilities(), refreshConnectionStatus()])
                .then(function () {
                    return restoreActiveCall();
                })
                .then(function () {
                    if (activeTab === 'history') {
                        loadHistory();
                    } else if (activeTab === 'voicemail') {
                        loadVoicemails();
                    }

                    refreshVoicemailBadge();
                    render();
                })
                .catch(function (error) {
                    showError(error && error.message ? error.message : String(error));
                });
        }

        // Manual restart loop for when the hub connection closes for good. SignalR's automatic reconnect only
        // covers a connection that started successfully and then dropped, and it does not retry an initial start
        // that never succeeded. This loop guarantees the soft phone keeps trying to reconnect indefinitely (same
        // backoff as the automatic policy) so an agent is never stranded unable to receive calls until a reload.
        function scheduleManualReconnect() {
            if (manualReconnectTimer || pageUnloading || !connection) {
                return;
            }

            var delay = reconnectDelayMs(manualReconnectAttempt);
            manualReconnectAttempt++;
            setHubReconnecting(true);

            manualReconnectTimer = window.setTimeout(function () {
                manualReconnectTimer = null;

                connection.start().then(function () {
                    manualReconnectAttempt = 0;

                    return afterConnected();
                }).catch(function () {
                    // Still down: keep trying. The status stays on "Reconnecting...".
                    scheduleManualReconnect();
                });
            }, delay);
        }

        function connect() {
            if (!signalRFactory || !config.hubUrl) {
                render();

                return Promise.resolve();
            }

            connection = new signalRFactory.HubConnectionBuilder()
                .withUrl(config.hubUrl)
                .withAutomaticReconnect({
                    // Never return null: retry with a backoff that caps at ~30s but never gives up, so a hub
                    // connection lost for longer than SignalR's default schedule still recovers on its own
                    // without a page reload. previousRetryCount is 0 on the first retry.
                    nextRetryDelayInMilliseconds: function (retryContext) {
                        return reconnectDelayMs(retryContext.previousRetryCount);
                    }
                })
                .build();

            registerClientCallbacks();

            return connection.start().then(function () {
                manualReconnectAttempt = 0;

                return afterConnected();
            }).catch(function (error) {
                showError(error && error.message ? error.message : String(error));
                // The initial start failed (for example the server was briefly unreachable at page load).
                // SignalR's automatic reconnect does not cover a start that never succeeded, so kick off the
                // manual restart loop to keep trying instead of leaving the soft phone permanently offline.
                scheduleManualReconnect();
            });
        }

        function bindEvents() {
            document.addEventListener('visibilitychange', function () {
                // Background tabs throttle timers, so the heartbeat can be delayed while hidden. Re-check the
                // credential the moment the agent brings the soft phone back to the foreground.
                if (!document.hidden) {
                    renewBrowserAudioIfNeeded();
                }
            });

            if (dom.toggle) {
                dom.toggle.addEventListener('click', function () {
                    if (suppressToggleClick) {
                        suppressToggleClick = false;

                        return;
                    }

                    togglePanel();
                });
            }

            if (dom.close) {
                dom.close.addEventListener('click', function () { togglePanel(false); });
            }

            dom.tabs.forEach(function (tab) {
                tab.addEventListener('click', function () {
                    setActiveTab(tab.getAttribute('data-telephony-tab'));
                });
            });

            if (dom.voicemailDelete) {
                dom.voicemailDelete.addEventListener('click', deleteSelectedVoicemails);
            }

            if (dom.voicemailSelectAll) {
                dom.voicemailSelectAll.addEventListener('change', toggleSelectAllVoicemails);
            }

            if (dom.dial) {
                dom.dial.addEventListener('click', dial);
            }

            if (dom.micRetry) {
                dom.micRetry.addEventListener('click', retryMicrophone);
            }

            if (dom.settingsToggle) {
                dom.settingsToggle.addEventListener('click', toggleSettings);
            }

            if (dom.settingsBack) {
                dom.settingsBack.addEventListener('click', closeSettings);
            }

            if (dom.inputDevice) {
                dom.inputDevice.addEventListener('change', onInputDeviceChange);
            }

            if (dom.outputDevice) {
                dom.outputDevice.addEventListener('change', onOutputDeviceChange);
            }

            [dom.processingEc, dom.processingNs, dom.processingAgc].forEach(function (control) {
                if (control) {
                    control.addEventListener('change', onProcessingChange);
                }
            });

            if (dom.micBoost) {
                dom.micBoost.addEventListener('change', onBoostChange);
            }

            if (dom.playoutDelay) {
                dom.playoutDelay.addEventListener('change', onPlayoutDelayChange);
            }

            if (dom.signalingRegion) {
                dom.signalingRegion.addEventListener('change', onSignalingRegionChange);
            }

            if (dom.processingEc || dom.processingNs || dom.processingAgc || dom.micBoost || dom.playoutDelay ||
                dom.signalingRegion) {
                syncProcessingControls();
            }

            if (navigator.mediaDevices && typeof navigator.mediaDevices.addEventListener === 'function') {
                // Re-enumerate when devices are plugged in or removed so the picker stays current (item 5).
                navigator.mediaDevices.addEventListener('devicechange', function () {
                    populateDevicePickers();
                });
            }

            if (dom.diagRun) {
                dom.diagRun.addEventListener('click', runEchoTest);
            }

            if (dom.diagDump) {
                dom.diagDump.addEventListener('click', dumpDiagnostics);
            }

            if (dom.dialModeToggle) {
                dom.dialModeToggle.addEventListener('click', toggleDialMode);
            }

            if (dom.number) {
                dom.number.addEventListener('input', function () {
                    numberIsCallDisplay = false;
                    // Clear a transient error (for example "Enter a phone number to call.") as soon as the
                    // user starts entering a number.
                    showError(null);
                });
                dom.number.addEventListener('focus', function () {
                    if (currentCall && normalizeState(currentCall.state) === 'OnHold') {
                        dom.number.select();
                    }
                });
                dom.number.addEventListener('keydown', function (event) {
                    if (event.key !== 'Enter' || event.isComposing) {
                        return;
                    }

                    event.preventDefault();

                    if ((!currentCall || normalizeState(currentCall.state) === 'OnHold') && !activeCommand) {
                        dial();
                    }
                });
            }

            if (dom.hangup) {
                dom.hangup.addEventListener('click', hangup);
            }

            if (dom.hangupAll) {
                dom.hangupAll.addEventListener('click', hangupAll);
            }

            if (dom.hold) {
                dom.hold.addEventListener('click', hold);
            }

            if (dom.resume) {
                dom.resume.addEventListener('click', resume);
            }

            if (dom.mute) {
                dom.mute.addEventListener('click', mute);
            }

            if (dom.unmute) {
                dom.unmute.addEventListener('click', unmute);
            }

            if (dom.transfer) {
                dom.transfer.addEventListener('click', transfer);
            }

            if (dom.transferCancel) {
                dom.transferCancel.addEventListener('click', cancelTransfer);
            }

            if (dom.transferConfirm) {
                dom.transferConfirm.addEventListener('click', confirmTransfer);
            }

            if (dom.merge) {
                dom.merge.addEventListener('click', merge);
            }

            if (dom.incomingAnswer) {
                dom.incomingAnswer.addEventListener('click', function () { answerIncoming(null); });
            }

            if (dom.incomingVoicemail) {
                dom.incomingVoicemail.addEventListener('click', voicemailIncoming);
            }

            if (dom.voicemailAudio) {
                dom.voicemailAudio.addEventListener('ended', stopVoicemailPlayback);
                dom.voicemailAudio.addEventListener('error', handleVoicemailAudioError);
            }

            if (dom.incomingIgnore) {
                dom.incomingIgnore.addEventListener('click', ignoreIncoming);
            }

            if (dom.connect) {
                dom.connect.addEventListener('click', handleConnect);
            }

            if (dom.disconnect) {
                dom.disconnect.addEventListener('click', handleDisconnect);
            }

            dom.keys.forEach(function (key) {
                key.addEventListener('click', function () {
                    pressKey(key.getAttribute('data-telephony-key'));
                });
            });

            attachDrag(dom.dragHandle, { ignoreButtons: true, suppressClick: false });
            attachDrag(dom.toggle, { ignoreButtons: false, suppressClick: true });

            window.addEventListener('message', onOAuthMessage);
            window.addEventListener('beforeunload', function (event) {
                // Seatbelt for navigating away mid-call: a full page load tears down the WebRTC media, so the
                // call cannot survive a reload. Warn the agent before they leave while a call is live (the
                // browser shows its own generic confirmation). This does NOT release audio -- if the agent
                // cancels the navigation the call keeps running; cleanup happens in pagehide only when the page
                // actually goes away. This is a safety net, not a fix for navigating while on a call.
                if (hasLiveCall()) {
                    event.preventDefault();
                    event.returnValue = '';
                }
            });
            window.addEventListener('pagehide', function () {
                // The page is actually being unloaded (navigation confirmed or tab closing): stop the manual
                // reconnect loop from scheduling a doomed restart during teardown, silence the ringtone, and
                // release the provider media session.
                pageUnloading = true;
                ringtone.stop();
                releaseBrowserAudio();
            });
            window.addEventListener('resize', function () {
                restorePosition();
                syncViewHeight();
            });
        }

        bindEvents();
        restoreLayout();
        // Load the persisted device selection before the first registration so getUserMedia uses the saved
        // input device (item 5).
        loadDeviceSelection();
        // The stored processing flags are known only now; the checkboxes were wired (and defaulted) earlier.
        syncProcessingControls();
        render();

        // A restored Diagnostics tab is active without setActiveTab having run, so the meter that tab owns was
        // never started: the panel opens showing a bar that cannot move, which reads as a microphone that is not
        // being read at all.
        if (activeTab === 'diagnostics' && diagnosticsEnabled) {
            startMicMeter();
            updateDiagnosticsReadout();
        }

        rootElement.style.visibility = '';

        var startPromise = connect();

        return {
            element: rootElement,
            config: config,
            dial: dial,
            dialNumber: dialNumber,
            hangup: hangup,
            hangupAll: hangupAll,
            hold: hold,
            resume: resume,
            mute: mute,
            unmute: unmute,
            transfer: transfer,
            merge: merge,
            pressKey: pressKey,
            togglePanel: togglePanel,
            open: function () { togglePanel(true); },
            getCurrentCall: function () { return currentCall; },
            getActiveCalls: getActiveCalls,
            isIncomingAcceptPending: function () { return incomingAcceptPending; },
            // Lets the Contact Center layer declare that a routed leg is on its way to this browser, so the
            // media adapter answers it instead of ringing it as an unsolicited incoming call.
            armInboundAutoAnswer: armInboundAutoAnswer,
            // Answers (accepted) or hangs up (not) the leg held for an offer; returns whether a held leg was answered.
            settleOfferLeg: settleOfferLeg,
            setIncomingOffer: setIncomingOffer,
            clearIncomingOffer: clearIncomingOffer,
            showError: showError,
            getConnection: function () { return connection; },
            registerMediaAdapter: function (name, adapter) {
                if (!name || typeof adapter !== 'function') {
                    return false;
                }

                mediaAdapters[name] = adapter;

                return true;
            },
            started: startPromise
        };
    }

    function initializeAll() {
        var elements = document.querySelectorAll('#telephony-soft-phone, .telephony-soft-phone');

        Array.prototype.forEach.call(elements, function (element) {
            if (!element.__telephonySoftPhone) {
                element.__telephonySoftPhone = createSoftPhone(element);
            }
        });
    }

    function getInstance() {
        var element = document.querySelector('#telephony-soft-phone, .telephony-soft-phone');

        return element ? element.__telephonySoftPhone : null;
    }

    window.telephonySoftPhone = {
        create: createSoftPhone,
        initializeAll: initializeAll,
        getInstance: getInstance,
        formatPhoneNumber: formatPhoneNumber,
        // Authentication handlers keyed by scheme. Providers using a different per-user authentication
        // scenario can register their own handler so the widget remains extensible.
        authHandlers: {
            oauth2: function (context) {
                context.startOAuth();
            }
        },
        dial: function (number) {
            var instance = getInstance();

            if (instance) {
                instance.dialNumber(number);
            }
        }
    };

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeAll);
    } else {
        initializeAll();
    }
})();
