import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

// Browsers fill the site's saved sign-in password into a settings page's secret field, and ignore autocomplete="off"
// on a password field when they do. Live, a Telnyx settings save replaced the API key with the administrator's own
// password, Telnyx then refused every soft phone registration ("Could not find any usable credentials"), and nothing
// on the page said the key had changed. "new-password" is the value browsers respect.
function views(directory) {
    return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
        if (entry.isDirectory()) {
            return ['bin', 'obj', 'node_modules', 'wwwroot'].includes(entry.name) ? [] : views(join(directory, entry.name));
        }

        return entry.name.endsWith('.cshtml') ? [join(directory, entry.name)] : [];
    });
}

describe('secret fields', () => {
    const fields = views('src').flatMap(path =>
        [...readFileSync(path, 'utf8').matchAll(/<input\b[^>]*\btype="password"[^>]*>/gs)].map(match => ({ path, tag: match[0] })));

    it('are found, so the check below is not passing on nothing', () => {
        expect(fields.length).toBeGreaterThan(20);
    });

    it('are never filled in by the browser', () => {
        const filled = fields
            .filter(({ tag }) => !/\bautocomplete="new-password"/.test(tag))
            .map(({ path, tag }) => `${path}: ${tag.slice(0, 80)}`);

        expect(filled).toEqual([]);
    });

    // A new account's own password, marked data-account-password, is the one field a password manager should offer
    // to save; every other secret is the site's, such as an API key, and is kept away from them.
    it('are skipped by password managers', () => {
        const offered = fields
            .filter(({ tag }) => !tag.includes('data-account-password'))
            .filter(({ tag }) => !tag.includes('data-1p-ignore') || !tag.includes('data-lpignore') || !tag.includes('data-bwignore'))
            .map(({ path }) => path);

        expect(offered).toEqual([]);
    });
});
