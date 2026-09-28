import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/voicemail-delete.js';

const {
    voicemailDeleteFailureReason,
    deleteVoicemailsInTurn,
    voicemailDeleteFailureText,
    describeVoicemailDeleteFailures,
} = globalThis.CrestAppsSoftPhone;

const answer = (status, extra) => Object.assign({ status, ok: status >= 200 && status < 300, type: 'basic', redirected: false }, extra);

// Regression: the agent selected all five voicemails and pressed Delete. Four went; the server refused the fifth, and
// because a refusal came back as a redirect the browser followed it to a page that answered 200. The phone treated
// every answer alike, reloaded the list and said nothing, and one voicemail simply stayed.
describe('voicemailDeleteFailureReason', () => {
    it('accepts a 2xx answer', () => {
        expect(voicemailDeleteFailureReason(answer(200))).toBeNull();
        expect(voicemailDeleteFailureReason(answer(204))).toBeNull();
    });

    it('never mistakes a redirect for a deletion', () => {
        expect(voicemailDeleteFailureReason(answer(0, { type: 'opaqueredirect', ok: false }))).toBe('refused');
        expect(voicemailDeleteFailureReason(answer(200, { redirected: true }))).toBe('refused');
        expect(voicemailDeleteFailureReason(answer(302))).toBe('refused');
    });

    it('names why the server refused', () => {
        expect(voicemailDeleteFailureReason(answer(400))).toBe('expired');
        expect(voicemailDeleteFailureReason(answer(401))).toBe('signedOut');
        expect(voicemailDeleteFailureReason(answer(403))).toBe('forbidden');
        expect(voicemailDeleteFailureReason(answer(404))).toBe('notFound');
        expect(voicemailDeleteFailureReason(answer(409))).toBe('legalHold');
        expect(voicemailDeleteFailureReason(answer(500))).toBe('failed');
    });

    it('treats a missing answer as a network failure', () => {
        expect(voicemailDeleteFailureReason(null)).toBe('network');
    });
});

describe('deleteVoicemailsInTurn', () => {
    it('reports which voicemails were deleted and which were not, and why', async () => {
        const answers = { a: answer(200), b: answer(403), c: answer(200), d: answer(0, { type: 'opaqueredirect', ok: false }) };

        const outcome = await deleteVoicemailsInTurn(['a', 'b', 'c', 'd'], id => Promise.resolve(answers[id]));

        expect(outcome.deleted).toEqual(['a', 'c']);
        expect(outcome.failed).toEqual([{ id: 'b', reason: 'forbidden' }, { id: 'd', reason: 'refused' }]);
    });

    it('keeps going after a request that could not be sent', async () => {
        const outcome = await deleteVoicemailsInTurn(['a', 'b'], id => id === 'a' ? Promise.reject(new Error('offline')) : Promise.resolve(answer(200)));

        expect(outcome.deleted).toEqual(['b']);
        expect(outcome.failed).toEqual([{ id: 'a', reason: 'network' }]);
    });

    it('sends one request at a time, in the order the voicemails were selected', async () => {
        const order = [];
        let inFlight = 0;
        let maxInFlight = 0;

        await deleteVoicemailsInTurn(['a', 'b', 'c'], id => {
            order.push(id);
            inFlight++;
            maxInFlight = Math.max(maxInFlight, inFlight);

            return new Promise(resolve => setTimeout(() => {
                inFlight--;
                resolve(answer(200));
            }, 1));
        });

        expect(order).toEqual(['a', 'b', 'c']);
        expect(maxInFlight).toBe(1);
    });

    it('skips a selection without an id', async () => {
        const outcome = await deleteVoicemailsInTurn(['', null, 'a'], () => Promise.resolve(answer(200)));

        expect(outcome.deleted).toEqual(['a']);
        expect(outcome.failed).toEqual([]);
    });
});

describe('describeVoicemailDeleteFailures', () => {
    it('says nothing when every voicemail was deleted', () => {
        expect(describeVoicemailDeleteFailures({ deleted: ['a'], failed: [] }, {})).toBeNull();
    });

    it('explains a single refused voicemail with its reason', () => {
        const message = describeVoicemailDeleteFailures({ deleted: [], failed: [{ id: 'a', reason: 'forbidden' }] }, {});

        expect(message).toContain('could not be deleted');
        expect(message).toContain(voicemailDeleteFailureText('forbidden', {}));
    });

    it('counts the voicemails left behind when only some were deleted', () => {
        const message = describeVoicemailDeleteFailures(
            { deleted: ['a', 'b', 'c', 'd'], failed: [{ id: 'e', reason: 'refused' }] },
            { voicemailDeletePartiallyFailed: '{0} of {1} voicemails could not be deleted.' });

        expect(message).toContain('1 of 5 voicemails could not be deleted.');
    });

    it('uses the localized text it is given', () => {
        expect(voicemailDeleteFailureText('legalHold', { voicemailDeleteLegalHold: 'Retenu.' })).toBe('Retenu.');
        expect(voicemailDeleteFailureText('unknown-reason', {})).toBe(voicemailDeleteFailureText('failed', {}));
    });
});
