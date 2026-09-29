import { describe, expect, it } from 'vitest';

import '../../../src/Modules/CrestApps.OrchardCore.Omnichannel.Messaging/Assets/js/shared/messaging-attachments.js';

const { attachmentAcceptAttribute, findAttachmentFormat, fitDimensions, needsShrinking, perFileBudget, planAttachments } = globalThis.CrestAppsMessaging;

const file = (type, size = 1000, name = 'a') => ({ type, size, name });

// The formats as a channel declares them: SMS carries pictures only; a channel such as email can carry documents.
const images = [
    { name: 'JPEG', contentType: 'image/jpeg', extensions: ['.jpg', '.jpeg'], isImage: true, canShrink: true },
    { name: 'PNG', contentType: 'image/png', extensions: ['.png'], isImage: true, canShrink: true },
    { name: 'GIF', contentType: 'image/gif', extensions: ['.gif'], isImage: true, canShrink: false },
];
const documents = [
    { name: 'PDF', contentType: 'application/pdf', extensions: ['.pdf'], isImage: false, canShrink: false },
    { name: 'Text', contentType: 'text/plain', extensions: ['.txt'], isImage: false, canShrink: false },
];

// Only the files the channel carries may be attached; anything else dropped on the conversation is refused rather than
// silently sent as something the customer cannot open.
describe('planAttachments', () => {
    it('accepts what the channel carries and refuses the rest', () => {
        const plan = planAttachments(0, [file('image/png'), file('application/pdf', 1000, 'form.pdf'), file('image/jpeg')], 10, images);

        expect(plan.accepted.map((f) => f.type)).toEqual(['image/png', 'image/jpeg']);
        expect(plan.notAllowed).toHaveLength(1);
        expect(plan.overCount).toHaveLength(0);
    });

    it('lets a channel that carries documents take them', () => {
        const plan = planAttachments(0, [file('application/pdf', 1000, 'form.pdf')], 10, [...images, ...documents]);

        expect(plan.accepted).toHaveLength(1);
    });

    it('stops at the number of files one message may carry, counting those already attached', () => {
        const plan = planAttachments(9, [file('image/png'), file('image/png')], 10, images);

        expect(plan.accepted).toHaveLength(1);
        expect(plan.overCount).toHaveLength(1);
    });

    it('refuses everything on a channel that carries no files', () => {
        expect(planAttachments(0, [file('image/png')], 10, []).notAllowed).toHaveLength(1);
    });

    it('refuses an SVG, which a browser would run as a page', () => {
        expect(findAttachmentFormat(file('image/svg+xml', 10, 'cat.svg'), images)).toBeNull();
    });

    it('recognises a file the browser gave no type by its extension', () => {
        expect(findAttachmentFormat(file('', 10, 'Notes.TXT'), documents).name).toBe('Text');
    });

    it('builds the file picker filter from the channel formats', () => {
        expect(attachmentAcceptAttribute(documents)).toBe('application/pdf,.pdf,text/plain,.txt');
    });
});

// A phone photo is several times larger than a carrier accepts, so each picture is shrunk to its share of the
// message's budget on a channel that shrinks them. A GIF is never redrawn, because that loses its animation, and a
// document is never touched.
describe('shrinking', () => {
    it('splits the budget between the files', () => {
        expect(perFileBudget(1000, 4)).toBe(250);
        expect(perFileBudget(1000, 0)).toBe(1000);
    });

    it('shrinks a still picture over its share, but never a GIF or a document', () => {
        expect(needsShrinking(file('image/jpeg', 5000), 1000, images[0], true)).toBe(true);
        expect(needsShrinking(file('image/jpeg', 500), 1000, images[0], true)).toBe(false);
        expect(needsShrinking(file('image/gif', 5000), 1000, images[2], true)).toBe(false);
        expect(needsShrinking(file('application/pdf', 5000), 1000, documents[0], true)).toBe(false);
    });

    it('does not shrink on a channel that does not ask for it', () => {
        expect(needsShrinking(file('image/jpeg', 5000), 1000, images[0], false)).toBe(false);
    });

    it('keeps the proportions when fitting the longer edge', () => {
        expect(fitDimensions(4000, 3000, 2000)).toEqual({ width: 2000, height: 1500 });
        expect(fitDimensions(1000, 3000, 1500)).toEqual({ width: 500, height: 1500 });
        expect(fitDimensions(800, 600, 2000)).toEqual({ width: 800, height: 600 });
    });
});
