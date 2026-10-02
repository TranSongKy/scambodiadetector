import { LABELS, MAX_MESSAGE_LENGTH } from "./constants.js";

export const VERDICTS = Object.freeze({
  [LABELS.scam]: "⚠️ CÓ DẤU HIỆU LỪA ĐẢO",
  [LABELS.spam]: "📢 Tin quảng cáo (spam)",
  [LABELS.normal]: "✅ Chưa thấy dấu hiệu lừa đảo",
});

export const REASON_DESCRIPTIONS = Object.freeze({
  model_predicted_scam: "Nội dung giống các tin lừa đảo đã biết",
  model_predicted_spam: "Nội dung giống tin quảng cáo hàng loạt",
  scam_probability_below_threshold: "Có vài dấu hiệu lừa đảo nhưng chưa đủ chắc chắn",
  url_shortener: "Có link rút gọn, che giấu trang đích",
  url_ip_address_host: "Link dùng địa chỉ IP thay vì tên miền",
  url_punycode: "Tên miền dùng ký tự giả mạo (punycode)",
  url_suspicious_tld: "Tên miền có đuôi thường bị dùng để lừa đảo",
});

export const SCAM_ADVICE =
  "Đừng bấm link, đừng cung cấp OTP/mật khẩu và đừng chuyển tiền. Hãy gọi tổng đài chính thức để xác minh.";

export const ERROR_MESSAGES = Object.freeze({
  emptyText: "Hãy dán nội dung tin nhắn cần kiểm tra.",
  textTooLong: `Tin nhắn quá dài (tối đa ${MAX_MESSAGE_LENGTH} ký tự).`,
  modelUnavailable: "Hệ thống đang bảo trì, bạn thử lại sau nhé.",
  network: "Không kết nối được máy chủ. Kiểm tra địa chỉ API trong phần Cài đặt.",
  unexpected: "Có lỗi xảy ra, bạn thử lại sau nhé.",
  invalidApiUrl: "Địa chỉ API không hợp lệ (cần bắt đầu bằng http:// hoặc https://).",
});
