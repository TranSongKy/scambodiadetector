import argparse
import csv
import re
import sys
from collections import Counter
from pathlib import Path

from masking import find_maskable_pii, has_url, mask, normalize_text

OUTPUT_COLUMNS = ["id", "text", "label", "scam_type", "channel", "source", "has_url", "collected_at", "annotator"]
REQUIRED_INPUT_COLUMNS = {"text", "label", "channel", "source", "collected_at", "annotator"}
RAW_DIRECTORY_NAME = "raw"
ID_PREFIX = "msg_"
ID_DIGITS = 6
CSV_TRUE = "true"
CSV_FALSE = "false"
HONORIFIC_NAME_PATTERN = re.compile(
    r"\b(?:anh|chị|chi|em|ông|ong|bà|cô|chú|chu|bác|bac|bạn|ban|mr|mrs|ms)\.?\s+([^\W\d_]+)",
    re.IGNORECASE,
)


class AnonymizeError(Exception):
    pass


def format_id(number: int) -> str:
    return f"{ID_PREFIX}{number:0{ID_DIGITS}d}"


def may_contain_name(text: str) -> bool:
    return any(match.group(1)[0].isupper() for match in HONORIFIC_NAME_PATTERN.finditer(text))


def anonymize_row(row: dict[str, str], row_id: str) -> dict[str, str]:
    text = mask(normalize_text(row["text"]))
    anonymized = {column: (row.get(column) or "").strip() for column in OUTPUT_COLUMNS}
    anonymized.update({"id": row_id, "text": text, "has_url": CSV_TRUE if has_url(text) else CSV_FALSE})
    return anonymized


def read_input(input_path: Path) -> list[dict[str, str]]:
    with input_path.open(encoding="utf-8-sig", newline="") as input_file:
        reader = csv.DictReader(input_file)
        missing_columns = REQUIRED_INPUT_COLUMNS - set(reader.fieldnames or [])
        if missing_columns:
            raise AnonymizeError(f"File đầu vào thiếu cột: {', '.join(sorted(missing_columns))}")
        return list(reader)


def write_output(output_path: Path, rows: list[dict[str, str]]) -> None:
    if RAW_DIRECTORY_NAME in output_path.resolve().parts:
        raise AnonymizeError("Không ghi kết quả ẩn danh vào thư mục data/raw/")
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w", encoding="utf-8", newline="") as output_file:
        writer = csv.DictWriter(output_file, fieldnames=OUTPUT_COLUMNS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def anonymize(input_path: Path, output_path: Path, start_id: int) -> tuple[list[dict[str, str]], Counter[str]]:
    raw_rows = read_input(input_path)
    placeholder_counts: Counter[str] = Counter()
    rows: list[dict[str, str]] = []

    for offset, raw_row in enumerate(raw_rows):
        placeholder_counts.update(find_maskable_pii(normalize_text(raw_row["text"])))
        rows.append(anonymize_row(raw_row, format_id(start_id + offset)))

    write_output(output_path, rows)
    return rows, placeholder_counts


def parse_arguments(arguments: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Ẩn danh tin nhắn gốc theo docs/data-schema.md mục 5.")
    parser.add_argument("input", type=Path, help="CSV gốc (có cột text, label, channel, source, collected_at, ...)")
    parser.add_argument("output", type=Path, help="CSV đã ẩn danh, cùng cột với dataset.csv")
    parser.add_argument("--start-id", type=int, default=1, help="Số thứ tự id đầu tiên (msg_000001)")
    return parser.parse_args(arguments)


def main(arguments: list[str]) -> int:
    options = parse_arguments(arguments)
    try:
        rows, placeholder_counts = anonymize(options.input, options.output, options.start_id)
    except AnonymizeError as error:
        print(f"Lỗi: {error}")
        return 1

    print(f"Đã ẩn danh {len(rows)} mẫu vào {options.output}")
    for pii_name, count in sorted(placeholder_counts.items()):
        print(f"  {pii_name}: {count} mẫu")

    rows_to_review = [row["id"] for row in rows if may_contain_name(row["text"])]
    if rows_to_review:
        print(f"Cần kiểm tra tay tên người (<NAME>) ở {len(rows_to_review)} mẫu: {', '.join(rows_to_review)}")
    print("Bước tiếp: kiểm tra tay, gộp vào dataset.csv rồi chạy scripts/validate_dataset.py")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
