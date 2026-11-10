import fs from "node:fs";
import path from "node:path";
import { minify } from "vite";
import { relativeToRepo, toLf, toMinifiedPath, toPosix } from "./groups.mjs";

// The browsers the 'vite' groups are built for (Vite's 'baseline-widely-available' target), so a minified copy never
// uses syntax that a built script could not.
const minifyTarget = ["chrome111", "edge111", "firefox114", "safari16.4", "ios16.4"];

// A 'copy' group copies its sources unchanged. A 'dest' ending in "/" is a folder that keeps each file's name; any
// other 'dest' is the one file its single source is copied to, which lets a group rename what it copies. With
// "minify": true, each copied script also gets a minified .min.js sibling, for a package that publishes only a
// readable browser build.
export async function buildCopyGroup(group) {
    const copies = getCopiedPaths(group);
    const files = group.sourcePaths.map((source, index) => ({ path: copies[index], contents: fs.readFileSync(source) }));

    if (group.minify === true) {
        const minified = await Promise.all(files.map((file, index) => minifyScript(group, group.sourcePaths[index], file.contents)));

        files.push(...minified.map((contents, index) => ({ path: toMinifiedPath(copies[index]), contents })));
    }

    return {
        files,
        inputs: [...group.sourcePaths],
    };
}

export function getCopyOutputs(group) {
    const copies = getCopiedPaths(group);

    return group.minify === true ? [...copies, ...copies.map(toMinifiedPath)] : copies;
}

function getCopiedPaths(group) {
    if (group.minify === true) {
        const other = group.sourcePaths.find(source => path.extname(source).toLowerCase() !== ".js");

        if (other !== undefined) {
            throw new Error(`${group.relativeManifestPath} (${group.name}): 'minify' minifies .js files only, and '${path.basename(other)}' is not one.`);
        }
    }

    if (group.destIsFolder) {
        return group.sourcePaths.map(source => toPosix(path.join(group.destPath, path.basename(source))));
    }

    if (group.sourcePaths.length !== 1) {
        throw new Error(`${group.relativeManifestPath} (${group.name}): copying ${group.sourcePaths.length} files needs a 'dest' folder ending in '/'.`);
    }

    return [group.destPath];
}

// Minifies with Oxc, as the 'vite' groups do. The file is minified as a classic script, not a module, so its top-level
// names stay page globals (FullCalendar's global.js sets window.FullCalendar that way), and its license comment stays.
async function minifyScript(group, source, contents) {
    const result = await minify(path.basename(source), contents.toString("utf8"), {
        module: false,
        compress: { target: minifyTarget },
        codegen: { legalComments: "inline" },
    });

    if (result.errors.length > 0) {
        const errors = result.errors.map(error => error.message).join("\n");

        throw new Error(`${group.relativeManifestPath} (${group.name}): could not minify '${relativeToRepo(source)}'.\n${errors}`);
    }

    return toLf(result.code);
}
