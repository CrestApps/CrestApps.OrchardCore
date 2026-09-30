import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { describe, expect, it } from 'vitest';

// Every screen a module controller renders carries an Orchard Core breadcrumb trail in its Title zone, so the admin
// shows where the page sits (Dashboard › Queues › Edit 'Support' Queue) and the trail doubles as the page heading. A
// view that prints its own <h1> instead drops out of the trail and gives the page no way back to its parent. Views
// rendered without a layout (Layout = null) are standalone pages with no admin chrome, so they have no trail.
const modulesDirectory = join('src', 'Modules');

function controllerNames(moduleDirectory) {
    const controllersDirectory = join(moduleDirectory, 'Controllers');

    if (!existsSync(controllersDirectory)) {
        return [];
    }

    return readdirSync(controllersDirectory, { recursive: true })
        .map(file => basename(String(file)))
        .filter(file => file.endsWith('Controller.cs'))
        .map(file => file.slice(0, -'Controller.cs'.length));
}

function pageViews() {
    return readdirSync(modulesDirectory, { withFileTypes: true })
        .filter(entry => entry.isDirectory())
        .flatMap(entry => {
            const moduleDirectory = join(modulesDirectory, entry.name);

            return controllerNames(moduleDirectory).flatMap(controller => {
                const viewsDirectory = join(moduleDirectory, 'Views', controller);

                if (!existsSync(viewsDirectory)) {
                    return [];
                }

                return readdirSync(viewsDirectory)
                    .filter(file => file.endsWith('.cshtml') && !file.startsWith('_'))
                    .map(file => join(viewsDirectory, file));
            });
        })
        .map(path => ({ path, text: readFileSync(path, 'utf8') }));
}

describe('the page views module controllers render', () => {
    const views = pageViews();

    it('are found, so the check below is not passing on nothing', () => {
        expect(views.length).toBeGreaterThan(100);
    });

    it('declare a breadcrumb trail', () => {
        const missing = views
            .filter(({ text }) => !/Layout\s*=\s*null\s*;/.test(text))
            .filter(({ text }) => !/<breadcrumb\s/.test(text) && !/<partial\s+name="_CatalogList"/.test(text))
            .map(({ path }) => path);

        expect(missing).toEqual([]);
    });

    it('do not print their own page heading next to the trail', () => {
        const duplicated = views
            .filter(({ text }) => /<breadcrumb\s/.test(text) && /RenderTitleSegments\(/.test(text))
            .map(({ path }) => path);

        expect(duplicated).toEqual([]);
    });
});
