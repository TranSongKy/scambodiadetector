import csv
import hashlib
import re
from dataclasses import asdict, dataclass, fields, replace
from pathlib import Path

CANDIDATES_FILE = "candidates.csv"
REVIEWED_FILE = "reviewed.csv"
MANUAL_MESSAGES_FILE = "manual_messages.csv"
SOURCES_FILE = "sources.json"
CANDIDATE_ID_PREFIX = "cand_"
CANDIDATE_ID_DIGITS = 6
KEY_LENGTH = 16
EXCLUDE_LABEL = "exclude"
CONFIDENCE_LEVELS = ("high", "medium", "low")
ORIGINS = ("news", "forum", "public_dataset", "manual", "synthetic")
WHITESPACE = re.compile(r"\s+")


@dataclass(frozen=True)
class Candidate:
    id: str
    text: str
    origin: str
    source: str
    source_url: str
    collected_at: str
    hint_label: str = ""
    hint_channel: str = ""
    suggested_label: str = ""
    suggested_scam_type: str = ""
    suggested_channel: str = ""
    confidence: str = ""
    note: str = ""

    @property
    def is_labeled(self) -> bool:
        return bool(self.suggested_label)


@dataclass(frozen=True)
class ReviewedKey:
    key: str
    decision: str
    decided_at: str


def text_key(text: str) -> str:
    normalized = WHITESPACE.sub(" ", text).strip().lower()
    return hashlib.sha256(normalized.encode("utf-8")).hexdigest()[:KEY_LENGTH]


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


def load_candidates(directory: Path) -> list[Candidate]:
    names = {field.name for field in fields(Candidate)}
    return [
        Candidate(**{key: value for key, value in row.items() if key in names})
        for row in read_rows(directory / CANDIDATES_FILE)
    ]


def save_candidates(directory: Path, candidates: list[Candidate]) -> None:
    write_records(directory / CANDIDATES_FILE, candidates, Candidate)


def load_reviewed(directory: Path) -> list[ReviewedKey]:
    return [ReviewedKey(**row) for row in read_rows(directory / REVIEWED_FILE)]


def save_reviewed(directory: Path, reviewed: list[ReviewedKey]) -> None:
    write_records(directory / REVIEWED_FILE, reviewed, ReviewedKey)


def next_candidate_number(candidates: list[Candidate]) -> int:
    numbers = [int(candidate.id.removeprefix(CANDIDATE_ID_PREFIX)) for candidate in candidates]
    return max(numbers, default=0) + 1


def format_candidate_id(number: int) -> str:
    return f"{CANDIDATE_ID_PREFIX}{number:0{CANDIDATE_ID_DIGITS}d}"


def with_suggestion(candidate: Candidate, **suggestion: str) -> Candidate:
    return replace(candidate, **suggestion)
