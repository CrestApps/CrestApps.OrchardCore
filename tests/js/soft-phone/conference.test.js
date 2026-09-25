import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/call-legs.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/conference.js';

const { planMerge, buildActiveCallsHtml } = globalThis.CrestAppsSoftPhone;

const escapeHtml = value => String(value == null ? '' : value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');

const held = { callId: 'a', state: 'OnHold' };
const live = { callId: 'b', state: 'Connected' };
const third = { callId: 'c', state: 'OnHold' };

// Bug: with one call held and a second call up, the agent could not see how to join them. Merge only appeared once
// two checkboxes were ticked, as an unlabelled icon among the call buttons.
describe('planMerge', () => {
    it('offers to merge a held call with the call that is up, with nothing ticked', () => {
        expect(planMerge([held, live], {})).toEqual({
            available: true,
            callIds: ['a', 'b'],
            calls: [held, live],
            usesSelection: false
        });
    });

    it('offers nothing with a single call', () => {
        expect(planMerge([live], {}).available).toBe(false);
    });

    it('merges only the ticked calls when the agent ticked two or more', () => {
        const plan = planMerge([held, live, third], { a: true, c: true });

        expect(plan.callIds).toEqual(['a', 'c']);
        expect(plan.usesSelection).toBe(true);
    });

    it('merges every call when fewer than two are ticked', () => {
        expect(planMerge([held, live, third], { a: true }).callIds).toEqual(['a', 'b', 'c']);
    });

    it('never offers a call this browser placed itself, which the server cannot merge', () => {
        const browserCall = { callId: 'x', state: 'Connected', browserOriginated: true };

        expect(planMerge([held, browserCall], {}).available).toBe(false);
    });

    it('does not count a call that is still ringing or connecting', () => {
        expect(planMerge([held, { callId: 'r', state: 'Ringing' }], {}).available).toBe(false);
    });

    it('offers nothing once the calls are already one conference', () => {
        const merged = [
            { callId: 'a', state: 'Connected', metadata: { isConference: true } },
            { callId: 'b', state: 'Connected', metadata: { isConference: 'true' } }
        ];

        expect(planMerge(merged, {}).available).toBe(false);
    });

    it('offers to add a new call to an existing conference', () => {
        const calls = [
            { callId: 'a', state: 'OnHold', metadata: { isConference: true } },
            { callId: 'b', state: 'OnHold', metadata: { isConference: true } },
            { callId: 'n', state: 'Connected' }
        ];

        expect(planMerge(calls, {})).toMatchObject({ available: true, callIds: ['a', 'b', 'n'] });
    });

    it('reads the state and conference flag through the accessors it is given', () => {
        const plan = planMerge([{ callId: 'a', state: 3 }, { callId: 'b', state: 4 }], {}, {
            stateOf: call => (call.state === 3 ? 'Connected' : 'OnHold'),
            isConference: () => false
        });

        expect(plan.available).toBe(true);
    });
});

describe('buildActiveCallsHtml', () => {
    const strings = {
        mergeCalls: 'Merge calls',
        mergeSummary: 'Join {0} in one conference.',
        conferenceParticipants: 'Conference · {0} participants',
        hangupParticipant: 'Hang up {0}',
        selectCall: 'Select {0}'
    };

    it('names the calls the Merge action joins', () => {
        const html = buildActiveCallsHtml({
            calls: [
                { callId: 'a', number: '(555) 123-4567', state: 'On hold', selectable: true },
                { callId: 'b', number: '(555) 765-4321', state: 'In call', current: true, selectable: true }
            ],
            merge: { available: true, numbers: ['(555) 123-4567', '(555) 765-4321'] }
        }, strings, escapeHtml);

        expect(html).toContain('data-telephony-merge-calls');
        expect(html).toContain('Join (555) 123-4567 + (555) 765-4321 in one conference.');
        expect(html).toContain('Merge calls');
        expect(html).not.toContain('data-telephony-conference-participant');
    });

    it('leaves the Merge action out when there is nothing to merge', () => {
        const html = buildActiveCallsHtml({
            calls: [{ callId: 'a', number: '1', state: 'In call' }],
            merge: { available: false, numbers: [] }
        }, strings, escapeHtml);

        expect(html).not.toContain('data-telephony-merge-calls');
    });

    it('lists a conference under its own heading, with a hang-up for each participant', () => {
        const html = buildActiveCallsHtml({
            calls: [
                { callId: 'a', number: '(555) 123-4567', state: 'In conference', inConference: true, canHangup: true, selectable: true },
                { callId: 'b', number: '(555) 765-4321', state: 'In conference', inConference: true, canHangup: true, selectable: true }
            ],
            merge: { available: false }
        }, strings, escapeHtml);

        expect(html).toContain('Conference · 2 participants');
        expect(html).toContain('data-telephony-conference-participant="a"');
        expect(html).toContain('data-telephony-participant-hangup="b"');
        expect(html).toContain('aria-label="Hang up (555) 765-4321"');
    });

    it('offers no participant hang-up when the provider cannot hang up', () => {
        const html = buildActiveCallsHtml({
            calls: [{ callId: 'a', number: '1', state: 'In conference', inConference: true, canHangup: false }]
        }, strings, escapeHtml);

        expect(html).not.toContain('data-telephony-participant-hangup');
    });

    it('escapes what it shows', () => {
        const html = buildActiveCallsHtml({
            calls: [{ callId: '"><x', number: '<b>', state: 'In call', selectable: true }]
        }, strings, escapeHtml);

        expect(html).not.toContain('<b>');
        expect(html).not.toContain('"><x');
    });
});
