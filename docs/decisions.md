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
- Đăng ký DI (`AddScamDetector`) và health check model nằm ở Infrastructure để Api và Bot dùng chung; Infrastructure tham chiếu shared framework `Microsoft.AspNetCore.App` (có sẵn trong runtime, không phải gói NuGet).

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

## 011. Huấn luyện bằng script, notebook chỉ là vỏ Colab

- **Lựa chọn:** `scripts/train_phobert.py` chứa toàn bộ logic (đọc split, fine-tune `vinai/phobert-base-v2`, đánh giá macro F1/F1 scam trên test, export ONNX opset 17, so logits PyTorch–ONNX, sinh `tokenizer-golden.json`). `notebooks/train_phobert_colab.ipynb` chỉ clone repo, cài thư viện và gọi script.
- **Gói (chỉ cho huấn luyện, `requirements-train.txt`):** `torch`, `transformers`, `datasets`, `accelerate` để fine-tune; `onnx`, `onnxruntime` để export và kiểm tra ONNX; `numpy`. Không dùng `scikit-learn`: F1 tự tính (có unit test) để bớt một phụ thuộc. Backend không cần các gói này.
- **Kiểm tra tokenizer:** `scripts/VerifyTokenizer.cs` chạy tokenizer .NET trên `tokenizer-golden.json` do HuggingFace sinh ra; phải khớp 100% trước khi deploy model.
- **Tokenizer chậm (`use_fast=False`):** dùng `PhobertTokenizer` gốc để có đúng `vocab.txt` và `bpe.codes` mà backend đọc.

## 012. Telegram bot gọi thẳng Bot API, chạy long polling

- **Lựa chọn:** `ScamDetector.Bot` dùng `HttpClient` gọi `getUpdates`/`sendMessage`, không dùng thư viện `Telegram.Bot`. Chạy long polling trong `BackgroundService`, không cần URL công khai hay webhook.
- **Lý do:** Chỉ cần 2 method của Bot API; tự viết ~60 dòng thay vì thêm một gói lớn (quy tắc 6). Long polling dễ chạy ở máy dev và container sau NAT.
- **Tầng:** Bot gọi `IMessageClassifier` của Core qua DI dùng chung (`AddScamDetector` trong Infrastructure, quyết định 007 được cập nhật: Infrastructure tham chiếu shared framework `Microsoft.AspNetCore.App`, không thêm gói NuGet). Phần giao tiếp Telegram nằm trong Bot vì chỉ Bot dùng.
- **Bảo mật:** Không log nội dung tin nhắn. Token chỉ lấy từ cấu hình/biến môi trường `Telegram__BotToken`, không commit.
- **Lỗi:** Gửi trả lời thất bại cho một chat (ví dụ người dùng chặn bot) chỉ ghi log rồi xử lý tin tiếp theo; lỗi `getUpdates` thì chờ `ErrorRetryDelaySeconds` rồi thử lại.

## 013. Chrome extension không cần CORS và không cần build

- **Lựa chọn:** Manifest V3, JavaScript module thuần (không bundler, không gói npm). Gọi API từ popup với `host_permissions`; địa chỉ API khác mặc định được xin quyền qua `optional_host_permissions` khi lưu cài đặt.
- **Lý do:** Trang của extension có host permission thì không bị CORS chặn, nên backend không phải mở CORS cho `chrome-extension://*`. Không có bước build giúp review và đóng gói đơn giản.
- **Kiểm tra chéo:** Test `node --test` so danh sách mã lý do (`REASON_DESCRIPTIONS`) với các hằng số trong `ClassificationReasons.cs` và `UrlReasons.cs`.
- **Quyền riêng tư:** Extension chỉ gửi đoạn text người dùng chủ động dán hoặc bôi đen; không đọc nội dung trang tự động.

## 014. Lưu báo cáo người dùng đã ẩn danh trong SQL Server

