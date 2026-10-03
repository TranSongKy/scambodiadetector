import Tesseract from "../vendor/tesseract/tesseract.esm.min.js";
import {
  OCR_ENGINE_LSTM_ONLY,
  OCR_LANGUAGE,
  OCR_MAX_UPSCALE,
  OCR_MIN_WIDTH,
  TESSERACT_PATHS,
} from "./constants.js";
import { invertColors, isDarkImage, normalizeOcrText, upscaleFactor } from "./image-input.js";

const PNG_TYPE = "image/png";
let workerPromise = null;

function createOcrWorker(onProgress) {
  return Tesseract.createWorker(OCR_LANGUAGE, OCR_ENGINE_LSTM_ONLY, {
    workerPath: chrome.runtime.getURL(TESSERACT_PATHS.worker),
    corePath: chrome.runtime.getURL(TESSERACT_PATHS.core),
    langPath: chrome.runtime.getURL(TESSERACT_PATHS.lang),
    workerBlobURL: false,
    cacheMethod: "none",
    gzip: true,
    logger: (message) => onProgress?.(message.progress ?? 0),
  });
}

async function prepareImage(file) {
  const bitmap = await createImageBitmap(file);
  const scale = upscaleFactor(bitmap.width, OCR_MIN_WIDTH, OCR_MAX_UPSCALE);
  const canvas = new OffscreenCanvas(bitmap.width * scale, bitmap.height * scale);
  const context = canvas.getContext("2d", { willReadFrequently: true });
  context.imageSmoothingQuality = "high";
  context.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
  bitmap.close();

  const imageData = context.getImageData(0, 0, canvas.width, canvas.height);
  if (isDarkImage(imageData.data)) {
    context.putImageData(new ImageData(invertColors(imageData.data), canvas.width, canvas.height), 0, 0);
  }
  return canvas.convertToBlob({ type: PNG_TYPE });
}

export async function recognizeText(file, onProgress) {
  workerPromise ??= createOcrWorker(onProgress);
  const worker = await workerPromise;
  const { data } = await worker.recognize(await prepareImage(file));
  return normalizeOcrText(data.text);
}

export async function releaseOcr() {
  const pending = workerPromise;
  workerPromise = null;
  await (await pending)?.terminate();
}
