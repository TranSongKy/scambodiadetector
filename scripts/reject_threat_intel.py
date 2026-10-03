import argparse
import sys
from datetime import UTC, datetime
from pathlib import Path

from threat_intel.similarity import fingerprint
from threat_intel.store import (
    BLOCKED_DOMAINS_FILE,
    REJECTED_DOMAINS_FILE,
    REJECTED_TEMPLATES_FILE,
    SCAM_TEMPLATES_FILE,
    BlockedDomain,
    RejectedDomain,
    RejectedTemplate,
    ScamTemplate,
    load_blocked_domains,
    load_rejected_domains,
    load_rejected_templates,
    load_templates,
    write_records,
)

REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_DATA_DIR = REPOSITORY_ROOT / "data" / "threat-intel"
MIN_REASON_CHARACTERS = 10


class RejectError(Exception):
    pass


def reject_domain(data_dir: Path, domain: str, reason: str, today: str) -> None:
    domains = load_blocked_domains(data_dir)
    remaining = [record for record in domains if record.domain != domain]
    if len(remaining) == len(domains):
        raise RejectError(f"Không có tên miền '{domain}' trong {BLOCKED_DOMAINS_FILE}")
    write_records(data_dir / BLOCKED_DOMAINS_FILE, remaining, BlockedDomain)
    rejected = load_rejected_domains(data_dir) + [RejectedDomain(domain, reason, today)]
    write_records(data_dir / REJECTED_DOMAINS_FILE, sorted(rejected, key=lambda r: r.domain), RejectedDomain)


def reject_template(data_dir: Path, template_id: str, reason: str, today: str) -> None:
    templates = load_templates(data_dir)
    matched = [record for record in templates if record.id == template_id]
    if not matched:
        raise RejectError(f"Không có văn mẫu '{template_id}' trong {SCAM_TEMPLATES_FILE}")
    write_records(data_dir / SCAM_TEMPLATES_FILE, [r for r in templates if r.id != template_id], ScamTemplate)
    rejected = load_rejected_templates(data_dir) + [RejectedTemplate(fingerprint(matched[0].text), reason, today)]
    write_records(data_dir / REJECTED_TEMPLATES_FILE, rejected, RejectedTemplate)


def parse_arguments(arguments: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Loại một tên miền hoặc văn mẫu và ghi nhớ để crawler không thêm lại.")
    target = parser.add_mutually_exclusive_group(required=True)
    target.add_argument("--domain", help="Tên miền cần loại khỏi blocked_domains.csv")
    target.add_argument("--template", help="Mã văn mẫu (tpl_000001) cần loại khỏi scam_templates.csv")
    parser.add_argument("--reason", required=True, help=f"Lý do loại (tối thiểu {MIN_REASON_CHARACTERS} ký tự)")
    parser.add_argument("--data-dir", type=Path, default=DEFAULT_DATA_DIR)
    return parser.parse_args(arguments)


def main(arguments: list[str]) -> int:
    options = parse_arguments(arguments)
    reason = options.reason.strip()
    if len(reason) < MIN_REASON_CHARACTERS:
        print(f"Lỗi: lý do phải có ít nhất {MIN_REASON_CHARACTERS} ký tự")
        return 2
    today = datetime.now(UTC).date().isoformat()
    try:
        if options.domain:
            reject_domain(options.data_dir, options.domain.strip().lower(), reason, today)
        else:
            reject_template(options.data_dir, options.template.strip(), reason, today)
    except RejectError as error:
        print(f"Lỗi: {error}")
        return 1
    print(f"Đã loại {options.domain or options.template}: {reason}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
