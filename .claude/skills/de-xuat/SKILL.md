---
name: de-xuat
description: Thu thập đề xuất cải tiến để người dùng duyệt: chạy song song agent automation-ideator (tự động hóa), qol-advisor (quality of life) và, nếu người dùng muốn, performance-optimizer ở chế độ chỉ đo. Dùng khi người dùng gõ /de-xuat, hoặc hỏi "nên cải thiện gì tiếp", "có gì tự động hóa được".
---

# /de-xuat

Mục tiêu: một danh sách đề xuất gọn để người dùng chọn, **không sửa code** ở bước này.

1. Gọi song song (một tin nhắn, nhiều lời gọi Agent) `automation-ideator` và `qol-advisor`. Nếu người dùng nhắc đến tốc độ/hiệu năng, gọi thêm `performance-optimizer` với yêu cầu rõ: "chỉ đo và đề xuất, không sửa file".
2. Gộp kết quả thành một danh sách, giữ mã đề xuất (A1…, Q1…, P1…), bỏ đề xuất trùng nhau giữa các agent, xếp theo tác động chia chi phí. Mỗi mục một đến ba dòng, giữ bằng chứng file:dòng.
3. Kết thúc bằng: "Trả lời mã đề xuất muốn làm, ví dụ: làm A1, Q3".
4. Khi người dùng chọn: phiên chính triển khai từng mục đã chọn (hoặc gọi `performance-optimizer`/`code-polisher` cho mục thuộc loại đó), chạy kiểm tra, rồi chạy `/polish` trên diff trước khi đề nghị commit. Mục không được chọn thì bỏ, không tự làm.
