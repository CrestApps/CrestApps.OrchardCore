/*
 * One remote audio element per call.
 *
 * The provider SDK plays each call's far end through the media element it is handed. The soft phone used to hand
 * every call the one <audio> element on the page, so a second call attached its stream over the first's (the Telnyx
 * SDK's SHARED_REMOTE_ELEMENT_OVERWRITE warning, 33011): the held call lost its audio for good, muting one call for
 * hold muted the other, and when the second call ended the first was left with an element pointing at a dead stream.
 *
 * The pool gives the first call the page's own element and every call beside it an element of its own, created on
 * demand and removed when that call ends. It is pure bookkeeping: creating and disposing an element are the caller's.
 *
 * Part of the soft phone, concatenated ahead of soft-phone.js by the module asset pipeline. It attaches to a shared
 * namespace rather than exporting, so the same file runs in the browser bundle and under the unit tests.
 */
(function (root) {
    'use strict';

    var softPhone = root.CrestAppsSoftPhone = root.CrestAppsSoftPhone || {};

    //   primary        - the page's own remote audio element; the first call to need one gets it.
    //   createElement  - makes an element for a call beside another; returns null when it cannot.
    //   disposeElement - removes an element the pool created, once its call no longer needs it.
    function createRemoteElementPool(primary, createElement, disposeElement) {
        return {
            primary: primary || null,
            create: typeof createElement === 'function' ? createElement : function () { return null; },
            dispose: typeof disposeElement === 'function' ? disposeElement : function () { },
            byKey: {}
        };
    }

    function isElementInUse(pool, element) {
        return Object.keys(pool.byKey).some(function (key) {
            return pool.byKey[key] === element;
        });
    }

    // The element a call plays through, claimed for it on first use. The page's own element goes to whichever call
    // needs one while it is free; a call beside another gets a new element. When no element can be made the page's own
    // is shared, which is no worse than before.
    function acquireRemoteElement(pool, key) {
        if (!pool || !key) {
            return pool ? pool.primary : null;
        }

        if (pool.byKey[key]) {
            return pool.byKey[key];
        }

        var element = pool.primary && !isElementInUse(pool, pool.primary)
            ? pool.primary
            : pool.create() || pool.primary;

        if (element) {
            pool.byKey[key] = element;
        }

        return element;
    }

    // The element a call already plays through, without claiming one.
    function remoteElementFor(pool, key) {
        return pool && key && pool.byKey[key] ? pool.byKey[key] : null;
    }

    // Moves a claim to another key: an element claimed before the provider gave the call its id.
    function rekeyRemoteElement(pool, fromKey, toKey) {
        if (!pool || !fromKey || !toKey || fromKey === toKey || !pool.byKey[fromKey]) {
            return;
        }

        pool.byKey[toKey] = pool.byKey[fromKey];
        delete pool.byKey[fromKey];
    }

    // Frees a call's element. One the pool created is disposed once no call uses it; the page's own is only freed.
    function releaseRemoteElement(pool, key) {
        if (!pool || !key || !pool.byKey[key]) {
            return;
        }

        var element = pool.byKey[key];

        delete pool.byKey[key];

        if (element !== pool.primary && !isElementInUse(pool, element)) {
            try {
                pool.dispose(element);
            } catch (error) { /* best effort */ }
        }
    }

    // Frees every call's element.
    function releaseAllRemoteElements(pool) {
        if (!pool) {
            return;
        }

        Object.keys(pool.byKey).forEach(function (key) {
            releaseRemoteElement(pool, key);
        });
    }

    softPhone.createRemoteElementPool = createRemoteElementPool;
    softPhone.acquireRemoteElement = acquireRemoteElement;
    softPhone.remoteElementFor = remoteElementFor;
    softPhone.rekeyRemoteElement = rekeyRemoteElement;
    softPhone.releaseRemoteElement = releaseRemoteElement;
    softPhone.releaseAllRemoteElements = releaseAllRemoteElements;
}(typeof globalThis !== 'undefined' ? globalThis : window));
