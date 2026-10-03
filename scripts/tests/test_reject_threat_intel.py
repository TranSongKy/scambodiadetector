import tempfile
import unittest
from pathlib import Path

from reject_threat_intel import main
from threat_intel.similarity import fingerprint
from threat_intel.store import load_blocked_domains, load_rejected_domains, load_rejected_templates, load_templates

TEMPLATE = "Tài khoản của quý khách bị tạm khóa, vui lòng truy cập <URL> để xác minh trong 24h"


class RejectThreatIntelTests(unittest.TestCase):
    def setUp(self) -> None:
        self._directory = tempfile.TemporaryDirectory()
        self.data_dir = Path(self._directory.name)
        (self.data_dir / "blocked_domains.csv").write_text(
            "domain,source,source_url,first_seen,evidence\nlua-dao.vip,news,,2026-10-03,\nkhac.top,news,,2026-10-03,\n",
            encoding="utf-8",
        )
        (self.data_dir / "scam_templates.csv").write_text(
            f'id,text,source,source_url,first_seen\ntpl_000001,"{TEMPLATE}",news,,2026-10-03\n', encoding="utf-8"
        )

    def tearDown(self) -> None:
        self._directory.cleanup()

    def run_main(self, *arguments: str) -> int:
        return main([*arguments, "--data-dir", str(self.data_dir)])

    def test_rejecting_domain_moves_it_to_rejected_list(self) -> None:
        exit_code = self.run_main("--domain", "lua-dao.vip", "--reason", "tên miền của báo, không phải lừa đảo")

        self.assertEqual(0, exit_code)
        self.assertEqual(["khac.top"], [record.domain for record in load_blocked_domains(self.data_dir)])
        rejected = load_rejected_domains(self.data_dir)
        self.assertEqual(
            ("lua-dao.vip", "tên miền của báo, không phải lừa đảo"), (rejected[0].domain, rejected[0].reason)
        )

    def test_rejecting_template_stores_only_a_fingerprint(self) -> None:
        exit_code = self.run_main("--template", "tpl_000001", "--reason", "lời khuyên của công an, không phải tin lừa")

        self.assertEqual(0, exit_code)
        self.assertEqual([], load_templates(self.data_dir))
        stored = (self.data_dir / "rejected_templates.csv").read_text(encoding="utf-8")
        self.assertEqual(fingerprint(TEMPLATE), load_rejected_templates(self.data_dir)[0].fingerprint)
        self.assertNotIn("quý khách", stored)

    def test_unknown_item_fails_without_changes(self) -> None:
        exit_code = self.run_main("--domain", "khong-co.vip", "--reason", "không tồn tại trong danh sách")

        self.assertEqual(1, exit_code)
        self.assertEqual(2, len(load_blocked_domains(self.data_dir)))

    def test_short_reason_is_refused(self) -> None:
        self.assertEqual(2, self.run_main("--domain", "lua-dao.vip", "--reason", "sai"))
        self.assertEqual(2, len(load_blocked_domains(self.data_dir)))


if __name__ == "__main__":
    unittest.main()
