import { copyFileSync, mkdirSync, readdirSync, rmSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const extensionRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const modules = join(extensionRoot, "node_modules");
const target = join(extensionRoot, "vendor", "tesseract");
const LSTM_CORE_SUFFIX = "-lstm.wasm.js";
const LANGUAGE_DATA = join(modules, "@tesseract.js-data", "vie", "4.0.0_best_int", "vie.traineddata.gz");

function copy(source, destination) {
  mkdirSync(dirname(destination), { recursive: true });
  copyFileSync(source, destination);
}

rmSync(target, { recursive: true, force: true });
copy(join(modules, "tesseract.js", "dist", "tesseract.esm.min.js"), join(target, "tesseract.esm.min.js"));
copy(join(modules, "tesseract.js", "dist", "worker.min.js"), join(target, "worker.min.js"));
for (const file of readdirSync(join(modules, "tesseract.js-core")).filter((name) => name.endsWith(LSTM_CORE_SUFFIX))) {
  copy(join(modules, "tesseract.js-core", file), join(target, "core", file));
}
copy(LANGUAGE_DATA, join(target, "lang", "vie.traineddata.gz"));
console.log(`Đã chép Tesseract vào ${target}`);
