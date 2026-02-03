#!/usr/bin/env node

'use strict';

const glob = require("glob"),
      exec = require('child_process').exec,
      packageFileName = 'package.json';

// Use 'npm ci' in CI environments to avoid lockfile rewrites.
const isCI = process.env.CI === 'true' || process.env.CI === true;
const npmCommand = isCI ? 'npm ci' : 'npm install';

const assetPaths = glob.sync("./src/{Modules,Themes}/*/" + packageFileName, {});

assetPaths.forEach(function (assetPath) {
    let path = assetPath.substring(0, assetPath.length - packageFileName.length);
    console.log(`Running '${npmCommand}' on '${assetPath}'`);

    exec(npmCommand, {
        'cwd': path
    }, (error, stdout, stderr) => {
        if (error) {
            console.log(`Failed to run '${npmCommand}' on '${assetPath}'`, error);
        }
    })
});
