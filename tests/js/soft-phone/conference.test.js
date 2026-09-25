import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/call-legs.js';
import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/conference.js';

const { planMerge, conferenceAfterMerge, buildActiveCallsHtml } = globalThis.CrestAppsSoftPhone;

const escapeHtml = value => String(value == null ? '' : value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');

const held = { callId: 'a', state: 'OnHold' };
const live = { callId: 'b', state: 'Connected' };
const third = { callId: 'c', state: 'OnHold' };

// With several calls up, the agent picks the calls to join by ticking the box beside each line; Merge is offered as soon
// as two lines could be joined, and does something only once two or more are ticked.
describe('planMerge', () => {
    it('offers a merge for two calls that can be joined, but waits for the agent to tick them', () => {
        expect(planMerge([held, live], {})).toMatchObject({
            offered: true,
            canMerge: false,
            selectedCount: 0,
            callIds: [],
            eligibleCallIds: ['a', 'b'],
            allSelected: false
        });
    });

    it('does not merge a single ticked call', () => {
        expect(planMerge([held, live], { a: true })).toMatchObject({ offered: true, canMerge: false, selectedCount: 1 });
    });

    it('merges the two calls once both are ticked', () => {
        expect(planMerge([held, live], { a: true, b: true })).toMatchObject({
            canMerge: true,
            selectedCount: 2,
            callIds: ['a', 'b'],
            calls: [held, live],
            addsToConference: false,
            allSelected: true
        });
    });

    it('merges only the ticked calls out of three, and all three when all are ticked', () => {
        expect(planMerge([held, live, third], { a: true, c: true }).callIds).toEqual(['a', 'c']);
        expect(planMerge([held, live, third], { a: true, b: true, c: true })).toMatchObject({ canMerge: true, selectedCount: 3, callIds: ['a', 'b', 'c'] });
    });

    it('offers nothing with a single call', () => {
        expect(planMerge([live], {}).offered).toBe(false);
    });

    it('ignores a tick left on a call that has since ended', () => {
        expect(planMerge([held, live], { a: true, gone: true })).toMatchObject({ canMerge: false, selectedCount: 1 });
    });

    // A call dialed from this browser runs in the provider SDK alone, so the server cannot join it to anything.
    it('never counts a call this browser placed itself, and says why nothing can be merged', () => {
        const browserCall = { callId: 'x', state: 'Connected', browserOriginated: true };

        expect(planMerge([held, browserCall], { a: true, x: true })).toMatchObject({ offered: false, canMerge: false, blocked: 'browser-call' });
        expect(planMerge([held, live, browserCall], {})).toMatchObject({ offered: true, blocked: 'browser-call', eligibleCallIds: ['a', 'b'] });
        expect(planMerge([held, live], {}).blocked).toBe('');
    });

    it('does not count a call that is still ringing or connecting', () => {
        expect(planMerge([held, { callId: 'r', state: 'Ringing' }], {}).offered).toBe(false);
    });

    it('offers nothing once the calls are already one conference', () => {
        const merged = [
            { callId: 'a', state: 'Connected', metadata: { isConference: true } },
            { callId: 'b', state: 'Connected', metadata: { isConference: 'true' } }
        ];

        expect(planMerge(merged, { a: true, b: true })).toMatchObject({ offered: false, canMerge: false });
    });

    describe('with a conference already running', () => {
        const calls = [
            { callId: 'a', state: 'Connected', metadata: { isConference: true, conferenceName: 'conf-a' } },
            { callId: 'b', state: 'Connected', metadata: { isConference: true, conferenceName: 'conf-a' } },
            { callId: 'n', state: 'OnHold' }
        ];

        it('counts the conference as one line to join', () => {
            expect(planMerge(calls, {})).toMatchObject({ offered: true, canMerge: false });
            expect(planMerge(calls, { a: true })).toMatchObject({ canMerge: false, selectedCount: 1 });
        });

        it('adds a ticked call to the conference, asking the provider for the conference it already made', () => {
            expect(planMerge(calls, { b: true, n: true })).toMatchObject({
                canMerge: true,
                selectedCount: 2,
                addsToConference: true,
                conferenceName: 'conf-a',
                callIds: ['a', 'n']
            });
        });

        it('names the conference by the call it was made from', () => {
            const madeFromB = calls.map(call => call.metadata
                ? { ...call, metadata: { ...call.metadata, conferencePrimaryCallId: 'b' } }
                : call);

            expect(planMerge(madeFromB, { a: true, n: true }).callIds).toEqual(['b', 'n']);
        });

        // A provider's report of the held participant replaces the call, and with it what the phone noted on it.
        it('reads the conference\'s own call and name from whichever participant still carries them', () => {
            const reported = [
                { callId: 'a', state: 'OnHold', metadata: { isConference: true } },
                { callId: 'b', state: 'Connected', metadata: { isConference: true, conferencePrimaryCallId: 'b', conferenceName: 'conf-b' } },
                { callId: 'n', state: 'Connected' }
            ];

            expect(planMerge(reported, { a: true, n: true })).toMatchObject({ callIds: ['b', 'n'], conferenceName: 'conf-b' });
        });
    });

    it('reads the state and conference flag through the accessors it is given', () => {
        const plan = planMerge([{ callId: 'a', state: 3 }, { callId: 'b', state: 4 }], { a: true, b: true }, {
            stateOf: call => (call.state === 3 ? 'Connected' : 'OnHold'),
            isConference: () => false
        });

        expect(plan.canMerge).toBe(true);
    });
});

describe('conferenceAfterMerge', () => {
    it('makes the merged calls one conference, named after the call the provider made it from', () => {
        const plan = planMerge([held, live, third], { a: true, c: true });

        expect(conferenceAfterMerge([held, live, third], plan, { call: { callId: 'a', metadata: { conferenceName: 'conf-a' } } }))
            .toEqual({ callIds: ['a', 'c'], primaryCallId: 'a', conferenceName: 'conf-a' });
    });

    it('keeps the running conference and the call it was made from when a call is added to it', () => {
        const calls = [
            { callId: 'a', state: 'Connected', metadata: { isConference: true, conferenceName: 'conf-a' } },
            { callId: 'b', state: 'Connected', metadata: { isConference: true, conferenceName: 'conf-a' } },
            { callId: 'n', state: 'OnHold' }
        ];
        const plan = planMerge(calls, { a: true, n: true });

        expect(conferenceAfterMerge(calls, plan, { call: { callId: 'a' } }))
            .toEqual({ callIds: ['a', 'b', 'n'], primaryCallId: 'a', conferenceName: 'conf-a' });
    });

    it('falls back to the first merged call when the provider named no call', () => {
        const plan = planMerge([held, live], { a: true, b: true });

        expect(conferenceAfterMerge([held, live], plan, {})).toEqual({ callIds: ['a', 'b'], primaryCallId: 'a', conferenceName: '' });
    });
});

describe('buildActiveCallsHtml', () => {
    const strings = {
        mergeSummary: 'Join {0} in one conference.',
        mergeSelectHint: 'Tick two or more calls to merge them.',
        mergeSelectedCalls: 'Merge {0} calls',
        mergeCalls: 'Merge calls',
        addToConference: 'Add to conference',
        addToConferenceSummary: 'Add {0} to the conference.',
        selectAllCalls: 'Select all',
        conferenceParticipants: 'Conference · {0} participants',
        hangupParticipant: 'Hang up {0}',
        selectCall: 'Select {0}',
        cannotMergeBrowserCall: 'A call dialed from this phone cannot be merged.'
    };

    const twoCalls = [
        { callId: 'a', number: '(555) 123-4567', state: 'On hold', selectable: true },
        { callId: 'b', number: '(555) 765-4321', state: 'In call', current: true, selectable: true }
    ];

    it('puts a checkbox beside every call line', () => {
        const html = buildActiveCallsHtml({ calls: twoCalls, merge: { offered: true } }, strings, escapeHtml);

        expect(html).toContain('data-telephony-conference-call="a"');
        expect(html).toContain('data-telephony-conference-call="b"');
        expect(html).toContain('aria-label="Select (555) 765-4321"');
    });

    it('offers Merge disabled until two calls are ticked, and says what to do', () => {
        const html = buildActiveCallsHtml({ calls: twoCalls, merge: { offered: true, canMerge: false, selectedCount: 1, numbers: ['(555) 123-4567'] } }, strings, escapeHtml);

        expect(html).toMatch(/<button[^>]*data-telephony-merge-calls[^>]*disabled/);
        expect(html).toContain('Tick two or more calls to merge them.');
        expect(html).toContain('Merge calls</button>');
    });

    it('names how many calls Merge joins, and which, once they are ticked', () => {
        const html = buildActiveCallsHtml({
            calls: twoCalls.map(call => ({ ...call, selected: true })),
            merge: { offered: true, canMerge: true, selectedCount: 2, allSelected: true, numbers: ['(555) 123-4567', '(555) 765-4321'] }
        }, strings, escapeHtml);

        expect(html).not.toMatch(/<button[^>]*data-telephony-merge-calls[^>]*disabled/);
        expect(html).toContain('Merge 2 calls');
        expect(html).toContain('Join (555) 123-4567 + (555) 765-4321 in one conference.');
        expect(html).toMatch(/data-telephony-conference-call="a"[^>]*checked/);
        expect(html).toMatch(/data-telephony-merge-select-all[^>]*checked/);
    });

    it('offers to add the ticked call to a running conference', () => {
        const html = buildActiveCallsHtml({
            calls: twoCalls,
            merge: { offered: true, canMerge: true, selectedCount: 2, addsToConference: true, numbers: ['(555) 123-4567'] }
        }, strings, escapeHtml);

        expect(html).toContain('Add to conference');
        expect(html).toContain('Add (555) 123-4567 to the conference.');
    });

    it('leaves the Merge action out when there is nothing to merge', () => {
        const html = buildActiveCallsHtml({
            calls: [{ callId: 'a', number: '1', state: 'In call', selectable: true }],
            merge: { offered: false, numbers: [] }
        }, strings, escapeHtml);

        expect(html).not.toContain('data-telephony-merge-calls');
        expect(html).not.toContain('data-telephony-merge-select-all');
    });

    it('shows a call that cannot be merged with a disabled checkbox that says why', () => {
        const html = buildActiveCallsHtml({
            calls: [
                { callId: 'x', number: '(702) 499-3350', state: 'In call', selectable: false, unselectableReason: 'browser-call' },
                { callId: 'y', number: '(702) 555-0100', state: 'On hold', selectable: false, unselectableReason: 'browser-call' }
            ],
            merge: { offered: false, blocked: 'browser-call' }
        }, strings, escapeHtml);

        expect(html).toMatch(/data-telephony-conference-call="x"[^>]*disabled/);
        expect(html).toContain('title="A call dialed from this phone cannot be merged."');
        expect(html).toContain('data-telephony-merge-blocked');
        expect(html).not.toContain('data-telephony-merge-calls');
    });

    it('lists a conference under its own heading, with a hang-up for each participant', () => {
        const html = buildActiveCallsHtml({
            calls: [
                { callId: 'a', number: '(555) 123-4567', state: 'In conference', inConference: true, canHangup: true, selectable: true },
                { callId: 'b', number: '(555) 765-4321', state: 'In conference', inConference: true, canHangup: true, selectable: true }
            ],
            merge: { offered: false }
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
            calls: [{ callId: '"><x', number: '<b>', state: 'In call', selectable: true }],
            merge: { offered: true, numbers: ['<i>'] }
        }, strings, escapeHtml);

        expect(html).not.toContain('<b>');
        expect(html).not.toContain('<i>');
        expect(html).not.toContain('"><x');
    });
});
