import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';

// Orchard builds an editor shape from the model handed to Initialize<T> by generating a subclass of it, so a sealed
// model throws while the shape is built and the editor renders without its fields. Nothing is shown to the user; the
// save that follows then binds nothing and clears what the missing fields held. It has happened twice: the extension
// editor rendered empty, and the SMS endpoint editor lost its provider field, so every save reset the endpoint to the
// default provider.
function csharpFiles(directory) {
    return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
        if (entry.isDirectory()) {
            return ['bin', 'obj', 'node_modules', 'wwwroot'].includes(entry.name) ? [] : csharpFiles(join(directory, entry.name));
        }

        return entry.name.endsWith('.cs') ? [join(directory, entry.name)] : [];
    });
}

describe('the models editors are built from', () => {
    const sources = csharpFiles('src').map(path => ({ path, text: readFileSync(path, 'utf8') }));

    const initialized = new Set(sources.flatMap(({ text }) =>
        [...text.matchAll(/\bInitialize<([A-Za-z_][A-Za-z0-9_]*)>\s*\(/g)].map(match => match[1])));

    it('are found, so the check below is not passing on nothing', () => {
        expect(initialized.size).toBeGreaterThan(50);
    });

    it('are never sealed', () => {
        const sealed = [];

        for (const { path, text } of sources) {
            for (const match of text.matchAll(/\bsealed\s+(?:partial\s+)?(?:class|record)\s+([A-Za-z_][A-Za-z0-9_]*)/g)) {
                if (initialized.has(match[1])) {
                    sealed.push(`${match[1]} (${path})`);
                }
            }
        }

        expect(sealed).toEqual([]);
    });
});
