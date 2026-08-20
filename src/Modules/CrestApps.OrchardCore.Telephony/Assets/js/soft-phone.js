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

    // Chrome requires RTCP multiplexing, but the Telnyx SDP answer omits "a=rtcp-mux", so
    // setRemoteDescription rejects the answer ("RTCP-MUX is not enabled") and the call is torn down on
    // answer. Patch setRemoteDescription once to add a=rtcp-mux to any audio m-section of an answer that
    // lacks it; RTP still flows on the single muxed port, so audio negotiates and connects.
    function ensureRtcpMuxAnswerWorkaround() {
        if (typeof RTCPeerConnection === 'undefined' || RTCPeerConnection.prototype.__rtcpMuxAnswerPatched) {
            return;
        }

        var originalSetRemoteDescription = RTCPeerConnection.prototype.setRemoteDescription;

        RTCPeerConnection.prototype.setRemoteDescription = function (description) {
            if (description && description.type === 'answer' && description.sdp &&
                description.sdp.indexOf('a=rtcp-mux') === -1) {
                description = {
                    type: description.type,
                    sdp: description.sdp.replace(/(m=audio[^\r\n]*\r?\n)/g, '$1a=rtcp-mux\r\n')
                };
            }

            return originalSetRemoteDescription.apply(this, [description].concat(Array.prototype.slice.call(arguments, 1)));
        };

        RTCPeerConnection.prototype.__rtcpMuxAnswerPatched = true;
    }

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
        ensureRtcpMuxAnswerWorkaround();

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
        ensureRtcpMuxAnswerWorkaround();

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

    function clamp(value, min, max) {
        return Math.min(Math.max(value, min), max);
    }

    function isFiniteNumber(value) {
        return typeof value === 'number' && isFinite(value);
    }

    function normalizeDialNumber(value) {
        var input = String(value || '').trim();
        var hasInternationalPrefix = input.charAt(0) === '+';
        var digits = input.replace(/\D/g, '');

        return (hasInternationalPrefix ? '+' : '') + digits;
    }

    function formatNanpNumber(digits, international) {
        var national = international ? digits.substring(1) : digits;
        var formatted = '';

        if (international) {
            formatted = '+1';
        }

        if (national.length > 0) {
            formatted += (international ? ' ' : '') + '(' + national.substring(0, 3);
        }

        if (national.length >= 3) {
            formatted += ')';
        }

        if (national.length > 3) {
            formatted += ' ' + national.substring(3, 6);
        }

        if (national.length > 6) {
            formatted += '-' + national.substring(6, 10);
        }

        return formatted;
    }

    function formatInternationalNumber(digits) {
        if (!digits) {
            return '+';
        }

        var countryCodeLength = digits.length > 10 ? Math.min(3, digits.length - 10) : Math.min(2, digits.length);
        var countryCode = digits.substring(0, countryCodeLength);
        var national = digits.substring(countryCodeLength);
        var groups = [];

        while (national.length > 4) {
            groups.push(national.substring(0, 3));
            national = national.substring(3);
        }

        if (national) {
            groups.push(national);
        }

        return '+' + countryCode + (groups.length ? ' ' + groups.join(' ') : '');
    }

    // Formats a number for display only (call history and active-call rows). This is deliberately
    // independent of intl-tel-input, which only enhances the editable keypad input; the display
    // formatter must work even where the phone-field library is not loaded.
    function formatPhoneNumber(value) {
        var normalized = normalizeDialNumber(value);
        var international = normalized.charAt(0) === '+';
        var digits = normalized.replace(/\D/g, '');

        if (!international && digits.length < 7) {
            return digits;
        }

        if ((!international && digits.length <= 10) ||
            (international && digits.charAt(0) === '1' && digits.length <= 11)) {
            return formatNanpNumber(digits, international);
        }

        return international ? formatInternationalNumber(digits) : digits;
    }

    function createSoftPhone(rootElement, options) {
        options = options || {};

        var config = parseConfig(rootElement);
        var strings = config.strings || {};
        var capabilities = config.capabilities || 0;
        var storageKey = (config.storageKey || 'telephony-soft-phone') + '-layout';
        var mediaAdapters = createBrowserMediaAdapterRegistry(rootElement, config);

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
            remoteAudio: rootElement.querySelector('[data-telephony-remote-audio]')
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
        var browserAudioPromise = null;
        var browserAudioSession = null;
        var localAudioStream = null;
        // Controllers for calls the browser originated itself (client-originated providers such as Telnyx),
        // keyed by the synthetic call id. Server-tracked calls are not in this map.
        var browserCallControllers = {};

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

            if (telInput && typeof telInput.getNumber === 'function') {
                // Only trust the intl-tel-input E.164 output for real, valid phone numbers. Short
                // strings such as internal extensions are not valid numbers, so they are dialed
                // verbatim instead of being turned into a bogus "+1101" style destination.
                var isValid = typeof telInput.isValidNumber !== 'function' || telInput.isValidNumber();

                if (isValid) {
                    var e164 = telInput.getNumber();

                    if (e164 && e164.charAt(0) === '+') {
                        return e164;
                    }
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
            if (!localAudioStream) {
                return;
            }

            localAudioStream.getTracks().forEach(function (track) {
                track.stop();
            });
            localAudioStream = null;
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

        function ensureBrowserAudio() {
            if (!isBrowserAudioEnabled()) {
                return Promise.resolve(null);
            }

            if (browserAudioSession) {
                return Promise.resolve(browserAudioSession);
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

                return navigator.mediaDevices.getUserMedia({ audio: true }).then(function (stream) {
                    localAudioStream = stream;

                    return Promise.resolve(adapter({
                        credentials: credentials,
                        localStream: stream,
                        remoteAudioElement: dom.remoteAudio,
                        setRemoteStream: setRemoteAudioStream,
                        showError: showError
                    }));
                });
            }).then(function (session) {
                browserAudioSession = session || {};

                return browserAudioSession;
            }).catch(function (error) {
                releaseBrowserAudio();

                throw error;
            }).finally(function () {
                browserAudioPromise = null;
            });

            return browserAudioPromise;
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
            return ensureBrowserAudio().then(function (session) {
                if (session && session.canOriginate && typeof session.originate === 'function') {
                    originateBrowserCall(session, number);

                    return null;
                }

                return invoke('Dial', { to: number, isExtension: isExtension });
            }).catch(function (error) {
                showError(error && error.message ? error.message : String(error));

                return null;
            });
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

                // Preserve a local hold state (the SIP session has no distinct hold signal to the UI).
                if (!(stateName === 'Connected' && existing.isOnHold)) {
                    existing.state = stateName;
                }

                if (stateName === 'Disconnected') {
                    removeActiveCall(callId);
                    delete browserCallControllers[callId];
                } else {
                    upsertActiveCall(existing, false);
                }

                render();
            });

            if (controller) {
                browserCallControllers[callId] = controller;
            } else {
                removeActiveCall(callId);
                render();
            }
        }

        function currentBrowserController() {
            return currentCall && currentCall.browserOriginated
                ? browserCallControllers[currentCall.callId] || null
                : null;
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
            return tab === 'keypad' || tab === 'history';
        }

        function hasExtensionTabs() {
            return dom.tabs.some(function (tab) {
                return !isTelephonyTab(tab.getAttribute('data-telephony-tab'));
            });
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

            if (currentCall && currentCall.callId === callId) {
                currentCall = getActiveCalls()[0] || null;
            }
        }

        function upsertActiveCall(call, select) {
            if (!call || !call.callId) {
                return;
            }

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

            setStatus(currentCall ? statusTextForCall(currentCall) : (strings.idle || 'Ready'));

            if (dom.number && currentCall && (active || stateName === 'OnHold')) {
                var peerNumber = getPeerNumber(currentCall);

                if (peerNumber) {
                    setNumberDisplay(peerNumber);
                    numberIsCallDisplay = true;
                }
            } else if (dom.number && canDial && numberIsCallDisplay) {
                clearNumberInput();
                numberIsCallDisplay = false;
            }

            show(dom.dial, canDial && has(CAPABILITIES.Dial));
            // Allow hanging up (cancelling) while the call is still connecting or ringing, not only once media
            // is live, so an outbound call that has not been answered yet can still be ended.
            show(dom.hangup, active && has(CAPABILITIES.Hangup));
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
                var numberDisabled = !canDial || !!activeCommand;

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

            activeCalls = {};

            calls.forEach(function (call) {
                upsertActiveCall(call, false);
            });

            preservedBrowserCalls.forEach(function (call) {
                upsertActiveCall(call, false);
            });

            currentCall = previousCallId && activeCalls[previousCallId]
                ? activeCalls[previousCallId]
                : getActiveCalls()[0] || null;

            if (!currentCall) {
                incomingHandled = false;
            }

            render();
            notifyBrowserAudio(currentCall);

            if (!currentCall) {
                releaseBrowserAudio();
            }

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

            if (!number) {
                showError(strings.invalidNumber || 'Enter a phone number to call.');

                return;
            }

            clearNumberInput();
            numberIsCallDisplay = false;

            placeCall(number, extensionMode);
        }

        function dialNumber(number) {
            if (!number) {
                return;
            }

            setActiveTab('keypad');
            togglePanel(true);

            clearNumberInput();
            numberIsCallDisplay = false;

            placeCall(normalizeDialNumber(number), false);
        }

        function hangup() {
            var controller = currentBrowserController();

            if (controller) {
                Promise.resolve(controller.terminate()).catch(function () { });

                return;
            }

            var call = currentCallReference();

            if (call) {
                invoke('Hangup', call);
            }
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

        function hold() {
            var controller = currentBrowserController();

            if (controller) {
                Promise.resolve(controller.setHold(true)).catch(function () { });
                currentCall.isOnHold = true;
                currentCall.state = 'OnHold';
                upsertActiveCall(currentCall, true);
                render();

                return;
            }

            var call = currentCallReference();

            if (call) {
                invoke('Hold', call);
            }
        }

        function resume() {
            var controller = currentBrowserController();

            if (controller) {
                Promise.resolve(controller.setHold(false)).catch(function () { });
                currentCall.isOnHold = false;
                currentCall.state = 'Connected';
                upsertActiveCall(currentCall, true);
                render();

                return;
            }

            var call = currentCallReference();

            if (call) {
                invoke('Resume', call);
            }
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
                dom.number.value = dom.number.value + value;
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
            renderIncomingCards();
            scheduleIncomingExpiry();
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
                actions += '<button type="button" class="btn btn-sm btn-success" data-telephony-card-answer data-url="' + escapeHtml(card.url) + '"><i class="fa-solid fa-phone"></i> ' + escapeHtml(strings.answerAndOpen || 'Answer & open') + '</button>';
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
            incomingAcceptPending = true;

            postLifecycle('acceptUrl').then(function (result) {
                if (!result || result.succeeded === false) {
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
            var call = currentCallReference();

            postLifecycle('declineUrl');

            if (call) {
                invoke('Voicemail', call);
            }
        }

        function ignoreIncoming() {
            var call = currentCallReference();
            var hasOffer = incomingContext && incomingContext.properties && incomingContext.properties.declineUrl;

            if (hasOffer) {
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
        }

        function isInbound(interaction) {
            return interaction.direction === 1 || interaction.direction === 'Inbound';
        }

        function isMissed(interaction) {
            return interaction.outcome === 2 || interaction.outcome === 'Missed' ||
                interaction.outcome === 3 || interaction.outcome === 'Rejected';
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

            dom.historyList.innerHTML = items.map(function (interaction) {
                var inbound = isInbound(interaction);
                var missed = isMissed(interaction);
                var inProgress = isInProgress(interaction);
                var directionGlyph = inbound ? '\u2199' : '\u2197';
                var number = inbound ? (interaction.from || '') : (interaction.to || '');
                var formattedNumber = formatPhoneNumber(number);
                var label = missed ? (strings.missed || 'Missed') : (inbound ? (strings.incoming || 'Incoming') : (strings.outgoing || 'Outgoing'));
                var time = formatTime(interaction.startedUtc);
                var cls = 'telephony-soft-phone__history-item' +
                    (missed ? ' telephony-soft-phone__history-item--missed' : '') +
                    (inProgress ? ' telephony-soft-phone__history-item--active' : '');

                var meta = escapeHtml(label) +
                    (time ? ' \u2022 ' + escapeHtml(time) : '');

                return '<button type="button" class="' + cls + '" data-telephony-history-number="' + escapeHtml(number) + '">' +
                    '<span class="telephony-soft-phone__history-dir" aria-hidden="true">' + directionGlyph + '</span>' +
                    '<span class="telephony-soft-phone__history-body">' +
                    '<span class="telephony-soft-phone__history-number">' + escapeHtml(formattedNumber || number || label) + '</span>' +
                    '<span class="telephony-soft-phone__history-meta">' + meta + '</span>' +
                    '</span></button>';
            }).join('');

            Array.prototype.forEach.call(dom.historyList.querySelectorAll('[data-telephony-history-number]'), function (item) {
                item.addEventListener('click', function () {
                    var number = item.getAttribute('data-telephony-history-number');

                    if (number) {
                        dialNumber(number);
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

            connection.on('ReceiveError', function (message) {
                showError(message);
            });

            connection.on('CredentialsIssued', function () { });

            connection.onclose(function () {
                setStatus(strings.disconnectedHub || 'Disconnected');
                clearActiveCallsRefresh();
                releaseBrowserAudio();
            });

            if (typeof connection.onreconnected === 'function') {
                connection.onreconnected(function () {
                    showError(null);
                    return Promise.all([refreshCapabilities(), refreshConnectionStatus()])
                        .then(function () {
                            return restoreActiveCall();
                        })
                        .then(function () {
                            if (activeTab === 'history') {
                                loadHistory();
                            }

                            render();
                        })
                        .catch(function (error) {
                            showError(error && error.message ? error.message : String(error));
                        });
                });
            }
        }

        function connect() {
            if (!signalRFactory || !config.hubUrl) {
                render();

                return Promise.resolve();
            }

            connection = new signalRFactory.HubConnectionBuilder()
                .withUrl(config.hubUrl)
                .withAutomaticReconnect()
                .build();

            registerClientCallbacks();

            return connection.start().then(function () {
                showError(null);
                return Promise.all([refreshCapabilities(), refreshConnectionStatus()]);
            }).then(function () {
                return restoreActiveCall();
            }).then(function () {
                if (activeTab === 'history') {
                    loadHistory();
                }

                render();
            }).catch(function (error) {
                showError(error && error.message ? error.message : String(error));
            });
        }

        function bindEvents() {
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

            if (dom.dial) {
                dom.dial.addEventListener('click', dial);
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
            window.addEventListener('beforeunload', releaseBrowserAudio);
            window.addEventListener('resize', function () {
                restorePosition();
                syncViewHeight();
            });
        }

        bindEvents();
        restoreLayout();
        render();
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
