import argparse
import sys
from datetime import UTC, datetime
from pathlib import Path

from threat_intel.fetcher import PoliteFetcher
from training_data.candidates import SOURCES_FILE, load_candidates, load_reviewed, save_candidates
from training_data.collect import CollectionReport, build_collector, collect_source
from training_data.dataset_file import dataset_texts
from training_data.sources import load_training_sources

REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_DATA_DIR = REPOSITORY_ROOT / "data" / "training-candidates"
DEFAULT_DATASET = REPOSITORY_ROOT / "data" / "processed" / "dataset.csv"
DEFAULT_MAX_NEW = 300


def render_report(reports: list[CollectionReport], total: int, dry_run: bool) -> str:
    lines = [
        "## Ứng viên dữ liệu huấn luyện" + (" (chạy thử, không ghi file)" if dry_run else ""),
        "",
        f"- Ứng viên mới: **{sum(report.added for report in reports)}** (đang chờ: {total})",
        "",
        "| Nguồn | Bài đã đọc | Mới | Trùng | Có thể chứa tên người | Không hợp lệ | Lỗi |",
        "|---|---|---|---|---|---|---|",
    ]
    lines += [
        f"| {r.source} | {r.articles} | {r.added} | {r.skipped_duplicate} | {r.skipped_name} | "
        f"{r.skipped_invalid} | {len(r.errors)} |"
        for r in reports
    ]
    errors = [error for report in reports for error in report.errors]
    if errors:
        lines += ["", "### Lỗi và nguồn bị bỏ qua", ""] + [f"- {error}" for error in errors[:50]]
    lines += [
        "",
        "Bước tiếp: chạy agent `training-data-labeler` để gợi ý nhãn, rồi duyệt bằng "
        "`python scripts/review_candidates.py --annotator <mã của bạn>`. Không mẫu nào vào dataset nếu chưa duyệt.",
    ]
    return "\n".join(lines) + "\n"


def parse_arguments(arguments: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Thu thập tin nhắn ứng viên cho dữ liệu huấn luyện (chờ duyệt).")
    parser.add_argument("--data-dir", type=Path, default=DEFAULT_DATA_DIR)
    parser.add_argument("--dataset", type=Path, default=DEFAULT_DATASET)
    parser.add_argument("--sources", type=Path)
    parser.add_argument("--only", action="append", default=[])
    parser.add_argument("--max-new", type=int, default=DEFAULT_MAX_NEW)
    parser.add_argument("--delay", type=float, default=2.0)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--dry-run", action="store_true")
    return parser.parse_args(arguments)


def main(arguments: list[str]) -> int:
    options = parse_arguments(arguments)
    sources = load_training_sources(options.sources or options.data_dir / SOURCES_FILE)
    sources = [source for source in sources if not options.only or source.name in options.only]
    existing = load_candidates(options.data_dir)
    reviewed_keys = {record.key for record in load_reviewed(options.data_dir)}
    today = datetime.now(UTC).date().isoformat()
    collector = build_collector(existing, dataset_texts(options.dataset), reviewed_keys, today)
    fetch = PoliteFetcher(delay_seconds=options.delay).fetch

    reports = []
    for source in sources:
        if len(collector.added) >= options.max_new:
            break
        reports.append(collect_source(source, fetch, collector, options.data_dir))

    new_candidates = collector.added[: options.max_new]
    if not options.dry_run:
        save_candidates(options.data_dir, existing + new_candidates)
    report = render_report(reports, len(existing) + len(new_candidates), options.dry_run)
    if options.report:
        options.report.write_text(report, encoding="utf-8")
    print(report)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
