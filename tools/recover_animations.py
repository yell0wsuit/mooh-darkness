#!/usr/bin/env python3
"""Recover the original game's animation state tables from its Python 2.3 bytecode.

The recovered `.pyo` modules build every sprite state table with list comprehensions such as
`[ 'OWlkLeft%2.2i' % x for x in xrange(10, 26) ]`. The decompiler that produced `mooh_src/` lost
every comprehension body, which is exactly where the frame names live, but the bytecode is
intact. This tool runs a small symbolic interpreter over that bytecode: constants, `xrange`,
string formatting and comprehension loops evaluate concretely, while everything that would need
the game's runtime (pages, classes, engine constants) stays a symbolic node. The result is a
JSON dump of every function's recorded attribute assignments and return value.

It never executes the original code. It needs `xdis` (`pip install xdis`).

Usage: recover_animations.py <mooh_extracted directory> <output json>
"""

import json
import sys
from pathlib import Path

import xdis.load
import xdis.op_imports
from xdis.bytecode import Bytecode

MODULES = [
    "Game", "GamePlayer", "GameObjs", "GameObjBases", "GameObjMonGr", "GameObjMonBlue",
    "GameObjBio", "GameObjSmBeetle", "GameObjTPort", "GameCellPipes", "GameCells",
    "GameObjWeap", "GameObjWaterDrop", "GameObjAntiGArrow", "GameObjMonCanon", "GameBack",
]


class Sym:
    """A value the interpreter cannot know: a name, an attribute or a call."""

    def __init__(self, kind, **fields):
        self.kind = kind
        self.fields = fields

    def to_json(self):
        if self.kind == "name":
            return {"name": self.fields["name"]}
        if self.kind == "attr":
            base = to_json(self.fields["obj"])
            if isinstance(base, dict) and set(base) == {"name"}:
                return {"name": base["name"] + "." + self.fields["attr"]}
            return {"attr": self.fields["attr"], "of": base}
        if self.kind == "call":
            func = to_json(self.fields["func"])
            result = {"call": func["name"] if isinstance(func, dict) and set(func) == {"name"} else func,
                      "args": [to_json(a) for a in self.fields["args"]]}
            if self.fields["kwargs"]:
                result["kwargs"] = {k: to_json(v) for k, v in self.fields["kwargs"].items()}
            return result
        if self.kind == "subscr":
            return {"index": to_json(self.fields["index"]), "of": to_json(self.fields["obj"])}
        if self.kind == "binop":
            return {"op": self.fields["op"], "left": to_json(self.fields["left"]),
                    "right": to_json(self.fields["right"])}
        if self.kind == "lambda":
            return {"lambda": self.fields["name"]}
        if self.kind == "unknown":
            return {"unknown": self.fields["why"]}
        return {"sym": self.kind}


class Appender:
    """The bound `append` a 2.3 comprehension stores in `_[1]`."""

    def __init__(self, target):
        self.target = target


def to_json(value):
    if isinstance(value, Sym):
        return value.to_json()
    if isinstance(value, Appender):
        return {"sym": "appender"}
    if isinstance(value, dict):
        return {"dict": [[to_json(k), to_json(v)] for k, v in value.items()]}
    if isinstance(value, (list, tuple)):
        return [to_json(v) for v in value]
    if isinstance(value, bytes):
        return value.decode("latin1")
    if isinstance(value, (str, int, float, bool)) or value is None:
        return value
    if hasattr(value, "co_name"):
        return {"code": value.co_name}
    return {"repr": repr(value)}


class HashableKey:
    """Lets a symbolic value key a dict."""

    def __init__(self, value):
        self.value = value
        self.text = json.dumps(to_json(value), sort_keys=True)

    def __hash__(self):
        return hash(self.text)

    def __eq__(self, other):
        return isinstance(other, HashableKey) and other.text == self.text


def key(value):
    try:
        hash(value)
        return value
    except TypeError:
        return HashableKey(value)


def is_concrete(value):
    return not isinstance(value, (Sym, Appender))


def call_counts(inst):
    arg = inst.arg or 0
    return arg & 0xFF, (arg >> 8) & 0xFF


