namespace ScamDetector.Bot.Messaging;

public static class BotReplies
{
    public const string Welcome =
        "Xin chào! Hãy gửi hoặc chuyển tiếp (forward) tin nhắn bạn nghi ngờ, mình sẽ kiểm tra xem có dấu hiệu lừa đảo không.\n\n" +
        "Lưu ý: kết quả chỉ mang tính tham khảo. Không bao giờ cung cấp mã OTP, mật khẩu hay chuyển tiền cho người lạ.";

    public const string ScamVerdict = "⚠️ CÓ DẤU HIỆU LỪA ĐẢO";
    public const string SpamVerdict = "📢 Tin quảng cáo (spam)";
    public const string NormalVerdict = "✅ Chưa thấy dấu hiệu lừa đảo";
    public const string ConfidenceLabel = "Độ tin cậy";
    public const string ReasonsLabel = "Lý do";
    public const string ScamAdvice = "Đừng bấm link, đừng cung cấp OTP/mật khẩu và đừng chuyển tiền. Hãy gọi tổng đài chính thức để xác minh.";
    public const string EmptyText = "Tin nhắn trống. Hãy gửi nội dung cần kiểm tra.";
    public const string TextTooLong = "Tin nhắn quá dài (tối đa 2000 ký tự). Hãy gửi phần nội dung chính.";
    public const string InvalidText = "Không đọc được nội dung tin nhắn.";
    public const string ModelUnavailable = "Hệ thống đang bảo trì, bạn thử lại sau nhé.";
}
