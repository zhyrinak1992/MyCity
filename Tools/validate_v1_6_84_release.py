from pathlib import Path
import subprocess, sys
root=Path(__file__).resolve().parents[1]
for script in ('validate_family_relationships.py','validate_family_generation.py'):
    result=subprocess.run([sys.executable,str(root/'Tools'/script)],check=False)
    if result.returncode: raise SystemExit(result.returncode)
files=list((root/'Assets'/'Scripts').rglob('*.cs'))
for path in files:
    source=path.read_text(encoding='utf-8')
    if source.count('{') != source.count('}'):
        raise SystemExit(f'Brace mismatch: {path}')
print(f'PASS C# brace balance: {len(files)} files')
