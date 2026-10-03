# Cấu trúc dữ liệu

## 1. Luồng dữ liệu

```
data/training-candidates/  Ứng viên do crawler thu thập (đã che PII), agent gợi ý nhãn,
   │  scripts/review_candidates.py → người duyệt → thẳng vào dataset.csv (quyết định 021)
   ▼
MessageReports (DB)  Báo cáo người dùng, đã che PII khi lưu.
   │  scripts/export_reports.py → duyệt tay → vào data/raw/
   ▼
data/raw/            Dữ liệu gốc, có thể chứa thông tin cá nhân. KHÔNG commit.
   │  scripts/anonymize.py
   ▼
data/processed/dataset.csv     Đã ẩn danh, đã kiểm tra. Được commit.
   │  scripts/split_dataset.py
   ▼
data/processed/splits/{train,val,test}.csv
```

## 2. File `dataset.csv`

- Mã hóa **UTF-8 không BOM**, phân cách dấu phẩy, có header, chuẩn hóa Unicode **NFC**.
- Mỗi dòng là một tin nhắn.

| Cột | Kiểu | Bắt buộc | Ràng buộc |
|---|---|---|---|
| `id` | string | Có | Dạng `msg_000001`, duy nhất |
| `text` | string | Có | 5–2000 ký tự, đã ẩn danh |
| `label` | enum | Có | `scam`, `spam`, `normal` |
| `scam_type` | enum | Khi `label = scam` | Xem mục 4; để trống nếu không phải scam |
| `channel` | enum | Có | `sms`, `zalo`, `messenger`, `telegram`, `email`, `other` |
| `source` | enum | Có | `collected`, `public_post`, `contributed`, `synthetic` |
| `has_url` | bool | Có | `true` / `false`, tính tự động từ `text` |
| `collected_at` | date | Có | `YYYY-MM-DD` |
| `annotator` | string | Có | Mã người gán nhãn, ví dụ `ky` |

Ví dụ:
```csv
id,text,label,scam_type,channel,source,has_url,collected_at,annotator
msg_000001,"[BANK] Tai khoan cua quy khach bi tam khoa. Xac minh tai <URL>",scam,bank_impersonation,sms,collected,true,2026-10-03,ky
msg_000002,"Mai 7h họp nhóm đồ án ở thư viện nha",normal,,zalo,collected,false,2026-10-03,ky
```

## 3. Nhãn chính (`label`)

| Nhãn | Định nghĩa | Không phải |
|---|---|---|
| `scam` | Cố lấy tiền, tài khoản, OTP, thông tin cá nhân bằng lừa dối | Quảng cáo thật dù phiền |
| `spam` | Quảng cáo, tiếp thị hàng loạt, không lừa dối | Tin giả danh thương hiệu để lừa |
| `normal` | Tin nhắn cá nhân, công việc, thông báo hợp lệ | — |

**Quy tắc khi phân vân:** có yêu cầu chuyển tiền / OTP / bấm link lạ / cung cấp thông tin → ưu tiên `scam`.

## 4. Loại lừa đảo (`scam_type`)

| Giá trị | Mô tả |
|---|---|
| `bank_impersonation` | Giả ngân hàng, ví điện tử, yêu cầu xác minh tài khoản |
| `authority_impersonation` | Giả công an, tòa án, thuế, điện lực, nhà mạng |
| `job_scam` | Việc nhẹ lương cao, cộng tác viên, làm nhiệm vụ |
| `prize_scam` | Trúng thưởng, quà tặng, tri ân khách hàng |
| `investment_scam` | Đầu tư, tiền ảo, chứng khoán lợi nhuận cao |
| `loan_scam` | Cho vay nhanh, giải ngân yêu cầu đóng phí |
| `delivery_scam` | Giả shipper, đơn hàng, phí giao hàng |
| `relative_impersonation` | Giả người quen, người thân cần tiền gấp |
| `account_takeover` | Xin OTP, mã xác nhận, mượn tài khoản |
| `other` | Kiểu khác, ghi chú thêm trong `docs/` |

## 5. Ẩn danh hóa (bắt buộc trước khi vào `processed/`)

