import fs from "node:fs";
import path from "node:path";
import { toPosix } from "./groups.mjs";

// A 'copy' group copies its sources unchanged. A 'dest' ending in "/" is a folder that keeps each file's name; any
// other 'dest' is the one file its single source is copied to, which lets a group rename what it copies.
export function buildCopyGroup(group) {
    const outputs = getCopyOutputs(group);

    return {
        files: group.sourcePaths.map((source, index) => ({ path: outputs[index], contents: fs.readFileSync(source) })),
        inputs: [...group.sourcePaths],
    };
}

export function getCopyOutputs(group) {
    if (group.destIsFolder) {
        return group.sourcePaths.map(source => toPosix(path.join(group.destPath, path.basename(source))));
    }

    if (group.sourcePaths.length !== 1) {
        throw new Error(`${group.relativeManifestPath} (${group.name}): copying ${group.sourcePaths.length} files needs a 'dest' folder ending in '/'.`);
    }

    return [group.destPath];
}
