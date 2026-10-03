import tempfile
import unittest
from pathlib import Path

from validate_dataset import find_pii, has_url, validate_dataset

from scripts.tests.dataset_rows import make_balanced_rows, make_row, write_dataset


class FindPiiTests(unittest.TestCase):
    def test_separated_phone_is_detected(self) -> None:
        self.assertIn("phone", find_pii("Goi 090 123 4567 ngay"))

    def test_international_phone_is_detected(self) -> None:
        self.assertIn("phone", find_pii("Lien he +84 901 234 567"))

    def test_date_and_time_are_not_phone(self) -> None:
        self.assertEqual([], find_pii("Han 01.02.2026 10.30 den"))

    def test_dotted_amount_is_not_pii(self) -> None:
        self.assertEqual([], find_pii("Chuyen 500.000.000d truoc 5h"))

    def test_otp_after_keyword_is_detected(self) -> None:
        self.assertIn("otp", find_pii("Mã xác minh 8472 nhé"))

    def test_card_number_is_detected(self) -> None:
        self.assertIn("account", find_pii("The 1234 5678 9012 3456"))

    def test_raw_url_is_detected(self) -> None:
        self.assertIn("url", find_pii("Vao vcb.vip/x ngay"))

    def test_placeholders_are_not_pii(self) -> None:
        self.assertEqual([], find_pii("Goi <PHONE> hoac vao <URL> nhap <OTP>"))


class HasUrlTests(unittest.TestCase):
    def test_placeholder_counts_as_url(self) -> None:
        self.assertTrue(has_url("Vao <URL> ngay"))

    def test_plain_text_has_no_url(self) -> None:
        self.assertFalse(has_url("Mai hop nhom nhe"))


class ValidateDatasetTests(unittest.TestCase):
    def setUp(self) -> None:
        self._directory = tempfile.TemporaryDirectory()
        self.root = Path(self._directory.name)

    def tearDown(self) -> None:
        self._directory.cleanup()

    def test_balanced_dataset_is_valid(self) -> None:
        dataset = write_dataset(self.root / "dataset.csv", make_balanced_rows(per_label=5))

        self.assertEqual([], validate_dataset(dataset))

    def test_empty_dataset_is_reported(self) -> None:
        dataset = write_dataset(self.root / "dataset.csv", [])

        self.assertEqual(["Dataset rỗng"], validate_dataset(dataset))

    def test_bom_is_reported(self) -> None:
        dataset = self.root / "dataset.csv"
        dataset.write_text("﻿id,text\n", encoding="utf-8")

        self.assertIn("BOM", validate_dataset(dataset)[0])

    def test_scam_without_scam_type_is_reported(self) -> None:
        rows = make_balanced_rows(per_label=5)
        rows[0] = {**make_row(1, "scam"), "scam_type": ""}
        dataset = write_dataset(self.root / "dataset.csv", rows)

        self.assertTrue(any("scam_type" in error for error in validate_dataset(dataset)))

    def test_duplicate_text_ignoring_case_and_spaces_is_reported(self) -> None:
        rows = make_balanced_rows(per_label=5)
        rows.append({**rows[0], "id": "msg_000999", "text": "  " + rows[0]["text"].upper() + "  "})
        dataset = write_dataset(self.root / "dataset.csv", rows)

        self.assertTrue(any("trùng" in error for error in validate_dataset(dataset)))

    def test_missing_column_in_row_does_not_crash(self) -> None:
        dataset = self.root / "dataset.csv"
        dataset.write_text(
            "id,text,label,scam_type,channel,source,has_url,collected_at,annotator\nmsg_000001,thieu cot\n",
            encoding="utf-8",
        )

        self.assertTrue(any("thiếu cột" in error for error in validate_dataset(dataset)))

    def test_synthetic_over_limit_is_reported(self) -> None:
        rows = [{**row, "source": "synthetic"} for row in make_balanced_rows(per_label=5)]
        dataset = write_dataset(self.root / "dataset.csv", rows)

        self.assertTrue(any("synthetic" in error for error in validate_dataset(dataset)))

    def test_collecting_mode_skips_distribution_but_keeps_row_checks(self) -> None:
        rows = [{**row, "source": "synthetic"} for row in make_balanced_rows(per_label=5) if row["label"] == "scam"]
        dataset = write_dataset(self.root / "dataset.csv", rows)
        broken = write_dataset(self.root / "broken.csv", [{**rows[0], "label": "lua"}])

        self.assertTrue(validate_dataset(dataset))
        self.assertEqual([], validate_dataset(dataset, enforce_distribution=False))
        self.assertTrue(validate_dataset(broken, enforce_distribution=False))

    def test_unknown_placeholder_is_reported(self) -> None:
        rows = make_balanced_rows(per_label=5)
        rows[1] = {**rows[1], "text": rows[1]["text"] + " <FOO>"}
        dataset = write_dataset(self.root / "dataset.csv", rows)

        self.assertTrue(any("<FOO>" in error for error in validate_dataset(dataset)))


if __name__ == "__main__":
    unittest.main()
