import { describe, expect, it } from 'vitest';

import { isWindowFinishedMessage, windowFinishedMessage } from '../../../src/Modules/CrestApps.OrchardCore.Learning.Packages/Assets/js/package-frame-messages.js';

const opened = {};
const origin = 'https://learn.example.com';

// A package that opens in a window of its own tells the page that opened it when the session finished, so the page
// shows the progress the server has. The page takes that only from the window it opened, on its own origin.
describe('messages from a package window', () => {
    it('takes the finished message of its own window on its own origin', () => {
        expect(isWindowFinishedMessage({ source: opened, origin, data: windowFinishedMessage('item1') }, opened, origin, 'item1')).toBe(true);
    });

    it('refuses another window, another origin, another item or another message', () => {
        expect(isWindowFinishedMessage({ source: {}, origin, data: windowFinishedMessage('item1') }, opened, origin, 'item1')).toBe(false);
        expect(isWindowFinishedMessage({ source: opened, origin: 'https://packages.example.com', data: windowFinishedMessage('item1') }, opened, origin, 'item1')).toBe(false);
        expect(isWindowFinishedMessage({ source: opened, origin, data: windowFinishedMessage('item2') }, opened, origin, 'item1')).toBe(false);
        expect(isWindowFinishedMessage({ source: opened, origin, data: { protocol: 'crestapps-scorm12', type: 'finish' } }, opened, origin, 'item1')).toBe(false);
        expect(isWindowFinishedMessage({ source: opened, origin, data: null }, null, origin, 'item1')).toBe(false);
    });
});
