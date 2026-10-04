---
name: polish
description: Đánh bóng code rồi review: chạy agent code-polisher, sau đó agent convention-reviewer trên diff, sửa các mục "Phải sửa" (tối đa 2 vòng), báo cáo. Dùng khi người dùng gõ /polish [file hoặc thư mục], hoặc nhờ "polish code", "dọn code rồi review".
---

# /polish

Agent con không gọi được agent khác, nên phiên chính điều phối theo đúng thứ tự dưới. Không commit, không push trừ khi người dùng yêu cầu.

1. **Chuẩn bị.** Ghi lại `git status --short`. Nếu đang có thay đổi chưa commit không liên quan đến yêu cầu, báo người dùng và hỏi có tiếp tục không (diff sẽ lẫn vào nhau).
2. **Polish.** Gọi agent `code-polisher`, truyền nguyên phạm vi người dùng nêu (đối số của `/polish`); không có thì để agent tự lấy diff so với `origin/main`.
3. **Review.** Gọi agent `convention-reviewer` (nó review `git diff HEAD`).
4. **Vòng sửa.** Có mục **Phải sửa**:
   - Gọi lại `code-polisher` với danh sách mục đó (file:dòng, vấn đề), chỉ sửa đúng các mục ấy.
   - Gọi lại `convention-reviewer`.
   - Tối đa 2 vòng. Sau 2 vòng vẫn còn → dừng, báo các mục còn lại.
   - Mục **Nên sửa**: không tự sửa, liệt kê để người dùng chọn.
5. **Kiểm tra cuối** trong phiên chính, theo loại file đã đổi: `dotnet build && dotnet test`; `ruff check . && ruff format --check . && python3 -m unittest discover -s scripts/tests -t .`; `cd extension && npm test`. Đỏ → báo đúng lỗi, không che.
6. **Báo cáo ngắn (tiếng Việt):** số file đổi, các thay đổi chính, kết quả review (đã sạch hay còn gì), kết quả test, mục "Nên sửa" và mục polisher không dám sửa đang chờ người dùng quyết. Hỏi người dùng có muốn commit không.
