import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it, vi } from 'vitest';

const scripts = 'src/Modules/CrestApps.OrchardCore.Telephony/Assets/js';

// The files a module reaches through its static imports, itself included.
function reachableFrom(entry) {
    const seen = new Set();
    const visit = file => {
        if (seen.has(file)) {
            return;
        }

        seen.add(file);

        for (const match of readFileSync(file, 'utf8').matchAll(/^import\s[^;]*?from\s+'(\.[^']+)';/gms)) {
            visit(path.posix.join(path.posix.dirname(file), match[1]));
        }
    };

    visit(entry);

    return seen;
}

// The soft phone is built from soft-phone.js and what it imports. A helper file nothing imports is not in the browser
// at all, so its tests would pass while the phone never ran it.
describe('the soft phone bundle', () => {
    it('reaches every helper module through its imports', () => {
        const reached = reachableFrom(`${scripts}/soft-phone.js`);

        readdirSync(`${scripts}/soft-phone`)
            .filter(file => file.endsWith('.js'))
            .forEach(file => expect(reached, file).toContain(`${scripts}/soft-phone/${file}`));
    });
});

// The agent workspace renders call history through the telephony client and has no soft phone on the page. Without
// the formatter on the client it fell back to raw digits — +17024445506 where a person expects (702) 444-5506 — with
// nothing failing to say so. Every surface that shows a duration reads the shared call timer off it too.
describe('the telephony client', () => {
    it('carries the phone formatter and the call timer for the other bundles', async () => {
        vi.resetModules();
        globalThis.window = {};
        globalThis.document = { createElement: () => ({ textContent: '', innerHTML: '' }) };

        await import('../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/telephony-client.js');

        expect(globalThis.window.telephonyClient.formatPhoneNumber('7024445506')).toBe('(702) 444-5506');
        expect(globalThis.window.telephonyClient.formatDuration(3725)).toBe('1:02:05');

        delete globalThis.window;
        delete globalThis.document;
    });
});
