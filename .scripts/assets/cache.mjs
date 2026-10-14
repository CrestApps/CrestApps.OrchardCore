import crypto from "node:crypto";
import fs from "node:fs";
import path from "node:path";
import { globSync } from "glob";
import { repoRoot } from "./groups.mjs";

export const cacheDirectory = path.join(repoRoot, "node_modules", ".cache", "crestapps-assets");

// A group is up to date when its definition, the build tooling, every file that went into its last build and every file
// it wrote are all unchanged. Content hashes rather than timestamps, so a branch switch or a fresh clone is judged by
// what the files say, not when git last touched them.
export function isUpToDate(group, toolingKey) {
    const record = readRecord(group);

    return record !== null &&
        record.definition === hashDefinition(group, toolingKey) &&
        record.inputs === hashFiles(record.inputFiles) &&
        record.outputs === hashFiles(record.outputFiles);
}

export function getRecordedInputs(group) {
    return readRecord(group)?.inputFiles ?? [];
}

export function saveRecord(group, toolingKey, inputFiles, outputFiles) {
    fs.mkdirSync(cacheDirectory, { recursive: true });
    fs.writeFileSync(recordPath(group), JSON.stringify({
        definition: hashDefinition(group, toolingKey),
        inputFiles,
        inputs: hashFiles(inputFiles),
        outputFiles,
        outputs: hashFiles(outputFiles),
    }));
}

export function removeRecord(group) {
    fs.rmSync(recordPath(group), { force: true });
}

// Changing the build scripts or the bundler invalidates every group.
export function getToolingKey() {
    const hash = crypto.createHash("sha256");
    const scripts = globSync("*.mjs", { cwd: import.meta.dirname, posix: true }).sort();

    for (const script of scripts) {
        hash.update(fs.readFileSync(path.join(import.meta.dirname, script)));
    }

    for (const tool of ["vite", "sass"]) {
        hash.update(readPackageVersion(tool));
    }

    return hash.digest("hex");
}

function readPackageVersion(name) {
    try {
        return JSON.parse(fs.readFileSync(path.join(repoRoot, "node_modules", name, "package.json"), "utf8")).version;
    } catch {
        return "missing";
    }
}

function hashDefinition(group, toolingKey) {
    const { name, action, source, dest, global: globalName, tags } = group;

    return crypto.createHash("sha256")
        .update(toolingKey)
        .update(JSON.stringify({ name, action, source, dest, global: globalName, tags }))
        .digest("hex");
}

function hashFiles(files) {
    const hash = crypto.createHash("sha256");

    for (const file of files) {
        hash.update(file);

        if (!fs.existsSync(file)) {
            return "missing";
        }

        hash.update(fs.readFileSync(file));
    }

    return hash.digest("hex");
}

function readRecord(group) {
    try {
        return JSON.parse(fs.readFileSync(recordPath(group), "utf8"));
    } catch {
        return null;
    }
}

function recordPath(group) {
    return path.join(cacheDirectory, `${group.name}.json`);
}
