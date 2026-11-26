from pathlib import Path
root = Path(__file__).resolve().parents[1]
records = (root / 'Assets/Scripts/Data/CityRecords.cs').read_text(encoding='utf-8')
system = (root / 'Assets/Scripts/Simulation/CitizenLifeCycleSystem.cs').read_text(encoding='utf-8')
bootstrap = (root / 'Assets/Scripts/MyCityBootstrap.cs').read_text(encoding='utf-8')
households = (root / 'Assets/Scripts/Simulation/HouseholdSystem.cs').read_text(encoding='utf-8')
checks = {
    'Citizen death and burial fields are serializable': all(x in records for x in ['IsDeceased', 'DeathDay', 'DeathCause', 'FuneralStatus', 'MorgueBuildingId', 'CemeteryBuildingId']),
    'Funeral event history is included in city save data': 'List<FuneralEventRecord> FuneralEvents' in records,
    'Age advances on 360-day game years': 'elapsed / 360' in system,
    'Natural mortality is deterministic and age-gated': 'ShouldDieNaturally' in system and 'citizen.Age < 70' in system,
    'Death stops employment and records life history': 'Смерть · игровой день' in system,
    'Morgue capacity is enforced': 'defaultCapacity' in system and 'Ожидает места в морге' in system,
    'Cemetery capacity and two-day morgue hold are enforced': 'day - citizen.MorgueAdmissionDay >= 2' in system and 'Ожидает места на кладбище' in system,
    'Facility removal returns pending cases to queues': 'morgues.ContainsKey' in system and 'cemeteries.ContainsKey' in system,
    'Funeral data loads and saves': 'lifeCycleSystem.Load(saved.FuneralEvents)' in bootstrap and 'lifeCycleSystem.Export(data.FuneralEvents)' in bootstrap,
    'New morgue and cemetery can be built': 'Tool.Morgue' in bootstrap and 'Tool.Cemetery' in bootstrap,
    'Deceased residents are not restored as active agents': 'if (record.IsDeceased)' in bootstrap and 'Home = null, Body = null' in bootstrap,
    'Deceased residents remain archived across reloads': 'if (home == null && !citizen.IsDeceased) continue;' in bootstrap and 'if (!resident.Record.IsDeceased) resident.Record.HomeBuildingId' in bootstrap,
    'Deceased residents cannot shop': 'citizen.IsDeceased' in (root / 'Assets/Scripts/Simulation/CityEconomySystem.cs').read_text(encoding='utf-8'),
    'Deceased residents are excluded from household aggregation': 'citizen.IsDeceased' in households,
    'Life-cycle panel is exposed': 'DrawLifeCyclePanel();' in bootstrap and 'showLifeCyclePanel' in bootstrap,
    'Save schema migrated to 21': 'SchemaVersion = 21' in bootstrap,
}
for name, ok in checks.items(): print(('PASS' if ok else 'FAIL') + ' | ' + name)
print('RESULT: %d/%d' % (sum(checks.values()), len(checks)))
if not all(checks.values()): raise SystemExit(1)