- **Lựa chọn:** Bảng `MessageReports` (EF Core, SQL Server) lưu tin người dùng báo là `scam`/`spam`/`normal` kèm kênh và thời điểm. `MessageReportService` (Core) chuẩn hóa và che PII bằng `ModelInputMasker` trước khi gọi repository, nên tin gốc không bao giờ tới database (`docs/conventions.md` mục 4.4).
- **Khóa chính:** `Guid.CreateVersion7` theo `TimeProvider` (test được). SQL Server sắp `uniqueidentifier` theo byte cuối nên v7 không tuần tự hoàn toàn trên clustered index; chấp nhận được với lượng báo cáo nhỏ, có index riêng trên `CreatedAt`.
- **Không có database:** Thiếu connection string thì đăng ký `UnavailableMessageReportRepository`, endpoint trả 503; phân loại vẫn hoạt động.
- **Gói:** `Microsoft.EntityFrameworkCore.SqlServer` (provider), `Microsoft.EntityFrameworkCore.Design` (`PrivateAssets=all`, chỉ cho `dotnet ef`), `Microsoft.EntityFrameworkCore.Sqlite` (chỉ trong Infrastructure.Tests, test repository không cần SQL Server). `dotnet-ef` là tool cục bộ (`dotnet-tools.json`).
- **Kiểm tra:** `MigrationTests` so model với snapshot; CI chạy `dotnet ef database update` trên container SQL Server thật.
- **Độ dài sau khi che:** Placeholder có thể dài hơn chuỗi gốc (`a@b.vn` thành `<EMAIL>`), nên service kiểm tra lại độ dài sau khi che để không vượt cột `nvarchar(2000)`.
- **Lỗi database:** `EfMessageReportRepository` đổi `DbUpdateException`/`DbException` thành `ReportsUnavailableException`, nên Api trả 503 và bot vẫn trả lời callback thay vì để nút quay mãi.
- **Dữ liệu chưa đáng tin:** Endpoint không xác thực; `channel` do client tự khai và ai cũng có thể gửi nhãn sai (rate limit chỉ giảm nhẹ). `MessageReports` **không bao giờ** được đưa thẳng vào `dataset.csv`: người gán nhãn phải duyệt từng mẫu rồi đi qua quy trình ở `docs/data-schema.md`, gắn `source = contributed`.
- **Che PII có giới hạn:** Chỉ che email, URL, OTP, CCCD/CMND, số điện thoại, số tài khoản/thẻ; tên người và địa chỉ có thể còn, nên người duyệt vẫn phải kiểm tra tay.
- **Cấu hình compose là cho dev:** dùng tài khoản `sa` và `TrustServerCertificate=True`. Production cần user SQL riêng chỉ có quyền trên database `ScamDetector`, chứng chỉ TLS hợp lệ và mật khẩu từ secret store (không chứa `;` hay `=` nếu ghép vào connection string).

## 015. Giới hạn request theo IP

- **Lựa chọn:** `RateLimiter` có sẵn của ASP.NET, cửa sổ cố định theo IP cho endpoint phân loại và báo cáo (mặc định 30 request/60 giây, hằng `RateLimitOptions.Default*`); `/health` không giới hạn.
- **Sau reverse proxy:** `RateLimiting:TrustForwardedHeaders=true` bật `X-Forwarded-For` và tin mọi proxy (xóa `KnownProxies`/`KnownIPNetworks`). Chỉ bật khi API **không** nhận kết nối trực tiếp từ internet, nếu không client tự đặt header để lách giới hạn. Mặc định tắt.

## 016. Xuất báo cáo qua API có API key

- **Lựa chọn:** `GET /api/v1/reports` trả báo cáo đã ẩn danh, bảo vệ bằng header `X-Api-Key` so với `ReportAdmin:ApiKey` (so sánh hash SHA-256 bằng `CryptographicOperations.FixedTimeEquals` để không lộ qua thời gian phản hồi). Không cấu hình key thì endpoint trả 404 như không tồn tại.
- **Lý do:** Người gán nhãn không cần quyền truy cập SQL Server; script `export_reports.py` chỉ dùng thư viện chuẩn Python (không thêm `pyodbc`).
- **Đánh đổi:** Một key dùng chung, chưa phân quyền theo người. Đủ cho nhóm nhỏ; nếu mở rộng thì thay bằng xác thực thật (OIDC).
- **SQLite trong test:** SQLite không so sánh được `DateTimeOffset`, nên `ScamDetectorDbContext` chỉ bật `DateTimeOffsetToBinaryConverter` khi provider là SQLite; model SQL Server và migration không đổi (`MigrationTests` kiểm tra).

## 017. Kiểm chứng tokenizer .NET với HuggingFace

