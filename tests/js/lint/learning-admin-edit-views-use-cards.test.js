import { readdirSync, readFileSync } from 'node:fs';
import { basename, join, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

// Every Learning admin edit page, part editor and settings editor groups its fields in Bootstrap cards (a card with a
// card-header naming the group and a card-body), as the other admin screens do; a form with only a few fields still
// gets one card, so every page looks the same. A field left outside a card sits on the bare page background, unlike
// every other editor. The buttons stay in the sticky action-bar-bottom, which is not a field.
const modulesDirectory = join('src', 'Modules');

// The editors this rule is about. Workflow activity, deployment step and data pipeline step fields (*.Fields.Edit)
// render inside their own designers, report and list filters inside their lists, and Shared partials inside the card
// of the page that renders them.
function isAdminEditView(path, text) {
    const parts = path.split(sep);
    const name = basename(path);

    if (parts.includes('Shared') || parts.includes('Items') || name.startsWith('_')) {
        return false;
    }

    if (name.endsWith('.Fields.Edit.cshtml') || /Filters?\.Edit\.cshtml$/.test(name)) {
        return false;
    }

    if (name.endsWith('.Edit.cshtml')) {
        return true;
    }

    // A page of an admin controller (Views/<Controller>/<Action>.cshtml) whose form keeps its buttons in the action bar.
    return parts.length >= 4 && parts[parts.length - 3] === 'Views' && text.includes('action-bar-bottom') && /<form\b/.test(text);
}

// A field: an ocat row, a check box row, a visible input, a select, a text area or a picker component.
const fieldPattern = /<div class="(?:ocat-wrapper|ocat-limited-wrapper|form-check)\b[^"]*"|<input\b(?![^>]*type="hidden")[^>]*>|<select\b|<textarea\b|Component\.InvokeAsync\("(?:UserPicker|ItemSelector)"/g;

const containerTag = /<(\/?)(div|fieldset|section|template)\b([^>]*?)(\/?)>/g;

// The classes of the elements open at an index, outermost first.
function openClasses(text, index) {
    const stack = [];

    for (const match of text.slice(0, index).matchAll(containerTag)) {
        const [, closing, , attributes, selfClosing] = match;

        if (selfClosing) {
            continue;
        }

        if (closing) {
            stack.pop();
        } else {
            stack.push(/class="([^"]*)"/.exec(attributes)?.[1] ?? '');
        }
    }

    return stack;
}

function fieldsOutsideCards(text) {
    const outside = [];

    for (const match of text.matchAll(fieldPattern)) {
        const classes = openClasses(text, match.index);

        if (classes.some(value => /\b(card|card-body|modal|action-bar-bottom)\b/.test(value))) {
            continue;
        }

        outside.push(text.slice(0, match.index).split('\n').length);
    }

    return outside;
}

function views() {
    return readdirSync(modulesDirectory, { recursive: true })
        .map(file => String(file))
        .filter(file => file.startsWith('CrestApps.OrchardCore.Learning') && file.endsWith('.cshtml'))
        .filter(file => !file.split(sep).some(part => part === 'bin' || part === 'obj'))
        .map(file => join(modulesDirectory, file))
        .map(path => ({ path, text: readFileSync(path, 'utf8') }))
        .filter(({ path, text }) => isAdminEditView(path, text));
}

describe('Learning admin edit views', () => {
    const all = views();

    it('are found, so the check below is not passing on nothing', () => {
        expect(all.length).toBeGreaterThan(30);
    });

    it('put their fields inside a card', () => {
        const offenders = all
            .map(({ path, text }) => ({ path, lines: fieldsOutsideCards(text) }))
            .filter(({ lines }) => lines.length > 0)
            .map(({ path, lines }) => `${path}:${lines.join(',')}`);

        expect(offenders).toEqual([]);
    });
});
