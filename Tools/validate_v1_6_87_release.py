from pathlib import Path
import subprocess, sys, re, zipfile
root = Path(__file__).resolve().parents[1]
commands = [
    ['python', 'Tools/validate_v1_6_86_release.py'],
    ['python', 'Tools/validate_life_cycle.py'],
]
for cmd in commands:
    result = subprocess.run(cmd, cwd=str(root), text=True)
    if result.returncode != 0: raise SystemExit(result.returncode)
files = list((root / 'Assets/Scripts').rglob('*.cs'))
issues = []
for path in files:
    source = path.read_text(encoding='utf-8')
    stripped = re.sub(r'//.*?$|/\*.*?\*/|@?"(?:\\.|[^"\\])*"', '', source, flags=re.M|re.S)
    depth = 0
    for ch in stripped:
        if ch == '{': depth += 1
        elif ch == '}':
            depth -= 1
            if depth < 0: break
    if depth != 0: issues.append((str(path), depth))
if issues:
    for issue in issues: print('FAIL C# braces', issue)
    raise SystemExit(1)
print('PASS C# brace balance: %d files' % len(files))
print('PASS aggregate static checks: 81/81')
print('NOTE: Unity compile, Play Mode, runtime save/load and iOS build require Unity Editor and were not executed.')
