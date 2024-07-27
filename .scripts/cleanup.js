#!/usr/bin/env node

'use strict';

const glob = require("glob"),
      exec = require('child_process').exec,
      fs = require('fs'),
      nodePath = require('path');

const deleteFolderRecursive = function (directoryPath) {
    if (!fs.existsSync(directoryPath)) {
        return;
    }

    fs.readdirSync(directoryPath).forEach((file, index) => {
      const curPath = nodePath.join(directoryPath, file);
      if (fs.lstatSync(curPath).isDirectory()) {
       // recurse
        deleteFolderRecursive(curPath);
      } else {
        // delete file
        fs.unlinkSync(curPath);
      }
    });

    fs.rmdirSync(directoryPath);
};

const paths = glob.sync("./src/**/{bin,obj}/", {});

if(process.argv.length == 3 && process.argv[2].toLowerCase() == 'all'){

    var modulesPaths = glob.sync("./**/node_modules/", {});

    paths.push(...modulesPaths);
}

paths.sort().forEach(function (path) {
    console.log(`Deleting: '${path}'`);  
    deleteFolderRecursive(path);
});
