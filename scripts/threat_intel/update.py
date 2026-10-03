from dataclasses import dataclass, field

from threat_intel.crawler import SourceResult
from threat_intel.domains import SHARED_PLATFORM_ROOTS, is_allowed
from threat_intel.similarity import matches_fingerprint
from threat_intel.store import BlockedDomain, ScamTemplate, format_template_id, next_template_id
from threat_intel.templates import DUPLICATE_JACCARD, candidate_templates


@dataclass
class UpdateResult:
    domains: list[BlockedDomain]
    templates: list[ScamTemplate]
    new_domains: list[BlockedDomain] = field(default_factory=list)
    new_templates: list[ScamTemplate] = field(default_factory=list)
    skipped_allowed: list[str] = field(default_factory=list)
    skipped_rejected: int = 0


def merge_domains(
    existing: list[BlockedDomain],
    results: list[SourceResult],
    allowlist: set[str],
    rejected: set[str],
    today: str,
    update: UpdateResult,
) -> None:
    known = {record.domain for record in existing}
    for result in results:
        for domain, sighting in sorted(result.domains.items()):
            if domain in known:
                continue
            if domain in rejected or domain in SHARED_PLATFORM_ROOTS:
                update.skipped_rejected += 1
                continue
            if is_allowed(domain, allowlist):
                update.skipped_allowed.append(domain)
                continue
            known.add(domain)
            update.new_domains.append(
                BlockedDomain(domain, result.source, sighting.source_url, today, sighting.evidence)
            )
    update.domains = sorted(existing + update.new_domains, key=lambda record: record.domain)


def merge_templates(
    existing: list[ScamTemplate], results: list[SourceResult], rejected: list[str], today: str, update: UpdateResult
) -> None:
    known_texts = [template.text for template in existing]
    next_number = next_template_id(existing)
    for result in results:
        for source_url, texts in _group_by_url(result.texts).items():
            for text in candidate_templates(texts, known_texts):
                known_texts.append(text)
                if matches_fingerprint(text, rejected, DUPLICATE_JACCARD):
                    update.skipped_rejected += 1
                    continue
                update.new_templates.append(
                    ScamTemplate(format_template_id(next_number), text, result.source, source_url, today)
                )
                next_number += 1
    update.templates = existing + update.new_templates


def _group_by_url(texts: list[tuple[str, str]]) -> dict[str, list[str]]:
    grouped: dict[str, list[str]] = {}
    for text, source_url in texts:
        grouped.setdefault(source_url, []).append(text)
    return grouped


def build_update(
    existing_domains: list[BlockedDomain],
    existing_templates: list[ScamTemplate],
    results: list[SourceResult],
    allowlist: set[str],
    today: str,
    rejected_domains: set[str] | None = None,
    rejected_templates: list[str] | None = None,
) -> UpdateResult:
    update = UpdateResult(domains=existing_domains, templates=existing_templates)
    merge_domains(existing_domains, results, allowlist, rejected_domains or set(), today, update)
    merge_templates(existing_templates, results, rejected_templates or [], today, update)
    return update
