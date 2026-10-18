#!/usr/bin/env python3
"""Normalize the raw dump of `recover_animations.py` into the presentation's animation data.

The raw dump keeps the original's structure as symbolic calls. This pass resolves what the
presentation needs and nothing more:

- state keys such as `CGameObjPlayer.ST_WlkLeft` become the interned state names (`st_wlkleft`);
- page attributes such as `self.PgPlayer` become sprite page files (`SpritePlayer`);
- `Game.DIR_*` become direction names;
- every constructor call (`CState`, `CType`, `CStateInfo`, `CObjRepInfo`) is bound to its
  recovered parameter names and defaults;
- a type-table call such as `GameObjs.GameObjBubble(self)` is replaced by what that function
  returns;
- the game module's effect animations are kept under `effects`, and its sound aliases under
  `sounds`.

Usage: normalize_animations.py <raw json> <output json>
"""

import json
import sys

DIRECTIONS = {"DIR_Left": "Left", "DIR_Top": "Up", "DIR_Right": "Right", "DIR_Bottom": "Down",
              "DIR_Last": None}

# Classes whose nested constructors are inherited rather than defined: `class
# CGameObjTable(CGameObjFSABase)` declares no `CType` or `CState` of its own.
INHERITED = {"CGameObjTable": "CGameObjFSABase"}


