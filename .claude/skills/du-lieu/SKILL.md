---
name: du-lieu
description: Luồng dữ liệu huấn luyện hằng tuần theo docs/quy-trinh-agents.md mục 3.5 - gợi ý nhãn cho ứng viên mới bằng training-data-labeler, (tuỳ chọn) sinh synthetic khi người dùng yêu cầu, rồi hướng dẫn người dùng duyệt và chạy dataset-auditor. Dùng khi người dùng gõ /du-lieu hoặc sau khi merge PR training-data/candidates.
---

# /du-lieu

Không bước nào ghi vào `data/processed/dataset.csv` thay người dùng.

1. **Tình trạng.** Chạy `python3 scripts/label_candidates.py --list --limit 1` (số chưa gán) và `python3 scripts/validate_dataset.py --collecting` (phân bố nhãn hiện tại). Báo ngắn: bao nhiêu ứng viên chờ, nhãn nào đang thiếu.
2. **Gợi ý nhãn.** Có ứng viên chưa gán → gọi agent `training-data-labeler` (tối đa 200 mẫu một lần). Đọc báo cáo của nó.
3. **Synthetic (chỉ khi người dùng yêu cầu rõ trong lời gọi hoặc trả lời).** Thiếu `normal`/`spam` thì hỏi người dùng có muốn sinh không và bao nhiêu; đồng ý mới gọi `synthetic-message-writer`.
4. **Hướng dẫn duyệt.** Nhắc người dùng chạy `python scripts/review_candidates.py --annotator <mã>` (Enter đồng ý, s/p/n đổi nhãn, x loại, q lưu và thoát). Đây là bước của người dùng, phiên chính không tự duyệt.
5. **Sau khi người dùng duyệt xong:** gọi agent `dataset-auditor`, chạy `python3 scripts/validate_dataset.py --collecting`, báo kết quả; nếu sạch thì commit `data/processed/dataset.csv`, `data/training-candidates/` với message nêu số mẫu theo nhãn.
6. **Sẵn sàng train?** Chạy `python3 scripts/validate_dataset.py` (bản đầy đủ). Đạt → nói người dùng có thể chia tập và train; chưa đạt → nói còn thiếu gì.
