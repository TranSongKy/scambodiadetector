import argparse
import sys
from pathlib import Path

from make_tokenizer_golden import SAMPLE_TEXTS
from train_phobert import (
    BASE_MODEL_NAME,
    ONNX_PARITY_TOLERANCE,
    SEED,
    export_onnx,
    load_base_model,
    predict,
    prepare_text,
    set_seed,
    verify_onnx_parity,
    write_tokenizer_golden,
)


def parse_arguments(arguments: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Export PhoBERT với head phân loại chưa train để kiểm tra hợp đồng ONNX với backend."
    )
    parser.add_argument("output_dir", type=Path, help="Nơi ghi scam-detector.onnx, vocab.txt, bpe.codes")
    parser.add_argument("--base-model", default=BASE_MODEL_NAME)
    return parser.parse_args(arguments)


def main(arguments: list[str]) -> int:
    options = parse_arguments(arguments)
    set_seed(SEED)
    options.output_dir.mkdir(parents=True, exist_ok=True)
    model, tokenizer = load_base_model(options.base_model)
    model.eval()

    texts = [prepare_text(text) for text in SAMPLE_TEXTS]
    expected_logits = predict(model, tokenizer, texts)
    onnx_path = export_onnx(model, tokenizer, options.output_dir)
    difference = verify_onnx_parity(onnx_path, tokenizer, texts, expected_logits)
    write_tokenizer_golden(tokenizer, texts, options.output_dir)

    print(f"Chênh lệch logits PyTorch/ONNX tối đa: {difference:.6f}")
    return 0 if difference <= ONNX_PARITY_TOLERANCE else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
