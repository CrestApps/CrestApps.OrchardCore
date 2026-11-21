import { readFileSync } from 'node:fs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { buildKeypadExtensionResultsHtml } from '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/keypad-search.js';

// A browser writes a text node back out escaping only &, < and >: quotes stay as they are, because they are harmless
// in text. The soft phone used that trick (textContent in, innerHTML out) as its escapeHtml, then put the result inside
// attributes, where a quote ends the value. This element serializes the way the browser does, so a helper still built
// on it fails here.
function browserLikeDocument() {
    return {
        createElement: () => {
            let text = '';

            return {
                set textContent(value) { text = String(value); },
                get textContent() { return text; },
                appendChild(node) { text += node.data; },
                get innerHTML() { return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'); },
            };
        },
        createTextNode: data => ({ data: String(data) }),
    };
}

// The attributes of the first tag in the markup, read the way a browser tokenizes them.
function attributesOf(html) {
    const tag = /^<\w+((?:\s+[\w-]+(?:="[^"]*")?)*)\s*>/.exec(html.slice(html.indexOf('<button')));

    expect(tag, 'the button tag is well formed').not.toBeNull();

    return [...tag[1].matchAll(/([\w-]+)(?:="([^"]*)")?/g)].map(([, name, value]) => [name, value]);
}

describe('the telephony client escapeHtml', () => {
    beforeEach(() => {
        vi.resetModules();
        globalThis.window = {};
        globalThis.document = browserLikeDocument();
    });

    afterEach(() => {
        delete globalThis.window;
        delete globalThis.document;
    });

    it('escapes all five characters, quotes included', async () => {
        await import('../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/telephony-client.js');

        expect(globalThis.window.telephonyClient.escapeHtml(`<a href="x">Tom & Jerry's</a>`))
            .toBe('&lt;a href=&quot;x&quot;&gt;Tom &amp; Jerry&#39;s&lt;/a&gt;');
        expect(globalThis.window.telephonyClient.escapeHtml(null)).toBe('');
    });

    // A crafted caller id reaches the agent's recent calls and voicemail list. With the quote left alone it closed the
    // data attribute and added an event handler that ran when the agent hovered the call-back button.
    it('keeps a quote in a caller number inside its attribute', async () => {
        await import('../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/telephony-client.js');

        const html = buildKeypadExtensionResultsHtml(
            [{ extension: '1" onmouseover="alert(1)', name: 'Ann', detail: '1' }],
            {},
            globalThis.window.telephonyClient.escapeHtml);

        expect(attributesOf(html)).toEqual([
            ['type', 'button'],
            ['role', 'listitem'],
            ['class', 'telephony-soft-phone__keypad-result'],
            ['data-telephony-keypad-extension', '1&quot; onmouseover=&quot;alert(1)'],
        ]);
    });
});

// The agent bar and the activity query builder carried their own copy of the textContent trick as a fallback. They
// now take the one shared helper, so there is no second implementation to disagree with it.
describe.each([
    'src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/contact-center-agent-bar.js',
    'src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Assets/js/activity-query-builder.js',
])('%s', source => {
    it('escapes with the shared helper instead of its own copy', () => {
        const code = readFileSync(source, 'utf8');

        expect(code).toMatch(/^import \{ escapeHtml \} from '[./]+CrestApps\.OrchardCore\.Resources\/Assets\/js\/browser\/html\.js';$/m);
        expect(code).not.toMatch(/function escapeHtml|escapeHtml\s*=.*function|\.innerHTML;\s*\n\s*\};/);
    });
});
