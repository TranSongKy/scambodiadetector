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
  url_obfuscated: "Link bị viết lách (như [.] hay hxxp) để né bộ lọc",
  url_brand_impersonation: "Tên miền giả danh ngân hàng, ví điện tử hoặc cơ quan nhà nước",
  url_blocklisted: "Link nằm trong danh sách tên miền lừa đảo đã được cảnh báo",
  matches_known_scam_template: "Nội dung trùng với mẫu tin lừa đảo đã được cảnh báo",
});

export const SCAM_ADVICE =
  "Đừng bấm link, đừng cung cấp OTP/mật khẩu và đừng chuyển tiền. Hãy gọi tổng đài chính thức để xác minh.";

export const ERROR_MESSAGES = Object.freeze({
  emptyText: "Hãy dán nội dung tin nhắn cần kiểm tra.",
  textTooLong: `Tin nhắn quá dài (tối đa ${MAX_MESSAGE_LENGTH} ký tự).`,
  modelUnavailable: "Hệ thống đang bảo trì, bạn thử lại sau nhé.",
  rateLimited: "Bạn kiểm tra hơi nhiều trong thời gian ngắn. Vui lòng đợi khoảng 1 phút rồi thử lại.",
  network: "Không kết nối được máy chủ. Kiểm tra địa chỉ API trong phần Cài đặt.",
  unexpected: "Có lỗi xảy ra, bạn thử lại sau nhé.",
  invalidApiUrl: "Địa chỉ API phải dùng https:// (chỉ cho phép http:// với localhost).",
  timeout: "Máy chủ phản hồi quá lâu, bạn thử lại sau nhé.",
  notAnImage: "Tệp này không phải ảnh. Hãy dán ảnh chụp màn hình (PNG, JPG...).",
  imageTooLarge: "Ảnh quá lớn (tối đa 10 MB).",
  ocrFailed: "Không đọc được chữ trong ảnh. Hãy thử ảnh rõ hơn hoặc gõ nội dung vào ô.",
  ocrEmpty: "Không tìm thấy chữ nào trong ảnh.",
});

export const UI_TEXT = Object.freeze({
  confidenceLabel: "Độ tin cậy",
  contextMenuTitle: "Kiểm tra tin nhắn này có lừa đảo không",
  ocrRunning: "Đang đọc chữ trong ảnh…",
  ocrDone: "Đã đọc chữ từ ảnh. Kiểm tra lại nội dung rồi bấm Kiểm tra.",
  saved: "Đã lưu.",
  permissionDenied: "Cần cấp quyền truy cập địa chỉ này để kiểm tra tin nhắn.",
  reportPrompt: "Kết quả chưa đúng? Cho mình biết tin này thực sự là:",
  reportThanks: "Cảm ơn bạn! Phản hồi đã được ghi nhận (số điện thoại, tài khoản, OTP, link đã được ẩn).",
});

export const REPORT_LABEL_TEXT = Object.freeze({
  [LABELS.scam]: "Lừa đảo",
  [LABELS.spam]: "Quảng cáo",
  [LABELS.normal]: "Bình thường",
});

export const PAGE_SCAN_TEXT = Object.freeze({
  messageWarningTitle: "⚠️ Tin nhắn có dấu hiệu lừa đảo",
  dangerousLinkTitle: "⚠️ Scambodia: link nguy hiểm",
  suspiciousLinkTitle: "Scambodia: link đáng ngờ",
  dangerousLinkConfirm: "Scambodia cảnh báo: link này có dấu hiệu lừa đảo. Bạn vẫn muốn mở?",
  showMessage: "Xem tin",
  dismiss: "Đóng cảnh báo",
  optionsHeading: "Tự động kiểm tra trên trang chat",
  optionsHint:
    "Chỉ chạy trên các trang bạn bật. Extension kiểm tra link trên trang và tin nhắn mới hiện ra; số điện thoại, số tài khoản, OTP, email được che ngay trên máy trước khi gửi đi.",
  customSiteLabel: "Thêm trang khác (https://…)",
  addSite: "Thêm",
  invalidSite: "Địa chỉ trang phải bắt đầu bằng https://",
  siteEnabled: "Đã bật tự động kiểm tra.",
  siteDisabled: "Đã tắt tự động kiểm tra.",
});
