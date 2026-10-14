#!/usr/bin/env node
// Builds the front-end assets every module declares in its Assets.json. See docs/engineering/assets-pipeline.md.
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { parseArgs } from "node:util";
import { getCopyOutputs, buildCopyGroup } from "./copy.mjs";
import { getRecordedInputs, getToolingKey, isUpToDate, removeRecord, saveRecord, cacheDirectory } from "./cache.mjs";
import { filterAssetGroups, getAssetGroups, relativeToRepo, repoRoot, toPosix } from "./groups.mjs";
import { buildViteGroup, getViteOutputs } from "./vite.mjs";

const usage = `
Usage: npm run <command> [-- [-n name[,name]] [-t tag[,tag]] [--force]]

Commands:
  build      Build the asset groups whose sources changed since their last build.
  rebuild    Build every asset group (build --force).
  watch      Build, then rebuild a group whenever one of its sources changes. Copies do not watch.
  clean      Delete the outputs of the selected groups and the build cache.
  dry-run    List the selected groups and the files each one writes.

Filters (combine freely; a group matching any of them is selected):
  -n, --name   Asset group names from Assets.json.
  -t, --tag    Tags from Assets.json. Every group is also tagged with its module, for example -t Telephony.
`;

const { values, positionals } = parseArgs({
    allowPositionals: true,
    options: {
        name: { type: "string", short: "n", multiple: true },
        tag: { type: "string", short: "t", multiple: true },
        force: { type: "boolean", short: "f" },
        help: { type: "boolean", short: "h" },
    },
});

const command = positionals[0] ?? "build";

if (values.help || !["build", "watch", "clean", "dry-run"].includes(command)) {
    console.log(usage);
    process.exit(values.help ? 0 : 1);
}

checkNodeVersion();

const filters = {
    names: splitList(values.name),
    tags: splitList(values.tag),
};

try {
    switch (command) {
        case "build":
            await runBuild(selectGroups(), values.force === true);
            break;
        case "watch":
            await runWatch();
            break;
        case "clean":
            runClean(selectGroups());
            break;
        case "dry-run":
            runDryRun(selectGroups());
            break;
    }
} catch (error) {
    console.error(`\n✖ ${error.message ?? error}`);
    process.exit(1);
}

function selectGroups() {
    const groups = filterAssetGroups(getAssetGroups(), filters);

    if (groups.length === 0) {
        throw new Error("No asset group matches the filters.");
    }

    return groups;
}

async function runBuild(groups, force) {
    const started = performance.now();
    const toolingKey = getToolingKey();
    const pending = force ? groups : groups.filter(group => !isUpToDate(group, toolingKey));
    let written = 0;
    let failed = 0;

    await runLimited(pending, async group => {
        try {
            const changed = await buildGroup(group, toolingKey);
            written += changed;
        } catch (error) {
            failed++;
            console.error(`✖ ${group.name} (${group.relativeManifestPath})\n${indent(error.message ?? String(error))}`);
        }
    });

    const seconds = ((performance.now() - started) / 1000).toFixed(1);
    console.log(`\nBuilt ${pending.length - failed} of ${groups.length} asset groups (${groups.length - pending.length} up to date), ` +
        `wrote ${written} changed file${written === 1 ? "" : "s"} in ${seconds}s.`);

    if (failed > 0) {
        throw new Error(`${failed} asset group${failed === 1 ? "" : "s"} failed to build.`);
    }
}

async function buildGroup(group, toolingKey) {
    const started = performance.now();
    const result = group.action === "vite" ? await buildViteGroup(group) : buildCopyGroup(group);
    let written = 0;

    for (const file of result.files) {
        if (writeIfChanged(file.path, file.contents)) {
            written++;
        }
    }

    saveRecord(group, toolingKey, result.inputs, result.files.map(file => file.path));

    const changed = written > 0 ? `${written} changed` : "unchanged";
    console.log(`✓ ${group.name} → ${relativeToRepo(group.destPath)} (${changed}, ${Math.round(performance.now() - started)}ms)`);

    return written;
}

// Writes only when the contents differ, so an unchanged output keeps its timestamp and stays out of git status.
function writeIfChanged(filePath, contents) {
    const buffer = Buffer.isBuffer(contents) ? contents : Buffer.from(contents, "utf8");

    if (fs.existsSync(filePath) && fs.readFileSync(filePath).equals(buffer)) {
        return false;
    }

    fs.mkdirSync(path.dirname(filePath), { recursive: true });
    fs.writeFileSync(filePath, buffer);

    return true;
}

