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

## Thu thập dữ liệu huấn luyện tự động

Chưa có tin nhắn thật thì dùng quy trình bán tự động (quyết định 021). Máy thu thập và gợi ý nhãn, **người duyệt từng mẫu** trước khi vào `dataset.csv`.

```
crawler (hằng tuần, GitHub Actions) ──► data/training-candidates/candidates.csv   (đã che PII, chưa có nhãn)
agent training-data-labeler        ──► gợi ý nhãn + lý do vào candidates.csv
agent synthetic-message-writer     ──► (khi bạn yêu cầu) tin normal/spam tự sinh, source=synthetic
python scripts/review_candidates.py ──► bạn duyệt → dataset.csv (annotator = bạn)
```

Nguồn và luật:

| Nguồn | Lấy gì | Ghi chú |
|---|---|---|
| Báo chí, Bộ Công an, tinnhiemmang.vn | Tin nhắn lừa đảo được trích nguyên văn trong bài (trong dấu ngoặc kép) | Thường là `scam` |
| Diễn đàn otofun | Bài viết trong chủ đề có từ khóa lừa đảo, tin nhắn, quảng cáo | Agent loại bài thảo luận và nội dung thù địch |
| Bộ dữ liệu công khai | Khai báo trong `sources.json` loại `csv_dataset`, **bắt buộc ghi giấy phép** | Chưa bật bộ nào, xem quyết định 021 |
| Nhập tay | `data/training-candidates/manual_messages.csv` (`text,label,channel,source_url,note`) | Tin bạn hoặc người quen nhận được; tin từ X/Reddit/Facebook chỉ vào bằng cách này |
| Tự sinh | Agent `synthetic-message-writer`, chỉ khi bạn yêu cầu | ≤ 20% dataset, không vào tập test |

- Trước khi lấy dữ liệu từ một trang, crawler đọc `robots.txt`; trang khai báo `Content-Signal: ai-train=no` hoặc chặn bot huấn luyện AI (GPTBot, ClaudeBot, CCBot…) bị bỏ qua. Vì vậy voz.vn và tinhte.vn không được dùng.
- Mọi tin được che PII ngay khi lấy (`<PHONE>`, `<URL>`…); tin có thể chứa tên người bị bỏ luôn, không lưu.
- `reviewed.csv` chỉ lưu mã băm của tin đã duyệt để không thu thập lại.

```bash
python scripts/collect_training_candidates.py --dry-run          # chạy thử (cần mạng tới các trang Việt Nam)
# trong Claude Code: "dùng agent training-data-labeler gán nhãn ứng viên"
# (tuỳ chọn) "dùng agent synthetic-message-writer sinh 100 tin normal và 100 tin spam"
python scripts/review_candidates.py --annotator ky                # Enter = đồng ý, s/p/n = đổi nhãn, x = loại, q = lưu và thoát
python scripts/validate_dataset.py data/processed/dataset.csv     # kiểm tra đủ, kể cả cân bằng nhãn, trước khi train
```

Workflow `training-data.yml` chạy mỗi thứ Hai (7:41 giờ Việt Nam): thu thập tối đa 300 ứng viên rồi mở PR `training-data/candidates`. Merge PR đó chỉ thêm ứng viên vào hàng chờ, chưa thêm gì vào dataset. Khi đang gom dữ liệu, CI chạy `validate_dataset.py --collecting`: lỗi từng dòng vẫn làm CI đỏ, còn phân bố nhãn chưa cân bằng chỉ là cảnh báo.

## Huấn luyện model

Mở `notebooks/train_phobert_colab.ipynb` trên Google Colab (GPU), hoặc chạy trực tiếp trên máy có GPU:

```bash
pip install -r requirements-train.txt
python scripts/train_phobert.py data/processed/splits models/
dotnet run scripts/VerifyTokenizer.cs -- models   # tokenizer .NET phải khớp HuggingFace
```

Script in macro F1, F1 scam trên tập test và dòng để ghi vào `docs/experiments.md`; trả lỗi nếu logits ONNX lệch PyTorch quá `1e-3`.

## Chạy API ở máy

API chọn model theo thứ tự (quyết định 024):

