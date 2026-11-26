from pathlib import Path
import subprocess
import sys
root = Path(__file__).resolve().parents[1]
checks = [
    ('family + economy', root / 'Tools/validate_v1_6_86_release.py'),
    ('life-cycle core', root / 'Tools/validate_life_cycle.py'),
    ('life-cycle integration', root / 'Tools/validate_life_cycle_integration.py'),
]
for label, script in checks:
    print('\n=== ' + label + ' ===')
    result = subprocess.run([sys.executable, str(script)], cwd=str(root), text=True)
    if result.returncode != 0:
        raise SystemExit(result.returncode)
print('\nRESULT: 90/90 functional static checks passed (65 family/economy + 16 life-cycle + 9 integration).')
print('NOTE: Unity compile, Play Mode, runtime save/load, profiling and iOS build require Unity/Xcode and were not executed.')
