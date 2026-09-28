import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/host-bridge.js';

const {
    HOST_ACK_TIMEOUT_MS,
    HOST_ACTION_GRACE_MS,
    buildIncomingCallMessage,
    createHostDelegation,
    findHostChannel,
    presentationFor,
    readHostMessage
} = globalThis.CrestAppsSoftPhone;

const offer = callId => ({ type: 'incoming-call', protocol: 1, callId, from: '+17025550100', cards: [] });

function createDelegation() {
    const posted = [];
    const onChange = vi.fn();
    const delegation = createHostDelegation({ post: message => posted.push(message), onChange });

    return { delegation, posted, onChange };
}

function readyDelegation() {
    const created = createDelegation();

    created.delegation.receive({ type: 'host-ready', protocol: 1 });
    created.onChange.mockClear();

    return created;
}

// Regression: the Windows soft phone showed its own incoming-call notification while the page's full-screen modal
// showed the same call. The page now hides its modal only when the host confirms its notification is on screen; a
// notification is a must, so every path that is not a confirmation keeps (or brings back) the modal.
describe('createHostDelegation', () => {
    beforeEach(() => {
        vi.useFakeTimers();
    });

    afterEach(() => {
        vi.useRealTimers();
    });

    it('keeps the modal when no host answered the ready message', () => {
        const { delegation, posted } = createDelegation();

        delegation.sync(offer('call-1'), false);

        expect(posted).toEqual([]);
        expect(delegation.presentation(true)).toEqual({ showModal: true, ring: true });
    });

    it('tells a ready host about the ringing call and waits quietly for its confirmation', () => {
        const { delegation, posted } = readyDelegation();

        delegation.sync(offer('call-1'), false);

        expect(posted).toEqual([offer('call-1')]);
        expect(delegation.presentation(true)).toEqual({ showModal: false, ring: false });
    });

    it('hides the modal once the host confirms its notification for that call', () => {
        const { delegation, onChange } = readyDelegation();

        delegation.sync(offer('call-1'), false);
        delegation.receive({ type: 'incoming-call-shown', callId: 'call-1', ringing: true });

        expect(onChange).toHaveBeenCalled();
        expect(delegation.presentation(true)).toEqual({ showModal: false, ring: false });
    });

    it('rings in the page when the host confirms but does not ring itself', () => {
        const { delegation } = readyDelegation();

        delegation.sync(offer('call-1'), false);
        delegation.receive({ type: 'incoming-call-shown', callId: 'call-1', ringing: false });

        expect(delegation.presentation(true)).toEqual({ showModal: false, ring: true });
    });

    it('shows the modal when the host does not confirm in time', () => {
        const { delegation, onChange } = readyDelegation();

        delegation.sync(offer('call-1'), false);
        vi.advanceTimersByTime(HOST_ACK_TIMEOUT_MS - 1);

        expect(delegation.presentation(true).showModal).toBe(false);

        vi.advanceTimersByTime(1);

        expect(onChange).toHaveBeenCalled();
        expect(delegation.presentation(true)).toEqual({ showModal: true, ring: true });
    });

    it('ignores a confirmation for a different call', () => {
        const { delegation } = readyDelegation();

        delegation.sync(offer('call-1'), false);
        delegation.receive({ type: 'incoming-call-shown', callId: 'call-2', ringing: true });
        vi.advanceTimersByTime(HOST_ACK_TIMEOUT_MS);

        expect(delegation.presentation(true).showModal).toBe(true);
    });

    it('brings the modal back when the host notification is closed without an answer', () => {
        const { delegation } = readyDelegation();

        delegation.sync(offer('call-1'), false);
        delegation.receive({ type: 'incoming-call-shown', callId: 'call-1', ringing: true });
        delegation.receive({ type: 'incoming-call-dismissed', callId: 'call-1' });

        expect(delegation.presentation(true)).toEqual({ showModal: true, ring: true });
    });

    it('lets a late confirmation hide a modal that fell back', () => {
        const { delegation } = readyDelegation();

        delegation.sync(offer('call-1'), false);
        vi.advanceTimersByTime(HOST_ACK_TIMEOUT_MS);
        delegation.receive({ type: 'incoming-call-shown', callId: 'call-1', ringing: true });

        expect(delegation.presentation(true).showModal).toBe(false);
    });

    it('keeps a modal that was already on screen until the host confirms', () => {
        const { delegation } = readyDelegation();

        delegation.sync(offer('call-1'), true);

        expect(delegation.presentation(true)).toEqual({ showModal: true, ring: true });

        delegation.receive({ type: 'incoming-call-shown', callId: 'call-1', ringing: true });

        expect(delegation.presentation(true).showModal).toBe(false);
    });

    it('returns the agent choice from the host notification and stays quiet while it runs', () => {
        const { delegation } = readyDelegation();

        delegation.sync(offer('call-1'), false);
        delegation.receive({ type: 'incoming-call-shown', callId: 'call-1', ringing: true });

        const choice = delegation.receive({ type: 'incoming-call-action', callId: 'call-1', action: 'answer' });

        expect(choice).toEqual({ action: 'answer', callId: 'call-1', matchesOffer: true });
        expect(delegation.presentation(true)).toEqual({ showModal: false, ring: false });
    });

    it('shows the modal when the call still rings after the chosen action had time to run', () => {
        const { delegation } = readyDelegation();

        delegation.sync(offer('call-1'), false);
        delegation.receive({ type: 'incoming-call-shown', callId: 'call-1', ringing: true });
        delegation.receive({ type: 'incoming-call-action', callId: 'call-1', action: 'answer' });
        vi.advanceTimersByTime(HOST_ACTION_GRACE_MS);

        expect(delegation.presentation(true)).toEqual({ showModal: true, ring: true });
    });

    it('keeps waiting while the chosen answer is still under way, then falls back once it is not', () => {
        let underWay = true;
        const delegation = createHostDelegation({ post: () => { }, isActionUnderWay: () => underWay });

        delegation.receive({ type: 'host-ready', protocol: 1 });
        delegation.sync(offer('call-1'), false);
        delegation.receive({ type: 'incoming-call-shown', callId: 'call-1', ringing: true });
        delegation.receive({ type: 'incoming-call-action', callId: 'call-1', action: 'answer' });
        vi.advanceTimersByTime(HOST_ACTION_GRACE_MS * 3);

        expect(delegation.presentation(true).showModal).toBe(false);

        underWay = false;
        vi.advanceTimersByTime(HOST_ACTION_GRACE_MS);

        expect(delegation.presentation(true)).toEqual({ showModal: true, ring: true });
    });

    it('does not let a late confirmation cut short a running action', () => {
        const { delegation } = readyDelegation();

        delegation.sync(offer('call-1'), false);
        delegation.receive({ type: 'incoming-call-action', callId: 'call-1', action: 'decline' });
        delegation.receive({ type: 'incoming-call-shown', callId: 'call-1', ringing: true });
        vi.advanceTimersByTime(HOST_ACTION_GRACE_MS);

        expect(delegation.presentation(true).showModal).toBe(true);
    });

    it('reports a choice for a call the page is not showing without changing the offer on screen', () => {
        const { delegation } = readyDelegation();

        delegation.sync(offer('call-1'), false);
        delegation.receive({ type: 'incoming-call-shown', callId: 'call-1', ringing: true });

        const choice = delegation.receive({ type: 'incoming-call-action', callId: 'call-9', action: 'answer' });

        expect(choice).toEqual({ action: 'answer', callId: 'call-9', matchesOffer: false });
        expect(delegation.current().status).toBe('shown');
    });

    it('re-sends the offer only when its details change', () => {
        const { delegation, posted } = readyDelegation();

        delegation.sync(offer('call-1'), false);
        delegation.sync(offer('call-1'), false);

        const withCards = { ...offer('call-1'), cards: [{ id: 'c1', title: 'Jane Doe' }] };

        delegation.sync(withCards, false);

        expect(posted).toEqual([offer('call-1'), withCards]);
    });

    it('tells the host when the call stops ringing on the page', () => {
        const { delegation, posted } = readyDelegation();

        delegation.sync(offer('call-1'), false);
        delegation.sync(null, false);

        expect(posted.at(-1)).toEqual({ type: 'incoming-call-ended', callId: 'call-1' });
        expect(delegation.current()).toBeNull();
    });

    it('ends the previous call and starts over when a different call rings', () => {
        const { delegation, posted } = readyDelegation();

        delegation.sync(offer('call-1'), false);
        delegation.receive({ type: 'incoming-call-shown', callId: 'call-1', ringing: true });
        delegation.sync(offer('call-2'), false);

        expect(posted.slice(-2)).toEqual([{ type: 'incoming-call-ended', callId: 'call-1' }, offer('call-2')]);
        expect(delegation.current()).toMatchObject({ callId: 'call-2', status: 'pending' });
    });

    it('does not fall back for a call that already ended', () => {
        const { delegation, onChange } = readyDelegation();

        delegation.sync(offer('call-1'), false);
        delegation.sync(null, false);
        vi.advanceTimersByTime(HOST_ACK_TIMEOUT_MS);

        expect(onChange).not.toHaveBeenCalled();
    });
});

