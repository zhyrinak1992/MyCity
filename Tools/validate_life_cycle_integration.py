from pathlib import Path
root = Path(__file__).resolve().parents[1]
system = (root / 'Assets/Scripts/Simulation/CitizenLifeCycleSystem.cs').read_text(encoding='utf-8')
bootstrap = (root / 'Assets/Scripts/MyCityBootstrap.cs').read_text(encoding='utf-8')
checks = {
    'Funeral event journal deduplicates citizen records on load': 'HashSet<string> seenCitizens' in system and 'seenCitizens.Add(item.CitizenId)' in system,
    'Lifecycle counters are reset before restoration': 'DeceasedCount = WaitingForMorgueCount = InMorgueCount = WaitingForBurialCount = BuriedCount = 0;' in system,
    'Lifecycle counters can be rebuilt from restored citizens': 'public void RebuildStatistics(IList<CitizenRecord> citizens)' in system and 'lifeCycleSystem.RebuildStatistics(citizenRecords);' in bootstrap,
    'Selected deceased resident shows death day and cause': 'Статус: умер · игровой день ' in bootstrap and 'Причина: ' in bootstrap,
    'Selected deceased resident shows funeral processing status': 'Похороны: ' in bootstrap,
    'Spouse archive state is visible in family profile': 'other.Record.IsDeceased ? " · умер"' in bootstrap,
    'Parent archive state is visible in family profile': 'parent.Record.IsDeceased ? " · умер"' in bootstrap,
    'Child archive state is visible in family profile': 'child.Record.IsDeceased ? " · умер"' in bootstrap,
    'Funeral panel includes cause and burial day': 'resident.Record.DeathCause' in bootstrap and 'resident.Record.BurialDay' in bootstrap,
}
for name, ok in checks.items(): print(('PASS' if ok else 'FAIL') + ' | ' + name)
print('RESULT: %d/%d' % (sum(checks.values()), len(checks)))
if not all(checks.values()): raise SystemExit(1)
