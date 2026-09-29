import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/microphone-errors.js';

const { classifyMicrophoneError, describeBrowserError, describeMicrophoneFacts } = globalThis.CrestAppsSoftPhone;

const facts = (overrides) => ({
    name: 'NotAllowedError',
    message: 'Permission denied',
    permissionState: null,
    isSecureContext: true,
    policyAllowed: true,
    audioInputCount: 1,
    ...overrides,
});

// A dial failed live with "Microphone access is blocked. Allow microphone access in your browser", and the only thing
// logged was "NotAllowedError". The browser throws that one name for several causes with different fixes, so the soft
// phone must name the actual cause and log what it saw.
describe('classifyMicrophoneError', () => {
    it('recognises the operating system blocking the browser, which the browser settings cannot fix', () => {
        expect(classifyMicrophoneError(facts({ message: 'Permission denied by system' }))).toBe('system-denied');
        expect(classifyMicrophoneError(facts({ message: 'The request is not allowed by the user agent or the platform in the current context.' }))).toBe('system-denied');
    });

    it('treats a refusal while the site holds the permission as coming from below the browser', () => {
        expect(classifyMicrophoneError(facts({ permissionState: 'granted' }))).toBe('system-denied');
    });

    it('tells a blocked site from a dismissed prompt', () => {
        expect(classifyMicrophoneError(facts({ permissionState: 'denied' }))).toBe('site-denied');
        expect(classifyMicrophoneError(facts({ permissionState: 'prompt' }))).toBe('dismissed');
    });

    it('falls back to a blocked site when the browser cannot report the permission', () => {
        expect(classifyMicrophoneError(facts({ permissionState: null }))).toBe('site-denied');
        expect(classifyMicrophoneError(facts({ name: 'PermissionDeniedError', permissionState: null }))).toBe('site-denied');
    });

    it('names an insecure page before anything else, since no browser offers a microphone there', () => {
        expect(classifyMicrophoneError(facts({ isSecureContext: false, permissionState: 'denied' }))).toBe('insecure');
    });

    it('recognises a page embedded without the microphone', () => {
        expect(classifyMicrophoneError(facts({ policyAllowed: false }))).toBe('policy');
        expect(classifyMicrophoneError(facts({ name: 'SecurityError', message: '' }))).toBe('policy');
    });

    it('recognises a missing microphone', () => {
        expect(classifyMicrophoneError(facts({ name: 'NotFoundError', message: 'Requested device not found' }))).toBe('not-found');
        expect(classifyMicrophoneError(facts({ name: 'OverconstrainedError' }))).toBe('not-found');
        expect(classifyMicrophoneError(facts({ name: 'Error', audioInputCount: 0 }))).toBe('not-found');
    });

    it('recognises a microphone another application is holding', () => {
        expect(classifyMicrophoneError(facts({ name: 'NotReadableError', message: 'Could not start audio source' }))).toBe('in-use');
        expect(classifyMicrophoneError(facts({ name: 'AbortError' }))).toBe('in-use');
    });

    it('leaves anything else unknown', () => {
        expect(classifyMicrophoneError(facts({ name: 'TypeError', message: 'boom' }))).toBe('unknown');
        expect(classifyMicrophoneError(undefined)).toBe('unknown');
    });
});

describe('describing the failure', () => {
    it('shows the agent the browser error in its own words', () => {
        expect(describeBrowserError(facts({ message: 'Permission denied by system' }))).toBe('NotAllowedError: Permission denied by system');
        expect(describeBrowserError(facts({ message: '' }))).toBe('NotAllowedError');
    });

    it('logs every fact the classification used, and says when one was unknown', () => {
        const line = describeMicrophoneFacts(facts({ permissionState: 'granted', policyAllowed: null, audioInputCount: 2, origin: 'https://crm.example.test' }));

        expect(line).toContain('error=NotAllowedError: Permission denied');
        expect(line).toContain('permission=granted');
        expect(line).toContain('secureContext=true');
        expect(line).toContain('policy=unknown');
        expect(line).toContain('audioInputs=2');
        expect(line).toContain('origin=https://crm.example.test');
    });
});