describe('presentationFor', () => {
    it('shows nothing with no offer on screen', () => {
        expect(presentationFor(false, { status: 'fallback' })).toEqual({ showModal: false, ring: false });
    });

    it('treats an unknown status as a fallback', () => {
        expect(presentationFor(true, { status: 'unknown' })).toEqual({ showModal: true, ring: true });
    });
});

describe('readHostMessage', () => {
    it('reads the host ready message only for a supported protocol', () => {
        expect(readHostMessage({ type: 'host-ready', protocol: 1 })).toEqual({ type: 'host-ready', protocol: 1 });
        expect(readHostMessage({ type: 'host-ready', protocol: 0 })).toBeNull();
        expect(readHostMessage({ type: 'host-ready' })).toBeNull();
    });

    it('reads a JSON string as well as an object', () => {
        expect(readHostMessage('{"type":"incoming-call-shown","callId":"call-1","ringing":true}'))
            .toEqual({ type: 'incoming-call-shown', callId: 'call-1', ringing: true });
    });

    it('accepts only the known actions', () => {
        expect(readHostMessage({ type: 'incoming-call-action', callId: 'call-1', action: 'voicemail' }))
            .toEqual({ type: 'incoming-call-action', callId: 'call-1', action: 'voicemail' });
        expect(readHostMessage({ type: 'incoming-call-action', callId: 'call-1', action: 'hangup' })).toBeNull();
    });

    it('rejects messages without a call id, unknown types, and malformed input', () => {
        expect(readHostMessage({ type: 'incoming-call-shown' })).toBeNull();
        expect(readHostMessage({ type: 'dial', number: '100' })).toBeNull();
        expect(readHostMessage('not json')).toBeNull();
        expect(readHostMessage(null)).toBeNull();
    });
});

