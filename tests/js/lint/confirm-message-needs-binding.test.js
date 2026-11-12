import { readdirSync, readFileSync } from 'node:fs';
import { join, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

// The admin theme shows its confirmation dialog only for [data-url-af~="RemoveUrl"]. A button that carries
// data-title/data-message without it (or with UnsafeUrl alone) declares a question that is never asked: "Refund and
// close", "Buy label" and "Lock to payroll" went through on the first click. An element with data-message needs one of:
// - data-url-af="... RemoveUrl ..." (the admin theme's dialog), or
// - data-confirm (the crestapps-browser helpers ask), or
// - data-action (a bulk action of an admin list; the list script asks with its dialog).
const modulesDirectory = join('src', 'Modules');

// Views whose own script asks the question with the button's data-title/data-message. None left: the Save & Load
// button, the last, now asks with data-confirm.
const ownScript = [];

// The opening tags of a view. A '>' inside a quoted attribute value (a lambda in a Razor expression) does not end one.
function* tags(text) {
    for (const start of text.matchAll(/<[a-zA-Z][\w-]*\b/g)) {
        let quoted = false;

        for (let index = start.index + start[0].length; index < text.length; index++) {
            const character = text[index];

            if (character === '"') {
                quoted = !quoted;
            } else if (character === '>' && !quoted && text[index - 1] !== '=') {
                yield { tag: text.slice(start.index, index + 1), index: start.index };

                break;
            }
        }
    }
}

function views() {
    return readdirSync(modulesDirectory, { recursive: true })
        .map(file => String(file))
        .filter(file => file.endsWith('.cshtml') && !file.split(sep).some(part => part === 'bin' || part === 'obj'))
        .map(file => ({ name: file.split(sep).join('/'), text: readFileSync(join(modulesDirectory, file), 'utf8') }));
}

describe('a declared confirmation message', () => {
    const all = views();

    it('is found, so the check below is not passing on nothing', () => {
        expect(all.filter(({ text }) => text.includes('data-message=')).length).toBeGreaterThan(50);
    });

    it('is bound to something that shows it', () => {
        const unbound = all
            .filter(({ name }) => !ownScript.includes(name))
            .flatMap(({ name, text }) => [...tags(text)]
                .filter(({ tag }) => /\sdata-message=/.test(tag))
                .filter(({ tag }) => !/\sdata-url-af="[^"]*\bRemoveUrl\b/.test(tag) && !/\sdata-confirm=/.test(tag) && !/\sdata-action=/.test(tag))
                .map(({ tag, index }) => `${name}:${text.slice(0, index).split('\n').length}: ${tag.replace(/\s+/g, ' ').slice(0, 120)}`));

        expect(unbound).toEqual([]);
    });
});
