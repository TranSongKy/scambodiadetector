import csv
from dataclasses import asdict, dataclass, fields
from pathlib import Path

BLOCKED_DOMAINS_FILE = "blocked_domains.csv"
ALLOWED_DOMAINS_FILE = "allowed_domains.csv"
SCAM_TEMPLATES_FILE = "scam_templates.csv"
MANUAL_SUBMISSIONS_FILE = "manual_submissions.csv"
REJECTED_DOMAINS_FILE = "rejected_domains.csv"
REJECTED_TEMPLATES_FILE = "rejected_templates.csv"
TEMPLATE_ID_PREFIX = "tpl_"
TEMPLATE_ID_DIGITS = 6


@dataclass(frozen=True)
class BlockedDomain:
    domain: str
    source: str
    source_url: str
    first_seen: str
    evidence: str = ""


@dataclass(frozen=True)
class ScamTemplate:
    id: str
    text: str
    source: str
    source_url: str
    first_seen: str


@dataclass(frozen=True)
class RejectedDomain:
    domain: str
    reason: str
    rejected_at: str


@dataclass(frozen=True)
class RejectedTemplate:
    fingerprint: str
    reason: str
    rejected_at: str


def read_rows(path: Path) -> list[dict[str, str]]:
    if not path.exists():
        return []
    with path.open(encoding="utf-8-sig", newline="") as csv_file:
        return list(csv.DictReader(csv_file))


def write_records(path: Path, records: list, record_type: type) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as csv_file:
        writer = csv.DictWriter(csv_file, fieldnames=[field.name for field in fields(record_type)], lineterminator="\n")
        writer.writeheader()
        writer.writerows(asdict(record) for record in records)


def load_blocked_domains(data_dir: Path) -> list[BlockedDomain]:
    return [BlockedDomain(**row) for row in read_rows(data_dir / BLOCKED_DOMAINS_FILE)]


def load_allowed_domains(data_dir: Path) -> set[str]:
    domains = (row["domain"].strip().lower() for row in read_rows(data_dir / ALLOWED_DOMAINS_FILE))
    return {domain for domain in domains if domain}


def load_templates(data_dir: Path) -> list[ScamTemplate]:
    return [ScamTemplate(**row) for row in read_rows(data_dir / SCAM_TEMPLATES_FILE)]


def load_rejected_domains(data_dir: Path) -> list[RejectedDomain]:
    return [RejectedDomain(**row) for row in read_rows(data_dir / REJECTED_DOMAINS_FILE)]


def load_rejected_templates(data_dir: Path) -> list[RejectedTemplate]:
    return [RejectedTemplate(**row) for row in read_rows(data_dir / REJECTED_TEMPLATES_FILE)]


def next_template_id(templates: list[ScamTemplate]) -> int:
    numbers = [int(template.id.removeprefix(TEMPLATE_ID_PREFIX)) for template in templates]
    return max(numbers, default=0) + 1


def format_template_id(number: int) -> str:
    return f"{TEMPLATE_ID_PREFIX}{number:0{TEMPLATE_ID_DIGITS}d}"
