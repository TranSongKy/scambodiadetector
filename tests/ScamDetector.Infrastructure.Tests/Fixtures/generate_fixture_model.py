import sys
from pathlib import Path

import onnx
from onnx import TensorProto, helper

LABEL_COUNT = 3
OPSET_VERSION = 17


def build_fixture_model() -> onnx.ModelProto:
    input_ids = helper.make_tensor_value_info("input_ids", TensorProto.INT64, [1, "sequence"])
    attention_mask = helper.make_tensor_value_info("attention_mask", TensorProto.INT64, [1, "sequence"])
    logits = helper.make_tensor_value_info("logits", TensorProto.FLOAT, [1, LABEL_COUNT])

    weights = helper.make_tensor("weights", TensorProto.FLOAT, [1, LABEL_COUNT], [0.0, 0.0, 1.0])
    bias = helper.make_tensor("bias", TensorProto.FLOAT, [LABEL_COUNT], [1.0, 0.0, 0.0])

    nodes = [
        helper.make_node("Mul", ["input_ids", "attention_mask"], ["masked_ids"]),
        helper.make_node("Cast", ["masked_ids"], ["masked_ids_float"], to=TensorProto.FLOAT),
        helper.make_node("ReduceMax", ["masked_ids_float"], ["max_id"], axes=[1], keepdims=1),
        helper.make_node("MatMul", ["max_id", "weights"], ["scaled"]),
        helper.make_node("Add", ["scaled", "bias"], ["logits"]),
    ]
    graph = helper.make_graph(nodes, "fixture", [input_ids, attention_mask], [logits], [weights, bias])
    model = helper.make_model(graph, opset_imports=[helper.make_opsetid("", OPSET_VERSION)])
    model.ir_version = 8
    onnx.checker.check_model(model)
    return model


def main() -> int:
    output_path = Path(sys.argv[1]) if len(sys.argv) == 2 else Path(__file__).with_name("fixture-model.onnx")
    onnx.save(build_fixture_model(), output_path)
    print(f"Đã ghi {output_path}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
