import { readdirSync, readFileSync } from 'node:fs';
import { join, sep } from 'node:path';
import { describe, expect, it } from 'vitest';

// A site migrated from another LMS has hundreds of courses, so a picker that chooses several courses (course
// prerequisites, the courses of a learning path set, the courses of a workflow event) searches them on the server
// with the ItemSelector component and the course search endpoint, and renders only the chosen ones; so does the
// course of each row of what a learning product grants. A select over every course renders them all and stops at an
// arbitrary number.
const modulesDirectory = join('src', 'Modules');
const modules = /^CrestApps\.OrchardCore\.Learning/;
const pickers = [
    ['CrestApps.OrchardCore.Learning', 'Views', 'CoursePart.Edit.cshtml'],
    ['CrestApps.OrchardCore.Learning', 'Views', 'Shared', 'LearningEventActivityFields.cshtml'],
    ['CrestApps.OrchardCore.Learning.Programs', 'Views', 'Shared', 'LearningPathSetRow.cshtml'],
    ['CrestApps.OrchardCore.Learning.Commerce', 'Views', 'LearningProductPart.Edit.cshtml'],
];

function views() {
    return readdirSync(modulesDirectory, { recursive: true })
        .map(file => String(file))
        .filter(file => modules.test(file) && file.endsWith('.cshtml'))
        .filter(file => !file.split(sep).some(part => part === 'bin' || part === 'obj'))
        .map(file => join(modulesDirectory, file))
        .map(path => ({ path, text: readFileSync(path, 'utf8') }));
}

describe('Learning course pickers', () => {
    it('search courses on the server', () => {
        for (const parts of pickers) {
            const text = readFileSync(join(modulesDirectory, ...parts), 'utf8');

            expect(text, parts.join('/')).toContain('Component.InvokeAsync("ItemSelector"');
            expect(text, parts.join('/')).toContain('LearningCourseSearch');
        }
    });

    it('never render every course in a multiple select', () => {
        const offenders = [];

        for (const { path, text } of views()) {
            for (const match of text.matchAll(/<select\b[^>]*\bmultiple\b[^>]*>/g)) {
                const line = text.slice(0, match.index).split('\n').length;
                const rest = text.slice(match.index, text.indexOf('</select>', match.index));

                if (/\bCourse(Options|s)\b/.test(rest)) {
                    offenders.push(`${path}:${line} lists every course`);
                }
            }
        }

        expect(offenders).toEqual([]);
    });
});
