import csv
from pathlib import Path

FIELDNAMES = ["id", "text", "label", "scam_type", "channel", "source", "has_url", "collected_at", "annotator"]
SCAM_TEXT = "Tai khoan bi khoa, xac minh tai <URL> ngay {index}"
SPAM_TEXT = "Giam gia 50% toan bo cua hang dip cuoi tuan {index}"
NORMAL_TEXT = "Mai hop nhom luc 7h o thu vien nhe {index}"


def make_row(index: int, label: str, source: str = "collected") -> dict[str, str]:
    templates = {"scam": SCAM_TEXT, "spam": SPAM_TEXT, "normal": NORMAL_TEXT}
    text = templates[label].format(index=index)
    return {
        "id": f"msg_{index:06d}",
        "text": text,
        "label": label,
        "scam_type": "bank_impersonation" if label == "scam" else "",
        "channel": "sms",
        "source": source,
        "has_url": "true" if "<URL>" in text else "false",
        "collected_at": "2026-10-02",
        "annotator": "ky",
    }


def make_balanced_rows(per_label: int) -> list[dict[str, str]]:
    labels = ["scam", "spam", "normal"]
    return [make_row(index, labels[index % len(labels)]) for index in range(1, per_label * len(labels) + 1)]


def write_dataset(path: Path, rows: list[dict[str, str]]) -> Path:
    with path.open("w", encoding="utf-8", newline="") as dataset_file:
        writer = csv.DictWriter(dataset_file, fieldnames=FIELDNAMES, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)
    return path
