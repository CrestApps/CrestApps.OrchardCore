import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

// Options built from site settings are refreshed without restarting the tenant: saving the settings calls
// IOptionsUpdateNotifier.RequestUpdate<T>(), and AddSignalOptionsChangeTokenSource<T>() tells IOptionsMonitor<T> to
// recompute them. IOptions<T> is computed once and never sees the signal. Live, the Telnyx API client read its key
// through IOptions<TelnyxOptions>, so a corrected key was ignored and Telnyx went on refusing the soft phone until
// the app was restarted, while every other part of the provider had the new settings.
function sources(directory) {
    return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
        if (entry.isDirectory()) {
            return ['bin', 'obj', 'node_modules', 'wwwroot'].includes(entry.name) ? [] : sources(join(directory, entry.name));
        }

        return entry.name.endsWith('.cs') ? [join(directory, entry.name)] : [];
    });
}

describe('options refreshed by a settings save', () => {
    const files = sources('src').map(path => ({ path, text: readFileSync(path, 'utf8') }));

    const signalled = new Set(files.flatMap(({ text }) =>
        [...text.matchAll(/AddSignalOptionsChangeTokenSource<([A-Za-z_][A-Za-z0-9_]*)>/g)].map(match => match[1])));

    it('are found, so the check below is not passing on nothing', () => {
        expect(signalled.size).toBeGreaterThan(5);
    });

    it('are always read through IOptionsMonitor or IOptionsSnapshot, never IOptions', () => {
        const stale = [];

        for (const { path, text } of files) {
            for (const match of text.matchAll(/\bIOptions<([A-Za-z_][A-Za-z0-9_]*)>/g)) {
                if (signalled.has(match[1])) {
                    stale.push(`${match[1]} (${path})`);
                }
            }
        }

        expect(stale).toEqual([]);
    });
});
