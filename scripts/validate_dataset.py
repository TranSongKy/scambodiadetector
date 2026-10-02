import csv
import re
import sys
import unicodedata
from collections import Counter
from datetime import date
from pathlib import Path

REQUIRED_COLUMNS = [
    "id",
    "text",
    "label",
    "scam_type",
    "channel",
    "source",
    "has_url",
    "collected_at",
    "annotator",
]
LABELS = {"scam", "spam", "normal"}
SCAM_TYPES = {
    "bank_impersonation",
    "authority_impersonation",
    "job_scam",
    "prize_scam",
    "investment_scam",
    "loan_scam",
    "delivery_scam",
    "relative_impersonation",
    "account_takeover",
    "other",
}
CHANNELS = {"sms", "zalo", "messenger", "telegram", "email", "other"}
SOURCES = {"collected", "public_post", "contributed", "synthetic"}
ID_PATTERN = re.compile(r"^msg_\d{6}$")
URL_PATTERN = re.compile(r"(https?://|www\.|<URL>)", re.IGNORECASE)
PII_PATTERNS = {
    "phone": re.compile(r"(?<!\d)(\+84|0)\d{9,10}(?!\d)"),
    "email": re.compile(r"[\w.+-]+@[\w-]+\.[\w.]+"),
    "long_number": re.compile(r"(?<!\d)\d{12,19}(?!\d)"),
}
MIN_TEXT_LENGTH = 5
MAX_TEXT_LENGTH = 2000
MIN_LABEL_RATIO = 0.15
MAX_SYNTHETIC_RATIO = 0.20


def normalize_for_duplicate_check(text: str) -> str:
    return re.sub(r"\s+", " ", text).strip().lower()


def validate_row(row: dict[str, str], line_number: int) -> list[str]:
    errors: list[str] = []
    prefix = f"Dòng {line_number}"
    text = row["text"]

    if not ID_PATTERN.match(row["id"]):
        errors.append(f"{prefix}: id '{row['id']}' sai định dạng msg_000001")
    if unicodedata.normalize("NFC", text) != text:
        errors.append(f"{prefix}: text chưa chuẩn hóa NFC")
    if not MIN_TEXT_LENGTH <= len(text) <= MAX_TEXT_LENGTH:
        errors.append(f"{prefix}: text dài {len(text)} ký tự, ngoài khoảng {MIN_TEXT_LENGTH}-{MAX_TEXT_LENGTH}")
    if row["label"] not in LABELS:
        errors.append(f"{prefix}: label '{row['label']}' không hợp lệ")
    if row["label"] == "scam" and row["scam_type"] not in SCAM_TYPES:
        errors.append(f"{prefix}: scam cần scam_type hợp lệ, nhận '{row['scam_type']}'")
    if row["label"] != "scam" and row["scam_type"]:
        errors.append(f"{prefix}: scam_type phải để trống khi label không phải scam")
    if row["channel"] not in CHANNELS:
        errors.append(f"{prefix}: channel '{row['channel']}' không hợp lệ")
    if row["source"] not in SOURCES:
        errors.append(f"{prefix}: source '{row['source']}' không hợp lệ")

    expected_has_url = "true" if URL_PATTERN.search(text) else "false"
    if row["has_url"] != expected_has_url:
        errors.append(f"{prefix}: has_url phải là {expected_has_url}")

    try:
        date.fromisoformat(row["collected_at"])
    except ValueError:
        errors.append(f"{prefix}: collected_at '{row['collected_at']}' không đúng YYYY-MM-DD")

    if not row["annotator"]:
        errors.append(f"{prefix}: thiếu annotator")

    for pii_name, pattern in PII_PATTERNS.items():
        if pattern.search(text):
            errors.append(f"{prefix}: có thể còn thông tin cá nhân ({pii_name}), cần ẩn danh")

    return errors


def validate_distribution(rows: list[dict[str, str]]) -> list[str]:
    errors: list[str] = []
    total = len(rows)
    label_counts = Counter(row["label"] for row in rows)

    for label in LABELS:
        ratio = label_counts[label] / total
        if ratio < MIN_LABEL_RATIO:
            errors.append(f"Nhãn '{label}' chỉ chiếm {ratio:.1%}, cần tối thiểu {MIN_LABEL_RATIO:.0%}")

    synthetic_ratio = sum(row["source"] == "synthetic" for row in rows) / total
    if synthetic_ratio > MAX_SYNTHETIC_RATIO:
        errors.append(f"Dữ liệu synthetic chiếm {synthetic_ratio:.1%}, vượt {MAX_SYNTHETIC_RATIO:.0%}")

    return errors


def validate_uniqueness(rows: list[dict[str, str]]) -> list[str]:
    errors: list[str] = []
    seen_ids: set[str] = set()
    seen_texts: dict[str, str] = {}

    for row in rows:
        if row["id"] in seen_ids:
            errors.append(f"id '{row['id']}' bị trùng")
        seen_ids.add(row["id"])

        normalized_text = normalize_for_duplicate_check(row["text"])
        if normalized_text in seen_texts:
            errors.append(f"text của '{row['id']}' trùng với '{seen_texts[normalized_text]}'")
        else:
            seen_texts[normalized_text] = row["id"]

    return errors


def validate_dataset(dataset_path: Path) -> list[str]:
    with dataset_path.open(encoding="utf-8", newline="") as dataset_file:
        if dataset_file.read(1) == "﻿":
            return ["File có BOM, cần lưu dạng UTF-8 không BOM"]
        dataset_file.seek(0)
        reader = csv.DictReader(dataset_file)

        missing_columns = set(REQUIRED_COLUMNS) - set(reader.fieldnames or [])
        if missing_columns:
            return [f"Thiếu cột: {', '.join(sorted(missing_columns))}"]

        rows = list(reader)

    if not rows:
        return ["Dataset rỗng"]

    errors: list[str] = []
    for line_number, row in enumerate(rows, start=2):
        errors.extend(validate_row(row, line_number))
    errors.extend(validate_uniqueness(rows))
    errors.extend(validate_distribution(rows))
    return errors


def print_summary(dataset_path: Path) -> None:
    with dataset_path.open(encoding="utf-8", newline="") as dataset_file:
        rows = list(csv.DictReader(dataset_file))
    label_counts = Counter(row["label"] for row in rows)
    print(f"Tổng: {len(rows)} mẫu")
    for label, count in label_counts.most_common():
        print(f"  {label}: {count} ({count / len(rows):.1%})")


def main() -> int:
    if len(sys.argv) != 2:
        print("Cách dùng: python scripts/validate_dataset.py <đường dẫn dataset.csv>")
        return 2

    dataset_path = Path(sys.argv[1])
    errors = validate_dataset(dataset_path)

    if errors:
        print(f"Phát hiện {len(errors)} lỗi:")
        for error in errors:
            print(f"  - {error}")
        return 1

    print("Dataset hợp lệ.")
    print_summary(dataset_path)
    return 0


if __name__ == "__main__":
    sys.exit(main())
