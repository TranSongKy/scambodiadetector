# Deploy miễn phí trên Oracle Cloud

Kết quả cuối: API chạy ở `https://scambodia-<IP>.sslip.io` với model AI `qwen2.5:3b` (Ollama) kiểm tra mọi tin, bot Telegram chạy 24/7, dữ liệu lừa đảo tự cập nhật sau mỗi lần bạn merge PR. Chi phí: 0 đồng (quyết định 023).

```
GitHub Actions (miễn phí)                     Máy ảo Oracle (Always Free, ARM 2 nhân, 12 GB)
  crawl hằng ngày ─► PR ─► bạn merge ─► main ◄── cron 15 phút: scambodia.sh update
                                                  ├─ chỉ dữ liệu đổi → API tự nạp lại
                                                  └─ code đổi → build lại container
                                                Caddy (HTTPS tự động) ─► API ─► danh sách chặn + PhoBERT hoặc Ollama (LLM)
                                                Bot Telegram (long polling)
```

## Bước 1. Tạo tài khoản Oracle Cloud

1. Vào <https://www.oracle.com/cloud/free/>, bấm **Start for free**.
2. Chọn **Home Region** gần Việt Nam: Singapore, Tokyo hoặc Seoul. Không đổi được sau này, và máy Always Free chỉ tạo được ở Home Region.
3. Oracle cần thẻ Visa/Mastercard để xác minh (có thể giữ tạm một khoản nhỏ rồi hoàn lại). Tài khoản Free Tier không bị trừ tiền nếu chỉ dùng tài nguyên Always Free.

## Bước 2. Tạo máy ảo

**Compute → Instances → Create instance**:

| Mục | Chọn |
|---|---|
| Image | **Canonical Ubuntu 24.04** (bản aarch64) |
| Shape | **Ampere → VM.Standard.A1.Flex**, **2 OCPU, 12 GB RAM** (đúng hạn mức Always Free từ 15/06/2026) |
| Networking | Để mặc định, đánh dấu **Assign a public IPv4 address** |
| SSH keys | **Generate a key pair for me**, tải cả hai file key về máy |
| Boot volume | Mặc định 50 GB (Always Free cho tới 200 GB) |

Báo **Out of capacity**: máy ARM miễn phí hay hết chỗ. Thử Availability Domain khác, thử lại vào giờ khác, hoặc thử sau vài ngày.

Ghi lại **Public IP address** của máy sau khi tạo xong.

## Bước 3. Mở cổng 80 và 443 trên Oracle

**Networking → Virtual cloud networks →** VCN của máy **→ Subnet → Security List mặc định → Add Ingress Rules**, thêm 2 dòng:

| Source CIDR | IP Protocol | Destination Port |
|---|---|---|
| `0.0.0.0/0` | TCP | `80` |
| `0.0.0.0/0` | TCP | `443` |

(Tường lửa bên trong Ubuntu sẽ được script tự mở.)

## Bước 4. Cài đặt bằng một lệnh

Trên máy tính của bạn (Windows dùng PowerShell, macOS/Linux dùng Terminal):

```bash
ssh -i <đường-dẫn-file-private-key> ubuntu@<PUBLIC_IP>
```

Trên máy ảo:

```bash
git clone https://github.com/TranSongKy/scambodiadetector.git
cd scambodiadetector
sudo deploy/oracle/scambodia.sh install
```

Script sẽ: cài Docker, mở cổng trong Ubuntu, tạo swap 2 GB, tạo file cấu hình `deploy/oracle/.env` (quyền 600) với tên miền `scambodia-<IP-dạng-gạch-ngang>.sslip.io`, build và chạy API + Ollama + Caddy, tải model AI `qwen2.5:3b` (khoảng 2 GB), đặt lịch tự cập nhật 15 phút một lần. Lần đầu mất khoảng 10–20 phút. Cuối cùng in ra địa chỉ API.

