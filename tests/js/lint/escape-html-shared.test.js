import { readdirSync, readFileSync } from 'node:fs';
import { join, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

// escapeHtml was written over and over, and the copies disagreed: the textContent-then-innerHTML form leaves quotes
// alone, so its result broke out of an attribute (SEC-FE2). There is one now, in the crestapps-browser toolkit
// (Resources/Assets/js/browser/html.js, crestapps.html.escape in a page). A bundled script imports it; a view requires
// the crestapps-browser resource and calls crestapps.html.escape.
const roots = [join('src', 'Modules'), join('src', 'Startup')];
const shared = join('CrestApps.OrchardCore.Resources', 'Assets', 'js', 'browser') + sep;
const definition = /function\s+escapeHtml\b|\bescapeHtml\s*=\s*(?:function\b|\([^)]*\)\s*=>|\w+\s*=>)/;

// The copies still left. Each moves to the shared helper when its view is next touched (UX-7 extracts these scripts);
// this list may only get shorter.
const remaining = [
    'src/Modules/CrestApps.OrchardCore.AI/Views/AIPersonaFields.Edit.cshtml',
    'src/Modules/CrestApps.OrchardCore.AI.Documents/Views/AICompletionWithConfigDocuments.Edit.cshtml',
    'src/Modules/CrestApps.OrchardCore.AI.Documents/Views/AIProfileDocuments.Edit.cshtml',
    'src/Modules/CrestApps.OrchardCore.AI.Documents/Views/ChatInteractionDocuments.Edit.cshtml',
    'src/Modules/CrestApps.OrchardCore.AI.Prompting/Views/PromptTemplateChatInteractionSelection.Edit.cshtml',
    'src/Modules/CrestApps.OrchardCore.AI.Prompting/Views/PromptTemplateSelection.Edit.cshtml',
    // The sample hosts are stand-alone sites without the CrestApps resources.
    'src/Startup/CrestApps.OrchardCore.Samples.A2AClient/Pages/Agents.cshtml',
    'src/Startup/CrestApps.OrchardCore.Samples.McpClient/Pages/Prompts.cshtml',
    'src/Startup/CrestApps.OrchardCore.Samples.McpClient/Pages/Resources.cshtml',
    'src/Startup/CrestApps.OrchardCore.Samples.McpClient/Pages/ResourceTemplates.cshtml',
    'src/Startup/CrestApps.OrchardCore.Samples.McpClient/Pages/Tools.cshtml',
];

function sources() {
    return roots.flatMap(root => readdirSync(root, { recursive: true })
        .map(file => String(file))
        .filter(file => /\.(?:js|mjs|cshtml)$/.test(file))
        .filter(file => !file.split(sep).some(part => ['bin', 'obj', 'node_modules', 'wwwroot'].includes(part)))
        .map(file => ({ file, path: join(root, file) })));
}

describe('escapeHtml', () => {
    const defining = sources()
        .filter(({ file }) => !file.startsWith(shared))
        .filter(({ path }) => definition.test(readFileSync(path, 'utf8')))
        .map(({ path }) => path.split(sep).join('/'))
        .sort();

    it('is defined once, in the shared browser helpers', () => {
        expect(defining.filter(path => !remaining.includes(path))).toEqual([]);
    });

    it('has no allowance left for a copy that is gone, so the list only shrinks', () => {
        expect(remaining.filter(path => !defining.includes(path))).toEqual([]);
    });

    it('is the five-character form in the shared helpers', () => {
        const helper = readFileSync(join(roots[0], shared, 'html.js'), 'utf8');

        expect(helper).toMatch(/export function escapeHtml/);
        expect(helper).toContain('&quot;');
        expect(helper).toContain('&#39;');
    });
});
