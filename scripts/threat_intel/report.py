from threat_intel.crawler import SourceResult
from threat_intel.update import UpdateResult

MAX_LISTED_ITEMS = 50


def render_report(results: list[SourceResult], update: UpdateResult, dry_run: bool) -> str:
    lines = [
        "## Cập nhật dữ liệu lừa đảo" + (" (chạy thử, không ghi file)" if dry_run else ""),
        "",
        f"- Tên miền mới: **{len(update.new_domains)}** (tổng {len(update.domains)})",
        f"- Văn mẫu mới: **{len(update.new_templates)}** (tổng {len(update.templates)})",
        f"- Bỏ qua vì nằm trong danh sách trắng: {len(update.skipped_allowed)}",
        f"- Bỏ qua vì đã bị loại trước đây: {update.skipped_rejected}",
        "",
        "| Nguồn | Bài đã đọc | Tên miền tìm thấy | Đoạn trích | Lỗi |",
        "|---|---|---|---|---|",
    ]
    lines += [
        f"| {result.source} | {result.articles} | {len(result.domains)} | {len(result.texts)} | {len(result.errors)} |"
        for result in results
    ]
    if update.new_domains:
        lines += ["", "### Tên miền mới", ""]
        new_domains = update.new_domains[:MAX_LISTED_ITEMS]
        lines += [f"- `{record.domain}` ({record.source}): {record.evidence}" for record in new_domains]
    if update.new_templates:
        lines += ["", "### Văn mẫu mới", ""]
        lines += [f"- `{record.id}`: {record.text}" for record in update.new_templates[:MAX_LISTED_ITEMS]]
    errors = [error for result in results for error in result.errors]
    if errors:
        lines += ["", "### Lỗi", ""] + [f"- {error}" for error in errors[:MAX_LISTED_ITEMS]]
    lines += [
        "",
        "Người duyệt: kiểm tra từng tên miền và văn mẫu trước khi merge (có thể dùng agent threat-intel-reviewer).",
        "Loại mục sai bằng `python scripts/reject_threat_intel.py --domain|--template ... --reason ...`,",
        "không xóa tay để lần crawl sau không thêm lại.",
    ]
    return "\n".join(lines) + "\n"
