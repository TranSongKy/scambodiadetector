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
- **Đo sau chuẩn hóa:** Tin gõ dạng NFD (tổ hợp dấu tách rời) dài gần gấp đôi khi đếm thô; đo sau NFC để không từ chối oan người dùng.
- **Chặn ở biên:** Kestrel giới hạn body 32 KB (`Kestrel:Limits:MaxRequestBodySize`) nên chuẩn hóa/regex không bao giờ chạy trên input quá lớn.

## 006. Dò PII trong `validate_dataset.py` theo heuristic

- **Lựa chọn:** Ghép các chữ số bị tách bằng dấu cách/chấm/gạch rồi mới dò số điện thoại; dò riêng số thẻ dạng `4-4-4-4`, dãy 9–19 chữ số liền (CMND/CCCD, số tài khoản), mã OTP đi sau từ khóa "OTP"/"mã xác nhận", URL thô chưa thay bằng `<URL>`, và placeholder không có trong danh sách của `docs/data-schema.md`.
- **Lý do:** Lọt PII vào `data/processed/` là rủi ro lớn nhất vì file này được commit.
- **Đánh đổi:** Số tiền viết liền ≥ 9 chữ số (ví dụ `500000000`) sẽ bị báo nhầm; nên viết có dấu chấm. Tên người (`<NAME>`) không thể dò tự động, vẫn phải kiểm tra tay.

## 007. Gói NuGet/pip cho Infrastructure và Api

- `Microsoft.ML.OnnxRuntime` (Infrastructure): chạy model ONNX trên CPU, đúng quyết định 001. Không dùng `Microsoft.ML` hay `Microsoft.ML.Tokenizers` để giữ phụ thuộc tối thiểu; tokenizer fastBPE của PhoBERT tự viết (~100 dòng).
- `Microsoft.AspNetCore.Mvc.Testing` (Api.Tests): `WebApplicationFactory` cho integration test. Không có cách tương đương mà không thêm gói.
- `onnx` (pip, chỉ dùng khi tạo lại fixture): `tests/ScamDetector.Infrastructure.Tests/Fixtures/generate_fixture_model.py` sinh model 410 byte để test `OnnxScamModel` thật. File `.onnx` đã commit nên chạy test không cần cài `onnx`.
- Composition root đặt ở Api (`AddScamDetector`) thay vì Infrastructure, để Infrastructure không cần các gói `Microsoft.Extensions.*`.

## 008. Che PII ở đầu vào model khi suy luận

- **Bối cảnh:** Dữ liệu train đã ẩn danh (`<URL>`, `<PHONE>`...), còn tin nhắn thật khi suy luận thì chưa. Nếu không che, model thấy phân phối khác lúc train.
- **Lựa chọn:** `ModelInputMasker` (Core) che theo thứ tự email → URL → OTP → CCCD/CMND → điện thoại → số tài khoản. OTP và `<ID>` chỉ che khi có từ khóa đứng trước (`otp`, `mã xác nhận`, `cccd`...). URL inspector vẫn nhận text chưa che để phân tích tên miền.
- **Một nguồn mỗi ngôn ngữ:** Phía Python chỉ có `scripts/masking.py`; `anonymize.py` dùng nó để che, `validate_dataset.py` dùng nó để dò ("masker còn đổi được text" nghĩa là còn PII). Phía C# là `ModelInputMasker`.
- **Kiểm tra chéo:** `tests/shared/masking-cases.json` được cả `SharedMaskingCasesTests` (C#) và `test_regex_parity.py` (Python) chạy. Sửa regex ở một bên thì phải sửa bên kia và thêm case vào file này.
- **Chưa làm:** `<NAME>` không che tự động khi suy luận; dữ liệu train nên hạn chế `<NAME>` (xem `docs/data-schema.md`).

## 009. Lỗi request ở môi trường Development

- Minimal API ném `BadHttpRequestException` khi JSON sai ở Development; `BadHttpRequestExceptionHandler` chuyển thành `ProblemDetails` 400 để hành vi giống Production.

## 010. Không tách từ (word segmentation) ở cả train và suy luận

- **Bối cảnh:** PhoBERT gốc được pre-train trên văn bản đã tách từ bằng VnCoreNLP (`học_sinh`). Tách từ ở .NET cần port RDRSegmenter hoặc chạy Java.
- **Lựa chọn:** Phiên bản đầu fine-tune và suy luận trên văn bản **không tách từ**, chỉ tách theo khoảng trắng. Notebook huấn luyện bắt buộc không gọi VnCoreNLP.
- **Lý do:** Nhất quán giữa train và suy luận quan trọng hơn vài điểm F1; tin nhắn lừa đảo thường viết không dấu, sai chính tả nên lợi ích tách từ thấp hơn văn bản chuẩn.
- **Đánh đổi:** Có thể kém hơn PhoBERT có tách từ. Nếu thí nghiệm cho thấy chênh lệch lớn (ghi trong `docs/experiments.md`), cần thêm `IWordSegmenter` ở Infrastructure và cập nhật notebook cùng lúc.
- **Placeholder:** `<URL>`, `<PHONE>`... không đăng ký làm special token; BPE tách chúng thành subword giống nhau ở cả hai phía.
