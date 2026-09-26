import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.ContactCenter/Assets/js/shared/coalesced-refresh.js';

const { coalesceRefresh } = globalThis.CrestAppsContactCenter;

function deferred() {
    let resolve;
    let reject;
    const promise = new Promise((res, rej) => {
        resolve = res;
        reject = rej;
    });

    return { promise, resolve, reject };
}

// Bug: every real-time event the agent's page hears -- the offer, the presence change it causes, the queue stats for
// each queue -- asked for the whole workspace state on its own, so a single state change fetched it 3 to 6 times
// within a third of a second. Refreshes that arrive while one is in flight must fold into it.
describe('coalesceRefresh', () => {
    it('folds a burst into the fetch in flight plus one trailing fetch', async () => {
        const pending = [];
        const refresh = coalesceRefresh(() => {
            const next = deferred();
            pending.push(next);

            return next.promise;
        });

        const calls = [refresh(), refresh(), refresh(), refresh(), refresh(), refresh()];

        expect(pending).toHaveLength(1);

        pending[0].resolve();
        await Promise.resolve();
        await Promise.resolve();

        // The events that arrived mid-flight may describe a change the first response predates, so exactly one more
        // fetch runs to pick it up.
        expect(pending).toHaveLength(2);

        pending[1].resolve();
        await Promise.all(calls);

        expect(pending).toHaveLength(2);
    });

    it('fetches once for a single request', async () => {
        let runs = 0;
        const refresh = coalesceRefresh(() => {
            runs++;

            return Promise.resolve();
        });

        await refresh();

        expect(runs).toBe(1);
    });

    it('fetches again for a request made after the last one finished', async () => {
        let runs = 0;
        const refresh = coalesceRefresh(() => {
            runs++;

            return Promise.resolve();
        });

        await refresh();
        await refresh();

        expect(runs).toBe(2);
    });

    it('keeps working after a failed fetch', async () => {
        let runs = 0;
        const refresh = coalesceRefresh(() => {
            runs++;

            return runs === 1 ? Promise.reject(new Error('offline')) : Promise.resolve();
        });

        await refresh().catch(() => { });
        await refresh();

        expect(runs).toBe(2);
    });
});

const assets = JSON.parse(readFileSync('src/Modules/CrestApps.OrchardCore.ContactCenter/Assets.json', 'utf8'));
const helper = 'Assets/js/shared/coalesced-refresh.js';

describe.each([
    ['wwwroot/scripts/agent-workspace.js', 'Assets/js/agent-workspace.js'],
    ['wwwroot/scripts/contact-center-agent-bar.js', 'Assets/js/contact-center-agent-bar.js'],
])('the %s bundle', (output, script) => {
    const group = assets.find(candidate => candidate.output === output);

    it('carries the coalescing helper ahead of the script that uses it', () => {
        expect(group).toBeDefined();
        expect(group.inputs).toContain(helper);
        expect(group.inputs.indexOf(helper)).toBeLessThan(group.inputs.indexOf(script));
    });
});