def run(code, module_globals, local_values=None, limit=200000):
    """Interpret one code object. Returns (return value, attribute stores, local env)."""
    instructions = list(Bytecode(code, OPC))
    by_offset = {inst.offset: i for i, inst in enumerate(instructions)}
    env = dict(local_values or {})
    stack = []
    stores = {}
    blocks = []
    pc = 0
    steps = 0

    def load_name(name):
        if name in env:
            return env[name]
        if name in module_globals:
            return module_globals[name]
        return Sym("name", name=name)

    while pc < len(instructions):
        steps += 1
        if steps > limit:
            raise RuntimeError("step limit in " + code.co_name)
        inst = instructions[pc]
        op = inst.opname
        pc += 1
        if op in ("SET_LINENO", "NOP"):
            continue
        if op == "LOAD_CONST":
            stack.append(inst.argval)
        elif op in ("LOAD_FAST", "LOAD_NAME", "LOAD_GLOBAL", "LOAD_DEREF", "LOAD_CLOSURE"):
            stack.append(load_name(inst.argval))
        elif op in ("STORE_FAST", "STORE_NAME", "STORE_GLOBAL", "STORE_DEREF"):
            env[inst.argval] = stack.pop()
        elif op in ("DELETE_FAST", "DELETE_NAME"):
            env.pop(inst.argval, None)
        elif op == "LOAD_ATTR":
            obj = stack.pop()
            if isinstance(obj, list) and inst.argval == "append":
                stack.append(Appender(obj))
            elif isinstance(obj, Sym) and obj.kind == "name" and (obj.fields["name"] + "." + inst.argval) in stores:
                stack.append(stores[obj.fields["name"] + "." + inst.argval])
            else:
                stack.append(Sym("attr", obj=obj, attr=inst.argval))
        elif op == "STORE_ATTR":
            obj = stack.pop()
            value = stack.pop()
            target = to_json(obj)
            name = target["name"] if isinstance(target, dict) and set(target) == {"name"} else json.dumps(target)
            stores[name + "." + inst.argval] = value
        elif op == "DELETE_ATTR":
            stack.pop()
        elif op == "STORE_SUBSCR":
            index = stack.pop()
            obj = stack.pop()
            value = stack.pop()
            if isinstance(obj, dict):
                obj[key(index)] = value
            elif isinstance(obj, list) and isinstance(index, int):
                obj[index] = value
            else:
                target = to_json(obj)
                name = target["name"] if isinstance(target, dict) and set(target) == {"name"} else json.dumps(target)
                stores.setdefault(name + "[]", []).append([index, value])
        elif op == "BINARY_SUBSCR":
            index = stack.pop()
            obj = stack.pop()
            if is_concrete(obj) and is_concrete(index) and not isinstance(obj, dict):
                try:
                    stack.append(obj[index])
                    continue
                except Exception:
                    pass
            stack.append(Sym("subscr", obj=obj, index=index))
        elif op in ("BUILD_LIST", "BUILD_TUPLE"):
            count = inst.arg
            items = stack[len(stack) - count:] if count else []
            del stack[len(stack) - count:]
            stack.append(list(items) if op == "BUILD_LIST" else tuple(items))
        elif op == "BUILD_MAP":
            stack.append({})
        elif op == "DUP_TOP":
            stack.append(stack[-1])
        elif op == "DUP_TOPX":
            stack.extend(stack[-inst.arg:])
        elif op == "POP_TOP":
            stack.pop()
        elif op == "ROT_TWO":
            stack[-1], stack[-2] = stack[-2], stack[-1]
        elif op == "ROT_THREE":
            a = stack.pop(); b = stack.pop(); c = stack.pop()
            stack.extend([a, c, b])
        elif op == "ROT_FOUR":
            a = stack.pop(); b = stack.pop(); c = stack.pop(); d = stack.pop()
            stack.extend([a, d, c, b])
        elif op.startswith("BINARY_") or op.startswith("INPLACE_"):
            right = stack.pop()
            left = stack.pop()
            kind = op.split("_", 1)[1]
            result = None
            if is_concrete(left) and is_concrete(right):
                try:
                    result = {
                        "MODULO": lambda a, b: a % b,
                        "ADD": lambda a, b: a + b,
                        "SUBTRACT": lambda a, b: a - b,
                        "MULTIPLY": lambda a, b: a * b,
                        "DIVIDE": lambda a, b: a // b if isinstance(a, int) and isinstance(b, int) else a / b,
                        "FLOOR_DIVIDE": lambda a, b: a // b,
                        "TRUE_DIVIDE": lambda a, b: a / b,
                        "OR": lambda a, b: a | b,
                        "AND": lambda a, b: a & b,
                        "XOR": lambda a, b: a ^ b,
                        "LSHIFT": lambda a, b: a << b,
                        "RSHIFT": lambda a, b: a >> b,
                        "POWER": lambda a, b: a ** b,
                    }[kind](left, right)
                except Exception:
                    result = None
            stack.append(result if result is not None else Sym("binop", op=kind, left=left, right=right))
        elif op.startswith("UNARY_"):
            value = stack.pop()
            if is_concrete(value):
                try:
                    stack.append({"NEGATIVE": lambda v: -v, "NOT": lambda v: not v,
                                  "POSITIVE": lambda v: +v, "INVERT": lambda v: ~v}[op[6:]](value))
                    continue
                except Exception:
                    pass
            stack.append(Sym("unknown", why=op))
        elif op == "COMPARE_OP":
            right = stack.pop(); left = stack.pop()
            stack.append(Sym("unknown", why="compare"))
        elif op == "CALL_FUNCTION" or op in ("CALL_FUNCTION_VAR", "CALL_FUNCTION_KW", "CALL_FUNCTION_VAR_KW"):
            npos, nkw = call_counts(inst)
            extra = []
            if op in ("CALL_FUNCTION_VAR", "CALL_FUNCTION_KW"):
                extra = [stack.pop()]
            elif op == "CALL_FUNCTION_VAR_KW":
                extra = [stack.pop(), stack.pop()]
            kwargs = {}
            for _ in range(nkw):
                value = stack.pop(); name = stack.pop()
                kwargs[name] = value
            kwargs = dict(reversed(list(kwargs.items())))
            args = stack[len(stack) - npos:] if npos else []
            del stack[len(stack) - npos:]
            func = stack.pop()
            if isinstance(func, Appender):
                func.target.append(args[0])
                stack.append(None)
            elif isinstance(func, Sym) and func.kind == "name" and func.fields["name"] in ("xrange", "range") \
                    and all(isinstance(a, int) for a in args):
                stack.append(list(range(*args)))
            elif isinstance(func, Sym) and func.kind == "name" and func.fields["name"] == "intern" and args:
                stack.append(args[0])
            elif isinstance(func, Sym) and func.kind == "name" and func.fields["name"] == "len" \
                    and args and isinstance(args[0], (list, tuple, str)):
                stack.append(len(args[0]))
            elif isinstance(func, Sym) and func.kind == "name" and func.fields["name"] == "tuple" \
                    and args and isinstance(args[0], (list, tuple)):
                stack.append(tuple(args[0]))
            else:
                stack.append(Sym("call", func=func, args=list(args), kwargs=kwargs))
        elif op == "MAKE_FUNCTION" or op == "MAKE_CLOSURE":
            code_obj = stack.pop()
            if op == "MAKE_CLOSURE":
                stack.pop()
            defaults = [stack.pop() for _ in range(inst.arg or 0)]
            DEFAULTS[id(code_obj)] = list(reversed(defaults))
            stack.append(Sym("lambda", name=getattr(code_obj, "co_name", "?")))
        elif op == "GET_ITER":
            pass
        elif op == "FOR_ITER":
            iterable = stack[-1]
            if not isinstance(iterable, (list, tuple, str)):
                # A symbolic iterable cannot be expanded: skip the loop.
                stack.pop()
                pc = by_offset[inst.argval]
                continue
            state = blocks[-1] if blocks and blocks[-1][0] == inst.offset and blocks[-1][1] is iterable else None
            if state is None:
                blocks.append([inst.offset, iterable, 0])
                state = blocks[-1]
            if state[2] < len(iterable):
                stack.append(iterable[state[2]])
                state[2] += 1
            else:
                blocks.pop()
                stack.pop()
                pc = by_offset[inst.argval]
        elif op == "JUMP_ABSOLUTE" or op == "JUMP_FORWARD" or op == "CONTINUE_LOOP":
            pc = by_offset[inst.argval]
        elif op in ("JUMP_IF_FALSE", "JUMP_IF_TRUE", "POP_JUMP_IF_FALSE", "POP_JUMP_IF_TRUE"):
            cond = stack[-1]
            if op.startswith("POP_"):
                stack.pop()
            if is_concrete(cond):
                taken = (not cond) if "FALSE" in op else bool(cond)
                if taken:
                    pc = by_offset[inst.argval]
            # A symbolic condition falls through, which follows the first branch written.
        elif op in ("SETUP_LOOP", "SETUP_EXCEPT", "SETUP_FINALLY", "POP_BLOCK", "END_FINALLY",
                    "PRINT_ITEM", "PRINT_NEWLINE", "BREAK_LOOP"):
            if op == "BREAK_LOOP":
                break
            continue
        elif op == "RETURN_VALUE":
            return stack.pop() if stack else None, stores, env
        elif op in ("BUILD_CLASS", "IMPORT_NAME", "IMPORT_FROM", "IMPORT_STAR", "BUILD_SLICE",
                    "SLICE+0", "SLICE+1", "SLICE+2", "SLICE+3", "LOAD_LOCALS", "RAISE_VARARGS",
                    "UNPACK_SEQUENCE", "EXEC_STMT", "YIELD_VALUE", "STORE_SLICE+0", "DELETE_SUBSCR"):
            handle_misc(op, inst, stack)
        else:
            raise RuntimeError("unsupported opcode %s in %s" % (op, code.co_name))
    return None, stores, env


