#!/usr/bin/env python3
from pathlib import Path
import sys
root=Path(__file__).resolve().parents[1]
boot=(root/'Assets/Scripts/MyCityBootstrap.cs').read_text(encoding='utf-8')
elevator=(root/'Assets/Scripts/Simulation/HighRiseElevatorController.cs').read_text(encoding='utf-8')
schedule=(root/'Assets/Scripts/Simulation/CitizenDailySchedule.cs').read_text(encoding='utf-8')
checks={
 'dedicated elevator controller exists': 'class HighRiseElevatorController' in elevator,
 'one controller per tower': 'Dictionary<string, HighRiseElevatorController>' in boot,
 'physical cabin primitives created': 'Elevator cabin ' in elevator and 'CreatePrimitive(PrimitiveType.Cube)' in elevator,
 'finite cabin capacity': 'Capacity = 8' in elevator,
 'FIFO request queue': 'Queue<Request>' in elevator and 'Queue.Enqueue' in elevator and 'Queue.Dequeue' in elevator,
 'floor-based travel duration': 'Mathf.Abs(car.TargetFloor - car.Floor) * 2' in elevator,
 'minute-driven movement': 'AdvanceOneMinute' in elevator and 'AdvanceHighRiseElevators();' in boot,
 'residents create elevator calls on schedule changes': 'QueueResidentElevatorTrips(resident, previousPhase, phase)' in boot,
 'home tower trips link apartment and lobby': 'QueueHighRiseElevator(resident.Home, homeFloor, 1)' in boot and 'QueueHighRiseElevator(resident.Home, 1, homeFloor)' in boot,
 'workplace tower trips link lobby and office floor': 'QueueHighRiseElevator(workplace, 1, workFloor)' in boot and 'QueueHighRiseElevator(workplace, workFloor, 1)' in boot,
 'trip counter increments on passenger arrival': 'if (car.Passengers > 0) { CompletedTrips++;' in elevator,
 'synthetic trip counter removed': 'building.ElevatorTripsToday += peak ? activeCars * 2 : activeCars' not in boot,
 'queue and passenger metrics visible': 'waitingElevatorRequests' in boot and 'elevatorPassengers' in boot,
 'schedule source remains canonical': 'CitizenDailySchedule.GetPhase(gameClock.MinuteOfDay)' in boot and 'enum CitizenDailyPhase' in schedule,
}
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
print('RESULT: %d/%d checks passed' % (len(checks)-len(failed),len(checks)))
if failed: sys.exit(1)
