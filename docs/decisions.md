# Quyết định kỹ thuật

Mỗi quyết định ghi: bối cảnh, lựa chọn, lý do, đánh đổi.

## 001. Train bằng Python, chạy model bằng .NET qua ONNX

- **Bối cảnh:** PhoBERT chỉ có sẵn trên hệ sinh thái HuggingFace (Python); backend dùng ASP.NET Core.
- **Lựa chọn:** Fine-tune bằng Python, export ONNX, nạp bằng ONNX Runtime trong .NET.
- **Lý do:** Tận dụng công cụ ML tốt nhất mà không phải chạy thêm service Python khi production.
- **Đánh đổi:** Phải tự viết bước tách từ (word segmentation) và tokenizer phía .NET cho khớp với lúc train.

## 002. Dùng Result thay vì exception cho luồng nghiệp vụ

- **Lựa chọn:** `Result<T>` trong Core.
- **Lý do:** Lỗi nghiệp vụ (text rỗng, quá dài) là bình thường, không phải ngoại lệ; code rõ luồng hơn và nhanh hơn.

## 003. Dùng xUnit cho unit test

- **Lựa chọn:** `xunit`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk` trong `tests/ScamDetector.Core.Tests`.
- **Lý do:** Agent `test-writer` và `docs/conventions.md` quy định xUnit; ba gói này là tối thiểu để `dotnet test` chạy được. Bỏ `coverlet.collector` của template vì chưa cần đo coverage.
- **Đánh đổi:** Không dùng thư viện mock; fake viết tay trong thư mục `Fakes/` của project test.

## 004. Ngưỡng kết luận `scam` dựa trên xác suất từng nhãn

- **Bối cảnh:** `docs/data-schema.md` yêu cầu chỉ kết luận `scam` khi `confidence ≥ 0.7`.
- **Lựa chọn:** `IScamModel` trả xác suất cho cả 3 nhãn (`ModelPrediction`). Nếu P(scam) ≥ ngưỡng → `scam`; ngược lại chọn nhãn lớn hơn giữa `spam` và `normal`, và thêm lý do `scam_probability_below_threshold` khi scam vẫn là nhãn có xác suất cao nhất.
- **Lý do:** Tránh báo nhầm lừa đảo khi model không chắc, nhưng vẫn để lại tín hiệu cho người dùng qua `reasons`.
- **Đánh đổi:** Ngưỡng nằm trong `ClassificationOptions` (Core không phụ thuộc `Microsoft.Extensions.Options`); tầng Api sẽ bind từ `appsettings.json`.

## 005. Validate độ dài trong Core

- **Lựa chọn:** `MessageClassifier` kiểm tra text rỗng và dài quá `ClassificationLimits.MaxMessageLength` (2000) sau khi chuẩn hóa.
- **Lý do:** Core được dùng chung bởi Api và Bot; đặt một chỗ duy nhất tránh lặp. Tầng Api chỉ ánh xạ `DomainError` sang `ProblemDetails`.
