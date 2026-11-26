from pathlib import Path
import sys
root=Path(__file__).resolve().parents[1]
records=(root/'Assets/Scripts/Data/CityRecords.cs').read_text(encoding='utf-8')
system=(root/'Assets/Scripts/Simulation/HouseholdSystem.cs').read_text(encoding='utf-8')
checks={
'member role list':'List<HouseholdMemberRecord> Members' in records,
'citizen identity':'public string CitizenId;' in records,
'role label':'public string Role;' in records,
'guardian link':'public string GuardianCitizenId;' in records,
'household head':'HouseholdHeadCitizenId' in records,
'roles rebuilt':'BuildHouseholdRoles(household, citizens)' in system,
'oldest adult':'member.Age > oldestAdult.Age' in system,
'minor guardian':'role.GuardianCitizenId = oldestAdult.Id' in system,
'no adult branch':'if (oldestAdult == null)' in system,
'load rebuild':'Rebuild(citizens, day);' in system}
for k,v in checks.items(): print(('PASS ' if v else 'FAIL ')+k)
print(str(sum(checks.values()))+'/'+str(len(checks))+' checks passed')
sys.exit(0 if all(checks.values()) else 1)
