# CLAUDE.md

Hướng dẫn cho Claude (và người cộng tác) khi làm việc trong repo này. Đọc file này trước mọi thay đổi.

## Dự án

Hệ thống phát hiện tin nhắn lừa đảo tiếng Việt.
- Model: PhoBERT fine-tune (Python) → export ONNX
- Backend: ASP.NET Core (.NET 10) chạy model bằng ONNX Runtime
- Database: SQL Server (EF Core)
- Kênh: Web API, Telegram bot, Chrome extension

## Quy tắc bắt buộc

1. Tuân thủ `docs/conventions.md` (code) và `docs/data-schema.md` (dữ liệu).
2. Không viết comment trong code. Tên biến, hàm, lớp phải tự giải thích. Giải thích thiết kế đặt trong `docs/`.
3. Không bịa dữ liệu huấn luyện. Chỉ được sinh dữ liệu khi được yêu cầu rõ, và phải gắn `source = synthetic`.
4. Không đọc, in ra hay commit nội dung trong `data/raw/`.
5. Mọi thay đổi ở `ScamDetector.Core` phải có unit test đi kèm.
6. Không thêm package NuGet/pip mới khi chưa giải thích lý do.
7. Tài liệu viết bằng tiếng Việt; tên trong code viết bằng tiếng Anh.

## Lệnh thường dùng

```bash
dotnet build
dotnet test
dotnet run --project src/ScamDetector.Api
python scripts/validate_dataset.py data/processed/dataset.csv
```

## Cấu trúc

```
src/ScamDetector.Core           Domain, interface, logic thuần, không phụ thuộc hạ tầng
src/ScamDetector.Infrastructure ONNX Runtime, EF Core, gọi API ngoài
src/ScamDetector.Api            ASP.NET Core Web API
src/ScamDetector.Bot            Telegram bot
tests/                          Unit test và integration test
notebooks/                      Huấn luyện model trên Colab
scripts/                        Script xử lý, kiểm tra dữ liệu
data/                           Dữ liệu (xem docs/data-schema.md)
docs/                           Tài liệu thiết kế và quyết định
```
