import { describe, expect, it } from 'vitest';

import {
    describeProviderError,
    getProviderErrorName,
    getProviderErrorText,
} from '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/provider-errors.js';

// Observed live: an agent's soft phone showed "SDP_SEND_FAILED: Failed to send call data to server". It means the
// Telnyx SDK could not deliver the call over its own signaling socket, not that this site failed, and the server log
// had no trace of it.
describe('getProviderErrorName', () => {
    it('reads the name from the text the SDK raises', () => {
        expect(getProviderErrorName({ error: 'SDP_SEND_FAILED: Failed to send call data to server' })).toBe('SDP_SEND_FAILED');
    });

    it('reads the name from a structured error', () => {
        expect(getProviderErrorName({ error: { name: 'WEBSOCKET_ERROR', message: 'WebSocket error' } })).toBe('WEBSOCKET_ERROR');
    });

    it('reads the name from a message', () => {
        expect(getProviderErrorName({ message: 'LOGIN_FAILED: Login failed' })).toBe('LOGIN_FAILED');
    });

    it('returns nothing when the SDK gives no name', () => {
        expect(getProviderErrorName({ message: 'Something went wrong' })).toBe('');
        expect(getProviderErrorName(null)).toBe('');
    });
});

describe('getProviderErrorText', () => {
    it('keeps the SDK words for the diagnostics', () => {
        expect(getProviderErrorText({ error: 'SDP_SEND_FAILED: Failed to send call data to server' }))
            .toBe('SDP_SEND_FAILED: Failed to send call data to server');
        expect(getProviderErrorText({ error: { name: 'WEBSOCKET_ERROR', message: 'WebSocket error' } }))
            .toBe('WEBSOCKET_ERROR: WebSocket error');
        expect(getProviderErrorText('plain')).toBe('plain');
        expect(getProviderErrorText(undefined)).toBe('');
    });
});

describe('describeProviderError', () => {
    it('tells the agent the connection to the phone service dropped instead of the SDK text', () => {
        const message = describeProviderError({ error: 'SDP_SEND_FAILED: Failed to send call data to server' }, {});

        expect(message).toContain('connection to the phone service dropped');
        expect(message).not.toContain('SDP_SEND_FAILED');
    });

    it('uses the localized text when the page provides it', () => {
        expect(describeProviderError({ error: 'SDP_SEND_FAILED: x' }, { providerSignalingLost: 'Localized' })).toBe('Localized');
    });

    it('keeps the SDK text for errors it has nothing better to say about', () => {
        expect(describeProviderError({ error: 'MEDIA_DEVICE_NOT_FOUND: No microphone' }, {})).toBe('MEDIA_DEVICE_NOT_FOUND: No microphone');
    });

    it('falls back to a generic message when the SDK says nothing', () => {
        expect(describeProviderError({}, {})).toBe('Telnyx WebRTC error.');
    });
});
