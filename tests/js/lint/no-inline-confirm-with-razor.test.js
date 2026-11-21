import { readdirSync, readFileSync } from 'node:fs';
import { join, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

// @T["..."] writes its text as it is (only its arguments are encoded), and a model value written into a JavaScript
// string is no safer: an apostrophe ends the string, so the handler is a syntax error and the form submits with no
// question asked ("Cancel this order? This can't be undone." did exactly that in English), and a quote in a record's
// title can add script. Text reaches a script through a data-* attribute instead: data-url-af="RemoveUrl" with
// data-title/data-message in the admin, data-confirm with the crestapps-browser resource elsewhere.
const modulesDirectory = join('src', 'Modules');

function readViews() {
    return readdirSync(modulesDirectory, { recursive: true })
        .map(file => String(file))
        .filter(file => file.endsWith('.cshtml') && !file.split(sep).some(part => part === 'bin' || part === 'obj'))
        .map(file => join(modulesDirectory, file))
        .map(path => ({ path: path.split(sep).join('/'), text: readFileSync(path, 'utf8') }));
}

// Read once while the file is collected: reading every view takes a few seconds, more while a build runs beside it.
const all = readViews();

function offending(pattern) {
    return all.flatMap(({ path, text }) => text.split(/\r?\n/)
        .map((line, index) => ({ line, number: index + 1 }))
        .filter(({ line }) => pattern.test(line))
        .map(({ line, number }) => `${path}:${number}: ${line.trim()}`));
}

describe('views', () => {
    it('never put Razor output inside a confirm() string', () => {
        expect(offending(/\bconfirm\(\s*['"`]@/)).toEqual([]);
    });

    it('never put Razor output inside a JavaScript string in an inline event handler', () => {
        expect(offending(/\son[a-z]+="[^"]*['`]@/)).toEqual([]);
    });
});
