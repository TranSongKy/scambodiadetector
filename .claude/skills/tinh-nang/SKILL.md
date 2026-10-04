---
name: tinh-nang
description: Luồng chuẩn để làm một tính năng hoặc sửa lỗi theo docs/quy-trinh-agents.md mục 3.1 - kế hoạch ngắn, code và test, test-writer cho Core, /polish, kiểm tra đầy đủ, commit. Dùng khi người dùng gõ /tinh-nang <mô tả>, hoặc khi bắt đầu một thay đổi code không nhỏ.
---

# /tinh-nang

Làm đúng thứ tự. Đối số là mô tả việc cần làm.

1. **Hiểu và lên kế hoạch.** Đọc code liên quan. Nêu kế hoạch 3–6 dòng: file sẽ đổi, test sẽ thêm, rủi ro. Kiểm tra các điều kiện đặc biệt ở `docs/quy-trinh-agents.md` mục 3.1 (che PII hai ngôn ngữ, so khớp văn mẫu hai ngôn ngữ, package mới, cấu hình/schema/mã lý do). Chỉ hỏi người dùng khi có quyết định thật sự của họ; còn lại tự quyết và nói rõ giả định.
2. **Viết code và test** trong phiên chính, theo `docs/conventions.md`, không comment.
3. **Core có đổi?** Gọi agent `test-writer` cho các class Core vừa đổi chưa có test đủ.
4. **Gọi skill `/polish`** trên diff.
5. **Kiểm tra đầy đủ:** `./scripts/check_all.sh`; nếu thiếu công cụ thì chạy phần tương ứng (dotnet, ruff + unittest, `npm test`). Đỏ → sửa, không bỏ qua test.
6. **Commit và push** lên nhánh làm việc với message rõ ràng. Không mở PR, không merge trừ khi người dùng yêu cầu.
7. **Báo cáo:** đã đổi gì, test (số liệu thật), mục "Nên sửa" chờ quyết, đề nghị bước tiếp (mở PR?).
