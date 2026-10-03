import argparse
import sys
from datetime import UTC, datetime
from pathlib import Path

from threat_intel.crawler import crawl_source
from threat_intel.fetcher import PoliteFetcher
from threat_intel.report import render_report
from threat_intel.sources import load_sources
from threat_intel.store import (
    BLOCKED_DOMAINS_FILE,
    SCAM_TEMPLATES_FILE,
    BlockedDomain,
    ScamTemplate,
    load_allowed_domains,
    load_blocked_domains,
    load_rejected_domains,
    load_rejected_templates,
    load_templates,
    write_records,
)
from threat_intel.update import build_update

REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_DATA_DIR = REPOSITORY_ROOT / "data" / "threat-intel"


def parse_arguments(arguments: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Crawl nguồn cảnh báo lừa đảo, cập nhật tên miền và văn mẫu.")
    parser.add_argument("--data-dir", type=Path, default=DEFAULT_DATA_DIR)
    parser.add_argument("--sources", type=Path, default=DEFAULT_DATA_DIR / "sources.json")
    parser.add_argument("--report", type=Path, help="Ghi báo cáo Markdown (dùng làm mô tả PR)")
    parser.add_argument("--dry-run", action="store_true", help="Chỉ crawl và báo cáo, không ghi file dữ liệu")
    parser.add_argument("--only", action="append", default=[], help="Chỉ chạy nguồn có tên này (lặp lại được)")
    parser.add_argument("--delay", type=float, default=2.0, help="Số giây chờ giữa hai request tới cùng một host")
    return parser.parse_args(arguments)


def main(arguments: list[str]) -> int:
    options = parse_arguments(arguments)
    sources = [source for source in load_sources(options.sources) if not options.only or source.name in options.only]
    fetcher = PoliteFetcher(delay_seconds=options.delay)
    results = [crawl_source(source, fetcher.fetch, options.data_dir) for source in sources]

    today = datetime.now(UTC).date().isoformat()
    update = build_update(
        load_blocked_domains(options.data_dir),
        load_templates(options.data_dir),
        results,
        load_allowed_domains(options.data_dir),
        today,
        {record.domain for record in load_rejected_domains(options.data_dir)},
        [record.fingerprint for record in load_rejected_templates(options.data_dir)],
    )
    if not options.dry_run:
        write_records(options.data_dir / BLOCKED_DOMAINS_FILE, update.domains, BlockedDomain)
        write_records(options.data_dir / SCAM_TEMPLATES_FILE, update.templates, ScamTemplate)

    report = render_report(results, update, options.dry_run)
    if options.report:
        options.report.write_text(report, encoding="utf-8")
    print(report)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
