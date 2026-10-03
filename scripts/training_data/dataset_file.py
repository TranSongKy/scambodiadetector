import csv
from pathlib import Path

from masking import has_url
from training_data.candidates import read_rows

DATASET_COLUMNS = ["id", "text", "label", "scam_type", "channel", "source", "has_url", "collected_at", "annotator"]
MESSAGE_ID_PREFIX = "msg_"
MESSAGE_ID_DIGITS = 6


def dataset_texts(dataset_path: Path) -> list[str]:
    return [row["text"] for row in read_rows(dataset_path)]


def next_message_number(rows: list[dict[str, str]]) -> int:
    numbers = [int(row["id"].removeprefix(MESSAGE_ID_PREFIX)) for row in rows if row.get("id", "").startswith("msg_")]
    return max(numbers, default=0) + 1


def append_messages(dataset_path: Path, messages: list[dict[str, str]]) -> list[str]:
    rows = read_rows(dataset_path)
    number = next_message_number(rows)
    new_ids: list[str] = []
    for message in messages:
        message_id = f"{MESSAGE_ID_PREFIX}{number:0{MESSAGE_ID_DIGITS}d}"
        rows.append({**message, "id": message_id, "has_url": "true" if has_url(message["text"]) else "false"})
        new_ids.append(message_id)
        number += 1
    with dataset_path.open("w", encoding="utf-8", newline="") as dataset_file:
        writer = csv.DictWriter(dataset_file, fieldnames=DATASET_COLUMNS, lineterminator="\n")
        writer.writeheader()
        writer.writerows({column: row.get(column, "") for column in DATASET_COLUMNS} for row in rows)
    return new_ids
