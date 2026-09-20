const path = require("node:path");

function isInside(root, candidate) {
  const resolvedRoot = path.resolve(root);
  const resolvedCandidate = path.resolve(candidate);
  return (
    resolvedCandidate === resolvedRoot ||
    resolvedCandidate.startsWith(`${resolvedRoot}${path.sep}`)
  );
}

function safeJoin(root, relativePath) {
  if (typeof relativePath !== "string" || !relativePath.trim()) {
    throw new Error("Mod 中包含空文件路径。");
  }

  const normalized = relativePath.replaceAll("\\", "/");
  if (
    normalized.startsWith("/") ||
    /^[a-zA-Z]:/.test(normalized) ||
    normalized.split("/").includes("..")
  ) {
    throw new Error(`Mod 中包含不安全路径：${relativePath}`);
  }

  const target = path.resolve(root, ...normalized.split("/"));
  if (!isInside(root, target) || target === path.resolve(root)) {
    throw new Error(`Mod 路径越过了游戏副本目录：${relativePath}`);
  }
  return target;
}

module.exports = { isInside, safeJoin };
