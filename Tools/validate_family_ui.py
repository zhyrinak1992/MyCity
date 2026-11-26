from pathlib import Path
root=Path(__file__).resolve().parents[1]
bootstrap=(root/'Assets/Scripts/MyCityBootstrap.cs').read_text(encoding='utf-8')
checks={
    'family panel state exists':'showFamilyPanel' in bootstrap and 'familyPanelScroll' in bootstrap,
    'family panel toggle is available':'Семья · родственные связи' in bootstrap,
    'family panel is drawn from OnGUI':'DrawFamilyPanel();' in bootstrap,
    'selected citizen is displayed':'selectedResident.Name' in bootstrap and 'person.Age' in bootstrap,
    'spouse relationship is displayed':'relation.RelationshipType != "Spouse"' in bootstrap,
    'parent links are displayed':'relation.ChildCitizenId != selectedResident.Id' in bootstrap,
    'child links are displayed':'relation.ParentCitizenId != selectedResident.Id' in bootstrap,
    'co-residents are displayed separately':'СОВМЕСТНО ПРОЖИВАЮТ (НЕ ОБЯЗАТЕЛЬНО РОДСТВЕННИКИ)' in bootstrap,
    'co-residence is not presented as kinship':'Общий адрес сам по себе не означает родство.' in bootstrap,
    'family data comes from persisted relationship system':'citizenRelationshipSystem.Relationships' in bootstrap,
    'household membership comes from household system':'householdSystem.GetForCitizen(person.Id)' in bootstrap,
    'close control exists':'showFamilyPanel = false; return;' in bootstrap,
    'resident lookup uses persistent citizen id':'residents[i].Id == citizenId' in bootstrap,
}
for name,ok in checks.items():
    print(('PASS' if ok else 'FAIL')+' '+name)
if not all(checks.values()): raise SystemExit(1)
print(f'RESULT: {sum(checks.values())}/{len(checks)}')