- **Bối cảnh:** Quyết định 001 đánh đổi bằng việc tự viết tokenizer; lệch một id là model cho kết quả sai mà không báo lỗi.
- **Đã kiểm chứng:** Train một bộ BPE 1500 merge (cùng kiểu fastBPE) trên văn bản tiếng Việt ngẫu nhiên, so `PhobertTokenizer` của `transformers` với `PhoBertTokenizer` .NET trên 3005 câu (gồm chữ hoa, emoji, placeholder, chuỗi rỗng, câu dài bị cắt ở 256 token): khớp 3005/3005.
- **Trong CI:** Job `tokenizer-parity` tải tokenizer thật của `vinai/phobert-base-v2`, sinh `tokenizer-golden.json` bằng `scripts/make_tokenizer_golden.py` (chỉ cần `transformers`, không cần `torch`) rồi chạy `scripts/VerifyTokenizer.cs`. Mọi thay đổi ở `Tokenization/` đều được kiểm tra với vocab thật.
- **Hợp đồng export:** Workflow `model-contract.yml` (chạy khi đổi script train, Core, Onnx/Tokenization hoặc Api) tải `vinai/phobert-base-v2` với head 3 nhãn chưa train, export bằng chính `export_onnx` của script train, so logits PyTorch/ONNX, đối chiếu tokenizer, rồi chạy `ScamDetector.Api` bản publish với model đó và gọi `/api/v1/classifications`. Model chưa train nên kết quả vô nghĩa; mục tiêu là bắt lỗi tên input/output, kiểu dữ liệu, shape và opset trước khi tốn công train thật.

## 018. Extension dạng side panel, đọc chữ trong ảnh ngay trên máy người dùng

- **Bối cảnh:** Người dùng muốn mở extension ra một cửa sổ nhỏ để dán nội dung hoặc **ảnh chụp màn hình** tin nhắn, rồi để API xác định có lừa đảo không.
- **Giao diện:** Side panel (`chrome.sidePanel`) thay cho popup: không tự đóng khi bấm ra ngoài, đủ chỗ cho ảnh xem trước và kết quả. Bôi đen giữ cách chuột phải → Kiểm tra để không cần quyền đọc mọi trang web.
- **OCR trên máy (Tesseract.js, dữ liệu `vie` bản `4.0.0_best_int`, 1,4 MB):** Ảnh chụp thường có tên, số tài khoản, tin nhắn riêng; đọc trên máy nghĩa là ảnh không bao giờ gửi đi, chỉ phần chữ (người dùng xem và sửa được) tới API. Backend không phải đổi gì. Ảnh nền tối được đảo màu, ảnh nhỏ được phóng to trước khi đọc.
- **Đã đo:** Ảnh chụp tin nhắn tiếng Việt (SMS, Zalo, OTP, chế độ tối) đọc đúng 100% ký tự, 0,3–0,5 giây mỗi ảnh sau lần khởi tạo đầu (~1 giây).
- **Gói npm:** `tesseract.js` (OCR, chạy WebAssembly; manifest cần `'wasm-unsafe-eval'`), `@tesseract.js-data/vie` (dữ liệu tiếng Việt), `playwright` (dev, chỉ cho test e2e chạy Chromium thật). Các file Tesseract được chép vào `extension/vendor/` khi `npm run build` (không commit, ~13 MB) vì Manifest V3 cấm tải code từ xa.
- **Đánh đổi:** Gói extension nặng thêm ~13 MB; OCR kém hơn với ảnh mờ, chữ viết tay hoặc font lạ. Người dùng luôn thấy và sửa được chữ đã đọc trước khi kiểm tra.

## 019. Tự cập nhật danh sách chặn và văn mẫu lừa đảo, hai tầng

