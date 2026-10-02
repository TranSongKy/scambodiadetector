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
SCAM_LABEL = "scam"
LABELS = {SCAM_LABEL, "spam", "normal"}
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
SYNTHETIC_SOURCE = "synthetic"
SOURCES = {"collected", "public_post", "contributed", SYNTHETIC_SOURCE}
CSV_TRUE = "true"
CSV_FALSE = "false"
BOM = "\ufeff"
ID_PATTERN = re.compile(r"^msg_\d{6}$")
URL_PLACEHOLDER = "<URL>"
RAW_URL_PATTERN = re.compile(
    r"(https?://|www\.|\b[\w-]+\.(com|vn|net|org|info|xyz|top|me|ly|io|cc|online|site|shop|link)\b)",
    re.IGNORECASE,
)
PLACEHOLDERS = {"<PHONE>", "<ACCOUNT>", "<NAME>", "<EMAIL>", URL_PLACEHOLDER, "<ID>", "<OTP>"}
PLACEHOLDER_PATTERN = re.compile(r"<[A-Z_]+>")
DIGIT_SEPARATOR_PATTERN = re.compile(r"(?<=\d)[ .\-](?=\d)")
SEPARATED_DIGIT_PII_PATTERNS = {
    "phone": re.compile(r"(?<!\d)(\+?84|0)\d{9,10}(?!\d)"),
}
PII_PATTERNS = {
    "card_number": re.compile(r"(?<!\d)\d{4}([ .\-]\d{4}){3}(?!\d)"),
    "email": re.compile(r"[\w.+-]+@[\w-]+\.[\w.]+"),
    "long_number": re.compile(r"(?<!\d)\d{9,19}(?!\d)"),
    "otp": re.compile(r"(otp|m[ãa] x[áa]c (nh[ậa]n|th[ựu]c))\D{0,20}\d{4,8}(?!\d)", re.IGNORECASE),
    "url": RAW_URL_PATTERN,
}
MIN_TEXT_LENGTH = 5
MAX_TEXT_LENGTH = 2000
MIN_LABEL_RATIO = 0.15
MAX_SYNTHETIC_RATIO = 0.20


def normalize_for_duplicate_check(text: str) -> str:
    return re.sub(r"\s+", " ", text).strip().lower()


def has_url(text: str) -> bool:
    return URL_PLACEHOLDER in text or RAW_URL_PATTERN.search(text) is not None


def find_pii(text: str) -> list[str]:
    joined_digits_text = DIGIT_SEPARATOR_PATTERN.sub("", text)
    found = [name for name, pattern in SEPARATED_DIGIT_PII_PATTERNS.items() if pattern.search(joined_digits_text)]
    found.extend(name for name, pattern in PII_PATTERNS.items() if pattern.search(text))
    return found


def validate_row(row: dict[str | None, str | None], line_number: int) -> list[str]:
    prefix = f"Dòng {line_number}"
    if None in row:
        return [f"{prefix}: thừa cột so với header"]
    if any(row.get(column) is None for column in REQUIRED_COLUMNS):
        return [f"{prefix}: thiếu cột so với header"]
    return validate_complete_row({key: value or "" for key, value in row.items() if key is not None}, prefix)


def validate_complete_row(row: dict[str, str], prefix: str) -> list[str]:
    errors = validate_text(row["text"], prefix)
    errors.extend(validate_categories(row, prefix))
    errors.extend(validate_metadata(row, prefix))
    return errors


def validate_text(text: str, prefix: str) -> list[str]:
    errors: list[str] = []
    if unicodedata.normalize("NFC", text) != text:
        errors.append(f"{prefix}: text chưa chuẩn hóa NFC")
    text_length = len(text.strip())
    if not MIN_TEXT_LENGTH <= text_length <= MAX_TEXT_LENGTH:
        errors.append(f"{prefix}: text dài {text_length} ký tự, ngoài khoảng {MIN_TEXT_LENGTH}-{MAX_TEXT_LENGTH}")
    for pii_name in find_pii(text):
        errors.append(f"{prefix}: có thể còn thông tin cá nhân ({pii_name}), cần ẩn danh")
    for placeholder in sorted(set(PLACEHOLDER_PATTERN.findall(text)) - PLACEHOLDERS):
        errors.append(f"{prefix}: placeholder {placeholder} không có trong docs/data-schema.md")
    return errors


def validate_categories(row: dict[str, str], prefix: str) -> list[str]:
    errors: list[str] = []
    is_scam = row["label"] == SCAM_LABEL
    if row["label"] not in LABELS:
        errors.append(f"{prefix}: label '{row['label']}' không hợp lệ")
    if is_scam and row["scam_type"] not in SCAM_TYPES:
        errors.append(f"{prefix}: scam cần scam_type hợp lệ, nhận '{row['scam_type']}'")
    if not is_scam and row["scam_type"]:
        errors.append(f"{prefix}: scam_type phải để trống khi label không phải scam")
    if row["channel"] not in CHANNELS:
        errors.append(f"{prefix}: channel '{row['channel']}' không hợp lệ")
    if row["source"] not in SOURCES:
        errors.append(f"{prefix}: source '{row['source']}' không hợp lệ")
    return errors


def validate_metadata(row: dict[str, str], prefix: str) -> list[str]:
    errors: list[str] = []
    if not ID_PATTERN.match(row["id"]):
        errors.append(f"{prefix}: id '{row['id']}' sai định dạng msg_000001")
    expected_has_url = CSV_TRUE if has_url(row["text"]) else CSV_FALSE
    if row["has_url"] != expected_has_url:
        errors.append(f"{prefix}: has_url phải là {expected_has_url}")
    try:
        date.fromisoformat(row["collected_at"])
    except ValueError:
        errors.append(f"{prefix}: collected_at '{row['collected_at']}' không đúng YYYY-MM-DD")
    if not row["annotator"]:
        errors.append(f"{prefix}: thiếu annotator")
    return errors


def validate_distribution(rows: list[dict[str, str]]) -> list[str]:
    errors: list[str] = []
    total = len(rows)
    if total == 0:
        return errors
    label_counts = Counter(row["label"] for row in rows)

    for label in sorted(LABELS):
        ratio = label_counts[label] / total
        if ratio < MIN_LABEL_RATIO:
            errors.append(f"Nhãn '{label}' chỉ chiếm {ratio:.1%}, cần tối thiểu {MIN_LABEL_RATIO:.0%}")

    synthetic_ratio = sum(row["source"] == SYNTHETIC_SOURCE for row in rows) / total
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
        if dataset_file.read(1) == BOM:
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
    complete_rows = [row for row in rows if None not in row and None not in row.values()]
    errors.extend(validate_uniqueness(complete_rows))
    errors.extend(validate_distribution(complete_rows))
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
