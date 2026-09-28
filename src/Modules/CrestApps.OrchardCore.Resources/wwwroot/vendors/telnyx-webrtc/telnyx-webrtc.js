var TelnyxWebRTC = (() => {
  var __getOwnPropNames = Object.getOwnPropertyNames;
  var __commonJS = (cb, mod) => function __require() {
    try {
      return mod || (0, cb[__getOwnPropNames(cb)[0]])((mod = { exports: {} }).exports, mod), mod.exports;
    } catch (e) {
      throw mod = 0, e;
    }
  };

  // node_modules/@telnyx/webrtc/lib/bundle.js
  var require_bundle = __commonJS({
    "node_modules/@telnyx/webrtc/lib/bundle.js"(exports, module) {
      !(function(e, t) {
        "object" == typeof exports && "undefined" != typeof module ? t(exports) : "function" == typeof define && define.amd ? define(["exports"], t) : t((e = e || self).TelnyxWebRTC = {});
      })(exports, (function(e) {
        "use strict";
        function t(e2, t2) {
          var i2 = {};
          for (var n2 in e2) Object.prototype.hasOwnProperty.call(e2, n2) && t2.indexOf(n2) < 0 && (i2[n2] = e2[n2]);
          if (null != e2 && "function" == typeof Object.getOwnPropertySymbols) {
            var s2 = 0;
            for (n2 = Object.getOwnPropertySymbols(e2); s2 < n2.length; s2++) t2.indexOf(n2[s2]) < 0 && Object.prototype.propertyIsEnumerable.call(e2, n2[s2]) && (i2[n2[s2]] = e2[n2[s2]]);
          }
          return i2;
        }
        function i(e2, t2, i2, n2) {
          return new (i2 || (i2 = Promise))((function(s2, o2) {
            function r2(e3) {
              try {
                c2(n2.next(e3));
              } catch (e4) {
                o2(e4);
              }
            }
            function a2(e3) {
              try {
                c2(n2.throw(e3));
              } catch (e4) {
                o2(e4);
              }
            }
            function c2(e3) {
              var t3;
              e3.done ? s2(e3.value) : (t3 = e3.value, t3 instanceof i2 ? t3 : new i2((function(e4) {
                e4(t3);
              }))).then(r2, a2);
            }
            c2((n2 = n2.apply(e2, t2 || [])).next());
          }));
        }
        "function" == typeof SuppressedError && SuppressedError;
        var n = "undefined" != typeof crypto && crypto.getRandomValues && crypto.getRandomValues.bind(crypto) || "undefined" != typeof msCrypto && "function" == typeof msCrypto.getRandomValues && msCrypto.getRandomValues.bind(msCrypto), s = new Uint8Array(16);
        function o() {
          if (!n) throw new Error("crypto.getRandomValues() not supported. See https://github.com/uuidjs/uuid#getrandomvalues-not-supported");
          return n(s);
        }
        for (var r = [], a = 0; a < 256; ++a) r[a] = (a + 256).toString(16).substr(1);
        function c(e2, t2, i2) {
          var n2 = t2 && i2 || 0;
          "string" == typeof e2 && (t2 = "binary" === e2 ? new Array(16) : null, e2 = null);
          var s2 = (e2 = e2 || {}).random || (e2.rng || o)();
          if (s2[6] = 15 & s2[6] | 64, s2[8] = 63 & s2[8] | 128, t2) for (var a2 = 0; a2 < 16; ++a2) t2[n2 + a2] = s2[a2];
          return t2 || (function(e3, t3) {
            var i3 = t3 || 0, n3 = r;
            return [n3[e3[i3++]], n3[e3[i3++]], n3[e3[i3++]], n3[e3[i3++]], "-", n3[e3[i3++]], n3[e3[i3++]], "-", n3[e3[i3++]], n3[e3[i3++]], "-", n3[e3[i3++]], n3[e3[i3++]], "-", n3[e3[i3++]], n3[e3[i3++]], n3[e3[i3++]], n3[e3[i3++]], n3[e3[i3++]], n3[e3[i3++]]].join("");
          })(s2);
        }
        const l = { SDP_CREATE_OFFER_FAILED: 40001, SDP_CREATE_ANSWER_FAILED: 40002, SDP_SET_LOCAL_DESCRIPTION_FAILED: 40003, SDP_SET_REMOTE_DESCRIPTION_FAILED: 40004, SDP_SEND_FAILED: 40005, MEDIA_MICROPHONE_PERMISSION_DENIED: 42001, MEDIA_DEVICE_NOT_FOUND: 42002, MEDIA_GET_USER_MEDIA_FAILED: 42003, HOLD_FAILED: 44001, INVALID_CALL_PARAMETERS: 44002, BYE_SEND_FAILED: 44003, SUBSCRIBE_FAILED: 44004, PEER_CLOSED_DURING_INIT: 44005, WEBSOCKET_CONNECTION_FAILED: 45001, WEBSOCKET_ERROR: 45002, RECONNECTION_EXHAUSTED: 45003, GATEWAY_FAILED: 45004, LOGIN_FAILED: 46001, INVALID_CREDENTIALS: 46002, AUTHENTICATION_REQUIRED: 46003, ICE_RESTART_FAILED: 47001, NETWORK_OFFLINE: 48001, SESSION_NOT_REATTACHED: 48501, UNEXPECTED_ERROR: 49001 }, d = { HIGH_RTT: 31001, HIGH_JITTER: 31002, HIGH_PACKET_LOSS: 31003, LOW_MOS: 31004, LOW_LOCAL_AUDIO: 31005, LOW_INBOUND_AUDIO: 31006, LOW_BYTES_RECEIVED: 32001, LOW_BYTES_SENT: 32002, RECORDING_UNAVAILABLE: 32003, RECORDING_BUFFER_OVERFLOW: 32004, ICE_CONNECTIVITY_LOST: 33001, ICE_GATHERING_TIMEOUT: 33002, ICE_GATHERING_EMPTY: 33003, PEER_CONNECTION_FAILED: 33004, ONLY_HOST_ICE_CANDIDATES: 33005, ANSWER_WHILE_PEER_ACTIVE: 33006, ICE_CANDIDATE_PAIR_CHANGED: 33008, AUDIO_INPUT_DEVICE_CHANGE_SKIPPED: 33009, MULTIPLE_ACTIVE_CALLS_DETECTED: 33010, DUPLICATE_INBOUND_ANSWER: 33007, SHARED_REMOTE_ELEMENT_OVERWRITE: 33011, TOKEN_EXPIRING_SOON: 34001, UNKNOWN_REATTACHED_SESSION: 35002, SIGNALING_RECOVERY_REQUIRED: 36003, MEDIA_RECOVERY_REQUIRED: 36004, RECONNECTION_FAILED_WITH_NO_AUTO_RECONNECT: 36005 }, { SDP_CREATE_OFFER_FAILED: u, SDP_CREATE_ANSWER_FAILED: h, SDP_SET_LOCAL_DESCRIPTION_FAILED: p, SDP_SET_REMOTE_DESCRIPTION_FAILED: g, SDP_SEND_FAILED: v, MEDIA_MICROPHONE_PERMISSION_DENIED: m, MEDIA_DEVICE_NOT_FOUND: f, MEDIA_GET_USER_MEDIA_FAILED: _, HOLD_FAILED: S, INVALID_CALL_PARAMETERS: y, BYE_SEND_FAILED: b, SUBSCRIBE_FAILED: I, PEER_CLOSED_DURING_INIT: E, WEBSOCKET_CONNECTION_FAILED: C, WEBSOCKET_ERROR: w, RECONNECTION_EXHAUSTED: T, GATEWAY_FAILED: k, LOGIN_FAILED: R, INVALID_CREDENTIALS: A, AUTHENTICATION_REQUIRED: O, ICE_RESTART_FAILED: D, NETWORK_OFFLINE: N, SESSION_NOT_REATTACHED: L, UNEXPECTED_ERROR: M } = l, { HIGH_RTT: P, HIGH_JITTER: x, HIGH_PACKET_LOSS: U, LOW_MOS: F, LOW_LOCAL_AUDIO: $, LOW_INBOUND_AUDIO: B, LOW_BYTES_RECEIVED: j, LOW_BYTES_SENT: H, RECORDING_UNAVAILABLE: W, RECORDING_BUFFER_OVERFLOW: G, ICE_CONNECTIVITY_LOST: V, ICE_GATHERING_TIMEOUT: q, ICE_GATHERING_EMPTY: Y, PEER_CONNECTION_FAILED: K, ONLY_HOST_ICE_CANDIDATES: J, ANSWER_WHILE_PEER_ACTIVE: z, ICE_CANDIDATE_PAIR_CHANGED: X, AUDIO_INPUT_DEVICE_CHANGE_SKIPPED: Q, MULTIPLE_ACTIVE_CALLS_DETECTED: Z, DUPLICATE_INBOUND_ANSWER: ee, SHARED_REMOTE_ELEMENT_OVERWRITE: te, TOKEN_EXPIRING_SOON: ie, UNKNOWN_REATTACHED_SESSION: ne, SIGNALING_RECOVERY_REQUIRED: se, MEDIA_RECOVERY_REQUIRED: oe, RECONNECTION_FAILED_WITH_NO_AUTO_RECONNECT: re } = d, ae = "wss://rtc.telnyx.com", ce = { NORMAL_CLOSURE: 1e3, GOING_AWAY: 1001, PROTOCOL_ERROR: 1002, UNSUPPORTED_DATA: 1003, NO_STATUS_RECEIVED: 1005, ABNORMAL_CLOSURE: 1006, INVALID_FRAME_PAYLOAD: 1007, POLICY_VIOLATION: 1008, MESSAGE_TOO_BIG: 1009, INTERNAL_ERROR: 1011 }, le = { urls: "stun:stun.l.google.com:19302" }, de = { urls: "stun:stun.telnyx.com:3478" }, ue = { urls: "turn:turn.telnyx.com:3478?transport=udp", username: "testuser", credential: "testpassword" }, he = { urls: "turn:turn.telnyx.com:3478?transport=tcp", username: "testuser", credential: "testpassword" }, pe = { urls: "turns:turn.telnyx.com:443", username: "testuser", credential: "testpassword" }, ge = { urls: "turns:turn2.telnyx.com:443", username: "testuser", credential: "testpassword" }, ve = [de, le, ...[ue, he], pe, ge], me = [{ urls: "stun:stundev.telnyx.com:3478" }, le, { urls: "turn:turndev.telnyx.com:3478?transport=udp", username: "testuser", credential: "testpassword" }, { urls: "turn:turndev.telnyx.com:3478?transport=tcp", username: "testuser", credential: "testpassword" }, { urls: "turns:turndev.telnyx.com:443", username: "testuser", credential: "testpassword" }], fe = { GOOGLE_STUN: le, TELNYX_STUN: de, TELNYX_TURN_UDP_3478: ue, TELNYX_TURN_TCP_3478: he, TELNYX_TURNS_TCP_443: ge, TELNYX_TURNS_TCP_443_PRIMARY: pe };
        var _e;
        (_e = e.SwEvent || (e.SwEvent = {})).SocketOpen = "telnyx.socket.open", _e.SocketClose = "telnyx.socket.close", _e.SocketError = "telnyx.socket.error", _e.SocketMessage = "telnyx.socket.message", _e.SpeedTest = "telnyx.internal.speedtest", _e.SocketActivity = "telnyx.internal.socketActivity", _e.Ready = "telnyx.ready", _e.Error = "telnyx.error", _e.Warning = "telnyx.warning", _e.Notification = "telnyx.notification", _e.StatsFrame = "telnyx.stats.frame", _e.StatsReport = "telnyx.stats.report", _e.Messages = "telnyx.messages", _e.Calls = "telnyx.calls", _e.MediaError = "telnyx.rtc.mediaError", _e.PeerConnectionFailureError = "telnyx.rtc.peerConnectionFailureError", _e.PeerConnectionSignalingStateClosed = "telnyx.rtc.peerConnectionSignalingStateClosed", _e.AIConversationMessage = "telnyx.ai.conversation";
        const Se = { 40001: { name: "SDP_CREATE_OFFER_FAILED", message: "Failed to create call offer", description: "The browser was unable to generate a local SDP offer. This typically indicates a WebRTC API error or invalid media constraints.", causes: ["Browser WebRTC API error", "Missing or invalid media constraints"], solutions: ["Check getUserMedia permissions", "Verify ICE server configuration"], fatal: true }, 40002: { name: "SDP_CREATE_ANSWER_FAILED", message: "Failed to answer the call", description: "The browser was unable to generate a local SDP answer. The remote offer may be invalid or the browser state inconsistent.", causes: ["Browser WebRTC API error", "Invalid remote SDP offer"], solutions: ["Retry the call", "Check browser WebRTC compatibility"], fatal: true }, 40003: { name: "SDP_SET_LOCAL_DESCRIPTION_FAILED", message: "Failed to apply local call settings", description: "setLocalDescription() was rejected by the browser. The generated SDP may be malformed or the browser state may be inconsistent.", causes: ["Malformed SDP", "Browser state inconsistency"], solutions: ["Retry the call"], fatal: true }, 40004: { name: "SDP_SET_REMOTE_DESCRIPTION_FAILED", message: "Failed to apply remote call settings", description: "setRemoteDescription() was rejected by the browser. The remote SDP may be malformed or contain unsupported codecs.", causes: ["Malformed remote SDP", "Browser codec mismatch"], solutions: ["Retry the call", "Check codec configuration"], fatal: true }, 40005: { name: "SDP_SEND_FAILED", message: "Failed to send call data to server", description: "The Invite or Answer message could not be delivered via the signaling WebSocket. The connection may have been lost.", causes: ["WebSocket connection lost", "Server error"], solutions: ["Check network connectivity", "Retry the call"], fatal: true }, 42001: { name: "MEDIA_MICROPHONE_PERMISSION_DENIED", message: "Microphone access denied", description: "The user or operating system denied microphone permission. The browser permission prompt was dismissed or OS-level access is disabled.", causes: ["User denied browser permission prompt", "OS-level microphone access disabled"], solutions: ["Ask user to grant microphone permission in browser settings"], fatal: true }, 42002: { name: "MEDIA_DEVICE_NOT_FOUND", message: "No microphone found", description: "The requested audio input device is not available. No microphone is connected, the device was disconnected, or an invalid deviceId was specified.", causes: ["No microphone connected", "Device was disconnected", "Invalid deviceId"], solutions: ["Check that a microphone is connected", "Select a valid audio input device"], fatal: true }, 42003: { name: "MEDIA_GET_USER_MEDIA_FAILED", message: "Failed to access microphone", description: "getUserMedia() was rejected for an unexpected reason. The device may be in use by another application or the browser encountered an internal error.", causes: ["Browser error", "Device in use by another application"], solutions: ["Close other applications using the microphone", "Retry"], fatal: true }, 44001: { name: "HOLD_FAILED", message: "Failed to hold the call", description: "The server rejected or did not respond to the hold request. The WebSocket connection may have been lost during the operation.", causes: ["Server error", "WebSocket connection lost during hold"], solutions: ["Retry the hold operation", "Check network connectivity"], fatal: false }, 44002: { name: "INVALID_CALL_PARAMETERS", message: "Invalid call parameters", description: "The call could not be initiated because required parameters are missing or invalid. For example, no destination number was provided to newCall().", causes: ["Missing destinationNumber in call options", "Invalid or empty call parameters"], solutions: ["Provide a valid destinationNumber when calling newCall()", "Check the call options object for required fields"], fatal: true }, 44003: { name: "BYE_SEND_FAILED", message: "Failed to hang up cleanly", description: "The hangup signal could not be delivered to the server. The call was terminated locally but the server may not be aware.", causes: ["WebSocket connection lost before BYE sent"], solutions: ["No action needed \u2014 call is terminated locally", "Check network connectivity"], fatal: false }, 44004: { name: "SUBSCRIBE_FAILED", message: "Failed to subscribe to call events", description: "The Verto subscribe request for the call channel failed. This may prevent receiving call state updates from the server.", causes: ["WebSocket connection lost during subscribe", "Server rejected the subscription request"], solutions: ["Check network connectivity", "Retry the call"], fatal: false }, 44005: { name: "PEER_CLOSED_DURING_INIT", message: "Call was closed during setup", description: "The PeerConnection was closed (e.g. by hangup()) while peer.init() was still running. This is a race condition: an async operation such as setRemoteDescription, getUserMedia, or the media recovery flow yielded control, and close() ran during that gap. The init() cannot continue because the underlying RTCPeerConnection has been destroyed.", causes: ["call.hangup() or call.close() was called while the call was still setting up", "A WebSocket Bye message arrived during getUserMedia prompt or SDP negotiation", "User clicked hangup/decline before media permissions were granted"], solutions: ["This is expected if the user intentionally hung up during setup \u2014 no action needed", "If this happens frequently without user action, check for automatic hangup triggers that may fire too early"], fatal: true }, 45001: { name: "WEBSOCKET_CONNECTION_FAILED", message: "Unable to connect to server", description: "The WebSocket connection to the signaling server could not be established. The server may be unreachable, the URL may be incorrect, or a firewall may be blocking the connection.", causes: ["Server unreachable", "Incorrect WebSocket URL", "Firewall blocking WebSocket connections", "Network interruption"], solutions: ["Check network connectivity", "Verify the signaling server URL", "Ensure WebSocket connections are not blocked by a firewall"], fatal: true }, 45002: { name: "WEBSOCKET_ERROR", message: "Connection to server lost", description: "An error occurred on the WebSocket connection after it was established. The connection may have been dropped due to network issues or server-side closure.", causes: ["Network interruption", "Server closed the connection", "Idle timeout"], solutions: ["Check network connectivity", "SDK will attempt automatic reconnection if configured"], fatal: false }, 45003: { name: "RECONNECTION_EXHAUSTED", message: "Unable to reconnect to server", description: "All automatic reconnection attempts have been exhausted. The SDK tried to re-establish the WebSocket connection multiple times but failed on every attempt.", causes: ["Prolonged network outage", "Server unreachable", "Firewall or proxy blocking reconnection"], solutions: ["Check network connectivity", "Call client.disconnect() and client.connect() to manually retry", "Notify the user that the connection was lost"], fatal: true }, 45004: { name: "GATEWAY_FAILED", message: "Gateway connection failed", description: "The upstream gateway reported a FAILED, FAIL_WAIT, or TIMEOUT state. The signaling server could not establish or maintain a connection to the gateway. When autoReconnect is disabled, this is immediately fatal. When enabled, the SDK will retry until RECONNECTION_EXHAUSTED.", causes: ["Gateway down or unreachable", "Server-side infrastructure issue", "Network partition between signaling server and gateway"], solutions: ["Wait for automatic reconnection (if autoReconnect is enabled)", "Call client.disconnect() and client.connect() to manually retry", "Check Telnyx service status"], fatal: false }, 46001: { name: "LOGIN_FAILED", message: "Authentication failed", description: "The login request was rejected by the server. The credentials may be invalid, expired, or the account may be suspended.", causes: ["Invalid credentials (username/password or token)", "Expired authentication token", "Account suspended or disabled"], solutions: ["Verify credentials", "Generate a new authentication token", "Check account status"], fatal: true }, 46002: { name: "INVALID_CREDENTIALS", message: "Invalid credential parameters", description: "The SDK rejected the login options before sending any request to the server. This is an internal client-side validation guard \u2014 the credentials object is missing required fields or has an invalid structure. No network request was made.", causes: ["Missing login and password fields", "Missing or malformed authentication token", "Invalid combination of credential fields in the options object"], solutions: ["Provide valid login/password or a valid authentication token", "Check the TelnyxRTC constructor options against the documentation", "Ensure the credential object matches one of the supported auth modes (credentials, token, or anonymous)"], fatal: true }, 46003: { name: "AUTHENTICATION_REQUIRED", message: "Authentication required", description: "The server rejected a request because the session is not authenticated. This can happen when the client sends a message (e.g. Invite, Subscribe, or Ping) before login completes, after a token expires mid-session, or after the server drops the authenticated state for any reason.", causes: ["Message sent before login completed", "Authentication token expired during the session", "Server-side session was invalidated", "WebSocket reconnected but re-authentication did not complete"], solutions: ["Ensure the client is fully logged in before sending messages", "Re-authenticate using client.login() with fresh credentials", "Listen for telnyx.ready before making calls or sending requests"], fatal: false }, 47001: { name: "ICE_RESTART_FAILED", message: "ICE restart failed", description: "The ICE restart Modify request could not be sent or the server returned an error. The media path could not be recovered via ICE restart.", causes: ["WebSocket connection lost during ICE restart", "Server rejected the Modify request", "Timeout waiting for server response"], solutions: ["The call may recover via WebSocket reconnect + Attach", "If the call does not recover, hang up and retry"], fatal: false }, 48001: { name: "NETWORK_OFFLINE", message: "Device is offline", description: "The browser reported that the device has lost network connectivity (navigator.onLine === false). All WebSocket and media connections will fail until the network is restored.", causes: ["Wi-Fi or ethernet disconnected", "Airplane mode enabled", "Network interface went down"], solutions: ["Check network connectivity", "Reconnect to Wi-Fi or ethernet", "Disable airplane mode"], fatal: false }, 48501: { name: "SESSION_NOT_REATTACHED", message: "Active call lost after reconnect", description: "The WebSocket reconnected successfully but the server did not reattach the active call session. The server no longer knows about the call, so any subsequent call-control operation (hangup, hold, etc.) will fail with CALL_DOES_NOT_EXIST. The call is unrecoverable and must be terminated locally.", causes: ["Server-side session expired during the disconnection window", "Reconnect token was invalidated", "Backend restarted or lost in-memory call state"], solutions: ["Terminate the local call and notify the user", "Start a new call", "Investigate why the session was not preserved on the server"], fatal: true }, 49001: { name: "UNEXPECTED_ERROR", message: "An unexpected error occurred", description: "An error was thrown that does not match any known SDK error category. This is a catch-all for unclassified failures.", causes: ["Unknown or unhandled error condition"], solutions: ["Check the originalError property for the underlying cause", "Report the issue if it persists"], fatal: true } }, ye = { 31001: { name: "HIGH_RTT", message: "High network latency detected", description: "Round-trip time (RTT) exceeded the threshold for multiple consecutive samples. High latency causes perceptible audio delays.", causes: ["Poor network connection", "Geographic distance to media server", "Network congestion"], solutions: ["Check network connectivity", "Use a wired connection instead of Wi-Fi", "Close bandwidth-heavy applications"] }, 31002: { name: "HIGH_JITTER", message: "High jitter detected", description: "Jitter (variability in packet arrival time) exceeded the threshold for multiple consecutive samples. High jitter causes crackling and choppy audio.", causes: ["Network congestion", "Unstable Wi-Fi connection", "Overloaded network equipment"], solutions: ["Use a wired connection instead of Wi-Fi", "Close bandwidth-heavy applications", "Check network equipment"] }, 31003: { name: "HIGH_PACKET_LOSS", message: "High packet loss detected", description: "Packet loss exceeded the threshold for multiple consecutive samples. High packet loss causes choppy audio or dropped calls.", causes: ["Network congestion", "Unstable connection", "Firewall or QoS misconfiguration"], solutions: ["Check network connectivity", "Use a wired connection", "Contact network administrator"] }, 31004: { name: "LOW_MOS", message: "Low call quality score", description: "Mean Opinion Score (MOS) dropped below the acceptable threshold for multiple consecutive samples. This is a composite indicator of overall call quality.", causes: ["Combination of high latency, jitter, and/or packet loss", "Poor network conditions"], solutions: ["Check network connectivity", "Use a wired connection", "Close bandwidth-heavy applications"] }, 31005: { name: "LOW_LOCAL_AUDIO", message: "Low local microphone audio detected", description: "Local outbound audio level stayed below the acceptable threshold before the microphone produced real audio, or stayed silent for a long continuous window after audio was confirmed. This may indicate that the microphone is not capturing enough audio even while RTP is being sent.", causes: ["Microphone input level is too low", "Wrong microphone selected", "Microphone is obstructed or too far from the speaker", "Operating system input gain is muted or very low"], solutions: ["Check the selected microphone", "Increase microphone input gain", "Move closer to the microphone", "Verify the microphone is not muted at the operating system or hardware level"] }, 31006: { name: "LOW_INBOUND_AUDIO", message: "Low inbound audio detected", description: "Inbound (remote) audio level stayed below the acceptable threshold for multiple consecutive stats intervals while RTP packets continued to flow. This may indicate the remote party is sending silence or comfort-noise (e.g. one-way audio caused by a media bridge issue), as opposed to LOW_BYTES_RECEIVED which fires when no bytes arrive at all.", causes: ["Remote party microphone is muted or capturing very low audio", "Media bridge or PBX is injecting comfort-noise/silence instead of forwarding real audio", "One-way audio where RTP flows but content is silent (server-side media issue)", "Remote party is on hold or not speaking"], solutions: ["Verify the remote party is not muted and is actively speaking", "Check the media bridge / PBX for comfort-noise injection or transcoding issues", "Inspect PCAP RTP payload uniqueness to distinguish real audio from comfort-noise", "If the issue persists, report the call for server-side media investigation"] }, 32001: { name: "LOW_BYTES_RECEIVED", message: "No audio data received", description: "No bytes have been received from the remote party for multiple consecutive seconds. This may indicate a network interruption or remote-side issue.", causes: ["Network interruption", "Remote party microphone issue", "Firewall blocking inbound media"], solutions: ["Check network connectivity", "Ask remote party to check their microphone", "Check firewall rules for media ports"] }, 32002: { name: "LOW_BYTES_SENT", message: "No audio data being sent", description: "No bytes have been sent for multiple consecutive seconds. This may indicate a local microphone issue or network interruption.", causes: ["Microphone muted or disconnected", "Network interruption", "Local media track ended"], solutions: ["Check that the microphone is not muted", "Verify the microphone is still connected", "Check network connectivity"] }, 32003: { name: "RECORDING_UNAVAILABLE", message: "Call recording is not available in this browser", description: "Call recording was enabled (enableCallRecording is true), but the browser does not support MediaStreamTrackProcessor (required to capture raw audio PCM). Recording is disabled for this call; the call proceeds normally. Currently affects Firefox and older Chromium (< 94).", causes: ["Browser does not implement MediaStreamTrackProcessor (Firefox, Safari)", "Chromium version older than 94", "Recording enabled on an unsupported platform"], solutions: ["Use a recent Chromium-based browser (Chrome 94+, Edge 94+) to capture recordings", "Disable enableCallRecording if recording is not required for this deployment", "See the enableCallRecording JSDoc for platform support details"] }, 32004: { name: "RECORDING_BUFFER_OVERFLOW", message: "Call recording buffer overflow \u2014 oldest packets dropped", description: "The in-memory call recording buffer reached its maxBufferBytes cap before the next scheduled flush. The oldest captured RTP packets were dropped to keep memory bounded. This indicates the flush interval is too long for the call audio rate, or the upload endpoint is slow/unreachable. The recording continues with the remaining (newer) packets.", causes: ["Flush interval (callRecordingFlushIntervalMs) is larger than the buffer can hold at the current audio rate", "Upload endpoint (/call_recording) is slow or unreachable, so intermediate flushes back up", "A very long call with high sample rate filling the buffer between flushes"], solutions: ["Reduce callRecordingFlushIntervalMs so flushes happen more frequently", "Increase callRecordingMaxBufferBytes if memory headroom allows", "Check the /call_recording endpoint health and network path to voice-sdk-proxy"] }, 33001: { name: "ICE_CONNECTIVITY_LOST", message: "Connection interrupted", description: "The ICE connection transitioned to the disconnected state. The previously selected connection path was lost and renegotiation may be required. The connection may recover automatically.", causes: ["Temporary network interruption", "Network interface change (e.g. Wi-Fi to cellular)", "NAT rebinding"], solutions: ["Wait for automatic recovery", "Check network connectivity"] }, 33002: { name: "ICE_GATHERING_TIMEOUT", message: "ICE gathering timed out", description: "ICE candidate gathering did not complete within the safety timeout. This is typically caused by network restrictions blocking STUN/TURN. The call may still succeed if candidates arrive late.", causes: ["Firewall blocking STUN/TURN", "Network unreachable", "STUN/TURN server not responding"], solutions: ["Check STUN/TURN server reachability", "Ensure UDP traffic is not blocked", "Try forceRelayCandidate option"] }, 33003: { name: "ICE_GATHERING_EMPTY", message: "No ICE candidates gathered", description: "No ICE candidates were gathered after sending the initial SDP. This may indicate a firewall blocking all STUN/TURN traffic or no available network interface.", causes: ["Firewall blocking all STUN/TURN traffic", "No network interface available", "VPN blocking UDP"], solutions: ["Check STUN/TURN server reachability", "Ensure UDP traffic is not blocked", "Use forceRelayCandidate option"] }, 33004: { name: "PEER_CONNECTION_FAILED", message: "Connection failed", description: "RTCPeerConnection entered the failed state. This is a recoverable condition \u2014 the SDK may attempt ICE restart or the connection may recover. If it does not recover, the call will eventually be terminated.", causes: ["ICE failure", "DTLS handshake failure", "Prolonged network interruption"], solutions: ["Wait for automatic recovery", "Check network connectivity", "Verify TURN server credentials"] }, 33005: { name: "ONLY_HOST_ICE_CANDIDATES", message: "Only local network candidates available", description: "ICE gathering completed with host (local network) candidates only. This is diagnostic evidence that no server-reflexive (srflx), peer-reflexive (prflx), or relay (TURN) candidates were found; connectivity may still succeed, but network traversal options can be limited.", causes: ["STUN/TURN servers unreachable", "Firewall blocking UDP traffic to STUN/TURN servers", "Incorrect TURN server configuration or credentials", "Restrictive corporate network or VPN"], solutions: ["Verify STUN/TURN server URLs and credentials", "Ensure UDP traffic to STUN/TURN ports is not blocked", "Check firewall or VPN settings", "Try using TCP-based TURN as a fallback"] }, 33006: { name: "ANSWER_WHILE_PEER_ACTIVE", message: "Call answer ignored because a peer connection is already active", description: "answer() was called on a call that already has an active or connecting peer connection. Creating a second peer connection for the same call would duplicate media negotiation, confuse the remote party, and break call reporting. This is typically caused by application code invoking answer() multiple times (e.g. from multiple event handlers).", causes: ["Application called answer() twice on the same call object", "Multiple click handlers or event listeners triggering answer()"], solutions: ["Ensure answer() is called only once per call", "Disable the answer button after the first click", "Check that answer() is not invoked from multiple event handlers"] }, 33008: { name: "ICE_CANDIDATE_PAIR_CHANGED", message: "ICE candidate pair changed mid-call", description: "The selected ICE candidate pair changed during an active call. This indicates a network path shift \u2014 for example, a Wi-Fi to cellular handoff, a NAT rebinding, or a relay fallback. The call may continue normally, but the path change can briefly affect audio quality.", causes: ["Network interface change (e.g. Wi-Fi to cellular)", "NAT rebinding or IP address change", "Previous candidate pair failed and ICE selected an alternative", "Network topology change"], solutions: ["Monitor for audio quality degradation after the path change", "Check network stability if changes are frequent", "Verify TURN server configuration for relay fallback"] }, 33007: { name: "DUPLICATE_INBOUND_ANSWER", message: "Call answer ignored because another inbound call is already being answered", description: "answer() was called on an inbound call while another inbound call is already answering or active in this JavaScript runtime. Answering both legs can trigger SIP 486 USER_BUSY / LOSE_RACE when duplicate WebSocket registrations receive the same incoming call.", causes: ["Multiple TelnyxRTC instances in the same page", "Application code recreating a client without disconnecting the previous instance", "Duplicate inbound call notifications produced by duplicate WebSocket registrations"], solutions: ["Keep a single active TelnyxRTC instance for inbound call handling", "Call disconnect() before replacing an SDK client instance", "Only call answer() for one inbound call notification at a time"] }, 34001: { name: "TOKEN_EXPIRING_SOON", message: "Authentication token expiring soon", description: "The authentication token is approaching its expiration time. If the token expires the connection will be lost and calls will fail. A new token should be generated before expiration.", causes: ["Token was issued with a limited lifetime"], solutions: ["Generate a new authentication token", "Reconnect with fresh credentials before the token expires"] }, 36003: { name: "SIGNALING_RECOVERY_REQUIRED", message: "Signaling recovery required", description: "The signaling (WebSocket) path has been detected as unhealthy and the SDK will force-close the socket and reconnect. The source field in the warning payload indicates what triggered the recovery (probe, request, peer_failure, or no_rtp). Active calls will be recovered via reattach after reconnection.", causes: ["WebSocket probe timed out with no response", "Critical signaling request timed out", "Peer/media failure detected while signaling is also unhealthy"], solutions: ["The SDK will automatically reconnect and recover the call", "Check for network interface changes or interruptions", "Verify firewall/NAT timeout settings"] }, 36004: { name: "MEDIA_RECOVERY_REQUIRED", message: "Media recovery required", description: "The peer connection or media flow has been detected as unhealthy while signaling is healthy. The SDK will attempt ICE restart to recover the media path. No socket reconnection is needed.", causes: ["ICE connection state changed to failed", "RTCPeerConnection state changed to failed", "No RTP packets/bytes received while media should be active"], solutions: ["The SDK will automatically attempt ICE restart", "Check network connectivity and ICE candidate availability", "Verify TURN server configuration"] }, 36005: { name: "RECONNECTION_FAILED_WITH_NO_AUTO_RECONNECT", message: "Reconnection failed \u2014 auto-reconnect disabled", description: "The WebSocket was closed and auto-reconnect is disabled, so the SDK will not attempt to reconnect. This typically occurs after the user called disconnect() or after reconnection attempts were exhausted.", causes: ["Auto-reconnect was disabled by the application", "Reconnection attempts were previously exhausted", "The session was intentionally disconnected"], solutions: ["Call connect() manually to re-establish the session", "Check if disconnect() was called intentionally", "Review maxReconnectAttempts configuration"] }, 33009: { name: "AUDIO_INPUT_DEVICE_CHANGE_SKIPPED", message: "Audio input device change skipped", description: "The SDK could not change the microphone because the active peer connection has no audio RTP sender to replace. The existing local media and mute state were left unchanged.", causes: ["The call was created without an audio sender", "The peer connection was not ready when setAudioInDevice was called", "The call is already ending or the local media sender was removed"], solutions: ["Retry after the call is active and local media is attached", "Verify the call was started with audio enabled", "Inspect call state and peer connection sender availability"] }, 33010: { name: "MULTIPLE_ACTIVE_CALLS_DETECTED", message: "Multiple active calls detected in one SDK session", description: "A new call was created or received while another call is still active (ringing, answering, active, held, or recovering) in the same SDK session. This may be intentional for some applications (e.g. call waiting, transfer) but is often abnormal and makes call reports and application behavior harder to reason about. The new call proceeds normally \u2014 this warning is diagnostic only.", causes: ["Application created an outbound call while another call is active", "An inbound call arrived while another call is already active", "Application did not hang up the previous call before starting a new one", "Call waiting or multi-call scenario (may be intentional)"], solutions: ["Verify this is the expected behavior for your application", "Ensure the previous call is hung up before creating a new one if only one call is expected", "Use call.hold() before starting a new call if needed", "Check the warning payload for call IDs to correlate which calls are involved"] }, 33011: { name: "SHARED_REMOTE_ELEMENT_OVERWRITE", message: "Remote media element overwritten by another call", description: "A new MediaStream was attached to an HTML media element (audio/video) that already held a different MediaStream from another active call. The SDK overwrote the existing stream (last-writer-wins), which disrupts the other call remote media playout. This happens when two concurrent calls share a single remoteElement instead of each having its own. Use a per-call remoteElement (client.newCall({ remoteElement }) or call.answer({ remoteElement })) so each call owns a distinct element.", causes: ["Two concurrent calls share one remoteElement (legacy single-element app)", "Application did not pass a per-call remoteElement to newCall() or answer()", "A second inbound call rang into a session using one session-level remoteElement"], solutions: ["Pass a distinct remoteElement per call via client.newCall({ remoteElement })", "For inbound calls, override at answer time via call.answer({ remoteElement })", "Give each call its own <audio>/<video> element so attach/detach lifecycles are independent"] }, 35002: { name: "UNKNOWN_REATTACHED_SESSION", message: "Unknown reattach session after reconnect", description: "The WebSocket reconnected successfully and the server sent an Attach message for a session that does not match any active SDK call. The unknown Attach is ACK'd and ignored.", causes: ["Server sent an Attach for a call that no longer exists in the SDK", "Multiple Attach messages arrived and only the first was recovered", "Race condition between reconnection and new inbound call"], solutions: ["Check application logic for multiple simultaneous calls", "Inspect the Attach callID in the warning payload for details", "If a call should be active, start a new call manually"] } };
        function be(e2, t2) {
          const i2 = ye[e2];
          return { code: e2, name: i2.name, message: t2 || i2.message, description: i2.description, causes: [...i2.causes], solutions: [...i2.solutions] };
        }
        class Ie extends Error {
          constructor(e2) {
            super(e2.message || `[${e2.code}] ${e2.name}`), this.name = e2.name, this.code = e2.code, this.description = e2.description, this.causes = e2.causes, this.solutions = e2.solutions, this.originalError = e2.originalError, this.fatal = e2.fatal, Object.setPrototypeOf(this, Ie.prototype);
          }
          toJSON() {
            return { code: this.code, name: this.name, description: this.description, message: this.message, causes: this.causes, solutions: this.solutions, originalError: this.originalError, fatal: this.fatal };
          }
        }
        function Ee(e2) {
          if (e2 instanceof DOMException) {
            if ("NotAllowedError" === e2.name) return m;
            if ("NotFoundError" === e2.name || "OverconstrainedError" === e2.name) return f;
          }
          return _;
        }
        function Ce(e2, t2, i2, n2) {
          const s2 = Se[e2], o2 = t2 instanceof Error ? t2 : void 0 !== t2 ? new Error(String(t2)) : void 0;
          return new Ie({ code: e2, name: s2.name, description: s2.description, message: i2 || s2.message, causes: [...s2.causes], solutions: [...s2.solutions], originalError: o2, fatal: null != n2 ? n2 : s2.fatal });
        }
        class we extends Error {
          constructor(e2, t2, i2 = "") {
            super(`Signaling request timed out (id=${e2}, method=${i2 || "unknown"}, timeout=${t2}ms)`), this.name = "RequestTimeoutError", this.requestId = e2, this.timeoutMs = t2, this.method = i2;
          }
        }
        class Te extends Error {
          constructor(e2, t2, i2) {
            super(`Stale request cancelled (id=${e2}, gen=${t2}, current=${i2})`), this.name = "StaleRequestError", this.requestId = e2, this.staleGeneration = t2, this.currentGeneration = i2;
          }
        }
        var ke = "undefined" != typeof globalThis ? globalThis : "undefined" != typeof window ? window : "undefined" != typeof global ? global : "undefined" != typeof self ? self : {};
        function Re(e2, t2) {
          return e2(t2 = { exports: {} }, t2.exports), t2.exports;
        }
        var Ae = Re((function(e2) {
          var t2, i2;
          t2 = ke, i2 = function() {
            var e3 = function() {
            }, t3 = "undefined", i3 = typeof window !== t3 && typeof window.navigator !== t3 && /Trident\/|MSIE /.test(window.navigator.userAgent), n2 = ["trace", "debug", "info", "warn", "error"];
            function s2(e4, t4) {
              var i4 = e4[t4];
              if ("function" == typeof i4.bind) return i4.bind(e4);
              try {
                return Function.prototype.bind.call(i4, e4);
              } catch (t5) {
                return function() {
                  return Function.prototype.apply.apply(i4, [e4, arguments]);
                };
              }
            }
            function o2() {
              console.log && (console.log.apply ? console.log.apply(console, arguments) : Function.prototype.apply.apply(console.log, [console, arguments])), console.trace && console.trace();
            }
            function r2(t4, i4) {
              for (var s3 = 0; s3 < n2.length; s3++) {
                var o3 = n2[s3];
                this[o3] = s3 < t4 ? e3 : this.methodFactory(o3, t4, i4);
              }
              this.log = this.debug;
            }
            function a2(e4, i4, n3) {
              return function() {
                typeof console !== t3 && (r2.call(this, i4, n3), this[e4].apply(this, arguments));
              };
            }
            function c2(n3, r3, c3) {
              return (function(n4) {
                return "debug" === n4 && (n4 = "log"), typeof console !== t3 && ("trace" === n4 && i3 ? o2 : void 0 !== console[n4] ? s2(console, n4) : void 0 !== console.log ? s2(console, "log") : e3);
              })(n3) || a2.apply(this, arguments);
            }
            function l2(e4, i4, s3) {
              var o3, a3 = this;
              i4 = null == i4 ? "WARN" : i4;
              var l3 = "loglevel";
              function d3() {
                var e5;
                if (typeof window !== t3 && l3) {
                  try {
                    e5 = window.localStorage[l3];
                  } catch (e6) {
                  }
                  if (typeof e5 === t3) try {
                    var i5 = window.document.cookie, n3 = i5.indexOf(encodeURIComponent(l3) + "=");
                    -1 !== n3 && (e5 = /^([^;]+)/.exec(i5.slice(n3))[1]);
                  } catch (e6) {
                  }
                  return void 0 === a3.levels[e5] && (e5 = void 0), e5;
                }
              }
              "string" == typeof e4 ? l3 += ":" + e4 : "symbol" == typeof e4 && (l3 = void 0), a3.name = e4, a3.levels = { TRACE: 0, DEBUG: 1, INFO: 2, WARN: 3, ERROR: 4, SILENT: 5 }, a3.methodFactory = s3 || c2, a3.getLevel = function() {
                return o3;
              }, a3.setLevel = function(i5, s4) {
                if ("string" == typeof i5 && void 0 !== a3.levels[i5.toUpperCase()] && (i5 = a3.levels[i5.toUpperCase()]), !("number" == typeof i5 && i5 >= 0 && i5 <= a3.levels.SILENT)) throw "log.setLevel() called with invalid level: " + i5;
                if (o3 = i5, false !== s4 && (function(e5) {
                  var i6 = (n2[e5] || "silent").toUpperCase();
                  if (typeof window !== t3 && l3) {
                    try {
                      return void (window.localStorage[l3] = i6);
                    } catch (e6) {
                    }
                    try {
                      window.document.cookie = encodeURIComponent(l3) + "=" + i6 + ";";
                    } catch (e6) {
                    }
                  }
                })(i5), r2.call(a3, i5, e4), typeof console === t3 && i5 < a3.levels.SILENT) return "No console available for logging";
              }, a3.setDefaultLevel = function(e5) {
                i4 = e5, d3() || a3.setLevel(e5, false);
              }, a3.resetLevel = function() {
                a3.setLevel(i4, false), (function() {
                  if (typeof window !== t3 && l3) {
                    try {
                      return void window.localStorage.removeItem(l3);
                    } catch (e5) {
                    }
                    try {
                      window.document.cookie = encodeURIComponent(l3) + "=; expires=Thu, 01 Jan 1970 00:00:00 UTC";
                    } catch (e5) {
                    }
                  }
                })();
              }, a3.enableAll = function(e5) {
                a3.setLevel(a3.levels.TRACE, e5);
              }, a3.disableAll = function(e5) {
                a3.setLevel(a3.levels.SILENT, e5);
              };
              var u3 = d3();
              null == u3 && (u3 = i4), a3.setLevel(u3, false);
            }
            var d2 = new l2(), u2 = {};
            d2.getLogger = function(e4) {
              if ("symbol" != typeof e4 && "string" != typeof e4 || "" === e4) throw new TypeError("You must supply a name when creating a logger.");
              var t4 = u2[e4];
              return t4 || (t4 = u2[e4] = new l2(e4, d2.getLevel(), d2.methodFactory)), t4;
            };
            var h2 = typeof window !== t3 ? window.log : void 0;
            return d2.noConflict = function() {
              return typeof window !== t3 && window.log === d2 && (window.log = h2), d2;
            }, d2.getLoggers = function() {
              return u2;
            }, d2.default = d2, d2;
          }, e2.exports ? e2.exports = i2() : t2.log = i2();
        }));
        const Oe = { debug: 0, info: 1, warn: 2, error: 3 };
        class De {
          constructor(e2 = {}) {
            var t2, i2, n2;
            this.buffer = [], this.isCapturing = false, this.options = { enabled: null !== (t2 = e2.enabled) && void 0 !== t2 && t2, level: null !== (i2 = e2.level) && void 0 !== i2 ? i2 : "debug", maxEntries: null !== (n2 = e2.maxEntries) && void 0 !== n2 ? n2 : 1e3 };
          }
          start() {
            this.options.enabled && (this.isCapturing = true, this.buffer = []);
          }
          stop() {
            this.isCapturing = false;
          }
          addEntry(e2, t2, i2) {
            if (!this.isCapturing || !this.options.enabled) return;
            if (Oe[e2] < Oe[this.options.level]) return;
            const n2 = Object.assign({ timestamp: (/* @__PURE__ */ new Date()).toISOString(), level: e2, message: t2 }, i2 && Object.keys(i2).length > 0 ? { context: i2 } : {});
            this.buffer.push(n2), this.buffer.length > this.options.maxEntries && this.buffer.shift();
          }
          getLogs() {
            return [...this.buffer];
          }
          getLogCount() {
            return this.buffer.length;
          }
          drain() {
            const e2 = this.buffer;
            return this.buffer = [], e2;
          }
          clear() {
            this.buffer = [];
          }
          isActive() {
            return this.isCapturing;
          }
          isEnabled() {
            return this.options.enabled;
          }
        }
        let Ne = null;
        const Le = Ae.getLogger("telnyx"), Me = { trace: 0, debug: 1, info: 2, warn: 3, error: 4 };
        let Pe = Me.info;
        function xe(e2) {
          if (null == e2) return e2;
          if ("object" != typeof e2) return e2;
          try {
            const t3 = JSON.stringify(e2), i2 = JSON.parse(t3);
            if ("object" == typeof i2 && null !== i2 && Object.keys(i2).length > 1) return i2;
          } catch (e3) {
          }
          const t2 = {};
          for (const i2 in e2) try {
            const n2 = e2[i2];
            if ("function" == typeof n2) continue;
            if ("object" == typeof n2 && null !== n2) try {
              t2[i2] = JSON.parse(JSON.stringify(n2));
            } catch (e3) {
              t2[i2] = String(n2);
            }
            else t2[i2] = n2;
          } catch (e3) {
          }
          return Object.keys(t2).length > 0 ? t2 : { value: String(e2) };
        }
        const Ue = Le.methodFactory;
        Le.methodFactory = (e2, t2, i2) => {
          const n2 = Ue(e2, t2, i2);
          return function(...t3) {
            if (Me[e2] >= Pe) {
              const e3 = [(/* @__PURE__ */ new Date()).toISOString().replace("T", " ").replace("Z", ""), "-"];
              for (const i4 of t3) e3.push(i4);
              n2(...e3);
            }
            const i3 = Ne;
            if (null == i3 ? void 0 : i3.isActive()) {
              const [n3, ...s2] = t3, o2 = "string" == typeof n3 ? n3 : JSON.stringify(n3);
              let r2;
              s2.length > 0 && (r2 = 1 === s2.length && "object" == typeof s2[0] && null !== s2[0] ? xe(s2[0]) : { args: s2.map(xe) }), i3.addEntry(e2, o2, r2);
            }
          };
        }, Le.setLevel("debug", false);
        const Fe = (e2) => {
          const [t2, i2, n2, s2, o2, r2] = e2;
          let a2 = {};
          try {
            a2 = JSON.parse(o2.replace(/ID"/g, 'Id"'));
          } catch (e3) {
            Le.warn("Verto LA invalid media JSON string:", o2);
          }
          return { participantId: Number(t2), participantNumber: i2, participantName: n2, codec: s2, media: a2, participantData: r2 };
        }, $e = (e2) => {
          if ("string" != typeof e2) return e2;
          try {
            return JSON.parse(e2);
          } catch (t2) {
            return e2;
          }
        }, Be = (e2) => e2 instanceof Function || "function" == typeof e2, je = (e2) => "object" == typeof document && "getElementById" in document ? "string" == typeof e2 ? document.getElementById(e2) || null : "function" == typeof e2 ? e2() : e2 instanceof HTMLMediaElement ? e2 : null : null, He = /^(ws|wss):\/\//, We = (e2, t2 = null) => {
          const { result: i2 = {}, error: n2 } = e2;
          if (n2) return { error: n2 };
          const { result: s2 = null } = i2;
          if (null === s2) return null !== t2 && (i2.node_id = t2), { result: i2 };
          const { code: o2 = null, node_id: r2 = null, result: a2 = null } = s2;
          return o2 && "200" !== o2 ? { error: s2 } : a2 ? We(a2, r2) : { result: s2 };
        }, Ge = ({ login: e2, passwd: t2, password: i2, login_token: n2 }) => Boolean(e2 && (t2 || i2) || n2), Ve = ({ anonymous_login: e2 }) => Boolean(e2) && Boolean(e2.target_id) && Boolean(e2.target_type), qe = (e2) => {
          var t2, i2, n2, s2, o2, r2;
          let a2 = "", c2 = "";
          (null === (i2 = null === (t2 = null == e2 ? void 0 : e2.result) || void 0 === t2 ? void 0 : t2.params) || void 0 === i2 ? void 0 : i2.state) && (a2 = null === (s2 = null === (n2 = null == e2 ? void 0 : e2.result) || void 0 === n2 ? void 0 : n2.params) || void 0 === s2 ? void 0 : s2.state), (null === (o2 = null == e2 ? void 0 : e2.params) || void 0 === o2 ? void 0 : o2.state) && (c2 = null === (r2 = null == e2 ? void 0 : e2.params) || void 0 === r2 ? void 0 : r2.state);
          return a2 || c2;
        };
        function Ye({ debounceTime: e2 }) {
          let t2, i2;
          return { promise: new Promise(((n2, s2) => {
            t2 = e2 ? Ke(n2, e2) : n2, i2 = s2;
          })), resolve: t2, reject: i2 };
        }
        const Ke = (e2, t2) => {
          let i2;
          return (...n2) => {
            clearTimeout(i2), i2 = window.setTimeout((() => {
              e2(...n2);
            }), t2);
          };
        }, Je = "telnyx-voice-sdk-id", ze = "telnyx-voice-sdk-session-id", Xe = "telnyx-voice-sdk-session-id-stored-at", Qe = "telnyx-voice-sdk-active-calls", Ze = 9e4;
        function et(e2 = Date.now()) {
          const t2 = (function() {
            const e3 = Number(sessionStorage.getItem(Xe));
            return Number.isFinite(e3) ? e3 : null;
          })();
          return null !== t2 && e2 - t2 <= Ze;
        }
        function tt() {
          return sessionStorage.getItem(Je);
        }
        function it(e2, t2 = Date.now()) {
          sessionStorage.setItem(ze, e2), sessionStorage.setItem(Xe, String(t2));
        }
        function nt() {
          sessionStorage.removeItem(Je), sessionStorage.removeItem(ze), sessionStorage.removeItem(Xe);
        }
        function st() {
          !(function(e2) {
            try {
              sessionStorage.removeItem(e2);
            } catch (t2) {
              Le.debug(`safeRemoveItem('${e2}') failed: ${t2 instanceof Error ? t2.message : String(t2)}`);
            }
          })(Qe);
        }
        function ot(e2 = Date.now()) {
          const t2 = (function(e3) {
            try {
              return sessionStorage.getItem(e3);
            } catch (t3) {
              return Le.debug(`safeGetItem('${e3}') failed: ${t3 instanceof Error ? t3.message : String(t3)}`), null;
            }
          })(Qe);
          if (!t2) return null;
          try {
            const i2 = JSON.parse(t2);
            if (!i2 || "object" != typeof i2 || !Array.isArray(i2.calls)) return Le.debug("Active-calls recovery marker payload was malformed \u2014 discarded."), st(), null;
            const n2 = Number(i2.storedAt);
            return !Number.isFinite(n2) || e2 - n2 > 9e5 ? (Le.debug("Active-calls recovery marker was stale or had an invalid timestamp \u2014 discarded."), st(), null) : 0 === i2.calls.length ? (Le.debug("Active-calls recovery marker had no call records \u2014 discarded."), st(), null) : i2;
          } catch (e3) {
            return Le.debug(`Active-calls recovery marker JSON parse failed \u2014 discarded: ${e3 instanceof Error ? e3.message : String(e3)}`), st(), null;
          }
        }
        function rt(e2, t2, i2 = Date.now()) {
          if (!Array.isArray(e2) || 0 === e2.length) return void st();
          const n2 = { sessionId: t2, calls: e2, storedAt: i2 };
          !(function(e3, t3) {
            try {
              sessionStorage.setItem(e3, t3);
            } catch (t4) {
              Le.info(`safeSetItem('${e3}') failed: ${t4 instanceof Error ? t4.message : String(t4)}`);
            }
          })(Qe, JSON.stringify(n2));
        }
        var at, ct, lt;
        !(function(e2) {
          e2.Offer = "offer", e2.Answer = "answer";
        })(at || (at = {})), (function(e2) {
          e2.Inbound = "inbound", e2.Outbound = "outbound";
        })(ct || (ct = {})), (function(e2) {
          e2.Invite = "telnyx_rtc.invite", e2.Attach = "telnyx_rtc.attach", e2.Answer = "telnyx_rtc.answer", e2.Info = "telnyx_rtc.info", e2.Candidate = "telnyx_rtc.candidate", e2.EndOfCandidates = "telnyx_rtc.endOfCandidates", e2.Display = "telnyx_rtc.display", e2.Media = "telnyx_rtc.media", e2.Event = "telnyx_rtc.event", e2.Bye = "telnyx_rtc.bye", e2.Punt = "telnyx_rtc.punt", e2.Broadcast = "telnyx_rtc.broadcast", e2.Subscribe = "telnyx_rtc.subscribe", e2.Unsubscribe = "telnyx_rtc.unsubscribe", e2.ClientReady = "telnyx_rtc.clientReady", e2.Modify = "telnyx_rtc.modify", e2.Ringing = "telnyx_rtc.ringing", e2.GatewayState = "telnyx_rtc.gatewayState", e2.Ping = "telnyx_rtc.ping", e2.Pong = "telnyx_rtc.pong";
        })(lt || (lt = {}));
        const dt = { generic: "event", [lt.Display]: "participantData", [lt.Attach]: "participantData", conferenceUpdate: "conferenceUpdate", callUpdate: "callUpdate", vertoClientReady: "vertoClientReady", userMediaError: "userMediaError", peerConnectionFailureError: "peerConnectionFailureError", signalingStateClosed: "signalingStateClosed" }, ut = { invalidCredentialsOptions: "InvalidCredentialsOptions" }, ht = 8e6, pt = { destinationNumber: "", remoteCallerName: "Outbound Call", remoteCallerNumber: "", callerName: "", callerNumber: "", audio: true, useStereo: false, debug: false, debugOutput: "socket", attach: false, screenShare: false, userVariables: {}, mediaSettings: { useSdpASBandwidthKbps: false, sdpASBandwidthKbps: 0 }, mutedMicOnStart: false, prefetchIceCandidates: true };
        var gt, vt, mt, ft, _t, St;
        !(function(e2) {
          e2[e2.New = 0] = "New", e2[e2.Requesting = 1] = "Requesting", e2[e2.Trying = 2] = "Trying", e2[e2.Recovering = 3] = "Recovering", e2[e2.Ringing = 4] = "Ringing", e2[e2.Answering = 5] = "Answering", e2[e2.Early = 6] = "Early", e2[e2.Active = 7] = "Active", e2[e2.Held = 8] = "Held", e2[e2.Hangup = 9] = "Hangup", e2[e2.Destroy = 10] = "Destroy", e2[e2.Purge = 11] = "Purge";
        })(gt || (gt = {})), (function(e2) {
          e2.Participant = "participant", e2.Moderator = "moderator";
        })(vt || (vt = {})), (function(e2) {
          e2.Join = "join", e2.Leave = "leave", e2.Bootstrap = "bootstrap", e2.Add = "add", e2.Modify = "modify", e2.Delete = "delete", e2.Clear = "clear", e2.ChatMessage = "chatMessage", e2.LayerInfo = "layerInfo", e2.LogoInfo = "logoInfo", e2.LayoutInfo = "layoutInfo", e2.LayoutList = "layoutList", e2.ModCmdResponse = "modCommandResponse";
        })(mt || (mt = {})), (function(e2) {
          e2.Video = "videoinput", e2.AudioIn = "audioinput", e2.AudioOut = "audiooutput";
        })(ft || (ft = {})), (function(e2) {
          e2.REGED = "REGED", e2.UNREGED = "UNREGED", e2.NOREG = "NOREG", e2.FAILED = "FAILED", e2.FAIL_WAIT = "FAIL_WAIT", e2.TIMEOUT = "TIMEOUT", e2.REGISTER = "REGISTER", e2.TRYING = "TRYING", e2.EXPIRED = "EXPIRED", e2.UNREGISTER = "UNREGISTER";
        })(_t || (_t = {})), (function(e2) {
          e2.Hold = "hold", e2.Unhold = "unhold", e2.ToggleHold = "toggleHold", e2.UpdateMedia = "updateMedia";
        })(St || (St = {}));
        const yt = "GLOBAL", bt = {}, It = (e2, t2) => `${e2}|${t2}`, Et = (e2, t2 = yt) => It(e2, t2) in bt, Ct = (e2, t2, i2 = yt) => {
          const n2 = It(e2, i2);
          n2 in bt || (bt[n2] = []), bt[n2].push(t2);
        }, wt = (e2, t2, i2 = yt) => {
          const n2 = function(s2) {
            Tt(e2, n2, i2), t2(s2);
          };
          return n2.prototype.targetRef = t2, Ct(e2, n2, i2);
        }, Tt = (e2, t2, i2 = yt) => {
          if (!Et(e2, i2)) return false;
          const n2 = It(e2, i2);
          if (Be(t2)) {
            for (let e3 = bt[n2].length - 1; e3 >= 0; e3--) {
              const i3 = bt[n2][e3];
              (t2 === i3 || i3.prototype && t2 === i3.prototype.targetRef) && bt[n2].splice(e3, 1);
            }
          } else bt[n2] = [];
          return 0 === bt[n2].length && delete bt[n2], true;
        }, kt = (e2, t2, i2 = yt, n2 = true) => {
          const s2 = n2 && i2 !== yt;
          if (!Et(e2, i2)) return s2 && kt(e2, t2), false;
          const o2 = It(e2, i2), r2 = bt[o2].length;
          if (!r2) return s2 && kt(e2, t2), false;
          for (let e3 = r2 - 1; e3 >= 0; e3--) bt[o2][e3](t2);
          return s2 && kt(e2, t2), true;
        }, Rt = (e2) => {
          const t2 = It(e2, "");
          Object.keys(bt).filter(((e3) => 0 === e3.indexOf(t2))).forEach(((e3) => delete bt[e3]));
        };
        let At = "undefined" != typeof WebSocket ? WebSocket : null;
        const Ot = 0, Dt = 1, Nt = 2, Lt = 3;
        class Mt {
          constructor(e2) {
            this.session = e2, this.previousGatewayState = "", this.lastInboundAt = 0, this.socketGeneration = 0, this._wsClient = null, this._host = ae, this._timers = {}, this._useCanaryRtcServer = false, this._hasCanaryBeenUsed = false, this._safetyTimeoutId = null, this._pendingRequestIds = /* @__PURE__ */ new Set(), this._pendingRequestTimers = /* @__PURE__ */ new Map(), this._pendingRequestRejecters = /* @__PURE__ */ new Map(), this.upDur = null, this.downDur = null;
            const { host: t2, env: i2, region: n2, useCanaryRtcServer: s2 } = e2.options;
            Le.debug("Creating new Connection", { host: this._host, env: i2, region: n2, useCanaryRtcServer: s2 }), i2 && (this._host = "development" === i2 ? "wss://rtcdev.telnyx.com" : ae), t2 && (this._host = ((e3) => `${He.test(e3) ? "" : "wss://"}${e3}`)(t2)), n2 && (this._host = this._host.replace(/rtc(dev)?/, `${n2}.rtc$1`)), s2 && (this._useCanaryRtcServer = true);
          }
          get connected() {
            return !!this._wsClient && this._wsClient.readyState === Dt;
          }
          get connecting() {
            return !!this._wsClient && this._wsClient.readyState === Ot;
          }
          get closing() {
            return !!this._wsClient && this._wsClient.readyState === Nt;
          }
          get closed() {
            return !!this._wsClient && this._wsClient.readyState === Lt;
          }
          get isAlive() {
            return this.connecting || this.connected;
          }
          get isDead() {
            return this.closing || this.closed;
          }
          get host() {
            return this._host;
          }
          connect() {
            Le.debug("Connection.connect() called", { host: this._host, socketGeneration: this.socketGeneration, sessionId: this.session.sessionid });
            const t2 = new URL(this._host);
            let i2 = tt();
            this.session.options.rtcIp && this.session.options.rtcPort && (i2 = null, this._useCanaryRtcServer = false, t2.searchParams.set("rtc_ip", this.session.options.rtcIp), t2.searchParams.set("rtc_port", this.session.options.rtcPort.toString())), i2 && t2.searchParams.set("voice_sdk_id", i2), this._useCanaryRtcServer && (t2.searchParams.set("canary", "true"), i2 && !this._hasCanaryBeenUsed && (t2.searchParams.delete("voice_sdk_id"), Le.debug("first canary connection. Refreshing voice_sdk_id")), this._hasCanaryBeenUsed = true), this.session.callReportVoiceSdkId = t2.searchParams.get("voice_sdk_id"), this.session.options.skipLastVoiceSdkId && t2.searchParams.has("voice_sdk_id") && t2.searchParams.set("skip_last_voice_sdk_id", "true"), this.session.options.skipTrailing && t2.searchParams.set("skip_trailing", "true");
            try {
              const e2 = this.socketGeneration;
              this._wsClient = new At(t2.toString()), this.socketGeneration += 1, Le.debug("WebSocket connection created", { sessionId: this.session.sessionid, voiceSdkId: this.session.callReportVoiceSdkId, socketGeneration: this.socketGeneration, reconnectCount: e2 }), this.lastInboundAt = 0, this._cleanupPendingRequests(), this._registerSocketEvents(this._wsClient);
            } catch (t3) {
              Le.error("WebSocket connection failed:", t3);
              const i3 = Ce(C, t3);
              kt(e.SwEvent.Error, { error: i3, sessionId: this.session.sessionid }, this.session.uuid), this.session._terminateActiveCallsLocally();
            }
          }
          sendRawText(e2) {
            var t2;
            null === (t2 = this._wsClient) || void 0 === t2 || t2.send(e2);
          }
          send(e2, t2) {
            var i2;
            const { request: n2 } = e2, s2 = this.socketGeneration, o2 = n2.method || "", r2 = new Promise(((e3, i3) => {
              if (n2.hasOwnProperty("result")) return e3();
              let r3 = false, a2 = null;
              const c2 = (t3) => {
                if (null !== a2 && (clearTimeout(a2), this._pendingRequestTimers.delete(n2.id), a2 = null), this._pendingRequestIds.delete(n2.id), this._pendingRequestRejecters.delete(n2.id), this.session.onOutboundConfirmed(), r3) return;
                const { result: s3, error: o3 } = We(t3);
                return o3 ? i3(o3) : e3(s3);
              };
              wt(n2.id, c2), this._pendingRequestIds.add(n2.id), this._pendingRequestRejecters.set(n2.id, { reject: i3, generation: s2 }), t2 && t2 > 0 && (a2 = setTimeout((() => {
                if (r3 = true, a2 = null, this._pendingRequestTimers.delete(n2.id), this._pendingRequestRejecters.delete(n2.id), Tt(n2.id, c2), this._pendingRequestIds.delete(n2.id), this.socketGeneration !== s2) return Le.debug(`Stale request timeout for ${n2.id} (gen ${s2}, current ${this.socketGeneration}) \u2014 settling with StaleRequestError`), void i3(new Te(n2.id, s2, this.socketGeneration));
                i3(new we(n2.id, t2, o2));
              }), t2), this._pendingRequestTimers.set(n2.id, a2));
            }));
            return Le.debug("SEND: \n", JSON.stringify(n2, null, 2), "\n"), null === (i2 = this._wsClient) || void 0 === i2 || i2.send(JSON.stringify(n2)), r2;
          }
          close() {
            if (Le.debug("Connection.close() called", { hasWsClient: !!this._wsClient, closing: this.closing, closed: this.closed, socketGeneration: this.socketGeneration, safetyTimeoutId: this._safetyTimeoutId, sessionId: this.session.sessionid }), !this._wsClient || this.closing) return;
            this._cleanupPendingRequests();
            const e2 = this._wsClient;
            Be(this._wsClient._beginClose) ? this._wsClient._beginClose() : this._wsClient.close(), this._safetyTimeoutId || (this._safetyTimeoutId = setTimeout((() => this._handleCloseTimeout(e2)), 5e3));
          }
          _registerSocketEvents(t2) {
            const i2 = this.socketGeneration;
            t2.onopen = (t3) => (Le.debug("WebSocket onopen", { socketGeneration: this.socketGeneration, sessionId: this.session.sessionid }), kt(e.SwEvent.SocketOpen, t3, this.session.uuid)), t2.onclose = (n2) => (this._clearSafetyTimeout(), this._safetyCleanupSocket(t2, "close"), Le.debug("WebSocket onclose", { code: null == n2 ? void 0 : n2.code, reason: null == n2 ? void 0 : n2.reason, wasClean: null == n2 ? void 0 : n2.wasClean, socketGeneration: i2, sessionId: this.session.sessionid }), kt(e.SwEvent.SocketClose, { event: n2, socketGeneration: i2 }, this.session.uuid)), t2.onerror = (n2) => {
              this._clearSafetyTimeout(), this._safetyCleanupSocket(t2, "error"), Le.debug("WebSocket onerror", { socketGeneration: this.socketGeneration, sessionId: this.session.sessionid });
              const s2 = Ce(w);
              return kt(e.SwEvent.Error, { error: s2, sessionId: this.session.sessionid }, this.session.uuid), kt(e.SwEvent.SocketError, { error: n2, sessionId: this.session.sessionid, socketGeneration: i2 }, this.session.uuid);
            }, t2.onmessage = (t3) => {
              var i3, n2;
              this.lastInboundAt = Date.now(), kt(e.SwEvent.SocketActivity, { timestamp: this.lastInboundAt }, this.session.uuid);
              const s2 = $e(t3.data);
              var o2;
              if ("string" != typeof s2) {
                if (s2.voice_sdk_id && (this.session.callReportVoiceSdkId = s2.voice_sdk_id, o2 = s2.voice_sdk_id, sessionStorage.setItem(Je, o2)), this._unsetTimer(s2.id), Le.debug("RECV: \n", JSON.stringify(s2, null, 2), "\n"), _t[`${null === (n2 = null === (i3 = null == s2 ? void 0 : s2.result) || void 0 === i3 ? void 0 : i3.params) || void 0 === n2 ? void 0 : n2.state}`] || !kt(s2.id, s2)) {
                  const t4 = qe(s2);
                  kt(e.SwEvent.SocketMessage, s2, this.session.uuid), Boolean(t4) && (this.previousGatewayState = t4);
                }
              } else this._handleStringResponse(s2);
            };
          }
          _deregisterSocketEvents(e2) {
            e2.onopen = null, e2.onclose = null, e2.onerror = null, e2.onmessage = null;
          }
          _handleCloseTimeout(t2) {
            this._safetyTimeoutId = null, t2 && t2.readyState !== Lt ? (Le.warn("Socket stuck in CLOSING after 5s \u2014 forcefully cleaning up"), this._deregisterSocketEvents(t2), this._safetyCleanupSocket(t2, "timeout"), this._wsClient && this._wsClient !== t2 ? Le.debug("Safety timeout: socket was replaced, not emitting SocketClose", { hasWsClient: !!this._wsClient, isSameSocket: this._wsClient === t2 }) : (Le.debug("Safety timeout: emitting SocketClose for stuck socket", { hasWsClient: !!this._wsClient, isSameSocket: this._wsClient === t2 }), kt(e.SwEvent.SocketClose, { code: ce.ABNORMAL_CLOSURE, reason: "STUCK_WS_TIMEOUT: Socket got stuck in CLOSING state and was forcefully cleaned up by safety timeout", wasClean: false, socketGeneration: this.socketGeneration }, this.session.uuid))) : Le.warn("Safety timeout fired but socket is already closed or cleaned up");
          }
          _clearSafetyTimeout() {
            this._safetyTimeoutId && (Le.debug("Clearing safety timeout"), clearTimeout(this._safetyTimeoutId), this._safetyTimeoutId = null);
          }
          _safetyCleanupSocket(e2, t2) {
            this._wsClient === e2 ? (Le.debug(`Nulling socket reference (reason: ${t2})`), this._wsClient = null) : Le.debug(`Skipping socket cleanup - old socket already replaced (reason: ${t2})`);
          }
          _cleanupPendingRequests() {
            Array.from(this._pendingRequestIds).forEach(((e2) => {
              Tt(e2);
              const t2 = this._pendingRequestTimers.get(e2), i2 = this._pendingRequestRejecters.get(e2);
              t2 && (clearTimeout(t2), this._pendingRequestTimers.delete(e2), i2 && i2.reject(new Te(e2, i2.generation, this.socketGeneration))), this._pendingRequestRejecters.delete(e2);
            })), this._pendingRequestIds.clear();
          }
          _unsetTimer(e2) {
            clearTimeout(this._timers[e2]), delete this._timers[e2];
          }
          _handleStringResponse(t2) {
            if (/^#SP/.test(t2)) switch (t2[3]) {
              case "U":
                this.upDur = parseInt(t2.substring(4));
                break;
              case "D":
                this.downDur = parseInt(t2.substring(4)), kt(e.SwEvent.SpeedTest, { upDur: this.upDur, downDur: this.downDur }, this.session.uuid);
            }
            else Le.warn("Unknown message from socket", t2);
          }
        }
        Mt.DEFAULT_REQUEST_TIMEOUT_MS = 1e4;
        class Pt {
          buildRequest(e2) {
            this.request = Object.assign({ jsonrpc: "2.0", id: c() }, e2);
          }
          buildNotification(e2) {
            this.request = Object.assign({ jsonrpc: "2.0" }, e2);
          }
        }
        const xt = { id: "callID", destinationNumber: "destination_number", remoteCallerName: "remote_caller_id_name", remoteCallerNumber: "remote_caller_id_number", callerName: "caller_id_name", callerNumber: "caller_id_number", customHeaders: "custom_headers" };
        class Ut extends Pt {
          constructor(e2 = {}) {
            if (super(), e2.hasOwnProperty("dialogParams")) {
              const i2 = t(e2.dialogParams, ["remoteSdp", "localStream", "remoteStream", "localElement", "remoteElement", "onNotification", "camId", "micId", "speakerId"]);
              for (const e3 in xt) e3 && i2.hasOwnProperty(e3) && (i2[xt[e3]] = i2[e3], delete i2[e3]);
              e2.dialogParams = i2;
            }
            this.buildRequest({ method: this.toString(), params: e2 });
          }
        }
        class Ft extends Ut {
          constructor(e2) {
            super(), this.method = lt.GatewayState;
            this.buildRequest({ method: this.method, voice_sdk_id: e2, params: {} });
          }
        }
        class $t {
          constructor(e2) {
            this.pendingRequestId = null, this.onSocketMessage = (e3) => i(this, void 0, void 0, (function* () {
              e3.id === this.pendingRequestId && this.gatewayStateTask.resolve(qe(e3));
            })), this.getIsRegistered = () => i(this, void 0, void 0, (function* () {
              const e3 = new Ft(tt());
              this.pendingRequestId = e3.request.id, this.gatewayStateTask = Ye({}), this.session.execute(e3);
              const t2 = yield this.gatewayStateTask.promise;
              return !!t2 && [_t.REGISTER, _t.REGED].includes(t2);
            })), this.session = e2, this.gatewayStateTask = Ye({}), this.session.on("telnyx.socket.message", this.onSocketMessage);
          }
        }
        class Bt extends Ut {
          constructor(e2) {
            super(), this.method = lt.Ping;
            this.buildRequest({ method: this.method, voice_sdk_id: e2, params: {} });
          }
        }
        class jt {
          constructor(e2) {
            this._session = e2, this._lastInboundAt = 0, this._lastOutboundConfirmedAt = 0, this._lastProbeSentAt = 0, this._probeInFlight = false, this._intervalId = null, this._pendingMediaRecovery = null, this._browserWasOffline = false, this._onlineHandler = null, this._offlineHandler = null;
          }
          static isCriticalMethod(e2) {
            return jt.CRITICAL_METHODS.has(e2);
          }
          start() {
            this._intervalId || (Le.debug("Signaling health: monitor started"), this._lastInboundAt = Date.now(), this._lastOutboundConfirmedAt = Date.now(), this._probeInFlight = false, this._lastProbeSentAt = 0, this._setupBrowserListeners(), this._intervalId = setInterval((() => this._check()), 3e3));
          }
          stop() {
            this._intervalId && (clearInterval(this._intervalId), this._intervalId = null, this._probeInFlight = false, this._lastProbeSentAt = 0), this._pendingMediaRecovery = null, this._cleanupBrowserListeners(), Le.debug("Signaling health: monitor stopped");
          }
          get isRunning() {
            return null !== this._intervalId;
          }
          get isProbeInFlight() {
            return this._probeInFlight;
          }
          onSocketActivity() {
            this._lastInboundAt = Date.now();
          }
          onOutboundConfirmed() {
            this._lastOutboundConfirmedAt = Date.now();
          }
          _resolveProbe() {
            if (!this._probeInFlight) return;
            if (this._probeInFlight = false, this._lastProbeSentAt = 0, this._lastInboundAt = Date.now(), this._lastOutboundConfirmedAt = Date.now(), Le.debug("Signaling health: probe resolved by matching Ping response"), !this._pendingMediaRecovery) return void Le.debug("Signaling health: probe resolved but no pending media recovery");
            const e2 = this._pendingMediaRecovery;
            this._pendingMediaRecovery = null, "healthy" === this._getSignalingHealthState() && (Le.info(`Signaling health: signaling probe resolved, triggering pending ICE restart for call ${e2.callId}`), this._triggerIceRestart(e2.callId, e2.reason));
          }
          _probeIfNeeded(e2) {
            var t2;
            (null === (t2 = this._session.connection) || void 0 === t2 ? void 0 : t2.connected) && (this._probeInFlight ? Le.debug(`Signaling health: probe already in flight, skipping duplicate probe (${e2})`) : (Le.info(`Signaling health: ${e2}, sending signaling probe`), this._sendProbe()));
          }
          onRequestTimeout(e2, t2, i2 = "") {
            var n2;
            if (!(null === (n2 = this._session.connection) || void 0 === n2 ? void 0 : n2.connected)) return;
            jt.isCriticalMethod(i2) ? (Le.warn(`Critical signaling request timed out (id=${e2}, method=${i2}, timeout=${t2}ms) \u2014 declaring signaling unhealthy`), this._triggerSignalingRecovery(`Critical signaling request timed out (method=${i2}, id=${e2}, timeout=${t2}ms)`, "request")) : Le.warn(`Non-critical signaling request timed out (id=${e2}, method=${i2 || "unknown"}, timeout=${t2}ms) \u2014 logging but not triggering signaling recovery`);
          }
          onPeerFailure(e2, t2) {
            Le.warn(`Signaling health: peer failure reported (callId=${e2}, evidence=${t2})`), this._recoverMediaOrSignaling(e2, `Peer connection failure (${t2})`, `Peer failure detected (${t2}) while signaling is unhealthy`, "peer_failure");
          }
          onNoRtp(e2, t2) {
            Le.warn(`Signaling health: no RTP detected (callId=${e2}, direction=${t2})`), this._recoverMediaOrSignaling(e2, `No RTP ${t2} while media should be active`, `No RTP ${t2} while signaling is unhealthy`, "no_rtp");
          }
          _setupBrowserListeners() {
            "undefined" == typeof window || this._onlineHandler || (this._onlineHandler = () => {
              this._browserWasOffline && (Le.debug(`Signaling health: browser online \u2014 clearing browser offline state for session ${this._session.sessionid}`), this._browserWasOffline = false);
            }, this._offlineHandler = () => {
              this._browserWasOffline = true, Le.debug(`Signaling health: browser offline signal received for session ${this._session.sessionid}`);
              const t2 = Ce(N);
              kt(e.SwEvent.Error, { error: t2, sessionId: this._session.sessionid }, this._session.uuid), this.isRunning && this._probeIfNeeded("browser offline signal while monitor is active");
            }, window.addEventListener("online", this._onlineHandler), window.addEventListener("offline", this._offlineHandler));
          }
          _cleanupBrowserListeners() {
            "undefined" != typeof window && this._onlineHandler && this._offlineHandler && (window.removeEventListener("online", this._onlineHandler), window.removeEventListener("offline", this._offlineHandler), this._onlineHandler = null, this._offlineHandler = null, this._browserWasOffline = false);
          }
          onIceRestartFailed(e2) {
            Le.warn(`Signaling health: ICE restart failed (callId=${e2}) \u2014 triggering socket reconnect`), this._triggerSignalingRecovery(`ICE restart failed for call ${e2}`, "peer_failure");
          }
          _check() {
            var e2;
            if (!(null === (e2 = this._session.connection) || void 0 === e2 ? void 0 : e2.connected)) return;
            const t2 = Date.now(), i2 = t2 - this._lastInboundAt, n2 = t2 - this._lastOutboundConfirmedAt, s2 = i2 >= 2e4;
            if (!s2 && !(n2 >= 45e3)) return;
            if (!this._probeInFlight) return Le.info(s2 ? `Signaling health: no inbound WS activity for ${Math.round(i2 / 1e3)}s during active call, sending health probe` : `Signaling health: inbound WS activity is flowing but no outbound request has been answered for ${Math.round(n2 / 1e3)}s, sending health probe`), void this._sendProbe();
            const o2 = t2 - this._lastProbeSentAt;
            o2 >= 5e3 && (Le.warn(`Signaling health: probe timed out after ${o2}ms (inbound silent for ${Math.round(i2 / 1e3)}s, no outbound request answered for ${Math.round(n2 / 1e3)}s) \u2014 declaring signaling unhealthy`), this._triggerSignalingRecovery(s2 ? "Signaling health probe timed out: no inbound WS activity after probe" : "Signaling health probe timed out: inbound WS activity is flowing but no outbound request is being answered", "probe"));
          }
          _sendProbe() {
            var e2;
            this._probeInFlight = true, this._lastProbeSentAt = Date.now();
            const t2 = new Bt(tt());
            null === (e2 = this._session.connection) || void 0 === e2 || e2.send(t2).then((() => this._resolveProbe())).catch(((e3) => {
              Le.warn("Signaling health: probe Ping failed to send", e3);
            }));
          }
          _recoverMediaOrSignaling(e2, t2, i2, n2) {
            if (!this._session.hasActiveCall()) return void Le.debug(`Signaling health: ignoring ${n2} recovery without an active call`);
            const s2 = this._getSignalingHealthState();
            return "healthy" === s2 ? (Le.info(`Signaling health: signaling is healthy, triggering ICE restart for call ${e2}`), void this._triggerIceRestart(e2, t2)) : "unknown" === s2 ? (Le.info(`Signaling health: signaling health is unknown, deferring ICE restart decision for call ${e2}`), this._pendingMediaRecovery = { callId: e2, reason: t2, source: n2 }, void this._probeIfNeeded(`${n2} detected with stale/unknown signaling`)) : (Le.info("Signaling health: signaling is unhealthy, triggering socket reconnect instead of ICE restart"), void this._triggerSignalingRecovery(i2, n2));
          }
          _getSignalingHealthState() {
            var e2;
            if (!(null === (e2 = this._session.connection) || void 0 === e2 ? void 0 : e2.connected)) return "unhealthy";
            if (this._probeInFlight) return "unknown";
            const t2 = Date.now(), i2 = this._lastInboundAt || this._session.connection.lastInboundAt || 0;
            return 0 === i2 ? "unknown" : t2 - i2 < 3e3 ? "healthy" : "unknown";
          }
          _triggerSignalingRecovery(t2, i2 = "probe") {
            var n2, s2, o2;
            this._pendingMediaRecovery = null, this._probeInFlight = false, this._lastProbeSentAt = 0, Le.debug(`Signaling recovery triggered (source=${i2}, reason=${t2})`);
            const r2 = be(se);
            kt(e.SwEvent.Warning, { warning: r2, reason: t2, source: i2, sessionId: this._session.sessionid }, this._session.uuid), (null === (n2 = this._session.connection) || void 0 === n2 ? void 0 : n2.connected) ? (Le.info("Signaling health: force-closing WebSocket to trigger reconnect"), this._session.socketDisconnect()) : Le.debug("Signaling health: recovery triggered but connection not connected", { connected: null === (s2 = this._session.connection) || void 0 === s2 ? void 0 : s2.connected, hasConnection: !!this._session.connection, socketGeneration: null === (o2 = this._session.connection) || void 0 === o2 ? void 0 : o2.socketGeneration });
          }
          _triggerIceRestart(t2, i2) {
            Le.info(`Signaling health: triggering ICE restart for call ${t2}`);
            const n2 = this._session.triggerIceRestart(t2);
            if (!n2.started) return void Le.info(`Signaling health: ICE restart not started for call ${t2}: ${n2.reason}`);
            const s2 = be(oe);
            kt(e.SwEvent.Warning, { warning: s2, reason: i2, callId: t2, sessionId: this._session.sessionid }, this._session.uuid);
          }
        }
        jt.CRITICAL_METHODS = /* @__PURE__ */ new Set([lt.Modify, lt.Bye, lt.Ping]);
        var Ht = "2.27.9", Wt = Ht;
        class Gt extends Ut {
          constructor(e2, t2, i2, n2, s2 = {}, o2) {
            super(), this.method = "login";
            const r2 = { login: e2, passwd: t2, login_token: i2, userVariables: s2, reconnection: o2, loginParams: {}, "User-Agent": { sdkVersion: Wt, data: navigator.userAgent } };
            n2 && (r2.sessid = n2), this.buildRequest({ method: this.method, params: r2 });
          }
        }
        class Vt extends Ut {
          constructor(e2, t2) {
            super(), this.buildRequest({ id: e2, result: { method: t2 } });
          }
        }
        class qt extends Ut {
          toString() {
            return lt.Invite;
          }
        }
        class Yt extends Ut {
          toString() {
            return lt.Answer;
          }
        }
        class Kt extends Ut {
          toString() {
            return lt.Attach;
          }
        }
        class Jt extends Ut {
          toString() {
            return lt.Bye;
          }
        }
        class zt extends Ut {
          toString() {
            return lt.Candidate;
          }
        }
        class Xt extends Ut {
          toString() {
            return lt.EndOfCandidates;
          }
        }
        class Qt extends Ut {
          toString() {
            return lt.Modify;
          }
        }
        class Zt extends Ut {
          toString() {
            return lt.Info;
          }
        }
        class ei extends Ut {
          toString() {
            return lt.Broadcast;
          }
        }
        class ti extends Ut {
          toString() {
            return lt.Subscribe;
          }
        }
        class ii extends Ut {
          toString() {
            return lt.Unsubscribe;
          }
        }
        class ni extends Ut {
          constructor(e2) {
            super(), this.method = "anonymous_login";
            const { target_type: t2, target_id: i2, target_version_id: n2, target_params: s2, userVariables: o2, sessionId: r2, reconnection: a2 } = e2, c2 = { target_type: t2, target_id: i2, userVariables: o2, reconnection: a2, "User-Agent": { sdkVersion: Wt, data: navigator.userAgent } };
            r2 && (c2.sessid = r2), n2 && (c2.target_version_id = n2), s2 && (c2.target_params = s2), this.buildRequest({ method: this.method, params: c2 });
          }
        }
        class si {
          constructor(e2) {
            if (this.options = e2, this.uuid = c(), this.sessionid = "", this.subscriptions = {}, this.signature = null, this.relayProtocol = null, this.contexts = [], this.timeoutErrorCode = -32e3, this.invalidMethodErrorCode = -32601, this.authenticationRequiredErrorCode = -32e3, this.callReportId = null, this.callReportVoiceSdkId = null, this.dc = null, this.region = null, this.connection = null, this._jwtAuth = false, this._autoReconnect = true, this._idle = false, this._reconnectAttempts = 0, this._reconnectCountedGeneration = -1, this._intentionalClose = false, this._tokenExpiryTimeout = null, this._pendingCallReportUploads = /* @__PURE__ */ new Set(), this._signalingHealthMonitor = new jt(this), this._executeQueue = [], !this.validateOptions()) throw new Error("Invalid init options");
            var t2, i2;
            t2 = e2.debug ? "debug" : "info", Pe = null !== (i2 = Me[t2]) && void 0 !== i2 ? i2 : Me.info, this._onSocketOpen = this._onSocketOpen.bind(this), this.onNetworkClose = this.onNetworkClose.bind(this), this._onSocketMessage = this._onSocketMessage.bind(this), this._handleLoginError = this._handleLoginError.bind(this), this._onSocketActivity = this._onSocketActivity.bind(this), this._attachListeners(), this.connection = new Mt(this), this.registerAgent = new $t(this);
          }
          get __logger() {
            return Le;
          }
          get connected() {
            return this.connection && this.connection.connected;
          }
          getIsRegistered() {
            return i(this, void 0, void 0, (function* () {
              return this.registerAgent.getIsRegistered();
            }));
          }
          get reconnectDelay() {
            const e2 = this._reconnectAttempts, t2 = Math.min(1e3 * Math.pow(2, e2 - 1), 3e4), i2 = Math.floor(0.25 * t2 * (2 * Math.random() - 1)), n2 = t2 + i2;
            return Le.debug("Reconnect delay computed", { attempt: e2, baseDelayMs: 1e3, maxDelayMs: 3e4, delayMs: t2, jitterMs: i2, totalDelay: n2, sessionId: this.sessionid }), n2;
          }
          execute(t2) {
            var n2;
            if (this._idle) return new Promise(((e2) => this._executeQueue.push({ resolve: e2, msg: t2 })));
            if (!this.connected) return new Promise(((e2) => {
              this._executeQueue.push({ resolve: e2, msg: t2 }), Le.debug("Calling connect from execute since not currently connected."), this.connect();
            }));
            const s2 = jt.isCriticalMethod((null === (n2 = t2.request) || void 0 === n2 ? void 0 : n2.method) || "") ? Mt.DEFAULT_REQUEST_TIMEOUT_MS : void 0;
            return (null != s2 ? this.connection.send(t2, s2) : this.connection.send(t2)).catch(((t3) => i(this, void 0, void 0, (function* () {
              if (t3 instanceof Te) throw Le.debug(`Stale request settled (id=${t3.requestId}, gen=${t3.staleGeneration}) \u2014 not triggering recovery`), t3;
              if ("RequestTimeoutError" === (null == t3 ? void 0 : t3.name)) throw this.onSignalingRequestTimeout(t3.requestId, t3.timeoutMs, t3.method), t3;
              if ((null == t3 ? void 0 : t3.code) === this.authenticationRequiredErrorCode) {
                if (!this._autoReconnect) {
                  const i2 = Ce(O, t3, void 0, true);
                  kt(e.SwEvent.Error, { error: i2, sessionId: this.sessionid }, this.uuid);
                }
                yield this.login();
              }
              throw t3;
            }))));
          }
          executeRaw(e2) {
            this._idle ? this._executeQueue.push({ msg: e2 }) : this.connection.sendRawText(e2);
          }
          trackCallReportUpload(e2) {
            this._pendingCallReportUploads.add(e2), e2.then((() => this._pendingCallReportUploads.delete(e2)), (() => this._pendingCallReportUploads.delete(e2)));
          }
          _drainCallReportUploads() {
            return i(this, void 0, void 0, (function* () {
              if (0 === this._pendingCallReportUploads.size) return;
              const e2 = Array.from(this._pendingCallReportUploads);
              let t2 = false;
              yield Promise.race([Promise.all(e2.map(((e3) => e3.catch((() => {
              }))))), new Promise(((e3) => setTimeout((() => {
                t2 = true, e3();
              }), si.CALL_REPORT_UPLOAD_DRAIN_TIMEOUT_MS)))]), t2 && Le.warn("Timed out waiting for pending call report uploads", { pendingCount: this._pendingCallReportUploads.size });
            }));
          }
          validateOptions() {
            return Ge(this.options) || Ve(this.options);
          }
          broadcast(e2) {
          }
          disconnect() {
            return i(this, void 0, void 0, (function* () {
              clearTimeout(this._reconnectTimeout), this._clearTokenExpiryTimeout(), this.subscriptions = {}, this._autoReconnect = false, this._intentionalClose = true, this._reconnectAttempts = 0, this.relayProtocol = null, yield this._drainCallReportUploads(), this._closeConnection(), yield sessionStorage.removeItem(this.signature), this._executeQueue = [], this._detachListeners(), Le.debug("Session disconnected. Cleaned up all listeners and subscriptions, closed connection, disabled auto-reconnect.");
            }));
          }
          on(e2, t2) {
            return Ct(e2, t2, this.uuid), this;
          }
          off(e2, t2) {
            return Tt(e2, t2, this.uuid), this;
          }
          connect() {
            return i(this, void 0, void 0, (function* () {
              this.connection || (Le.debug("No existing connection found, creating a new one."), this.connection = new Mt(this)), this._attachListeners(), this._autoReconnect || (Le.debug("autoReconnect was disabled, resetting reconnect attempts"), this._reconnectAttempts = 0), this._autoReconnect = true, this.connection.isAlive || (Le.debug("Connection wasn't alive, initiating connection to the server..."), this.connection.connect()), Le.debug("Connect method called. Connection initiated.");
            }));
          }
          resetReconnectAttempts() {
            this._reconnectAttempts = 0, this._reconnectCountedGeneration = -1;
          }
          _handleLoginError(t2) {
            const i2 = Ce(R, t2);
            kt(e.SwEvent.Error, { error: i2, sessionId: this.sessionid }, this.uuid);
          }
          clearReconnectToken() {
            nt();
          }
          _checkTokenExpiry() {
            this._clearTokenExpiryTimeout();
            const e2 = this.options.login_token;
            if (e2 && "string" == typeof e2) try {
              const t2 = e2.split(".");
              if (3 !== t2.length) return;
              const i2 = JSON.parse(atob(t2[1])).exp;
              if ("number" != typeof i2) return;
              const n2 = i2 - Math.floor(Date.now() / 1e3);
              if (n2 <= 0) return;
              if (n2 <= si.TOKEN_EXPIRY_WARNING_SECONDS) this._emitTokenExpiryWarning();
              else {
                const e3 = 1e3 * (n2 - si.TOKEN_EXPIRY_WARNING_SECONDS);
                this._tokenExpiryTimeout = setTimeout((() => {
                  this._emitTokenExpiryWarning();
                }), e3);
              }
            } catch (e3) {
              Le.debug("login_token is not a decodable JWT, skipping expiry check");
            }
          }
          _emitTokenExpiryWarning() {
            const t2 = be(ie);
            kt(e.SwEvent.Warning, { warning: t2, sessionId: this.sessionid }, this.uuid);
          }
          _clearTokenExpiryTimeout() {
            null !== this._tokenExpiryTimeout && (clearTimeout(this._tokenExpiryTimeout), this._tokenExpiryTimeout = null);
          }
          login({ creds: t2, onSuccess: n2, onError: s2 } = {}) {
            return i(this, void 0, void 0, (function* () {
              if (this.connection && this.connection.isAlive) {
                if (t2 && (void 0 !== t2.login && (this.options.login = t2.login), void 0 !== t2.password && (this.options.password = t2.password), void 0 !== t2.passwd && (this.options.passwd = t2.passwd), void 0 !== t2.login_token && (this.options.login_token = t2.login_token), void 0 !== t2.userVariables && (this.options.userVariables = t2.userVariables), void 0 !== t2.anonymous_login && (this.options.anonymous_login = t2.anonymous_login)), Ge(this.options)) return this._login({ type: "login", onSuccess: n2, onError: s2 });
                if (Ve(this.options)) return this._login({ type: "anonymous_login", onSuccess: n2, onError: s2 });
                {
                  const t3 = "Invalid login options provided for authentication.";
                  Le.error(t3);
                  const i2 = Ce(A, void 0, t3);
                  return void kt(e.SwEvent.Error, { error: i2, type: ut.invalidCredentialsOptions, sessionId: this.sessionid }, this.uuid);
                }
              }
            }));
          }
          _login({ type: e2, onSuccess: t2, onError: n2 }) {
            var s2, o2, r2;
            return i(this, void 0, void 0, (function* () {
              let i2;
              const a2 = !!tt(), c2 = a2 && (this.sessionid && et() ? this.sessionid : (function(e3 = Date.now()) {
                const t3 = sessionStorage.getItem(ze);
                return t3 ? et(e3) ? t3 : (sessionStorage.removeItem(ze), sessionStorage.removeItem(Xe), null) : null;
              })()) || "";
              if ("login" === e2) {
                const e3 = null !== (r2 = null !== (s2 = this.options.pushWhenActive) && void 0 !== s2 ? s2 : null === (o2 = this.options.userVariables) || void 0 === o2 ? void 0 : o2.push_when_active) && void 0 !== r2 && r2, t3 = Object.assign(Object.assign({}, this.options.userVariables), { push_when_active: e3, pn_late_fanout: e3 });
                i2 = new Gt(this.options.login, this.options.password || this.options.passwd, this.options.login_token, c2, t3, a2);
              } else i2 = new ni({ target_id: this.options.anonymous_login.target_id, target_type: this.options.anonymous_login.target_type, target_version_id: this.options.anonymous_login.target_version_id, target_params: this.options.anonymous_login.target_params, sessionId: c2, userVariables: this.options.userVariables, reconnection: a2 });
              const l2 = yield this.execute(i2).catch(((e3) => {
                this._handleLoginError(e3), n2 && n2(e3);
              }));
              l2 && (this.sessionid = l2.sessid, this.sessionid && it(this.sessionid), this._checkTokenExpiry(), t2 && t2());
            }));
          }
          _onSocketOpen() {
            return i(this, void 0, void 0, (function* () {
              this.startSignalingHealthMonitor();
            }));
          }
          _flushIntermediateCallReports(e2) {
            const t2 = this.calls;
            t2 && Object.values(t2).forEach(((t3) => {
              if (null == t3 ? void 0 : t3.flushIntermediateCallReport) try {
                t3.flushIntermediateCallReport(e2);
              } catch (i2) {
                Le.error("Failed to flush intermediate call report", { callId: t3.id, flushReason: e2, error: i2 });
              }
            }));
          }
          _getSocketCloseCodeName(e2) {
            if (void 0 === e2) return;
            const t2 = Object.entries(ce).find((([, t3]) => t3 === e2));
            return null == t2 ? void 0 : t2[0];
          }
          _getSocketCloseError(e2) {
            if (e2) return e2 instanceof Error ? e2.message : String(e2);
          }
          _createSocketCloseFlushReason(e2) {
            return { type: (null == e2 ? void 0 : e2.error) ? "socket-error" : "socket-close", socketClose: { code: null == e2 ? void 0 : e2.code, codeName: this._getSocketCloseCodeName(null == e2 ? void 0 : e2.code), reason: null == e2 ? void 0 : e2.reason, wasClean: null == e2 ? void 0 : e2.wasClean, error: this._getSocketCloseError(null == e2 ? void 0 : e2.error) } };
          }
          onNetworkClose(t2) {
            var i2, n2, s2, o2;
            const r2 = null !== (n2 = null === (i2 = this.connection) || void 0 === i2 ? void 0 : i2.socketGeneration) && void 0 !== n2 ? n2 : 0, a2 = null !== (s2 = null == t2 ? void 0 : t2.socketGeneration) && void 0 !== s2 ? s2 : r2;
            if (a2 < r2) Le.debug(`Skipping stale onNetworkClose for socket generation ${a2} (current generation is ${r2})`);
            else if (a2 !== this._reconnectCountedGeneration) {
              this._flushIntermediateCallReports(this._createSocketCloseFlushReason(t2)), Le.debug("onNetworkClose called", { closeCode: null == t2 ? void 0 : t2.code, closeReason: null == t2 ? void 0 : t2.reason, wasClean: null == t2 ? void 0 : t2.wasClean, voiceSdkId: this.callReportVoiceSdkId, sessid: this.sessionid || void 0, autoReconnect: this._autoReconnect, reconnectAttempts: this._reconnectAttempts }), this.relayProtocol && Rt(this.relayProtocol);
              for (const e2 in this.subscriptions) Rt(e2);
              if (this.subscriptions = {}, this.contexts = [], clearTimeout(this._keepAliveTimeout), clearTimeout(this._reconnectTimeout), this.stopSignalingHealthMonitor(), this.sessionid && this._autoReconnect && it(this.sessionid), this.connection && (this.connection.previousGatewayState = ""), this._autoReconnect) {
                const t3 = null !== (o2 = this.options.maxReconnectAttempts) && void 0 !== o2 ? o2 : 10;
                if (this._reconnectCountedGeneration = a2, this._reconnectAttempts += 1, t3 > 0 && this._reconnectAttempts > t3) {
                  Le.info(`Reconnection exhausted after ${t3} attempts. Stopping automatic reconnect.`), this._reconnectAttempts = 0, this._autoReconnect = false, this._terminateActiveCallsLocally();
                  const i4 = Ce(T);
                  return void kt(e.SwEvent.Error, { error: i4, sessionId: this.sessionid }, this.uuid);
                }
                const i3 = this.reconnectDelay;
                Le.debug(`Reconnect attempt ${this._reconnectAttempts}${t3 > 0 ? ` of ${t3}` : ""} (delay=${i3}ms)`), this._reconnectTimeout = setTimeout((() => {
                  Le.debug("Calling connect due to network close and auto-reconnect enabled."), this.connect();
                }), i3);
              } else if (Le.debug("auto_reconnect disabled, not reconnecting", { voiceSdkId: this.callReportVoiceSdkId, sessid: this.sessionid || void 0, intentionalClose: this._intentionalClose }), !this._intentionalClose) {
                const t3 = be(re);
                kt(e.SwEvent.Warning, { warning: t3, reason: "auto_reconnect_disabled", sessionId: this.sessionid }, this.uuid);
              }
              this._intentionalClose = false;
            } else Le.debug(`Skipping duplicate onNetworkClose for socket generation ${a2} (already handled, reconnect attempt ${this._reconnectAttempts})`);
          }
          _onSocketMessage(e2) {
          }
          _removeSubscription(e2, t2) {
            this._existsSubscription(e2, t2) && (t2 ? (delete this.subscriptions[e2][t2], Tt(e2, null, t2)) : (delete this.subscriptions[e2], Rt(e2)));
          }
          _addSubscription(e2, t2 = null, i2) {
            this._existsSubscription(e2, i2) || (this._existsSubscription(e2) || (this.subscriptions[e2] = {}), this.subscriptions[e2][i2] = {}, Be(t2) && Ct(e2, t2, i2));
          }
          _existsSubscription(e2, t2) {
            return !(!this.subscriptions.hasOwnProperty(e2) || !(!t2 || t2 && this.subscriptions[e2].hasOwnProperty(t2)));
          }
          _attachListeners() {
            this._detachListeners(), Le.debug("Attaching socket event listeners"), this.on(e.SwEvent.SocketOpen, this._onSocketOpen), this.on(e.SwEvent.SocketClose, this.onNetworkClose), this.on(e.SwEvent.SocketError, this.onNetworkClose), this.on(e.SwEvent.SocketMessage, this._onSocketMessage), this.on(e.SwEvent.SocketActivity, this._onSocketActivity);
          }
          _detachListeners() {
            Le.debug("Detaching socket event listeners"), this.off(e.SwEvent.SocketOpen, this._onSocketOpen), this.off(e.SwEvent.SocketClose, this.onNetworkClose), this.off(e.SwEvent.SocketError, this.onNetworkClose), this.off(e.SwEvent.SocketMessage, this._onSocketMessage), this.off(e.SwEvent.SocketActivity, this._onSocketActivity);
          }
          _emptyExecuteQueues() {
            this._executeQueue.forEach((({ resolve: e2, msg: t2 }) => {
              "string" == typeof t2 ? this.executeRaw(t2) : e2(this.execute(t2));
            }));
          }
          _closeConnection() {
            var e2, t2;
            this._idle = true, clearTimeout(this._keepAliveTimeout), this.stopSignalingHealthMonitor(), Le.debug("_closeConnection called", { connected: null === (e2 = this.connection) || void 0 === e2 ? void 0 : e2.connected, socketGeneration: null === (t2 = this.connection) || void 0 === t2 ? void 0 : t2.socketGeneration, voiceSdkId: this.callReportVoiceSdkId, sessid: this.sessionid || void 0 }), this.connection && this.connection.close();
          }
          _resetKeepAlive() {
            false === this._pong && (Le.warn("No ping/pong received, forcing PING ACK to keep alive"), this.execute(new Bt(tt()))), clearTimeout(this._keepAliveTimeout), this._triggerKeepAliveTimeoutCheck();
          }
          _triggerKeepAliveTimeoutCheck() {
            this._pong = false, this._keepAliveTimeout = setTimeout((() => this._resetKeepAlive()), 35e3);
          }
          setPingReceived() {
            Le.debug("Ping received"), this._pong = true;
          }
          _onSocketActivity() {
            this._signalingHealthMonitor.onSocketActivity();
          }
          onOutboundConfirmed() {
            this._signalingHealthMonitor.onOutboundConfirmed();
          }
          hasActiveCall() {
            const e2 = this.calls;
            if (!e2) return false;
            const t2 = /* @__PURE__ */ new Set([gt.Early, gt.Active, gt.Held]);
            return Object.values(e2).some(((e3) => e3 && null != e3._state && t2.has(e3._state)));
          }
          _terminateActiveCallsLocally() {
            const e2 = this.calls;
            if (!e2) return;
            const t2 = Object.keys(e2);
            if (0 !== t2.length) {
              Le.debug(`Reconnection exhausted \u2014 locally terminating ${t2.length} active call(s) (no BYE): ${t2.join(", ")}`);
              for (const i2 of t2) {
                const t3 = e2[i2];
                null == t3 || t3.hangup({}, false);
              }
            }
          }
          startSignalingHealthMonitor() {
            this._signalingHealthMonitor.start();
          }
          stopSignalingHealthMonitor() {
            this._signalingHealthMonitor.stop();
          }
          triggerIceRestart(e2) {
            const t2 = this.calls, i2 = null == t2 ? void 0 : t2[e2];
            if (!i2) return Le.warn(`Signaling health: cannot trigger ICE restart \u2014 call ${e2} not found`), { started: false, reason: "call not found" };
            const n2 = i2.peer;
            if (!(null == n2 ? void 0 : n2.restartIce)) return Le.warn(`Signaling health: cannot trigger ICE restart \u2014 no peer for call ${e2}`), { started: false, reason: "no peer" };
            const s2 = n2.restartIce();
            return s2.started || Le.debug(`Signaling health: ICE restart skipped for call ${e2}: ${s2.reason}`), s2;
          }
          onSignalingRequestTimeout(e2, t2, i2 = "") {
            this._signalingHealthMonitor.onRequestTimeout(e2, t2, i2);
          }
          reportPeerFailure(e2, t2) {
            this._signalingHealthMonitor.onPeerFailure(e2, t2);
          }
          reportNoRtp(e2, t2) {
            this._signalingHealthMonitor.onNoRtp(e2, t2);
          }
          reportIceRestartFailed(e2) {
            this._signalingHealthMonitor.onIceRestartFailed(e2);
          }
          static on(e2, t2) {
            Ct(e2, t2);
          }
          static off(e2) {
            Tt(e2);
          }
          static uuid() {
            return c();
          }
          clearConnection() {
            this.connection = null;
          }
          hasAutoReconnect() {
            return this._autoReconnect;
          }
        }
        si.TOKEN_EXPIRY_WARNING_SECONDS = 120, si.CALL_REPORT_UPLOAD_DRAIN_TIMEOUT_MS = 1e4;
        const oi = (e2) => navigator.mediaDevices.getUserMedia(e2), ri = (e2) => e2 && e2 instanceof MediaStream, ai = (t2, i2, n2) => {
          const s2 = je(t2);
          if (null !== s2) {
            if (s2.getAttribute("autoplay") || s2.setAttribute("autoplay", "autoplay"), s2.getAttribute("playsinline") || s2.setAttribute("playsinline", "playsinline"), s2.srcObject && s2.srcObject !== i2 && (Le.warn("attachMediaStream: element already has a different MediaStream attached; overwriting will disrupt the existing call. Use a per-call remoteElement (client.newCall({ remoteElement }) or call.answer({ remoteElement })) for concurrent calls."), n2)) {
              const t3 = be(te);
              kt(e.SwEvent.Warning, { warning: t3, callId: n2.callId, sessionId: n2.sessionId }, n2.eventTarget);
            }
            s2.srcObject = i2;
          }
        }, ci = (e2, t2) => {
          const i2 = je(e2);
          if (i2) {
            if (t2 && i2.srcObject !== t2) return;
            i2.srcObject = null;
          }
        }, li = (e2, t2) => i(void 0, void 0, void 0, (function* () {
          const i2 = je(e2);
          if (null === i2) return Le.info("No HTMLMediaElement to attach the speakerId"), false;
          if ("string" != typeof t2) return Le.info(`Invalid speaker deviceId: '${t2}'`), false;
          try {
            return yield i2.setSinkId(t2), true;
          } catch (e3) {
            return false;
          }
        })), di = (e2) => {
          e2 && "live" === e2.readyState && e2.stop();
        }, ui = (e2) => {
          ri(e2) && e2.getTracks().forEach(di), e2 = null;
        }, hi = (e2) => i(void 0, void 0, void 0, (function* () {
          Le.info("RTCService.getUserMedia", e2);
          const { audio: t2, video: i2 } = e2;
          if (!t2 && !i2) return null;
          try {
            return yield oi(e2);
          } catch (t3) {
            if (Le.error("getUserMedia error: ", t3), ((e3) => "NotReadableError" === e3.name || "NotFoundError" === e3.name || "OverconstrainedError" === e3.name)(t3)) {
              const i3 = ((e3) => {
                const { audio: t4, video: i4 } = e3;
                let n2 = false, s2 = t4, o2 = i4;
                if ("object" == typeof t4 && null !== t4 && "deviceId" in t4) {
                  n2 = true;
                  const e4 = Object.assign({}, t4);
                  delete e4.deviceId, s2 = 0 === Object.keys(e4).length || e4;
                }
                if ("object" == typeof i4 && null !== i4 && "deviceId" in i4) {
                  n2 = true;
                  const e4 = Object.assign({}, i4);
                  delete e4.deviceId, o2 = 0 === Object.keys(e4).length || e4;
                }
                return n2 ? { audio: s2, video: o2 } : null;
              })(e2);
              if (i3) {
                Le.warn("Device not found or not readable, falling back to default device");
                try {
                  return yield oi(i3);
                } catch (e3) {
                  throw Le.error("Fallback getUserMedia also failed: ", e3), t3;
                }
              }
            }
            throw t3;
          }
        })), pi = (e2 = null, t2 = false) => i(void 0, void 0, void 0, (function* () {
          let i2 = [];
          const n2 = yield navigator.mediaDevices.getUserMedia(((e3 = null) => ({ audio: !e3 || e3 === ft.AudioIn || e3 === ft.AudioOut, video: !e3 || e3 === ft.Video }))(e2)).catch(((e3) => (Le.error(e3), null)));
          if (n2) {
            if (ui(n2), i2 = yield navigator.mediaDevices.enumerateDevices(), e2 && (i2 = i2.filter(((t3) => t3.kind === e2))), true === t2) return i2;
            const s2 = [];
            i2 = i2.filter(((e3) => {
              if (!e3.groupId) return true;
              const t3 = `${e3.kind}-${e3.groupId}`;
              return !s2.includes(t3) && (s2.push(t3), true);
            }));
          }
          return i2;
        })), gi = [[320, 240], [640, 360], [640, 480], [1280, 720], [1920, 1080]], vi = (e2, t2, n2) => i(void 0, void 0, void 0, (function* () {
          const i2 = yield pi(n2, true);
          for (let n3 = 0; n3 < i2.length; n3++) {
            const { deviceId: s2, label: o2 } = i2[n3];
            if (e2 === s2 || t2 === o2) return s2;
          }
          return null;
        })), mi = (e2) => {
          const t2 = navigator.mediaDevices.getSupportedConstraints();
          Object.keys(e2).map(((i2) => {
            t2.hasOwnProperty(i2) && null !== e2[i2] && void 0 !== e2[i2] || delete e2[i2];
          }));
        }, fi = (e2) => e2 ? { id: e2.id, kind: e2.kind, enabled: e2.enabled, muted: e2.muted, readyState: e2.readyState } : null, _i = (e2) => {
          var t2, i2, n2, s2, o2, r2;
          if (!e2) return [];
          return (null !== (i2 = null === (t2 = e2.getTracks) || void 0 === t2 ? void 0 : t2.call(e2)) && void 0 !== i2 ? i2 : [...null !== (s2 = null === (n2 = e2.getAudioTracks) || void 0 === n2 ? void 0 : n2.call(e2)) && void 0 !== s2 ? s2 : [], ...null !== (r2 = null === (o2 = e2.getVideoTracks) || void 0 === o2 ? void 0 : o2.call(e2)) && void 0 !== r2 ? r2 : []]).map(((e3) => fi(e3)));
        }, Si = (e2, t2) => {
          if (!e2) return false;
          const { subscribed: i2, alreadySubscribed: n2 } = yi(e2);
          return i2.includes(t2) || n2.includes(t2);
        }, yi = (e2) => {
          const t2 = { subscribed: [], alreadySubscribed: [], unauthorized: [], unsubscribed: [], notSubscribed: [] };
          return Object.keys(t2).forEach(((i2) => {
            t2[i2] = e2[`${i2}Channels`] || [];
          })), t2;
        }, bi = (e2, t2 = null, i2 = null) => {
          if (!ri(e2)) return null;
          let n2 = [];
          switch (t2) {
            case "audio":
              n2 = e2.getAudioTracks();
              break;
            case "video":
              n2 = e2.getVideoTracks();
              break;
            default:
              n2 = e2.getTracks();
          }
          n2.forEach(((e3) => {
            switch (i2) {
              case "on":
              case true:
                e3.enabled = true;
                break;
              case "off":
              case false:
                e3.enabled = false;
                break;
              default:
                e3.enabled = !e3.enabled;
            }
          }));
        }, Ii = (e2) => {
          bi(e2, "audio", true);
        }, Ei = (e2) => {
          bi(e2, "audio", false);
        };
        function Ci() {
          try {
            const { browserInfo: e2, name: t2, version: i2, supportAudio: n2, supportVideo: s2 } = (function() {
              if (!window || !window.navigator || !window.navigator.userAgent) throw new Error("You should use @telnyx/webrtc in a web browser such as Chrome|Firefox|Safari");
              if (navigator.userAgent.match(/chrom(e|ium)/gim) && !navigator.userAgent.match(/OPR\/[0-9]{2}/gi) && !navigator.userAgent.match(/edg/gim)) {
                const e3 = navigator.userAgent.match(/chrom(e|ium)\/[0-9]+\./gim)[0].split("/"), t3 = e3[0], i3 = parseInt(e3[1], 10);
                return { browserInfo: navigator.userAgent, name: t3, version: i3, supportAudio: true, supportVideo: true };
              }
              if (navigator.userAgent.match(/firefox/gim) && !navigator.userAgent.match(/OPR\/[0-9]{2}/gi) && !navigator.userAgent.match(/edg/gim)) {
                const e3 = navigator.userAgent.match(/firefox\/[0-9]+\./gim)[0].split("/"), t3 = e3[0], i3 = parseInt(e3[1], 10);
                return { browserInfo: navigator.userAgent, name: t3, version: i3, supportAudio: true, supportVideo: false };
              }
              if (navigator.userAgent.match(/safari/gim) && !navigator.userAgent.match(/OPR\/[0-9]{2}/gi) && !navigator.userAgent.match(/edg/gim)) {
                const e3 = navigator.userAgent.match(/safari/gim)[0], t3 = navigator.userAgent.match(/version\/[0-9]+\./gim)[0].split("/"), i3 = parseInt(t3[1], 10);
                return { browserInfo: navigator.userAgent, name: e3, version: i3, supportAudio: true, supportVideo: true };
              }
              if (navigator.userAgent.match(/edg/gim) && !navigator.userAgent.match(/OPR\/[0-9]{2}/gi)) {
                const e3 = navigator.userAgent.match(/edg\/[0-9]+\./gim)[0].split("/"), t3 = e3[0], i3 = parseInt(e3[1], 10);
                return { browserInfo: navigator.userAgent, name: t3, version: i3, supportAudio: true, supportVideo: true };
              }
              throw new Error("This browser does not support @telnyx/webrtc. To see browser support list: `TelnyxRTC.webRTCSupportedBrowserList()`");
            })(), o2 = window.RTCPeerConnection, r2 = window.RTCSessionDescription, a2 = window.RTCIceCandidate, c2 = window.navigator && window.navigator.mediaDevices, l2 = navigator.getUserMedia || navigator.webkitGetUserMedia || navigator.msGetUserMedia || navigator.mozGetUserMedia;
            return { browserInfo: e2, browserName: t2, browserVersion: i2, supportWebRTC: !!(o2 && r2 && a2 && c2 && l2), supportWebRTCAudio: n2, supportWebRTCVideo: s2, supportRTCPeerConnection: !!o2, supportSessionDescription: !!r2, supportIceCandidate: !!a2, supportMediaDevices: !!c2, supportGetUserMedia: !!hi };
          } catch (e2) {
            return e2.message;
          }
        }
        var wi;
        function Ti(e2, t2) {
          const i2 = document.getElementById(t2);
          if (i2) return i2;
          if (e2 && t2) {
            const i3 = document.createElement("audio");
            return i3.id = t2, i3.loop = true, i3.src = e2, i3.preload = "auto", i3.load(), document.body.appendChild(i3), i3;
          }
          return null;
        }
        function ki(e2) {
          e2 && (e2._playFulfilled = false, e2._promise = e2.play(), e2._promise.then((() => {
            e2._playFulfilled = true;
          })).catch(((t2) => {
            Le.error("playAudio", t2), e2._playFulfilled = true;
          })));
        }
        function Ri(e2) {
          e2 && (e2._playFulfilled ? (e2.pause(), e2.currentTime = 0) : e2._promise && e2._promise.then ? e2._promise.then((() => {
            e2.pause(), e2.currentTime = 0;
          })) : setTimeout((() => {
            e2.pause(), e2.currentTime = 0;
          }), 1e3));
        }
        !(function(e2) {
          e2.not_supported = "not supported", e2.full = "full", e2.partial = "partial";
        })(wi || (wi = {}));
        const Ai = (e2) => {
          const t2 = [], i2 = [];
          return e2 && 0 !== e2.length ? (e2.forEach(((e3) => {
            const n2 = e3.mimeType.toLocaleLowerCase();
            n2.startsWith("audio/") ? t2.push(e3) : n2.startsWith("video/") && i2.push(e3);
          })), { audioCodecs: t2, videoCodecs: i2 }) : { audioCodecs: t2, videoCodecs: i2 };
        };
        class Oi extends Pt {
          constructor(e2, t2) {
            super(), this.method = "ai_conversation", this.buildRequest({ method: this.method, params: { type: "conversation.item.create", previous_item_id: null, item: { id: c(), type: "message", role: "user", content: [{ type: "input_text", text: e2 }, ...null == t2 ? void 0 : t2.map(((e3) => ({ type: "image_url", image_url: { url: e3 } })))] } } });
          }
        }
        class Di extends Pt {
          constructor(e2) {
            super(), this.method = "ai_conversation", this.buildNotification({ method: this.method, params: { type: "conversation.item.create", item: e2 } });
          }
        }
        class Ni {
          constructor(e2, t2) {
            var i2;
            this.peerConnection = null, this.intervalId = null, this.statsBuffer = [], this.intervalStartTime = null, this.callEndTime = null, this.logCollector = null, this.intervalAudioLevels = { outbound: [], inbound: [] }, this.intervalJitters = [], this.intervalRTTs = [], this.intervalBitrates = { outbound: [], inbound: [] }, this.previousStats = {}, this.previousCandidatePairSnapshot = null, this.MAX_BUFFER_SIZE = 360, this.onFlushNeeded = null, this.onWarning = null, this._breachCounters = {}, this._activeWarnings = /* @__PURE__ */ new Set(), this._lastWarningEmitted = {}, this._prevPacketsReceived = null, this._prevPacketsLost = null, this._previousStatsEntryForWarnings = null, this._isHeld = false, this._lastLocalAudioTrackSnapshotJson = null, this._hasConfirmedLocalAudio = false, this._confirmedLocalAudioSilenceMs = 0, this._segmentIndex = 0, this._flushing = false, this._stopped = false, this.options = e2, this.logCollectorOptions = t2 || { enabled: false, level: "debug", maxEntries: 1e3 }, this.callStartTime = /* @__PURE__ */ new Date(), this._lastIntermediateFlushTime = this.callStartTime, this.logCollectorOptions.enabled && (this.logCollector = (function(e3) {
              return new De(e3);
            })(this.logCollectorOptions), this.logCollector.start(), i2 = this.logCollector, Ne = i2);
          }
          start(e2) {
            var t2, i2;
            this.options.enabled && (this.peerConnection = e2, this.intervalStartTime = /* @__PURE__ */ new Date(), this._lastIntermediateFlushTime = this.intervalStartTime, this._stopped = false, Le.info("CallReportCollector: Starting stats collection", { interval: this.options.interval, initialInterval: Ni.INITIAL_COLLECTION_INTERVAL_MS, logCollectorActive: null !== (i2 = null === (t2 = this.logCollector) || void 0 === t2 ? void 0 : t2.isActive()) && void 0 !== i2 && i2 }), this._scheduleNextCollection());
          }
          stop() {
            var e2, t2;
            return i(this, void 0, void 0, (function* () {
              this._stopped = true, this.intervalId && (clearTimeout(this.intervalId), this.intervalId = null), this.callEndTime = /* @__PURE__ */ new Date(), this.peerConnection && this.intervalStartTime && (yield this._collectStats(true));
              const i2 = null !== (t2 = null === (e2 = this.logCollector) || void 0 === e2 ? void 0 : e2.getLogCount()) && void 0 !== t2 ? t2 : 0;
              this.logCollector && this.logCollector.stop(), Le.info("CallReportCollector: Stopped stats collection", { totalIntervals: this.statsBuffer.length, totalLogs: i2, duration: this.callEndTime.getTime() - this.callStartTime.getTime() });
            }));
          }
          flush(e2, t2) {
            var i2, n2, s2, o2;
            const r2 = this.statsBuffer.length, a2 = null !== (n2 = null === (i2 = this.logCollector) || void 0 === i2 ? void 0 : i2.getLogCount()) && void 0 !== n2 ? n2 : 0, c2 = "socket-close" === (null == t2 ? void 0 : t2.type) || "socket-error" === (null == t2 ? void 0 : t2.type), l2 = r2 > 0 || a2 > 0 || c2;
            if (this._flushing || !l2) return Le.debug("CallReportCollector: Skipping intermediate flush", { reason: this._flushing ? "already-flushing" : "no-stats-or-logs", flushReason: t2, statsIntervals: r2, logEntries: a2 }), null;
            this._flushing = true;
            try {
              const i3 = this._segmentIndex++, n3 = this.statsBuffer;
              this.statsBuffer = [];
              const r3 = null !== (o2 = null === (s2 = this.logCollector) || void 0 === s2 ? void 0 : s2.drain()) && void 0 !== o2 ? o2 : [], a3 = /* @__PURE__ */ new Date();
              this._lastIntermediateFlushTime = a3;
              const c3 = Object.assign(Object.assign(Object.assign({ summary: Object.assign(Object.assign({}, e2), { durationSeconds: (a3.getTime() - this.callStartTime.getTime()) / 1e3, startTimestamp: this.callStartTime.toISOString(), endTimestamp: a3.toISOString() }), stats: n3 }, r3.length > 0 ? { logs: r3 } : {}), { segment: i3 }), t2 ? { flushReason: t2 } : {});
              return Le.info("CallReportCollector: Flushed intermediate segment", { segment: i3, intervals: n3.length, logEntries: r3.length, callId: e2.callId, flushReason: t2 }), c3;
            } finally {
              this._flushing = false;
            }
          }
          postReport(e2, t2, n2, s2) {
            var o2, r2;
            return i(this, void 0, void 0, (function* () {
              const i2 = null === (o2 = this.logCollector) || void 0 === o2 ? void 0 : o2.getLogs(), a2 = i2 && i2.length > 0;
              if (!this.options.enabled) return void Le.info("CallReportCollector: Skipping report \u2014 call reports disabled");
              if (0 === this.statsBuffer.length && !a2) return void Le.info("CallReportCollector: Skipping report \u2014 no stats or logs collected");
              const c2 = this._segmentIndex > 0, l2 = this._segmentIndex, d2 = Object.assign(Object.assign({ summary: Object.assign(Object.assign({}, e2), { durationSeconds: this.callEndTime && this.callStartTime ? (this.callEndTime.getTime() - this.callStartTime.getTime()) / 1e3 : void 0, startTimestamp: this.callStartTime.toISOString(), endTimestamp: null === (r2 = this.callEndTime) || void 0 === r2 ? void 0 : r2.toISOString() }), stats: this.statsBuffer }, i2 && i2.length > 0 ? { logs: i2 } : {}), c2 ? { segment: l2 } : {});
              yield this._sendPayload(d2, t2, n2, s2, true);
            }));
          }
          sendPayload(e2, t2, n2, s2, o2 = false) {
            return i(this, void 0, void 0, (function* () {
              yield this._sendPayload(e2, t2, n2, s2, false, o2);
            }));
          }
          _sendPayload(e2, t2, n2, s2, o2 = true, r2 = false) {
            var a2, c2;
            return i(this, void 0, void 0, (function* () {
              const i2 = new URL(n2), l2 = `${i2.protocol.replace(/^ws/, "http")}//${i2.host}/call_report`, d2 = o2 ? "final report" : `intermediate segment ${e2.segment}`;
              Le.info(`CallReportCollector: Posting ${d2}`, { endpoint: l2, intervals: e2.stats.length, logEntries: null !== (c2 = null === (a2 = e2.logs) || void 0 === a2 ? void 0 : a2.length) && void 0 !== c2 ? c2 : 0, callId: e2.summary.callId, segment: e2.segment });
              const u2 = { "Content-Type": "application/json", "x-call-report-id": t2, "x-call-id": e2.summary.callId };
              s2 && (u2["x-voice-sdk-id"] = s2);
              const h2 = JSON.stringify(e2), p2 = (o2 || r2) && h2.length <= Ni.KEEPALIVE_BODY_LIMIT_BYTES;
              let g2;
              for (let e3 = 0; e3 <= Ni.RETRY_DELAYS_MS.length; e3++) {
                try {
                  const t4 = yield fetch(l2, Object.assign({ method: "POST", headers: u2, body: h2 }, p2 ? { keepalive: true } : {}));
                  if (t4.ok) return void Le.info(`CallReportCollector: Successfully posted ${d2}`, { attempt: e3 + 1, status: t4.status });
                  {
                    const i3 = yield t4.text();
                    g2 = new Error(`Call report POST failed with status ${t4.status}`), Le.error(`CallReportCollector: Failed to post ${d2}`, { attempt: e3 + 1, status: t4.status, error: i3 });
                  }
                } catch (t4) {
                  g2 = t4, Le.warn(`CallReportCollector: Network error posting ${d2}`, { attempt: e3 + 1, error: t4 });
                }
                const t3 = Ni.RETRY_DELAYS_MS[e3];
                if (void 0 === t3) break;
                Le.info(`CallReportCollector: Retrying ${d2} in ${t3}ms`, { attempt: e3 + 2 }), yield new Promise(((e4) => setTimeout(e4, t3)));
              }
              throw Le.error(`CallReportCollector: Exhausted retries posting ${d2}`, { error: g2 }), g2 instanceof Error ? g2 : new Error("Call report POST failed after retries");
            }));
          }
          getStatsBuffer() {
            return this.statsBuffer;
          }
          shouldForceRelayCandidateForRecovery() {
            var e2, t2, i2, n2, s2, o2, r2, a2, c2, l2, d2, u2, h2, p2, g2, v2;
            if (this.statsBuffer.length < 2) return false;
            const m2 = this.statsBuffer[this.statsBuffer.length - 1], f2 = this.statsBuffer[this.statsBuffer.length - 2], _2 = null === (e2 = m2.ice) || void 0 === e2 ? void 0 : e2.local;
            if (!("vpn" === (null == _2 ? void 0 : _2.networkType) && "relay" !== (null == _2 ? void 0 : _2.candidateType))) return false;
            const S2 = false === (null === (t2 = m2.ice) || void 0 === t2 ? void 0 : t2.writable), y2 = "disconnected" === (null === (i2 = m2.transport) || void 0 === i2 ? void 0 : i2.iceState) || "failed" === (null === (n2 = m2.transport) || void 0 === n2 ? void 0 : n2.iceState), b2 = this._positiveDelta(null === (s2 = m2.ice) || void 0 === s2 ? void 0 : s2.requestsSent, null === (o2 = f2.ice) || void 0 === o2 ? void 0 : o2.requestsSent), I2 = this._positiveDelta(null === (r2 = m2.ice) || void 0 === r2 ? void 0 : r2.responsesReceived, null === (a2 = f2.ice) || void 0 === a2 ? void 0 : a2.responsesReceived), E2 = b2 > 0 && 0 === I2, C2 = this._positiveDelta(null === (l2 = null === (c2 = m2.audio) || void 0 === c2 ? void 0 : c2.outbound) || void 0 === l2 ? void 0 : l2.bytesSent, null === (u2 = null === (d2 = f2.audio) || void 0 === d2 ? void 0 : d2.outbound) || void 0 === u2 ? void 0 : u2.bytesSent), w2 = this._positiveDelta(null === (p2 = null === (h2 = m2.audio) || void 0 === h2 ? void 0 : h2.inbound) || void 0 === p2 ? void 0 : p2.bytesReceived, null === (v2 = null === (g2 = f2.audio) || void 0 === g2 ? void 0 : g2.inbound) || void 0 === v2 ? void 0 : v2.bytesReceived);
            return S2 || y2 || E2 || C2 > 0 && 0 === w2;
          }
          getLogs() {
            var e2, t2;
            return null !== (t2 = null === (e2 = this.logCollector) || void 0 === e2 ? void 0 : e2.getLogs()) && void 0 !== t2 ? t2 : [];
          }
          _positiveDelta(e2, t2) {
            return void 0 === e2 || void 0 === t2 ? 0 : Math.max(0, e2 - t2);
          }
          cleanup() {
            this._lastLocalAudioTrackSnapshotJson = null, this.logCollector && (this.logCollector.clear(), this.logCollector = null);
          }
          setHeld(e2) {
            this._isHeld !== e2 && (this._isHeld = e2, this._trackBreach(B, false), this._trackBreach(j, false));
          }
          _scheduleNextCollection() {
            if (this._stopped || !this.peerConnection || !this.intervalStartTime || this.intervalId) return;
            const e2 = this._collectionIntervalFor();
            this.intervalId = setTimeout((() => {
              this.intervalId = null, this._collectStats().finally((() => {
                !this._stopped && this.peerConnection && this._scheduleNextCollection();
              }));
            }), e2);
          }
          _collectionIntervalFor() {
            const e2 = this._positiveInterval(this.options.interval, 5e3);
            return Math.min(Ni.INITIAL_COLLECTION_INTERVAL_MS, e2);
          }
          _positiveInterval(e2, t2) {
            return "number" == typeof e2 && Number.isFinite(e2) && e2 > 0 ? e2 : t2;
          }
          _collectStats(e2 = false) {
            var t2, n2;
            return i(this, void 0, void 0, (function* () {
              if (this.peerConnection && this.intervalStartTime) try {
                const i2 = yield this.peerConnection.getStats(), s2 = /* @__PURE__ */ new Date();
                let o2 = null, r2 = null, a2 = null, c2 = null, l2 = null, d2 = null;
                const u2 = [];
                let h2 = null;
                i2.forEach(((e3) => {
                  switch (e3.type) {
                    case "outbound-rtp":
                      "audio" === e3.kind && "audio" === e3.mediaType && (o2 = e3);
                      break;
                    case "inbound-rtp":
                      "audio" === e3.kind && "audio" === e3.mediaType && (r2 = e3);
                      break;
                    case "media-playout":
                      "audio" === e3.kind && (a2 = e3);
                      break;
                    case "remote-inbound-rtp":
                      "audio" === e3.kind && (c2 = e3);
                      break;
                    case "remote-outbound-rtp":
                      "audio" === e3.kind && (l2 = e3);
                      break;
                    case "candidate-pair":
                      (e3.nominated || "succeeded" === e3.state) && u2.push(e3);
                      break;
                    case "transport":
                      h2 = e3;
                  }
                }));
                const p2 = null == h2 ? void 0 : h2.selectedCandidatePairId, g2 = p2 ? null === (n2 = (t2 = i2).get) || void 0 === n2 ? void 0 : n2.call(t2, p2) : void 0, v2 = this._getCodec(i2, null == o2 ? void 0 : o2.codecId), m2 = this._getCodec(i2, null == r2 ? void 0 : r2.codecId);
                if (d2 = "candidate-pair" === (null == g2 ? void 0 : g2.type) ? g2 : p2 ? u2.find(((e3) => e3.id === p2)) || u2[u2.length - 1] || null : u2[u2.length - 1] || null, o2) {
                  const e3 = this._getOutboundAudioLevel(i2, o2);
                  if (null !== e3 && this.intervalAudioLevels.outbound.push(e3), void 0 !== this.previousStats.outboundBytes && void 0 !== this.previousStats.timestamp) {
                    const e4 = (o2.bytesSent || 0) - this.previousStats.outboundBytes, t3 = (o2.timestamp || s2.getTime()) - this.previousStats.timestamp;
                    if (t3 > 0) {
                      const i3 = 8 * e4 * 1e3 / t3;
                      this.intervalBitrates.outbound.push(i3);
                    }
                  }
                  this.previousStats.outboundBytes = o2.bytesSent;
                }
                if (r2) {
                  const e3 = this._getInboundAudioLevel(i2, r2);
                  if (null !== e3 && this.intervalAudioLevels.inbound.push(e3), void 0 !== r2.jitter && this.intervalJitters.push(1e3 * r2.jitter), void 0 !== this.previousStats.inboundBytes && void 0 !== this.previousStats.timestamp) {
                    const e4 = (r2.bytesReceived || 0) - this.previousStats.inboundBytes, t3 = (r2.timestamp || s2.getTime()) - this.previousStats.timestamp;
                    if (t3 > 0) {
                      const i3 = 8 * e4 * 1e3 / t3;
                      this.intervalBitrates.inbound.push(i3);
                    }
                  }
                  this.previousStats.inboundBytes = r2.bytesReceived;
                }
                let f2, _2, S2, y2, b2;
                d2 && void 0 !== d2.currentRoundTripTime && this.intervalRTTs.push(d2.currentRoundTripTime), d2 && (f2 = this._resolveCandidate(i2, d2.localCandidateId), _2 = this._resolveCandidate(i2, d2.remoteCandidateId)), d2 && (S2 = Object.assign(Object.assign({ id: d2.id, localCandidateId: d2.localCandidateId, remoteCandidateId: d2.remoteCandidateId, state: d2.state, nominated: d2.nominated, writable: d2.writable, currentRoundTripTime: d2.currentRoundTripTime, requestsSent: d2.requestsSent, responsesReceived: d2.responsesReceived }, f2 ? { local: f2 } : {}), _2 ? { remote: _2 } : {}), null !== this.previousCandidatePairSnapshot && d2.id !== this.previousCandidatePairSnapshot.id && (Le.debug("CallReportCollector: ICE candidate pair changed mid-call", { previous: this.previousCandidatePairSnapshot, current: S2 }), this._emitWarning(X)), this.previousCandidatePairSnapshot = S2), o2 && (y2 = this._getLocalAudioTrackSnapshot(), b2 = this._getOutboundAudioSourceStats(i2, o2)), this._logLocalAudioTrackSnapshot(y2, b2), this.previousStats.timestamp = s2.getTime();
                const I2 = s2.getTime() - this.intervalStartTime.getTime(), E2 = this._collectionIntervalFor();
                if (e2 || I2 >= E2) {
                  const e3 = this._createStatsEntry(this.intervalStartTime, s2, o2, r2, d2, f2, _2, h2, y2, b2, a2, c2, l2, v2, m2);
                  this.statsBuffer.push(e3), this.statsBuffer.length > this.MAX_BUFFER_SIZE && (this.statsBuffer.shift(), Le.warn("CallReportCollector: Buffer size limit reached, removing oldest entry")), this._checkQualityWarnings(e3, r2), this._requestIntermediateFlushIfNeeded(s2), this.intervalStartTime = s2, this._resetIntervalAccumulators();
                }
              } catch (e3) {
                Le.error("CallReportCollector: Error collecting stats", { error: e3 });
              }
            }));
          }
          _getIntermediateReportInterval() {
            var e2;
            return null !== (e2 = this.options.intermediateReportInterval) && void 0 !== e2 ? e2 : Ni.DEFAULT_INTERMEDIATE_REPORT_INTERVAL_MS;
          }
          _requestIntermediateFlushIfNeeded(e2) {
            var t2, i2;
            const n2 = this.statsBuffer.length, s2 = null !== (i2 = null === (t2 = this.logCollector) || void 0 === t2 ? void 0 : t2.getLogCount()) && void 0 !== i2 ? i2 : 0;
            if (!this.onFlushNeeded || this._flushing) return;
            if (0 === n2 && 0 === s2) return void Le.debug("CallReportCollector: Skipping intermediate flush request \u2014 no stats or logs buffered");
            const o2 = this._getIntermediateReportInterval(), r2 = e2.getTime() - this._lastIntermediateFlushTime.getTime();
            let a2 = null;
            if (n2 >= Ni.STATS_FLUSH_THRESHOLD || s2 >= Ni.LOGS_FLUSH_THRESHOLD ? a2 = "buffer-limit" : o2 > 0 && r2 >= o2 && (a2 = "safety-interval"), a2) {
              Le.info("CallReportCollector: Requesting intermediate report flush", { reason: a2, statsIntervals: n2, logEntries: s2, msSinceLastFlush: r2 }), this._lastIntermediateFlushTime = e2;
              try {
                this.onFlushNeeded();
              } catch (e3) {
                Le.error("CallReportCollector: onFlushNeeded callback error", { error: e3 });
              }
            }
          }
          _checkQualityWarnings(e2, t2) {
            var i2, n2, s2, o2, r2, a2, c2, l2, d2, u2, h2, p2, g2, v2, m2;
            if (!this.onWarning) return;
            const f2 = null === (i2 = e2.connection) || void 0 === i2 ? void 0 : i2.roundTripTimeAvg, _2 = null === (s2 = null === (n2 = e2.audio) || void 0 === n2 ? void 0 : n2.inbound) || void 0 === s2 ? void 0 : s2.jitterAvg;
            let S2;
            if (t2) {
              const e3 = null !== (o2 = t2.packetsReceived) && void 0 !== o2 ? o2 : 0, i3 = null !== (r2 = t2.packetsLost) && void 0 !== r2 ? r2 : 0;
              if (null !== this._prevPacketsReceived && null !== this._prevPacketsLost) {
                const t3 = e3 - this._prevPacketsReceived, n3 = i3 - this._prevPacketsLost, s3 = t3 + n3;
                s3 > 0 && (S2 = n3 / s3 * 100);
              }
              this._prevPacketsReceived = e3, this._prevPacketsLost = i3;
            }
            if (this._trackBreach(P, void 0 !== f2 && f2 > Ni.THRESHOLD_RTT_MS), this._trackBreach(x, void 0 !== _2 && _2 > Ni.THRESHOLD_JITTER_MS), this._trackBreach(U, void 0 !== S2 && S2 > Ni.THRESHOLD_PACKET_LOSS_PCT), this._trackLowLocalAudio(e2), this._isHeld ? this._trackBreach(B, false) : this._trackLowInboundAudio(e2), void 0 !== f2 && void 0 !== _2 && void 0 !== S2) {
              const e3 = 93.2 - 0.11 * _2 - 2.5 * S2 - 0.01 * (1e3 * f2), t3 = Math.max(1, Math.min(4.5, 1 + 0.035 * e3 + e3 * (e3 - 60) * (100 - e3) * 7e-6));
              this._trackBreach(F, t3 < Ni.THRESHOLD_MOS);
            } else this._trackBreach(F, false);
            if (void 0 !== (null === (c2 = null === (a2 = e2.audio) || void 0 === a2 ? void 0 : a2.inbound) || void 0 === c2 ? void 0 : c2.bytesReceived)) {
              const t3 = null === (u2 = null === (d2 = null === (l2 = this._previousStatsEntryForWarnings) || void 0 === l2 ? void 0 : l2.audio) || void 0 === d2 ? void 0 : d2.inbound) || void 0 === u2 ? void 0 : u2.bytesReceived, i3 = e2.audio.inbound.bytesReceived;
              this._trackBreach(j, !this._isHeld && void 0 !== t3 && i3 - t3 == 0);
            }
            if (void 0 !== (null === (p2 = null === (h2 = e2.audio) || void 0 === h2 ? void 0 : h2.outbound) || void 0 === p2 ? void 0 : p2.bytesSent)) {
              const t3 = null === (m2 = null === (v2 = null === (g2 = this._previousStatsEntryForWarnings) || void 0 === g2 ? void 0 : g2.audio) || void 0 === v2 ? void 0 : v2.outbound) || void 0 === m2 ? void 0 : m2.bytesSent, i3 = e2.audio.outbound.bytesSent;
              this._trackBreach(H, void 0 !== t3 && i3 - t3 == 0);
            }
            this._previousStatsEntryForWarnings = e2;
          }
          _trackLowLocalAudio(e2) {
            var t2;
            const i2 = null === (t2 = e2.audio) || void 0 === t2 ? void 0 : t2.outbound, n2 = null == i2 ? void 0 : i2.audioLevelAvg, s2 = null == i2 ? void 0 : i2.localTrack;
            if (void 0 === n2 || false === (null == s2 ? void 0 : s2.enabled) || true === (null == s2 ? void 0 : s2.muted)) return void this._resetLowLocalAudioWarning();
            if (!(n2 <= Ni.THRESHOLD_LOCAL_AUDIO_LEVEL)) return this._hasConfirmedLocalAudio = true, this._confirmedLocalAudioSilenceMs = 0, void this._trackBreach($, false);
            this._hasConfirmedLocalAudio ? (this._confirmedLocalAudioSilenceMs += this._getStatsIntervalDurationMs(e2), this._confirmedLocalAudioSilenceMs >= Ni.CONFIRMED_LOCAL_AUDIO_SILENCE_MS && this._emitWarningOncePerEpisode($)) : this._trackBreach($, true);
          }
          _resetLowLocalAudioWarning() {
            this._confirmedLocalAudioSilenceMs = 0, this._trackBreach($, false);
          }
          _getStatsIntervalDurationMs(e2) {
            const t2 = new Date(e2.intervalStartUtc).getTime(), i2 = new Date(e2.intervalEndUtc).getTime() - t2;
            return Number.isFinite(i2) && i2 > 0 ? i2 : this.options.interval;
          }
          _trackLowInboundAudio(e2) {
            var t2;
            const i2 = null === (t2 = e2.audio) || void 0 === t2 ? void 0 : t2.inbound, n2 = null == i2 ? void 0 : i2.audioLevelAvg;
            if (void 0 === n2) return void this._trackBreach(B, false);
            const s2 = n2 <= Ni.THRESHOLD_INBOUND_AUDIO_LEVEL;
            this._trackBreach(B, s2);
          }
          _trackBreach(e2, t2) {
            var i2, n2;
            if (t2) {
              if (this._breachCounters[e2] = (null !== (i2 = this._breachCounters[e2]) && void 0 !== i2 ? i2 : 0) + 1, this._breachCounters[e2] >= Ni.CONSECUTIVE_BREACHES_REQUIRED) {
                this._activeWarnings.add(e2);
                const t3 = Date.now();
                t3 - (null !== (n2 = this._lastWarningEmitted[e2]) && void 0 !== n2 ? n2 : 0) >= Ni.WARNING_THROTTLE_MS && (this._lastWarningEmitted[e2] = t3, this._emitWarning(e2));
              }
            } else this._breachCounters[e2] = 0, this._activeWarnings.delete(e2), delete this._lastWarningEmitted[e2];
          }
          _emitWarningOncePerEpisode(e2) {
            this._activeWarnings.has(e2) || (this._activeWarnings.add(e2), this._lastWarningEmitted[e2] = Date.now(), this._emitWarning(e2));
          }
          _emitWarning(e2) {
            var t2;
            try {
              const i2 = be(e2);
              Le.warn(`CallReportCollector: warning ${i2.code}: ${i2.message}`), null === (t2 = this.onWarning) || void 0 === t2 || t2.call(this, i2);
            } catch (t3) {
              Le.error(`CallReportCollector: Failed to emit warning ${e2}`, { error: t3 });
            }
          }
          _createStatsEntry(e2, t2, i2, n2, s2, o2, r2, a2, c2, l2, d2, u2, h2, p2, g2) {
            const v2 = { intervalStartUtc: e2.toISOString(), intervalEndUtc: t2.toISOString(), audio: {} };
            if (i2 && (v2.audio.outbound = Object.assign(Object.assign(Object.assign(Object.assign(Object.assign(Object.assign(Object.assign(Object.assign(Object.assign(Object.assign({ packetsSent: i2.packetsSent, bytesSent: i2.bytesSent, audioLevelAvg: this._average(this.intervalAudioLevels.outbound), bitrateAvg: this._average(this.intervalBitrates.outbound) }, c2 ? { localTrack: c2 } : {}), l2 ? { mediaSource: l2 } : {}), void 0 !== i2.retransmittedPacketsSent ? { retransmittedPacketsSent: i2.retransmittedPacketsSent } : {}), void 0 !== i2.retransmittedBytesSent ? { retransmittedBytesSent: i2.retransmittedBytesSent } : {}), void 0 !== i2.headerBytesSent ? { headerBytesSent: i2.headerBytesSent } : {}), void 0 !== i2.nackCount ? { nackCount: i2.nackCount } : {}), void 0 !== i2.targetBitrate ? { targetBitrate: i2.targetBitrate } : {}), void 0 !== i2.totalPacketSendDelay ? { totalPacketSendDelay: i2.totalPacketSendDelay } : {}), void 0 !== i2.active ? { active: i2.active } : {}), p2 ? { codec: this._buildCodecSnapshot(p2, i2.codecId) } : {})), n2 && (v2.audio.inbound = Object.assign(Object.assign(Object.assign(Object.assign(Object.assign(Object.assign(Object.assign(Object.assign(Object.assign(Object.assign(Object.assign(Object.assign({ packetsReceived: n2.packetsReceived, bytesReceived: n2.bytesReceived, packetsLost: n2.packetsLost, packetsDiscarded: n2.packetsDiscarded, jitterBufferDelay: n2.jitterBufferDelay, jitterBufferEmittedCount: n2.jitterBufferEmittedCount, totalSamplesReceived: n2.totalSamplesReceived, concealedSamples: n2.concealedSamples, concealmentEvents: n2.concealmentEvents, audioLevelAvg: this._average(this.intervalAudioLevels.inbound), jitterAvg: this._average(this.intervalJitters), bitrateAvg: this._average(this.intervalBitrates.inbound) }, void 0 !== n2.nackCount ? { nackCount: n2.nackCount } : {}), void 0 !== n2.headerBytesReceived ? { headerBytesReceived: n2.headerBytesReceived } : {}), void 0 !== n2.fecPacketsReceived ? { fecPacketsReceived: n2.fecPacketsReceived } : {}), void 0 !== n2.fecPacketsDiscarded ? { fecPacketsDiscarded: n2.fecPacketsDiscarded } : {}), void 0 !== n2.jitterBufferTargetDelay ? { jitterBufferTargetDelay: n2.jitterBufferTargetDelay } : {}), void 0 !== n2.jitterBufferMinimumDelay ? { jitterBufferMinimumDelay: n2.jitterBufferMinimumDelay } : {}), void 0 !== n2.totalSamplesDecoded ? { totalSamplesDecoded: n2.totalSamplesDecoded } : {}), void 0 !== n2.samplesDecodedWithSilence ? { samplesDecodedWithSilence: n2.samplesDecodedWithSilence } : {}), void 0 !== n2.samplesDecodedWithConcealment ? { samplesDecodedWithConcealment: n2.samplesDecodedWithConcealment } : {}), void 0 !== n2.totalAudioEnergy ? { totalAudioEnergy: n2.totalAudioEnergy } : {}), void 0 !== n2.totalSamplesDuration ? { totalSamplesDuration: n2.totalSamplesDuration } : {}), g2 ? { codec: this._buildCodecSnapshot(g2, n2.codecId) } : {})), s2 && (v2.connection = Object.assign(Object.assign({ roundTripTimeAvg: this._average(this.intervalRTTs), currentRoundTripTime: s2.currentRoundTripTime }, void 0 !== s2.currentRoundTripTime ? { roundTripTimeSource: "candidate-pair.currentRoundTripTime" } : {}), { packetsSent: s2.packetsSent, packetsReceived: s2.packetsReceived, bytesSent: s2.bytesSent, bytesReceived: s2.bytesReceived }), v2.ice = Object.assign(Object.assign({ id: s2.id, localCandidateId: s2.localCandidateId, remoteCandidateId: s2.remoteCandidateId, state: s2.state, nominated: s2.nominated, writable: s2.writable, currentRoundTripTime: s2.currentRoundTripTime, requestsSent: s2.requestsSent, responsesReceived: s2.responsesReceived }, o2 ? { local: o2 } : {}), r2 ? { remote: r2 } : {})), a2 && (v2.transport = Object.assign(Object.assign(Object.assign(Object.assign(Object.assign(Object.assign({}, void 0 !== a2.iceState ? { iceState: a2.iceState } : {}), void 0 !== a2.dtlsState ? { dtlsState: a2.dtlsState } : {}), void 0 !== a2.srtpCipher ? { srtpCipher: a2.srtpCipher } : {}), void 0 !== a2.tlsVersion ? { tlsVersion: a2.tlsVersion } : {}), void 0 !== a2.selectedCandidatePairChanges ? { selectedCandidatePairChanges: a2.selectedCandidatePairChanges } : {}), void 0 !== a2.selectedCandidatePairId ? { selectedCandidatePairId: a2.selectedCandidatePairId } : {})), d2 && (v2.mediaPlayout = this._withoutUndefined({ synthesizedSamples: d2.synthesizedSamples, synthesizedDuration: d2.synthesizedDuration, totalPlayoutDelay: d2.totalPlayoutDelay, totalSampleCount: d2.totalSampleCount })), u2 || h2) {
              const e3 = {};
              if (u2) {
                const t3 = void 0 !== u2.jitter ? 1e3 * u2.jitter : void 0;
                let i3;
                "number" == typeof u2.totalRoundTripTime && "number" == typeof u2.roundTripTimeMeasurements && u2.roundTripTimeMeasurements > 0 && (i3 = u2.totalRoundTripTime / u2.roundTripTimeMeasurements), e3.inbound = this._withoutUndefined({ packetsReceived: u2.packetsReceived, packetsLost: u2.packetsLost, fractionLost: u2.fractionLost, jitter: t3, roundTripTime: u2.roundTripTime, totalRoundTripTime: u2.totalRoundTripTime, roundTripTimeMeasurements: u2.roundTripTimeMeasurements, roundTripTimeAvg: i3, nackCount: u2.nackCount, reportsReceived: u2.reportsReceived, packetsDiscarded: u2.packetsDiscarded });
              }
              h2 && (e3.outbound = this._withoutUndefined({ packetsSent: h2.packetsSent, bytesSent: h2.bytesSent, reportsCount: h2.reportsCount, roundTripTime: h2.roundTripTime, totalPacketSendDelay: h2.totalPacketSendDelay })), v2.remoteRtcp = e3;
            }
            return v2;
          }
          _resolveCandidate(e2, t2) {
            if (!t2) return void Le.debug("CallReportCollector: candidateId is empty, skipping resolve");
            const i2 = e2.get(t2);
            if (!i2) return void Le.debug("CallReportCollector: candidate not found in stats report", { candidateId: t2 });
            const n2 = {};
            if (void 0 !== i2.id && (n2.id = i2.id), void 0 !== i2.address ? n2.address = i2.address : void 0 !== i2.ip && (n2.address = i2.ip), void 0 !== i2.port && (n2.port = i2.port), void 0 !== i2.candidateType && (n2.candidateType = i2.candidateType), void 0 !== i2.protocol && (n2.protocol = i2.protocol), void 0 !== i2.networkType && (n2.networkType = i2.networkType), void 0 !== i2.url && (n2.url = i2.url), void 0 !== i2.relayProtocol && (n2.relayProtocol = i2.relayProtocol), 0 !== Object.keys(n2).length) return n2;
            Le.debug("CallReportCollector: candidate report has no usable fields", { candidateId: t2 });
          }
          _getCodec(e2, t2) {
            if (!t2) return;
            const i2 = e2.get(t2);
            return i2 && "codec" === i2.type ? i2 : void 0;
          }
          _buildCodecSnapshot(e2, t2) {
            const i2 = this._withoutUndefined(Object.assign({ mimeType: e2.mimeType, clockRate: e2.clockRate, channels: e2.channels, payloadType: e2.payloadType, sdpFmtpLine: e2.sdpFmtpLine }, void 0 !== t2 ? { codecId: t2 } : {}));
            return Object.keys(i2).length > 0 ? i2 : void 0;
          }
          _getOutboundMediaSource(e2, t2) {
            let i2;
            return t2.mediaSourceId && (i2 = e2.get(t2.mediaSourceId)), i2 || e2.forEach(((e3) => {
              i2 || "media-source" !== e3.type || "audio" !== e3.kind || (i2 = e3);
            })), i2;
          }
          _getLocalAudioTrackSnapshot() {
            var e2, t2;
            try {
              const i2 = null === (e2 = this.peerConnection) || void 0 === e2 ? void 0 : e2.getSenders().find(((e3) => {
                var t3;
                return "audio" === (null === (t3 = e3.track) || void 0 === t3 ? void 0 : t3.kind);
              })), n2 = null == i2 ? void 0 : i2.track;
              if (!n2) return;
              const s2 = null === (t2 = n2.getSettings) || void 0 === t2 ? void 0 : t2.call(n2), o2 = this._withoutUndefined({ deviceId: null == s2 ? void 0 : s2.deviceId, groupId: null == s2 ? void 0 : s2.groupId, channelCount: null == s2 ? void 0 : s2.channelCount, sampleRate: null == s2 ? void 0 : s2.sampleRate, sampleSize: null == s2 ? void 0 : s2.sampleSize, latency: null == s2 ? void 0 : s2.latency, echoCancellation: null == s2 ? void 0 : s2.echoCancellation, noiseSuppression: null == s2 ? void 0 : s2.noiseSuppression, autoGainControl: null == s2 ? void 0 : s2.autoGainControl }), r2 = this._withoutUndefined(Object.assign({ id: n2.id, label: n2.label, enabled: n2.enabled, muted: n2.muted, readyState: n2.readyState, contentHint: n2.contentHint }, Object.keys(o2).length > 0 ? { settings: o2 } : {}));
              return Object.keys(r2).length > 0 ? r2 : void 0;
            } catch (e3) {
              return void Le.debug("CallReportCollector: unable to snapshot local audio track", { error: e3 });
            }
          }
          _getOutboundAudioSourceStats(e2, t2) {
            const i2 = this._getOutboundMediaSource(e2, t2);
            if (!i2) return;
            const n2 = this._withoutUndefined({ id: i2.id, trackIdentifier: i2.trackIdentifier, audioLevel: i2.audioLevel, totalAudioEnergy: i2.totalAudioEnergy, totalSamplesDuration: i2.totalSamplesDuration, echoReturnLoss: i2.echoReturnLoss, echoReturnLossEnhancement: i2.echoReturnLossEnhancement });
            return Object.keys(n2).length > 0 ? n2 : void 0;
          }
          _logLocalAudioTrackSnapshot(e2, t2) {
            if (!e2 || 0 === Object.keys(e2).length) return;
            const i2 = this._stableStringify(e2);
            i2 !== this._lastLocalAudioTrackSnapshotJson && (this._lastLocalAudioTrackSnapshotJson = i2, Le.debug("CallReportCollector: local audio track snapshot", { localTrack: e2, mediaSource: t2 }));
          }
          _withoutUndefined(e2) {
            return Object.keys(e2).reduce(((t2, i2) => {
              const n2 = e2[i2];
              return void 0 !== n2 && (t2[i2] = n2), t2;
            }), {});
          }
          _stableStringify(e2) {
            return JSON.stringify(this._sortObjectKeys(e2));
          }
          _sortObjectKeys(e2) {
            return Array.isArray(e2) ? e2.map(((e3) => this._sortObjectKeys(e3))) : e2 && "object" == typeof e2 ? Object.keys(e2).sort().reduce(((t2, i2) => (t2[i2] = this._sortObjectKeys(e2[i2]), t2)), {}) : e2;
          }
          _getOutboundAudioLevel(e2, t2) {
            const i2 = this._getOutboundMediaSource(e2, t2);
            if (void 0 !== (null == i2 ? void 0 : i2.audioLevel)) return i2.audioLevel;
            if (i2) {
              const e3 = this._computeAudioLevelFromEnergy(i2.totalAudioEnergy, i2.totalSamplesDuration, "outbound");
              if (null !== e3) return e3;
            }
            return this._getTrackAudioLevel(e2, t2.trackId);
          }
          _getInboundAudioLevel(e2, t2) {
            if (void 0 !== t2.audioLevel) return t2.audioLevel;
            const i2 = this._computeAudioLevelFromEnergy(t2.totalAudioEnergy, t2.totalSamplesDuration, "inbound");
            return null !== i2 ? i2 : this._getTrackAudioLevel(e2, t2.trackId);
          }
          _computeAudioLevelFromEnergy(e2, t2, i2) {
            if (void 0 === e2 || void 0 === t2) return null;
            const n2 = "inbound" === i2 ? "inboundAudioEnergy" : "outboundAudioEnergy", s2 = "inbound" === i2 ? "inboundSamplesDuration" : "outboundSamplesDuration", o2 = this.previousStats[n2], r2 = this.previousStats[s2];
            if (this.previousStats[n2] = e2, this.previousStats[s2] = t2, void 0 === o2 || void 0 === r2) return null;
            const a2 = e2 - o2, c2 = t2 - r2;
            if (c2 <= 0) return null;
            const l2 = Math.sqrt(a2 / c2);
            return Math.min(1, Math.max(0, l2));
          }
          _getTrackAudioLevel(e2, t2) {
            var i2;
            if (!t2) return null;
            const n2 = e2.get(t2);
            return n2 && null !== (i2 = n2.audioLevel) && void 0 !== i2 ? i2 : null;
          }
          _average(e2) {
            if (0 === e2.length) return;
            const t2 = e2.reduce(((e3, t3) => e3 + t3), 0);
            return parseFloat((t2 / e2.length).toFixed(4));
          }
          _resetIntervalAccumulators() {
            this.intervalAudioLevels = { outbound: [], inbound: [] }, this.intervalJitters = [], this.intervalRTTs = [], this.intervalBitrates = { outbound: [], inbound: [] };
          }
        }
        Ni.INITIAL_COLLECTION_INTERVAL_MS = 1e3, Ni.STATS_FLUSH_THRESHOLD = 300, Ni.LOGS_FLUSH_THRESHOLD = 800, Ni.DEFAULT_INTERMEDIATE_REPORT_INTERVAL_MS = 18e4, Ni.CONSECUTIVE_BREACHES_REQUIRED = 3, Ni.THRESHOLD_RTT_MS = 0.4, Ni.THRESHOLD_JITTER_MS = 30, Ni.THRESHOLD_PACKET_LOSS_PCT = 1, Ni.THRESHOLD_MOS = 3.5, Ni.THRESHOLD_LOCAL_AUDIO_LEVEL = 1e-3, Ni.CONFIRMED_LOCAL_AUDIO_SILENCE_MS = 3e4, Ni.THRESHOLD_INBOUND_AUDIO_LEVEL = 1e-3, Ni.WARNING_THROTTLE_MS = 15e3, Ni.RETRY_DELAYS_MS = [500, 1e3, 2e3], Ni.KEEPALIVE_BODY_LIMIT_BYTES = 61440;
        class Li {
          constructor(e2, t2) {
            this._buffers = { local: [], remote: [] }, this._bufferBytes = { local: 0, remote: 0 }, this._trackStates = { local: null, remote: null }, this._processors = [], this._readerCancellers = [], this._flushTimerId = null, this._flushing = false, this._stopped = false, this._started = false, this._startedAt = null, this._endedAt = null, this._overflowWarnedThisWindow = { local: false, remote: false }, this.onWarning = null, this._host = null, this._callReportId = null, this.options = e2, this.callContext = t2, this.recordingId = t2.recordingId || `${t2.callId}-${Date.now().toString(36)}`;
          }
          start(e2, t2) {
            if (!this.options.enabled || this._started) return;
            if ("function" != typeof MediaStreamTrackProcessor) return Le.warn("CallRecorder: MediaStreamTrackProcessor unavailable \u2014 recording disabled for this call"), this._emitWarning(W), void (this._started = true);
            const i2 = this.options.tracks || ["local", "remote"];
            let n2 = 0;
            if (i2.includes("local") && e2 && (this._attach(e2, "local"), n2++), i2.includes("remote") && t2 && (this._attach(t2, "remote"), n2++), 0 === n2) return Le.info("CallRecorder: no audio tracks available to record \u2014 recording idle for this call", { callId: this.callContext.callId }), void (this._started = true);
            this._startedAt = /* @__PURE__ */ new Date(), this._started = true, this._scheduleFlush(), Le.info("CallRecorder: started", { callId: this.callContext.callId, tracksAttached: n2, flushIntervalMs: this._flushIntervalMs(), maxBufferBytes: this._maxBufferBytes() });
          }
          stop() {
            this._stopped || (this._stopped = true, this._clearFlushTimer(), this._cancelReaders(), this._endedAt = /* @__PURE__ */ new Date(), Le.info("CallRecorder: stopped", { callId: this.callContext.callId, packetsBuffered: this._buffers.local.length + this._buffers.remote.length }));
          }
          postFinalReport() {
            return i(this, void 0, void 0, (function* () {
              if (!this.options.enabled) return;
              if (this.stop(), !this._startedAt) return void Le.debug("CallRecorder: postFinalReport skipped \u2014 never started");
              const e2 = this._resolveEndpointFromContext();
              if (!e2) return void Le.debug("CallRecorder: postFinalReport skipped \u2014 no host available");
              const t2 = this.options.tracks || ["local", "remote"], i2 = this._startedAt, n2 = this._endedAt || /* @__PURE__ */ new Date(), s2 = t2.flatMap(((e3) => {
                const t3 = this._drain(e3);
                if (0 === t3.length) return [];
                const s3 = this._buildEnvelope(t3, e3, "final", i2, n2);
                return [{ envelope: s3, bodyBytes: JSON.stringify(s3).length }];
              })), o2 = s2.reduce(((e3, t3) => e3 + t3.bodyBytes), 0) <= Li.KEEPALIVE_BODY_LIMIT_BYTES, r2 = (yield Promise.allSettled(s2.map((({ envelope: t3 }) => this._postRecording(e2, t3, o2))))).find(((e3) => "rejected" === e3.status));
              if (r2) throw r2.reason;
            }));
          }
          cleanup() {
            this._clearFlushTimer(), this._cancelReaders(), this._buffers = { local: [], remote: [] }, this._bufferBytes = { local: 0, remote: 0 }, this._trackStates = { local: null, remote: null };
          }
          _attach(e2, t2) {
            this._trackStates[t2] = { ssrc: Math.floor(4294967295 * Math.random()) >>> 0, seq: 0, ts: 0 };
            try {
              const n2 = new MediaStreamTrackProcessor({ track: e2 });
              this._processors.push(n2);
              const s2 = n2.readable.getReader();
              let o2 = false;
              const r2 = () => {
                o2 = true, s2.cancel().catch((() => {
                })).finally((() => {
                }));
              };
              this._readerCancellers.push(r2);
              (() => i(this, void 0, void 0, (function* () {
                try {
                  for (; !o2 && !this._stopped; ) {
                    const { done: e3, value: i2 } = yield s2.read();
                    if (e3) break;
                    this._onFrame(t2, i2);
                  }
                } catch (e3) {
                  this._stopped || Le.warn("CallRecorder: reader loop error", { track: t2, error: e3 });
                }
              })))();
            } catch (e3) {
              Le.warn("CallRecorder: failed to attach track", { track: t2, error: e3 }), this._trackStates[t2] = null;
            }
          }
          _onFrame(e2, t2) {
            var i2;
            const n2 = this._trackStates[e2];
            if (!n2 || this._stopped) return;
            const s2 = t2.numberOfFrames, o2 = new Float32Array(s2);
            t2.copyTo(o2, { planeIndex: 0 }), null === (i2 = t2.close) || void 0 === i2 || i2.call(t2);
            const r2 = new Uint8Array(o2.buffer, o2.byteOffset, o2.byteLength);
            n2.seq = n2.seq + 1 & 65535;
            const a2 = n2.ts;
            n2.ts += s2;
            const c2 = { rtpSeq: n2.seq, rtpTs: a2, rtpSsrc: n2.ssrc, capturedAt: (/* @__PURE__ */ new Date()).toISOString(), payloadBytes: r2 };
            this._pushPacket(c2, e2);
          }
          _pushPacket(e2, t2) {
            const i2 = e2.payloadBytes.length + Li.PACKET_OVERHEAD_BYTES, n2 = this._maxBufferBytes(), s2 = this._buffers[t2];
            if (s2.push(e2), this._bufferBytes[t2] += i2, this._bufferBytes[t2] > n2) {
              for (; s2.length > 1 && this._bufferBytes[t2] > n2; ) {
                const e3 = s2.shift();
                e3 && (this._bufferBytes[t2] -= e3.payloadBytes.length + Li.PACKET_OVERHEAD_BYTES);
              }
              this._overflowWarnedThisWindow[t2] || (this._overflowWarnedThisWindow[t2] = true, Le.warn("CallRecorder: buffer overflow \u2014 oldest packets dropped", { track: t2 }), this._emitWarning(G));
            }
          }
          _flushIntervalMs() {
            var e2;
            const t2 = null !== (e2 = this.options.flushIntervalMs) && void 0 !== e2 ? e2 : 15e3;
            return t2 <= 0 ? t2 : Math.min(t2, this._maxSafeFlushIntervalMs());
          }
          _maxSafeFlushIntervalMs() {
            const e2 = this._sampleRate() * Li.BYTES_PER_SAMPLE + Li.FRAMES_PER_SECOND * Li.PACKET_OVERHEAD_BYTES;
            return Math.max(1e3, this._maxBufferBytes() / e2 * 500);
          }
          _maxBufferBytes() {
            var e2;
            return null !== (e2 = this.options.maxBufferBytes) && void 0 !== e2 ? e2 : ht;
          }
          _sampleRate() {
            var e2;
            return null !== (e2 = this.options.sampleRate) && void 0 !== e2 ? e2 : 48e3;
          }
          _scheduleFlush() {
            if (this._stopped || this._flushTimerId) return;
            const e2 = this._flushIntervalMs();
            e2 <= 0 || (this._flushTimerId = setInterval((() => {
              this._periodicFlush().catch(((e3) => {
                Le.error("CallRecorder: periodic flush error", { error: e3 });
              }));
            }), e2));
          }
          _clearFlushTimer() {
            this._flushTimerId && (clearInterval(this._flushTimerId), this._flushTimerId = null);
          }
          _cancelReaders() {
            for (const e2 of this._readerCancellers) try {
              e2();
            } catch (e3) {
            }
            this._readerCancellers = [], this._processors = [];
          }
          _periodicFlush() {
            return i(this, void 0, void 0, (function* () {
              if (!this._flushing && !this._stopped) {
                this._flushing = true;
                try {
                  const e2 = this._resolveEndpointFromContext();
                  if (!e2) return void Le.debug("CallRecorder: periodic flush skipped \u2014 no host available");
                  const t2 = this.options.tracks || ["local", "remote"], i2 = this._startedAt || /* @__PURE__ */ new Date(), n2 = /* @__PURE__ */ new Date();
                  yield Promise.allSettled(t2.map(((t3) => {
                    const s2 = this._drain(t3);
                    if (0 === s2.length) return Promise.resolve();
                    const o2 = this._buildEnvelope(s2, t3, "intermediate", i2, n2);
                    return this._postRecording(e2, o2, false).catch(((e3) => {
                      Le.error("CallRecorder: intermediate flush failed", { track: t3, error: e3 });
                    }));
                  }))), this._overflowWarnedThisWindow = { local: false, remote: false };
                } finally {
                  this._flushing = false;
                }
              }
            }));
          }
          _drain(e2) {
            const t2 = this._buffers[e2];
            return this._buffers[e2] = [], this._bufferBytes[e2] = 0, t2;
          }
          _resolveEndpointFromContext() {
            const e2 = this.callContext.host;
            return e2 ? this._resolveEndpoint(e2) : this._host ? this._resolveEndpoint(this._host) : null;
          }
          _setHost(e2) {
            this._host = e2;
          }
          _setCallReportId(e2) {
            e2 && (this._callReportId = e2);
          }
          _resolveCallReportId() {
            return this._callReportId || this.callContext.callReportId;
          }
          _resolveEndpoint(e2) {
            const t2 = this.options.endpoint || "/call_recording";
            try {
              const i2 = new URL(e2);
              return `${`${i2.protocol.replace(/^ws/, "http")}//${i2.host}`}${t2}`;
            } catch (i2) {
              return /^https?:\/\//.test(e2) ? `${e2}${t2}` : `https://${e2}${t2}`;
            }
          }
          _buildEnvelope(e2, t2, i2, n2, s2) {
            let o2 = 0;
            const r2 = e2.map(((e3) => (o2 += e3.payloadBytes.length, { rtp_seq: e3.rtpSeq, rtp_ts: e3.rtpTs, rtp_ssrc: e3.rtpSsrc, captured_at: e3.capturedAt, payload_b64: this._toBase64(e3.payloadBytes) })));
            return Object.assign(Object.assign({ call_report_id: this._resolveCallReportId(), call_id: this.callContext.callId }, this.callContext.voiceSdkId ? { voice_sdk_id: this.callContext.voiceSdkId } : {}), { recording_id: this.recordingId, segment: i2, track: t2, codec: "pcm-f32-le", sample_rate: this._sampleRate(), channels: 1, started_at: n2.toISOString(), ended_at: s2.toISOString(), packet_count: e2.length, byte_count: o2, packets: r2 });
          }
          _postRecording(e2, t2, n2) {
            return i(this, void 0, void 0, (function* () {
              const i2 = { "Content-Type": "application/json", "x-call-report-id": t2.call_report_id, "x-call-id": t2.call_id };
              t2.voice_sdk_id && (i2["x-voice-sdk-id"] = t2.voice_sdk_id);
              const s2 = JSON.stringify(t2), o2 = n2 && s2.length <= Li.KEEPALIVE_BODY_LIMIT_BYTES, r2 = this.options.fetchImpl || fetch;
              let a2;
              for (let n3 = 0; n3 <= Li.RETRY_DELAYS_MS.length; n3++) {
                try {
                  const c3 = yield r2(e2, Object.assign({ method: "POST", headers: i2, body: s2 }, o2 ? { keepalive: true } : {}));
                  if (c3.ok) return void Le.info("CallRecorder: posted", { track: t2.track, segment: t2.segment, attempt: n3 + 1, status: c3.status, packets: t2.packet_count });
                  {
                    const e3 = yield c3.text().catch((() => ""));
                    a2 = new Error(`Call recording POST failed with status ${c3.status}`), Le.error("CallRecorder: failed to post", { track: t2.track, segment: t2.segment, attempt: n3 + 1, status: c3.status, error: e3 });
                  }
                } catch (e3) {
                  a2 = e3, Le.warn("CallRecorder: network error posting", { track: t2.track, segment: t2.segment, attempt: n3 + 1, error: e3 });
                }
                const c2 = Li.RETRY_DELAYS_MS[n3];
                if (void 0 === c2) break;
                Le.info("CallRecorder: retrying", { track: t2.track, segment: t2.segment, inMs: c2, attempt: n3 + 2 }), yield new Promise(((e3) => setTimeout(e3, c2)));
              }
              throw Le.error("CallRecorder: exhausted retries", { track: t2.track, segment: t2.segment, error: a2 }), a2 instanceof Error ? a2 : new Error("Call recording POST failed after retries");
            }));
          }
          _emitWarning(e2) {
            var t2;
            try {
              const i2 = be(e2);
              null === (t2 = this.onWarning) || void 0 === t2 || t2.call(this, i2);
            } catch (t3) {
              Le.warn("CallRecorder: failed to emit warning", { code: e2, error: t3 });
            }
          }
          _toBase64(e2) {
            let t2 = "";
            for (let i2 = 0; i2 < e2.length; i2 += 32768) {
              const n2 = e2.subarray(i2, i2 + 32768);
              t2 += String.fromCharCode.apply(null, Array.from(n2));
            }
            return btoa(t2);
          }
        }
        Li.RETRY_DELAYS_MS = [500, 1e3, 2e3], Li.KEEPALIVE_BODY_LIMIT_BYTES = 61440, Li.PACKET_OVERHEAD_BYTES = 160, Li.BYTES_PER_SAMPLE = 4, Li.FRAMES_PER_SECOND = 100;
        class Mi {
          constructor() {
            this._rawDeviceCache = [], this._deviceChangeHandler = null, this._stopped = false;
          }
          logDevicesAtStart() {
            return i(this, void 0, void 0, (function* () {
              try {
                const e2 = yield this._enumerateAudioDevices();
                this._rawDeviceCache = e2, Le.debug("MediaDeviceCollector: devices at call start", { devices: e2 }), this._startDeviceChangeListener();
              } catch (e2) {
                Le.debug("MediaDeviceCollector: failed to log devices at start", { error: e2 });
              }
            }));
          }
          stop() {
            this._stopped = true, this._removeDeviceChangeListener();
          }
          _enumerateAudioDevices() {
            return i(this, void 0, void 0, (function* () {
              try {
                if ("undefined" != typeof navigator && navigator.mediaDevices && "function" == typeof navigator.mediaDevices.enumerateDevices) {
                  return (yield navigator.mediaDevices.enumerateDevices()).filter(((e2) => "audioinput" === e2.kind || "audiooutput" === e2.kind));
                }
              } catch (e2) {
                Le.debug("MediaDeviceCollector: enumerateDevices failed", { error: e2 });
              }
              return [];
            }));
          }
          _startDeviceChangeListener() {
            "undefined" != typeof navigator && navigator.mediaDevices && "function" == typeof navigator.mediaDevices.addEventListener && (this._deviceChangeHandler = () => {
              this._onDeviceChange();
            }, navigator.mediaDevices.addEventListener("devicechange", this._deviceChangeHandler));
          }
          _removeDeviceChangeListener() {
            this._deviceChangeHandler && "undefined" != typeof navigator && navigator.mediaDevices && "function" == typeof navigator.mediaDevices.removeEventListener && navigator.mediaDevices.removeEventListener("devicechange", this._deviceChangeHandler), this._deviceChangeHandler = null;
          }
          _onDeviceChange() {
            return i(this, void 0, void 0, (function* () {
              if (!this._stopped) try {
                const e2 = yield this._enumerateAudioDevices(), t2 = new Set(this._rawDeviceCache.map(((e3) => e3.deviceId))), i2 = new Set(e2.map(((e3) => e3.deviceId))), n2 = e2.filter(((e3) => !t2.has(e3.deviceId))), s2 = this._rawDeviceCache.filter(((e3) => !i2.has(e3.deviceId)));
                (n2.length > 0 || s2.length > 0) && Le.debug("MediaDeviceCollector: devices changed during call", { connected: n2, disconnected: s2 }), this._rawDeviceCache = e2;
              } catch (e2) {
                Le.debug("MediaDeviceCollector: error handling devicechange", { error: e2 });
              }
            }));
          }
        }
        var Pi, xi = Re((function(e2, t2) {
          var i2;
          function n2() {
          }
          function s2() {
            s2.init.call(this);
          }
          function o2(e3) {
            return void 0 === e3._maxListeners ? s2.defaultMaxListeners : e3._maxListeners;
          }
          function r2(e3, t3, i3, s3) {
            var r3, a3, c3;
            if ("function" != typeof i3) throw new TypeError('"listener" argument must be a function');
            if ((a3 = e3._events) ? (a3.newListener && (e3.emit("newListener", t3, i3.listener ? i3.listener : i3), a3 = e3._events), c3 = a3[t3]) : (a3 = e3._events = new n2(), e3._eventsCount = 0), c3) {
              if ("function" == typeof c3 ? c3 = a3[t3] = s3 ? [i3, c3] : [c3, i3] : s3 ? c3.unshift(i3) : c3.push(i3), !c3.warned && ((r3 = o2(e3)) && 0 < r3 && c3.length > r3)) {
                c3.warned = true;
                var l3 = new Error("Possible EventEmitter memory leak detected. " + c3.length + " " + t3 + " listeners added. Use emitter.setMaxListeners() to increase limit");
                l3.name = "MaxListenersExceededWarning", l3.emitter = e3, l3.type = t3, l3.count = c3.length, (function(e4) {
                  "function" == typeof console.warn ? console.warn(e4) : console.log(e4);
                })(l3);
              }
            } else c3 = a3[t3] = i3, ++e3._eventsCount;
            return e3;
          }
          function a2(e3, t3, i3) {
            function n3() {
              e3.removeListener(t3, n3), s3 || (s3 = true, i3.apply(e3, arguments));
            }
            var s3 = false;
            return n3.listener = i3, n3;
          }
          function c2(e3) {
            var t3 = this._events;
            if (t3) {
              var i3 = t3[e3];
              if ("function" == typeof i3) return 1;
              if (i3) return i3.length;
            }
            return 0;
          }
          function l2(e3, t3) {
            for (var i3 = Array(t3); t3--; ) i3[t3] = e3[t3];
            return i3;
          }
          Object.defineProperty(t2, "__esModule", { value: true }), n2.prototype = /* @__PURE__ */ Object.create(null), s2.EventEmitter = s2, s2.usingDomains = false, s2.prototype.domain = void 0, s2.prototype._events = void 0, s2.prototype._maxListeners = void 0, s2.defaultMaxListeners = 10, s2.init = function() {
            this.domain = null, s2.usingDomains && i2.active && !(this instanceof i2.Domain) && (this.domain = i2.active), this._events && this._events !== Object.getPrototypeOf(this)._events || (this._events = new n2(), this._eventsCount = 0), this._maxListeners = this._maxListeners || void 0;
          }, s2.prototype.setMaxListeners = function(e3) {
            if ("number" != typeof e3 || 0 > e3 || isNaN(e3)) throw new TypeError('"n" argument must be a positive number');
            return this._maxListeners = e3, this;
          }, s2.prototype.getMaxListeners = function() {
            return o2(this);
          }, s2.prototype.emit = function(e3) {
            var t3, i3, n3, s3, o3, r3, a3, c3 = "error" === e3;
            if (r3 = this._events) c3 = c3 && null == r3.error;
            else if (!c3) return false;
            if (a3 = this.domain, c3) {
              if (t3 = arguments[1], !a3) {
                if (t3 instanceof Error) throw t3;
                var d3 = new Error('Uncaught, unspecified "error" event. (' + t3 + ")");
                throw d3.context = t3, d3;
              }
              return t3 || (t3 = new Error('Uncaught, unspecified "error" event')), t3.domainEmitter = this, t3.domain = a3, t3.domainThrown = false, a3.emit("error", t3), false;
            }
            if (!(i3 = r3[e3])) return false;
            var u3 = "function" == typeof i3;
            switch (n3 = arguments.length) {
              case 1:
                !(function(e4, t4, i4) {
                  if (t4) e4.call(i4);
                  else for (var n4 = e4.length, s4 = l2(e4, n4), o4 = 0; o4 < n4; ++o4) s4[o4].call(i4);
                })(i3, u3, this);
                break;
              case 2:
                !(function(e4, t4, i4, n4) {
                  if (t4) e4.call(i4, n4);
                  else for (var s4 = e4.length, o4 = l2(e4, s4), r4 = 0; r4 < s4; ++r4) o4[r4].call(i4, n4);
                })(i3, u3, this, arguments[1]);
                break;
              case 3:
                !(function(e4, t4, i4, n4, s4) {
                  if (t4) e4.call(i4, n4, s4);
                  else for (var o4 = e4.length, r4 = l2(e4, o4), a4 = 0; a4 < o4; ++a4) r4[a4].call(i4, n4, s4);
                })(i3, u3, this, arguments[1], arguments[2]);
                break;
              case 4:
                !(function(e4, t4, i4, n4, s4, o4) {
                  if (t4) e4.call(i4, n4, s4, o4);
                  else for (var r4 = e4.length, a4 = l2(e4, r4), c4 = 0; c4 < r4; ++c4) a4[c4].call(i4, n4, s4, o4);
                })(i3, u3, this, arguments[1], arguments[2], arguments[3]);
                break;
              default:
                for (s3 = Array(n3 - 1), o3 = 1; o3 < n3; o3++) s3[o3 - 1] = arguments[o3];
                !(function(e4, t4, i4, n4) {
                  if (t4) e4.apply(i4, n4);
                  else for (var s4 = e4.length, o4 = l2(e4, s4), r4 = 0; r4 < s4; ++r4) o4[r4].apply(i4, n4);
                })(i3, u3, this, s3);
            }
            return true;
          }, s2.prototype.addListener = function(e3, t3) {
            return r2(this, e3, t3, false);
          }, s2.prototype.on = s2.prototype.addListener, s2.prototype.prependListener = function(e3, t3) {
            return r2(this, e3, t3, true);
          }, s2.prototype.once = function(e3, t3) {
            if ("function" != typeof t3) throw new TypeError('"listener" argument must be a function');
            return this.on(e3, a2(this, e3, t3)), this;
          }, s2.prototype.prependOnceListener = function(e3, t3) {
            if ("function" != typeof t3) throw new TypeError('"listener" argument must be a function');
            return this.prependListener(e3, a2(this, e3, t3)), this;
          }, s2.prototype.removeListener = function(e3, t3) {
            var i3, s3, o3, r3, a3;
            if ("function" != typeof t3) throw new TypeError('"listener" argument must be a function');
            if (!(s3 = this._events)) return this;
            if (!(i3 = s3[e3])) return this;
            if (i3 === t3 || i3.listener && i3.listener === t3) 0 == --this._eventsCount ? this._events = new n2() : (delete s3[e3], s3.removeListener && this.emit("removeListener", e3, i3.listener || t3));
            else if ("function" != typeof i3) {
              for (o3 = -1, r3 = i3.length; 0 < r3--; ) if (i3[r3] === t3 || i3[r3].listener && i3[r3].listener === t3) {
                a3 = i3[r3].listener, o3 = r3;
                break;
              }
              if (0 > o3) return this;
              if (1 === i3.length) {
                if (i3[0] = void 0, 0 == --this._eventsCount) return this._events = new n2(), this;
                delete s3[e3];
              } else !(function(e4, t4) {
                for (var i4 = t4, n3 = i4 + 1, s4 = e4.length; n3 < s4; i4 += 1, n3 += 1) e4[i4] = e4[n3];
                e4.pop();
              })(i3, o3);
              s3.removeListener && this.emit("removeListener", e3, a3 || t3);
            }
            return this;
          }, s2.prototype.removeAllListeners = function(e3) {
            var t3, i3;
            if (!(i3 = this._events)) return this;
            if (!i3.removeListener) return 0 === arguments.length ? (this._events = new n2(), this._eventsCount = 0) : i3[e3] && (0 == --this._eventsCount ? this._events = new n2() : delete i3[e3]), this;
            if (0 === arguments.length) {
              for (var s3, o3 = Object.keys(i3), r3 = 0; r3 < o3.length; ++r3) "removeListener" !== (s3 = o3[r3]) && this.removeAllListeners(s3);
              return this.removeAllListeners("removeListener"), this._events = new n2(), this._eventsCount = 0, this;
            }
            if ("function" == typeof (t3 = i3[e3])) this.removeListener(e3, t3);
            else if (t3) do {
              this.removeListener(e3, t3[t3.length - 1]);
            } while (t3[0]);
            return this;
          }, s2.prototype.listeners = function(e3) {
            var t3, i3, n3 = this._events;
            return n3 ? i3 = (t3 = n3[e3]) ? "function" == typeof t3 ? [t3.listener || t3] : (function(e4) {
              for (var t4 = Array(e4.length), i4 = 0; i4 < t4.length; ++i4) t4[i4] = e4[i4].listener || e4[i4];
              return t4;
            })(t3) : [] : i3 = [], i3;
          }, s2.listenerCount = function(e3, t3) {
            return "function" == typeof e3.listenerCount ? e3.listenerCount(t3) : c2.call(e3, t3);
          }, s2.prototype.listenerCount = c2, s2.prototype.eventNames = function() {
            return 0 < this._eventsCount ? Reflect.ownKeys(this._events) : [];
          };
          var d2, u2 = new Uint8Array(16);
          function h2() {
            if (!d2 && !(d2 = "undefined" != typeof crypto && crypto.getRandomValues && crypto.getRandomValues.bind(crypto) || "undefined" != typeof msCrypto && "function" == typeof msCrypto.getRandomValues && msCrypto.getRandomValues.bind(msCrypto))) throw new Error("crypto.getRandomValues() not supported. See https://github.com/uuidjs/uuid#getrandomvalues-not-supported");
            return d2(u2);
          }
          var p2 = /^(?:[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}|00000000-0000-0000-0000-000000000000)$/i;
          for (var g2 = [], v2 = 0; 256 > v2; ++v2) g2.push((v2 + 256).toString(16).substr(1));
          function m2(e3) {
            var t3 = 1 < arguments.length && void 0 !== arguments[1] ? arguments[1] : 0, i3 = (g2[e3[t3 + 0]] + g2[e3[t3 + 1]] + g2[e3[t3 + 2]] + g2[e3[t3 + 3]] + "-" + g2[e3[t3 + 4]] + g2[e3[t3 + 5]] + "-" + g2[e3[t3 + 6]] + g2[e3[t3 + 7]] + "-" + g2[e3[t3 + 8]] + g2[e3[t3 + 9]] + "-" + g2[e3[t3 + 10]] + g2[e3[t3 + 11]] + g2[e3[t3 + 12]] + g2[e3[t3 + 13]] + g2[e3[t3 + 14]] + g2[e3[t3 + 15]]).toLowerCase();
            if (!(function(e4) {
              return "string" == typeof e4 && p2.test(e4);
            })(i3)) throw TypeError("Stringified UUID is invalid");
            return i3;
          }
          function f2(e3, t3, i3) {
            var n3 = (e3 = e3 || {}).random || (e3.rng || h2)();
            if (n3[6] = 64 | 15 & n3[6], n3[8] = 128 | 63 & n3[8], t3) {
              i3 = i3 || 0;
              for (var s3 = 0; 16 > s3; ++s3) t3[i3 + s3] = n3[s3];
              return t3;
            }
            return m2(n3);
          }
          function _2(e3, t3) {
            if (!e3 || !t3) return {};
            const i3 = { ...e3 };
            if (i3.localCandidateId) {
              const e4 = t3.get(i3.localCandidateId);
              i3.local = { ...e4 };
            }
            if (i3.remoteCandidateId) {
              const e4 = t3.get(i3.remoteCandidateId);
              i3.remote = { ...e4 };
            }
            return i3;
          }
          function S2(e3, t3, i3) {
            return 8 * (function(e4, t4, i4) {
              const n3 = e4[i4], s3 = t4 ? t4[i4] : null;
              return null === n3 || null === s3 ? null : (n3 - s3) / (e4.timestamp - t4.timestamp) * 1e3;
            })(e3, t3, i3);
          }
          function y2(e3) {
            if (!e3.entries) return e3;
            const t3 = {};
            return e3.forEach((function(e4, i3) {
              t3[i3] = e4;
            })), t3;
          }
          function b2(e3, t3, i3 = {}) {
            if (!e3) return null;
            let n3 = { audio: { inbound: [], outbound: [] }, video: { inbound: [], outbound: [] }, connection: { inbound: [], outbound: [] } };
            i3.remote && (n3.remote = { audio: { inbound: [], outbound: [] }, video: { inbound: [], outbound: [] } });
            for (const t4 of e3.values()) switch (t4.type) {
              case "outbound-rtp": {
                const i4 = t4.mediaType || t4.kind, s3 = {};
                let o3 = {};
                if (!["audio", "video"].includes(i4)) continue;
                if (t4.codecId) {
                  const i5 = e3.get(t4.codecId);
                  i5 && (s3.clockRate = i5.clockRate, s3.mimeType = i5.mimeType, s3.payloadType = i5.payloadType);
                }
                o3 = e3.get(t4.mediaSourceId) || e3.get(t4.trackId) || {}, n3[i4].outbound.push({ ...t4, ...s3, track: { ...o3 } });
                break;
              }
              case "inbound-rtp": {
                let i4 = t4.mediaType || t4.kind, s3 = {};
                const o3 = {};
                if (!["audio", "video"].includes(i4)) if (t4.id.includes("Video")) i4 = "video";
                else {
                  if (!t4.id.includes("Audio")) continue;
                  i4 = "audio";
                }
                if (t4.codecId) {
                  const i5 = e3.get(t4.codecId);
                  i5 && (o3.clockRate = i5.clockRate, o3.mimeType = i5.mimeType, o3.payloadType = i5.payloadType);
                }
                if (!n3.connection.id && t4.transportId) {
                  const i5 = e3.get(t4.transportId);
                  if (i5 && i5.selectedCandidatePairId) {
                    const t5 = e3.get(i5.selectedCandidatePairId);
                    n3.connection = _2(t5, e3);
                  }
                }
                s3 = e3.get(t4.mediaSourceId) || e3.get(t4.trackId) || {}, n3[i4].inbound.push({ ...t4, ...o3, track: { ...s3 } });
                break;
              }
              case "peer-connection":
                n3.connection.dataChannelsClosed = t4.dataChannelsClosed, n3.connection.dataChannelsOpened = t4.dataChannelsOpened;
                break;
              case "remote-inbound-rtp": {
                if (!i3.remote) break;
                let s3 = t4.mediaType || t4.kind;
                const o3 = {};
                if (!["audio", "video"].includes(s3)) if (t4.id.includes("Video")) s3 = "video";
                else {
                  if (!t4.id.includes("Audio")) continue;
                  s3 = "audio";
                }
                if (t4.codecId) {
                  const i4 = e3.get(t4.codecId);
                  i4 && (o3.clockRate = i4.clockRate, o3.mimeType = i4.mimeType, o3.payloadType = i4.payloadType);
                }
                if (!n3.connection.id && t4.transportId) {
                  const i4 = e3.get(t4.transportId);
                  if (i4 && i4.selectedCandidatePairId) {
                    const t5 = e3.get(i4.selectedCandidatePairId);
                    n3.connection = _2(t5, e3);
                  }
                }
                n3.remote[s3].inbound.push({ ...t4, ...o3 });
                break;
              }
              case "remote-outbound-rtp": {
                if (!i3.remote) break;
                const s3 = t4.mediaType || t4.kind, o3 = {};
                if (!["audio", "video"].includes(s3)) continue;
                if (t4.codecId) {
                  const i4 = e3.get(t4.codecId);
                  i4 && (o3.clockRate = i4.clockRate, o3.mimeType = i4.mimeType, o3.payloadType = i4.payloadType);
                }
                n3.remote[s3].outbound.push({ ...t4, ...o3 });
                break;
              }
            }
            if (!n3.connection.id) for (const t4 of e3.values()) "candidate-pair" === t4.type && t4.nominated && "succeeded" === t4.state && (n3.connection = _2(t4, e3));
            return n3 = (function(e4, t4) {
              return t4 ? (e4.audio.inbound.map(((e5) => {
                let i4 = t4.audio.inbound.find(((t5) => t5.id === e5.id));
                e5.bitrate = S2(e5, i4, "bytesReceived"), e5.packetRate = S2(e5, i4, "packetsReceived");
              })), e4.audio.outbound.map(((e5) => {
                let i4 = t4.audio.outbound.find(((t5) => t5.id === e5.id));
                e5.bitrate = S2(e5, i4, "bytesSent"), e5.packetRate = S2(e5, i4, "packetsSent");
              })), e4.video.inbound.map(((e5) => {
                let i4 = t4.video.inbound.find(((t5) => t5.id === e5.id));
                e5.bitrate = S2(e5, i4, "bytesReceived"), e5.packetRate = S2(e5, i4, "packetsReceived");
              })), e4.video.outbound.map(((e5) => {
                let i4 = t4.video.outbound.find(((t5) => t5.id === e5.id));
                e5.bitrate = S2(e5, i4, "bytesSent"), e5.packetRate = S2(e5, i4, "packetsSent");
              })), e4) : e4;
            })(n3, t3), n3;
          }
          let I2, E2 = {}, C2 = [];
          t2.WebRTCStats = class extends s2 {
            constructor(e3) {
              if (super(), this.monitoringSetInterval = 0, this.connectionMonitoringSetInterval = 0, this.connectionMonitoringInterval = 1e3, this.remote = true, this.peersToMonitor = {}, this.timeline = [], this.statsToMonitor = ["inbound-rtp", "outbound-rtp", "remote-inbound-rtp", "remote-outbound-rtp", "peer-connection", "data-channel", "stream", "track", "sender", "receiver", "transport", "candidate-pair", "local-candidate", "remote-candidate"], "undefined" == typeof window) throw new Error("WebRTCStats only works in browser");
              const t3 = { ...e3 };
              this.isEdge = !!window.RTCIceGatherer, this.getStatsInterval = t3.getStatsInterval || 1e3, this.rawStats = !!t3.rawStats, this.statsObject = !!t3.statsObject, this.filteredStats = !!t3.filteredStats, this.shouldWrapGetUserMedia = !!t3.wrapGetUserMedia, "boolean" == typeof t3.remote && (this.remote = t3.remote), this.debug = !!t3.debug, this.logLevel = t3.logLevel || "none", this.shouldWrapGetUserMedia && this.wrapGetUserMedia();
            }
            async addPeer(e3, t3) {
              return console.warn("The addPeer() method has been deprecated, please use addConnection()"), this.addConnection({ peerId: e3, pc: t3 });
            }
            async addConnection(e3) {
              const { pc: t3, peerId: i3 } = e3;
              let { connectionId: n3, remote: s3 } = e3;
              if (s3 = "boolean" == typeof s3 ? s3 : this.remote, !(t3 && t3 instanceof RTCPeerConnection)) throw new Error("Missing argument 'pc' or is not of instance RTCPeerConnection");
              if (!i3) throw new Error("Missing argument peerId");
              if (this.isEdge) throw new Error("Can't monitor peers in Edge at this time.");
              if (this.peersToMonitor[i3]) {
                if (n3 && n3 in this.peersToMonitor[i3]) throw new Error(`We are already monitoring connection with id ${n3}.`);
                for (let e4 in this.peersToMonitor[i3]) {
                  const n4 = this.peersToMonitor[i3][e4];
                  if (n4.pc === t3) throw new Error(`We are already monitoring peer with id ${i3}.`);
                  "closed" === n4.pc.connectionState && this.removeConnection({ pc: n4.pc });
                }
              }
              const o3 = t3.getConfiguration();
              return o3.iceServers && o3.iceServers.forEach((function(e4) {
                delete e4.credential;
              })), n3 || (n3 = f2()), this.emitEvent({ event: "addConnection", tag: "peer", peerId: i3, connectionId: n3, data: { options: e3, peerConfiguration: o3 } }), this.monitorPeer({ peerId: i3, connectionId: n3, pc: t3, remote: s3 }), { connectionId: n3 };
            }
            getTimeline(e3) {
              return this.timeline = this.timeline.sort(((e4, t3) => e4.timestamp.getTime() - t3.timestamp.getTime())), e3 ? this.timeline.filter(((t3) => t3.tag === e3)) : this.timeline;
            }
            get logger() {
              const e3 = (e4) => {
                const t3 = ["none", "error", "warn", "info", "debug"];
                return t3.slice(0, t3.indexOf(this.logLevel) + 1).indexOf(e4) > -1;
              };
              return { error(...t3) {
                this.debug && e3("error") && console.error("[webrtc-stats][error] ", ...t3);
              }, warn(...t3) {
                this.debug && e3("warn") && console.warn("[webrtc-stats][warn] ", ...t3);
              }, info(...t3) {
                this.debug && e3("info") && console.log("[webrtc-stats][info] ", ...t3);
              }, debug(...t3) {
                this.debug && e3("debug") && console.debug("[webrtc-stats][debug] ", ...t3);
              } };
            }
            removeConnection(e3) {
              let t3, { connectionId: i3, pc: n3 } = e3;
              if (!n3 && !i3) throw new Error("Missing arguments. You need to either send pc or a connectionId.");
              if (i3) {
                if ("string" != typeof i3) throw new Error("connectionId must be a string.");
                for (let e4 in this.peersToMonitor) i3 in this.peersToMonitor[e4] && (n3 = this.peersToMonitor[e4][i3].pc, t3 = e4);
              } else if (n3) {
                if (!(n3 instanceof RTCPeerConnection)) throw new Error("pc must be an instance of RTCPeerConnection.");
                for (let e4 in this.peersToMonitor) for (let s3 in this.peersToMonitor[e4]) this.peersToMonitor[e4][s3].pc === n3 && (i3 = s3, t3 = e4);
              }
              if (!n3 || !i3) throw new Error("Could not find the desired connection.");
              return this.removePeerConnectionEventListeners(i3, n3), delete this.peersToMonitor[t3][i3], 0 === Object.values(this.peersToMonitor[t3]).length && delete this.peersToMonitor[t3], { connectionId: i3 };
            }
            removeAllPeers() {
              for (let e3 in this.peersToMonitor) this.removePeer(e3);
            }
            removePeer(e3) {
              if (this.logger.info(`Removing PeerConnection with id ${e3}.`), this.peersToMonitor[e3]) {
                for (let t3 in this.peersToMonitor[e3]) {
                  let i3 = this.peersToMonitor[e3][t3].pc;
                  this.removePeerConnectionEventListeners(t3, i3);
                }
                delete this.peersToMonitor[e3];
              }
            }
            destroy() {
              this.removeAllPeers(), C2.forEach(((e3) => {
                this.removeTrackEventListeners(e3);
              })), C2 = [], this.shouldWrapGetUserMedia && I2 && (navigator.mediaDevices.getUserMedia = I2);
            }
            monitorPeer(e3) {
              let { peerId: t3, connectionId: i3, pc: n3, remote: s3 } = e3;
              if (!n3) return void this.logger.warn("Did not receive pc argument when calling monitorPeer()");
              const o3 = { pc: n3, connectionId: i3, stream: null, stats: { parsed: null, raw: null }, options: { remote: s3 } };
              if (this.peersToMonitor[t3]) {
                if (i3 in this.peersToMonitor[t3]) return void this.logger.warn(`Already watching connection with ID ${i3}`);
                this.peersToMonitor[t3][i3] = o3;
              } else this.peersToMonitor[t3] = { [i3]: o3 };
              this.addPeerConnectionEventListeners(t3, i3, n3), 1 === this.numberOfMonitoredPeers && (this.startStatsMonitoring(), this.startConnectionStateMonitoring());
            }
            startStatsMonitoring() {
              this.monitoringSetInterval || (this.monitoringSetInterval = window.setInterval((() => {
                this.numberOfMonitoredPeers || this.stopStatsMonitoring(), this.getStats().then(((e3) => {
                  e3.forEach(((e4) => {
                    this.emitEvent(e4);
                  }));
                }));
              }), this._getStatsInterval));
            }
            stopStatsMonitoring() {
              this.monitoringSetInterval && (window.clearInterval(this.monitoringSetInterval), this.monitoringSetInterval = 0);
            }
            async getStats(e3 = null) {
              this.logger.info(e3 ? `Getting stats from peer ${e3}` : "Getting stats from all peers");
              let t3 = {};
              if (e3) {
                if (!this.peersToMonitor[e3]) throw new Error(`Cannot get stats. Peer with id ${e3} does not exist`);
                t3[e3] = this.peersToMonitor[e3];
              } else t3 = this.peersToMonitor;
              let i3 = [];
              for (const e4 in t3) for (const n3 in t3[e4]) {
                const s3 = t3[e4][n3], o3 = s3.pc;
                if (o3 && !this.checkIfConnectionIsClosed(e4, n3, o3)) try {
                  const t4 = this.getTimestamp(), r3 = o3.getStats(null);
                  if (r3) {
                    const o4 = await r3, a3 = this.getTimestamp(), c3 = y2(o4), l3 = { remote: s3.options.remote }, d3 = b2(o4, s3.stats.parsed, l3), u3 = { event: "stats", tag: "stats", peerId: e4, connectionId: n3, timeTaken: a3 - t4, data: d3 };
                    true === this.rawStats && (u3.rawStats = o4), true === this.statsObject && (u3.statsObject = c3), true === this.filteredStats && (u3.filteredStats = this.filteroutStats(c3)), i3.push(u3), s3.stats.parsed = d3;
                  } else this.logger.error(`PeerConnection from peer ${e4} did not return any stats data`);
                } catch (e5) {
                  this.logger.error(e5);
                }
              }
              return i3;
            }
            startConnectionStateMonitoring() {
              this.connectionMonitoringSetInterval = window.setInterval((() => {
                this.numberOfMonitoredPeers || this.stopConnectionStateMonitoring();
                for (const e3 in this.peersToMonitor) for (const t3 in this.peersToMonitor[e3]) {
                  const i3 = this.peersToMonitor[e3][t3].pc;
                  this.checkIfConnectionIsClosed(e3, t3, i3);
                }
              }), this.connectionMonitoringInterval);
            }
            checkIfConnectionIsClosed(e3, t3, i3) {
              const n3 = this.isConnectionClosed(i3);
              if (n3) {
                this.removeConnection({ pc: i3 });
                let n4 = "closed" === i3.connectionState ? "onconnectionstatechange" : "oniceconnectionstatechange";
                this.emitEvent({ event: n4, peerId: e3, connectionId: t3, tag: "connection", data: "closed" });
              }
              return n3;
            }
            isConnectionClosed(e3) {
              return "closed" === e3.connectionState || "closed" === e3.iceConnectionState;
            }
            stopConnectionStateMonitoring() {
              this.connectionMonitoringSetInterval && (window.clearInterval(this.connectionMonitoringSetInterval), this.connectionMonitoringSetInterval = 0);
            }
            wrapGetUserMedia() {
              if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) return void this.logger.warn("'navigator.mediaDevices.getUserMedia' is not available in browser. Will not wrap getUserMedia.");
              this.logger.info("Wrapping getUsermedia functions."), I2 = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
              const e3 = this.parseGetUserMedia.bind(this);
              navigator.mediaDevices.getUserMedia = function() {
                return e3({ constraints: arguments[0] }), I2.apply(navigator.mediaDevices, arguments).then(((t3) => (e3({ stream: t3 }), t3)), ((t3) => (e3({ error: t3 }), Promise.reject(t3))));
              }.bind(navigator.mediaDevices);
            }
            filteroutStats(e3 = {}) {
              const t3 = { ...e3 };
              for (const e4 in t3) {
                var i3 = t3[e4];
                this.statsToMonitor.includes(i3.type) || delete t3[e4];
              }
              return t3;
            }
            get peerConnectionListeners() {
              return { icecandidate: (e3, t3, i3, n3) => {
                this.logger.debug("[pc-event] icecandidate | peerId: ${peerId}", n3), this.emitEvent({ event: "onicecandidate", tag: "connection", peerId: e3, connectionId: t3, data: n3.candidate });
              }, track: (e3, t3, i3, n3) => {
                this.logger.debug(`[pc-event] track | peerId: ${e3}`, n3);
                const s3 = n3.track, o3 = n3.streams[0];
                e3 in this.peersToMonitor && t3 in this.peersToMonitor[e3] && (this.peersToMonitor[e3][t3].stream = o3), this.addTrackEventListeners(s3, t3), this.emitEvent({ event: "ontrack", tag: "track", peerId: e3, connectionId: t3, data: { stream: o3 ? this.getStreamDetails(o3) : null, track: s3 ? this.getMediaTrackDetails(s3) : null, title: n3.track.kind + ":" + n3.track.id + " " + n3.streams.map((function(e4) {
                  return "stream:" + e4.id;
                })) } });
              }, signalingstatechange: (e3, t3, i3) => {
                this.logger.debug(`[pc-event] signalingstatechange | peerId: ${e3}`), this.emitEvent({ event: "onsignalingstatechange", tag: "connection", peerId: e3, connectionId: t3, data: { signalingState: i3.signalingState, localDescription: i3.localDescription, remoteDescription: i3.remoteDescription } });
              }, iceconnectionstatechange: (e3, t3, i3) => {
                this.logger.debug(`[pc-event] iceconnectionstatechange | peerId: ${e3}`), this.emitEvent({ event: "oniceconnectionstatechange", tag: "connection", peerId: e3, connectionId: t3, data: i3.iceConnectionState });
              }, icegatheringstatechange: (e3, t3, i3) => {
                this.logger.debug(`[pc-event] icegatheringstatechange | peerId: ${e3}`), this.emitEvent({ event: "onicegatheringstatechange", tag: "connection", peerId: e3, connectionId: t3, data: i3.iceGatheringState });
              }, icecandidateerror: (e3, t3, i3, n3) => {
                this.logger.debug(`[pc-event] icecandidateerror | peerId: ${e3}`), this.emitEvent({ event: "onicecandidateerror", tag: "connection", peerId: e3, connectionId: t3, error: { errorCode: n3.errorCode } });
              }, connectionstatechange: (e3, t3, i3) => {
                this.logger.debug(`[pc-event] connectionstatechange | peerId: ${e3}`), this.emitEvent({ event: "onconnectionstatechange", tag: "connection", peerId: e3, connectionId: t3, data: i3.connectionState });
              }, negotiationneeded: (e3, t3, i3) => {
                this.logger.debug(`[pc-event] negotiationneeded | peerId: ${e3}`), this.emitEvent({ event: "onnegotiationneeded", tag: "connection", peerId: e3, connectionId: t3 });
              }, datachannel: (e3, t3, i3, n3) => {
                this.logger.debug(`[pc-event] datachannel | peerId: ${e3}`, n3), this.emitEvent({ event: "ondatachannel", tag: "datachannel", peerId: e3, connectionId: t3, data: n3.channel });
              } };
            }
            addPeerConnectionEventListeners(e3, t3, i3) {
              this.logger.debug(`Adding event listeners for peer ${e3} and connection ${t3}.`), E2[t3] = {}, Object.keys(this.peerConnectionListeners).forEach(((n3) => {
                E2[t3][n3] = this.peerConnectionListeners[n3].bind(this, e3, t3, i3), i3.addEventListener(n3, E2[t3][n3], false);
              }));
            }
            parseGetUserMedia(e3) {
              try {
                const t3 = { event: "getUserMedia", tag: "getUserMedia", data: { ...e3 } };
                e3.stream && (t3.data.details = this.parseStream(e3.stream), e3.stream.getTracks().map(((e4) => {
                  this.addTrackEventListeners(e4), C2.push(e4);
                }))), this.emitEvent(t3);
              } catch (e4) {
              }
            }
            parseStream(e3) {
              const t3 = { audio: [], video: [] };
              return e3.getTracks().forEach(((e4) => {
                t3[e4.kind].push(this.getMediaTrackDetails(e4));
              })), t3;
            }
            getMediaTrackDetails(e3) {
              return { enabled: e3.enabled, id: e3.id, contentHint: e3.contentHint, kind: e3.kind, label: e3.label, muted: e3.muted, readyState: e3.readyState, constructorName: e3.constructor.name, capabilities: e3.getCapabilities ? e3.getCapabilities() : {}, constraints: e3.getConstraints ? e3.getConstraints() : {}, settings: e3.getSettings ? e3.getSettings() : {}, _track: e3 };
            }
            getStreamDetails(e3) {
              return { active: e3.active, id: e3.id, _stream: e3 };
            }
            getTrackEventObject(e3) {
              return { mute: (t3) => {
                this.emitEvent({ event: "mute", tag: "track", connectionId: e3, data: { event: t3 } });
              }, unmute: (t3) => {
                this.emitEvent({ event: "unmute", tag: "track", connectionId: e3, data: { event: t3 } });
              }, overconstrained: (t3) => {
                this.emitEvent({ event: "overconstrained", tag: "track", connectionId: e3, data: { event: t3 } });
              }, ended: (t3) => {
                this.emitEvent({ event: "ended", tag: "track", connectionId: e3, data: { event: t3 } }), this.removeTrackEventListeners(t3.target);
              } };
            }
            addTrackEventListeners(e3, t3) {
              E2[e3.id] = {};
              const i3 = this.getTrackEventObject(t3);
              Object.keys(i3).forEach(((t4) => {
                E2[e3.id][t4] = i3[t4].bind(this), e3.addEventListener(t4, E2[e3.id][t4]);
              })), E2[e3.id].readyState = setInterval((() => {
                if ("ended" === e3.readyState) {
                  let t4 = new CustomEvent("ended", { detail: { check: "readyState" } });
                  e3.dispatchEvent(t4);
                }
              }), 1e3);
            }
            removeTrackEventListeners(e3) {
              if (e3.id in E2) {
                const t3 = this.getTrackEventObject();
                Object.keys(t3).forEach(((t4) => {
                  e3.removeEventListener(t4, E2[e3.id][t4]);
                })), clearInterval(E2[e3.id].readyState), delete E2[e3.id];
              }
            }
            addToTimeline(e3) {
              this.timeline.push(e3), this.emit("timeline", e3);
            }
            emitEvent(e3) {
              const t3 = { ...e3, timestamp: /* @__PURE__ */ new Date() };
              this.addToTimeline(t3), t3.tag && this.emit(t3.tag, t3);
            }
            set getStatsInterval(e3) {
              if (!Number.isInteger(e3)) throw new Error(`getStatsInterval should be an integer, got: ${e3}`);
              this._getStatsInterval = e3, this.monitoringSetInterval && (this.stopStatsMonitoring(), this.startStatsMonitoring());
            }
            get numberOfMonitoredPeers() {
              return Object.keys(this.peersToMonitor).length;
            }
            removePeerConnectionEventListeners(e3, t3) {
              e3 in E2 && (Object.keys(this.peerConnectionListeners).forEach(((i3) => {
                t3.removeEventListener(i3, E2[e3][i3], false);
              })), delete E2[e3]), t3.getSenders().forEach(((e4) => {
                e4.track && this.removeTrackEventListeners(e4.track);
              })), t3.getReceivers().forEach(((e4) => {
                e4.track && this.removeTrackEventListeners(e4.track);
              }));
            }
            getTimestamp() {
              return Date.now();
            }
            wrapGetDisplayMedia() {
              const e3 = this;
              if (navigator.mediaDevices && navigator.mediaDevices.getDisplayMedia) {
                const t3 = navigator.mediaDevices.getDisplayMedia.bind(navigator.mediaDevices), i3 = function() {
                  return e3.debug("navigator.mediaDevices.getDisplayMedia", null, arguments[0]), t3.apply(navigator.mediaDevices, arguments).then((function(e4) {
                    return e4;
                  }), (function(t4) {
                    return e3.debug("navigator.mediaDevices.getDisplayMediaOnFailure", null, t4.name), Promise.reject(t4);
                  }));
                };
                navigator.mediaDevices.getDisplayMedia = i3.bind(navigator.mediaDevices);
              }
            }
          };
        }));
        (Pi = xi) && Pi.__esModule && Object.prototype.hasOwnProperty.call(Pi, "default") && Pi.default;
        var Ui = xi.WebRTCStats;
        function Fi(e2) {
          const { packetsLost: t2, packetsReceived: i2, jitter: n2, rtt: s2 } = e2, o2 = (function(e3) {
            const { jitter: t3, rtt: i3 } = e3, n3 = t3 + i3 / 2;
            return 0.024 * n3 + 0.11 * (n3 - 177.3) * (n3 > 177.3 ? 1 : 0);
          })({ rtt: s2, jitter: n2 }), r2 = (function(e3) {
            const { packetsLost: t3, packetsReceived: i3 } = e3, n3 = t3 / (i3 + t3) * 100;
            return 20 * Math.log(1 + n3);
          })({ packetsLost: t2, packetsReceived: i2 }), a2 = 93.2 - o2 - r2 + 0, c2 = 1 + 0.035 * a2 + 7e-6 * a2 * (a2 - 60) * (100 - a2);
          return Math.min(Math.max(c2, 1), 5);
        }
        function $i(e2) {
          return isNaN(e2) ? null : e2 > 4.2 ? "excellent" : e2 >= 4.1 && e2 <= 4.2 ? "good" : e2 >= 3.7 && e2 <= 4 ? "fair" : e2 >= 3.1 && e2 <= 3.6 ? "poor" : "bad";
        }
        class Bi extends Pt {
          constructor(e2, t2) {
            super(), this.buildRequest({ type: "debug_report_start", debug_report_id: e2, debug_report_version: 1, call_id: t2 });
          }
        }
        class ji extends Pt {
          constructor(e2, t2) {
            super(), this.buildRequest({ type: "debug_report_stop", debug_report_id: e2, debug_report_version: 1, call_id: t2 });
          }
        }
        class Hi extends Pt {
          constructor(e2, t2, i2) {
            super(), this.buildRequest({ type: "debug_report_data", debug_report_id: e2, debug_report_version: 1, call_id: t2, debug_report_data: i2 });
          }
        }
        function Wi(t2, n2) {
          const s2 = c();
          let o2 = false;
          const r2 = new Ui({ getStatsInterval: 1e3, rawStats: false, statsObject: true, filteredStats: false, remote: true, debug: false, logLevel: "warn" }), a2 = (o3) => i(this, void 0, void 0, (function* () {
            "stats" === o3.event && kt(e.SwEvent.StatsFrame, (function({ data: e2 }) {
              var t3, i2, n3, s3, o4, r3, a3, c2;
              const { audio: l2, remote: d2 } = e2, { audio: u2 } = d2, h2 = null !== (i2 = null === (t3 = u2.inbound[0]) || void 0 === t3 ? void 0 : t3.jitter) && void 0 !== i2 ? i2 : 1 / 0, p2 = null !== (s3 = null === (n3 = u2.inbound[0]) || void 0 === n3 ? void 0 : n3.roundTripTime) && void 0 !== s3 ? s3 : 1 / 0, g2 = null !== (r3 = null === (o4 = l2.inbound[0]) || void 0 === o4 ? void 0 : o4.packetsReceived) && void 0 !== r3 ? r3 : -1, v2 = null !== (c2 = null === (a3 = l2.inbound[0]) || void 0 === a3 ? void 0 : a3.packetsLost) && void 0 !== c2 ? c2 : -1, m2 = Fi({ jitter: 1e3 * h2, rtt: 1e3 * p2, packetsLost: v2, packetsReceived: g2 });
              return { jitter: h2, rtt: p2, mos: m2, quality: $i(m2), inboundAudio: l2.inbound[0], outboundAudio: l2.outbound[0], remoteInboundAudio: u2.inbound[0], remoteOutboundAudio: u2.outbound[0] };
            })(o3), t2.uuid), yield t2.execute(new Hi(s2, n2, o3));
          }));
          return { get isRunning() {
            return o2;
          }, start: (e2, c2, l2) => i(this, void 0, void 0, (function* () {
            if (o2) Le.debug(`[${n2}] Stats reporter already running, skipping start`);
            else {
              yield t2.execute(new Bi(s2, n2)), r2.on("timeline", a2);
              try {
                yield r2.addConnection({ pc: e2, peerId: c2, connectionId: l2 }), o2 = true;
              } catch (e3) {
                Le.error(`[${n2}] Failed to start stats reporter:`, e3), r2.removeAllPeers(), r2.destroy();
              }
            }
          })), stop: (a3) => i(this, void 0, void 0, (function* () {
            const i2 = r2.getTimeline();
            if (kt(e.SwEvent.StatsReport, i2, t2.uuid), "file" === a3) {
              !(function(e2, t3) {
                const i3 = new Blob([JSON.stringify(e2)], { type: "application/json" }), n3 = URL.createObjectURL(i3), s3 = document.createElement("a");
                s3.href = n3, s3.download = `${t3}.json`, s3.click(), URL.revokeObjectURL(n3);
              })(i2, `webrtc-stats-${s2}-${Date.now()}`);
            }
            yield t2.execute(new ji(s2, n2)), r2.removeAllPeers(), r2.destroy(), o2 = false;
          })), reportConnectionStateChange: (e2) => {
            const t3 = { event: "connectionstatechange-detailed", tag: "connection", timestamp: (/* @__PURE__ */ new Date()).toISOString(), data: e2 };
            a2(t3);
          }, reportIceCandidateError: (e2) => {
            const t3 = { event: "icecandidateerror-detailed", tag: "connection", timestamp: (/* @__PURE__ */ new Date()).toISOString(), data: e2 };
            a2(t3);
          } };
        }
        const Gi = (t2, i2) => {
          const { contentType: n2, canvasType: s2, callID: o2, canvasInfo: r2 = null, currentLayerIdx: a2 = -1 } = i2;
          r2 && "mcu-personal-canvas" !== s2 && delete r2.memberID;
          const c2 = { type: dt.conferenceUpdate, call: t2.calls[o2], canvasInfo: Vi(r2), currentLayerIdx: a2 };
          switch (n2) {
            case "layer-info": {
              const i3 = Object.assign({ action: mt.LayerInfo }, c2);
              kt(e.SwEvent.Notification, i3, t2.uuid);
              break;
            }
            case "layout-info": {
              const i3 = Object.assign({ action: mt.LayoutInfo }, c2);
              kt(e.SwEvent.Notification, i3, t2.uuid);
              break;
            }
          }
        }, Vi = (e2) => {
          const t2 = JSON.stringify(e2).replace(/memberID/g, "participantId").replace(/ID"/g, 'Id"').replace(/POS"/g, 'Pos"');
          return $e(t2);
        }, qi = ["new-call-start", "new-peer", "get-user-media", "peer-creation-end", "start-negotiation", "create-offer", "create-answer", "set-local-description", "ice-gathering-started", "first-candidate", "first-non-host-candidate", "send-sdp", "ice-gathering-completed", "ringing", "telnyx-rtc-media", "first-remote-media-track", "set-remote-description", "telnyx-rtc-answer", "ice-connected", "dtls-connected", "call-active", "answer-called"], Yi = { "new-call-start": "Call Start", "new-peer": "Peer object created", "get-user-media": "Media devices acquired", "peer-creation-end": "Peer setup complete", "start-negotiation": "SDP negotiation started", "create-offer": "SDP offer generated", "create-answer": "SDP answer generated", "set-local-description": "Local description applied", "ice-gathering-started": "ICE candidate gathering started", "first-candidate": "First ICE candidate found", "first-non-host-candidate": "First server-reflexive/relay candidate found", "send-sdp": "SDP sent to server", "ice-gathering-completed": "All ICE candidates gathered", ringing: "Remote side ringing", "telnyx-rtc-media": "Early media received from server", "first-remote-media-track": "First remote audio/video track received", "set-remote-description": "Remote description applied", "telnyx-rtc-answer": "Call answered by remote side", "ice-connected": "ICE connection established", "dtls-connected": "Secure media channel established (DTLS)", "call-active": "Call is active", "answer-called": "Answer delay (invite \u2192 call.answer)" };
        function Ki(e2, t2) {
          return `telnyx:call:${e2}:${t2}`;
        }
        function Ji(e2) {
          try {
            const t2 = performance.getEntriesByName(e2, "mark");
            return t2.length > 0 ? t2[0].startTime : void 0;
          } catch (e3) {
            return;
          }
        }
        function zi(e2) {
          for (const t2 of qi) try {
            performance.clearMarks(Ki(e2, t2));
          } catch (e3) {
            Le.warn("Clearing performance marks is failed");
          }
        }
        class Xi {
          constructor(t2, n2, s2, o2, r2) {
            this.type = t2, this.options = n2, this.statsReporter = null, this.isIceRestarting = false, this.iceDone = false, this._negotiating = false, this._prevConnectionState = null, this._restartedIceOnConnectionStateFailed = false, this._sleepWakeupIntervalId = null, this._iceGatheringSafetyTimeout = null, this._gatheredCandidatesCount = 0, this._firstMediaTrackMarked = false, this._timingsCollected = false, this._iceRestartTimeoutId = null, this.handleConnectionStateChange = () => i(this, void 0, void 0, (function* () {
              var t3, n3;
              if (!this.instance) return void Le.debug("Connection state change ignored: instance is null");
              const { connectionState: s3 } = this.instance;
              if (Le.info(`[${(/* @__PURE__ */ new Date()).toISOString()}] Connection State changed: ${this._prevConnectionState} -> ${s3}`), "failed" !== s3 && "disconnected" !== s3 || (this.isDebugEnabled && this.statsReporter && (function(e2, t4) {
                return i(this, void 0, void 0, (function* () {
                  const i2 = { connectionState: e2.connectionState, previousConnectionState: t4, iceConnectionState: e2.iceConnectionState, iceGatheringState: e2.iceGatheringState, signalingState: e2.signalingState }, n4 = e2.getTransceivers();
                  if (n4.length > 0) {
                    const e3 = n4[0].sender, t5 = null == e3 ? void 0 : e3.transport;
                    t5 && (i2.dtlsState = t5.state);
                  }
                  e2.sctp && (i2.sctpState = e2.sctp.state);
                  try {
                    const t5 = yield e2.getStats();
                    t5.forEach(((e3) => {
                      "candidate-pair" === e3.type && "succeeded" === e3.state && (i2.candidatePairState = e3.state, t5.forEach(((t6) => {
                        "local-candidate" === t6.type && t6.id === e3.localCandidateId && (i2.localCandidateType = t6.candidateType, i2.selectedCandidatePair = i2.selectedCandidatePair || { local: {}, remote: {} }, i2.selectedCandidatePair.local = { address: t6.address, port: t6.port, protocol: t6.protocol, candidateType: t6.candidateType }), "remote-candidate" === t6.type && t6.id === e3.remoteCandidateId && (i2.remoteCandidateType = t6.candidateType, i2.selectedCandidatePair = i2.selectedCandidatePair || { local: {}, remote: {} }, i2.selectedCandidatePair.remote = { address: t6.address, port: t6.port, protocol: t6.protocol, candidateType: t6.candidateType });
                      }))), "transport" === e3.type && (i2.dtlsCipher = e3.dtlsCipher, i2.srtpCipher = e3.srtpCipher, i2.tlsVersion = e3.tlsVersion, e3.dtlsState && (i2.dtlsState = e3.dtlsState));
                    }));
                  } catch (e3) {
                    Le.error("Error gathering connection state details:", e3);
                  }
                  return i2;
                }));
              })(this.instance, this._prevConnectionState).then(((e2) => {
                this.statsReporter.reportConnectionStateChange(e2);
              })), null === (n3 = (t3 = this._session).reportPeerFailure) || void 0 === n3 || n3.call(t3, this.options.id, "connection_failed")), "disconnected" === s3) {
                const t4 = be(V);
                kt(e.SwEvent.Warning, { warning: t4, callId: this.options.id, sessionId: this._session.sessionid }, this.options.id);
              }
              if ("failed" === s3) {
                const t4 = be(K);
                kt(e.SwEvent.PeerConnectionFailureError, { warning: t4, error: new Error(`Peer Connection failed. previous state: ${this._prevConnectionState}, current state: ${s3}`), sessionId: this._session.sessionid }, this.options.id);
              }
              this._prevConnectionState = s3, "connected" === s3 && (performance.mark(Ki(this.options.id, "dtls-connected")), this.tryCollectTimings(), this._restartedIceOnConnectionStateFailed = false), this._isTrickleIce() && ("connecting" === s3 && performance.mark(Ki(this.options.id, "peer-connection-connecting")), "connected" === s3 && (this._clearIceGatheringSafetyTimeout(), performance.mark(Ki(this.options.id, "peer-connection-connected"))));
            })), this._handleIceConnectionStateChange = () => {
              var e2, t3;
              if (!this.instance) return void Le.debug("ICE connection state change ignored: instance is null");
              const i2 = this.instance.iceConnectionState;
              Le.debug(`[${(/* @__PURE__ */ new Date()).toISOString()}] ICE Connection State`, i2), "connected" === i2 && performance.mark(Ki(this.options.id, "ice-connected")), "failed" === i2 && (null === (t3 = (e2 = this._session).reportPeerFailure) || void 0 === t3 || t3.call(e2, this.options.id, "ice_failed"));
            }, this._handleIceGatheringStateChange = () => {
              const e2 = this.instance.iceGatheringState;
              Le.debug(`[${(/* @__PURE__ */ new Date()).toISOString()}] ICE Gathering State`, e2), "gathering" === e2 ? (this._gatheredCandidatesCount = 0, this._startIceGatheringSafetyTimeout()) : "complete" === e2 && this._clearIceGatheringSafetyTimeout();
            }, this._setCodecs = (e2, t3) => {
              if (e2.setCodecPreferences) return e2.setCodecPreferences(t3);
            }, Le.debug("New Peer with type:", this.type, "Options:", this.options), this._constraints = { offerToReceiveAudio: true, offerToReceiveVideo: !!n2.video }, this.handleSignalingStateChangeEvent = this.handleSignalingStateChangeEvent.bind(this), this.handleNegotiationNeededEvent = this.handleNegotiationNeededEvent.bind(this), this.handleTrackEvent = this.handleTrackEvent.bind(this), this.createPeerConnection = this.createPeerConnection.bind(this), this._session = s2, this._trickleIceSdpFn = o2, this._registerPeerEvents = r2;
          }
          finishIceRestart() {
            this.isIceRestarting && (this.isIceRestarting = false, this._iceRestartTimeoutId && (clearTimeout(this._iceRestartTimeoutId), this._iceRestartTimeoutId = null));
          }
          restartIce() {
            return this.instance ? this.isIceRestarting ? (Le.debug("ICE restart: already in progress, skipping"), { started: false, reason: "already in progress" }) : this._session.connected ? (this.isIceRestarting = true, this._restartedIceOnConnectionStateFailed = true, this.instance.restartIce(), this.iceDone = false, this._iceRestartTimeoutId = setTimeout((() => {
              this.isIceRestarting && (Le.warn("ICE restart: Modify exchange timed out, clearing isIceRestarting flag"), this.isIceRestarting = false), this._iceRestartTimeoutId = null;
            }), Xi.ICE_RESTART_TIMEOUT_MS), Le.info("ICE restart: initiated by signaling health monitor"), { started: true }) : (Le.debug("ICE restart: session not connected, skipping"), { started: false, reason: "session not connected" }) : (Le.warn("ICE restart: no RTCPeerConnection instance"), { started: false, reason: "no RTCPeerConnection instance" });
          }
          get isOffer() {
            return this.type === at.Offer;
          }
          get isAnswer() {
            return this.type === at.Answer;
          }
          get isDebugEnabled() {
            return this.options.debug || this._session.options.debug;
          }
          get debugOutput() {
            return this.options.debugOutput || this._session.options.debugOutput;
          }
          get restartedIceOnConnectionStateFailed() {
            return this._restartedIceOnConnectionStateFailed;
          }
          isConnectionHealthy() {
            return "connected" === this.instance.connectionState && "connected" === this.instance.iceConnectionState && "closed" !== this.instance.signalingState;
          }
          startNegotiation() {
            performance.mark(Ki(this.options.id, "start-negotiation")), this._negotiating = true, this._isOffer() || this.isIceRestarting ? this._createOffer().catch(((e2) => this._emitNegotiationError(e2))) : this._createAnswer().catch(((e2) => this._emitNegotiationError(e2)));
          }
          startTrickleIceNegotiation() {
            return i(this, void 0, void 0, (function* () {
              performance.mark(Ki(this.options.id, "start-negotiation")), this._negotiating = true, this._isOffer() || this.isIceRestarting ? yield this._createOffer().then(this._trickleIceSdpFn.bind(this)) : yield this._createAnswer().then(this._trickleIceSdpFn.bind(this));
            }));
          }
          _emitNegotiationError(t2) {
            t2 instanceof Ie && kt(e.SwEvent.Error, { error: t2, sessionId: this._session.sessionid }, this.options.id);
          }
          _logTransceivers() {
            this.instance ? (Le.info("Number of transceivers:", this.instance.getTransceivers().length), this.instance.getTransceivers().forEach(((e2, t2) => {
              Le.info(`>> Transceiver [${t2}]:`, e2.mid, e2.direction, e2.stopped), Le.info(`>> Sender Params [${t2}]:`, JSON.stringify(e2.sender.getParameters(), null, 2));
            }))) : Le.warn("Cannot log transceivers: peer connection is null");
          }
          handleSignalingStateChangeEvent() {
            switch (Le.info("signalingState:", this.instance.signalingState), this.instance.signalingState) {
              case "stable":
                this._negotiating = false;
                break;
              case "closed":
                kt(e.SwEvent.PeerConnectionSignalingStateClosed, { sessionId: this._session.sessionid }, this.options.id), this.instance && (Le.debug(`[${this.options.id}] Closing peer due to signalingState closed`), this.close());
                break;
              default:
                this._negotiating = true;
            }
          }
          handleNegotiationNeededEvent() {
            Le.info("Negotiation needed event"), "stable" !== this.instance.signalingState || this._negotiating ? Le.debug("Skipping negotiation, state:", this.instance.signalingState, "negotiating:", this._negotiating) : this._isTrickleIce() ? this.startTrickleIceNegotiation().catch(((e2) => this._emitNegotiationError(e2))) : this.startNegotiation();
          }
          handleTrackEvent(e2) {
            this._firstMediaTrackMarked || (performance.mark(Ki(this.options.id, "first-remote-media-track")), this._firstMediaTrackMarked = true);
            const { streams: [t2] } = e2, { remoteElement: i2, screenShare: n2 } = this.options;
            this.options.remoteStream = t2, false === n2 && ai(i2, this.options.remoteStream, { callId: this.options.id, sessionId: this._session.sessionid, eventTarget: this._session.uuid });
          }
          tryCollectTimings() {
            if (this._timingsCollected) return;
            if (!(performance.getEntriesByName(Ki(this.options.id, "call-active"), "mark").length > 0) || "connected" !== this.instance.connectionState) return;
            this._timingsCollected = true;
            const e2 = this._isTrickleIce() ? "trickle" : "non-trickle", t2 = this.isOffer ? "outbound" : "inbound", i2 = (function(e3, t3, i3) {
              const n2 = Ji(Ki(e3, "new-call-start"));
              if (void 0 === n2) return { mode: t3, direction: i3, steps: [] };
              const s2 = [];
              for (const t4 of qi) {
                if ("new-call-start" === t4) continue;
                const i4 = Ji(Ki(e3, t4));
                void 0 !== i4 && s2.push({ label: Yi[t4] || t4, fromStart: i4 - n2 });
              }
              s2.sort(((e4, t4) => e4.fromStart - t4.fromStart));
              const o2 = [];
              let r2 = 0;
              for (const e4 of s2) o2.push({ label: e4.label, fromStart: e4.fromStart, delta: e4.fromStart - r2 }), r2 = e4.fromStart;
              return { mode: t3, direction: i3, steps: o2 };
            })(this.options.id, e2, t2);
            !(function(e3) {
              const { mode: t3, direction: i3, steps: n2 } = e3, s2 = `[CallTimings][${i3}][${t3}]`;
              if (0 === n2.length) return void Le.info(`${s2} No timing data collected`);
              const o2 = Math.max(...n2.map(((e4) => e4.label.length)), 4) + 2, r2 = (e4, t4) => {
                for (; e4.length < t4; ) e4 += " ";
                return e4;
              }, a2 = (e4, t4) => {
                for (; e4.length < t4; ) e4 = " " + e4;
                return e4;
              }, c2 = r2("Step", o2) + a2("Delta", 14) + a2("From Start", 14);
              let l2 = "";
              for (let e4 = 0; e4 < c2.length; e4++) l2 += "-";
              Le.info(`${s2} Call establishment timing breakdown:`), Le.info(`${s2} ${c2}`), Le.info(`${s2} ${l2}`), Le.info(`${s2} ${r2("Call Start", o2)}${a2("-", 14)}${a2("0.00ms", 14)}`);
              for (const e4 of n2) {
                const t4 = e4.delta.toFixed(2) + "ms", i4 = e4.fromStart.toFixed(2) + "ms";
                Le.info(`${s2} ${r2(e4.label, o2)}${a2(t4, 14)}${a2(i4, 14)}`);
              }
              Le.info(`${s2} ${l2}`);
            })(i2), zi(this.options.id);
          }
          createPeerConnection() {
            return i(this, void 0, void 0, (function* () {
              var t2;
              if (this.instance = (t2 = this._config(), new window.RTCPeerConnection(t2)), this.instance.onsignalingstatechange = this.handleSignalingStateChangeEvent, this.instance.onnegotiationneeded = this.handleNegotiationNeededEvent, this.instance.ontrack = this.handleTrackEvent, this.instance.addEventListener("connectionstatechange", this.handleConnectionStateChange), this.instance.addEventListener("iceconnectionstatechange", this._handleIceConnectionStateChange), this.instance.addEventListener("icegatheringstatechange", this._handleIceGatheringStateChange), this.instance.addEventListener("addstream", ((e2) => {
                this.options.remoteStream = e2.stream;
              })), this._registerPeerEvents(this.instance), this._prevConnectionState = this.instance.connectionState, this.isAnswer && (yield this._setRemoteDescription({ sdp: this.options.remoteSdp, type: at.Offer }), performance.mark(Ki(this.options.id, "set-remote-description")), !this.instance)) throw Ce(E);
              const n2 = Boolean(this.options.receiveOnlyAudio) && !this.options.audio;
              let s2 = null;
              if (this.options.localStream = yield this._retrieveLocalStream().catch(((t3) => i(this, void 0, void 0, (function* () {
                const n3 = this._session.options.mediaPermissionsRecovery;
                if ((null == n3 ? void 0 : n3.enabled) && this._isAnswer()) {
                  let o2 = null, r2 = null;
                  return yield new Promise(((i2, s3) => {
                    r2 = setTimeout((() => s3(new Error("Media recovery flow timed out!"))), n3.timeout), kt(e.SwEvent.Error, { error: Ce(Ee(t3), t3, void 0, false), callId: this.options.id, sessionId: this._session.sessionid, recoverable: true, retryDeadline: Date.now() + n3.timeout, resume: () => {
                      i2();
                    }, reject: () => {
                      s3(new Error("Call was rejected during media recovery flow!"));
                    } }, this._session.uuid);
                  })).then((() => i(this, void 0, void 0, (function* () {
                    var e2;
                    r2 && (clearTimeout(r2), r2 = null), o2 = yield this._retrieveLocalStream(), null === (e2 = n3.onSuccess) || void 0 === e2 || e2.call(n3);
                  })))).catch(((e2) => {
                    var t4;
                    r2 && (clearTimeout(r2), r2 = null), s2 = e2, null === (t4 = n3.onError) || void 0 === t4 || t4.call(n3, e2);
                  })), o2;
                }
                return s2 = t3, null;
              })))), !this.instance) throw Ce(E);
              if (!this.options.localStream && !n2) {
                throw Ce(s2 ? Ee(s2) : _, null != s2 ? s2 : void 0);
              }
              performance.mark(Ki(this.options.id, "get-user-media")), this.options.mutedMicOnStart && ri(this.options.localStream) && (Le.info("Muting local audio tracks on start"), Ei(this.options.localStream)), this.options.applyDesiredAudioMuteState && this.options.applyDesiredAudioMuteState(), performance.mark(Ki(this.options.id, "peer-creation-end"));
            }));
          }
          incrementGatheredCandidates() {
            this._gatheredCandidatesCount++;
          }
          _startIceGatheringSafetyTimeout() {
            this._clearIceGatheringSafetyTimeout(), this._iceGatheringSafetyTimeout = setTimeout((() => {
              if (this.instance) {
                if (0 === this._gatheredCandidatesCount) {
                  const t2 = be(Y);
                  kt(e.SwEvent.Warning, { warning: t2, callId: this.options.id, sessionId: this._session.sessionid }, this.options.id);
                } else if ("complete" !== this.instance.iceGatheringState) {
                  const t2 = be(q);
                  kt(e.SwEvent.Warning, { warning: t2, callId: this.options.id, sessionId: this._session.sessionid }, this.options.id);
                }
              }
            }), Xi.ICE_GATHERING_SAFETY_TIMEOUT_MS);
          }
          _clearIceGatheringSafetyTimeout() {
            null !== this._iceGatheringSafetyTimeout && (clearTimeout(this._iceGatheringSafetyTimeout), this._iceGatheringSafetyTimeout = null);
          }
          init() {
            var e2;
            return i(this, void 0, void 0, (function* () {
              if (yield this.createPeerConnection(), !this.instance) throw Ce(E);
              this.isDebugEnabled && (this.statsReporter = Wi(this._session, this.options.id), yield null === (e2 = this.statsReporter) || void 0 === e2 ? void 0 : e2.start(this.instance, this._session.sessionid, this._session.sessionid));
              const { localElement: t2, localStream: i2 = null, screenShare: n2 = false } = this.options;
              if (ri(i2)) {
                const e3 = i2.getAudioTracks();
                let s2 = [...e3];
                if (Le.info("Local audio tracks: ", e3), "object" == typeof this.options.audio && e3.forEach(((e4) => {
                  Le.info("Local audio tracks constraints: ", e4.getConstraints());
                })), this.options.video) {
                  const t3 = i2.getVideoTracks();
                  s2 = [...e3, ...t3], Le.info("Local video tracks: ", t3), "object" == typeof this.options.video && t3.forEach(((e4) => {
                    Le.info("Local video tracks constraints: ", e4.getConstraints());
                  }));
                }
                const { audioCodecs: o2, videoCodecs: r2 } = Ai(this.options.preferred_codecs);
                if (this.isOffer && "function" == typeof this.instance.addTransceiver) {
                  const e4 = { direction: "sendrecv", streams: [i2] };
                  s2.forEach(((t3) => {
                    "audio" === t3.kind && (this.options.userVariables.microphoneLabel = t3.label), "video" === t3.kind && (this.options.userVariables.cameraLabel = t3.label);
                    const i3 = this.instance.addTransceiver(t3, e4);
                    "audio" === t3.kind && o2.length > 0 && this._setCodecs(i3, o2), "video" === t3.kind && r2.length > 0 && this._setCodecs(i3, r2);
                  }));
                } else "function" == typeof this.instance.addTrack ? (s2.forEach(((e4) => {
                  "audio" === e4.kind && (this.options.userVariables.microphoneLabel = e4.label), "video" === e4.kind && (this.options.userVariables.cameraLabel = e4.label), this.instance.addTrack(e4, i2);
                })), this.instance.getTransceivers().forEach(((e4) => {
                  "audio" === e4.receiver.track.kind && o2.length > 0 && this._setCodecs(e4, o2), "video" === e4.receiver.track.kind && r2.length > 0 && this._setCodecs(e4, r2);
                }))) : this.instance.addStream(i2);
                false === n2 && ai(t2, i2);
              } else if (this.options.receiveOnlyAudio && "function" == typeof this.instance.addTransceiver) {
                const e3 = this.instance.addTransceiver("audio", { direction: "recvonly" });
                Le.info("Added recvonly audio transceiver for receive-only mode", e3);
                const { audioCodecs: t3 } = Ai(this.options.preferred_codecs);
                t3.length > 0 && this._setCodecs(e3, t3);
              }
              this.isOffer ? (this.options.negotiateAudio && this._checkMediaToNegotiate("audio"), this.options.negotiateVideo && this._checkMediaToNegotiate("video")) : this._isTrickleIce() || this.startNegotiation(), this._isTrickleIce() && this.startTrickleIceNegotiation().catch(((e3) => this._emitNegotiationError(e3))), this._logTransceivers();
            }));
          }
          _getSenderByKind(e2) {
            return this.instance.getSenders().find((({ track: t2 }) => t2 && t2.kind === e2));
          }
          _checkMediaToNegotiate(e2) {
            if (!this._getSenderByKind(e2)) {
              const t2 = this.instance.addTransceiver(e2);
              Le.info("Add transceiver", e2, t2);
            }
          }
          _createOffer() {
            return i(this, void 0, void 0, (function* () {
              if (this._isOffer() || this.isIceRestarting) {
                this._constraints.offerToReceiveAudio = false !== this.options.audio || Boolean(this.options.receiveOnlyAudio), this._constraints.offerToReceiveVideo = Boolean(this.options.video), Le.info("_createOffer - this._constraints", this._constraints);
                try {
                  const e2 = yield this.instance.createOffer(this._constraints);
                  return performance.mark(Ki(this.options.id, "create-offer")), yield this._setLocalDescription(e2), performance.mark(Ki(this.options.id, "set-local-description")), performance.mark(Ki(this.options.id, "ice-gathering-started")), e2;
                } catch (e2) {
                  if (Le.error("Peer _createOffer error:", e2), e2 instanceof Ie) throw e2;
                  throw Ce(u, e2);
                }
              }
            }));
          }
          _setRemoteDescription(e2) {
            return i(this, void 0, void 0, (function* () {
              Le.debug("Setting remote description", e2);
              try {
                yield this.instance.setRemoteDescription(e2);
              } catch (e3) {
                Le.error("Peer _setRemoteDescription error:", e3);
                throw Ce(g, e3);
              }
            }));
          }
          _createAnswer() {
            return i(this, void 0, void 0, (function* () {
              if (this._isAnswer()) {
                if ("stable" !== this.instance.signalingState && "have-remote-offer" !== this.instance.signalingState) return Le.debug("Skipping negotiation, state:", this.instance.signalingState), Le.debug("  - But the signaling state isn't stable, so triggering rollback"), void (yield Promise.all([this.instance.setLocalDescription({ type: "rollback" }), this.instance.setRemoteDescription({ sdp: this.options.remoteSdp, type: at.Offer })]));
                this._logTransceivers();
                try {
                  const e2 = yield this.instance.createAnswer();
                  return performance.mark(Ki(this.options.id, "create-answer")), yield this._setLocalDescription(e2), performance.mark(Ki(this.options.id, "set-local-description")), performance.mark(Ki(this.options.id, "ice-gathering-started")), e2;
                } catch (e2) {
                  if (Le.error("Peer _createAnswer error:", e2), e2 instanceof Ie) throw e2;
                  throw Ce(h, e2);
                }
              }
            }));
          }
          _setLocalDescription(e2) {
            return i(this, void 0, void 0, (function* () {
              try {
                yield this.instance.setLocalDescription(e2);
              } catch (e3) {
                Le.error("Peer _setLocalDescription error:", e3);
                throw Ce(p, e3);
              }
            }));
          }
          _retrieveLocalStream() {
            return i(this, void 0, void 0, (function* () {
              if (ri(this.options.localStream)) return this.options.localStream;
              const e2 = yield (t2 = this.options, i(void 0, void 0, void 0, (function* () {
                let { audio: e3 = true, micId: i2, video: n2 = false, camId: s2 } = t2;
                const { micLabel: o2 = "", camLabel: r2 = "" } = t2;
                return i2 && (i2 = yield vi(i2, o2, ft.AudioIn).catch((() => null)), i2 && ("boolean" == typeof e3 && (e3 = {}), e3.deviceId = { exact: i2 })), s2 && (s2 = yield vi(s2, r2, ft.Video).catch((() => null)), s2 && ("boolean" == typeof n2 && (n2 = {}), n2.deviceId = { exact: s2 })), { audio: e3, video: n2 };
              })));
              var t2;
              return hi(e2);
            }));
          }
          _isOffer() {
            return this.type === at.Offer;
          }
          _isAnswer() {
            return this.type === at.Answer;
          }
          _isTrickleIce() {
            return true === this.options.trickleIce;
          }
          _config() {
            const { prefetchIceCandidates: e2, forceRelayCandidate: t2, iceServers: i2 } = this.options, n2 = { bundlePolicy: "balanced", iceCandidatePoolSize: e2 ? 10 : 0, iceServers: i2, iceTransportPolicy: t2 ? "relay" : "all" };
            return Le.info("RTC config", n2), n2;
          }
          restartStatsReporter() {
            return i(this, void 0, void 0, (function* () {
              this.isDebugEnabled && this.statsReporter && (this.instance ? this.statsReporter.isRunning ? Le.debug(`[${this.options.id}] Stats reporter already running, skipping restart`) : (Le.debug(`[${this.options.id}] Restarting stats reporter after reconnect`), yield this.statsReporter.start(this.instance, this._session.sessionid, this._session.sessionid)) : Le.debug(`[${this.options.id}] Cannot restart stats reporter - no peer connection instance`));
            }));
          }
          close() {
            return i(this, void 0, void 0, (function* () {
              zi(this.options.id), this.finishIceRestart(), this._clearIceGatheringSafetyTimeout(), null !== this._sleepWakeupIntervalId && (clearInterval(this._sleepWakeupIntervalId), this._sleepWakeupIntervalId = null), this.isDebugEnabled && this.statsReporter && (yield this.statsReporter.stop(this.debugOutput)), this.instance && (this.instance.close(), this.instance = null);
            }));
          }
        }
        Xi.ICE_GATHERING_SAFETY_TIMEOUT_MS = 15e3, Xi.ICE_RESTART_TIMEOUT_MS = 15e3;
        const Qi = Wt;
        class Zi {
          constructor(e2, t2) {
            this.session = e2, this._callReportCollector = null, this._callRecorder = null, this._mediaDeviceCollector = null, this.id = "", this.recoveredCallId = "", this.state = gt[gt.New], this.prevState = "", this.channels = [], this.role = vt.Participant, this.extension = null, this._state = gt.New, this._prevState = gt.New, this.gotAnswer = false, this.gotEarly = false, this._lastSerno = 0, this._targetNodeId = null, this._iceTimeout = null, this._statsBindings = [], this._statsIntervalId = null, this._pendingIceCandidates = [], this._isRemoteDescriptionSet = false, this._signalingStateClosed = false, this._creatingPeer = false, this._desiredAudioMuted = false, this._firstCandidateSent = false, this._firstNonHostCandidateSent = false, this._isRecovering = false, this._wasHeldBeforeRecovery = false, this._checkConferenceSerno = (e3) => {
              const t3 = e3 < 0 || !this._lastSerno || this._lastSerno && e3 === this._lastSerno + 1;
              return t3 && e3 >= 0 && (this._lastSerno = e3), t3;
            }, this._doStats = () => {
              this.peer && this.peer.instance && 0 !== this._statsBindings.length && this.peer.instance.getStats().then(((e3) => {
                e3.forEach(((e4) => {
                  this._statsBindings.forEach(((t3) => {
                    if (t3.callback) {
                      if (t3.constraints) {
                        for (const i3 in t3.constraints) if (t3.constraints.hasOwnProperty(i3) && t3.constraints[i3] !== e4[i3]) return;
                      }
                      t3.callback(e4);
                    }
                  }));
                }));
              }));
            };
            const { iceServers: i2, speaker: n2, micId: s2, micLabel: o2, camId: r2, camLabel: a2, localElement: c2, remoteElement: l2, options: d2, mediaConstraints: { audio: u2, video: h2 }, ringtoneFile: p2, ringbackFile: g2 } = e2;
            this.options = Object.assign({}, pt, { audio: u2, video: h2, iceServers: (null == t2 ? void 0 : t2.iceServers) && Array.isArray(t2.iceServers) ? t2.iceServers : i2, localElement: c2, remoteElement: l2, micId: s2, micLabel: o2, camId: r2, camLabel: a2, speakerId: n2, ringtoneFile: p2, ringbackFile: g2, debug: d2.debug, debugOutput: d2.debugOutput, trickleIce: d2.trickleIce, prefetchIceCandidates: d2.prefetchIceCandidates, forceRelayCandidate: d2.forceRelayCandidate, keepConnectionAliveOnSocketClose: d2.keepConnectionAliveOnSocketClose, mutedMicOnStart: d2.mutedMicOnStart }, t2), this._onMediaError = this._onMediaError.bind(this), this._onPeerConnectionFailureError = this._onPeerConnectionFailureError.bind(this), this._onPeerConnectionSignalingStateClosed = this._onPeerConnectionSignalingStateClosed.bind(this), this._onTrickleIceSdp = this._onTrickleIceSdp.bind(this), this._registerPeerEvents = this._registerPeerEvents.bind(this), this._desiredAudioMuted = Boolean(this.options.mutedMicOnStart), this.options.applyDesiredAudioMuteState = this._applyDesiredAudioMuteState.bind(this), this._init(), this.options && (this._ringtone = Ti(this.options.ringtoneFile, "_ringtone"), this._ringback = Ti(this.options.ringbackFile, "_ringback"));
          }
          get creatingPeer() {
            return this._creatingPeer;
          }
          get signalingStateClosed() {
            return this._signalingStateClosed;
          }
          _captureHangupCallerStack() {
            const e2 = new Error("Call.hangup caller").stack;
            return e2 ? e2.split("\n").map(((e3) => e3.trim())).filter(Boolean).slice(1, 11) : [];
          }
          get nodeId() {
            return this._targetNodeId;
          }
          set nodeId(e2) {
            this._targetNodeId = e2;
          }
          get isVideoCall() {
            return !!this.options.video;
          }
          get telnyxIDs() {
            return { telnyxCallControlId: this.options.telnyxCallControlId, telnyxSessionId: this.options.telnyxSessionId, telnyxLegId: this.options.telnyxLegId };
          }
          get localStream() {
            return this.options.localStream;
          }
          get remoteStream() {
            return this.options.remoteStream;
          }
          get memberChannel() {
            return `conference-member.${this.id}`;
          }
          get isAudioMuted() {
            return this._desiredAudioMuted;
          }
          _getLocalAudioTrackId() {
            var e2, t2;
            return null === (t2 = null === (e2 = this.options.localStream) || void 0 === e2 ? void 0 : e2.getAudioTracks()[0]) || void 0 === t2 ? void 0 : t2.id;
          }
          _hasActiveUnmutedLocalAudioTrack() {
            const e2 = this.options.localStream;
            return !!(null == e2 ? void 0 : e2.getAudioTracks) && e2.getAudioTracks().some(((e3) => true === e3.enabled && true !== e3.muted && "live" === e3.readyState));
          }
          shouldForceRelayCandidateForRecovery() {
            var e2, t2;
            return !!this.options.forceRelayCandidate || !!this.recoveredCallId && (null !== (t2 = null === (e2 = this._callReportCollector) || void 0 === e2 ? void 0 : e2.shouldForceRelayCandidateForRecovery()) && void 0 !== t2 && t2);
          }
          invite() {
            return i(this, void 0, void 0, (function* () {
              this._creatingPeer = true, this.direction = ct.Outbound, this.options.trickleIce && this._resetTrickleIceCandidateState(), performance.mark(Ki(this.id, "new-peer")), this.peer = new Xi(at.Offer, this.options, this.session, this._onTrickleIceSdp, this._registerPeerEvents);
              try {
                yield this.peer.init();
              } catch (t2) {
                Le.error("Peer init failed, aborting call", t2), this._creatingPeer = false;
                const i2 = t2 instanceof Ie ? t2 : Ce(M, t2 instanceof Error ? t2 : void 0);
                return kt(e.SwEvent.Error, { error: i2, callId: this.id, sessionId: this.session.sessionid, recoverable: false }, this.session.uuid), void this.hangup({ initiator: "sdk:peer-init-failed" }, false);
              }
              this._creatingPeer = false;
            }));
          }
          answer(t2 = {}) {
            var n2, s2, o2, r2, a2, c2, l2, d2;
            return i(this, void 0, void 0, (function* () {
              const i2 = (null !== (o2 = null === (s2 = (n2 = this.session).getActiveCalls) || void 0 === s2 ? void 0 : s2.call(n2)) && void 0 !== o2 ? o2 : []).filter(((e2) => e2.id !== this.id));
              if (i2.length > 0 && Le.debug(`[${this.id}] answer(): answering inbound call while ${i2.length} other active call(s) exist in session ${this.session.sessionid}`), this._creatingPeer || (null === (r2 = this.peer) || void 0 === r2 ? void 0 : r2.instance) && "closed" !== this.peer.instance.signalingState) {
                const t3 = be(z);
                return kt(e.SwEvent.Warning, { warning: t3, callId: this.id, sessionId: this.session.sessionid }, this.session.uuid), void Le.warn(`[${this.id}] answer() ignored: peer connection already exists or is being created (signalingState: ${null !== (l2 = null === (c2 = null === (a2 = this.peer) || void 0 === a2 ? void 0 : a2.instance) || void 0 === c2 ? void 0 : c2.signalingState) && void 0 !== l2 ? l2 : "creating"})`);
              }
              if (this._registerInboundAnswerAttempt()) {
                performance.mark(Ki(this.id, "answer-called")), this._creatingPeer = true, this.stopRingtone(), this.direction = ct.Inbound, (null === (d2 = null == t2 ? void 0 : t2.customHeaders) || void 0 === d2 ? void 0 : d2.length) > 0 && (this.options = Object.assign(Object.assign({}, this.options), { customHeaders: t2.customHeaders })), void 0 === t2.remoteElement && void 0 === t2.localElement || (this.options = Object.assign(Object.assign(Object.assign({}, this.options), void 0 !== t2.remoteElement && { remoteElement: t2.remoteElement }), void 0 !== t2.localElement && { localElement: t2.localElement })), this.options.trickleIce && this._resetTrickleIceCandidateState(), performance.mark(Ki(this.id, "new-peer")), this.peer = new Xi(at.Answer, this.options, this.session, this._onTrickleIceSdp, this._registerPeerEvents);
                try {
                  yield this.peer.init();
                } catch (t3) {
                  Le.error("Peer init failed, aborting call", t3), this._creatingPeer = false;
                  const i3 = t3 instanceof Ie ? t3 : Ce(M, t3 instanceof Error ? t3 : void 0);
                  return kt(e.SwEvent.Error, { error: i3, callId: this.id, sessionId: this.session.sessionid, recoverable: false }, this.session.uuid), void (yield this.hangup({ initiator: "sdk:peer-init-failed" }, true));
                }
                this._creatingPeer = false;
              }
            }));
          }
          playRingtone() {
            ki(this._ringtone);
          }
          stopRingtone() {
            Ri(this._ringtone);
          }
          playRingback() {
            ki(this._ringback);
          }
          stopRingback() {
            Ri(this._ringback);
          }
          hangup(t2, n2) {
            var s2, o2, r2, a2, c2, l2;
            return i(this, void 0, void 0, (function* () {
              const i2 = t2 || {}, d2 = false !== n2, u2 = this.state, h2 = this.prevState, p2 = this._captureHangupCallerStack(), g2 = i2.initiator || "app:call.hangup", v2 = this._state < gt.Active ? { cause: "USER_BUSY", causeCode: 17 } : { cause: "NORMAL_CLEARING", causeCode: 16 };
              if (this.cause = i2.cause || v2.cause, this.causeCode = i2.causeCode || v2.causeCode, this.sipCode = i2.sipCode || null, this.sipReason = i2.sipReason || null, this.sipCallId = i2.sip_call_id || null, this.options.customHeaders = [...null !== (s2 = this.options.customHeaders) && void 0 !== s2 ? s2 : [], ...null !== (r2 = null === (o2 = null == i2 ? void 0 : i2.dialogParams) || void 0 === o2 ? void 0 : o2.customHeaders) && void 0 !== r2 ? r2 : []], Le.debug(`[${this.id}] hangup() invoked`, { callId: this.id, execute: d2, state: u2, prevState: h2, cause: this.cause, causeCode: this.causeCode, initiator: g2, sipCode: this.sipCode, sipReason: this.sipReason, sipCallId: this.sipCallId, isRecovering: Boolean(i2.isRecovering), hasDialogCustomHeaders: Boolean(null === (c2 = null === (a2 = i2.dialogParams) || void 0 === a2 ? void 0 : a2.customHeaders) || void 0 === c2 ? void 0 : c2.length), callerStack: p2 }), i2.isRecovering) return this._isRecovering = true, this.setState(gt.Recovering), void this._finalize();
              if (this.setState(gt.Hangup), this.stopRingtone(), this.stopRingback(), d2) {
                const t3 = new Jt({ sipCode: this.sipCode, sip_call_id: this.sipCallId, sessid: this.session.sessionid, dialogParams: this.options, cause: this.cause, causeCode: this.causeCode });
                let i3;
                try {
                  yield Promise.race([this._execute(t3), new Promise(((e2) => {
                    i3 = setTimeout((() => {
                      Le.warn(`[${this.id}] BYE execution timed out after 5000ms \u2014 proceeding to destroy.`), e2();
                    }), 5e3);
                  }))]);
                } catch (t4) {
                  Le.error("telnyx_rtc.bye failed!", t4);
                  const i4 = Ce(b, t4);
                  kt(e.SwEvent.Error, { error: i4, callId: this.id, sessionId: this.session.sessionid }, this.session.uuid);
                } finally {
                  i3 && clearTimeout(i3);
                }
              }
              Le.debug(`[${this.id}] Closing peer from hangup`), null === (l2 = this.peer) || void 0 === l2 || l2.close(), this.setState(gt.Destroy);
            }));
          }
          hold() {
            const e2 = new Qt({ sessid: this.session.sessionid, action: St.Hold, dialogParams: this.options });
            return this._execute(e2).then(this._handleChangeHoldStateSuccess.bind(this)).catch(this._handleChangeHoldStateError.bind(this));
          }
          unhold() {
            const e2 = new Qt({ sessid: this.session.sessionid, action: St.Unhold, dialogParams: this.options });
            return this._execute(e2).then(this._handleChangeHoldStateSuccess.bind(this)).catch(this._handleChangeHoldStateError.bind(this));
          }
          toggleHold() {
            const e2 = new Qt({ sessid: this.session.sessionid, action: St.ToggleHold, dialogParams: this.options });
            return this._execute(e2).then(this._handleChangeHoldStateSuccess.bind(this)).catch(this._handleChangeHoldStateError.bind(this));
          }
          dtmf(e2) {
            const t2 = new Zt({ sessid: this.session.sessionid, dtmf: e2, dialogParams: this.options });
            this._execute(t2);
          }
          message(e2, t2) {
            const i2 = { from: this.session.options.login, to: e2, body: t2 }, n2 = new Zt({ sessid: this.session.sessionid, msg: i2, dialogParams: this.options });
            this._execute(n2);
          }
          muteAudio() {
            Le.debug("muteAudio called", { callId: this.id, previousAudioState: this._desiredAudioMuted, audioTrackId: this._getLocalAudioTrackId() }), this._desiredAudioMuted = true, Ei(this.options.localStream);
          }
          unmuteAudio() {
            Le.debug("unmuteAudio called", { callId: this.id, previousAudioState: this._desiredAudioMuted, audioTrackId: this._getLocalAudioTrackId() }), this._desiredAudioMuted = false, Ii(this.options.localStream);
          }
          _applyDesiredAudioMuteState() {
            Le.debug("applyDesiredAudioMuteState called", { callId: this.id, previousAudioState: this._desiredAudioMuted, audioTrackId: this._getLocalAudioTrackId() }), this._desiredAudioMuted ? Ei(this.options.localStream) : Ii(this.options.localStream);
          }
          toggleAudioMute() {
            Le.debug("toggleAudioMute called", { callId: this.id, previousAudioState: this._desiredAudioMuted, audioTrackId: this._getLocalAudioTrackId() }), this._desiredAudioMuted = !this._desiredAudioMuted, this._applyDesiredAudioMuteState();
          }
          setAudioInDevice(t2, n2 = this._desiredAudioMuted) {
            var s2, o2, r2;
            return i(this, void 0, void 0, (function* () {
              const i2 = Boolean(n2), { instance: a2 } = this.peer, c2 = a2.getSenders().find((({ track: { kind: e2 } }) => "audio" === e2));
              if (!c2) return Le.warn("Skipping audio input device change: no audio sender found", { callId: this.id, deviceId: t2, audioMuted: this._desiredAudioMuted, audioTrackId: this._getLocalAudioTrackId() }), void kt(e.SwEvent.Warning, { warning: be(Q), callId: this.id, deviceId: t2, sessionId: this.session.sessionid }, (null === (s2 = this.options) || void 0 === s2 ? void 0 : s2.id) || this.id);
              let l2;
              Le.debug("Starting audio input device change", { callId: this.id, state: this.state, deviceId: t2, previousDesiredAudioMuted: this._desiredAudioMuted, newDesiredMuted: i2, currentSenderTrack: fi(c2.track), localTracks: _i(this.options.localStream) });
              try {
                l2 = yield oi({ audio: { deviceId: { exact: t2 } } });
              } catch (t3) {
                const i3 = Ce(Ee(t3), t3);
                return void kt(e.SwEvent.MediaError, i3, (null === (o2 = this.options) || void 0 === o2 ? void 0 : o2.id) || this.id);
              }
              const d2 = l2.getAudioTracks()[0];
              d2.enabled = !i2;
              try {
                yield c2.replaceTrack(d2);
              } catch (t3) {
                const i3 = Ce(Ee(t3), t3);
                return kt(e.SwEvent.MediaError, i3, (null === (r2 = this.options) || void 0 === r2 ? void 0 : r2.id) || this.id), void l2.getTracks().forEach(((e2) => e2.stop()));
              }
              this._desiredAudioMuted = i2, this.options.micId = t2;
              const { localStream: u2 } = this.options;
              u2.getAudioTracks().forEach(((e2) => e2.stop())), u2.getVideoTracks().forEach(((e2) => l2.addTrack(e2))), this.options.localStream = l2, Le.debug("Finished audio input device change", { callId: this.id, state: this.state, deviceId: t2, desiredAudioMuted: this._desiredAudioMuted, senderTrack: fi(c2.track), localTracks: _i(this.options.localStream) });
            }));
          }
          muteVideo() {
            var e2;
            e2 = this.options.localStream, bi(e2, "video", false);
          }
          unmuteVideo() {
            var e2;
            e2 = this.options.localStream, bi(e2, "video", true);
          }
          toggleVideoMute() {
            var e2;
            e2 = this.options.localStream, bi(e2, "video", null);
          }
          setVideoDevice(e2) {
            return i(this, void 0, void 0, (function* () {
              const { instance: t2 } = this.peer, i2 = t2.getSenders().find((({ track: { kind: e3 } }) => "video" === e3));
              if (i2) {
                const t3 = yield oi({ video: { deviceId: { exact: e2 } } }), n2 = t3.getVideoTracks()[0];
                i2.replaceTrack(n2);
                const { localElement: s2, localStream: o2 } = this.options;
                ai(s2, t3), this.options.camId = e2, o2.getAudioTracks().forEach(((e3) => t3.addTrack(e3))), o2.getVideoTracks().forEach(((e3) => e3.stop())), this.options.localStream = t3, this._applyDesiredAudioMuteState();
              }
            }));
          }
          deaf() {
            Ei(this.options.remoteStream);
          }
          undeaf() {
            Ii(this.options.remoteStream);
          }
          toggleDeaf() {
            var e2;
            e2 = this.options.remoteStream, bi(e2, "audio", null);
          }
          setBandwidthEncodingsMaxBps(e2, t2) {
            return i(this, void 0, void 0, (function* () {
              if (!this || !this.peer) return void Le.error("Could not set bandwidth (reason: no peer connection). Dynamic bandwidth can only be set when there is a call running - is there any call running?)");
              const { instance: i2 } = this.peer, n2 = i2.getSenders();
              if (!n2) return void Le.error("Could not set bandwidth (reason: no senders). Dynamic bandwidth can only be set when there is a call running - is there any call running?)");
              const s2 = n2.find((({ track: { kind: e3 } }) => e3 === t2));
              if (s2) {
                const i3 = s2.getParameters();
                i3.encodings || (i3.encodings = [{ rid: "h" }]), Le.info("Parameters: ", i3), Le.info("Setting max ", "audio" === t2 ? "audio" : "video", " bandwidth to: ", e2, " [bps]"), i3.encodings[0].maxBitrate = e2, yield s2.setParameters(i3).then((() => {
                  Le.info("audio" === t2 ? "New audio" : "New video", " bandwidth settings in use: ", s2.getParameters());
                })).catch(((e3) => Le.error(e3)));
              } else Le.error("Could not set bandwidth (reason: no " + t2 + " sender). Dynamic bandwidth can only be set when there is a call running - is there any call running?)");
            }));
          }
          setAudioBandwidthEncodingsMaxBps(e2) {
            this.setBandwidthEncodingsMaxBps(e2, "audio");
          }
          setVideoBandwidthEncodingsMaxBps(e2) {
            this.setBandwidthEncodingsMaxBps(e2, "video");
          }
          _isTerminatingOrTerminated() {
            return [gt.Hangup, gt.Destroy, gt.Purge].includes(this._state);
          }
          getStats(e2, t2) {
            if (!e2) return;
            const i2 = { callback: e2, constraints: t2 };
            if (this._statsBindings.push(i2), !this._statsIntervalId) {
              const e3 = 2e3;
              this._startStats(e3);
            }
          }
          setState(e2) {
            var t2, i2, n2, s2, o2, r2, a2;
            switch (this._prevState = this._state, this._state = e2, this.state = gt[this._state].toLowerCase(), this.prevState = gt[this._prevState].toLowerCase(), Le.debug(`Call ${this.id} state change from ${this.prevState} to ${this.state}`), this._dispatchNotification({ type: dt.callUpdate, call: this }), e2) {
              case gt.Purge:
                Le.info(`[${this.id}] Entering Purge state.`);
                break;
              case gt.Active:
                if (performance.mark(Ki(this.id, "call-active")), null === (t2 = this.peer) || void 0 === t2 || t2.tryCollectTimings(), this._isRecovering && (this._isRecovering = false, Le.debug(`[${this.id}] Recovery complete, call is active`)), this._wasHeldBeforeRecovery && (Le.debug(`[${this.id}] Cleared held-before-recovery intent on transition to Active`), this._wasHeldBeforeRecovery = false), null === (i2 = this._callReportCollector) || void 0 === i2 || i2.setHeld(false), this.session.startSignalingHealthMonitor(), setTimeout((() => {
                  const { remoteElement: e3, speakerId: t3 } = this.options;
                  e3 && t3 && li(e3, t3);
                }), 0), this._callReportCollector && (null === (n2 = this.peer) || void 0 === n2 ? void 0 : n2.instance) && this.session.callReportId && this._callReportCollector.start(this.peer.instance), this._callRecorder) {
                  const e3 = null === (s2 = this.session.connection) || void 0 === s2 ? void 0 : s2.host;
                  e3 && this._callRecorder._setHost(e3), this.session.callReportId && this._callRecorder._setCallReportId(this.session.callReportId);
                  const t3 = null === (o2 = this.options.localStream) || void 0 === o2 ? void 0 : o2.getAudioTracks()[0], i3 = null === (r2 = this.options.remoteStream) || void 0 === r2 ? void 0 : r2.getAudioTracks()[0];
                  this._callRecorder.start(t3, i3);
                }
                this._mediaDeviceCollector = new Mi(), this._mediaDeviceCollector.logDevicesAtStart();
                break;
              case gt.Held:
                null === (a2 = this._callReportCollector) || void 0 === a2 || a2.setHeld(true);
                break;
              case gt.Destroy:
                this._finalize();
            }
          }
          handleMessage(t2) {
            const { method: i2, params: n2 } = t2;
            switch (i2) {
              case lt.Answer:
                if (performance.mark(Ki(this.id, "telnyx-rtc-answer")), this.gotAnswer = true, n2.telnyx_call_control_id && (this.options.telnyxCallControlId = n2.telnyx_call_control_id), n2.telnyx_session_id && (this.options.telnyxSessionId = n2.telnyx_session_id), n2.telnyx_leg_id && (this.options.telnyxLegId = n2.telnyx_leg_id), this._state >= gt.Active) return;
                this._state >= gt.Early && this.setState(gt.Active), this.gotEarly || this._onRemoteSdp(n2.sdp), this.stopRingback(), this.stopRingtone();
                break;
              case lt.Media:
                if (performance.mark(Ki(this.id, "telnyx-rtc-media")), this._state >= gt.Early) return;
                this.gotEarly = true, this._onRemoteSdp(n2.sdp);
                break;
              case lt.Display: {
                const { display_name: t4, display_number: s2, display_direction: o2 } = n2;
                this.extension = s2;
                const r2 = o2 === ct.Inbound ? ct.Outbound : ct.Inbound, a2 = { type: dt[i2], call: this, displayName: t4, displayNumber: s2, displayDirection: r2 };
                kt(e.SwEvent.Notification, a2, this.id) || kt(e.SwEvent.Notification, a2, this.session.uuid);
                break;
              }
              case lt.Candidate:
                this._addIceCandidate(n2);
                break;
              case lt.Info:
              case lt.Event: {
                const t4 = Object.assign(Object.assign({}, n2), { type: dt.generic, call: this });
                kt(e.SwEvent.Notification, t4, this.id) || kt(e.SwEvent.Notification, t4, this.session.uuid);
                break;
              }
              case lt.Ringing:
                performance.mark(Ki(this.id, "ringing")), this.playRingback(), n2.telnyx_call_control_id && (this.options.telnyxCallControlId = n2.telnyx_call_control_id), n2.telnyx_session_id && (this.options.telnyxSessionId = n2.telnyx_session_id), n2.telnyx_leg_id && (this.options.telnyxLegId = n2.telnyx_leg_id);
                break;
              case lt.Bye:
                const t3 = n2.client_state || n2.clientState;
                t3 && (this.options.clientState = t3), this.stopRingback(), this.stopRingtone(), this.hangup(Object.assign(Object.assign({}, n2), { initiator: "remote:telnyx_rtc.bye" }), false);
            }
          }
          handleConferenceUpdate(e2, t2) {
            return i(this, void 0, void 0, (function* () {
              if (!this._checkConferenceSerno(e2.wireSerno) && e2.name !== t2.laName) return Le.error("ConferenceUpdate invalid wireSerno or packet name:", e2), "INVALID_PACKET";
              const { action: i2, data: n2, hashKey: s2 = String(this._lastSerno), arrIndex: o2 } = e2;
              switch (i2) {
                case "bootObj": {
                  this._lastSerno = 0;
                  const { chatChannel: e3, infoChannel: i3, modChannel: s3, laName: o3, conferenceMemberID: r2, role: a2 } = t2;
                  this._dispatchConferenceUpdate({ action: mt.Join, conferenceName: o3, participantId: Number(r2), role: a2 }), e3 && (yield this._subscribeConferenceChat(e3)), i3 && (yield this._subscribeConferenceInfo(i3));
                  const c2 = [];
                  for (const e4 in n2) c2.push(Object.assign({ callId: n2[e4][0], index: Number(e4) }, Fe(n2[e4][1])));
                  this._dispatchConferenceUpdate({ action: mt.Bootstrap, participants: c2 });
                  break;
                }
                case "add":
                  this._dispatchConferenceUpdate(Object.assign({ action: mt.Add, callId: s2, index: o2 }, Fe(n2)));
                  break;
                case "modify":
                  this._dispatchConferenceUpdate(Object.assign({ action: mt.Modify, callId: s2, index: o2 }, Fe(n2)));
                  break;
                case "del":
                  this._dispatchConferenceUpdate(Object.assign({ action: mt.Delete, callId: s2, index: o2 }, Fe(n2)));
                  break;
                case "clear":
                  this._dispatchConferenceUpdate({ action: mt.Clear });
                  break;
                default:
                  this._dispatchConferenceUpdate({ action: i2, data: n2, callId: s2, index: o2 });
              }
            }));
          }
          _addChannel(e2) {
            this.channels.includes(e2) || this.channels.push(e2);
            const t2 = this.session.relayProtocol;
            this.session._existsSubscription(t2, e2) && (this.session.subscriptions[t2][e2] = Object.assign(Object.assign({}, this.session.subscriptions[t2][e2]), { callId: this.id }));
          }
          _subscribeConferenceChat(e2) {
            return i(this, void 0, void 0, (function* () {
              const t2 = { nodeId: this.nodeId, channels: [e2], handler: (e3) => {
                const { direction: t3, from: i3, fromDisplay: n2, message: s2, type: o2 } = e3.data;
                this._dispatchConferenceUpdate({ action: mt.ChatMessage, direction: t3, participantNumber: i3, participantName: n2, messageText: s2, messageType: o2, messageId: e3.eventSerno });
              } }, i2 = yield this.session.vertoSubscribe(t2).catch(((e3) => {
                Le.error("ConfChat subscription error:", e3);
              }));
              Si(i2, e2) && (this._addChannel(e2), Object.defineProperties(this, { sendChatMessage: { configurable: true, value: (t3, i3) => {
                this.session.vertoBroadcast({ nodeId: this.nodeId, channel: e2, data: { action: "send", message: t3, type: i3 } });
              } } }));
            }));
          }
          _subscribeConferenceInfo(e2) {
            return i(this, void 0, void 0, (function* () {
              const t2 = { nodeId: this.nodeId, channels: [e2], handler: (e3) => {
                const { eventData: t3 } = e3;
                if ("layout-info" === t3.contentType) t3.callID = this.id, Gi(this.session, t3);
                else Le.error("Conference-Info unknown contentType", e3);
              } }, i2 = yield this.session.vertoSubscribe(t2).catch(((e3) => {
                Le.error("ConfInfo subscription error:", e3);
              }));
              Si(i2, e2) && this._addChannel(e2);
            }));
          }
          _confControl(e2, t2 = {}) {
            const i2 = Object.assign({ application: "conf-control", callID: this.id, value: null }, t2);
            this.session.vertoBroadcast({ nodeId: this.nodeId, channel: e2, data: i2 });
          }
          _handleChangeHoldStateSuccess(e2) {
            return "active" === e2.holdState ? this.setState(gt.Active) : this.setState(gt.Held), true;
          }
          _handleChangeHoldStateError(t2) {
            Le.error(`Failed to ${t2.action} on call ${this.id}`);
            const i2 = Ce(S, t2);
            return kt(e.SwEvent.Error, { error: i2, callId: this.id, sessionId: this.session.sessionid }, this.session.uuid), false;
          }
          _sendIceRestartModify(e2, t2 = {}) {
            const n2 = new Qt(Object.assign(Object.assign({ sessid: this.session.sessionid, action: St.UpdateMedia, callID: this.options.id, sdp: e2 }, t2.trickle ? { trickle: true } : {}), { dialogParams: this.options }));
            Le.info(`ICE restart: sending ${t2.trickle ? "trickle " : ""}Modify with new offer SDP`), this._execute(n2).then(((e3) => i(this, void 0, void 0, (function* () {
              var t3;
              (null == e3 ? void 0 : e3.sdp) ? (Le.info("ICE restart Modify response received"), null === (t3 = this.peer) || void 0 === t3 || t3.finishIceRestart(), yield this._onRemoteSdp(e3.sdp)) : this._onIceRestartFailed("ICE restart Modify response missing SDP");
            })))).catch(((e3) => {
              this._onIceRestartFailed("ICE restart Modify failed", e3);
            }));
          }
          _onIceRestartFailed(t2, i2) {
            var n2, s2, o2;
            Le.error(t2, i2), null === (n2 = this.peer) || void 0 === n2 || n2.finishIceRestart();
            const r2 = Ce(D, i2);
            kt(e.SwEvent.Error, { error: r2, callId: this.id, sessionId: this.session.sessionid }, this.session.uuid), null === (o2 = (s2 = this.session).reportIceRestartFailed) || void 0 === o2 || o2.call(s2, this.id);
          }
          _onRemoteSdp(t2) {
            return i(this, void 0, void 0, (function* () {
              const n2 = new RTCSessionDescription({ sdp: t2, type: at.Answer });
              yield this.peer.instance.setRemoteDescription(n2).then((() => {
                performance.mark(Ki(this.id, "set-remote-description")), this.options.trickleIce && (this._isRemoteDescriptionSet = true, this._flushPendingTrickleIceCandidates()), this.gotEarly && this.setState(gt.Early), this.gotAnswer && this._state !== gt.Held && this.setState(gt.Active);
              })).catch(((t3) => i(this, void 0, void 0, (function* () {
                Le.error("Call setRemoteDescription Error: ", t3);
                const i2 = Ce(g, t3);
                kt(e.SwEvent.Error, { error: i2, callId: this.id, sessionId: this.session.sessionid }, this.session.uuid);
                try {
                  yield this.hangup({ cause: "USER_BUSY", causeCode: 17, initiator: "sdk:set-remote-description-failure" }, true);
                } catch (e2) {
                  Le.error("Error during hangup after setRemoteDescription failure:", e2);
                }
              }))));
            }));
          }
          _warnIfOnlyHostIceCandidates(t2) {
            if (!((e2) => {
              if (!e2) return false;
              const t3 = e2.split(/\r?\n/).filter(((e3) => /^a=candidate:/i.test(e3)));
              return t3.length > 0 && t3.every(((e3) => /\styp\s+host(?:\s|$)/i.test(e3)));
            })(t2)) return;
            const i2 = be(J);
            Le.warn(`[${this.id}] Warning ${i2.code}: ${i2.message}`), kt(e.SwEvent.Warning, { warning: i2, callId: this.id, sessionId: this.session.sessionid }, this.session.uuid);
          }
          _onIceSdp(t2) {
            var n2, s2, o2;
            if (this._iceTimeout && clearTimeout(this._iceTimeout), this._iceTimeout = null, this._isTerminatingOrTerminated()) return;
            if (this.peer && (this.peer.iceDone = true), !t2) return void Le.warn("localDescription is null \u2014 PeerConnection may have been closed during ICE gathering");
            const { sdp: r2, type: a2 } = t2;
            null === (s2 = null === (n2 = this.peer) || void 0 === n2 ? void 0 : n2.instance) || void 0 === s2 || s2.removeEventListener("icecandidate", this._onIce), this._warnIfOnlyHostIceCandidates(r2), performance.mark(Ki(this.id, "ice-gathering-end"));
            let c2 = null;
            const l2 = { sessid: this.session.sessionid, sdp: r2, dialogParams: this.options, "User-Agent": `Web-${Qi}` };
            if (null === (o2 = this.peer) || void 0 === o2 ? void 0 : o2.isIceRestarting) this._sendIceRestartModify(r2);
            else {
              switch (a2) {
                case at.Offer:
                  this.setState(gt.Requesting), c2 = new qt(l2);
                  break;
                case at.Answer:
                  this._isRecovering || this.setState(gt.Answering), c2 = true === this.options.attach ? new Kt(l2) : new Yt(l2);
                  break;
                default:
                  return Le.error(`${this.id} - Unknown local SDP type:`, t2), void this.hangup({ initiator: "sdk:unknown-local-sdp-type" }, false);
              }
              performance.mark(Ki(this.id, "send-sdp")), this._execute(c2).then(((e2) => {
                if (this._isTerminatingOrTerminated()) return void Le.debug(`[${this.id}] Ignoring ${a2} response because call is ${this.state}`);
                const { node_id: t3 = null } = e2;
                this._targetNodeId = t3, a2 === at.Offer ? this.setState(gt.Trying) : this._wasHeldBeforeRecovery && this._isRecovering ? this.setState(gt.Held) : this.setState(gt.Active);
              })).catch(((t3) => i(this, void 0, void 0, (function* () {
                Le.error(`${this.id} - Sending ${a2} error:`, t3);
                const i2 = Ce(v, t3);
                kt(e.SwEvent.Error, { error: i2, callId: this.id, sessionId: this.session.sessionid }, this.session.uuid);
                try {
                  yield this.hangup({ cause: "USER_BUSY", causeCode: 17, initiator: "sdk:sdp-send-failure" }, true);
                } catch (e2) {
                  Le.error("Error during hangup after SDP send failure:", e2);
                }
              }))));
            }
          }
          _onTrickleIceSdp(t2) {
            var n2;
            if (this._isTerminatingOrTerminated()) return;
            if (!t2) return Le.error("No SDP data provided"), void this.hangup({ initiator: "sdk:missing-local-sdp" }, false);
            const { sdp: s2, type: o2 } = t2;
            let r2 = null;
            const a2 = { sessid: this.session.sessionid, sdp: s2, dialogParams: this.options, trickle: true, "User-Agent": `Web-${Qi}` };
            if (null === (n2 = this.peer) || void 0 === n2 ? void 0 : n2.isIceRestarting) this._sendIceRestartModify(s2, { trickle: true });
            else {
              switch (o2) {
                case at.Offer:
                  this.setState(gt.Requesting), r2 = new qt(a2);
                  break;
                case at.Answer:
                  this._isRecovering || this.setState(gt.Answering), r2 = true === this.options.attach ? new Kt(a2) : new Yt(a2);
                  break;
                default:
                  return Le.error(`${this.id} - Unknown local SDP type:`, t2), void this.hangup({ initiator: "sdk:unknown-local-sdp-type" }, false);
              }
              performance.mark(Ki(this.id, "send-sdp")), this._execute(r2).then(((e2) => {
                if (this._isTerminatingOrTerminated()) return void Le.debug(`[${this.id}] Ignoring ${o2} response because call is ${this.state}`);
                const { node_id: t3 = null } = e2;
                this._targetNodeId = t3, o2 === at.Offer ? this.setState(gt.Trying) : this._wasHeldBeforeRecovery && this._isRecovering ? this.setState(gt.Held) : this.setState(gt.Active);
              })).catch(((t3) => i(this, void 0, void 0, (function* () {
                Le.error(`${this.id} - Sending ${o2} error:`, t3);
                const i2 = Ce(v, t3);
                kt(e.SwEvent.Error, { error: i2, callId: this.id, sessionId: this.session.sessionid }, this.session.uuid);
                try {
                  yield this.hangup({ cause: "USER_BUSY", causeCode: 17, initiator: "sdk:sdp-send-failure" }, true);
                } catch (e2) {
                  Le.error("Error during hangup after SDP send failure:", e2);
                }
              }))));
            }
          }
          _onIce(e2) {
            var t2;
            if (this._isTerminatingOrTerminated()) return;
            const { instance: i2 } = this.peer;
            if (null === this._iceTimeout) {
              const e3 = this.options.attach ? 5e3 : 1e3;
              this._iceTimeout = setTimeout((() => this._onIceSdp(i2.localDescription)), e3);
            }
            e2.candidate ? (Le.debug("RTCPeer Candidate:", e2.candidate), null === (t2 = this.peer) || void 0 === t2 || t2.incrementGatheredCandidates(), this._trackCandidateMarks(e2.candidate)) : this._onIceSdp(i2.localDescription);
          }
          _onTrickleIce(e2) {
            var t2, i2, n2, s2;
            this._isTerminatingOrTerminated() || (e2.candidate && e2.candidate.candidate ? (Le.debug("RTCPeer Candidate:", e2.candidate), null === (t2 = this.peer) || void 0 === t2 || t2.incrementGatheredCandidates(), this._trackCandidateMarks(e2.candidate), this._sendIceCandidate(e2.candidate)) : (null === e2.candidate && this._warnIfOnlyHostIceCandidates(null === (s2 = null === (n2 = null === (i2 = this.peer) || void 0 === i2 ? void 0 : i2.instance) || void 0 === n2 ? void 0 : n2.localDescription) || void 0 === s2 ? void 0 : s2.sdp), this._sendEndOfCandidates()));
          }
          _sendIceCandidate(e2) {
            const t2 = new zt({ sessid: this.session.sessionid, candidate: e2.candidate, sdpMLineIndex: e2.sdpMLineIndex, sdpMid: e2.sdpMid, dialogParams: this.options });
            this._execute(t2);
          }
          _addIceCandidate(e2) {
            if (!this._isRemoteDescriptionSet) return Le.debug("Remote description not set. Queued ICE candidate.", e2), void this._pendingIceCandidates.push(e2);
            this._addIceCandidateToPeer(e2);
          }
          _addIceCandidateToPeer(e2) {
            const t2 = this.peer.instance.addIceCandidate(e2);
            Promise.resolve(t2).then((() => {
              Le.debug("Successfully added ICE candidate:", e2);
            })).catch(((t3) => {
              Le.error("Failed to add ICE candidate:", t3, e2);
            }));
          }
          _sendEndOfCandidates() {
            const e2 = new Xt({ sessid: this.session.sessionid, endOfCandidates: true, dialogParams: this.options });
            this._execute(e2);
          }
          _trackCandidateMarks(e2) {
            var t2;
            if (this._firstCandidateSent || (performance.mark(Ki(this.id, "first-candidate")), this._firstCandidateSent = true), !this._firstNonHostCandidateSent) {
              const i2 = null === (t2 = e2.candidate.match(/typ (\w+)/)) || void 0 === t2 ? void 0 : t2[1];
              i2 && "host" !== i2 && (performance.mark(Ki(this.id, "first-non-host-candidate")), this._firstNonHostCandidateSent = true);
            }
          }
          _resetTrickleIceCandidateState() {
            this._pendingIceCandidates = [], this._isRemoteDescriptionSet = false, this._firstCandidateSent = false, this._firstNonHostCandidateSent = false;
          }
          _flushPendingTrickleIceCandidates() {
            if (!this._pendingIceCandidates.length) return;
            const e2 = [...this._pendingIceCandidates];
            this._pendingIceCandidates = [], e2.forEach(((e3) => {
              this._addIceCandidateToPeer(e3);
            }));
          }
          _registerPeerEvents(e2) {
            e2.onicecandidate = (e3) => {
              var t2;
              this.options.trickleIce ? this._onTrickleIce(e3) : (null === (t2 = this.peer) || void 0 === t2 ? void 0 : t2.iceDone) || this._onIce(e3);
            }, e2.onicegatheringstatechange = (t2) => {
              Le.debug("ICE gathering state changed:", e2.iceGatheringState, t2), "complete" === e2.iceGatheringState && (Le.debug("Finished gathering candidates"), performance.mark(Ki(this.id, "ice-gathering-completed")));
            }, e2.onicecandidateerror = (t2) => {
              var i2;
              if (Le.debug("ICE candidate error:", t2), null === (i2 = this.peer) || void 0 === i2 ? void 0 : i2.statsReporter) {
                const i3 = (function(e3, t3) {
                  var i4, n2;
                  return { errorCode: e3.errorCode, errorText: e3.errorText, url: e3.url, address: e3.address, port: e3.port, connectionState: t3.connectionState, iceConnectionState: t3.iceConnectionState, iceGatheringState: t3.iceGatheringState, signalingState: t3.signalingState, localDescriptionType: null === (i4 = t3.localDescription) || void 0 === i4 ? void 0 : i4.type, remoteDescriptionType: null === (n2 = t3.remoteDescription) || void 0 === n2 ? void 0 : n2.type };
                })(t2, e2);
                this.peer.statsReporter.reportIceCandidateError(i3);
              }
            }, e2.addEventListener("addstream", ((e3) => {
              this.options.remoteStream = e3.stream;
            })), e2.addEventListener("track", ((e3) => {
              this.options.remoteStream = e3.streams[0];
              const { remoteElement: t2, remoteStream: i2, screenShare: n2 } = this.options;
              false === n2 && ai(t2, i2, { callId: this.id, sessionId: this.session.sessionid, eventTarget: this.session.uuid });
            }));
          }
          _onMediaError(t2) {
            const i2 = (null == t2 ? void 0 : t2.name) || "UnknownError", n2 = (null == t2 ? void 0 : t2.message) || "Unknown media error", s2 = (null == t2 ? void 0 : t2.originalError) || t2;
            this._dispatchNotification({ type: dt.userMediaError, error: s2, call: this, errorName: i2, errorMessage: n2 }), Le.error(`Media error (${i2}): ${n2}`, t2), kt(e.SwEvent.Error, { error: t2, callId: this.id, sessionId: this.session.sessionid }, this.session.uuid), this.hangup({ initiator: "sdk:media-error" }, false);
          }
          _onPeerConnectionFailureError(t2) {
            this._dispatchNotification({ type: dt.peerConnectionFailureError, error: t2.error }), Le.error("Peer connection failure error"), t2.warning && kt(e.SwEvent.Warning, { warning: t2.warning, callId: this.id, sessionId: this.session.sessionid }, this.session.uuid);
          }
          _onPeerConnectionSignalingStateClosed(e2) {
            this._signalingStateClosed = true, this._dispatchNotification(Object.assign({ type: dt.signalingStateClosed }, e2)), Le.debug("Peer connection signaling state closed, call is not recoverable");
          }
          _dispatchConferenceUpdate(e2) {
            this._dispatchNotification(Object.assign({ type: dt.conferenceUpdate, call: this }, e2));
          }
          _dispatchNotification(t2) {
            true !== this.options.screenShare && (kt(e.SwEvent.Notification, t2, this.id, false) || kt(e.SwEvent.Notification, t2, this.session.uuid));
          }
          _execute(e2) {
            return this.nodeId && (e2.targetNodeId = this.nodeId), this.session.execute(e2);
          }
          _registerInboundAnswerAttempt() {
            var t2;
            if (this.options.attach || this._isRecovering) return true;
            const i2 = null === (t2 = this.session.calls) || void 0 === t2 ? void 0 : t2[this.id];
            if (i2 && i2 !== this) {
              const t3 = be(ee);
              return kt(e.SwEvent.Warning, { warning: t3, callId: this.id, activeCallId: i2.id, sessionId: this.session.sessionid }, this.session.uuid), Le.warn(`[${this.id}] answer() ignored: callID ${this.id} already has an active call (${i2.id})`), false;
            }
            return true;
          }
          _init() {
            var t2, i2, n2, s2, o2, r2, a2, l2, d2, u2;
            const { id: h2, userVariables: p2, remoteCallerNumber: g2, onNotification: v2, recoveredCallId: m2, wasHeldBeforeRecovery: f2 } = this.options;
            var _2;
            this.options.id = h2 ? h2.toString() : c(), this.id = this.options.id, m2 && (this.recoveredCallId = m2, this._isRecovering = true, this._wasHeldBeforeRecovery = Boolean(f2)), p2 && (_2 = p2, 0 !== Object.keys(_2).length) || (this.options.userVariables = this.session.options.userVariables || {}), g2 || (this.options.remoteCallerNumber = this.options.destinationNumber), this.session.calls[this.id] = this, Ct(e.SwEvent.MediaError, this._onMediaError, this.id), Ct(e.SwEvent.PeerConnectionFailureError, this._onPeerConnectionFailureError, this.id), Ct(e.SwEvent.PeerConnectionSignalingStateClosed, this._onPeerConnectionSignalingStateClosed, this.id), Be(v2) && Ct(e.SwEvent.Notification, v2.bind(this), this.id);
            const S2 = false !== this.session.options.enableCallReports, y2 = this.session.options.callReportInterval || 5e3, b2 = null !== (t2 = this.session.options.callReportFlushInterval) && void 0 !== t2 ? t2 : 18e4, I2 = this.session.options.debugLogLevel || "debug", E2 = this.session.options.debugLogMaxEntries || 1e3;
            S2 && (this._callReportCollector = new Ni({ enabled: true, interval: y2, intermediateReportInterval: b2 }, { enabled: true, level: I2, maxEntries: E2 }), this._callReportCollector.onFlushNeeded = () => {
              this._flushIntermediateReport();
            }, this._callReportCollector.onWarning = (t3) => {
              var i3, n3, s3, o3;
              kt(e.SwEvent.Warning, { warning: t3, callId: this.id, sessionId: this.session.sessionid }, this.session.uuid), t3.code === j && this._state !== gt.Held ? null === (n3 = (i3 = this.session).reportNoRtp) || void 0 === n3 || n3.call(i3, this.id, "inbound") : t3.code === H && this._state !== gt.Held && this._hasActiveUnmutedLocalAudioTrack() && (null === (o3 = (s3 = this.session).reportNoRtp) || void 0 === o3 || o3.call(s3, this.id, "outbound"));
            });
            if (true === this.session.options.enableCallRecording) {
              const t3 = { enabled: true, flushIntervalMs: null !== (i2 = this.session.options.callRecordingFlushIntervalMs) && void 0 !== i2 ? i2 : 15e3, maxBufferBytes: null !== (n2 = this.session.options.callRecordingMaxBufferBytes) && void 0 !== n2 ? n2 : ht, sampleRate: null !== (s2 = this.session.options.callRecordingSampleRate) && void 0 !== s2 ? s2 : 48e3, tracks: null !== (o2 = this.session.options.callRecordingTracks) && void 0 !== o2 ? o2 : ["local", "remote"], endpoint: null !== (r2 = this.session.options.callRecordingEndpoint) && void 0 !== r2 ? r2 : "/call_recording" }, c2 = { callId: this.id, callReportId: null !== (a2 = this.session.callReportId) && void 0 !== a2 ? a2 : "", voiceSdkId: this._getCallReportVoiceSdkId() };
              this._callRecorder = new Li(t3, c2);
              const d3 = null === (l2 = this.session.connection) || void 0 === l2 ? void 0 : l2.host;
              d3 && this._callRecorder._setHost(d3), this._callRecorder.onWarning = (t4) => {
                kt(e.SwEvent.Warning, { warning: t4, callId: this.id, sessionId: this.session.sessionid }, this.session.uuid);
              };
            }
            this._isRecovering ? this.setState(gt.Recovering) : this.setState(gt.New), Le.info(`New Call \u2014 region: ${null !== (d2 = this.session.region) && void 0 !== d2 ? d2 : "unknown"}, dc: ${null !== (u2 = this.session.dc) && void 0 !== u2 ? u2 : "unknown"}`, this.options);
          }
          _finalize() {
            var t2, i2, n2;
            this._stopStats(), null === (t2 = this._mediaDeviceCollector) || void 0 === t2 || t2.stop(), this._mediaDeviceCollector = null, null === (i2 = this._callRecorder) || void 0 === i2 || i2.stop(), zi(this.id), Le.debug(`[${this.id}] Closing peer from _finalize`), null === (n2 = this.peer) || void 0 === n2 || n2.close();
            const { remoteStream: s2, localStream: o2, remoteElement: r2, localElement: a2 } = this.options;
            ui(s2), ui(o2), ci(r2, s2), ci(a2, o2), Tt(e.SwEvent.MediaError, null, this.id), Tt(e.SwEvent.PeerConnectionFailureError, null, this.id), Tt(e.SwEvent.PeerConnectionSignalingStateClosed, null, this.id), this.session.calls[this.id] = null, delete this.session.calls[this.id];
            const c2 = this._postCallReport().catch(((e2) => {
              Le.error("Unexpected error in _postCallReport", { error: e2 });
            }));
            this.session.trackCallReportUpload(c2);
          }
          _getCallReportVoiceSdkId() {
            return this.session.callReportVoiceSdkId || void 0;
          }
          _getClientSummary() {
            var e2, t2, i2, n2, s2, o2, r2, a2, c2, l2, d2, u2;
            const h2 = this.session.options, p2 = h2.anonymous_login;
            return { authentication: Object.assign({ type: this._getAuthenticationType() }, p2 ? { anonymousLogin: { targetType: p2.target_type, targetId: p2.target_id, targetVersionId: p2.target_version_id, targetParams: this._sanitizeClientOption(p2.target_params) } } : {}), connection: { env: h2.env, host: h2.host, project: h2.project, region: null !== (e2 = this.session.region) && void 0 !== e2 ? e2 : h2.region, dc: this.session.dc, rtcIp: h2.rtcIp, rtcPort: h2.rtcPort, autoReconnect: null === (t2 = h2.autoReconnect) || void 0 === t2 || t2, maxReconnectAttempts: null !== (i2 = h2.maxReconnectAttempts) && void 0 !== i2 ? i2 : 10, keepConnectionAliveOnSocketClose: null !== (n2 = h2.keepConnectionAliveOnSocketClose) && void 0 !== n2 && n2, hangupOnBeforeUnload: false !== h2.hangupOnBeforeUnload, useCanaryRtcServer: null !== (s2 = h2.useCanaryRtcServer) && void 0 !== s2 && s2, skipLastVoiceSdkId: null !== (o2 = h2.skipLastVoiceSdkId) && void 0 !== o2 && o2, skipTrailing: null !== (r2 = h2.skipTrailing) && void 0 !== r2 && r2 }, media: { audio: this._sanitizeClientOption(this.options.audio), video: this._sanitizeClientOption(this.options.video), mutedMicOnStart: null !== (a2 = this.options.mutedMicOnStart) && void 0 !== a2 && a2, prefetchIceCandidates: null === (c2 = this.options.prefetchIceCandidates) || void 0 === c2 || c2, forceRelayCandidate: null !== (l2 = this.options.forceRelayCandidate) && void 0 !== l2 && l2, trickleIce: null !== (d2 = this.options.trickleIce) && void 0 !== d2 && d2, iceServers: this._sanitizeIceServers(this.options.iceServers) }, callReports: { enabled: false !== h2.enableCallReports, intervalMs: h2.callReportInterval || 5e3, flushIntervalMs: null !== (u2 = h2.callReportFlushInterval) && void 0 !== u2 ? u2 : 18e4, debugLogLevel: h2.debugLogLevel || "debug", debugLogMaxEntries: h2.debugLogMaxEntries || 1e3 } };
          }
          _getAuthenticationType() {
            const e2 = this.session.options;
            return e2.anonymous_login ? "anonymous_login" : e2.login_token ? "login_token" : e2.login && (e2.password || e2.passwd) ? "login_password" : e2.token ? "token" : "unknown";
          }
          _sanitizeIceServers(e2) {
            if (e2 && 0 !== e2.length) return e2.map((({ urls: e3, username: t2, credential: i2 }) => ({ urls: e3, hasUsername: Boolean(t2), hasCredential: Boolean(i2) })));
          }
          _sanitizeClientOption(e2, t2 = 0) {
            if (void 0 !== e2) {
              if (null === e2) return null;
              if ("string" == typeof e2) return e2;
              if ("number" == typeof e2) return Number.isFinite(e2) ? e2 : void 0;
              if ("boolean" == typeof e2) return e2;
              if ("function" != typeof e2) {
                if (t2 >= 4) return "[truncated]";
                if (Array.isArray(e2)) return e2.map(((e3) => this._sanitizeClientOption(e3, t2 + 1))).filter(((e3) => void 0 !== e3));
                if ("object" == typeof e2) {
                  const i2 = Object.entries(e2).filter((([e3]) => !this._isSensitiveClientOptionKey(e3))).map((([e3, i3]) => [e3, this._sanitizeClientOption(i3, t2 + 1)])).filter(((e3) => void 0 !== e3[1]));
                  return Object.fromEntries(i2);
                }
              }
            }
          }
          _isSensitiveClientOptionKey(e2) {
            return /(password|passwd|credential|secret|token|authorization|auth|api[_-]?key)/i.test(e2);
          }
          flushIntermediateCallReport(e2 = { type: "manual" }) {
            this._flushIntermediateReport(e2);
          }
          _flushIntermediateReport(e2 = { type: "buffer-limit" }) {
            var t2;
            if (!this._callReportCollector) return;
            const i2 = this.session.callReportId;
            if (!i2) return void Le.debug("Cannot flush intermediate report: call_report_id not available");
            const n2 = null === (t2 = this.session.connection) || void 0 === t2 ? void 0 : t2.host;
            if (!n2) return void Le.debug("Cannot flush intermediate report: connection host not available");
            const s2 = { callId: this.id, destinationNumber: this.options.destinationNumber, callerNumber: this.options.callerNumber, direction: this.direction === ct.Inbound ? "inbound" : "outbound", state: this.state, telnyxSessionId: this.options.telnyxSessionId, telnyxLegId: this.options.telnyxLegId, sdkVersion: Qi, clientSummary: this._getClientSummary() }, o2 = this._callReportCollector.flush(s2, e2);
            if (!o2) return;
            Le.info("Flushing intermediate call report", { callId: this.id, flushReason: e2, segment: o2.segment });
            const r2 = this._getCallReportVoiceSdkId(), a2 = "page-unload" === e2.type, c2 = this._callReportCollector.sendPayload(o2, i2, n2, r2, a2).catch(((e3) => {
              Le.error("Failed to post intermediate call report segment", { error: e3 });
            }));
            this.session.trackCallReportUpload(c2);
          }
          _postCallReport() {
            var e2, t2, n2, s2;
            return i(this, void 0, void 0, (function* () {
              if (!this._callReportCollector) return void Le.warn("Call report collector not initialized");
              if (yield this._callReportCollector.stop(), this._callRecorder) {
                this._callRecorder.stop();
                const t3 = null === (e2 = this.session.connection) || void 0 === e2 ? void 0 : e2.host;
                t3 && this._callRecorder._setHost(t3), this.session.callReportId && this._callRecorder._setCallReportId(this.session.callReportId);
                const i3 = this._callRecorder.postFinalReport().catch(((e3) => {
                  Le.error("Failed to post final call recording", { error: e3 });
                })).finally((() => {
                  var e3;
                  null === (e3 = this._callRecorder) || void 0 === e3 || e3.cleanup();
                }));
                this.session.trackCallReportUpload(i3);
              }
              const i2 = this.session.callReportId;
              if (!i2) return Le.debug("Cannot post call report: call_report_id not available"), void this._callReportCollector.cleanup();
              const o2 = { callId: this.id, destinationNumber: this.options.destinationNumber, callerNumber: this.options.callerNumber, direction: this.direction === ct.Inbound ? "inbound" : "outbound", state: this.state, telnyxSessionId: this.options.telnyxSessionId, telnyxLegId: this.options.telnyxLegId, sdkVersion: Qi, clientSummary: this._getClientSummary() }, r2 = null === (t2 = this.session.connection) || void 0 === t2 ? void 0 : t2.host;
              if (!r2) return void Le.error("Cannot post call report: connection host not available");
              const a2 = this._getCallReportVoiceSdkId();
              try {
                yield this._callReportCollector.postReport(o2, i2, r2, a2);
              } catch (e3) {
                throw Le.error("Failed to post call report", { error: e3 }), e3;
              } finally {
                null === (n2 = this._callReportCollector) || void 0 === n2 || n2.cleanup(), null === (s2 = this._callRecorder) || void 0 === s2 || s2.cleanup();
              }
            }));
          }
          _startStats(e2) {
            this._statsIntervalId = setInterval(this._doStats, e2), Le.info("Stats started");
          }
          _stopStats() {
            this._statsIntervalId && (clearInterval(this._statsIntervalId), this._statsIntervalId = null), Le.debug("Stats stopped");
          }
        }
        Zi.setStateTelnyx = (e2) => {
          if (e2) {
            switch (e2._state) {
              case gt.Recovering:
                e2.state = "recovering";
                break;
              case gt.Requesting:
              case gt.Trying:
              case gt.Early:
                e2.state = "connecting";
                break;
              case gt.Active:
                e2.state = "active";
                break;
              case gt.Held:
                e2.state = "held";
                break;
              case gt.Hangup:
              case gt.Destroy:
                e2.state = "done";
                break;
              case gt.Answering:
                e2.state = "ringing";
                break;
              case gt.New:
                e2.state = "new";
            }
            return e2;
          }
        };
        class en extends Zi {
          constructor() {
            super(...arguments), this._statsInterval = null, this.sendConversationMessage = (e2, t2) => this.session.execute(new Oi(e2, t2)), this.sendAIConversationMessage = (e2) => {
              if (!this.session.connected) throw new Error("Cannot send AI conversation message: session is not connected. sendAIConversationMessage requires an active WebSocket connection.");
              const t2 = new Di(e2);
              this.session.connection.sendRawText(JSON.stringify(t2.request));
            };
          }
          hangup(e2 = {}, t2 = true) {
            const n2 = Object.create(null, { hangup: { get: () => super.hangup } });
            return i(this, void 0, void 0, (function* () {
              this.screenShare instanceof en && (yield this.screenShare.hangup(e2, t2)), yield n2.hangup.call(this, e2, t2);
            }));
          }
          startScreenShare(e2) {
            return i(this, void 0, void 0, (function* () {
              const t2 = yield (n2 = { video: true }, navigator.mediaDevices.getDisplayMedia(n2));
              var n2;
              t2.getTracks().forEach(((e3) => {
                e3.addEventListener("ended", (() => i(this, void 0, void 0, (function* () {
                  this.screenShare && (yield this.screenShare.hangup({ initiator: "sdk:screenshare-track-ended" }));
                }))));
              }));
              const { remoteCallerName: s2, remoteCallerNumber: o2, callerName: r2, callerNumber: a2 } = this.options, c2 = Object.assign({ screenShare: true, localStream: t2, destinationNumber: `${this.extension}-screen`, remoteCallerName: s2, remoteCallerNumber: `${o2}-screen`, callerName: `${r2} (Screen)`, callerNumber: `${a2} (Screen)` }, e2);
              return this.screenShare = new en(this.session, c2), this.screenShare.invite(), this.screenShare;
            }));
          }
          stopScreenShare() {
            return i(this, void 0, void 0, (function* () {
              this.screenShare instanceof en && (yield this.screenShare.hangup({ initiator: "app:stopScreenShare" }));
            }));
          }
          setAudioOutDevice(e2) {
            return i(this, void 0, void 0, (function* () {
              this.options.speakerId = e2;
              const { remoteElement: t2, speakerId: i2 } = this.options;
              return !(!t2 || !i2) && li(t2, i2);
            }));
          }
          _finalize() {
            this._stats(false), super._finalize();
          }
          _stats(e2 = true) {
            if (false === e2) return clearInterval(this._statsInterval);
            this._statsInterval = window.setInterval((() => i(this, void 0, void 0, (function* () {
              const e3 = yield this.peer.instance.getStats(null);
              let t2 = "";
              const i2 = ["certificate", "codec", "peer-connection", "stream", "local-candidate", "remote-candidate"], n2 = ["id", "type", "timestamp"];
              e3.forEach(((e4) => {
                i2.includes(e4.type) || (t2 += `
${e4.type}
`, Object.keys(e4).forEach(((i3) => {
                  n2.includes(i3) || (t2 += `	${i3}: ${e4[i3]}
`);
                })));
              })), Le.info(t2);
            }))), 2e3);
          }
        }
        class tn extends si {
          constructor(e2) {
            super(e2), this.calls = {}, this.autoRecoverCalls = true, this._iceServers = [], this._localElement = null, this._localElementId = null, this._remoteElement = null, this._remoteElementId = null, this._jwtAuth = true, this._audioConstraints = true, this._previousAudioConstraints = true, this._videoConstraints = false, this._speaker = null, this._videoConstraints = e2.video || false, this.iceServers = e2.iceServers, this.ringtoneFile = e2.ringtoneFile, this.ringbackFile = e2.ringbackFile;
          }
          getActiveCalls() {
            return Object.values(this.calls).filter(((e2) => tn.ACTIVE_CALL_STATE_NAMES.has(e2.state)));
          }
          _extractSafeCallIdentifiers(e2) {
            var t2, i2;
            const n2 = { callId: e2.id, state: e2.state, direction: e2.direction };
            (null === (t2 = e2.options) || void 0 === t2 ? void 0 : t2.telnyxSessionId) && (n2.telnyxSessionId = e2.options.telnyxSessionId), (null === (i2 = e2.options) || void 0 === i2 ? void 0 : i2.telnyxLegId) && (n2.telnyxLegId = e2.options.telnyxLegId);
            const s2 = e2.sipCallId;
            return "string" == typeof s2 && s2 && (n2.sipCallId = s2), n2;
          }
          emitMultipleActiveCallsWarning(t2) {
            const i2 = this.getActiveCalls().filter(((e2) => e2.id !== t2));
            if (0 === i2.length) return;
            const n2 = be(Z), s2 = this.calls[t2], o2 = s2 ? this._extractSafeCallIdentifiers(s2) : { callId: t2 };
            kt(e.SwEvent.Warning, { warning: n2, callId: t2, sessionId: this.sessionid, newCall: o2, activeCalls: i2.map(((e2) => this._extractSafeCallIdentifiers(e2))) }, this.uuid), Le.warn(`MULTIPLE_ACTIVE_CALLS_DETECTED: new call ${t2} created while ${i2.length} other call(s) are active in session ${this.sessionid}`);
          }
          getIsRegistered() {
            const e2 = Object.create(null, { getIsRegistered: { get: () => super.getIsRegistered } });
            return i(this, void 0, void 0, (function* () {
              return e2.getIsRegistered.call(this);
            }));
          }
          connect() {
            const e2 = Object.create(null, { connect: { get: () => super.connect } });
            return i(this, void 0, void 0, (function* () {
              Le.debug("BrowserSession.connect() called"), e2.connect.call(this);
            }));
          }
          checkPermissions(e2 = true, t2 = true) {
            return i(this, void 0, void 0, (function* () {
              try {
                const i2 = yield hi({ audio: e2, video: t2 });
                return ui(i2), true;
              } catch (e3) {
                return false;
              }
            }));
          }
          logout() {
            this.disconnect();
          }
          disconnect() {
            const e2 = Object.create(null, { disconnect: { get: () => super.disconnect } });
            return i(this, void 0, void 0, (function* () {
              Le.info("[disconnect] Client-initiated disconnect \u2014 setting Purge with BYE on all active calls.");
              for (const e3 in this.calls) {
                const t2 = this.calls[e3];
                t2.setState(gt.Purge), Le.info("Start hangup for ", t2), yield t2.hangup({ initiator: "app:client.disconnect" }, true);
              }
              this.calls = {}, yield e2.disconnect.call(this);
            }));
          }
          serverDisconnect() {
            const e2 = Object.create(null, { disconnect: { get: () => super.disconnect } });
            return i(this, void 0, void 0, (function* () {
              Le.info("[serverDisconnect] Server-initiated disconnect \u2014 setting Purge without BYE on all active calls.");
              for (const e3 in this.calls) {
                const t2 = this.calls[e3];
                t2.setState(gt.Purge), t2.hangup({ initiator: "sdk:server-disconnect" }, false);
              }
              this.calls = {}, yield e2.disconnect.call(this);
            }));
          }
          socketDisconnect() {
            this._closeConnection();
          }
          handleLoginError(e2) {
            super._handleLoginError(e2);
          }
          speedTest(t2) {
            return new Promise(((i2, n2) => {
              if (wt(e.SwEvent.SpeedTest, ((e2) => {
                const { upDur: n3, downDur: s3 } = e2, o3 = s3 ? 8 * t2 / (s3 / 1e3) / 1024 : 0;
                i2({ upDur: n3, downDur: s3, upKps: (n3 ? 8 * t2 / (n3 / 1e3) / 1024 : 0).toFixed(0), downKps: o3.toFixed(0) });
              }), this.uuid), !(t2 = Number(t2))) return n2(`Invalid parameter 'bytes': ${t2}`);
              this.executeRaw(`#SPU ${t2}`);
              let s2 = t2 / 1024;
              t2 % 1024 && s2++;
              const o2 = ".".repeat(1024);
              for (let e2 = 0; e2 < s2; e2++) this.executeRaw(`#SPB ${o2}`);
              this.executeRaw("#SPE");
            }));
          }
          getDevices() {
            return pi().catch(((t2) => {
              const i2 = Ce(Ee(t2), t2);
              return kt(e.SwEvent.MediaError, i2, this.uuid), [];
            }));
          }
          getVideoDevices() {
            return pi(ft.Video).catch(((t2) => (kt(e.SwEvent.MediaError, t2, this.uuid), [])));
          }
          getAudioInDevices() {
            return pi(ft.AudioIn).catch(((t2) => {
              const i2 = Ce(Ee(t2), t2);
              return kt(e.SwEvent.MediaError, i2, this.uuid), [];
            }));
          }
          getAudioOutDevices() {
            return pi(ft.AudioOut).catch(((t2) => (Le.error("getAudioOutDevices", t2), kt(e.SwEvent.MediaError, t2, this.uuid), [])));
          }
          validateDeviceId(e2, t2, i2) {
            return vi(e2, t2, i2);
          }
          getDeviceResolutions(e2) {
            return i(this, void 0, void 0, (function* () {
              try {
                return yield ((e3) => i(void 0, void 0, void 0, (function* () {
                  const t2 = [], i2 = yield hi({ video: { deviceId: { exact: e3 } } }), n2 = i2.getVideoTracks()[0];
                  for (let e4 = 0; e4 < gi.length; e4++) {
                    const [i3, s2] = gi[e4];
                    (yield n2.applyConstraints({ width: { exact: i3 }, height: { exact: s2 } }).then((() => true)).catch((() => false))) && t2.push({ resolution: `${i3}x${s2}`, width: i3, height: s2 });
                  }
                  return ui(i2), t2;
                })))(e2);
              } catch (e3) {
                throw e3;
              }
            }));
          }
          get mediaConstraints() {
            return { audio: this._audioConstraints, video: this._videoConstraints };
          }
          setAudioSettings(e2) {
            return i(this, void 0, void 0, (function* () {
              if (!e2) throw new Error("You need to provide the settings object");
              const { micId: n2, micLabel: s2 } = e2, o2 = t(e2, ["micId", "micLabel"]);
              return mi(o2), this._audioConstraints = yield ((e3, t2, n3, s3) => i(void 0, void 0, void 0, (function* () {
                const { deviceId: i2 } = s3;
                if (void 0 === i2 && (e3 || t2)) {
                  const i3 = yield vi(e3, t2, n3).catch((() => null));
                  i3 && (s3.deviceId = { exact: i3 });
                }
                return s3;
              })))(n2, s2, "audioinput", o2), this.micId = n2, this.micLabel = s2, this._audioConstraints;
            }));
          }
          disableMicrophone() {
            this._previousAudioConstraints = this._audioConstraints, this._audioConstraints = false;
          }
          enableMicrophone() {
            this._audioConstraints = this._previousAudioConstraints || true;
          }
          set iceServers(e2) {
            if (e2 && Array.isArray(e2)) this._iceServers = e2;
            else {
              const e3 = "development" === this.options.env;
              this._iceServers = e3 ? me : ve;
            }
          }
          get iceServers() {
            return this._iceServers;
          }
          set speaker(e2) {
            this._speaker = e2;
          }
          get speaker() {
            return this._speaker;
          }
          set localElement(e2) {
            this._localElementId = "string" == typeof e2 ? e2 : null, this._localElement = je(e2);
          }
          get localElement() {
            return this._localElement;
          }
          get localElementId() {
            return this._localElementId;
          }
          set remoteElement(e2) {
            this._remoteElementId = "string" == typeof e2 ? e2 : null, this._remoteElement = je(e2);
          }
          get remoteElement() {
            return this._remoteElement;
          }
          get remoteElementId() {
            return this._remoteElementId;
          }
          vertoBroadcast({ nodeId: e2, channel: t2 = "", data: i2 }) {
            if (!t2) throw new Error(`Invalid channel for broadcast: ${t2}`);
            const n2 = new ei({ sessid: this.sessionid, eventChannel: t2, data: i2 });
            e2 && (n2.targetNodeId = e2), this.execute(n2).catch(((e3) => e3));
          }
          vertoSubscribe({ nodeId: e2, channels: t2 = [], handler: n2 }) {
            return i(this, void 0, void 0, (function* () {
              if (!(t2 = t2.filter(((e3) => e3 && !this._existsSubscription(this.relayProtocol, e3)))).length) return {};
              const i2 = new ti({ sessid: this.sessionid, eventChannel: t2 });
              e2 && (i2.targetNodeId = e2);
              const s2 = yield this.execute(i2), { unauthorized: o2 = [], subscribed: r2 = [] } = yi(s2);
              return o2.length && o2.forEach(((e3) => this._removeSubscription(this.relayProtocol, e3))), r2.forEach(((e3) => this._addSubscription(this.relayProtocol, n2, e3))), s2;
            }));
          }
          vertoUnsubscribe({ nodeId: e2, channels: t2 = [] }) {
            return i(this, void 0, void 0, (function* () {
              if (!(t2 = t2.filter(((e3) => e3 && this._existsSubscription(this.relayProtocol, e3)))).length) return {};
              const i2 = new ii({ sessid: this.sessionid, eventChannel: t2 });
              e2 && (i2.targetNodeId = e2);
              const n2 = yield this.execute(i2), { unsubscribed: s2 = [], notSubscribed: o2 = [] } = yi(n2);
              return s2.forEach(((e3) => this._removeSubscription(this.relayProtocol, e3))), o2.forEach(((e3) => this._removeSubscription(this.relayProtocol, e3))), n2;
            }));
          }
          static telnyxStateCall(e2) {
            return en.setStateTelnyx(e2);
          }
        }
        tn.ACTIVE_CALL_STATE_NAMES = /* @__PURE__ */ new Set(["new", "requesting", "trying", "recovering", "ringing", "answering", "early", "active", "held"]);
        class nn {
          constructor(e2, t2) {
            this.code = t2, this.message = e2;
          }
        }
        class sn {
          constructor(e2) {
            this.session = e2, this.retriedConnect = 0, this.retriedRegister = 0;
          }
          _ack(e2, t2) {
            const i2 = new Vt(e2, t2);
            this.nodeId && (i2.targetNodeId = this.nodeId), this.session.execute(i2);
          }
          reconnectDelay() {
            return 1e3 * (e2 = 2, t2 = 6, Math.floor(Math.random() * (t2 - e2 + 1) + e2));
            var e2, t2;
          }
          handleMessage(t2) {
            var i2, n2, s2, o2, r2, a2, c2, l2, d2;
            const { session: u2 } = this;
            u2.setPingReceived();
            const { id: h2, method: p2, params: g2 = {}, voice_sdk_id: v2 } = t2, m2 = null == g2 ? void 0 : g2.callID, f2 = null == g2 ? void 0 : g2.eventChannel, _2 = null == g2 ? void 0 : g2.eventType, S2 = u2.calls[m2], y2 = null === (i2 = null == S2 ? void 0 : S2.peer) || void 0 === i2 ? void 0 : i2.isConnectionHealthy(), b2 = new Set(Object.keys(u2.calls));
            if (Array.isArray(null == g2 ? void 0 : g2.reattached_sessions) && b2.size) {
              Le.debug(`Reattach: active call IDs before cleanup check: [${Array.from(b2).join(", ")}].`);
              const t3 = 0 === g2.reattached_sessions.length, i3 = g2.reattached_sessions.some(((e2) => b2.has(e2)));
              if (t3 || !i3) for (const t4 of Object.keys(u2.calls)) {
                const i4 = u2.calls[t4];
                Le.debug(`Session not reattached \u2014 terminating active call ${t4} `);
                const n3 = Ce(L);
                kt(e.SwEvent.Error, { error: n3, callId: t4, sessionId: u2.sessionid }, u2.uuid), i4.hangup({}, false);
              }
            }
            if (Array.isArray(null == g2 ? void 0 : g2.reattached_sessions) && u2.sessionid) {
              const t3 = ot();
              if (t3 && t3.calls.length > 0) if (t3.sessionId !== u2.sessionid) Le.debug(`Recovery markers were saved for a different sessid (saved=${t3.sessionId}, current=${u2.sessionid}) \u2014 ignoring all.`), st();
              else {
                const i3 = new Set(g2.reattached_sessions), n3 = [];
                for (const s3 of t3.calls) try {
                  if (i3.has(s3.id)) {
                    Le.debug(`Recovery marker for call ${s3.id} was reattached \u2014 keeping for Attach element restore.`), n3.push(s3);
                    continue;
                  }
                  Le.info(`Recovery marker for call ${s3.id} (sessid=${u2.sessionid}) was not reattached \u2014 emitting SESSION_NOT_REATTACHED.`);
                  const t4 = Ce(L);
                  kt(e.SwEvent.Error, { error: t4, callId: s3.id, sessionId: u2.sessionid, customHeaders: s3.customHeaders }, u2.uuid);
                } catch (e2) {
                  Le.debug(`Recovery marker for a saved call failed to process (callId=${null == s3 ? void 0 : s3.id}) \u2014 skipping: ${e2 instanceof Error ? e2.message : String(e2)}`), n3.push(s3);
                }
                rt(n3, t3.sessionId, t3.storedAt);
              }
            }
            if ("channelPvtData" === _2) return this._handlePvtEvent(g2.pvtData);
            const I2 = ({ recoveredCallId: e2, forceRelayCandidateForRecovery: t3, mutedMicOnStart: i3, remoteElement: n3, localElement: s3, wasHeldBeforeRecovery: o3 } = {}) => {
              var r3, a3, c3, l3, d3, h3;
              const v3 = { audio: true, video: u2.options.video, remoteSdp: g2.sdp, destinationNumber: g2.callee_id_number, remoteCallerName: g2.caller_id_name, remoteCallerNumber: g2.caller_id_number, callerName: g2.callee_id_name, callerNumber: g2.callee_id_number, attach: p2 === lt.Attach, mediaSettings: g2.mediaSettings, debug: null !== (r3 = u2.options.debug) && void 0 !== r3 && r3, debugOutput: null !== (a3 = u2.options.debugOutput) && void 0 !== a3 ? a3 : "socket", trickleIce: null !== (c3 = u2.options.trickleIce) && void 0 !== c3 && c3, prefetchIceCandidates: null === (l3 = u2.options.prefetchIceCandidates) || void 0 === l3 || l3, forceRelayCandidate: "boolean" == typeof t3 ? t3 : null !== (d3 = u2.options.forceRelayCandidate) && void 0 !== d3 && d3, keepConnectionAliveOnSocketClose: null !== (h3 = u2.options.keepConnectionAliveOnSocketClose) && void 0 !== h3 && h3, mutedMicOnStart: null != i3 ? i3 : u2.options.mutedMicOnStart };
              m2 && (v3.id = m2), g2.telnyx_call_control_id && (v3.telnyxCallControlId = g2.telnyx_call_control_id), g2.telnyx_session_id && (v3.telnyxSessionId = g2.telnyx_session_id), g2.telnyx_leg_id && (v3.telnyxLegId = g2.telnyx_leg_id), g2.client_state && (v3.clientState = g2.client_state), g2.dialogParams && g2.dialogParams.custom_headers && g2.dialogParams.custom_headers.length && (v3.customHeaders = g2.dialogParams.custom_headers), e2 && (v3.recoveredCallId = e2), o3 && (v3.wasHeldBeforeRecovery = o3), void 0 !== n3 && (v3.remoteElement = n3), void 0 !== s3 && (v3.localElement = s3), performance.mark(Ki(v3.id, "new-call-start"));
              const f3 = new en(u2, v3);
              return f3.nodeId = this.nodeId, f3;
            }, E2 = new Ft(v2), C2 = new Bt(v2);
            switch (p2) {
              case lt.Answer:
              case lt.Display:
              case lt.Candidate:
              case lt.Ringing:
              case lt.Bye:
              case lt.Media:
                if (!m2 || !S2) return void Le.error(`Received ${p2} for non existing call:`, g2);
                S2.handleMessage(t2), this._ack(h2, p2);
                break;
              case lt.Ping:
                this.session.setPingReceived(), this.session.execute(C2);
                break;
              case lt.Punt:
                u2.options.keepConnectionAliveOnSocketClose && y2 ? (Le.info("[punt] Received PUNT from server. keepConnectionAliveOnSocketClose=true \u2014 disconnecting socket only, keeping calls alive."), u2.socketDisconnect(), this._ack(h2, p2)) : (Le.info("[punt] Received PUNT from server \u2014 calling serverDisconnect() to purge all calls without BYE."), u2.serverDisconnect());
                break;
              case lt.Invite: {
                const e2 = I2();
                e2.direction = ct.Inbound, e2.playRingtone(), e2.setState(gt.Ringing), this.session.emitMultipleActiveCallsWarning(e2.id), this._ack(h2, p2);
                break;
              }
              case lt.Attach: {
                const t3 = S2 || null;
                if (0 === Object.keys(u2.calls).length) {
                  let e2, t4;
                  Le.warn(`[${(/* @__PURE__ */ new Date()).toISOString()}][${m2}] Attach: SDK doens't have any active call therefore we recover first arrived attach session ${m2}`);
                  let i4 = false, n3 = false;
                  const s3 = ot();
                  if (s3 && s3.sessionId === u2.sessionid) {
                    const o4 = s3.calls.find(((e3) => null !== e3 && "object" == typeof e3 && e3.id === m2));
                    o4 && (e2 = o4.remoteElement, t4 = o4.localElement, i4 = true === o4.wasHeld, "boolean" == typeof o4.forceRelayCandidate && (n3 = o4.forceRelayCandidate), (e2 || t4) && Le.info(`[${m2}] Attach: restoring per-call media elements from recovery marker (remoteElement=${null != e2 ? e2 : "<none>"}, localElement=${null != t4 ? t4 : "<none>"}).`), "boolean" == typeof o4.forceRelayCandidate && Le.info(`[${m2}] Attach: restoring forceRelayCandidate=${o4.forceRelayCandidate} from recovery marker.`));
                  }
                  const o3 = I2({ recoveredCallId: m2, forceRelayCandidateForRecovery: n3, remoteElement: e2, localElement: t4, wasHeldBeforeRecovery: i4 });
                  o3.answer(), this.session.emitMultipleActiveCallsWarning(o3.id), this._ack(h2, p2);
                  break;
                }
                if (t3) {
                  const e2 = t3.id, i4 = "held" === t3.state, n3 = t3.shouldForceRelayCandidateForRecovery();
                  n3 && Le.warn(`[${(/* @__PURE__ */ new Date()).toISOString()}][${m2}] Attach: forcing relay candidate for recovery`), Le.info(`[${(/* @__PURE__ */ new Date()).toISOString()}][${m2}] Attach: recovering active call ${e2}.`), t3.hangup({ isRecovering: true, initiator: "sdk:attach-recovery" }, false);
                  const s3 = I2({ recoveredCallId: e2, forceRelayCandidateForRecovery: n3, mutedMicOnStart: t3.isAudioMuted, remoteElement: t3.options.remoteElement, localElement: t3.options.localElement, wasHeldBeforeRecovery: i4 });
                  s3.answer(), this.session.emitMultipleActiveCallsWarning(s3.id), this._ack(h2, p2);
                  break;
                }
                Le.warn(`Attach: callID ${m2} does not match any active calls. `), kt(e.SwEvent.Warning, { warning: be(ne), callId: m2, params: g2, sessionId: u2.sessionid }, u2.uuid), this._ack(h2, p2);
                break;
              }
              case lt.Event:
              case "webrtc.event":
                if (!f2) return void Le.error("Verto received an unknown event:", g2);
                const i3 = u2.relayProtocol, _3 = f2.split(".")[0];
                u2._existsSubscription(i3, f2) ? kt(i3, g2, f2) : f2 === u2.sessionid ? this._handleSessionEvent(g2.eventData) : u2._existsSubscription(i3, _3) ? kt(i3, g2, _3) : u2.calls.hasOwnProperty(f2) ? u2.calls[f2].handleMessage(t2) : kt(e.SwEvent.Notification, g2, u2.uuid);
                break;
              case lt.Info:
                g2.type = dt.generic, kt(e.SwEvent.Notification, g2, u2.uuid);
                break;
              case lt.ClientReady:
                this.session.execute(E2);
                break;
              case "ai_conversation":
                kt(e.SwEvent.AIConversationMessage, { method: "ai_conversation", params: g2, voice_sdk_id: v2 }, u2.uuid);
                break;
              default: {
                const i4 = qe(t2);
                if (i4) {
                  switch (i4) {
                    case _t.REGISTER:
                    case _t.REGED:
                      if (!this.isDuplicateGatewayState(i4, [_t.REGED, _t.REGISTER])) {
                        this.session._triggerKeepAliveTimeoutCheck(), this.retriedRegister = 0, i4 === _t.REGED && this.session.resetReconnectAttempts();
                        const h3 = null === (s2 = null === (n2 = null == t2 ? void 0 : t2.result) || void 0 === n2 ? void 0 : n2.params) || void 0 === s2 ? void 0 : s2.call_report_id;
                        h3 && (u2.callReportId = h3, Le.debug("Captured call_report_id from REGED:", h3));
                        const p3 = null === (r2 = null === (o2 = null == t2 ? void 0 : t2.result) || void 0 === o2 ? void 0 : o2.params) || void 0 === r2 ? void 0 : r2.dc;
                        p3 && (u2.dc = p3);
                        const v3 = null === (c2 = null === (a2 = null == t2 ? void 0 : t2.result) || void 0 === a2 ? void 0 : a2.params) || void 0 === c2 ? void 0 : c2.region;
                        v3 && (u2.region = v3), Le.info(`Connected to Telnyx \u2014 region: ${null !== (l2 = u2.region) && void 0 !== l2 ? l2 : "unknown"}, dc: ${null !== (d2 = u2.dc) && void 0 !== d2 ? d2 : "unknown"}`), g2.type = dt.vertoClientReady, kt(e.SwEvent.Ready, g2, u2.uuid);
                      }
                      break;
                    case _t.UNREGED:
                    case _t.NOREG:
                      if (this.retriedRegister += 1, 5 === this.retriedRegister) {
                        this.retriedRegister = 0;
                        const t3 = new nn("Fail to register the user, the server tried 5 times", "UNREGED|NOREG"), i5 = Ce(R, t3);
                        kt(e.SwEvent.Error, { error: i5, sessionId: u2.sessionid }, u2.uuid);
                        break;
                      }
                      setTimeout((() => {
                        this.session.execute(E2);
                      }), this.reconnectDelay());
                      break;
                    case _t.FAILED:
                    case _t.FAIL_WAIT:
                    case _t.TIMEOUT:
                      if (!this.isDuplicateGatewayState(i4, [_t.FAILED, _t.FAIL_WAIT, _t.TIMEOUT])) {
                        const t3 = Ce(k, new Error(`Gateway state: ${i4}`));
                        if (kt(e.SwEvent.Error, { error: t3, sessionId: u2.sessionid }, u2.uuid), u2.options.skipLastVoiceSdkId = true, Le.debug(`Set skipLastVoiceSdkId=true on session options to avoid sticky reconnect to same b2bua-rtc instance (sessionId=${u2.sessionid})`), !this.session.hasAutoReconnect()) {
                          this.retriedConnect = 0;
                          const t4 = new nn("Fail to connect the server, the server tried 5 times", "FAILED|FAIL_WAIT|TIMEOUT");
                          this.session._terminateActiveCallsLocally();
                          const i5 = Ce(T, t4);
                          kt(e.SwEvent.Error, { error: i5, sessionId: u2.sessionid }, u2.uuid);
                          break;
                        }
                        if (this.retriedConnect += 1, 5 === this.retriedConnect) {
                          this.retriedConnect = 0, this.session._terminateActiveCallsLocally();
                          const t4 = Ce(T, new Error("Connection Retry Failed"));
                          kt(e.SwEvent.Error, { error: t4, sessionId: u2.sessionid }, u2.uuid);
                          break;
                        }
                        setTimeout((() => {
                          if (Le.debug(`Reconnecting... Retry ${this.retriedConnect} of 5`), this.session.options.keepConnectionAliveOnSocketClose) {
                            const e2 = Object.values(u2.calls).some(((e3) => {
                              var t4;
                              return (null === (t4 = e3.peer) || void 0 === t4 ? void 0 : t4.instance) && !e3.signalingStateClosed;
                            }));
                            if (e2) return Le.debug("Reconnecting by keeping the existing session due to keepConnectionAliveOnSocketClose option being set."), void this.session.socketDisconnect();
                            Le.debug("keepConnectionAliveOnSocketClose is set but all peer connections have signalingState closed, doing full reconnect");
                          }
                          this.session.disconnect().then((() => {
                            this.session.clearConnection(), this.session.connect();
                          }));
                        }), this.reconnectDelay());
                      }
                      break;
                    default:
                      Le.warn("GatewayState message unknown method:", t2);
                  }
                  break;
                }
                Le.debug("Verto message unknown method:", t2);
                break;
              }
            }
          }
          isDuplicateGatewayState(e2, t2) {
            const { previousGatewayState: i2 } = this.session.connection, n2 = t2.includes(i2);
            return n2 && Le.debug(`Gateway state '${e2}' received but previous state was '${i2}' \u2014 guard condition met, skipping re-emission (sessionId=${this.session.sessionid})`), n2;
          }
          _retrieveCallId(e2, t2) {
            const i2 = Object.keys(this.session.calls);
            if ("bootObj" !== e2.action) return i2.find(((e3) => this.session.calls[e3].channels.includes(t2)));
            {
              const t3 = e2.data.find(((e3) => i2.includes(e3[0])));
              if (t3 instanceof Array) return t3[0];
            }
          }
          _handlePvtEvent(t2) {
            return i(this, void 0, void 0, (function* () {
              const { session: i2 } = this, n2 = i2.relayProtocol, { action: s2, laChannel: o2, laName: r2, chatChannel: a2, infoChannel: c2, modChannel: l2, conferenceMemberID: d2, role: u2, callID: h2 } = t2;
              switch (s2) {
                case "conference-liveArray-join": {
                  const n3 = () => {
                    i2.vertoBroadcast({ nodeId: this.nodeId, channel: o2, data: { liveArray: { command: "bootstrap", context: o2, name: r2 } } });
                  }, s3 = { nodeId: this.nodeId, channels: [o2], handler: ({ data: e2 }) => {
                    const s4 = h2 || this._retrieveCallId(e2, o2);
                    if (s4 && i2.calls.hasOwnProperty(s4)) {
                      const a4 = i2.calls[s4];
                      a4._addChannel(o2), a4.extension = r2, a4.handleConferenceUpdate(e2, t2).then(((e3) => {
                        "INVALID_PACKET" === e3 && n3();
                      }));
                    }
                  } }, a3 = yield i2.vertoSubscribe(s3).catch(((t3) => {
                    Le.error("liveArray subscription error:", t3);
                    const n4 = Ce(I, t3);
                    kt(e.SwEvent.Error, { error: n4, sessionId: i2.sessionid }, i2.uuid);
                  }));
                  Si(a3, o2) && n3();
                  break;
                }
                case "conference-liveArray-part": {
                  let t3 = null;
                  if (o2 && i2._existsSubscription(n2, o2)) {
                    const { callId: s4 = null } = i2.subscriptions[n2][o2];
                    if (t3 = i2.calls[s4] || null, null !== s4) {
                      const n3 = { type: dt.conferenceUpdate, action: mt.Leave, conferenceName: r2, participantId: Number(d2), role: u2 };
                      kt(e.SwEvent.Notification, n3, s4, false) || kt(e.SwEvent.Notification, n3, i2.uuid), null === t3 && Tt(e.SwEvent.Notification, null, s4);
                    }
                  }
                  const s3 = [o2, a2, c2, l2];
                  i2.vertoUnsubscribe({ nodeId: this.nodeId, channels: s3 }).then((({ unsubscribedChannels: e2 = [] }) => {
                    t3 && (t3.channels = t3.channels.filter(((t4) => !e2.includes(t4))));
                  })).catch(((e2) => {
                    Le.error("liveArray unsubscribe error:", e2);
                  }));
                  break;
                }
              }
            }));
          }
          _handleSessionEvent(t2) {
            switch (t2.contentType) {
              case "layout-info":
              case "layer-info":
                Gi(this.session, t2);
                break;
              case "logo-info": {
                const i2 = { type: dt.conferenceUpdate, action: mt.LogoInfo, logo: t2.logoURL };
                kt(e.SwEvent.Notification, i2, this.session.uuid);
                break;
              }
            }
          }
        }
        class on extends tn {
          constructor(e2) {
            super(e2), this.relayProtocol = "verto-protocol", this.timeoutErrorCode = -329990, this.handleLoginOnSocketOpen = () => i(this, void 0, void 0, (function* () {
              this._idle = false;
              const { autoReconnect: e3 = true } = this.options;
              yield this.login({ onSuccess: () => {
                this._autoReconnect = e3;
              } });
            })), this.handleAnonymousLoginOnSocketOpen = () => i(this, void 0, void 0, (function* () {
              this._idle = false, yield this.login();
            })), this._vertoHandler = new sn(this), window.addEventListener("beforeunload", ((t2) => {
              if (Le.debug("Window beforeunload triggered."), false !== e2.hangupOnBeforeUnload) return nt(), void (this.calls && Object.keys(this.calls).forEach(((e3) => {
                this.calls[e3] && (Le.info(`Hanging up call due to window unload: ${e3}`), this.calls[e3].hangup({ initiator: "sdk:beforeunload" }, true));
              })));
              try {
                const e3 = this.getActiveCalls().map(((e4) => {
                  const t3 = { id: e4.id, customHeaders: e4.options.customHeaders, forceRelayCandidate: e4.shouldForceRelayCandidateForRecovery() };
                  return "held" === e4.state && (t3.wasHeld = true), "string" == typeof e4.options.remoteElement ? t3.remoteElement = e4.options.remoteElement : this.remoteElementId && e4.options.remoteElement === this.remoteElement && (t3.remoteElement = this.remoteElementId), "string" == typeof e4.options.localElement ? t3.localElement = e4.options.localElement : this.localElementId && e4.options.localElement === this.localElement && (t3.localElement = this.localElementId), t3;
                }));
                if (this.getActiveCalls().forEach(((e4) => {
                  if (e4.flushIntermediateCallReport) try {
                    Le.debug(`beforeunload: flushing call report for ${e4.id}.`), e4.flushIntermediateCallReport({ type: "page-unload" });
                  } catch (t3) {
                    Le.debug(`beforeunload: failed to flush call report for ${e4.id}: ${t3 instanceof Error ? t3.message : String(t3)}`);
                  }
                })), !this.sessionid || 0 === e3.length) return Le.debug(`No sessionID ${this.sessionid} or activeCalls ${e3.length} during saving calls recover marker!`), void st();
                Le.info(`Saving recovery marker for ${e3.length} active call(s) before unload (sessid=${this.sessionid}): [${e3.map(((e4) => e4.id)).join(", ")}].`), rt(e3, this.sessionid);
              } catch (e3) {
                const t3 = this.calls ? Object.keys(this.calls) : [];
                Le.debug(`Failed to save active-calls recovery marker before unload (active call ids: [${t3.join(", ")}]): ${e3 instanceof Error ? e3.message : String(e3)}`);
              }
            })), document.addEventListener("visibilitychange", (() => {
              "hidden" === document.visibilityState && false === e2.hangupOnBeforeUnload && this.sessionid && this.hasActiveCall() && (Le.debug("visibilitychange \u2192 hidden: re-stamping reconnect session-id freshness."), it(this.sessionid));
            }));
          }
          validateOptions() {
            return Ge(this.options) || Ve(this.options);
          }
          newCall(e2) {
            if (!this.validateCallOptions(e2)) {
              throw Ce(y, void 0, "Error: destinationNumber is required");
            }
            const t2 = new en(this, e2);
            return e2.id || Le.debug(`newCall: callId was not provided in options, SDK-generated ID: ${t2.id}`), this.emitMultipleActiveCallsWarning(t2.id), performance.mark(Ki(t2.id, "new-call-start")), t2.invite(), t2;
          }
          broadcast(e2) {
            return this.vertoBroadcast(e2);
          }
          subscribe(e2) {
            return this.vertoSubscribe(e2);
          }
          unsubscribe(e2) {
            return this.vertoUnsubscribe(e2);
          }
          validateCallOptions(e2) {
            return !!Ve(this.options) || Boolean(e2.destinationNumber);
          }
          _onSocketOpen() {
            const e2 = Object.create(null, { _onSocketOpen: { get: () => super._onSocketOpen } });
            return i(this, void 0, void 0, (function* () {
              return e2._onSocketOpen.call(this), Ge(this.options) ? this.handleLoginOnSocketOpen() : Ve(this.options) ? this.handleAnonymousLoginOnSocketOpen() : void 0;
            }));
          }
          _onSocketMessage(e2) {
            this._vertoHandler.handleMessage(e2);
          }
        }
        class rn extends on {
          constructor(e2) {
            super(e2), Le.info(`SDK version: ${Ht}`);
          }
          newCall(e2) {
            return super.newCall(e2);
          }
          static webRTCInfo() {
            return Ci();
          }
          static webRTCSupportedBrowserList() {
            return [{ operationSystem: "Android", supported: [{ browserName: "Chrome", features: ["audio"], supported: wi.full }, { browserName: "Firefox", features: ["audio"], supported: wi.partial }, { browserName: "Safari", supported: wi.not_supported }, { browserName: "Edge", supported: wi.not_supported }] }, { operationSystem: "iOS", supported: [{ browserName: "Chrome", supported: wi.not_supported }, { browserName: "Firefox", supported: wi.not_supported }, { browserName: "Safari", features: ["video", "audio"], supported: wi.full }, { browserName: "Edge", supported: wi.not_supported }] }, { operationSystem: "Linux", supported: [{ browserName: "Chrome", features: ["video", "audio"], supported: wi.full }, { browserName: "Firefox", features: ["audio"], supported: wi.partial }, { browserName: "Safari", supported: wi.not_supported }, { browserName: "Edge", supported: wi.not_supported }] }, { operationSystem: "MacOS", supported: [{ browserName: "Chrome", features: ["video", "audio"], supported: wi.full }, { browserName: "Firefox", features: ["audio"], supported: wi.partial }, { browserName: "Safari", features: ["video", "audio"], supported: wi.full }, { browserName: "Edge", features: ["audio"], supported: wi.partial }] }, { operationSystem: "Windows", supported: [{ browserName: "Chrome", features: ["video", "audio"], supported: wi.full }, { browserName: "Firefox", features: ["audio"], supported: wi.partial }, { browserName: "Safari", supported: wi.not_supported }, { browserName: "Edge", features: ["audio"], supported: wi.partial }] }];
          }
        }
        class an {
          static run(t2) {
            return i(this, void 0, void 0, (function* () {
              const i2 = Ye({}), n2 = Ye({}), s2 = new rn(t2.credentials);
              yield s2.connect(), s2.on(e.SwEvent.Ready, i2.resolve), s2.on(e.SwEvent.Error, i2.reject), s2.on(e.SwEvent.MediaError, i2.reject), s2.on(e.SwEvent.MediaError, i2.reject), s2.on(e.SwEvent.Notification, ((e2) => {
                e2.call && e2.call.sipCode >= 400 && n2.reject(new Error(e2.call.sipReason));
              })), Ct(e.SwEvent.StatsReport, ((e2) => {
                n2.resolve(an.mapReport(e2));
              })), yield i2.promise, yield s2.newCall({ destinationNumber: t2.texMLApplicationNumber, debug: true });
              const o2 = yield n2.promise;
              return yield s2.disconnect(), o2;
            }));
          }
          static mapReport(e2) {
            var t2, i2, n2, s2, o2, r2, a2, c2, l2, d2, u2, h2, p2;
            const g2 = [], v2 = [];
            for (const t3 of e2) switch (t3.event) {
              case "onicecandidate":
                t3.data && g2.push(t3.data);
                break;
              case "stats":
                v2.push(t3.data);
            }
            let m2 = 0, f2 = 1 / 0, _2 = -1 / 0, S2 = 0, y2 = 1 / 0, b2 = -1 / 0, I2 = 0;
            v2.forEach(((e3) => {
              var t3, i3, n3;
              if (!(null === (t3 = e3.remote.audio.inbound) || void 0 === t3 ? void 0 : t3[0])) return;
              m2 += 1;
              const s3 = null !== (i3 = e3.remote.audio.inbound[0].jitter) && void 0 !== i3 ? i3 : 0, o3 = null !== (n3 = e3.remote.audio.inbound[0].roundTripTime) && void 0 !== n3 ? n3 : 0;
              S2 += s3, I2 += o3, _2 = Math.max(_2, s3), f2 = Math.min(f2, s3), b2 = Math.max(b2, o3), y2 = Math.min(y2, o3);
            }));
            const E2 = I2 / m2, C2 = S2 / m2, w2 = v2[v2.length - 1], T2 = Fi({ jitter: 1e3 * C2, rtt: 1e3 * E2, packetsReceived: null !== (n2 = null === (i2 = null === (t2 = w2.audio.inbound) || void 0 === t2 ? void 0 : t2[0]) || void 0 === i2 ? void 0 : i2.packetsReceived) && void 0 !== n2 ? n2 : 0, packetsLost: null !== (r2 = null === (o2 = null === (s2 = w2.audio.inbound) || void 0 === s2 ? void 0 : s2[0]) || void 0 === o2 ? void 0 : o2.packetsLost) && void 0 !== r2 ? r2 : 0 });
            return { iceCandidatePairStats: v2[v2.length - 1].connection, summaryStats: { mos: T2, jitter: { average: C2, max: _2, min: f2 }, rtt: { average: E2, max: b2, min: y2 }, quality: $i(T2) }, sessionStats: { packetsSent: null !== (a2 = w2.connection.packetsSent) && void 0 !== a2 ? a2 : 0, bytesSent: null !== (c2 = w2.connection.bytesSent) && void 0 !== c2 ? c2 : 0, bytesReceived: null !== (l2 = w2.connection.bytesReceived) && void 0 !== l2 ? l2 : 0, packetsLost: null !== (h2 = null === (u2 = null === (d2 = w2.remote.audio.inbound) || void 0 === d2 ? void 0 : d2[0]) || void 0 === u2 ? void 0 : u2.packetsLost) && void 0 !== h2 ? h2 : 0, packetsReceived: null !== (p2 = w2.connection.packetsReceived) && void 0 !== p2 ? p2 : 0 }, iceCandidateStats: g2 };
          }
          getTelnyxIds() {
            return { telnyxCallControlId: "", telnyxSessionId: "", telnyxLegId: "" };
          }
        }
        e.Call = en, e.ERROR_TYPE = ut, e.NOTIFICATION_TYPE = dt, e.PreCallDiagnosis = an, e.Region = { EU: "eu", US_CENTRAL: "us-central", US_EAST: "us-east", US_WEST: "us-west", CA_CENTRAL: "ca-central", APAC: "apac", SOUTH_ASIA: "south-asia" }, e.SDK_ERRORS = Se, e.SDK_WARNINGS = ye, e.TELNYX_ERROR_CODES = l, e.TELNYX_ICE_SERVERS = fe, e.TELNYX_WARNING_CODES = d, e.TelnyxError = Ie, e.TelnyxRTC = rn, e.isFunctionCallOutputParams = function(e2) {
          if (!e2 || "object" != typeof e2) return false;
          if ("conversation.item.create" !== e2.type) return false;
          const t2 = e2.item;
          if ("object" != typeof t2 || null === t2) return false;
          const i2 = t2;
          return "function_call_output" === i2.type && "string" == typeof i2.call_id && "string" == typeof i2.output;
        }, e.isFunctionCallParams = function(e2) {
          if (!e2 || "object" != typeof e2) return false;
          if ("conversation.item.created" !== e2.type) return false;
          const t2 = e2.item;
          if ("object" != typeof t2 || null === t2) return false;
          const i2 = t2;
          return "function_call" === i2.type && "string" == typeof i2.call_id && "string" == typeof i2.name && "string" == typeof i2.arguments;
        }, e.isMediaRecoveryErrorEvent = function(e2) {
          return true === e2.recoverable;
        }, Object.defineProperty(e, "__esModule", { value: true });
      }));
    }
  });

  // entry.js
  var require_entry = __commonJS({
    "entry.js"(exports, module) {
      module.exports = require_bundle();
    }
  });
  return require_entry();
})();
