import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { runInNewContext } from 'node:vm';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { buildCopyGroup, getCopyOutputs } from '../../../.scripts/assets/copy.mjs';
import { toPosix } from '../../../.scripts/assets/groups.mjs';

// Some libraries, such as FullCalendar 7, publish only a readable browser build. A 'copy' group with "minify": true
// copies that file unchanged and writes a minified .min.js beside it, so the resource manifest can serve the small
// file in production and the readable one in debug mode.
const libraryScript = `/*!
Library v1.0.0
License: MIT
*/
var Library = (function (exports) {
  'use strict';

  function describeValue(value) {
    const description = 'The value is ' + value;

    return description;
  }

  exports.describeValue = describeValue;

  return exports;
})({});
`;

describe('copy groups with minify', () => {
    let folder;

    beforeEach(() => {
        folder = toPosix(mkdtempSync(join(tmpdir(), 'crestapps-assets-')));
    });

    afterEach(() => {
        rmSync(folder, { recursive: true, force: true });
    });

    function createGroup(fileName, contents, dest, minify = true) {
        const source = `${folder}/${fileName}`;
        writeFileSync(source, contents);

        return {
            name: 'test-library',
            action: 'copy',
            relativeManifestPath: 'src/Modules/Test/Assets.json',
            minify,
            sourcePaths: [source],
            destPath: `${folder}/wwwroot/${dest}`,
            destIsFolder: dest.endsWith('/'),
        };
    }

    it('writes the copied script and a .min.js sibling', async () => {
        // Arrange
        const group = createGroup('global.js', libraryScript, 'library.global.js');

        // Act
        const result = await buildCopyGroup(group);

        // Assert
        expect(result.files.map(file => file.path)).toEqual([
            `${folder}/wwwroot/library.global.js`,
            `${folder}/wwwroot/library.global.min.js`,
        ]);
        expect(getCopyOutputs(group)).toEqual(result.files.map(file => file.path));
        expect(result.inputs).toEqual(group.sourcePaths);
        expect(result.files[0].contents.toString('utf8')).toBe(libraryScript);
    });

    it('names the .min.js sibling after each file of a folder destination', async () => {
        const group = createGroup('global.js', libraryScript, 'library/');

        expect(getCopyOutputs(group)).toEqual([
            `${folder}/wwwroot/library/global.js`,
            `${folder}/wwwroot/library/global.min.js`,
        ]);
    });

    it('minifies the script as a classic script that keeps its globals and its license comment', async () => {
        // Arrange
        const group = createGroup('global.js', libraryScript, 'library.global.js');

        // Act
        const minified = (await buildCopyGroup(group)).files[1].contents.toString('utf8');
        const page = {};
        runInNewContext(minified, page);

        // Assert
        expect(minified.startsWith('/*!\nLibrary v1.0.0\nLicense: MIT\n*/\n')).toBe(true);
        expect(minified).not.toContain('describeValue(value)');
        expect(minified.length).toBeLessThan(libraryScript.length);
        expect(minified.endsWith('\n')).toBe(true);
        expect(page.Library.describeValue(2)).toBe('The value is 2');
    });

    it('copies without a .min.js sibling when minify is off', async () => {
        const group = createGroup('global.js', libraryScript, 'library.global.js', false);

        const result = await buildCopyGroup(group);

        expect(result.files.map(file => file.path)).toEqual([`${folder}/wwwroot/library.global.js`]);
        expect(getCopyOutputs(group)).toEqual([`${folder}/wwwroot/library.global.js`]);
    });

    it('refuses to minify a file that is not a script', () => {
        const group = createGroup('theme.css', '.calendar { color: red; }\n', 'theme.css');

        expect(() => getCopyOutputs(group)).toThrow("src/Modules/Test/Assets.json (test-library): 'minify' minifies .js files only");
    });

    it('fails the group when the script cannot be parsed', async () => {
        const group = createGroup('broken.js', 'var Library = (function () {\n', 'broken.js');

        await expect(buildCopyGroup(group)).rejects.toThrow(/^src\/Modules\/Test\/Assets\.json \(test-library\): could not minify '[^']*broken\.js'\./);
    });
});
