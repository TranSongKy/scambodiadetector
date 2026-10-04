# Quy trình làm việc với agent

Tài liệu này quy định **khi nào dùng agent nào, theo thứ tự nào, ai quyết định**. Mọi phiên Claude Code trong repo phải theo quy trình này (CLAUDE.md trỏ tới đây).

## 1. Nguyên tắc chung

1. **Người dùng là người quyết.** Agent đề xuất, sửa code, gợi ý nhãn; chỉ người dùng merge PR, chọn đề xuất, duyệt dữ liệu vào `dataset.csv`. Không agent nào được merge, và commit/push chỉ do phiên chính làm trên nhánh làm việc.
2. **Agent con không gọi được agent khác.** Phiên chính điều phối chuỗi (`/polish`, `/de-xuat`, `/tinh-nang`, `/du-lieu`). Muốn nối bước mới, viết skill, đừng bảo agent "gọi tiếp agent X".
3. **Đề xuất trước, làm sau.** Việc có thể đổi hướng dự án (kiến trúc, cấu hình, dữ liệu, thêm package) đi qua bước đề xuất có mã (A1, Q3, P2…) để người dùng chọn.
4. **Mọi thay đổi code kết thúc bằng `/polish`.** Sau đó là kiểm tra đầy đủ, rồi commit.
5. **Chạy song song khi độc lập.** Agent chỉ đọc (ideator, qol-advisor, reviewer, auditor) chạy song song được. Agent sửa file chạy lần lượt, không hai agent cùng sửa một vùng code.
6. **Giới hạn vòng lặp.** Vòng sửa sau review tối đa 2 lần; quá thì dừng và báo người dùng.
7. **Luật cứng có hook bảo vệ.** Hook `PreToolUse` chặn công cụ chạm thư mục dữ liệu gốc và file bí mật; không agent nào được lách (đổi cách viết đường dẫn, gọi qua script trung gian).

## 2. Bản đồ agent

| Agent | Vai trò | Sửa file | Dùng khi |
|---|---|---|---|
| `automation-ideator` | Đề xuất tự động hóa (mã A) | Không | `/de-xuat`, sau mỗi đợt tính năng lớn |
| `qol-advisor` | Đề xuất trải nghiệm cho người dùng và dev (mã Q) | Không | `/de-xuat` |
| `performance-optimizer` | Đo và tối ưu (mã P khi chỉ đo) | Có | Khi chậm, hoặc `/de-xuat` có nhắc hiệu năng |
| `code-polisher` | Đánh bóng code, giữ hành vi | Có | Trong `/polish` |
| `convention-reviewer` | Review diff theo `docs/conventions.md` | Không | Trong `/polish`, trước mọi commit code |
| `test-writer` | Viết test xUnit cho Core | Có | Mọi thay đổi ở `ScamDetector.Core` (CLAUDE.md quy tắc 5) |
| `threat-intel-reviewer` | Duyệt PR danh sách chặn hằng ngày | Chỉ loại mục | PR `threat-intel/daily` |
| `training-data-labeler` | Gợi ý nhãn cho ứng viên dữ liệu | Chỉ ghi gợi ý | Sau khi merge PR `training-data/candidates` |
| `synthetic-message-writer` | Sinh tin synthetic | Chỉ thêm ứng viên | **Chỉ khi người dùng yêu cầu rõ** |
| `dataset-auditor` | Kiểm tra chất lượng `dataset.csv` | Không | Trước khi commit dữ liệu, trước khi train |

## 3. Các luồng

### 3.1. Tính năng hoặc sửa lỗi (`/tinh-nang`)

```
yêu cầu ─► phiên chính đọc code, nêu kế hoạch ngắn (hỏi nếu có quyết định thật)
        ─► viết code + test
        ─► test-writer            (nếu đụng ScamDetector.Core)
        ─► /polish                (code-polisher ─► convention-reviewer ─► sửa "Phải sửa", ≤ 2 vòng)
        ─► scripts/check_all.sh   (hoặc các lệnh tương ứng nếu thiếu công cụ)
        ─► commit + push nhánh làm việc
        ─► PR khi người dùng yêu cầu ─► CI xanh ─► người dùng merge
```

