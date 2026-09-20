const fs = require("node:fs/promises");
const path = require("node:path");

const CONFIG_VERSION = 1;

class ConfigStore {
  constructor(dataRoot) {
    this.dataRoot = path.resolve(dataRoot);
    this.filePath = path.join(this.dataRoot, "config.json");
  }

  defaults() {
    return {
      version: CONFIG_VERSION,
      gameDir: "",
      language: "",
      enabledMods: [],
      modOrder: [],
      activeFiles: [],
    };
  }

  async ensure() {
    await fs.mkdir(this.dataRoot, { recursive: true });
    try {
      await fs.access(this.filePath);
    } catch {
      await this.save(this.defaults());
    }
  }

  async load() {
    await this.ensure();
    let parsed;
    try {
      parsed = JSON.parse(await fs.readFile(this.filePath, "utf8"));
    } catch (error) {
      throw new Error(`配置文件无法读取：${error.message}`);
    }

    if (parsed.version !== CONFIG_VERSION) {
      throw new Error(`不支持的配置版本：${parsed.version}`);
    }

    return {
      ...this.defaults(),
      ...parsed,
      enabledMods: Array.isArray(parsed.enabledMods) ? parsed.enabledMods : [],
      modOrder: Array.isArray(parsed.modOrder) ? parsed.modOrder : [],
      activeFiles: Array.isArray(parsed.activeFiles) ? parsed.activeFiles : [],
    };
  }

  async save(config) {
    await fs.mkdir(this.dataRoot, { recursive: true });
    const temporary = `${this.filePath}.tmp`;
    await fs.writeFile(temporary, `${JSON.stringify(config, null, 2)}\n`, "utf8");
    await fs.copyFile(temporary, this.filePath);
    await fs.rm(temporary, { force: true });
  }

  async update(updater) {
    const current = await this.load();
    const next = await updater({ ...current });
    await this.save(next);
    return next;
  }
}

module.exports = { ConfigStore };
