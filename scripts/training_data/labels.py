from training_data.candidates import CONFIDENCE_LEVELS, EXCLUDE_LABEL, Candidate, with_suggestion
from validate_dataset import CHANNELS, LABELS, SCAM_LABEL, SCAM_TYPES

MIN_NOTE_LENGTH = 5


class LabelError(ValueError):
    pass


def validate_suggestion(label: str, scam_type: str, channel: str, confidence: str, note: str) -> None:
    if label == EXCLUDE_LABEL:
        if len(note.strip()) < MIN_NOTE_LENGTH:
            raise LabelError("Loại mẫu (exclude) phải ghi lý do trong note")
        return
    if label not in LABELS:
        raise LabelError(f"Nhãn '{label}' không hợp lệ, chọn: {', '.join(sorted(LABELS | {EXCLUDE_LABEL}))}")
    if label == SCAM_LABEL and scam_type not in SCAM_TYPES:
        raise LabelError(f"scam_type '{scam_type}' không hợp lệ cho nhãn scam")
    if label != SCAM_LABEL and scam_type:
        raise LabelError("Chỉ nhãn scam mới có scam_type")
    if channel not in CHANNELS:
        raise LabelError(f"channel '{channel}' không hợp lệ, chọn: {', '.join(sorted(CHANNELS))}")
    if confidence not in CONFIDENCE_LEVELS:
        raise LabelError(f"confidence phải là: {', '.join(CONFIDENCE_LEVELS)}")
    if len(note.strip()) < MIN_NOTE_LENGTH:
        raise LabelError("Phải ghi lý do ngắn trong note")


def apply_suggestions(candidates: list[Candidate], suggestions: list[dict[str, str]]) -> list[Candidate]:
    by_id = {candidate.id: candidate for candidate in candidates}
    for suggestion in suggestions:
        candidate_id = suggestion.get("id", "")
        if candidate_id not in by_id:
            raise LabelError(f"Không có ứng viên '{candidate_id}'")
        values = {
            "suggested_label": suggestion.get("label", "").strip(),
            "suggested_scam_type": suggestion.get("scam_type", "").strip(),
            "suggested_channel": suggestion.get("channel", "").strip(),
            "confidence": suggestion.get("confidence", "").strip(),
            "note": suggestion.get("note", "").strip(),
        }
        try:
            validate_suggestion(
                values["suggested_label"],
                values["suggested_scam_type"],
                values["suggested_channel"],
                values["confidence"],
                values["note"],
            )
        except LabelError as error:
            raise LabelError(f"{candidate_id}: {error}") from error
        by_id[candidate_id] = with_suggestion(by_id[candidate_id], **values)
    return [by_id[candidate.id] for candidate in candidates]
