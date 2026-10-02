import re
import unittest
from pathlib import Path

from scripts.validate_dataset import URL_TOP_LEVEL_DOMAINS

REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
URL_EXTRACTOR_PATH = REPOSITORY_ROOT / "src" / "ScamDetector.Core" / "Urls" / "UrlExtractor.cs"
CSHARP_TLD_GROUP = re.compile(r"\\\.\)\+\(\?:([a-z|]+)\)")


class RegexParityTests(unittest.TestCase):
    def test_url_top_level_domains_match_csharp_url_extractor(self) -> None:
        csharp_source = URL_EXTRACTOR_PATH.read_text(encoding="utf-8")
        match = CSHARP_TLD_GROUP.search(csharp_source)

        self.assertIsNotNone(match, "Không tìm thấy danh sách TLD trong UrlExtractor.cs")
        assert match is not None
        self.assertEqual(set(match.group(1).split("|")), set(URL_TOP_LEVEL_DOMAINS.split("|")))


if __name__ == "__main__":
    unittest.main()
