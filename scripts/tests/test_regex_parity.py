import json
import re
import unittest
from pathlib import Path

from masking import INVISIBLE_CHARACTERS_PATTERN, URL_TOP_LEVEL_DOMAINS, mask, normalize_text

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
URL_EXTRACTOR_PATH = REPOSITORY_ROOT / "src" / "ScamDetector.Core" / "Urls" / "UrlExtractor.cs"
MASKING_CASES_PATH = REPOSITORY_ROOT / "tests" / "shared" / "masking-cases.json"
TEXT_NORMALIZER_PATH = REPOSITORY_ROOT / "src" / "ScamDetector.Core" / "Text" / "TextNormalizer.cs"
CSHARP_TLD_GROUP = re.compile(r"\\\.\)\+\(\?-i:\(\?:([a-z|]+)\)")
CSHARP_INVISIBLE_CHARACTERS = re.compile(
    r'GeneratedRegex\("(\[[^"]+\])"\)\]\s+private static partial Regex InvisibleCharacters'
)


class RegexParityTests(unittest.TestCase):
    def test_url_top_level_domains_match_csharp_url_extractor(self) -> None:
        csharp_source = URL_EXTRACTOR_PATH.read_text(encoding="utf-8")
        match = CSHARP_TLD_GROUP.search(csharp_source)

        self.assertIsNotNone(match, "Không tìm thấy danh sách TLD trong UrlExtractor.cs")
        assert match is not None
        self.assertEqual(set(match.group(1).split("|")), set(URL_TOP_LEVEL_DOMAINS.split("|")))

    def test_invisible_characters_match_csharp_text_normalizer(self) -> None:
        csharp_source = TEXT_NORMALIZER_PATH.read_text(encoding="utf-8")
        match = CSHARP_INVISIBLE_CHARACTERS.search(csharp_source)

        self.assertIsNotNone(match, "Không tìm thấy InvisibleCharacters trong TextNormalizer.cs")
        assert match is not None
        csharp_class = match.group(1).encode("ascii").decode("unicode_escape").lower()
        self.assertEqual(csharp_class, INVISIBLE_CHARACTERS_PATTERN.pattern.lower())

    def test_normalize_text_removes_invisible_characters(self) -> None:
        self.assertEqual("vtp-vandon.online ngay", normalize_text("vtp\u200b-van\u00addon.online \ufeff ngay"))

    def test_python_mask_matches_shared_cases(self) -> None:
        cases = json.loads(MASKING_CASES_PATH.read_text(encoding="utf-8"))

        for case in cases:
            with self.subTest(text=case["input"]):
                self.assertEqual(case["expected"], mask(case["input"]))

    def test_python_mask_is_idempotent_on_shared_cases(self) -> None:
        cases = json.loads(MASKING_CASES_PATH.read_text(encoding="utf-8"))

        for case in cases:
            with self.subTest(text=case["expected"]):
                self.assertEqual(case["expected"], mask(case["expected"]))


if __name__ == "__main__":
    unittest.main()
