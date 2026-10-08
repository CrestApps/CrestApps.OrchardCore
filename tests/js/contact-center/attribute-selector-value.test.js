import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/shared/supervisor-actions.js';

const { attributeSelectorValue } = globalThis.CrestAppsContactCenter;

// Without CSS.escape the dashboard quotes an agent id into an attribute selector itself, so a backslash or quote in the
// id must not end the string early or change what the selector matches.
describe('attributeSelectorValue without CSS.escape', () => {
    it('escapes backslashes before quotes', () => {
        expect(attributeSelectorValue('a\\"b', null)).toBe('a\\\\\\"b');
    });

    it('escapes a lone backslash', () => {
        expect(attributeSelectorValue('a\\b', null)).toBe('a\\\\b');
    });

    it('leaves an ordinary id alone', () => {
        expect(attributeSelectorValue('4db0c8c5ddymn21d72mjv8dbnv', null)).toBe('4db0c8c5ddymn21d72mjv8dbnv');
    });

    it('uses CSS.escape when the browser has it', () => {
        expect(attributeSelectorValue('x"y', { escape: (v) => 'escaped:' + v })).toBe('escaped:x"y');
    });
});
