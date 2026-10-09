export const PLACEHOLDERS = Object.freeze({
  phone: "<PHONE>",
  account: "<ACCOUNT>",
  email: "<EMAIL>",
  id: "<ID>",
  otp: "<OTP>",
});

const EMAIL_PATTERN = /[\p{L}\p{N}_.+-]+@[\p{L}\p{N}_-]+(?:\.[\p{L}\p{N}_-]+)+/gu;
const URL_LIKE_PATTERN = /(?:https?:\/\/|www\.)[^\s<>"]+|\b(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z]{2,}\b(?:\/[^\s<>"]*)?/gi;
const OTP_PATTERN =
  /(?<=(?:otp|m[ãa] (?:x[áa]c (?:nh[ậa]n|th[ựu]c|minh)|b[ảa]o m[ậa]t|giao d[ịi]ch|k[íi]ch ho[ạa]t)|nh[ậa]p m[ãa])[^\d<>]{0,20})(?<![\d.,/])\d(?:[ .-]?\d){3,7}(?![\d.,/]?\d)/giu;
const ID_PATTERN =
  /(?<=(?:cccd|cmnd|c[ăa]n c[ưu][ớo]c|ch[ứu]ng minh|đ[ịi]nh danh|dinh danh)[^\d<>]{0,25})(?<![\d.,/])(?:\d{12}|\d{9}|\d{3}(?:[ .-]\d{3}){2,3}|\d{4}(?:[ .-]\d{4}){2})(?![\d.,/]?\d)/giu;
const PHONE_PATTERN =
  /(?<![\d.,/])(?:(?:\(\+?84\)|\+?84|0)[ .-]?(?:\d{9,10}|\d{2,4}(?:[ .-]\d{3,4}){2,3}|\d(?:[ .-]\d{2}){4})|\(0\d{1,3}\)[ .-]?\d{3,4}[ .-]?\d{3,4})(?![\d.,/]?\d)/gu;
const LONG_NUMBER_PATTERN =
  /(?<!\d)\d{4}(?:[ .-]\d{4}){2,3}(?:[ .-]\d{1,3})?(?!\d)|(?<!\d)\d{9,19}(?!\d)(?!\s?(?:đồng|dong|vnđ|vnd|đ|d)(?!\p{L}))/giu;
const URL_SLOT_PATTERN = /\u0000(\d+)\u0000/g;

export function maskPersonalData(text) {
  const protectedUrls = [];
  const withoutEmails = text.replace(EMAIL_PATTERN, PLACEHOLDERS.email);
  const withUrlSlots = withoutEmails.replace(URL_LIKE_PATTERN, (url) => {
    protectedUrls.push(url);
    return `\u0000${protectedUrls.length - 1}\u0000`;
  });
  const masked = withUrlSlots
    .replace(OTP_PATTERN, PLACEHOLDERS.otp)
    .replace(ID_PATTERN, PLACEHOLDERS.id)
    .replace(PHONE_PATTERN, PLACEHOLDERS.phone)
    .replace(LONG_NUMBER_PATTERN, PLACEHOLDERS.account);
  return masked.replace(URL_SLOT_PATTERN, (_, index) => protectedUrls[Number(index)]);
}
