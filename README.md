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

### `GET /health`

`200 Healthy` khi model đã nạp, `503 Unhealthy` khi thiếu file model.

## Telegram bot

1. Tạo bot với [@BotFather](https://t.me/BotFather), lấy token.
2. Chạy:

```bash
Telegram__BotToken=<token> dotnet run --project src/ScamDetector.Bot
```

Người dùng gửi hoặc forward tin nhắn nghi ngờ cho bot, bot trả lời kết luận, độ tin cậy, lý do bằng tiếng Việt và lời khuyên khi là lừa đảo. `/start`, `/help` hiện hướng dẫn. Bot không log nội dung tin nhắn.

## Chrome extension

1. Mở `chrome://extensions`, bật **Developer mode**, chọn **Load unpacked** và trỏ tới thư mục `extension/`.
2. Mặc định gọi API ở `http://localhost:8080`. Đổi trong **Cài đặt** của extension; Chrome sẽ hỏi quyền truy cập địa chỉ mới.
3. Dùng: bấm biểu tượng extension và dán tin nhắn, hoặc bôi đen tin nhắn trên trang web → chuột phải → **Kiểm tra tin nhắn này có lừa đảo không**.

Test: `cd extension && npm test` (Node 22, không cần cài gói). CI đóng gói `scambodia-extension.zip` để tải lên Chrome Web Store.

## Deploy bằng Docker

```bash
docker build -t scam-detector-api .
docker run -p 8080:8080 -v "$PWD/models:/models:ro" scam-detector-api

docker build --build-arg PROJECT=ScamDetector.Bot -t scam-detector-bot .
docker run -e Telegram__BotToken=<token> -v "$PWD/models:/models:ro" scam-detector-bot

# hoặc bằng compose (bot cần TELEGRAM_BOT_TOKEN trong file .env)
docker compose up --build              # chỉ api
docker compose --profile bot up --build # api + bot
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

`.github/workflows/ci.yml` chạy build và test .NET, ruff và test Python, validate dataset (khi có mẫu), test và đóng gói Chrome extension, build Docker image cho Api và Bot rồi smoke test container.
