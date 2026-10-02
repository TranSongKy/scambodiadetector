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
tests/                           xUnit: Core, Infrastructure (model ONNX fixture), Api (integration)
scripts/                         validate_dataset.py, split_dataset.py và test Python
data/processed/dataset.csv       Dữ liệu đã ẩn danh (data/raw/ không bao giờ commit)
models/                          File model khi chạy (không commit)
```

## Yêu cầu

- .NET SDK 10.0
- Python 3.11+ và `ruff` (cho script dữ liệu)
- Docker (tùy chọn, để deploy)

## Chạy test

```bash
dotnet build
dotnet test
python -m unittest discover -s scripts/tests -t .
ruff check . && ruff format --check .
python scripts/validate_dataset.py data/processed/dataset.csv
```

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

### `GET /health`

`200 Healthy` khi model đã nạp, `503 Unhealthy` khi thiếu file model.

## Deploy bằng Docker

```bash
docker build -t scam-detector-api .
docker run -p 8080:8080 -v "$PWD/models:/models:ro" scam-detector-api
# hoặc
docker compose up --build
```

- Image chạy bằng user không phải root, cổng `8080`.
- Model mount vào `/models` (chỉ đọc). Đổi đường dẫn bằng biến môi trường `OnnxModel__ModelPath`, `OnnxModel__VocabularyPath`, `OnnxModel__BpeCodesPath`.
- Đổi ngưỡng bằng `Classification__ScamThreshold` (trong khoảng (0, 1], sai thì app dừng khi khởi động).
- Dùng `/health` làm readiness probe.

## Hợp đồng giữa notebook huấn luyện và backend

Model export phải khớp các điểm sau, nếu không kết quả sẽ sai mà không báo lỗi:

1. Input ONNX: `input_ids` và `attention_mask`, kiểu `int64`, shape `[1, sequence]`. Output: `logits`, shape `[1, 3]`.
2. Thứ tự nhãn của logits khớp `OnnxModel:LabelOrder` (mặc định `normal`, `spam`, `scam`).
3. Tokenizer là PhoBERT fastBPE (`vocab.txt` và `bpe.codes`), tối đa 256 token kể cả `<s>` và `</s>`, không lowercase.
4. Text khi train đã ẩn danh bằng cùng placeholder mà `ModelInputMasker` dùng khi suy luận (`<URL>`, `<EMAIL>`, `<PHONE>`, `<ACCOUNT>`, `<OTP>`, `<ID>`). Xem quyết định 008.
5. Không tách từ bằng VnCoreNLP, text chuẩn hóa NFC và gộp khoảng trắng như `TextNormalizer`. Xem quyết định 010.

## CI

`.github/workflows/ci.yml` chạy build và test .NET, ruff và test Python, validate dataset (khi có mẫu), build Docker image và smoke test container.
