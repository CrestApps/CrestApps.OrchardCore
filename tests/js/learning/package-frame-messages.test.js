import { describe, expect, it } from 'vitest';

import { frameOrigin, isFrameMessage } from '../../../src/Modules/CrestApps.OrchardCore.Learning.Packages/Assets/js/package-frame-messages.js';

const frame = {};
const strict = { protocol: 'crestapps-scorm12', launchId: 'l1', contentOrigin: null };
const hosted = { protocol: 'crestapps-scorm12', launchId: 'l1', contentOrigin: 'https://packages.example.com' };

function message(origin, data = { protocol: 'crestapps-scorm12', launchId: 'l1', type: 'set' }, source = frame) {
    return { origin, data, source };
}

// The player takes a message only from its own frame, from the frame's origin, for the current launch.
describe('messages the package player takes', () => {
    it('expects the origin "null" in strict mode and the content host with one', () => {
        expect(frameOrigin(strict)).toBe('null');
        expect(frameOrigin(hosted)).toBe('https://packages.example.com');
        expect(frameOrigin(null)).toBe('null');
    });

    it('takes messages from the opaque frame in strict mode only', () => {
        expect(isFrameMessage(message('null'), frame, strict)).toBe(true);
        expect(isFrameMessage(message('https://packages.example.com'), frame, strict)).toBe(false);
        expect(isFrameMessage(message('https://learn.example.com'), frame, strict)).toBe(false);
    });

    it('takes messages from the content host only when one is set', () => {
        expect(isFrameMessage(message('https://packages.example.com'), frame, hosted)).toBe(true);
        expect(isFrameMessage(message('null'), frame, hosted)).toBe(false);
        expect(isFrameMessage(message('https://packages.example.com.evil.test'), frame, hosted)).toBe(false);
        expect(isFrameMessage(message('http://packages.example.com'), frame, hosted)).toBe(false);
    });

    it('refuses another window, another launch, another protocol and no launch', () => {
        expect(isFrameMessage(message('https://packages.example.com', undefined, {}), frame, hosted)).toBe(false);
        expect(isFrameMessage(message('https://packages.example.com', { protocol: 'crestapps-scorm12', launchId: 'l0' }), frame, hosted)).toBe(false);
        expect(isFrameMessage(message('https://packages.example.com', { protocol: 'crestapps-scorm2004', launchId: 'l1' }), frame, hosted)).toBe(false);
        expect(isFrameMessage(message('https://packages.example.com', null), frame, hosted)).toBe(false);
        expect(isFrameMessage(message('https://packages.example.com'), null, hosted)).toBe(false);
        expect(isFrameMessage(message('https://packages.example.com'), frame, null)).toBe(false);
    });
});