class Normalizer:
    def __init__(self, raw):
        self.raw = raw
        self.modules = [m for m in raw if "functions" in raw[m]]
        self.pages = {}
        for function in raw["Game"]["functions"].values():
            for target, value in function.get("stores", {}).items():
                if target.startswith("self.Pg") and isinstance(value, dict) \
                        and value.get("call") == "DDraw.PageMake":
                    self.pages[target[len("self."):]] = value["args"][0]
        self.signatures = {}
        self.class_constants = {}
        self.returns = {}
        for module in self.modules:
            for name, signature in raw[module].get("signatures", {}).items():
                if name.endswith(".__init__"):
                    self.signatures[name[: -len(".__init__")]] = signature
            for name, constants in raw[module].get("classConstants", {}).items():
                self.class_constants[name] = constants
            for name, function in raw[module]["functions"].items():
                if "returns" in function:
                    self.returns[module + "." + name] = function["returns"]

    def strip_module(self, dotted):
        head, _, rest = dotted.partition(".")
        return rest if head in self.raw and rest else dotted

    def resolve_name(self, dotted):
        parts = dotted.split(".")
        last = parts[-1]
        if last in DIRECTIONS and (len(parts) == 1 or parts[-2] == "Game"):
            return {"direction": DIRECTIONS[last]}
        if last.startswith("Pg") and last in self.pages:
            return {"page": self.pages[last]}
        owner = self.strip_module(".".join(parts[:-1]))
        if owner in self.class_constants and last in self.class_constants[owner]:
            return self.class_constants[owner][last]
        if last.startswith("ST_"):
            for constants in self.class_constants.values():
                if last in constants and isinstance(constants[last], str):
                    return constants[last]
        if dotted in ("True", "False", "None"):
            return {"True": True, "False": False, "None": None}[dotted]
        if parts[0] == "Game" and len(parts) == 2 and last in self.raw["Game"].get("constants", {}):
            return self.raw["Game"]["constants"][last]
        return {"ref": dotted}

    def resolve(self, node, depth=0):
        if depth > 40:
            return {"ref": "<deep>"}
        if isinstance(node, list):
            return [self.resolve(item, depth + 1) for item in node]
        if not isinstance(node, dict):
            return node
        if set(node) == {"name"}:
            return self.resolve_name(node["name"])
        if "dict" in node:
            result = {}
            for key, value in node["dict"]:
                resolved_key = self.resolve(key, depth + 1)
                if not isinstance(resolved_key, str):
                    resolved_key = json.dumps(resolved_key, sort_keys=True)
                result[resolved_key] = self.resolve(value, depth + 1)
            return result
        if "call" in node and isinstance(node["call"], str):
            callee = self.strip_module(node["call"])
            if callee.startswith("self.") and "CGameType." + callee[5:] in self.signatures:
                callee = "CGameType." + callee[5:]
            owner, _, nested = callee.rpartition(".")
            if callee not in self.signatures and owner in INHERITED:
                callee = INHERITED[owner] + "." + nested
            args = node.get("args", [])
            kwargs = node.get("kwargs", {})
            if callee in self.signatures:
                return self.bind(callee, args, kwargs, depth)
            qualified = node["call"]
            if qualified in self.returns and all(
                    isinstance(a, dict) and a.get("name") in ("self", "GMod", "App.GMod") for a in args):
                return self.resolve(self.returns[qualified], depth + 1)
            return {"call": qualified, "args": self.resolve(args, depth + 1),
                    **({"kwargs": {k: self.resolve(v, depth + 1) for k, v in kwargs.items()}} if kwargs else {})}
        return {k: self.resolve(v, depth + 1) for k, v in node.items()}

    def bind(self, callee, args, kwargs, depth):
        signature = self.signatures[callee]
        params = signature["params"]
        defaults = signature["defaults"]
        bound = {"new": callee}
        first_default = len(params) - len(defaults)
        for index, param in enumerate(params):
            if index < len(args):
                bound[param] = self.resolve(args[index], depth + 1)
            elif param in kwargs:
                bound[param] = self.resolve(kwargs[param], depth + 1)
            elif index >= first_default:
                bound[param] = self.resolve(defaults[index - first_default], depth + 1)
        for name, value in kwargs.items():
            if name not in params:
                bound[name] = self.resolve(value, depth + 1)
        return bound

    def state_tables(self):
        tables = {}
        for module in self.modules:
            for function in self.raw[module]["functions"].values():
                for target, value in function.get("stores", {}).items():
                    if target.endswith(".SMap") or (isinstance(value, dict) and str(value.get("call", "")).endswith(".CType")):
                        owner = self.strip_module(target.rsplit(".", 1)[0])
                        tables[owner + "." + target.rsplit(".", 1)[1]] = self.resolve(value)
        return tables

    def object_types(self):
        registrations = self.raw["Game"]["functions"]["CGameType.__ObjRepInit"]["stores"]["self.ObjRep"]["dict"]
        return {name: self.resolve(value) for name, value in registrations}

    def effects(self):
        """The game module's effect animations: every `self.Sfx*`, `self._Sfx*` and `self.Anm*`
        that `CGameType.ModInit` stores, such as the explosion's `SfxExpl` layers and the
        shadows `SfxShBig`, `SfxShMed` and `SfxShSm`."""
        result = {}
        stores = self.raw["Game"]["functions"]["CGameType.ModInit"]["stores"]
        for target, value in stores.items():
            name = target[len("self."):] if target.startswith("self.") else None
            if name and (name.startswith("Sfx") or name.startswith("_Sfx") or name.startswith("Anm")):
                result[name] = self.resolve(value)
        return result

    def sounds(self):
        """The game module's sound aliases: every `self.Snd*` string that `CGameType.ModInit`
        stores, such as `SndKeyDoor = 'KeyDoor'`, which `Sound.json` resolves to files."""
        stores = self.raw["Game"]["functions"]["CGameType.ModInit"]["stores"]
        return {target[len("self."):]: value for target, value in stores.items()
                if target.startswith("self.Snd") and isinstance(value, str)}

    def module_tables(self):
        """Every type-table function's resolved return value, by qualified name."""
        tables = {}
        for module in self.modules:
            for name, function in self.raw[module]["functions"].items():
                if "returns" in function and (name.startswith("GameType") or name.startswith("GameObj")
                                              or name.startswith("Cell") or name.startswith("GameWeap")):
                    tables[module + "." + name] = self.resolve(function["returns"])
        return tables


def main():
    raw = json.loads(open(sys.argv[1]).read())
    normalizer = Normalizer(raw)
    output = {
        "version": 1,
        "pages": normalizer.pages,
        "classTables": normalizer.state_tables(),
        "typeFunctions": normalizer.module_tables(),
        "objectTypes": normalizer.object_types(),
        "effects": normalizer.effects(),
        "sounds": normalizer.sounds(),
    }
    with open(sys.argv[2], "w") as stream:
        json.dump(output, stream, indent=1, sort_keys=True)
        stream.write("\n")


if __name__ == "__main__":
    main()
