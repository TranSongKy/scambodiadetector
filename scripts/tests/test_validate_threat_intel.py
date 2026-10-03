import json
import tempfile
import unittest
from pathlib import Path

from threat_intel.similarity import fingerprint
from validate_threat_intel import validate_threat_intel

VALID_TEMPLATE = "Tài khoản của quý khách bị tạm khóa, vui lòng truy cập <URL> để xác minh trong 24h"
DOMAIN_HEADER = "domain,source,source_url,first_seen,evidence\n"
TEMPLATE_HEADER = "id,text,source,source_url,first_seen\n"


class ValidateThreatIntelTests(unittest.TestCase):
    def setUp(self) -> None:
        self._directory = tempfile.TemporaryDirectory()
        self.data_dir = Path(self._directory.name)
        (self.data_dir / "allowed_domains.csv").write_text(
            "domain,note\nvietcombank.com.vn,\ngov.vn,\n", encoding="utf-8"
        )
        sources = [
            {"name": "news", "kind": "rss", "url": "https://news.example.vn/rss"},
            {"name": "tay", "kind": "manual"},
        ]
        (self.data_dir / "sources.json").write_text(json.dumps(sources), encoding="utf-8")
        (self.data_dir / "rejected_domains.csv").write_text("domain,reason,rejected_at\n", encoding="utf-8")
        (self.data_dir / "rejected_templates.csv").write_text("fingerprint,reason,rejected_at\n", encoding="utf-8")
        self.write_domains("vcb-xacminh.com,news,https://news.example.vn/a,2026-10-03,Trang giả mạo <URL>\n")
        self.write_templates(f'tpl_000001,"{VALID_TEMPLATE}",news,https://news.example.vn/a,2026-10-03\n')

    def tearDown(self) -> None:
        self._directory.cleanup()

    def write_domains(self, rows: str) -> None:
        (self.data_dir / "blocked_domains.csv").write_text(DOMAIN_HEADER + rows, encoding="utf-8")

    def write_templates(self, rows: str) -> None:
        (self.data_dir / "scam_templates.csv").write_text(TEMPLATE_HEADER + rows, encoding="utf-8")

    def assert_error_contains(self, fragment: str) -> None:
        errors = validate_threat_intel(self.data_dir)
        self.assertTrue(any(fragment in error for error in errors), errors)

    def test_valid_data_passes(self) -> None:
        self.assertEqual([], validate_threat_intel(self.data_dir))

    def test_allowlisted_domain_is_rejected(self) -> None:
        self.write_domains("ebank.vietcombank.com.vn,news,https://n.example/a,2026-10-03,\n")

        self.assert_error_contains("danh sách trắng")

    def test_government_subdomain_is_rejected(self) -> None:
        self.write_domains("dichvucong.gov.vn,news,https://n.example/a,2026-10-03,\n")

        self.assert_error_contains("danh sách trắng")

    def test_public_suffix_is_rejected(self) -> None:
        self.write_domains("com.vn,news,https://n.example/a,2026-10-03,\n")

        self.assert_error_contains("hậu tố công cộng")

    def test_unnormalized_domain_is_rejected(self) -> None:
        self.write_domains("WWW.Lua-Dao.vip,news,https://n.example/a,2026-10-03,\n")

        self.assert_error_contains("chưa chuẩn hóa")

    def test_duplicate_domain_is_rejected(self) -> None:
        row = "lua-dao.vip,news,https://n.example/a,2026-10-03,\n"
        self.write_domains(row + row)

        self.assert_error_contains("bị trùng")

    def test_template_with_phone_is_rejected(self) -> None:
        self.write_templates(
            'tpl_000001,"Quý khách vui lòng gọi 0901234567 để xác minh tài khoản ngay hôm nay",news,,2026-10-03\n'
        )

        self.assert_error_contains("thông tin cá nhân (phone)")

    def test_template_with_defanged_domain_is_rejected(self) -> None:
        self.write_templates(
            'tpl_000001,"Quý khách vui lòng truy cập vcb-xacminh[.]com để xác minh tài khoản ngay",news,,2026-10-03\n'
        )

        self.assert_error_contains("thông tin cá nhân (url)")

    def test_victim_narration_template_is_rejected(self) -> None:
        self.write_templates(
            'tpl_000001,"Tôi rất bất ngờ khi tài khoản mất hết tiền chỉ sau vài phút",news,,2026-10-03\n'
        )

        self.assert_error_contains("không đạt điều kiện văn mẫu")

    def test_near_duplicate_templates_are_rejected(self) -> None:
        self.write_templates(
            f'tpl_000001,"{VALID_TEMPLATE}",news,,2026-10-03\ntpl_000002,"{VALID_TEMPLATE}!",news,,2026-10-03\n'
        )

        self.assert_error_contains("gần trùng")

    def test_bad_template_id_and_date_are_rejected(self) -> None:
        self.write_templates(f'tpl_1,"{VALID_TEMPLATE}",news,,03/10/2026\n')

        errors = validate_threat_intel(self.data_dir)

        self.assertTrue(any("sai định dạng" in error for error in errors))
        self.assertTrue(any("YYYY-MM-DD" in error for error in errors))

    def test_non_https_source_is_rejected(self) -> None:
        (self.data_dir / "sources.json").write_text(
            json.dumps([{"name": "news", "kind": "rss", "url": "http://news.example.vn/rss"}]), encoding="utf-8"
        )

        self.assert_error_contains("phải dùng https")

    def test_wrong_header_is_rejected(self) -> None:
        (self.data_dir / "blocked_domains.csv").write_text("domain\nlua-dao.vip\n", encoding="utf-8")

        self.assert_error_contains("header phải là")

    def test_previously_rejected_domain_is_rejected(self) -> None:
        (self.data_dir / "rejected_domains.csv").write_text(
            "domain,reason,rejected_at\nvcb-xacminh.com,nhầm tên miền thật,2026-10-01\n", encoding="utf-8"
        )

        self.assert_error_contains("đã bị loại trước đây")

    def test_shared_platform_root_is_rejected_but_subdomain_is_allowed(self) -> None:
        self.write_domains("blogspot.com,news,https://n.example/a,2026-10-03,\n")
        self.assert_error_contains("nền tảng dùng chung")

        self.write_domains("thue-gia-mao.blogspot.com,news,https://n.example/a,2026-10-03,\n")
        self.assertEqual([], validate_threat_intel(self.data_dir))

    def test_template_matching_rejected_fingerprint_is_rejected(self) -> None:
        (self.data_dir / "rejected_templates.csv").write_text(
            f"fingerprint,reason,rejected_at\n{fingerprint(VALID_TEMPLATE)},không phải tin lừa đảo thật,2026-10-01\n",
            encoding="utf-8",
        )

        self.assert_error_contains("giống văn mẫu đã bị loại")


if __name__ == "__main__":
    unittest.main()
