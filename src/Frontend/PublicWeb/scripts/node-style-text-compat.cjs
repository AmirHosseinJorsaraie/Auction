const nodeModule = require("node:module");
const util = require("node:util");

try {
  util.styleText(["underline", "gray"], "compatibility check");
} catch (error) {
  if (error?.code !== "ERR_INVALID_ARG_VALUE") throw error;
  const originalStyleText = util.styleText;
  util.styleText = (format, text, options) =>
    Array.isArray(format)
      ? format.reduce((current, style) => originalStyleText(style, current, options), text)
      : originalStyleText(format, text, options);
  nodeModule.syncBuiltinESMExports();
}
