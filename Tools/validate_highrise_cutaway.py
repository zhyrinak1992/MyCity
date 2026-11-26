from pathlib import Path
import sys
p=Path(__file__).resolve().parents[1]/'Assets/Scripts/MyCityBootstrap.cs'
s=p.read_text(encoding='utf-8')
checks={
 'interior geometry builder':'CreateHighRiseInteriorGeometry' in s,
 'per-floor slabs and partitions':all(x in s for x in ['Interior floor ','Interior rear wall ','Interior left partition ','Interior right partition ']),
 'elevator core visual':'Elevator core ' in s,
 'engineering room props':'Engineering equipment ' in s,
 'residential room props':'Apartment furniture ' in s,
 'office room props':'Office desks ' in s,
 'enter/exit control':'Войти внутрь · 3D-разрез' in s and 'Вернуться в город' in s,
 'camera restore':all(x in s for x in ['savedCameraTarget','savedCameraDistance','savedCameraPitch']),
 'exterior/interior renderer separation':'!renderer.transform.IsChildOf(interior.transform)' in s,
 'restore-time interior generation':'if (b.IsHighRise) CreateHighRiseInteriorGeometry(b);' in s,
}
# Lightweight C# lexical scan for balanced braces outside comments and literals.
state='code'; depth=0; minimum=0; i=0
while i<len(s):
 c=s[i]; n=s[i+1] if i+1<len(s) else ''
 if state=='code':
  if c=='/' and n=='/': state='line'; i+=2; continue
  if c=='/' and n=='*': state='block'; i+=2; continue
  if c=='"': state='string'; i+=1; continue
  if c=="'": state='char'; i+=1; continue
  if c=='{': depth+=1
  elif c=='}': depth-=1; minimum=min(minimum,depth)
 elif state=='line':
  if c=='\n': state='code'
 elif state=='block':
  if c=='*' and n=='/': state='code'; i+=2; continue
 elif state in ('string','char'):
  if c=='\\': i+=2; continue
  if (state=='string' and c=='"') or (state=='char' and c=="'"): state='code'
 i+=1
checks['balanced C# braces']=depth==0 and minimum>=0 and state not in ('block','string','char')
for name,ok in checks.items(): print(('PASS' if ok else 'FAIL')+'  '+name)
if not all(checks.values()): sys.exit(1)
