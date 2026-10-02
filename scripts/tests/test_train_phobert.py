import csv
import tempfile
import unittest
from pathlib import Path

from train_phobert import LABELS, Metrics, Split, compute_metrics, f1_for_label, format_experiment_row, load_split

NORMAL, SPAM, SCAM = (LABELS.index(label) for label in ("normal", "spam", "scam"))


class MetricsTests(unittest.TestCase):
    def test_perfect_predictions_score_one(self) -> None:
        expected = [NORMAL, SPAM, SCAM, SCAM]

        metrics = compute_metrics(expected, expected)

        self.assertEqual(Metrics(macro_f1=1.0, scam_f1=1.0, accuracy=1.0), metrics)

    def test_f1_for_label_matches_hand_computation(self) -> None:
        predicted = [SCAM, SCAM, NORMAL, SCAM]
        expected = [SCAM, NORMAL, SCAM, SCAM]

        f1 = f1_for_label(predicted, expected, SCAM)

        self.assertAlmostEqual(2 / 3, f1)

    def test_label_never_predicted_scores_zero(self) -> None:
        self.assertEqual(0.0, f1_for_label([NORMAL, NORMAL], [SPAM, NORMAL], SPAM))

    def test_macro_f1_averages_all_labels(self) -> None:
        metrics = compute_metrics([NORMAL, NORMAL, NORMAL], [NORMAL, SPAM, SCAM])

        self.assertAlmostEqual((0.5 + 0 + 0) / 3, metrics.macro_f1)
        self.assertAlmostEqual(1 / 3, metrics.accuracy)


class LoadSplitTests(unittest.TestCase):
    def test_texts_are_normalized_and_masked_and_labels_mapped(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            split_path = Path(directory) / "train.csv"
            with split_path.open("w", encoding="utf-8", newline="") as split_file:
                writer = csv.DictWriter(split_file, fieldnames=["id", "text", "label"])
                writer.writeheader()
                writer.writerow({"id": "msg_000001", "text": "  Vao  bit.ly/x  ", "label": "scam"})
                writer.writerow({"id": "msg_000002", "text": "Mai hop nhe", "label": "normal"})

            split = load_split(split_path)

        self.assertEqual(Split(texts=["Vao <URL>", "Mai hop nhe"], label_ids=[SCAM, NORMAL]), split)


class ExperimentRowTests(unittest.TestCase):
    def test_row_lists_split_sizes_and_scores(self) -> None:
        splits = {
            "train": Split(["a", "b"], [0, 1]),
            "val": Split(["c"], [2]),
            "test": Split(["d"], [0]),
        }

        row = format_experiment_row("phobert", splits, "lr=2e-05", Metrics(0.91234, 0.88, 0.9))

        self.assertIn("| phobert | 2/1/1 | lr=2e-05 | 0.9123 | 0.8800 |", row)


if __name__ == "__main__":
    unittest.main()
