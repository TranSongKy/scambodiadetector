# Scambodia Detector

Hệ thống phát hiện tin nhắn lừa đảo tiếng Việt. Phân loại tin nhắn thành `scam`, `spam`, `normal`, kèm độ tin cậy và lý do.

- **Model:** PhoBERT fine-tune bằng Python, export ONNX (`notebooks/`).
- **Backend:** ASP.NET Core (.NET 10), chạy model bằng ONNX Runtime.
- **Kênh:** Web API, Telegram bot, Chrome extension.

Quy ước code và dữ liệu: [`CLAUDE.md`](CLAUDE.md), [`docs/conventions.md`](docs/conventions.md), [`docs/data-schema.md`](docs/data-schema.md). Lý do các quyết định kỹ thuật: [`docs/decisions.md`](docs/decisions.md).

## Cấu trúc

```
src/ScamDetector.Core            Domain: phân loại, ngưỡng scam, chuẩn hóa/che PII, kiểm tra URL
src/ScamDetector.Infrastructure  Tokenizer PhoBERT (fastBPE), model ONNX
src/ScamDetector.Api             Minimal API: POST /api/v1/classifications, /health
src/ScamDetector.Bot             Telegram bot (long polling), /health
extension/                       Chrome extension (Manifest V3)
tests/                           xUnit: Core, Infrastructure (model ONNX fixture), Api (integration), Bot
scripts/                         masking.py, anonymize.py, validate_dataset.py, split_dataset.py và test Python
data/processed/dataset.csv       Dữ liệu đã ẩn danh (data/raw/ không bao giờ commit)
models/                          File model khi chạy (không commit)
```

## Yêu cầu

- .NET SDK 10.0
- Python 3.11+ và `ruff` (cho script dữ liệu)
- Docker (tùy chọn, để deploy)

## Chạy test

Một lệnh cho tất cả (build, test .NET, ruff, test Python, dataset, extension):

```bash
./scripts/check_all.sh
```

Hoặc từng phần:

```bash
dotnet build
dotnet test
python -m unittest discover -s scripts/tests -t .
ruff check . && ruff format --check .
python scripts/validate_dataset.py data/processed/dataset.csv
(cd extension && npm test)
```

## Quy trình dữ liệu

```bash
python scripts/anonymize.py data/raw/batch.csv data/processed/new_batch.csv --start-id 1
# kiểm tra tay các id được báo có thể chứa tên người, gộp vào data/processed/dataset.csv
python scripts/validate_dataset.py data/processed/dataset.csv
python scripts/split_dataset.py data/processed/dataset.csv data/processed/splits
```

Báo cáo người dùng từ API/bot/extension: `scripts/export_reports.py` (xem mục API) → duyệt tay → `anonymize.py` như trên.

`anonymize.py` không in nội dung tin nhắn, chỉ in id và số lượng. Tập test trong `splits/test.csv` cố định sau lần chia đầu.

## Huấn luyện model

Mở `notebooks/train_phobert_colab.ipynb` trên Google Colab (GPU), hoặc chạy trực tiếp trên máy có GPU:

```bash
pip install -r requirements-train.txt
python scripts/train_phobert.py data/processed/splits models/
dotnet run scripts/VerifyTokenizer.cs -- models   # tokenizer .NET phải khớp HuggingFace
```

Script in macro F1, F1 scam trên tập test và dòng để ghi vào `docs/experiments.md`; trả lỗi nếu logits ONNX lệch PyTorch quá `1e-3`.

## Chạy API ở máy

API cần 3 file model trong `models/` (đường dẫn cấu hình ở `src/ScamDetector.Api/appsettings.json`, mục `OnnxModel`):

| File | Nguồn |
|---|---|
| `models/scam-detector.onnx` | Export từ notebook huấn luyện |
| `models/vocab.txt` | `vocab.txt` của tokenizer PhoBERT đã dùng khi train |
| `models/bpe.codes` | `bpe.codes` của tokenizer PhoBERT đã dùng khi train |

```bash
dotnet run --project src/ScamDetector.Api
```

Khi thiếu file model, API vẫn khởi động: `/health` trả `503 Unhealthy` và endpoint phân loại trả `503` dạng ProblemDetails.

Muốn chạy thử ngay mà chưa có model thật, dùng model fixture của test (chỉ để kiểm tra luồng, không phân loại có nghĩa):

```bash
F=tests/ScamDetector.Infrastructure.Tests/Fixtures
OnnxModel__ModelPath=$PWD/$F/fixture-model.onnx \
OnnxModel__VocabularyPath=$PWD/$F/vocab.txt \
OnnxModel__BpeCodesPath=$PWD/$F/bpe.codes \
dotnet run --project src/ScamDetector.Api
```

## API

### `POST /api/v1/classifications`

