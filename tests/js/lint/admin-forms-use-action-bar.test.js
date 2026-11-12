import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

// Orchard Core's admin theme keeps the buttons of an edit page (Save, Cancel, Delete...) in a sticky bar at the
// bottom of the screen, so they stay visible however long the form is. A view opts in by putting its buttons in
// <div class="action-bar-bottom">, and marks the less used ones with action-bar-secondary. A form that still ends
// with its buttons in an ocat-wrapper/ocat-end-offset row leaves them at the end of the page, unlike every other one.
const modulesDirectory = join('src', 'Modules');

// A button row: an ocat-wrapper holding only an ocat-end-offset whose content has no other div, followed by the
// end of the form, optionally after the end of the Razor block around the row.
const trailingButtonRow = /<div class="ocat-wrapper[^"]*">\s*<div class="ocat-end-offset[^"]*">((?:(?!<div)[\s\S])*?)<\/div>\s*<\/div>\s*\}?\s*<\/form>/g;

// The other hand-written row: a <div class="mb-3"> or <div class="d-flex ... gap-2"> holding the buttons, then the end of
// the form. Storefront pages keep their buttons inline, so this one is checked only in views of an [Admin] controller.
const trailingPlainRow = /<div class="(?:mb-3|d-flex[^"]*\bgap-2\b[^"]*)">((?:(?!<div)[\s\S])*?)<\/div>\s*\}?\s*<\/form>/g;

// A small form inside a card, an accordion or a modal, such as "Generate codes" under a coupon, acts on that part of
// the page only, so it keeps its button next to its fields.
const nestedContainer = /\b(card|accordion|modal)\b/;

function isNested(text, index) {
    const formStart = text.lastIndexOf('<form', index);
    const classes = [];

    for (const [, closing, attributes] of text.slice(0, formStart).matchAll(/<(\/?)div\b([^>]*)>/g)) {
        if (closing) {
            classes.pop();
        } else {
            classes.push(/class="([^"]*)"/.exec(attributes)?.[1] ?? '');
        }
    }

    return classes.some(value => nestedContainer.test(value));
}

// Views/{Controller}/{Action}.cshtml of a controller marked [Admin], or of a controller whose actions are.
function isAdminView(path) {
    const parts = path.split(sep);
    const viewsIndex = parts.lastIndexOf('Views');

    // A partial (_Compose, _Thread) is a piece of a workspace page, not an edit page of its own.
    if (viewsIndex < 0 || parts.length - viewsIndex !== 3 || parts[parts.length - 1].startsWith('_')) {
        return false;
    }

    const controller = join(...parts.slice(0, viewsIndex), 'Controllers', `${parts[viewsIndex + 1]}Controller.cs`);

    return existsSync(controller) && /\[Admin\b/.test(readFileSync(controller, 'utf8'));
}

function views() {
    return readdirSync(modulesDirectory, { recursive: true })
        .map(file => String(file))
        .filter(file => file.endsWith('.cshtml') && !file.split(sep).some(part => part === 'bin' || part === 'obj'))
        .map(file => join(modulesDirectory, file))
        .map(path => ({ path, text: readFileSync(path, 'utf8') }));
}

describe('admin forms', () => {
    const all = views();

    it('are found, so the check below is not passing on nothing', () => {
        expect(all.filter(({ text }) => text.includes('action-bar-bottom')).length).toBeGreaterThan(100);
    });

    it('put their buttons in the sticky action bar', () => {
        const inline = all
            .filter(({ text }) => [...text.matchAll(trailingButtonRow)]
                .some(match => /class="btn\b|<button\b/.test(match[1]) && !isNested(text, match.index)))
            .map(({ path }) => path);

        expect(inline).toEqual([]);
    });

    it('do not end with a mb-3 or d-flex gap-2 row of buttons in an admin view', () => {
        const inline = all
            .filter(({ path }) => isAdminView(path))
            .filter(({ text }) => [...text.matchAll(trailingPlainRow)]
                .some(match => /class="btn\b|<button\b/.test(match[1]) && !isNested(text, match.index)))
            .map(({ path }) => path.split(sep).join('/'));

        expect(inline).toEqual([]);
    });

    it('find the admin views through their controllers, so the check above is not passing on nothing', () => {
        expect(all.filter(({ path }) => isAdminView(path)).length).toBeGreaterThan(300);
    });
});
