import re

DEFANG_REPLACEMENTS = (
    (re.compile(r"\[\.\]|\(\.\)|\{\.\}|\[dot\]|\(dot\)", re.IGNORECASE), "."),
    (re.compile(r"\bhxxps?", re.IGNORECASE), "https"),
    (re.compile(r"\[:\]"), ":"),
)
DOMAIN_PATTERN = re.compile(
    r"(?:https?://)?(?:www\.)?((?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,24})(?![a-z0-9-])",
    re.IGNORECASE,
)
WARNING_CONTEXT_PATTERN = re.compile(
    r"gi[ảa] m[ạa]o|l[ừu]a [đd][ảa]o|m[ạa]o danh|gi[ảa] danh|trang (?:web|website|m[ạa]ng)|"
    r"[đd][ưu][ờo]ng (?:link|d[ẫa]n)|t[êe]n mi[ềe]n|website|truy c[ậa]p",
    re.IGNORECASE,
)
MAX_DOMAIN_LENGTH = 253
KNOWN_TOP_LEVEL_DOMAINS = frozenset(
    [
        "com",
        "net",
        "org",
        "info",
        "biz",
        "vn",
        "xyz",
        "top",
        "me",
        "ly",
        "io",
        "cc",
        "online",
        "site",
        "shop",
        "link",
        "club",
        "icu",
        "vip",
        "live",
        "buzz",
        "tk",
        "ml",
        "ga",
        "cf",
        "gq",
        "gl",
        "gd",
        "id",
        "at",
        "gy",
        "co",
        "app",
        "dev",
        "cn",
        "ru",
        "uk",
        "us",
        "asia",
        "pro",
        "store",
        "tech",
        "fun",
        "space",
        "website",
        "win",
        "bid",
        "loan",
        "work",
        "click",
        "cyou",
        "sbs",
        "cfd",
        "bond",
        "lol",
        "mobi",
        "tv",
        "in",
        "ph",
        "th",
        "sg",
        "my",
        "kr",
        "jp",
        "hk",
        "tw",
        "au",
        "nz",
        "de",
        "fr",
        "nl",
        "eu",
        "ca",
        "be",
        "ch",
        "es",
        "it",
        "pl",
        "br",
        "mx",
        "cloud",
        "today",
        "world",
        "news",
        "life",
        "one",
        "host",
        "press",
        "agency",
        "finance",
        "money",
        "bet",
        "casino",
        "games",
        "email",
        "digital",
        "network",
        "center",
        "group",
        "company",
        "services",
        "solutions",
        "support",
        "help",
        "global",
        "social",
        "kim",
        "ink",
        "pw",
        "su",
        "lat",
        "rest",
        "monster",
        "beauty",
        "homes",
        "autos",
        "quest",
        "best",
        "xin",
        "wang",
        "ltd",
        "vin",
        "mom",
        "pics",
        "zone",
        "tokyo",
        "cam",
        "fit",
        "run",
        "red",
        "blue",
        "pink",
        "gold",
        "plus",
        "team",
        "trade",
        "market",
        "exchange",
        "investments",
        "capital",
        "fund",
        "cash",
        "credit",
        "bank",
    ]
)
SHARED_PLATFORM_ROOTS = frozenset(
    {
        "blogspot.com",
        "wordpress.com",
        "github.io",
        "gitlab.io",
        "web.app",
        "firebaseapp.com",
        "vercel.app",
        "netlify.app",
        "pages.dev",
        "workers.dev",
        "herokuapp.com",
        "glitch.me",
        "weebly.com",
        "wixsite.com",
        "000webhostapp.com",
        "sites.google.com",
        "forms.gle",
        "linktr.ee",
        "ngrok.io",
        "ngrok-free.app",
        "azurewebsites.net",
        "cloudfront.net",
        "amazonaws.com",
        "web.core.windows.net",
        "notion.site",
    }
)


def refang(text: str) -> str:
    for pattern, replacement in DEFANG_REPLACEMENTS:
        text = pattern.sub(replacement, text)
    return text


def normalize_domain(value: str) -> str | None:
    candidate = value.strip().lower().rstrip(".")
    candidate = re.sub(r"^(?:https?://)?(?:www\.)?", "", candidate).split("/")[0].split(":")[0]
    if not candidate or len(candidate) > MAX_DOMAIN_LENGTH or "." not in candidate:
        return None
    if candidate.rsplit(".", 1)[1] not in KNOWN_TOP_LEVEL_DOMAINS:
        return None
    try:
        return candidate.encode("idna").decode("ascii")
    except UnicodeError:
        return None


def extract_domains(text: str) -> set[str]:
    domains = (normalize_domain(match.group(1)) for match in DOMAIN_PATTERN.finditer(refang(text)))
    return {domain for domain in domains if domain}


def extract_warned_domains(paragraphs: list[str]) -> dict[str, str]:
    warned: dict[str, str] = {}
    for paragraph in paragraphs:
        if WARNING_CONTEXT_PATTERN.search(paragraph):
            for domain in extract_domains(paragraph):
                warned.setdefault(domain, paragraph)
    return warned


def is_allowed(domain: str, allowlist: set[str]) -> bool:
    labels = domain.split(".")
    return any(".".join(labels[index:]) in allowlist for index in range(len(labels)))
