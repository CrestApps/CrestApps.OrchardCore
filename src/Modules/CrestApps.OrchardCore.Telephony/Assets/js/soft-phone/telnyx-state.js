/*
 * Translating the Telnyx WebRTC SDK's view of a call into the soft phone's, and the ICE-server check that keeps
 * a STUN-only configuration from taking the SDK's relay away.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a
 * shared namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    // Maps a Telnyx WebRTC SDK call state (call.state) to the soft-phone outbound state names the
    // originate() callback expects: 'Ringing', 'Connected', or 'Disconnected'. Returns null for
    // transient states that should not change the UI.
    function mapTelnyxOutboundState(state) {
        switch (state) {
            case 'new':
            case 'requesting':
            case 'trying':
            case 'recovering':
            case 'ringing':
            case 'answering':
            case 'early':
                return 'Ringing';
            case 'active':
                return 'Connected';
            case 'held':
                // A held call is still connected from the dialer's perspective; the hold indicator is driven
                // separately, so don't report a state change here.
                return null;
            case 'hangup':
            case 'destroy':
            case 'purge':
                return 'Disconnected';
            default:
                return null;
        }
    }

    function isTelnyxTerminalState(state) {
        return state === 'hangup' || state === 'destroy' || state === 'purge';
    }

    // Whether an ICE server list contains at least one TURN (relay) server. A STUN-only list does not, and must
    // not replace the Telnyx SDK's default ICE servers (which include TURN) or clients behind a restrictive NAT
    // lose the relay they need to receive inbound media.
    function iceServersIncludeTurn(iceServers) {
        if (!Array.isArray(iceServers)) {
            return false;
        }

        return iceServers.some(function (server) {
            var urls = server && server.urls;
            var list = Array.isArray(urls) ? urls : (urls ? [urls] : []);

            return list.some(function (url) {
                return /^turns?:/i.test(String(url || ''));
            });
        });
    }

    softPhone.mapTelnyxOutboundState = mapTelnyxOutboundState;
    softPhone.isTelnyxTerminalState = isTelnyxTerminalState;
    softPhone.iceServersIncludeTurn = iceServersIncludeTurn;
}(typeof globalThis !== 'undefined' ? globalThis : window));