def handle_misc(op, inst, stack):
    if op == "BUILD_CLASS":
        stack.pop(); stack.pop(); stack.pop()
        stack.append(Sym("unknown", why="class"))
    elif op == "IMPORT_NAME":
        stack.pop()
        if stack and stack[-1] is None or (stack and isinstance(stack[-1], (int,))):
            pass
        stack.append(Sym("name", name=inst.argval))
    elif op == "IMPORT_FROM":
        stack.append(Sym("attr", obj=stack[-1], attr=inst.argval))
    elif op == "IMPORT_STAR":
        stack.pop()
    elif op == "LOAD_LOCALS":
        stack.append({})
    elif op == "UNPACK_SEQUENCE":
        value = stack.pop()
        count = inst.arg
        if isinstance(value, (list, tuple)) and len(value) == count:
            stack.extend(reversed(list(value)))
        else:
            stack.extend(Sym("unknown", why="unpack") for _ in range(count))
    elif op.startswith("SLICE"):
        variant = int(op[-1])
        upper = stack.pop() if variant in (2, 3) else None
        lower = stack.pop() if variant in (1, 3) else None
        obj = stack.pop()
        if is_concrete(obj) and is_concrete(lower) and is_concrete(upper):
            try:
                stack.append(obj[lower:upper])
                return
            except Exception:
                pass
        stack.append(Sym("unknown", why="slice"))
    elif op == "BUILD_SLICE":
        for _ in range(inst.arg):
            stack.pop()
        stack.append(Sym("unknown", why="slice"))
    elif op == "RAISE_VARARGS":
        for _ in range(inst.arg):
            stack.pop()
    elif op == "YIELD_VALUE":
        stack.pop()
        stack.append(None)
    elif op in ("EXEC_STMT", "STORE_SLICE+0", "DELETE_SUBSCR"):
        pass