| Thông tin | Thay bằng |
|---|---|
| Số điện thoại | `<PHONE>` |
| Số tài khoản, số thẻ | `<ACCOUNT>` |
| Tên người thật | `<NAME>` |
| Email | `<EMAIL>` |
| URL | `<URL>` (giữ tên miền gốc trong file riêng không commit nếu cần phân tích) |
| CCCD/CMND | `<ID>` |
| Mã OTP | `<OTP>` |

Không thay tên ngân hàng, thương hiệu bị giả danh: đó là tín hiệu quan trọng cho model.

## 6. Ràng buộc chất lượng

1. Không trùng `text` sau khi chuẩn hóa (bỏ khoảng trắng thừa, viết thường).
2. Mỗi nhãn chiếm tối thiểu 15% tổng số mẫu.
3. `synthetic` không vượt quá 20% dataset và **không xuất hiện trong tập test**.
4. Chia tập theo tỉ lệ 70/15/15, phân tầng theo `label`, `SEED = 42`.
5. Tập test cố định sau khi tạo; chỉ thêm mẫu mới vào train/val.
6. Chạy `scripts/validate_dataset.py` trước mỗi commit dữ liệu (thêm `--collecting` khi đang gom dữ liệu: chỉ cảnh báo phân bố nhãn). Trước khi train phải đạt bản đầy đủ.

## 7. Giới hạn ở tầng API

| Ràng buộc | Giá trị |
|---|---|
| Độ dài tin nhắn đầu vào | 1–2000 ký tự |
| Số token tối đa đưa vào PhoBERT | 256 |
| Ngưỡng `confidence` để kết luận `scam` | 0.7 (cấu hình trong `appsettings.json`) |

## 8. Dữ liệu threat-intel (`data/threat-intel/`)

Không phải dữ liệu train. Dùng trực tiếp ở API (quyết định 019). Mọi file UTF-8, không BOM, `\n`.

| File | Cột | Ghi chú |
|---|---|---|
| `blocked_domains.csv` | `domain,source,source_url,first_seen,evidence` | `domain` chữ thường, IDNA (punycode), không `www.`; `evidence` là đoạn trích ≤ 240 ký tự đã ẩn danh |
| `scam_templates.csv` | `id,text,source,source_url,first_seen` | `id` dạng `tpl_000001`; `text` đã ẩn danh như mục 5, NFC |
| `allowed_domains.csv` | `domain,note` | Tên miền hợp pháp, không bao giờ bị chặn (gồm subdomain) |
| `rejected_domains.csv` | `domain,reason,rejected_at` | Ghi bởi `reject_threat_intel.py` |
| `rejected_templates.csv` | `fingerprint,reason,rejected_at` | Dấu vân tay, không lưu nội dung |
| `manual_submissions.csv` | `text,source_url,note` | Tin nhập tay từ mạng xã hội; crawler ẩn danh trước khi thành văn mẫu |
| `sources.json` | `name,kind,category,url,...` | `kind`: `rss`, `html_list`, `domain_list`, `domain_page` (trang liệt kê tên miền lừa đảo), `manual`; URL phải `https` |

Kiểm tra bằng `python scripts/validate_threat_intel.py`.

## 9. Ứng viên dữ liệu huấn luyện (`data/training-candidates/`)

| File | Cột | Ghi chú |
|---|---|---|
| `candidates.csv` | `id,text,origin,source,source_url,collected_at,hint_label,hint_channel,suggested_label,suggested_scam_type,suggested_channel,confidence,note` | `id` dạng `cand_000001`; `text` đã che PII; `origin`: `news`, `forum`, `public_dataset`, `manual`, `synthetic`; `suggested_label` thêm giá trị `exclude` |
| `reviewed.csv` | `key,decision,decided_at` | `key` = 16 ký tự đầu SHA-256 của text chuẩn hóa; không lưu nội dung |
| `manual_messages.csv` | `text,label,channel,source_url,note` | Tin nhập tay, `label` có thể để trống |
| `sources.json` | `name,kind,url,origin,...` | `kind`: `rss`, `html_list`, `csv_dataset` (bắt buộc `license`), `manual` |

Kiểm tra bằng `python scripts/validate_training_candidates.py`.

