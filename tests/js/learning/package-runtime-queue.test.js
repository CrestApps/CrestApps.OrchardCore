import { describe, expect, it } from 'vitest';

import { createRuntimeQueue } from '../../../src/Modules/CrestApps.OrchardCore.Learning.Packages/Assets/js/package-runtime-queue.js';

function deferred() {
    let resolve;
    const promise = new Promise(done => { resolve = done; });

    return { promise, resolve };
}

// Review round 9, M3: the player sends its runtime calls one at a time, in order, each with the next sequence number,
// so a finish never overtakes the commit before it and a busy answer is sent again before anything newer.
describe('runtime queue of the package player', () => {
    it('sends a finish only after the commit before it was answered', async () => {
        const sent = [];
        const commitAnswer = deferred();
        const queue = createRuntimeQueue(async payload => {
            sent.push(payload);

            return payload.commit ? commitAnswer.promise : { accepted: true };
        }, { retryDelay: 0 });

        const commit = queue.send(() => ({ changes: [['cmi.core.lesson_status', 'passed']], commit: true, finish: false }));
        const finish = queue.send(() => ({ changes: [], commit: false, finish: true }));

        await Promise.resolve();
        await Promise.resolve();

        expect(sent.map(payload => payload.finish)).toEqual([false]);

        commitAnswer.resolve({ accepted: true });
        await commit;
        await finish;

        expect(sent.map(payload => [payload.sequence, payload.commit, payload.finish])).toEqual([[1, true, false], [2, false, true]]);
    });

    it('sends a busy call again, with its sequence, before the next one', async () => {
        const sent = [];
        let first = true;
        const queue = createRuntimeQueue(async payload => {
            sent.push(payload);

            if (first) {
                first = false;

                return { accepted: false, retry: true };
            }

            return { accepted: true };
        }, { retryDelay: 0 });

        const older = queue.send(() => ({ changes: [['cmi.core.lesson_location', 'old']], commit: true, finish: false }));
        const newer = queue.send(() => ({ changes: [['cmi.core.lesson_location', 'new']], commit: true, finish: false }));

        await older;
        await newer;

        expect(sent.map(payload => [payload.sequence, payload.changes[0][1]])).toEqual([[1, 'old'], [1, 'old'], [2, 'new']]);
    });

    it('builds a call when its turn comes, so it takes every value set until then', async () => {
        const pending = [];
        const sent = [];
        const queue = createRuntimeQueue(async payload => {
            sent.push(payload);

            return { accepted: true };
        }, { retryDelay: 0 });

        pending.push(['cmi.core.score.raw', '50']);
        const first = queue.send(() => ({ changes: pending.splice(0), commit: true, finish: false }));
        pending.push(['cmi.core.score.raw', '90']);
        const second = queue.send(() => ({ changes: pending.splice(0), commit: false, finish: true }));

        await first;
        await second;

        // Both values were set before the first call's turn: it carries them, and the finish carries none.
        expect(sent.map(payload => payload.changes)).toEqual([[['cmi.core.score.raw', '50'], ['cmi.core.score.raw', '90']], []]);
        expect(sent.map(payload => payload.finish)).toEqual([false, true]);
    });
});
