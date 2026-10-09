import re
import unicodedata
from collections.abc import Callable

URL_TOP_LEVEL_DOMAINS = (
    "com|vn|net|org|info|xyz|top|me|ly|io|cc|online|site|shop|link|club|icu|vip|live|buzz|tk|ml|ga|cf|gq|gl|gd|id|at|gy"
)
URL_PLACEHOLDER = "<URL>"
EMAIL_PLACEHOLDER = "<EMAIL>"
OTP_PLACEHOLDER = "<OTP>"
ID_PLACEHOLDER = "<ID>"
PHONE_PLACEHOLDER = "<PHONE>"
ACCOUNT_PLACEHOLDER = "<ACCOUNT>"
OTP_KEYWORD_WINDOW = 20
ID_KEYWORD_WINDOW = 25
TRAILING_URL_PUNCTUATION = ".,;:!?)]}\"'"

WHITESPACE_PATTERN = re.compile(r"\s+")
BRACKETED_DOT_PATTERN = re.compile(
    r"(?<=[a-z0-9])\s?(?:\[\s?(?:\.|dot|ch[ấa]m)\s?\]|\(\s?(?:\.|dot|ch[ấa]m)\s?\)|\{\s?(?:\.|dot|ch[ấa]m)\s?\})\s?(?=[a-z0-9])",
    re.IGNORECASE,
)
DEFANGED_SCHEME_PATTERN = re.compile(r"\bh(?:xx|\*\*)p(s?)(?:\[:\]|:)//", re.IGNORECASE)
INVISIBLE_CHARACTERS_PATTERN = re.compile("[\u00ad\u200b-\u200d\u2060\ufeff]")
URL_PATTERN = re.compile(
    rf"(?:https?://|www\.)[^\s<>\"]+|\b(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+"
    rf"(?-i:(?:{URL_TOP_LEVEL_DOMAINS})|(?:{URL_TOP_LEVEL_DOMAINS.upper()}))\b(?:/[^\s<>\"]*)?",
    re.IGNORECASE,
)
EMAIL_PATTERN = re.compile(r"[\w.+-]+@[\w-]+(?:\.[\w-]+)+")
KEYWORD_GAP = r"[^\d<>]"
OTP_CANDIDATE_PATTERN = re.compile(r"(?<![\d.,/])\d(?:[ .\-]?\d){3,7}(?![\d.,/]?\d)")
OTP_KEYWORD_PATTERN = re.compile(
    r"(?:otp|m[ãa] (?:x[áa]c (?:nh[ậa]n|th[ựu]c|minh)|b[ảa]o m[ậa]t|giao d[ịi]ch|k[íi]ch ho[ạa]t)|nh[ậa]p m[ãa])"
    rf"{KEYWORD_GAP}{{0,{OTP_KEYWORD_WINDOW}}}$",
    re.IGNORECASE,
)
ID_CANDIDATE_PATTERN = re.compile(
    r"(?<![\d.,/])(?:\d{12}|\d{9}|\d{3}(?:[ .\-]\d{3}){2,3}|\d{4}(?:[ .\-]\d{4}){2})(?![\d.,/]?\d)"
)
ID_KEYWORD_PATTERN = re.compile(
    rf"(?:cccd|cmnd|c[ăa]n c[ưu][ớo]c|ch[ứu]ng minh|đ[ịi]nh danh|dinh danh){KEYWORD_GAP}{{0,{ID_KEYWORD_WINDOW}}}$",
    re.IGNORECASE,
)
PHONE_PATTERN = re.compile(
    r"(?<![\d.,/])(?:(?:\(\+?84\)|\+?84|0)[ .\-]?(?:\d{9,10}|\d{2,4}(?:[ .\-]\d{3,4}){2,3}|\d(?:[ .\-]\d{2}){4})"
    r"|\(0\d{1,3}\)[ .\-]?\d{3,4}[ .\-]?\d{3,4})(?![\d.,/]?\d)"
)
ACCOUNT_PATTERN = re.compile(
    r"(?<!\d)\d{4}(?:[ .\-]\d{4}){2,3}(?:[ .\-]\d{1,3})?(?!\d)"
    r"|(?<!\d)\d{9,19}(?!\d)(?!\s?(?:đồng|dong|vnđ|vnd|đ|d)(?![^\W\d_]))",
    re.IGNORECASE,
)


def normalize_text(text: str) -> str:
    visible_text = INVISIBLE_CHARACTERS_PATTERN.sub("", unicodedata.normalize("NFC", text))
    return WHITESPACE_PATTERN.sub(" ", visible_text).strip()


def restore_obfuscated_urls(text: str) -> str:
    return DEFANGED_SCHEME_PATTERN.sub(r"http\1://", BRACKETED_DOT_PATTERN.sub(".", text))


def has_url(text: str) -> bool:
    return URL_PLACEHOLDER in text or URL_PATTERN.search(restore_obfuscated_urls(text)) is not None


def mask_email(text: str) -> str:
    return EMAIL_PATTERN.sub(EMAIL_PLACEHOLDER, text)


def mask_url(text: str) -> str:
    def replace(match: re.Match[str]) -> str:
        url = match.group(0)
        trimmed_url = url.rstrip(TRAILING_URL_PUNCTUATION)
        return URL_PLACEHOLDER + url[len(trimmed_url) :]

    return URL_PATTERN.sub(replace, text)


def mask_after_keyword(
    text: str, candidate_pattern: re.Pattern[str], keyword_pattern: re.Pattern[str], placeholder: str
) -> str:
    def replace(match: re.Match[str]) -> str:
        preceding_text = text[: match.start()]
        return placeholder if keyword_pattern.search(preceding_text) else match.group(0)

    return candidate_pattern.sub(replace, text)


def mask_otp(text: str) -> str:
    return mask_after_keyword(text, OTP_CANDIDATE_PATTERN, OTP_KEYWORD_PATTERN, OTP_PLACEHOLDER)


def mask_id(text: str) -> str:
    return mask_after_keyword(text, ID_CANDIDATE_PATTERN, ID_KEYWORD_PATTERN, ID_PLACEHOLDER)


def mask_phone(text: str) -> str:
    return PHONE_PATTERN.sub(PHONE_PLACEHOLDER, text)


def mask_account(text: str) -> str:
    return ACCOUNT_PATTERN.sub(ACCOUNT_PLACEHOLDER, text)


MASKING_STEPS: list[tuple[str, Callable[[str], str]]] = [
    ("email", mask_email),
    ("url", mask_url),
    ("otp", mask_otp),
    ("id", mask_id),
    ("phone", mask_phone),
    ("account", mask_account),
]


def mask(text: str) -> str:
    text = restore_obfuscated_urls(text)
    for _, step in MASKING_STEPS:
        text = step(text)
    return text


def find_maskable_pii(text: str) -> list[str]:
    found: list[str] = []
    text = restore_obfuscated_urls(text)
    for name, step in MASKING_STEPS:
        masked_text = step(text)
        if masked_text != text:
            found.append(name)
        text = masked_text
    return found
