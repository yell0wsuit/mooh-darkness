# Tools

## Animation recovery

The original game builds its sprite state tables in Python 2.3 bytecode that the decompiler
could not fully recover: every list comprehension, which is where the frame names live, came
out empty. Two scripts rebuild those tables without executing the original code.

```bash
python3 -m venv .venv && .venv/bin/pip install xdis
.venv/bin/python -I tools/recover_animations.py <mooh_extracted> /tmp/animations-raw.json
python3 -I tools/normalize_animations.py /tmp/animations-raw.json assets/animations/animations.json
```

- `recover_animations.py` runs a small symbolic interpreter over every function of the game
  modules. Constants, `xrange`, string formatting and comprehension loops evaluate concretely;
  everything that needs the game's runtime stays a symbolic node.
- `normalize_animations.py` resolves state names, sprite pages, directions and constructor
  arguments into `assets/animations/animations.json`, which the presentation loads.
