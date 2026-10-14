import fs from "node:fs";
import path from "node:path";
import { globSync } from "glob";

export const repoRoot = path.resolve(import.meta.dirname, "../..");

const manifestGlob = "src/{Modules,Startup,Themes}/*/Assets.json";
const actions = new Set(["vite", "copy"]);
const moduleNamePrefix = "CrestApps.OrchardCore.";

// Reads every Assets.json in the repository and returns its asset groups with their paths resolved.
export function getAssetGroups() {
    const groups = [];
    const names = new Map();

    for (const manifestPath of globSync(manifestGlob, { cwd: repoRoot, posix: true }).sort()) {
        const absoluteManifestPath = toPosix(path.join(repoRoot, manifestPath));
        const manifest = JSON.parse(fs.readFileSync(absoluteManifestPath, "utf8").replace(/^﻿/, ""));

        if (!Array.isArray(manifest)) {
            throw new Error(`${manifestPath} must contain an array of asset groups.`);
        }

        for (const definition of manifest) {
            const group = resolveGroup(definition, absoluteManifestPath, manifestPath);

            if (names.has(group.name)) {
                throw new Error(`The asset group name '${group.name}' is used by both ${names.get(group.name)} and ${manifestPath}. Names must be unique.`);
            }

            names.set(group.name, manifestPath);
            groups.push(group);
        }
    }

    return groups;
}

// Keeps the groups matching any of the requested names or tags. A group is always tagged with its module's short name
// (for example "Telephony"), so `-t Telephony` builds a whole module.
export function filterAssetGroups(groups, { names = [], tags = [] }) {
    if (names.length === 0 && tags.length === 0) {
        return groups;
    }

    const wantedNames = new Set(names.map(name => name.toLowerCase()));
    const wantedTags = new Set(tags.map(tag => tag.toLowerCase()));

    const unknownNames = [...wantedNames].filter(name => !groups.some(group => group.name.toLowerCase() === name));

    if (unknownNames.length > 0) {
        throw new Error(`No asset group is named ${unknownNames.map(name => `'${name}'`).join(", ")}.`);
    }

    return groups.filter(group =>
        wantedNames.has(group.name.toLowerCase()) ||
        group.tags.some(tag => wantedTags.has(tag.toLowerCase())));
}

function resolveGroup(definition, manifestPath, relativeManifestPath) {
    const where = `${relativeManifestPath} (${definition.name ?? "unnamed group"})`;

    if (!actions.has(definition.action)) {
        throw new Error(`${where}: 'action' must be one of ${[...actions].join(", ")}.`);
    }

    if (typeof definition.name !== "string" || definition.name.length === 0) {
        throw new Error(`${where}: every asset group needs a 'name'.`);
    }

    if (typeof definition.dest !== "string" || definition.dest.length === 0) {
        throw new Error(`${where}: every asset group needs a 'dest'.`);
    }

    const sources = Array.isArray(definition.source) ? definition.source : [definition.source];

    if (sources.length === 0 || sources.some(source => typeof source !== "string" || source.length === 0)) {
        throw new Error(`${where}: 'source' must be a path or a non-empty array of paths.`);
    }

    const basePath = path.dirname(manifestPath);
    const moduleName = path.basename(basePath);
    const tags = Array.isArray(definition.tags) ? definition.tags : definition.tags ? [definition.tags] : [];

    return {
        ...definition,
        manifestPath,
        relativeManifestPath,
        basePath,
        tags: [...new Set([...tags, moduleName.startsWith(moduleNamePrefix) ? moduleName.substring(moduleNamePrefix.length) : moduleName])],
        sourcePaths: sources.flatMap(source => resolveSource(source, basePath, where)),
        destPath: toPosix(path.resolve(basePath, definition.dest)),
        destIsFolder: definition.dest.endsWith("/"),
    };
}

// Resolves a source path relative to the module. A path that starts with node_modules/ is looked up the way Node
// resolves packages, walking up from the module, so it finds the package whether npm installed it in the module or
// hoisted it to the repository root.
function resolveSource(source, basePath, where) {
    const isPackagePath = source.startsWith("node_modules/");
    const resolved = isPackagePath ? resolvePackagePath(source, basePath) : toPosix(path.resolve(basePath, source));

    if (resolved === null) {
        throw new Error(`${where}: '${source}' was not found in any node_modules folder. Run 'npm install'.`);
    }

    if (resolved.includes("*")) {
        const matches = globSync(resolved, { posix: true, nodir: true }).sort();

        if (matches.length === 0) {
            throw new Error(`${where}: '${source}' matches no files.`);
        }

        return matches;
    }

    if (!fs.existsSync(resolved)) {
        throw new Error(`${where}: '${source}' does not exist.`);
    }

    return [resolved];
}

function resolvePackagePath(source, basePath) {
    const packageRelativePath = source.substring("node_modules/".length);

    for (let directory = basePath; ; directory = path.dirname(directory)) {
        const candidate = toPosix(path.join(directory, "node_modules", packageRelativePath));
        const exists = candidate.includes("*")
            ? globSync(candidate, { posix: true }).length > 0
            : fs.existsSync(candidate);

        if (exists) {
            return candidate;
        }

        if (path.dirname(directory) === directory) {
            return null;
        }
    }
}

export function toPosix(value) {
    return value.replace(/\\/g, "/");
}

export function relativeToRepo(value) {
    return toPosix(path.relative(repoRoot, value));
}