Tên miền dùng [sslip.io](https://sslip.io): tên chứa sẵn IP nên không cần đăng ký tài khoản hay cấu hình DNS. Caddy tự xin chứng chỉ HTTPS (Let's Encrypt, nếu lỗi thì ZeroSSL). Muốn dùng tên miền riêng: trỏ bản ghi A về IP máy, sửa `DOMAIN=` trong `deploy/oracle/.env`, chạy lại `install`.

## Bước 5. Kiểm tra

```bash
sudo deploy/oracle/scambodia.sh status
curl -s https://scambodia-<IP>.sslip.io/health
```

`/health` trả `200 Healthy` khi Ollama đã tải xong model. Lần phân loại đầu chậm (10–30 giây để nạp model vào RAM), các lần sau vài giây mỗi tin. Đổi model: sửa `LLM_MODEL=` trong `deploy/oracle/.env` rồi chạy lại `install`.

## Bước 6. Kết nối extension

Mở extension → **Cài đặt** → Địa chỉ API: `https://scambodia-<IP>.sslip.io` → Lưu → bấm **Cho phép** khi Chrome hỏi quyền truy cập trang này.

## Bước 7. Bật bot Telegram (tuỳ chọn)

1. Nhắn [@BotFather](https://t.me/BotFather) trên Telegram: `/newbot`, đặt tên, nhận token.
2. Trên máy ảo: `nano deploy/oracle/.env`, điền `TELEGRAM_BOT_TOKEN=<token>`, lưu.
3. `sudo deploy/oracle/scambodia.sh install` (chạy lại an toàn, chỉ thêm bot).

## Bước 8. Database lưu báo cáo (tuỳ chọn)

Không có database thì mọi thứ vẫn chạy, chỉ nút "Báo" trả 503. Muốn lưu báo cáo miễn phí:

1. Azure Portal → **Azure SQL Database → Create** → bật **Apply free offer** (100.000 vCore-giây và 32 GB mỗi tháng, hết hạn mức thì tự tạm dừng, không tính tiền).
2. Trong **Networking**, cho phép IP công khai của máy Oracle.
3. Copy **ADO.NET connection string**, điền vào `SCAMDETECTOR_CONNECTION_STRING=` trong `deploy/oracle/.env`. Mật khẩu có ký tự `$` thì viết thành `$$`.
4. `sudo deploy/oracle/scambodia.sh install` (script tự bật chạy migration).

SQL Server không có bản cho máy ARM, nên dùng Azure SQL thay vì chạy SQL Server trên máy Oracle.

## Bước 9. Thêm model khi đã train xong

Từ máy tính của bạn:

```bash
scp -i <private-key> models/scam-detector.onnx models/vocab.txt models/bpe.codes ubuntu@<PUBLIC_IP>:scambodiadetector/models/
ssh -i <private-key> ubuntu@<PUBLIC_IP> "sudo scambodiadetector/deploy/oracle/scambodia.sh restart"
```

API tự dùng PhoBERT (nhanh hơn), LLM lùi về dự phòng.

## Tự cập nhật

- **Dữ liệu** (danh sách chặn, văn mẫu): crawler chạy trên GitHub Actions → mở PR → bạn merge → trong 15 phút máy chủ kéo về, API tự nạp lại, không khởi động lại.
- **Code**: merge vào `main` → trong 15 phút máy chủ kéo về và build lại container.
- Nhật ký: `sudo tail -f /var/log/scambodia-update.log`.
- Đừng sửa file trong repo trên máy chủ (trừ `deploy/oracle/.env` và `models/`, đều không được git theo dõi), nếu không `update` sẽ báo không fast-forward được.

## Lệnh thường dùng

| Lệnh | Việc |
|---|---|
| `sudo deploy/oracle/scambodia.sh status` | Trạng thái container và `/health` |
| `sudo deploy/oracle/scambodia.sh logs api` | 100 dòng log cuối của API (`bot`, `caddy` tương tự) |
| `sudo deploy/oracle/scambodia.sh update` | Cập nhật ngay, không đợi 15 phút |
| `sudo deploy/oracle/scambodia.sh restart` | Khởi động lại (sau khi chép model) |

## Lưu ý về Oracle Free Tier

- Oracle đã từng cắt hạn mức không báo trước (4 OCPU/24 GB xuống 2 OCPU/12 GB, 06/2026). Máy vượt hạn mức sẽ bị tắt, nên giữ đúng 2 OCPU/12 GB.
- Oracle có thể thu hồi máy Always Free bị coi là "nhàn rỗi" (CPU, mạng, RAM dùng rất thấp trong 7 ngày). Bot Telegram và cron cập nhật giúp máy luôn có hoạt động. Nâng tài khoản lên Pay As You Go (vẫn không tốn tiền nếu chỉ dùng tài nguyên Always Free) thì không bị thu hồi.
- Phương án dự phòng: chạy cùng `docker compose` này trên máy ở nhà, mở ra ngoài bằng Cloudflare Tunnel.

## Xử lý sự cố

| Triệu chứng | Nguyên nhân thường gặp |
|---|---|
| Trình duyệt báo lỗi chứng chỉ, `logs caddy` báo không xin được chứng chỉ | Chưa mở cổng 80/443 ở Bước 3 |
| `ssh` không vào được | Sai file key, hoặc chưa `chmod 600` file key (macOS/Linux) |
| Extension báo "Không kết nối được máy chủ" | Chưa bấm Cho phép quyền ở Bước 6, hoặc sai địa chỉ (phải có `https://`) |
| Build bị kill | Thiếu RAM: kiểm tra swap bằng `swapon --show` |
| `/health` 503, `logs api` báo LLM không trả lời | Model chưa tải xong: `sudo docker compose -f deploy/oracle/docker-compose.yml exec ollama ollama list`; tải lại bằng `install` |
| `update` báo không fast-forward | Có file bị sửa tay trên máy chủ: `git -C ~/scambodiadetector status` |