1. **PhoBERT ONNX** khi có đủ 3 file trong `models/` (mục `OnnxModel` trong `appsettings.json`):

   | File | Nguồn |
   |---|---|
   | `models/scam-detector.onnx` | Export từ notebook huấn luyện |
   | `models/vocab.txt` | `vocab.txt` của tokenizer PhoBERT đã dùng khi train |
   | `models/bpe.codes` | `bpe.codes` của tokenizer PhoBERT đã dùng khi train |

2. **LLM tự chạy qua [Ollama](https://ollama.com)** (mặc định `qwen2.5:3b`, mục `LlmModel`) khi chưa có PhoBERT. Cài một lần:

   ```bash
   # Windows: tải bộ cài ở https://ollama.com/download ; macOS/Linux theo hướng dẫn cùng trang
   ollama pull qwen2.5:3b     # khoảng 2 GB, cần thêm ~3 GB RAM khi chạy
   ```

```bash
dotnet run --project src/ScamDetector.Api
```

API nghe ở `http://localhost:8080`, cùng cổng với Docker và với địa chỉ mặc định của extension, nên nạp extension là dùng được ngay. `dotnet run` tự trỏ `LlmModel:BaseUrl` tới `http://localhost:11434` (xem `launchSettings.json`); để tắt LLM, đặt biến môi trường `LlmModel__BaseUrl=` rỗng.

Tin chứa tên miền trong danh sách chặn hoặc trùng văn mẫu luôn được kết luận lừa đảo ngay, không gọi model. Khi không có PhoBERT và Ollama chưa chạy (hoặc chưa `pull` model), API vẫn khởi động: `/health` trả `503 Unhealthy` và các tin còn lại trả `503` dạng ProblemDetails.

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
curl -X POST http://localhost:8080/api/v1/classifications \
  -H 'content-type: application/json' \
  -d '{"text":"Tai khoan cua quy khach bi khoa, xac minh tai bit.ly/abc"}'
```

```json
{ "label": "scam", "confidence": 0.93, "reasons": ["model_predicted_scam", "url_shortener"] }
```

| Trường | Ý nghĩa |
|---|---|
| `label` | `scam`, `spam` hoặc `normal`. Kết luận `scam` khi xác suất ≥ `Classification:ScamThreshold` (mặc định 0.7), hoặc khi tin có link thuộc danh sách tên miền lừa đảo, hoặc trùng văn mẫu lừa đảo đã biết (xem [Cập nhật dữ liệu lừa đảo](#cập-nhật-dữ-liệu-lừa-đảo)) |
| `confidence` | Xác suất của nhãn được chọn (khi trùng danh sách chặn: 0.99; khi trùng văn mẫu: tỉ lệ trùng nếu cao hơn xác suất model) |
| `reasons` | `model_predicted_scam`, `model_predicted_spam`, `scam_probability_below_threshold`, `url_shortener`, `url_ip_address_host`, `url_punycode`, `url_suspicious_tld`, `url_blocklisted`, `matches_known_scam_template` |

Lỗi trả về theo RFC 9457 (`application/problem+json`):

| Mã | Khi nào | `code` |
|---|---|---|
| 400 | Text rỗng | `classification.empty_text` |
| 400 | Text dài hơn 2000 ký tự | `classification.text_too_long` |
| 400 | JSON sai | — |
| 503 | Không có PhoBERT, LLM chưa sẵn sàng, và tin không trùng danh sách chặn hay văn mẫu | — |

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

`200 Healthy` khi PhoBERT đã nạp, hoặc khi đang dùng LLM và Ollama trả lời được với model đã `pull`; ngược lại `503 Unhealthy`.

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

## Cập nhật dữ liệu lừa đảo

Hệ thống tự cập nhật hai loại dữ liệu từ nguồn công khai, không cần train lại model:

| File | Nội dung | Dùng ở đâu |
|---|---|---|
| `data/threat-intel/blocked_domains.csv` | Tên miền lừa đảo đã được cảnh báo, kèm nguồn và đoạn trích làm bằng chứng | Tin có link tới tên miền này (hoặc subdomain) → `scam`, lý do `url_blocklisted` |
| `data/threat-intel/scam_templates.csv` | Văn mẫu lừa đảo đã ẩn danh (`<URL>`, `<PHONE>`...) | Tin trùng ≥ 60% cụm 3 từ của một văn mẫu → `scam`, lý do `matches_known_scam_template` |

Hai luồng tách biệt (quyết định 019):

1. **Danh sách chặn và văn mẫu**: workflow `threat-intel.yml` chạy mỗi ngày, crawl nguồn trong `data/threat-intel/sources.json`, chạy luật cứng `validate_threat_intel.py`, rồi mở PR `threat-intel/daily` có báo cáo. Merge PR là API/bot tự nạp lại dữ liệu (kiểm tra thay đổi mỗi `ThreatIntel:ReloadCheckSeconds` giây), không cần khởi động lại.
2. **Dữ liệu train model**: không bao giờ tự động. Văn mẫu chỉ là gợi ý; muốn đưa vào `dataset.csv` phải gán nhãn tay theo `docs/data-schema.md`.

Nguồn: cơ quan chức năng (danh sách website lừa đảo và bài cảnh báo của tinnhiemmang.vn, bài cảnh báo của Bộ Công an), báo chí (RSS Thanh Niên, VietNamNet, Tuổi Trẻ, Dân trí) và **mạng xã hội chỉ qua nhập tay**: dán tin vào `data/threat-intel/manual_submissions.csv` (cột `text,source_url,note`). Không tự động crawl Facebook/Zalo. Crawler tuân thủ `robots.txt`, chờ giữa các request, user agent `ScambodiaDetectorBot/1.0`.

```bash
python scripts/crawl_threat_intel.py --dry-run --report report.md   # chạy thử, không ghi file
python scripts/crawl_threat_intel.py --only vnexpress-phap-luat     # một nguồn
python scripts/validate_threat_intel.py                              # luật cứng
python scripts/probe_sources.py https://bao.vn/rss                   # kiểm tra URL trước khi thêm nguồn
python scripts/reject_threat_intel.py --domain abc.xyz --reason "Trang chính thức, bị trích nhầm"
python scripts/reject_threat_intel.py --template tpl_000012 --reason "Lời khuyên của công an, không phải tin lừa"
```

Loại mục sai bằng `reject_threat_intel.py`, đừng xóa tay: mục bị loại được ghi vào `rejected_*.csv` (văn mẫu chỉ lưu dấu vân tay SHA-256, không lưu nội dung) để lần crawl sau không thêm lại.

**Agent duyệt** `threat-intel-reviewer` (`.claude/agents/`, quyết định 020) duyệt PR cập nhật với ràng buộc cứng: không truy cập mạng, không mở link trong dữ liệu, chỉ được loại (không thêm, không sửa), chỉ xem dòng mới so với `origin/main`, không commit/push/merge. Agent xếp từng mục vào CHẤP NHẬN / LOẠI / CẦN NGƯỜI XEM theo tiêu chí ghi trong file agent, rồi báo cáo; người vẫn là người merge. Cách dùng: checkout nhánh `threat-intel/daily`, trong Claude Code gõ "dùng agent threat-intel-reviewer duyệt nhánh này".

Cấu hình (`appsettings.json`, biến môi trường `ThreatIntel__*`): `DataDirectory` (mặc định `../../data/threat-intel`, trong Docker là `/threat-intel`), `MinimumTemplateCoverage` (0.6), `ReloadCheckSeconds` (60).

Thiết lập GitHub một lần: Settings → Actions → General → bật *Allow GitHub Actions to create and approve pull requests*. PR tạo bằng `GITHUB_TOKEN` không kích hoạt CI; muốn CI chạy trên PR cập nhật, tạo fine-grained token (quyền Contents và Pull requests: write) và lưu vào secret `THREAT_INTEL_TOKEN`. Với PR sửa crawler hoặc `sources.json`, workflow chạy thử toàn bộ nguồn và đưa báo cáo vào Job summary để kiểm tra URL và parser còn đúng. Chạy tay workflow (tab Actions → Threat intel → Run workflow) có ba chế độ: `update` (crawl và mở PR), `dry-run` (chỉ báo cáo), `probe` (kiểm tra danh sách URL ứng viên, liệt kê link feed và link bài viết; dùng khi thêm hoặc sửa nguồn vì nhiều trang Việt Nam chỉ truy cập được từ runner GitHub).

## Agent và skill trong Claude Code

Định nghĩa trong `.claude/agents/` và `.claude/skills/`. Agent con không gọi được agent khác, nên chuỗi nhiều bước do skill điều phối.

| Agent | Việc | Sửa file? |
|---|---|---|
| `automation-ideator` | Đề xuất tự động hóa mới (mã A1…) để bạn duyệt | Không |
| `qol-advisor` | Đề xuất cải thiện quality of life cho người dùng và dev (mã Q1…) | Không |
| `code-polisher` | Đánh bóng code, giữ nguyên hành vi, chạy test | Có |
| `performance-optimizer` | Đo, tối ưu điểm nóng, chỉ giữ thay đổi nhanh hơn ≥ 10% và không đổi kết quả | Có |
| `convention-reviewer` | Review diff theo `docs/conventions.md` | Không |
| `test-writer` | Viết test xUnit cho Core | Có |
| `dataset-auditor` | Kiểm tra chất lượng `dataset.csv` | Không |
| `threat-intel-reviewer` | Duyệt PR cập nhật danh sách chặn | Chỉ loại mục |
| `training-data-labeler` | Gợi ý nhãn cho ứng viên dữ liệu huấn luyện | Chỉ ghi gợi ý |
| `synthetic-message-writer` | Sinh tin synthetic khi bạn yêu cầu | Chỉ thêm ứng viên |

| Skill | Luồng |
|---|---|
| `/de-xuat` | `automation-ideator` + `qol-advisor` chạy song song → danh sách gộp → bạn trả lời "làm A1, Q3" → triển khai → `/polish` |
| `/polish [file]` | `code-polisher` → `convention-reviewer` → sửa mục "Phải sửa" (tối đa 2 vòng) → test → hỏi commit |
| `/tinh-nang <mô tả>` | Kế hoạch ngắn → code + test → `test-writer` nếu đụng Core → `/polish` → kiểm tra đầy đủ → commit |
| `/du-lieu` | `training-data-labeler` gợi ý nhãn → (synthetic khi bạn yêu cầu) → bạn duyệt → `dataset-auditor` → commit |

Quy trình đầy đủ (luồng, nhịp định kỳ, ai quyết gì): [`docs/quy-trinh-agents.md`](docs/quy-trinh-agents.md). Không agent nào merge; bạn luôn là người quyết.

Hook `PreToolUse` trong `.claude/settings.json` (script `.claude/hooks/block_sensitive_paths.py`) chặn công cụ của Claude Code đọc, ghi hay chạy lệnh có đường dẫn viết liền tới thư mục dữ liệu gốc hoặc file `.env` (cho phép `.env.example`). Hook so khớp chuỗi nên không bắt được cách viết vòng (ví dụ `cd` vào thư mục cha rồi đọc tiếp); đó là lưới an toàn, không thay cho quy tắc 4 của CLAUDE.md. Lệnh `anonymize.py`, `export_reports.py` trên dữ liệu gốc bạn tự chạy trong terminal. Tắt tạm bằng menu `/hooks`.

## Database

SQL Server qua EF Core, bảng `MessageReports`. Cấu hình bằng `ConnectionStrings:ScamDetector`; để trống thì API vẫn chạy, chỉ `/api/v1/reports` trả 503.

```bash
dotnet tool restore
dotnet ef migrations add <TenMigration> --project src/ScamDetector.Infrastructure --output-dir Persistence/Migrations
dotnet ef database update --project src/ScamDetector.Infrastructure --connection "<connection string>"
```

Đặt `Database:ApplyMigrationsOnStartup=true` để API tự chạy migration khi khởi động (compose đã bật). Test `MigrationTests` báo lỗi nếu đổi model mà quên tạo migration.

## Deploy bằng Docker

**Deploy miễn phí lên Internet:** làm theo [`docs/deploy-oracle.md`](docs/deploy-oracle.md) (Oracle Cloud Always Free, HTTPS tự động qua sslip.io + Caddy, tự cập nhật dữ liệu mỗi 15 phút). Cài bằng một lệnh: `sudo deploy/oracle/scambodia.sh install`.

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
- Danh sách chặn và văn mẫu được chép vào image tại `/threat-intel`. Compose mount `./data/threat-intel` vào đó, nên `git pull` là container tự nạp dữ liệu mới.
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

`.github/workflows/ci.yml` chạy build và test .NET, chạy migration trên SQL Server thật, ruff và test Python, validate dataset (khi có mẫu), test và đóng gói Chrome extension, build Docker image cho Api và Bot rồi smoke test container, chạy API với Ollama thật (model nhỏ `qwen2.5:0.5b`) để kiểm tra hợp đồng gọi LLM, và chạy `validate_threat_intel.py`. `.github/workflows/threat-intel.yml` crawl hằng ngày và mở PR cập nhật dữ liệu lừa đảo.
