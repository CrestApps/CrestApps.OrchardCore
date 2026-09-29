import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/shared/messaging-attachments.js';

const { fitDimensions, isSendableImageType, needsShrinking, perFileBudget, planAttachments } = globalThis.CrestAppsMessaging;

const file = (type, size = 1000, name = 'a') => ({ type, size, name });

// Only pictures the carriers accept may be attached; anything else dropped on the conversation is refused rather
// than silently sent as something the customer cannot open.
describe('planAttachments', () => {
    it('accepts pictures and refuses other files', () => {
        const plan = planAttachments(0, [file('image/png'), file('application/pdf'), file('image/jpeg')], 10);

        expect(plan.accepted.map((f) => f.type)).toEqual(['image/png', 'image/jpeg']);
        expect(plan.notImages).toHaveLength(1);
        expect(plan.overCount).toHaveLength(0);
    });

    it('stops at the number of pictures one message may carry, counting those already attached', () => {
        const plan = planAttachments(9, [file('image/png'), file('image/png')], 10);

        expect(plan.accepted).toHaveLength(1);
        expect(plan.overCount).toHaveLength(1);
    });

    it('refuses an SVG, which a browser would run as a page', () => {
        expect(isSendableImageType('image/svg+xml')).toBe(false);
    });
});

// A phone photo is several times larger than a carrier accepts, so each picture is shrunk to its share of the
// message's budget. A GIF is never redrawn, because redrawing it loses its animation.
describe('shrinking', () => {
    it('splits the budget between the pictures', () => {
        expect(perFileBudget(1000, 4)).toBe(250);
        expect(perFileBudget(1000, 0)).toBe(1000);
    });

    it('shrinks a still picture over its share but never a GIF', () => {
        expect(needsShrinking(file('image/jpeg', 5000), 1000)).toBe(true);
        expect(needsShrinking(file('image/jpeg', 500), 1000)).toBe(false);
        expect(needsShrinking(file('image/gif', 5000), 1000)).toBe(false);
    });

    it('keeps the proportions when fitting the longer edge', () => {
        expect(fitDimensions(4000, 3000, 2000)).toEqual({ width: 2000, height: 1500 });
        expect(fitDimensions(1000, 3000, 1500)).toEqual({ width: 500, height: 1500 });
        expect(fitDimensions(800, 600, 2000)).toEqual({ width: 800, height: 600 });
    });
});
