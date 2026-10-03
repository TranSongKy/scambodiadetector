import re

from masking import mask, normalize_text
from threat_intel.domains import refang
from threat_intel.similarity import jaccard, tokenize

MIN_TEMPLATE_TOKENS = 8
MAX_TEMPLATE_CHARACTERS = 600
MAX_EVIDENCE_CHARACTERS = 240
DUPLICATE_JACCARD = 0.8
QUOTE_PATTERN = re.compile(r"[“\"«]([^”\"»]{30,600})[”\"»]")
SCAM_SIGNAL_PATTERN = re.compile(
    r"chuy[ểe]n (?:ti[ềe]n|kho[ảa]n)|\botp\b|m[ãa] x[áa]c|t[àa]i kho[ảa]n|tr[úu]ng th[ưu][ởo]ng|qu[àa] t[ặa]ng|"
    r"<URL>|<PHONE>|<ACCOUNT>|<OTP>|nh[ấa]n (?:v[àa]o|link)|truy c[ậa]p|x[áa]c (?:minh|th[ựu]c)|kh[óo]a|ph[ạa]t|"
    r"vi[ệe]c nh[ẹe]|c[ộo]ng t[áa]c vi[êe]n|nhi[ệe]m v[ụu]|vay|gi[ảa]i ng[âa]n|ph[íi]|n[ạa]p",
    re.IGNORECASE,
)
CALL_TO_ACTION_PATTERN = re.compile(
    r"\b(?:b[ạa]n|qu[ýy] kh[áa]ch|anh|ch[ịi]|em|vui l[òo]ng|h[ãa]y|nh[ấa]n|b[ấa]m|click|truy c[ậa]p|chuy[ểe]n|n[ạa]p|"
    r"cung c[ấa]p|[đd][ọo]c|g[ửu]i|li[êe]n h[ệe]|[đd][ăa]ng (?:k[ýy]|nh[ậa]p)|nh[ậa]n|x[áa]c (?:minh|nh[ậa]n|th[ựu]c)|"
    r"tham gia|k[ếe]t b[ạa]n|t[ảa]i|c[àa]i)\b",
    re.IGNORECASE,
)
ADVICE_OR_NARRATION_PATTERN = re.compile(
    r"^(?:ng[ưu][ờo]i d[âa]n|m[ọo]i ng[ưu][ờo]i|c[ôo]ng an|ng[âa]n h[àa]ng khuy[ếe]n c[áa]o|"
    r"t[ôo]i (?:đ[ãa]|r[ấa]t|c[ũu]ng)|nhi[ềe]u ng[ưu][ờo]i)"
    r"|khuy[ếe]n c[áa]o|tuy[ệe]t [đd][ốo]i kh[ôo]ng (?:truy c[ậa]p|cung c[ấa]p|chuy[ểe]n)|kh[ôo]ng n[êe]n|"
    r"\bcho bi[ếe]t\b|\bk[ểe] l[ạa]i\b|b[ịi] (?:l[ừu]a|chi[ếe]m [đd]o[ạa]t)",
    re.IGNORECASE,
)
REPORTER_LANGUAGE_PATTERN = re.compile(
    r"c[áa]c [đd][ốo]i t[ưu][ợo]ng|[đd][ấa]u tranh|tri[ệe]t [đd][ểe]|b[ịi] (?:can|c[áa]o)|n[ạa]n nh[âa]n|"
    r"th[ủu] [đd]o[ạa]n|ph[ưu][ơo]ng th[ứu]c|chi[êe]u tr[òo]|[đd][ưu][ờo]ng d[âa]y",
    re.IGNORECASE,
)
VIETNAMESE_LETTER_PATTERN = re.compile(r"[ăâđêôơưàảãạáằẳẵặắầẩẫậấèẻẽẹéềểễệếìỉĩịíòỏõọóồổỗộốờởỡợớùủũụúừửữựứỳỷỹỵý]", re.I)


def extract_quotes(paragraphs: list[str]) -> list[str]:
    return [match.group(1).strip() for paragraph in paragraphs for match in QUOTE_PATTERN.finditer(paragraph)]


def to_template(text: str) -> str:
    return mask(normalize_text(refang(text)))


def is_template_candidate(template: str) -> bool:
    return (
        len(template) <= MAX_TEMPLATE_CHARACTERS
        and len(tokenize(template)) >= MIN_TEMPLATE_TOKENS
        and SCAM_SIGNAL_PATTERN.search(template) is not None
        and CALL_TO_ACTION_PATTERN.search(template) is not None
        and ADVICE_OR_NARRATION_PATTERN.search(template) is None
        and REPORTER_LANGUAGE_PATTERN.search(template) is None
        and VIETNAMESE_LETTER_PATTERN.search(template) is not None
    )


def is_duplicate(template: str, existing: list[str]) -> bool:
    return any(jaccard(template, known) >= DUPLICATE_JACCARD for known in existing)


def candidate_templates(raw_texts: list[str], existing: list[str]) -> list[str]:
    accepted: list[str] = []
    for raw_text in raw_texts:
        template = to_template(raw_text)
        if is_template_candidate(template) and not is_duplicate(template, existing + accepted):
            accepted.append(template)
    return accepted


def evidence_excerpt(paragraph: str) -> str:
    masked = to_template(paragraph)
    return masked if len(masked) <= MAX_EVIDENCE_CHARACTERS else masked[: MAX_EVIDENCE_CHARACTERS - 1] + "…"
