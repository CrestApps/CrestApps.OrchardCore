import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/telnyx-state.js';

const { mapTelnyxOutboundState, isTelnyxTerminalState, iceServersIncludeTurn } = globalThis.CrestAppsSoftPhone;

// The SDK reports a dozen call states; the dialer understands three. Getting this mapping wrong shows the agent
// a call that is ringing as connected, or leaves a finished call on their screen.
describe('mapTelnyxOutboundState', () => {
    it('treats every pre-answer state as ringing', () => {
        ['new', 'requesting', 'trying', 'recovering', 'ringing', 'answering', 'early']
            .forEach(state => expect(mapTelnyxOutboundState(state)).toBe('Ringing'));
    });

    it('treats an active call as connected', () => {
        expect(mapTelnyxOutboundState('active')).toBe('Connected');
    });

    it('reports no change for a held call', () => {
        // Hold is shown by its own indicator. Reporting a state change here would take a held call off the
        // dialer as though it had ended.
        expect(mapTelnyxOutboundState('held')).toBeNull();
    });

    it('treats every ended state as disconnected', () => {
        ['hangup', 'destroy', 'purge'].forEach(state => expect(mapTelnyxOutboundState(state)).toBe('Disconnected'));
    });

    it('reports no change for a state it does not know', () => {
        // A state added by a future SDK version must not be guessed at.
        expect(mapTelnyxOutboundState('something-new')).toBeNull();
        expect(mapTelnyxOutboundState(undefined)).toBeNull();
    });
});

describe('isTelnyxTerminalState', () => {
    it('recognises the states a call does not come back from', () => {
        expect(isTelnyxTerminalState('hangup')).toBe(true);
        expect(isTelnyxTerminalState('destroy')).toBe(true);
        expect(isTelnyxTerminalState('purge')).toBe(true);
    });

    it('does not treat a live call as finished', () => {
        expect(isTelnyxTerminalState('active')).toBe(false);
        expect(isTelnyxTerminalState('held')).toBe(false);
    });
});

// This one is a fix that cost a long diagnosis: a STUN-only list replaced the SDK's defaults (which include
// TURN), and agents behind a restrictive NAT heard nothing on inbound calls.
describe('iceServersIncludeTurn', () => {
    it('accepts a list containing a TURN server', () => {
        expect(iceServersIncludeTurn([{ urls: 'turn:turn.example.com:3478' }])).toBe(true);
        expect(iceServersIncludeTurn([{ urls: ['stun:stun.example.com', 'turns:turn.example.com:5349'] }])).toBe(true);
    });

    it('rejects a STUN-only list, so the SDK keeps its own relay', () => {
        expect(iceServersIncludeTurn([{ urls: 'stun:stun.example.com:3478' }])).toBe(false);
    });

    it('rejects anything that is not a list of servers', () => {
        expect(iceServersIncludeTurn(null)).toBe(false);
        expect(iceServersIncludeTurn([])).toBe(false);
        expect(iceServersIncludeTurn([{}])).toBe(false);
    });
});
