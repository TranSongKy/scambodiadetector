import hashlib
import re
import unicodedata

SHINGLE_SIZE = 3
FINGERPRINT_HEX_LENGTH = 12
FINGERPRINT_SEPARATOR = " "
TOKEN_PATTERN = re.compile(r"<[A-Z]+>|[^\W_]+")


def tokenize(text: str) -> list[str]:
    composed = unicodedata.normalize("NFC", text)
    return [token if token.startswith("<") else token.lower() for token in TOKEN_PATTERN.findall(composed)]


def shingles(text: str) -> set[str]:
    tokens = tokenize(text)
    if len(tokens) < SHINGLE_SIZE:
        return {" ".join(tokens)} if tokens else set()
    return {" ".join(tokens[index : index + SHINGLE_SIZE]) for index in range(len(tokens) - SHINGLE_SIZE + 1)}


def template_coverage(template: str, message: str) -> float:
    template_shingles = shingles(template)
    if not template_shingles:
        return 0.0
    return len(template_shingles & shingles(message)) / len(template_shingles)


def jaccard(first: str, second: str) -> float:
    first_shingles, second_shingles = shingles(first), shingles(second)
    union = first_shingles | second_shingles
    return len(first_shingles & second_shingles) / len(union) if union else 0.0


def jaccard_sets(first: set[str], second: set[str]) -> float:
    union = first | second
    return len(first & second) / len(union) if union else 0.0


def fingerprint(text: str) -> str:
    hashed = {
        hashlib.sha256(shingle.encode("utf-8")).hexdigest()[:FINGERPRINT_HEX_LENGTH] for shingle in shingles(text)
    }
    return FINGERPRINT_SEPARATOR.join(sorted(hashed))


def fingerprint_set(value: str) -> set[str]:
    return set(value.split(FINGERPRINT_SEPARATOR)) if value else set()


def matches_fingerprint(text: str, fingerprints: list[str], threshold: float) -> bool:
    own = fingerprint_set(fingerprint(text))
    return any(jaccard_sets(own, fingerprint_set(known)) >= threshold for known in fingerprints)
