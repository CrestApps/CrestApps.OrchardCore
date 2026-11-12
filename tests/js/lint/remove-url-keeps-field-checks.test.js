import { readdirSync, readFileSync } from 'node:fs';
import { join, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

// The admin theme's dialog for [data-url-af~="RemoveUrl"] sends a submit button's form with form.submit(), which fires no
// submit event and skips the browser's checks of the form's fields. A refund amount kept between min="0.01" and the
// refundable balance by the browser was posted unchecked once its button moved to RemoveUrl, and no-multisubmit, which
// listens for the submit event, no longer stopped a second click. A submit button in a form whose fields the browser
// checks asks with data-confirm (the crestapps-browser helpers), which lets the browser's own submit go on.
const modulesDirectory = join('src', 'Modules');

const checkedField = /<(?:input|select|textarea)\b[^>]*\s(?:required|min=|max=|minlength=|pattern=)/;
const removeUrlSubmit = /<button\b(?![^>]*\stype="(?:button|reset)")[^>]*\sdata-url-af="[^"]*\bRemoveUrl\b/;

function views() {
    return readdirSync(modulesDirectory, { recursive: true })
        .map(file => String(file))
        .filter(file => file.endsWith('.cshtml') && !file.split(sep).some(part => part === 'bin' || part === 'obj'))
        .map(file => ({ name: file.split(sep).join('/'), text: readFileSync(join(modulesDirectory, file), 'utf8') }));
}

// Each form of a view, from its opening tag to its end.
function* forms(text) {
    for (const start of text.matchAll(/<form\b/g)) {
        const end = text.indexOf('</form>', start.index);

        if (end > 0) {
            yield { body: text.slice(start.index, end), line: text.slice(0, start.index).split('\n').length };
        }
    }
}

describe('a form whose fields the browser checks', () => {
    const all = views();

    it('is found, so the check below is not passing on nothing', () => {
        expect(all.flatMap(({ text }) => [...forms(text)]).filter(({ body }) => checkedField.test(body)).length).toBeGreaterThan(20);
    });

    it('is not sent by the admin theme\'s RemoveUrl dialog, which skips those checks', () => {
        const unchecked = all.flatMap(({ name, text }) => [...forms(text)]
            .filter(({ body }) => checkedField.test(body) && removeUrlSubmit.test(body))
            .map(({ line }) => `${name}:${line}`));

        expect(unchecked).toEqual([]);
    });
});