- **Bối cảnh:** Kẻ lừa đảo đổi tên miền và kịch bản liên tục; model chỉ học lại được khi train lại, chậm và tốn công gán nhãn.
- **Lựa chọn:** Hai tầng. Tầng 1: danh sách tên miền và văn mẫu cập nhật hằng ngày, áp dụng ngay khi PR được merge (API nạp lại file khi đổi, không khởi động lại). Tầng 2: dữ liệu train chỉ vào `dataset.csv` sau khi người gán nhãn, không bao giờ tự động (giữ quy tắc 3 trong CLAUDE.md).
- **Ra quyết định:** Trùng tên miền chặn (gồm subdomain) hoặc trùng ≥ 60% cụm 3 từ của văn mẫu → `scam`, kể cả khi model nói khác hoặc chưa có model. Đây là tín hiệu mạnh vì mỗi mục đều có nguồn chính thống và được người duyệt; độ tin cậy 0.99 cho tên miền, bằng tỉ lệ trùng cho văn mẫu. Khi chưa có model và không trùng gì, API vẫn trả 503 như cũ.
- **So khớp văn mẫu:** So trên text đã ẩn danh (cùng `ModelInputMasker`), token là placeholder hoặc chuỗi chữ/số, chữ thường, NFC. Độ phủ = số cụm 3 từ của văn mẫu xuất hiện trong tin / tổng số cụm của văn mẫu, nên tin dài có thêm lời chào vẫn khớp. Cài đặt song song ở Python (`similarity.py`) và C# (`TemplateShingles`).
- **Crawler (chỉ thư viện chuẩn Python):** Đọc `robots.txt`, chờ giữa request cùng host, giới hạn kích thước trang. Chỉ lấy tên miền nằm trong đoạn có ngữ cảnh cảnh báo; bỏ tên miền trong danh sách trắng (`allowed_domains.csv`, ngân hàng, ví, cơ quan nhà nước, báo chí) và bỏ gốc của nền tảng dùng chung (`blogspot.com`, `github.io`...; chỉ subdomain mới bị chặn). Văn mẫu phải có dấu hiệu lừa đảo, lời kêu gọi hành động, và không phải lời khuyên hay lời kể của nạn nhân.
- **Mạng xã hội chỉ nhập tay:** Tự động scrape Facebook/Zalo vi phạm điều khoản và dễ thu thập dữ liệu cá nhân, nên chỉ nhận qua `manual_submissions.csv`.
- **GitHub Actions mở PR thay vì commit thẳng:** Một tên miền chặn nhầm (ví dụ trang ngân hàng thật) gây hại ngay cho người dùng, nên luôn có người duyệt.
- **Đánh đổi:** Phụ thuộc cấu trúc HTML của từng nguồn; nguồn đổi giao diện thì crawler trả lỗi (ghi trong báo cáo, không làm hỏng lần chạy). URL nguồn chưa kiểm chứng được từ môi trường phát triển (bị chặn mạng), được kiểm tra bằng job chạy thử trong CI. Nguồn danh sách đen cộng đồng (loại `domain_list`) đã có code nhưng chưa thêm vào `sources.json` vì chưa xác nhận được URL.

## 020. Agent duyệt dữ liệu threat-intel với ràng buộc cứng

- **Bối cảnh:** Mỗi ngày có thể có hàng chục mục mới; người duyệt cần trợ giúp nhưng không thể để AI tự quyết đưa dữ liệu vào hệ thống.
- **Lựa chọn:** Hai lớp. Lớp 1 là luật cứng chạy bằng code (`validate_threat_intel.py`, chạy trong CI và trước khi mở PR): định dạng, không còn dữ liệu cá nhân sau khi ẩn danh, không trùng danh sách trắng, không phải gốc nền tảng dùng chung, không trùng mục đã loại. Lớp 2 là agent `threat-intel-reviewer` đánh giá ngữ nghĩa (tên miền có thật là giả mạo không, văn mẫu có phải tin lừa thật không).
- **Ràng buộc của agent:** không truy cập mạng và không mở link trong dữ liệu (tránh bị trang lừa đảo tác động hoặc prompt injection), chỉ được loại qua `reject_threat_intel.py` (không thêm, không sửa), chỉ duyệt dòng mới so với `origin/main`, không đọc `data/raw/`, không commit/push/merge. Trường hợp không chắc thì xếp CẦN NGƯỜI XEM, không đoán. Báo cáo cảnh báo nguồn có hơn 30% mục bị loại.
- **Đã thử:** Bộ 7 tên miền và 5 văn mẫu gồm cả bẫy (gốc `blogspot.com`, trang bán lẻ thật, lời khuyên của công an, lời kể nạn nhân, thông báo OTP hợp lệ): agent xử lý đúng cả 12 mục.
- **Mục bị loại lưu dấu vân tay:** `rejected_templates.csv` chỉ lưu SHA-256 (12 ký tự hex) của từng cụm 3 từ, không lưu nội dung, vì đoạn bị loại có thể chứa tên thật hay chi tiết riêng tư; vẫn đủ để nhận ra văn mẫu gần giống khi crawl lại.
