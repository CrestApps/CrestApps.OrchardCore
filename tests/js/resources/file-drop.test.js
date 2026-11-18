import { beforeAll, describe, expect, it } from 'vitest';

// The script enhances the pickers on the page when it loads; a page without any is enough here.
globalThis.document = globalThis.document ?? { readyState: 'complete', querySelectorAll: () => [], addEventListener: () => {} };

let drop;

beforeAll(async () => {
    await import('../../../src/Modules/CrestApps.OrchardCore.Resources/Assets/js/file-drop.js');
    drop = globalThis.CrestAppsFileDrop;
});

const file = (name, size = 10, type = '', lastModified = 1) => new File([new Uint8Array(size)], name, { type, lastModified });
const names = (files) => files.map((item) => item.name);

// Files dropped or picked one after another add up, as in the AI chat, instead of the last pick replacing the others.
describe('merge', () => {
    it('adds the files of each drop to the ones already chosen', () => {
        const chosen = drop.merge([file('log.txt')], [file('screen.png', 20, 'image/png'), file('notes.pdf')], { multiple: true });

        expect(names(chosen)).toEqual(['log.txt', 'screen.png', 'notes.pdf']);
    });

    it('skips a file that is already chosen', () => {
        const chosen = drop.merge([file('log.txt')], [file('log.txt'), file('log.txt', 11)], { multiple: true });

        expect(chosen.map((item) => [item.name, item.size])).toEqual([['log.txt', 10], ['log.txt', 11]]);
    });

    it('keeps one file, the last, when the picker takes one', () => {
        const chosen = drop.merge([file('a.txt')], [file('b.txt'), file('c.txt')], { multiple: false });

        expect(names(chosen)).toEqual(['c.txt']);
    });

    it('leaves out the files the picker does not accept', () => {
        const chosen = drop.merge([], [file('a.png', 1, 'image/png'), file('b.exe'), file('c.PDF')], { multiple: true, accept: 'image/*,.pdf' });

        expect(names(chosen)).toEqual(['a.png', 'c.PDF']);
    });
});

describe('accepts', () => {
    it.each([
        ['', 'a.exe', '', true],
        ['.pdf, .docx', 'report.DOCX', '', true],
        ['.pdf', 'report.docx', '', false],
        ['image/*', 'photo.jpg', 'image/jpeg', true],
        ['image/png', 'photo.jpg', 'image/jpeg', false],
        ['text/plain', 'a.txt', 'text/plain', true],
    ])('accept "%s" with %s (%s) is %s', (accept, name, type, expected) => {
        expect(drop.accepts(file(name, 1, type), accept)).toBe(expected);
    });
});

describe('formatSize', () => {
    it.each([
        [0, '0 B'],
        [999, '999 B'],
        [2048, '2 KB'],
        [1536 * 1024, '1.5 MB'],
        [3 * 1024 * 1024 * 1024, '3 GB'],
    ])('%i bytes is %s', (bytes, expected) => {
        expect(drop.formatSize(bytes)).toBe(expected);
    });
});
