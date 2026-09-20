(function (root) {
  function translate(bundle, section, key, values = {}) {
    const template = bundle?.strings?.[section]?.[key] ?? key;
    // Single-pass replacement keeps braces inside names/paths literal; no HTML or code.
    return template.replace(/\{([a-zA-Z][a-zA-Z0-9]*)\}/g, (token, name) =>
      Object.hasOwn(values, name) ? String(values[name]) : token);
  }
  const api = { translate };
  if (typeof module === "object" && module.exports) module.exports = api;
  else root.SCDEI18n = api;
})(globalThis);