describe('buildIncomingCallMessage', () => {
    const baseUrl = 'https://tenant.example.com/softphone';

    it('carries the caller, queue, heading, and voicemail capability', () => {
        const message = buildIncomingCallMessage({
            callId: 'call-1',
            from: '+17025550100',
            queue: 'Support',
            heading: 'Matched customers',
            canVoicemail: true,
            cards: []
        }, baseUrl);

        expect(message).toEqual({
            type: 'incoming-call',
            protocol: 1,
            callId: 'call-1',
            from: '+17025550100',
            queue: 'Support',
            heading: 'Matched customers',
            canVoicemail: true,
            cards: []
        });
    });

    it('resolves path-only card and link URLs against the page', () => {
        const message = buildIncomingCallMessage({
            callId: 'call-1',
            cards: [{
                id: 'c1',
                title: 'Jane Doe',
                url: '/Admin/Contents/ContentItems/abc/Edit',
                links: [{ text: 'Customer record', url: '/Admin/contact/abc' }]
            }]
        }, baseUrl);

        expect(message.cards[0].url).toBe('https://tenant.example.com/Admin/Contents/ContentItems/abc/Edit');
        expect(message.cards[0].links).toEqual([{ text: 'Customer record', url: 'https://tenant.example.com/Admin/contact/abc' }]);
    });

    it('drops URLs that are not web links', () => {
        const message = buildIncomingCallMessage({
            callId: 'call-1',
            cards: [{ title: 'Jane Doe', url: 'javascript:alert(1)', links: [{ text: 'x', url: 'file:///c:/x' }] }]
        }, baseUrl);

        expect(message.cards[0].url).toBe('');
        expect(message.cards[0].links).toEqual([]);
    });

    it('keeps only text badges and the resolved action labels', () => {
        const message = buildIncomingCallMessage({
            callId: 'call-1',
            cards: [{ title: 'Jane Doe', badges: ['Support', 7, ''], answerAndOpenText: 'Answer & open activity', openText: 'Open activity', secret: 'x' }]
        }, baseUrl);

        expect(message.cards[0]).toEqual({
            id: '',
            title: 'Jane Doe',
            subtitle: '',
            description: '',
            badges: ['Support'],
            url: '',
            answerAndOpenText: 'Answer & open activity',
            openText: 'Open activity',
            links: []
        });
    });
});

describe('findHostChannel', () => {
    it('finds the WebView2 channel', () => {
        const webview = { postMessage() { }, addEventListener() { } };

        expect(findHostChannel({ chrome: { webview } })).toBe(webview);
    });

    it('returns null in a normal browser', () => {
        expect(findHostChannel({ chrome: {} })).toBeNull();
        expect(findHostChannel({})).toBeNull();
        expect(findHostChannel(null)).toBeNull();
    });
});