```bash
curl -X POST http://localhost:5234/api/v1/classifications \
  -H 'content-type: application/json' \
  -d '{"text":"Tai khoan cua quy khach bi khoa, xac minh tai bit.ly/abc"}'
```

```json
{ "label": "scam", "confidence": 0.93, "reasons": ["model_predicted_scam", "url_shortener"] }
```

| Trường | Ý nghĩa |
|---|---|
| `label` | `scam`, `spam` hoặc `normal`. Chỉ kết luận `scam` khi xác suất ≥ `Classification:ScamThreshold` (mặc định 0.7) |
| `confidence` | Xác suất của nhãn được chọn |
| `reasons` | `model_predicted_scam`, `model_predicted_spam`, `scam_probability_below_threshold`, `url_shortener`, `url_ip_address_host`, `url_punycode`, `url_suspicious_tld` |

Lỗi trả về theo RFC 9457 (`application/problem+json`):

| Mã | Khi nào | `code` |
|---|---|---|
| 400 | Text rỗng | `classification.empty_text` |
| 400 | Text dài hơn 2000 ký tự | `classification.text_too_long` |
| 400 | JSON sai | — |
| 503 | Chưa có file model | — |

### `POST /api/v1/reports`

Người dùng báo một tin nhắn là `scam`, `spam` hoặc `normal` để bổ sung dữ liệu. Tin được chuẩn hóa và che PII **trước khi lưu**; database không bao giờ chứa tin gốc.

```bash
curl -X POST http://localhost:8080/api/v1/reports \
  -H 'content-type: application/json' \
  -d '{"text":"Goi 0901234567 de nhan qua","label":"scam","channel":"extension"}'
# 201 Created, Location: /api/v1/reports/{id}, body {"id":"..."}
```

`channel` là `api` (mặc định), `telegram` hoặc `extension`. Lỗi: 400 `report.invalid_label`, `report.invalid_channel`, `classification.empty_text`, `classification.text_too_long`; 503 khi chưa cấu hình database.

### `GET /api/v1/reports` (quản trị)

Liệt kê báo cáo đã ẩn danh để người gán nhãn duyệt. Cần header `X-Api-Key` khớp `ReportAdmin:ApiKey`; khi chưa cấu hình key thì endpoint trả 404. Tham số: `since` (ISO 8601), `limit` (1–500, mặc định 100).

```bash
REPORTS_API_KEY=<key> python scripts/export_reports.py data/raw/reports_review.csv --api https://api.example.vn --since 2026-10-01
```

File xuất có `source = contributed`, `annotator` để trống; duyệt và sửa nhãn rồi đưa qua `scripts/anonymize.py` như dữ liệu thô khác.

### `GET /health`

`200 Healthy` khi model đã nạp, `503 Unhealthy` khi thiếu file model.

## Telegram bot

