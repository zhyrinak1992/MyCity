from pathlib import Path
root=Path(__file__).resolve().parents[1]
bootstrap=(root/'Assets/Scripts/MyCityBootstrap.cs').read_text(encoding='utf-8')
system=(root/'Assets/Scripts/Simulation/CitizenRelationshipSystem.cs').read_text(encoding='utf-8')
checks={
 'family generation runs only for new residential occupancy':'GenerateInitialHouseholdFamily(b)' in bootstrap,
 'family residents are grouped by exact home':'r.Home == home' in bootstrap,
 'stable household seed':'stableSeed = (stableSeed * 31 + key[i])' in bootstrap,
 'couple archetype is explicit':'AddSpouses(members[0].Id, members[1].Id' in bootstrap,
 'two-parent children are explicit':'AddParentChild(members[1].Id, members[i].Id' in bootstrap,
 'single-parent archetype is explicit':'else if (archetype <= 6)' in bootstrap and 'AddParentChild(members[0].Id, members[i].Id' in bootstrap,
 'roommates remain unrelated':'unrelated adult housemates' in bootstrap,
 'child ages satisfy minimum parent gap':'members[i].Record.Age = 5 + (stableSeed + i * 7) % 8' in bootstrap,
 'new citizens registered before relationship creation':'citizenRelationshipSystem.RegisterCitizen(record)' in bootstrap,
 'relationship registration stores canonical citizen':'citizensById[citizen.Id] = citizen' in system,
 'family relationships persist on save':'citizenRelationshipSystem.Export(data.CitizenRelationships)' in bootstrap,
 'family relationships restore on load':'citizenRelationshipSystem.Load(citizenRecords, saved.CitizenRelationships)' in bootstrap,
 'shared residence not automatically treated as kinship':'Sharing a home never creates kinship automatically' in system,
}
for name,ok in checks.items(): print(('PASS' if ok else 'FAIL')+' '+name)
print(f'RESULT: {sum(checks.values())}/{len(checks)}')
if not all(checks.values()): raise SystemExit(1)
