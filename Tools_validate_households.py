from pathlib import Path
root=Path(__file__).parent
records=(root/'Assets/Scripts/Data/CityRecords.cs').read_text(encoding='utf-8')
system=(root/'Assets/Scripts/Simulation/HouseholdSystem.cs').read_text(encoding='utf-8')
boot=(root/'Assets/Scripts/MyCityBootstrap.cs').read_text(encoding='utf-8')
save=(root/'Assets/Scripts/Saving/CitySaveService.cs').read_text(encoding='utf-8')
checks={
 'CitizenRecord stores household identity':'public string HouseholdId;' in records,
 'HouseholdRecord stores persistent member ids':'List<string> MemberCitizenIds' in records,
 'CitySaveData persists household records':'List<HouseholdRecord> Households' in records,
 'Households group by shared home':'"home:" + homeId' in system,
 'Homeless residents remain individually grouped':'"person:" + citizen.Id' in system,
 'Adult and child counts derived from age':'citizen.Age >= 18' in system,
 'Combined purchasing power aggregates personal wallets':'CombinedPersonalWallets = checked' in system,
 'Personal wallet balances are not pooled or mutated':'WalletBalance =' not in system,
 'Household needs aggregate from member records':'household.FoodNeed += Mathf.Clamp' in system,
 'Household needs are averaged per member':'household.FoodNeed /= divisor' in system,
 'Restore rebuilds from authoritative citizen records':'householdSystem.Load(citizenRecords, saved.Day)' in boot,
 'Save exports household snapshot':'householdSystem.Export(data.Households)' in boot,
 'Household system is attached during bootstrap':'AddComponent<HouseholdSystem>()' in boot,
 'JSON save uses CitySaveData serialization':'JsonUtility.ToJson(data' in save,
}
failed=[k for k,v in checks.items() if not v]
for k,v in checks.items(): print(('PASS' if v else 'FAIL')+' '+k)
print(f'{len(checks)-len(failed)}/{len(checks)} checks passed')
if failed: raise SystemExit(1)