OPC = None

# Default argument values, keyed by the id of the function's code object. They are recorded
# when the enclosing class body or module runs its MAKE_FUNCTION.
DEFAULTS = {}


def module_constants(code):
    """Evaluates a module's top level for simple constant assignments, skipping its classes."""
    try:
        _, _, env = run(code, {}, limit=500000)
    except RuntimeError:
        env = {}
    return {k: v for k, v in env.items() if is_concrete(v) and not isinstance(v, dict)}


def is_class_body(code):
    """A 2.3 class body ends by returning its locals."""
    return any(inst.opname == "LOAD_LOCALS" for inst in Bytecode(code, OPC))


def functions(code, prefix=""):
    """Yields every nested function code object with a dotted qualified name."""
    for const in code.co_consts:
        if hasattr(const, "co_code"):
            name = prefix + const.co_name
            yield name, const
            yield from functions(const, name + ".")


def main():
    global OPC
    source = Path(sys.argv[1])
    output = Path(sys.argv[2])
    result = {}
    loaded = {}
    for module in MODULES:
        path = source / (module + ".pyo")
        if not path.exists():
            continue
        loaded_info = xdis.load.load_module(str(path))
        OPC = xdis.op_imports.get_opcode_module(loaded_info[0], loaded_info[4])
        loaded[module] = loaded_info[3]
    game_constants = module_constants(loaded["Game"]) if "Game" in loaded else {}
    result["Game"] = {"constants": game_constants}
    for module, code in loaded.items():
        constants = dict(module_constants(code))
        constants_json = {k: to_json(v) for k, v in constants.items()}
        globals_env = dict(constants)
        module_result = {"constants": constants_json, "functions": {}}
        class_constants = {}
        for name, function in functions(code):
            try:
                value, stores, local_env = run(function, globals_env)
            except Exception as failure:  # noqa: BLE001 - recorded, not fatal
                module_result["functions"][name] = {"error": str(failure)}
                continue
            if is_class_body(function):
                class_constants[name] = {k: to_json(v) for k, v in local_env.items()
                                         if isinstance(v, (str, int, float, bytes, tuple, list))}
                continue
            if value is None and not stores:
                continue
            entry = {}
            if value is not None:
                entry["returns"] = to_json(value)
            if stores:
                entry["stores"] = {k: to_json(v) for k, v in stores.items()}
            module_result["functions"][name] = entry
        signatures = {}
        for name, function in functions(code):
            params = list(function.co_varnames[:function.co_argcount])
            if params and params[0] == "self":
                params = params[1:]
            defaults = DEFAULTS.get(id(function), [])
            signatures[name] = {"params": params, "defaults": [to_json(v) for v in defaults]}
        module_result["signatures"] = signatures
        module_result["classConstants"] = class_constants
        result[module] = module_result
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=1, sort_keys=True) + "\n")


if __name__ == "__main__":
    main()
