from pathlib import Path
root=Path(__file__).resolve().parents[1]
records=(root/"Assets/Scripts/Data/CityRecords.cs").read_text(encoding="utf-8")
bootstrap=(root/"Assets/Scripts/MyCityBootstrap.cs").read_text(encoding="utf-8")
system=(root/"Assets/Scripts/Simulation/CitizenRelationshipSystem.cs").read_text(encoding="utf-8")
checks={
"persistent relationship record":"class CitizenRelationshipRecord" in records,
"save data relationship list":"CitizenRelationships = new List<CitizenRelationshipRecord>()" in records,
"system attached":"AddComponent<CitizenRelationshipSystem>()" in bootstrap,
"load wired":"citizenRelationshipSystem.Load(citizenRecords, saved.CitizenRelationships)" in bootstrap,
"export wired":"citizenRelationshipSystem.Export(data.CitizenRelationships)" in bootstrap,
"spouse creation":"AddSpouses(" in system,
"parent child creation":"AddParentChild(" in system,
"adult spouse validation":"first.Age >= 18 && second.Age >= 18" in system,
"parent age gap validation":"parent.Age - child.Age >= 16" in system,
"self relation prevented":"a != b" in system and "parentId != childId" in system,
"cycle prevention":"WouldCreateParentCycle" in system,
"historical ending":"IsActive = false" in system,
"co-residence not assumed":"Sharing a home never creates kinship automatically" in system,
"legacy-compatible optional list":"CitizenRelationships" in records,
}
for name,ok in checks.items(): print(("PASS" if ok else "FAIL")+" "+name)
print(f"RESULT: {sum(checks.values())}/{len(checks)}")
if not all(checks.values()): raise SystemExit(1)
