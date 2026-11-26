#!/usr/bin/env python3
from pathlib import Path
import re, zipfile, sys
root=Path(__file__).resolve().parents[1]
boot=(root/'Assets/Scripts/MyCityBootstrap.cs').read_text(encoding='utf-8')
records=(root/'Assets/Scripts/Data/CityRecords.cs').read_text(encoding='utf-8')
checks={
 'high-rise local variable declared before use': boot.index('bool workplace=false;') < boot.index('workplace = false;', boot.index('bool workplace=false;')),
 'floor model declared': 'class HighRiseFloorRecord' in records,
 'office floor generation': 'Офисы и арендаторы' in boot,
 'residential floor generation': 'Жилые квартиры' in boot,
 'commercial floor generation': 'Коммерческие помещения' in boot,
 'engineering floor generation': 'Инженерный этаж' in boot,
 'resident distribution': 'DistributeResidentsAcrossHighRiseFloors' in boot,
 'elevator simulation': 'AdvanceHighRiseOperations' in boot and 'ElevatorTripsToday' in boot,
 'visitor aggregation': 'floor.Visitors' in boot,
 'UI floor plan': 'DrawHighRiseInteriorPanel' in boot and 'Открыть план этажей' in boot,
 'save floor data': 'HighRiseFloors = b.IsHighRise ? b.HighRiseFloors' in boot,
 'restore floor data': 'restoreRecord.HighRiseFloors' in boot,
}
# lexical brace balance for all C# source files, ignoring comments and quoted literals
for path in (root/'Assets/Scripts').rglob('*.cs'):
    src=path.read_text(encoding='utf-8'); i=0; depth=0; state='code'
    while i<len(src):
        ch=src[i]; nxt=src[i+1] if i+1<len(src) else ''
        if state=='code':
            if ch=='/' and nxt=='/': state='line'; i+=2; continue
            if ch=='/' and nxt=='*': state='block'; i+=2; continue
            if ch=='"': state='string'; i+=1; continue
            if ch=="'": state='char'; i+=1; continue
            if ch=='{': depth+=1
            elif ch=='}': depth-=1
            if depth<0: break
        elif state=='line':
            if ch=='\n': state='code'
        elif state=='block':
            if ch=='*' and nxt=='/': state='code'; i+=2; continue
        elif state in ('string','char'):
            if ch=='\\': i+=2; continue
            if (state=='string' and ch=='"') or (state=='char' and ch=="'"): state='code'
        i+=1
    checks['balanced braces: '+path.name]=depth==0 and state not in ('block','string','char')
failed=[k for k,v in checks.items() if not v]
for k,v in checks.items(): print(('PASS' if v else 'FAIL')+' '+k)
if failed: sys.exit(1)
print('PASS all static high-rise checks')
