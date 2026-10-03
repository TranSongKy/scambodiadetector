from dataclasses import dataclass

from training_data.candidates import EXCLUDE_LABEL, Candidate, ReviewedKey, text_key
from validate_dataset import SCAM_LABEL

ACCEPTED = "accepted"
REJECTED = "rejected"


@dataclass(frozen=True)
class ReviewDecision:
    candidate_id: str
    accept: bool
    label: str = ""
    scam_type: str = ""
    channel: str = ""


@dataclass(frozen=True)
class ReviewOutcome:
    remaining: list[Candidate]
    messages: list[dict[str, str]]
    reviewed: list[ReviewedKey]


def apply_reviews(candidates: list[Candidate], decisions: list[ReviewDecision], annotator: str, today: str):
    decided = {decision.candidate_id: decision for decision in decisions}
    remaining: list[Candidate] = []
    messages: list[dict[str, str]] = []
    reviewed: list[ReviewedKey] = []
    for candidate in candidates:
        decision = decided.get(candidate.id)
        if decision is None:
            remaining.append(candidate)
            continue
        accepted = decision.accept and decision.label not in ("", EXCLUDE_LABEL)
        reviewed.append(ReviewedKey(text_key(candidate.text), ACCEPTED if accepted else REJECTED, today))
        if accepted:
            messages.append(
                {
                    "text": candidate.text,
                    "label": decision.label,
                    "scam_type": decision.scam_type if decision.label == SCAM_LABEL else "",
                    "channel": decision.channel or "other",
                    "source": candidate.source,
                    "collected_at": candidate.collected_at,
                    "annotator": annotator,
                }
            )
    return ReviewOutcome(remaining, messages, reviewed)