1. Tạo bot với [@BotFather](https://t.me/BotFather), lấy token.
2. Chạy:

```bash
Telegram__BotToken=<token> dotnet run --project src/ScamDetector.Bot
```

Người dùng gửi hoặc forward tin nhắn nghi ngờ cho bot, bot trả lời kết luận, độ tin cậy, lý do bằng tiếng Việt và lời khuyên khi là lừa đảo, kèm 3 nút **Báo: Lừa đảo / Quảng cáo / Bình thường** để người dùng sửa kết quả (lưu vào `MessageReports` đã ẩn danh, cần `ConnectionStrings:ScamDetector`). `/start`, `/help` hiện hướng dẫn. Bot không log nội dung tin nhắn.

## Chrome extension

Bấm biểu tượng extension để mở **bảng kiểm tra bên cạnh trang** (side panel). Trong bảng có thể:

- **Dán nội dung** tin nhắn vào ô rồi bấm **Kiểm tra**.
- **Dán ảnh chụp màn hình** (Ctrl+V), kéo thả ảnh, hoặc chọn file. Extension đọc chữ trong ảnh ngay trên máy (OCR tiếng Việt, cả ảnh nền tối), điền vào ô để người dùng sửa nếu cần, rồi mới gửi **phần chữ** lên API. Ảnh không bao giờ rời khỏi máy.
- Trên trang web bất kỳ: **bôi đen** tin nhắn → chuột phải → **Kiểm tra tin nhắn này có lừa đảo không**, bảng bên cạnh mở ra và tự kiểm tra.
- Dưới kết quả có nút báo lại nhãn đúng (gửi tới `/api/v1/reports`).

Cài đặt khi phát triển:

```bash
cd extension
npm ci
npm run build        # chép Tesseract (OCR) và dữ liệu tiếng Việt vào extension/vendor/
```

Mở `chrome://extensions`, bật **Developer mode**, chọn **Load unpacked** và trỏ tới thư mục `extension/`. Mặc định gọi API ở `http://localhost:8080`; đổi trong **Cài đặt** của extension (chỉ chấp nhận `https://`, hoặc `http://` với localhost).

Test:

```bash
npm test             # unit test (Node 22)
npm run test:e2e     # mở Chromium thật với extension: dán ảnh, OCR, gọi API giả lập, báo cáo
npm run package      # tạo scambodia-extension.zip để tải lên Chrome Web Store
```

## Database

SQL Server qua EF Core, bảng `MessageReports`. Cấu hình bằng `ConnectionStrings:ScamDetector`; để trống thì API vẫn chạy, chỉ `/api/v1/reports` trả 503.

```bash
dotnet tool restore
dotnet ef migrations add <TenMigration> --project src/ScamDetector.Infrastructure --output-dir Persistence/Migrations
dotnet ef database update --project src/ScamDetector.Infrastructure --connection "<connection string>"
```

Đặt `Database:ApplyMigrationsOnStartup=true` để API tự chạy migration khi khởi động (compose đã bật). Test `MigrationTests` báo lỗi nếu đổi model mà quên tạo migration.

## Deploy bằng Docker

```bash
docker build -t scam-detector-api .
docker run -p 8080:8080 -v "$PWD/models:/models:ro" scam-detector-api

docker build --build-arg PROJECT=ScamDetector.Bot -t scam-detector-bot .
docker run -e Telegram__BotToken=<token> -v "$PWD/models:/models:ro" scam-detector-bot

# hoặc bằng compose: SQL Server + api (+ bot)
cp .env.example .env                    # điền MSSQL_SA_PASSWORD (mật khẩu mạnh), TELEGRAM_BOT_TOKEN
docker compose up --build               # sqlserver + api, tự chạy migration
docker compose --profile bot up --build # thêm bot
```

- Image chạy bằng user không phải root, cổng `8080`.
- Model mount vào `/models` (chỉ đọc). Đổi đường dẫn bằng biến môi trường `OnnxModel__ModelPath`, `OnnxModel__VocabularyPath`, `OnnxModel__BpeCodesPath`.
- Đổi ngưỡng bằng `Classification__ScamThreshold` (trong khoảng (0, 1], sai thì app dừng khi khởi động).
- Số thread ONNX Runtime: `OnnxModel__IntraOpNumThreads` (0 = tự chọn theo số CPU). Khi giới hạn CPU cho container, đặt bằng số CPU được cấp để tránh tranh chấp.
- Dùng `/health` làm readiness probe (kiểm tra model và, khi có cấu hình, database).
- Giới hạn request theo IP: `RateLimiting__PermitLimit` (mặc định 30) mỗi `RateLimiting__WindowSeconds` (60) cho `/api/v1/classifications` và `/api/v1/reports`, vượt thì trả 429. Khi chạy sau reverse proxy tin cậy (nginx, load balancer), đặt `RateLimiting__TrustForwardedHeaders=true` để lấy IP thật từ `X-Forwarded-For`; không bật khi API lộ trực tiếp ra internet.
- Container chạy bằng UID 1654 (`app`); file trong `models/` phải đọc được bởi user này (`chmod a+r models/*`).
- Không commit `.env`; token và mật khẩu chỉ nằm trong biến môi trường hoặc secret store của nền tảng deploy.
- Compose dùng tài khoản `sa` cho tiện dev. Production: tạo user SQL riêng, ít quyền, bật TLS thật (xem quyết định 014).
- Báo cáo người dùng chưa được xác thực: duyệt tay trước khi đưa vào dataset (quyết định 014).

## Hợp đồng giữa notebook huấn luyện và backend

Model export phải khớp các điểm sau, nếu không kết quả sẽ sai mà không báo lỗi:

1. Input ONNX: `input_ids` và `attention_mask`, kiểu `int64`, shape `[1, sequence]`. Output: `logits`, shape `[1, 3]`.
2. Thứ tự nhãn của logits khớp `OnnxModel:LabelOrder` (mặc định `normal`, `spam`, `scam`).
3. Tokenizer là PhoBERT fastBPE (`vocab.txt` và `bpe.codes`), tối đa 256 token kể cả `<s>` và `</s>`, không lowercase.
4. Text khi train đã ẩn danh bằng cùng placeholder mà `ModelInputMasker` dùng khi suy luận (`<URL>`, `<EMAIL>`, `<PHONE>`, `<ACCOUNT>`, `<OTP>`, `<ID>`). Xem quyết định 008.
5. Không tách từ bằng VnCoreNLP, text chuẩn hóa NFC và gộp khoảng trắng như `TextNormalizer`. Xem quyết định 010.

## CI

`.github/workflows/ci.yml` chạy build và test .NET, chạy migration trên SQL Server thật, ruff và test Python, validate dataset (khi có mẫu), test và đóng gói Chrome extension, build Docker image cho Api và Bot rồi smoke test container.
