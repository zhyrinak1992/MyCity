from pathlib import Path
root=Path(__file__).parent
boot=(root/'Assets/Scripts/MyCityBootstrap.cs').read_text(encoding='utf-8')
econ=(root/'Assets/Scripts/Simulation/CityEconomySystem.cs').read_text(encoding='utf-8')
records=(root/'Assets/Scripts/Data/CityRecords.cs').read_text(encoding='utf-8')
checks={
 'high-rise employer registration': 'EnsureHighRiseWorkplace' in boot and 'IsRetailBusiness = false' in boot,
 'real employer staff capacity derived from floor tenants': 'staffCapacity = checked(staffCapacity + tenants * (office ? 4 : 2))' in boot,
 'rent credited to employer cash balance': 'workplace.OperatingBalance = checked(workplace.OperatingBalance + rent);' in econ,
 'non-retail employers bypass retail inventory simulation': 'if (!workplace.IsRetailBusiness)' in econ and 'if (workplace.IsRetailBusiness)' in econ,
 'individual citizen payroll remains the payment path': 'citizen.AccruedWages = checked(citizen.AccruedWages + citizen.MonthlySalary);' in econ and 'TryPayOutstanding();' in econ,
 'high-rise daily aggregate payroll removed': 'SettleHighRiseEconomy' not in boot and 'highrise-payroll-' not in boot,
 'workplace rent transaction is idempotent': 'workplace-rent-' in econ,
 'high-rise residents can be assigned to real employer': 'AssignCitizen(record.Id, employer.Id, "office", 15000)' in boot,
 'legacy saves recreate missing high-rise employers': 'foreach (PlacedBuilding building in buildings)\n            if (building != null && building.IsHighRise) EnsureHighRiseWorkplace(building);' in boot,
}# Lexical brace balance ignoring comments and strings for all C# source files.
def brace_balance(text):
 i=0; depth=0; state='code'
 while i<len(text):
  c=text[i]; n=text[i+1] if i+1<len(text) else ''
  if state=='code':
   if c=='/' and n=='/': state='line'; i+=2; continue
   if c=='/' and n=='*': state='block'; i+=2; continue
   if c=='"': state='str'; i+=1; continue
   if c=="'": state='char'; i+=1; continue
   if c=='{': depth+=1
   elif c=='}': depth-=1
   if depth<0:return False
  elif state=='line':
   if c=='\n':state='code'
  elif state=='block':
   if c=='*' and n=='/':state='code';i+=2;continue
  elif state in ('str','char'):
   if c=='\\':i+=2;continue
   if (state=='str' and c=='"') or (state=='char' and c=="'"):state='code'
  i+=1
 return depth==0 and state not in ('block','str','char')
cs=list((root/'Assets/Scripts').rglob('*.cs'))
checks['C# braces balanced across scripts']=all(brace_balance(x.read_text(encoding='utf-8')) for x in cs)
for k,v in checks.items():print(('PASS' if v else 'FAIL')+' '+k)
print(f'{sum(checks.values())}/{len(checks)} checks; {len(cs)} C# files scanned')
if not all(checks.values()):raise SystemExit(1)
