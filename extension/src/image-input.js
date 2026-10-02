import { DARK_IMAGE_LUMINANCE, IMAGE_MIME_PREFIX, LUMINANCE_SAMPLE_STEP, MAX_IMAGE_BYTES } from "./constants.js";
import { ERROR_MESSAGES } from "./messages.js";

const RGBA_CHANNELS = 4;
const MAX_CHANNEL_VALUE = 255;
const RED_WEIGHT = 0.2126;
const GREEN_WEIGHT = 0.7152;
const BLUE_WEIGHT = 0.0722;

export function validateImageFile(file) {
  if (!file?.type?.startsWith(IMAGE_MIME_PREFIX)) {
    return ERROR_MESSAGES.notAnImage;
  }
  return file.size > MAX_IMAGE_BYTES ? ERROR_MESSAGES.imageTooLarge : null;
}

export function findImageFile(dataTransfer) {
  const files = [...(dataTransfer?.files ?? [])];
  const fromItems = [...(dataTransfer?.items ?? [])]
    .filter((item) => item.kind === "file" && item.type.startsWith(IMAGE_MIME_PREFIX))
    .map((item) => item.getAsFile());
  return [...files.filter((file) => file.type.startsWith(IMAGE_MIME_PREFIX)), ...fromItems].find(Boolean) ?? null;
}

export function averageLuminance(rgba, sampleStep = LUMINANCE_SAMPLE_STEP) {
  let total = 0;
  let samples = 0;
  for (let index = 0; index < rgba.length; index += RGBA_CHANNELS * sampleStep) {
    total += RED_WEIGHT * rgba[index] + GREEN_WEIGHT * rgba[index + 1] + BLUE_WEIGHT * rgba[index + 2];
    samples += 1;
  }
  return samples === 0 ? 1 : total / samples / MAX_CHANNEL_VALUE;
}

export function isDarkImage(rgba) {
  return averageLuminance(rgba) < DARK_IMAGE_LUMINANCE;
}

export function invertColors(rgba) {
  for (let index = 0; index < rgba.length; index += RGBA_CHANNELS) {
    rgba[index] = MAX_CHANNEL_VALUE - rgba[index];
    rgba[index + 1] = MAX_CHANNEL_VALUE - rgba[index + 1];
    rgba[index + 2] = MAX_CHANNEL_VALUE - rgba[index + 2];
  }
  return rgba;
}

export function normalizeOcrText(text) {
  return (text ?? "")
    .split(/\n\s*\n/)
    .map((paragraph) => paragraph.replace(/\s+/g, " ").trim())
    .filter(Boolean)
    .join("\n");
}

export function upscaleFactor(width, minimumWidth, maximumFactor) {
  return width >= minimumWidth ? 1 : Math.min(maximumFactor, Math.ceil(minimumWidth / width));
}
