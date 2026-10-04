import argparse
import sys
from collections.abc import Callable
from datetime import UTC, datetime
from pathlib import Path

from training_data.candidates import Candidate, load_candidates, load_reviewed, save_candidates, save_reviewed
from training_data.dataset_file import append_messages
from training_data.review import ReviewDecision, apply_reviews
from validate_dataset import SCAM_LABEL, SCAM_TYPES

REPOSITORY_ROOT = Path(__file__).resolve().parent.parent
DEFAULT_DATA_DIR = REPOSITORY_ROOT / "data" / "training-candidates"
DEFAULT_DATASET = REPOSITORY_ROOT / "data" / "processed" / "dataset.csv"
LABEL_KEYS = {"s": "scam", "p": "spam", "n": "normal"}
ORDERED_SCAM_TYPES = sorted(SCAM_TYPES)
HELP = "[Enter] đồng ý  [s] scam  [p] spam  [n] bình thường  [x] loại  [b] bỏ qua  [q] lưu và thoát"
SAVE_EVERY = 10


def describe(candidate: Candidate, position: int, total: int) -> str:
    suggestion = " / ".join(
        part for part in (candidate.suggested_label, candidate.suggested_scam_type, candidate.suggested_channel) if part
    )
    return "\n".join(
        [
            "",
            f"[{position}/{total}] {candidate.id} · {candidate.origin} · {candidate.source_url or 'không có link'}",
            f"Tin: {candidate.text}",
            f"Agent gợi ý: {suggestion or '(chưa có)'} (độ chắc: {candidate.confidence or '-'})",
            f"Lý do: {candidate.note or '-'}",
            HELP,
        ]
    )


def ask_scam_type(read: Callable[[str], str], suggested: str) -> str:
    if suggested in SCAM_TYPES:
        return suggested
    menu = "  ".join(f"{index}.{name}" for index, name in enumerate(ORDERED_SCAM_TYPES, start=1))
    while True:
        answer = read(f"Loại lừa đảo? {menu}\n> ").strip()
        if answer.isdigit() and 1 <= int(answer) <= len(ORDERED_SCAM_TYPES):
            return ORDERED_SCAM_TYPES[int(answer) - 1]


def decide(candidate: Candidate, answer: str, read: Callable[[str], str]) -> ReviewDecision | None:
    channel = candidate.suggested_channel or candidate.hint_channel or "other"
    if answer == "":
        if candidate.suggested_label in LABEL_KEYS.values():
            scam_type = (
                ask_scam_type(read, candidate.suggested_scam_type) if candidate.suggested_label == SCAM_LABEL else ""
            )
            return ReviewDecision(candidate.id, True, candidate.suggested_label, scam_type, channel)
        return ReviewDecision(candidate.id, False)
    if answer in LABEL_KEYS:
        label = LABEL_KEYS[answer]
        scam_type = ask_scam_type(read, candidate.suggested_scam_type) if label == SCAM_LABEL else ""
        return ReviewDecision(candidate.id, True, label, scam_type, channel)
    if answer == "x":
        return ReviewDecision(candidate.id, False)
    return None


def run_review(data_dir: Path, dataset: Path, annotator: str, read: Callable[[str], str], write: Callable[[str], None]):
    today = datetime.now(UTC).date().isoformat()
    candidates = load_candidates(data_dir)
    queue = [candidate for candidate in candidates if candidate.is_labeled]
    write(f"Có {len(queue)} ứng viên đã được agent gợi ý nhãn, {len(candidates) - len(queue)} chưa gợi ý.")
    decisions: list[ReviewDecision] = []
    accepted = 0

    def save() -> None:
        nonlocal candidates, decisions, accepted
        outcome = apply_reviews(candidates, decisions, annotator, today)
        if outcome.messages:
            append_messages(dataset, outcome.messages)
        save_candidates(data_dir, outcome.remaining)
        save_reviewed(data_dir, load_reviewed(data_dir) + outcome.reviewed)
        accepted += len(outcome.messages)
        candidates, decisions = outcome.remaining, []

    for position, candidate in enumerate(queue, start=1):
        write(describe(candidate, position, len(queue)))
        answer = read("> ").strip().lower()
        if answer == "q":
            break
        decision = None if answer == "b" else decide(candidate, answer, read)
        if decision is not None:
            decisions.append(decision)
        if len(decisions) >= SAVE_EVERY:
            save()
    save()
    write(f"\nĐã thêm {accepted} mẫu vào {dataset}. Chạy: python scripts/validate_dataset.py {dataset}")
    return accepted


def parse_arguments(arguments: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Duyệt nhãn agent gợi ý rồi đưa mẫu đã duyệt vào dataset.csv.")
    parser.add_argument("--annotator", required=True, help="Mã người duyệt, ví dụ: ky")
    parser.add_argument("--data-dir", type=Path, default=DEFAULT_DATA_DIR)
    parser.add_argument("--dataset", type=Path, default=DEFAULT_DATASET)
    return parser.parse_args(arguments)


def main(arguments: list[str]) -> int:
    options = parse_arguments(arguments)
    run_review(options.data_dir, options.dataset, options.annotator, input, print)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
