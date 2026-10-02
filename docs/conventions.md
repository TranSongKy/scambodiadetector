# Quy ước code

## 1. Kiến trúc

Clean Architecture, phụ thuộc chỉ đi vào trong:

```
Api ──► Infrastructure ──► Core
Bot ──► Infrastructure ──► Core
```

| Project | Được chứa | Không được chứa |
|---|---|---|
| `Core` | Entity, enum, interface, logic thuần, Result | EF Core, ONNX, HttpClient, ASP.NET |
| `Infrastructure` | Triển khai interface của Core, DbContext, ONNX session | Controller, logic nghiệp vụ |
| `Api` | Endpoint, DTO request/response, DI, middleware | Truy vấn DB trực tiếp |
| `Bot` | Xử lý lệnh Telegram, gọi service của Core | Truy vấn DB trực tiếp |

## 2. C# (.NET 10)

### Đặt tên
| Loại | Quy tắc | Ví dụ |
|---|---|---|
| Class, record, enum, method, property | PascalCase | `MessageClassifier`, `ClassifyAsync` |
| Interface | `I` + PascalCase | `IScamClassifier` |
| Biến cục bộ, tham số | camelCase | `messageText` |
| Field private | `_camelCase` | `_session` |
| Hằng số | PascalCase | `MaxMessageLength` |
| Method bất đồng bộ | hậu tố `Async` | `SaveReportAsync` |
| Test | `Method_Condition_Expected` | `Classify_EmptyText_ReturnsFailure` |

### Quy tắc
1. File-scoped namespace, mỗi file một kiểu dữ liệu, tên file trùng tên kiểu.
2. Class mặc định `sealed`, chỉ bỏ khi thực sự cần kế thừa.
3. DTO và value object dùng `record`.
4. Bật `Nullable`, cảnh báo được coi là lỗi (đã cấu hình trong `Directory.Build.props`).
5. Mọi method async nhận `CancellationToken` ở tham số cuối.
6. Không dùng exception cho luồng nghiệp vụ, dùng `Result<T>`. Exception chỉ cho lỗi hệ thống.
7. Dùng primary constructor cho dependency injection.
8. Không có magic number/string, đưa vào hằng số hoặc `appsettings.json` (Options pattern).
9. Method tối đa ~30 dòng, class tối đa ~200 dòng. Vượt thì tách.
10. Không viết comment. Code khó hiểu thì đặt lại tên hoặc tách hàm.
11. Không `async void`, không `.Result`, không `.Wait()`.

### Mẫu
```csharp
namespace ScamDetector.Core.Classification;

public sealed class MessageClassifier(IScamModel model, IUrlInspector urlInspector) : IMessageClassifier
{
    public async Task<Result<ClassificationResult>> ClassifyAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Result.Failure<ClassificationResult>(ClassificationErrors.EmptyText);

        var normalizedText = TextNormalizer.Normalize(text);
        var prediction = await model.PredictAsync(normalizedText, cancellationToken);
        var urlFindings = await urlInspector.InspectAsync(normalizedText, cancellationToken);

        return ClassificationResult.From(prediction, urlFindings);
    }
}
```

## 3. API

1. Route dạng `/api/v1/{resource}` (danh từ số nhiều, kebab-case).
2. Lỗi trả về theo chuẩn `ProblemDetails` (RFC 9457).
3. Response phân loại luôn có: `label`, `confidence`, `reasons`.
4. Validate input ở biên (DTO), không validate lặp lại trong Core.
5. Giới hạn độ dài input theo `docs/data-schema.md`.

## 4. Database

1. Tên bảng số nhiều PascalCase (`Messages`, `UserReports`).
2. Khóa chính `Id` kiểu `Guid` (v7, sắp theo thời gian).
3. Mọi thay đổi schema đi qua EF Core migration, không sửa DB bằng tay.
4. Không lưu nội dung tin nhắn gốc chưa ẩn danh.

## 5. Python (notebooks, scripts)

1. Python 3.11+, format bằng `ruff format`, lint bằng `ruff check`.
2. `snake_case` cho hàm và biến, có type hint.
3. Mọi thí nghiệm cố định `SEED = 42`.
4. Notebook chỉ để thử nghiệm. Logic dùng lại đưa vào `scripts/`.
5. Ghi kết quả mỗi lần train vào `docs/experiments.md` (ngày, model, tham số, F1).

## 6. Git

### Nhánh
`main` (luôn chạy được) và `feat/...`, `fix/...`, `data/...`, `docs/...`.

### Commit (Conventional Commits)
```
<type>(<scope>): <mô tả ngắn, tiếng Anh, thì hiện tại>
```
| type | Khi nào |
|---|---|
| `feat` | Tính năng mới |
| `fix` | Sửa lỗi |
| `data` | Thêm/sửa dữ liệu, nhãn |
| `model` | Train, đổi model |
| `refactor` | Đổi cấu trúc, không đổi hành vi |
| `test` | Thêm/sửa test |
| `docs` | Tài liệu |
| `chore` | Cấu hình, CI, dependency |

Ví dụ: `feat(api): add classify endpoint`, `data: add 200 bank impersonation samples`.

### Trước khi commit
1. `dotnet build` không cảnh báo.
2. `dotnet test` pass.
3. Không có file trong `data/raw/` trong commit.
