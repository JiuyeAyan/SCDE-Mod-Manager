const fs = require("node:fs");
const path = require("node:path");
const AdmZip = require("adm-zip");

const sourceRoot = path.resolve(process.argv[2]);
const outputPath = path.resolve(process.argv[3]);
const zip = new AdmZip();

function addFiles(current) {
  for (const entry of fs.readdirSync(current, { withFileTypes: true })) {
    const absolute = path.join(current, entry.name);
    if (entry.isDirectory()) {
      addFiles(absolute);
    } else if (entry.isFile()) {
      const archivePath = path.relative(sourceRoot, absolute).split(path.sep).join("/");
      zip.addFile(archivePath, fs.readFileSync(absolute));
    }
  }
}

addFiles(sourceRoot);
fs.rmSync(outputPath, { force: true });
zip.writeZip(outputPath);
