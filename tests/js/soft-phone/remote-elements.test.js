import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Telephony/Assets/js/soft-phone/remote-elements.js';

const {
    createRemoteElementPool,
    acquireRemoteElement,
    remoteElementFor,
    rekeyRemoteElement,
    releaseRemoteElement,
    releaseAllRemoteElements,
} = globalThis.CrestAppsSoftPhone;

function createPool() {
    const primary = { name: 'page element' };
    const created = [];
    const disposed = [];
    const pool = createRemoteElementPool(
        primary,
        () => {
            const element = { name: 'call element ' + (created.length + 1) };
            created.push(element);
            return element;
        },
        element => disposed.push(element));

    return { pool, primary, created, disposed };
}

// Bug: every call was handed the page's one remote audio element. A second call attached its stream over the held
// call's (the Telnyx SDK's SHARED_REMOTE_ELEMENT_OVERWRITE warning), so the held call lost its audio and the two
// calls' hold mutes fought over the same element.
describe('the remote element pool', () => {
    it('gives the first call the page element and a call beside it an element of its own', () => {
        const { pool, primary, created } = createPool();

        const first = acquireRemoteElement(pool, 'call-1');
        const second = acquireRemoteElement(pool, 'call-2');

        expect(first).toBe(primary);
        expect(second).toBe(created[0]);
        expect(second).not.toBe(first);
    });

    it('gives a call the same element every time it asks', () => {
        const { pool } = createPool();

        expect(acquireRemoteElement(pool, 'call-1')).toBe(acquireRemoteElement(pool, 'call-1'));
        expect(remoteElementFor(pool, 'call-1')).toBe(acquireRemoteElement(pool, 'call-1'));
        expect(remoteElementFor(pool, 'call-9')).toBe(null);
    });

    it('removes a created element when its call ends, and hands the page element to the next call once free', () => {
        const { pool, primary, created, disposed } = createPool();
        acquireRemoteElement(pool, 'call-1');
        acquireRemoteElement(pool, 'call-2');

        releaseRemoteElement(pool, 'call-2');
        releaseRemoteElement(pool, 'call-1');

        expect(disposed).toEqual([created[0]]);
        expect(acquireRemoteElement(pool, 'call-3')).toBe(primary);
    });

    it('never disposes the page element', () => {
        const { pool, primary, disposed } = createPool();
        acquireRemoteElement(pool, 'call-1');

        releaseAllRemoteElements(pool);

        expect(disposed).not.toContain(primary);
        expect(remoteElementFor(pool, 'call-1')).toBe(null);
    });

    // The element is claimed before the provider has given the call an id.
    it('moves a claim made under a stand-in key to the call id', () => {
        const { pool, primary } = createPool();
        acquireRemoteElement(pool, 'pending-1');

        rekeyRemoteElement(pool, 'pending-1', 'call-1');

        expect(remoteElementFor(pool, 'call-1')).toBe(primary);
        expect(remoteElementFor(pool, 'pending-1')).toBe(null);
    });

    it('shares the page element when no element can be made, which is no worse than before', () => {
        const primary = { name: 'page element' };
        const pool = createRemoteElementPool(primary, () => null);

        acquireRemoteElement(pool, 'call-1');

        expect(acquireRemoteElement(pool, 'call-2')).toBe(primary);
    });
});