Thêm ở bước kế hoạch khi:
- Đụng che PII → sửa cả `masking.py` và `ModelInputMasker.cs`, thêm case vào `tests/shared/masking-cases.json`.
- Đụng so khớp văn mẫu → sửa cả `similarity.py` và `TemplateShingles`.
- Thêm package → ghi lý do vào `docs/decisions.md` trước khi thêm (CLAUDE.md quy tắc 6).
- Đổi cấu hình triển khai, schema dữ liệu, mã lý do API → hỏi người dùng trước.

### 3.2. Cải tiến định kỳ (`/de-xuat`)

```
automation-ideator ┐
qol-advisor        ├─ song song ─► phiên chính gộp, bỏ trùng, xếp hạng ─► người dùng chọn mã
(performance-optimizer chỉ đo) ┘                                          ─► mỗi mã chạy luồng 3.1
```

Nhịp: sau mỗi đợt tính năng lớn, hoặc mỗi 2 tuần. Đề xuất không được chọn thì bỏ, không tự làm.

### 3.3. Hiệu năng

```
performance-optimizer (đo trước, sửa ≤ 3 điểm nóng, đo lại, chỉ giữ thay đổi nhanh hơn ≥ 10%)
─► /polish ─► check_all ─► commit
```

Thay đổi đụng cấu hình (số thread ONNX, rate limit, Docker) → agent chỉ đề xuất, người dùng quyết.

### 3.4. Danh sách chặn hằng ngày

```
6:17 sáng: workflow threat-intel mở PR threat-intel/daily
─► (tuỳ chọn) checkout nhánh, "dùng agent threat-intel-reviewer duyệt nhánh này"
─► agent loại mục sai bằng reject_threat_intel.py, xếp CẦN NGƯỜI XEM khi không chắc
─► người dùng xem báo cáo, merge PR ─► API/bot tự nạp lại
```

Nguồn lỗi nhiều lần → chạy workflow chế độ `probe` để kiểm tra URL, sửa `sources.json` theo luồng 3.1.

### 3.5. Dữ liệu huấn luyện (`/du-lieu`)

```
thứ Hai: workflow training-data mở PR training-data/candidates ─► người dùng merge
─► training-data-labeler gợi ý nhãn (≤ 200 mẫu một lần)
─► nếu thiếu normal/spam VÀ người dùng yêu cầu: synthetic-message-writer (trần 20%)
─► người dùng: python scripts/review_candidates.py --annotator <mã>
─► dataset-auditor ─► python scripts/validate_dataset.py --collecting
─► commit dataset.csv
```

Trước khi train: `validate_dataset.py` bản đầy đủ (cân bằng nhãn ≥ 15%, synthetic ≤ 20%) phải đạt, `dataset-auditor` không còn lỗi, rồi `split_dataset.py` và notebook.

### 3.6. PR do Claude mở

Theo dõi CI và review; CI đỏ thì sửa và push; review "Phải sửa" thì sửa; mục tuỳ chọn thì trả lời. Chỉ khi CI xanh, không xung đột, mới báo người dùng merge.

## 4. Nhịp định kỳ

| Khi nào | Việc | Ai làm |
|---|---|---|
| Hằng ngày | PR danh sách chặn | Workflow → (agent duyệt) → người dùng merge |
| Thứ Hai | PR ứng viên dữ liệu → gợi ý nhãn → duyệt | Workflow → `/du-lieu` → người dùng duyệt |
| Mỗi thay đổi code | `/polish` + kiểm tra đầy đủ | Phiên chính |
| Sau đợt tính năng lớn / 2 tuần | `/de-xuat` | Phiên chính → người dùng chọn |
| Trước khi train | `dataset-auditor` + validate đầy đủ | Phiên chính → người dùng train |
| Khi thấy chậm | `performance-optimizer` | Phiên chính |

## 5. Báo cáo cho người dùng

- Tiếng Việt, ngắn, nói kết quả trước: đã làm gì, test ra sao (số liệu thật), còn gì chờ quyết.
- Không tóm tắt lời agent như sự thật khi chưa kiểm chứng; đề xuất của agent ghi rõ là đề xuất.
- Mục "Nên sửa" và mục agent không dám sửa luôn được liệt kê để người dùng chọn.