async function runWatch() {
    let groups = selectGroups();
    const toolingKey = getToolingKey();

    await runBuild(groups, false);

    const queue = new Set();
    let timer = null;
    let building = Promise.resolve();

    const schedule = changedPath => {
        const changed = toPosix(changedPath);

        if (changed.endsWith("/Assets.json")) {
            groups = selectGroups();
            groups.filter(group => group.manifestPath === changed && group.action === "vite").forEach(group => queue.add(group));
        } else {
            for (const group of groups) {
                const inputs = group.action === "vite" ? [...group.sourcePaths, ...getRecordedInputs(group)] : [];

                if (inputs.includes(changed)) {
                    queue.add(group);
                }
            }
        }

        clearTimeout(timer);
        timer = setTimeout(() => {
            const batch = [...queue].map(group => groups.find(candidate => candidate.name === group.name)).filter(Boolean);
            queue.clear();
            building = building.then(() => runLimited(batch, async group => {
                try {
                    await buildGroup(group, toolingKey);
                } catch (error) {
                    console.error(`✖ ${group.name}\n${indent(error.message ?? String(error))}`);
                }
            }));
        }, 150);
    };

    const watchedFolders = new Set(groups.map(group => group.basePath));

    for (const folder of watchedFolders) {
        fs.watch(folder, { recursive: true }, (_, fileName) => {
            if (fileName && !/[\\/]?(wwwroot|node_modules|bin|obj)[\\/]/.test(`/${fileName}`)) {
                schedule(path.join(folder, fileName));
            }
        });
    }

    console.log(`\nWatching ${groups.filter(group => group.action === "vite").length} asset groups in ${watchedFolders.size} modules. Press Ctrl+C to stop.`);
}

function runClean(groups) {
    let deleted = 0;

    for (const group of groups) {
        for (const output of getOutputs(group)) {
            if (fs.existsSync(output)) {
                fs.rmSync(output);
                deleted++;
            }
        }

        removeRecord(group);
    }

    if (filters.names.length === 0 && filters.tags.length === 0) {
        fs.rmSync(cacheDirectory, { recursive: true, force: true });
    }

    console.log(`Deleted ${deleted} output file${deleted === 1 ? "" : "s"} from ${groups.length} asset groups.`);
}

function runDryRun(groups) {
    for (const group of groups) {
        console.log(`${group.name} [${group.action}] (${group.relativeManifestPath}) tags: ${group.tags.join(", ")}`);

        for (const source of group.sourcePaths) {
            console.log(`  < ${relativeToRepo(source)}`);
        }

        for (const output of getOutputs(group)) {
            console.log(`  > ${relativeToRepo(output)}`);
        }
    }
}

function getOutputs(group) {
    return group.action === "vite" ? getViteOutputs(group) : getCopyOutputs(group);
}

async function runLimited(items, worker) {
    const limit = Math.max(1, Math.min(4, os.availableParallelism()));
    let next = 0;

    await Promise.all(Array.from({ length: Math.min(limit, items.length) }, async () => {
        while (next < items.length) {
            await worker(items[next++]);
        }
    }));
}

// The repository pins its Node major in .node-version; npm's major follows it, and npm majors disagree about the
// lockfile. A mismatch fails in CI and warns locally.
function checkNodeVersion() {
    const expected = fs.readFileSync(path.join(repoRoot, ".node-version"), "utf8").trim();
    const expectedMajor = expected.replace(/^v/, "").split(".")[0];
    const actualMajor = process.versions.node.split(".")[0];

    if (expectedMajor === actualMajor) {
        return;
    }

    const message = `Node.js ${process.versions.node} is running, but this repository builds its assets with Node.js ${expected} (see .node-version).`;

    if (process.env.CI) {
        console.error(`✖ ${message}`);
        process.exit(1);
    }

    console.warn(`⚠ ${message}\n`);
}

function splitList(values) {
    return (values ?? []).flatMap(value => value.split(",")).map(value => value.trim()).filter(Boolean);
}

function indent(text) {
    return text.split("\n").map(line => `    ${line}`).join("\n");
}
