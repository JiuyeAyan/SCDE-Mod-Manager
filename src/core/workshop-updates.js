// Numeric dotted versions (including four-part runtime versions) and prereleases.
// Unknown naming schemes are not guessed to be newer.
function compareVersions(first, second) {
  const parse = (text) => /^v?(\d+(?:\.\d+)*)(?:-([0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$/.exec(text);
  const a = parse(first), b = parse(second);
  if (!a || !b) return null;
  const x = a[1].split(".").map(BigInt), y = b[1].split(".").map(BigInt);
  for (let i = 0; i < Math.max(x.length, y.length); i++) {
    if ((x[i] || 0n) !== (y[i] || 0n)) return (x[i] || 0n) > (y[i] || 0n) ? 1 : -1;
  }
  if (a[2] === b[2]) return 0;
  if (!a[2]) return 1;
  if (!b[2]) return -1;
  const preA = a[2].split("."), preB = b[2].split(".");
  for (let i = 0; i < Math.max(preA.length, preB.length); i++) {
    if (preA[i] === preB[i]) continue;
    if (preA[i] === undefined) return -1;
    if (preB[i] === undefined) return 1;
    const numericA = /^\d+$/.test(preA[i]), numericB = /^\d+$/.test(preB[i]);
    if (numericA && numericB) {
      if (BigInt(preA[i]) === BigInt(preB[i])) continue;
      return BigInt(preA[i]) > BigInt(preB[i]) ? 1 : -1;
    }
    if (numericA !== numericB) return numericA ? -1 : 1;
    return preA[i] > preB[i] ? 1 : -1;
  }
  return 0;
}

function findWorkshopUpdates(installed, candidates, excludedIds = new Set()) {
  const versions = new Map(installed.map((mod) => [mod.id, mod.version]));
  return candidates.filter((item) => !excludedIds.has(item.id) && versions.has(item.id) &&
    compareVersions(item.version, versions.get(item.id)) === 1)
    .map((item) => ({ ...item, installedVersion: versions.get(item.id), source: "workshop" }));
}

module.exports = { compareVersions, findWorkshopUpdates };
