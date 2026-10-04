import re
from dataclasses import dataclass

AI_TRAINING_USER_AGENTS = frozenset(
    {
        "gptbot",
        "ccbot",
        "claudebot",
        "anthropic-ai",
        "google-extended",
        "applebot-extended",
        "bytespider",
        "meta-externalagent",
    }
)
CONTENT_SIGNAL_PATTERN = re.compile(r"^\s*content-signal\s*:\s*(.+)$", re.IGNORECASE | re.MULTILINE)
DIRECTIVE_PATTERN = re.compile(r"^\s*(user-agent|disallow|allow)\s*:\s*(.*?)\s*(?:#.*)?$", re.IGNORECASE)


@dataclass(frozen=True)
class TrainingUseDecision:
    allowed: bool
    reason: str


def _content_signal_forbids_training(robots_text: str) -> bool:
    for match in CONTENT_SIGNAL_PATTERN.finditer(robots_text):
        signals = dict(part.strip().lower().split("=", 1) for part in match.group(1).split(",") if "=" in part)
        if signals.get("ai-train") == "no":
            return True
    return False


def _blocked_ai_training_agents(robots_text: str) -> set[str]:
    blocked: set[str] = set()
    group_agents: list[str] = []
    reading_agents = False
    for line in robots_text.splitlines():
        match = DIRECTIVE_PATTERN.match(line)
        if not match:
            continue
        directive, value = match.group(1).lower(), match.group(2).strip()
        if directive == "user-agent":
            if not reading_agents:
                group_agents = []
            group_agents.append(value.lower())
            reading_agents = True
            continue
        reading_agents = False
        if directive == "disallow" and value == "/":
            blocked.update(agent for agent in group_agents if agent in AI_TRAINING_USER_AGENTS)
    return blocked


def decide_training_use(robots_text: str) -> TrainingUseDecision:
    if _content_signal_forbids_training(robots_text):
        return TrainingUseDecision(False, "robots.txt khai báo Content-Signal ai-train=no")
    blocked = _blocked_ai_training_agents(robots_text)
    if blocked:
        return TrainingUseDecision(False, f"robots.txt chặn bot huấn luyện AI: {', '.join(sorted(blocked))}")
    return TrainingUseDecision(True, "robots.txt không hạn chế huấn luyện AI")
