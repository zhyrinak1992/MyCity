using System;
using System.Collections.Generic;
using MyCity.Core;
using UnityEngine;

namespace MyCity.Simulation
{
    public enum IndividualPassengerState { Ticketed, Registered, SecurityCleared, WaitingAtGate, Boarding, Boarded, Arrived, Missed, BaggageClaim, Exited }
    public enum PassengerProfileType { Adult, Child, FamilyMember, Assisted }
    public enum PassengerTerminalActivity { Queueing, Walking, Sitting, Browsing, AtGate }

    [Serializable]
    public sealed class IndividualAirportPassenger
    {
        public string Id;
        public string Name;
        public string FlightId;
        public string FlightNumber;
        public string Destination;
        public int SeatNumber;
        public string FamilyGroupId;
        public PassengerProfileType ProfileType;
        public bool RequiresAssistance;
        public bool HasBaggage;
        public IndividualPassengerState State;
        public int CreatedDay;
        public int CreatedMinute;
        [NonSerialized] public GameObject Visual;
        [NonSerialized] public GameObject BaggageVisual;
        [NonSerialized] public Vector3 Target;
        [NonSerialized] public int QueuePosition;
        [NonSerialized] public float WalkSpeed;
        [NonSerialized] public float ActivityTimer;
        [NonSerialized] public PassengerTerminalActivity Activity;
        [NonSerialized] public Vector3 FamilyOffset;
    }

    /// <summary>Per-person airport passenger lifecycle with lightweight runtime avatars.</summary>
    public sealed class AirportIndividualPassengerSystem : MonoBehaviour
    {
        private const int MaxVisiblePassengers = 120;
        private readonly List<IndividualAirportPassenger> passengers = new List<IndividualAirportPassenger>();
        private readonly Dictionary<string, List<IndividualAirportPassenger>> byFlight = new Dictionary<string, List<IndividualAirportPassenger>>();
        private readonly HashSet<string> generatedFlights = new HashSet<string>();
        private InternationalAirlineFleetSystem fleet;
        private GameClock clock;
        private Transform terminalRoot;
        private Material[] avatarMaterials;
        private int nameCursor;
        private int passengerSerial;
        private int boardedCursor;
        private int checkInCursor;
        private int securityCursor;
        private int baggageVisualCount;
        private readonly List<Vector3> waitingSpots = new List<Vector3>();
        private readonly List<Vector3> seatSpots = new List<Vector3>();
        private readonly HashSet<int> occupiedSeats = new HashSet<int>();
        private int activityCursor;
        private const int MaxVisibleBaggage = 80;
        private const string SaveKey = "MyCity.AirportIndividualPassengers.v1";
        private int saveCountdown;
        [Serializable] private sealed class PassengerSaveContainer { public List<PassengerSaveData> Items = new List<PassengerSaveData>(); public int Serial; public int NameCursor; }
        [Serializable] private sealed class PassengerSaveData { public string Id, Name, FlightId, FlightNumber, Destination, FamilyGroupId; public int SeatNumber, CreatedDay, CreatedMinute, ProfileType, State, QueuePosition; public bool HasBaggage, RequiresAssistance; }
        private static readonly string[] GivenNames = { "Александр", "Мария", "Даниил", "Анна", "Михаил", "София", "Артём", "Ева", "Иван", "Алиса", "Максим", "Полина", "Лев", "Виктория", "Никита", "Екатерина", "Тимур", "Дарья", "Роман", "Арина" };
        private static readonly string[] FamilyNames = { "Иванов", "Смирнова", "Петров", "Кузнецова", "Соколов", "Попова", "Лебедев", "Козлова", "Новиков", "Морозова", "Волков", "Фёдорова", "Орлов", "Андреева" };

        public IList<IndividualAirportPassenger> Passengers { get { return passengers.AsReadOnly(); } }
        public int TotalPassengers { get { return passengers.Count; } }
        public int VisiblePassengers { get; private set; }
        public int RegisteredCount { get; private set; }
        public int SecurityCount { get; private set; }
        public int WaitingCount { get; private set; }
        public int BoardingCount { get; private set; }
        public int BoardedCount { get; private set; }
        public int FamiliesCount { get; private set; }
        public int AssistedPassengersCount { get; private set; }
        public int MissedPassengersCount { get; private set; }
        public int BaggageClaimCount { get; private set; }
        public int ArrivedPassengersCount { get; private set; }
        public int ExitedPassengersCount { get; private set; }
        public int BaggageAcceptedCount { get; private set; }
        public int CheckInQueueCount { get; private set; }
        public int SecurityQueueCount { get; private set; }
        public int ActiveBaggageVisuals { get { return baggageVisualCount; } }
        public event Action<string> OnPassengerMessage;

        public void Initialize(InternationalAirlineFleetSystem airlineFleet)
        {
            if (fleet != null) fleet.OnFlightUpdated -= HandleFlightUpdated;
            if (clock != null) clock.OnTick -= HandleTick;
            fleet = airlineFleet;
            clock = GameClock.Instance;
            if (fleet != null)
            {
                fleet.OnFlightUpdated += HandleFlightUpdated;
                LoadState();
                foreach (InternationalScheduledFlight flight in fleet.Flights) EnsurePassengers(flight);
            }
            if (clock != null) clock.OnTick += HandleTick;
            EnsureTerminal();
            RefreshCounts();
        }

        private void EnsureTerminal()
        {
            if (terminalRoot != null) return;
            GameObject root = new GameObject("Airport Passenger Terminal Simulation");
            terminalRoot = root.transform;
            terminalRoot.position = new Vector3(34f, 0f, 34f);
            avatarMaterials = new Material[6];
            Color[] colors = { new Color(.20f,.42f,.78f), new Color(.72f,.28f,.25f), new Color(.24f,.58f,.38f), new Color(.68f,.48f,.20f), new Color(.48f,.34f,.68f), new Color(.28f,.58f,.62f) };
            for (int i=0;i<colors.Length;i++) { avatarMaterials[i] = new Material(Shader.Find("Standard")); avatarMaterials[i].color = colors[i]; }
            // Small terminal footprint and gate markers; passenger movement is kept inside this area.
            CreateMarker("Check-in", new Vector3(-5f, .04f, 2f), new Vector3(2f,.08f,.8f), new Color(.18f,.42f,.68f));
            CreateMarker("Security", new Vector3(0f, .04f, 2f), new Vector3(1.5f,.08f,1f), new Color(.78f,.62f,.22f));
            CreateMarker("Waiting area", new Vector3(4f, .04f, -1f), new Vector3(3f,.08f,2f), new Color(.28f,.52f,.38f));
            CreateMarker("Gate", new Vector3(7f, .04f, 2f), new Vector3(1.8f,.08f,1f), new Color(.22f,.48f,.72f));
            CreateMarker("Baggage belt", new Vector3(-1f, .04f, -4f), new Vector3(4.2f,.12f,.7f), new Color(.24f,.25f,.28f));
            CreateMarker("Baggage claim", new Vector3(-1f,.04f,-6.1f), new Vector3(4.8f,.12f,1.0f), new Color(.18f,.38f,.42f));
            CreateMarker("Arrivals exit", new Vector3(5.2f,.04f,-6.1f), new Vector3(1.8f,.12f,1.0f), new Color(.24f,.48f,.32f));
            CreateMarker("Security queue", new Vector3(1.4f, .04f, 3.8f), new Vector3(2.4f,.08f,.55f), new Color(.72f,.58f,.24f));
            // Small seating and retail/waiting zones for observable terminal life.
            CreateMarker("Cafe", new Vector3(4.8f,.04f,-3.7f), new Vector3(2.2f,.08f,1.3f), new Color(.52f,.34f,.22f));
            CreateMarker("Quiet lounge", new Vector3(7.3f,.04f,-1.4f), new Vector3(1.6f,.08f,2.2f), new Color(.32f,.38f,.48f));
            for(int i=0;i<8;i++)
            {
                Vector3 seat=new Vector3(2.6f+(i%4)*.9f,.0f,-2.0f+(i/4)*.8f);
                seatSpots.Add(seat);
                GameObject chair=GameObject.CreatePrimitive(PrimitiveType.Cube); chair.name="Terminal seat "+(i+1); chair.transform.SetParent(terminalRoot,false); chair.transform.localPosition=seat+new Vector3(0f,.24f,0f); chair.transform.localScale=new Vector3(.55f,.42f,.55f);
                Renderer cr=chair.GetComponent<Renderer>(); if(cr!=null){Material cm=new Material(Shader.Find("Standard"));cm.color=new Color(.24f,.28f,.34f);cr.material=cm;} Collider cc=chair.GetComponent<Collider>();if(cc!=null)Destroy(cc);
            }
            for(int i=0;i<5;i++) waitingSpots.Add(new Vector3(3.8f+(i%3)*.65f,0f,-.4f+(i/3)*.65f));
        }
        private void CreateMarker(string label, Vector3 local, Vector3 scale, Color color)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube); marker.name = "Terminal " + label; marker.transform.SetParent(terminalRoot,false); marker.transform.localPosition=local; marker.transform.localScale=scale;
            Renderer r=marker.GetComponent<Renderer>(); if(r!=null){Material m=new Material(Shader.Find("Standard"));m.color=color;r.material=m;}
            Collider c=marker.GetComponent<Collider>(); if(c!=null) Destroy(c);
        }

        private void HandleFlightUpdated(InternationalScheduledFlight flight)
        {
            if (flight == null) return;
            EnsurePassengers(flight);
            List<IndividualAirportPassenger> list;
            if (!byFlight.TryGetValue(flight.Id, out list)) return;
            if (flight.Phase == ScheduledFlightPhase.CheckIn)
            {
                int count=0;
                foreach (IndividualAirportPassenger p in list) if(p.State==IndividualPassengerState.Ticketed){p.QueuePosition=count++;SetTarget(p, new Vector3(-5f + (p.QueuePosition%4)*.48f,0f,1.1f-(p.QueuePosition/4)*.34f));}
                Action<string> h=OnPassengerMessage;if(h!=null && count>0)h(flight.FlightNumber+": открыта индивидуальная очередь регистрации.");
            }
            else if (flight.Phase == ScheduledFlightPhase.Boarding)
            {
                // Security processing is performed per passenger in HandleTick; no mass status jump.
                foreach(IndividualAirportPassenger p in list) if(p.State==IndividualPassengerState.Registered || p.State==IndividualPassengerState.SecurityCleared){SetTarget(p,new Vector3(1.4f,0f,3.8f));}
            }
            else if (flight.Phase == ScheduledFlightPhase.Departed)
            {
                foreach(IndividualAirportPassenger p in list){if(p.State==IndividualPassengerState.Boarded || p.State==IndividualPassengerState.Boarding)p.State=IndividualPassengerState.Boarded;else p.State=IndividualPassengerState.Missed;DestroyVisual(p);DestroyBaggageVisual(p);}
            }
            else if (flight.Phase == ScheduledFlightPhase.Arrived)
            {
                // Arrival is an individual process: passengers leave the aircraft, collect bags, then exit.
                List<IndividualAirportPassenger> arrivals = list.FindAll(x => x.State == IndividualPassengerState.Boarded);
                for (int i=0;i<arrivals.Count;i++)
                {
                    IndividualAirportPassenger pax=arrivals[i];
                    pax.State=IndividualPassengerState.Arrived;
                    pax.Activity=PassengerTerminalActivity.Walking;
                    pax.ActivityTimer=8f+(pax.SeatNumber%7)*2f;
                    CreateArrivalAvatar(pax,i);
                    if(pax.HasBaggage) CreateArrivalBaggage(pax,i);
                    SetTarget(pax,pax.HasBaggage?new Vector3(-1f+(i%5)*.3f,0f,-6.1f):new Vector3(5.2f,0f,-6.1f));
                }
            }
            RefreshCounts();
        }

        private void EnsurePassengers(InternationalScheduledFlight flight)
        {
            if(flight==null || string.IsNullOrEmpty(flight.Id) || generatedFlights.Contains(flight.Id))return;
            generatedFlights.Add(flight.Id);
            int count=Mathf.Clamp(flight.TicketsSold>0?flight.TicketsSold:flight.PlannedPassengers,0,Mathf.Max(0,flight.Capacity));
            List<IndividualAirportPassenger> list=new List<IndividualAirportPassenger>(count);
            for(int i=0;i<count;i++)
            {
                passengerSerial++;
                string first=GivenNames[nameCursor%GivenNames.Length]; string last=FamilyNames[(nameCursor*7+i)%FamilyNames.Length]; nameCursor++;
                PassengerProfileType profile=(i%19==0)?PassengerProfileType.Assisted:((i%7==0)?PassengerProfileType.Child:((i%4==0)?PassengerProfileType.FamilyMember:PassengerProfileType.Adult));
                IndividualAirportPassenger p=new IndividualAirportPassenger { Id="PAX-"+passengerSerial.ToString("D6"), Name=first+" "+last,
                    FlightId=flight.Id, FlightNumber=flight.FlightNumber, Destination=flight.DestinationCity, SeatNumber=i+1,
                    FamilyGroupId=(profile==PassengerProfileType.Child || profile==PassengerProfileType.FamilyMember)?"FAM-"+flight.Id+"-"+(i/4):null, ProfileType=profile, RequiresAssistance=profile==PassengerProfileType.Assisted,
                    State=IndividualPassengerState.Ticketed, QueuePosition=i, CreatedDay=flight.Day, CreatedMinute=flight.DepartureMinute-90, WalkSpeed=profile==PassengerProfileType.Assisted?0.75f:1.1f+(i%5)*.08f };
                passengers.Add(p);list.Add(p);
            }
            byFlight[flight.Id]=list;
            EnsureTerminal();
            CreateVisibleAvatars(list);
            SaveState();
        }

        private void CreateVisibleAvatars(List<IndividualAirportPassenger> list)
        {
            int available=Mathf.Max(0,MaxVisiblePassengers-VisiblePassengers);
            for(int i=0;i<list.Count && available>0;i++,available--)
            {
                IndividualAirportPassenger p=list[i];
                GameObject body=GameObject.CreatePrimitive(PrimitiveType.Capsule);body.name=p.Id+" · "+p.Name;body.transform.SetParent(terminalRoot,false);
                body.transform.localScale=new Vector3(.34f,.82f,.34f);body.transform.localPosition=new Vector3(-7f+(i%14)*.52f,.82f,-4f+(i/14)*.58f);
                Renderer renderer=body.GetComponent<Renderer>();if(renderer!=null)renderer.sharedMaterial=avatarMaterials[i%avatarMaterials.Length];
                Collider collider=body.GetComponent<Collider>();if(collider!=null)Destroy(collider);
                GameObject head=GameObject.CreatePrimitive(PrimitiveType.Sphere);head.name="Head";head.transform.SetParent(body.transform,false);head.transform.localPosition=new Vector3(0f,.72f,0f);head.transform.localScale=new Vector3(.55f,.48f,.55f);
                Renderer hr=head.GetComponent<Renderer>();if(hr!=null){Material hm=new Material(Shader.Find("Standard"));hm.color=new Color(.78f,.64f,.53f);hr.material=hm;}
                Collider hc=head.GetComponent<Collider>();if(hc!=null)Destroy(hc);
                p.Visual=body;p.Target=body.transform.position;p.WalkSpeed=p.RequiresAssistance ? 0.75f : 1.15f;p.Activity=PassengerTerminalActivity.Queueing;p.ActivityTimer=2f+(i%5);VisiblePassengers++;
                if(p.HasBaggage && p.State!=IndividualPassengerState.Boarded && p.State!=IndividualPassengerState.Arrived && p.State!=IndividualPassengerState.Missed)CreateBaggageVisual(p);
                if(p.State==IndividualPassengerState.Ticketed)SetTarget(p,new Vector3(-5f+(p.QueuePosition%4)*.48f,0f,1.1f-(p.QueuePosition/4)*.34f));
                else if(p.State==IndividualPassengerState.Registered)SetTarget(p,new Vector3(1.4f,0f,3.8f));
                else if(p.State==IndividualPassengerState.SecurityCleared || p.State==IndividualPassengerState.WaitingAtGate)SetTarget(p,new Vector3(4f+(i%4)*.35f,0f,-1f+(i%3)*.3f));
                else if(p.State==IndividualPassengerState.Boarding)SetTarget(p,new Vector3(7f,0f,2f));
                else if(p.State==IndividualPassengerState.Arrived)SetTarget(p,p.HasBaggage?new Vector3(-1f,0f,-6.1f):new Vector3(5.2f,0f,-6.1f));
                else if(p.State==IndividualPassengerState.BaggageClaim)SetTarget(p,new Vector3(-1f,0f,-6.1f));
                else if(p.State==IndividualPassengerState.Exited)DestroyVisual(p);
            }
        }
        private void CreateBaggageVisual(IndividualAirportPassenger p)
        {
            if (p == null || !p.HasBaggage || p.BaggageVisual != null || baggageVisualCount >= MaxVisibleBaggage) return;
            GameObject bag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bag.name = "Baggage " + p.Id;
            bag.transform.SetParent(terminalRoot, false);
            bag.transform.localScale = new Vector3(.34f,.28f,.22f);
            bag.transform.localPosition = new Vector3(-1.8f + (baggageVisualCount % 12) * .29f,.22f,-4f);
            Renderer r = bag.GetComponent<Renderer>();
            if (r != null) { Material m = new Material(Shader.Find("Standard")); m.color = new Color(.22f + (p.SeatNumber%4)*.12f,.24f,.30f + (p.SeatNumber%3)*.1f); r.material = m; }
            Collider c = bag.GetComponent<Collider>(); if (c != null) Destroy(c);
            p.BaggageVisual = bag; baggageVisualCount++;
        }
        private void CreateArrivalAvatar(IndividualAirportPassenger p,int index)
        {
            if(p==null || p.Visual!=null || VisiblePassengers>=MaxVisiblePassengers)return;
            GameObject body=GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name=p.Id+" · прибытие"; body.transform.SetParent(terminalRoot,false);
            body.transform.localScale=new Vector3(.34f,.82f,.34f); body.transform.localPosition=new Vector3(7.8f+(index%8)*.42f,.82f,-5.2f+(index/8)*.42f);
            Renderer r=body.GetComponent<Renderer>(); if(r!=null)r.sharedMaterial=avatarMaterials[index%avatarMaterials.Length];
            Collider c=body.GetComponent<Collider>(); if(c!=null)Destroy(c);
            GameObject head=GameObject.CreatePrimitive(PrimitiveType.Sphere); head.name="Head"; head.transform.SetParent(body.transform,false); head.transform.localPosition=new Vector3(0f,.72f,0f); head.transform.localScale=new Vector3(.55f,.48f,.55f);
            Renderer hr=head.GetComponent<Renderer>(); if(hr!=null){Material hm=new Material(Shader.Find("Standard"));hm.color=new Color(.78f,.64f,.53f);hr.material=hm;}
            Collider hc=head.GetComponent<Collider>();if(hc!=null)Destroy(hc);
            p.Visual=body;p.Target=body.transform.position;p.WalkSpeed=p.RequiresAssistance?.75f:1.15f;VisiblePassengers++;
        }
        private void CreateArrivalBaggage(IndividualAirportPassenger p,int index)
        {
            if(p==null || !p.HasBaggage || p.BaggageVisual!=null || baggageVisualCount>=MaxVisibleBaggage)return;
            GameObject bag=GameObject.CreatePrimitive(PrimitiveType.Cube);bag.name="Baggage claim · "+p.Id;bag.transform.SetParent(terminalRoot,false);
            bag.transform.localScale=new Vector3(.36f,.28f,.25f);bag.transform.localPosition=new Vector3(-3.0f+(index%12)*.48f,.25f,-6.1f);
            Renderer r=bag.GetComponent<Renderer>();if(r!=null){Material m=new Material(Shader.Find("Standard"));m.color=new Color(.22f+(p.SeatNumber%4)*.12f,.24f,.30f+(p.SeatNumber%3)*.1f);r.material=m;}
            Collider c=bag.GetComponent<Collider>();if(c!=null)Destroy(c);p.BaggageVisual=bag;baggageVisualCount++;
        }
        private void DestroyBaggageVisual(IndividualAirportPassenger p)
        {
            if (p != null && p.BaggageVisual != null) { Destroy(p.BaggageVisual); p.BaggageVisual = null; baggageVisualCount = Mathf.Max(0,baggageVisualCount-1); }
        }
        private void SetTarget(IndividualAirportPassenger p,Vector3 local){if(p!=null && p.Visual!=null)p.Target=terminalRoot.TransformPoint(local);}
        private void Update()
        {
            float dt=Time.deltaTime;
            for(int i=0;i<passengers.Count;i++)
            {
                IndividualAirportPassenger p=passengers[i]; if(p.Visual==null)continue;
                if(p.State==IndividualPassengerState.Arrived)
                {
                    Vector3 claim=p.HasBaggage?terminalRoot.TransformPoint(new Vector3(-1f,.82f,-6.1f)):terminalRoot.TransformPoint(new Vector3(5.2f,.82f,-6.1f));
                    if(Vector3.Distance(p.Visual.transform.position,claim)<.65f)
                    {
                        if(p.HasBaggage){p.State=IndividualPassengerState.BaggageClaim;p.Activity=PassengerTerminalActivity.AtGate;p.ActivityTimer=5f+(p.SeatNumber%6)*2f;}
                        else {p.State=IndividualPassengerState.Exited;DestroyVisual(p);}
                    }
                }
                else if(p.State==IndividualPassengerState.BaggageClaim)
                {
                    p.ActivityTimer-=dt;
                    if(p.ActivityTimer<=0f){DestroyBaggageVisual(p);p.State=IndividualPassengerState.Exited;SetTarget(p,new Vector3(5.2f,0f,-6.1f));}
                }
                else if(p.State==IndividualPassengerState.Exited)
                {
                    if(p.Visual!=null && Vector3.Distance(p.Visual.transform.position,terminalRoot.TransformPoint(new Vector3(5.2f,.82f,-6.1f)))<.55f)DestroyVisual(p);
                }
                if(p.State==IndividualPassengerState.WaitingAtGate)
                {
                    p.ActivityTimer-=dt;
                    if(p.ActivityTimer<=0f && p.Activity!=PassengerTerminalActivity.AtGate)
                    {
                        ChooseWaitingActivity(p);
                    }
                }
                // Family members remain near the first visible member of their family group.
                if(!string.IsNullOrEmpty(p.FamilyGroupId) && p.State==IndividualPassengerState.WaitingAtGate)
                {
                    IndividualAirportPassenger leader=FindFamilyLeader(p);
                    if(leader!=null && leader!=p && leader.Visual!=null && leader.State==IndividualPassengerState.WaitingAtGate && p.Activity!=PassengerTerminalActivity.Sitting)
                        p.Target=leader.Target+p.FamilyOffset;
                }
                Vector3 current=p.Visual.transform.position; Vector3 target=p.Target; target.y=current.y;
                if(p.Activity==PassengerTerminalActivity.Sitting)
                {
                    p.Visual.transform.localScale=new Vector3(.34f,.52f,.34f);
                    current.y=.58f; p.Visual.transform.position=new Vector3(p.Visual.transform.position.x,.58f,p.Visual.transform.position.z);
                }
                else
                {
                    p.Visual.transform.localScale=new Vector3(.34f,.82f,.34f);
                    current.y=.82f; p.Visual.transform.position=new Vector3(p.Visual.transform.position.x,.82f,p.Visual.transform.position.z);
                }
                Vector3 separation=Vector3.zero;
                for(int j=0;j<passengers.Count;j++)
                {
                    if(i==j)continue; GameObject other=passengers[j].Visual;if(other==null)continue;
                    Vector3 delta=current-other.transform.position; delta.y=0f; float d=delta.magnitude;
                    if(d>0.001f && d<.48f) separation+=delta.normalized*((.48f-d)*.55f);
                }
                Vector3 adjusted=target+separation;
                if(Vector3.Distance(current,adjusted)>.08f)
                {
                    Vector3 next=Vector3.MoveTowards(current,adjusted,p.WalkSpeed*dt);p.Visual.transform.position=next;
                    Vector3 dir=adjusted-current;if(dir.sqrMagnitude>.01f)p.Visual.transform.rotation=Quaternion.Slerp(p.Visual.transform.rotation,Quaternion.LookRotation(dir),dt*5f);
                }
                if(p.State==IndividualPassengerState.SecurityCleared && Vector3.Distance(p.Visual.transform.position,terminalRoot.TransformPoint(new Vector3(4f,.82f,-1f)))<1.4f)
                { p.State=IndividualPassengerState.WaitingAtGate;p.Activity=PassengerTerminalActivity.Walking;p.ActivityTimer=2f+(p.SeatNumber%5); AssignFamilyOffset(p); }
                if(p.BaggageVisual!=null && p.State!=IndividualPassengerState.Boarded && p.State!=IndividualPassengerState.Arrived && p.State!=IndividualPassengerState.Missed)
                    p.BaggageVisual.transform.localPosition=Vector3.MoveTowards(p.BaggageVisual.transform.localPosition,new Vector3(-1f+(p.SeatNumber%8)*.35f,.22f,-4f),dt*.35f);
                if(p.BaggageVisual!=null && (p.State==IndividualPassengerState.Arrived || p.State==IndividualPassengerState.BaggageClaim))
                {
                    Vector3 bp=p.BaggageVisual.transform.localPosition;bp.x+=dt*.42f;if(bp.x>1.35f)bp.x=-3.25f;bp.y=.25f;bp.z=-6.1f;p.BaggageVisual.transform.localPosition=bp;
                }
            }
        }
        private IndividualAirportPassenger FindFamilyLeader(IndividualAirportPassenger passenger)
        {
            if(passenger==null || string.IsNullOrEmpty(passenger.FamilyGroupId))return null;
            IndividualAirportPassenger leader=null;
            for(int i=0;i<passengers.Count;i++)
            {
                IndividualAirportPassenger candidate=passengers[i];
                if(candidate.FamilyGroupId!=passenger.FamilyGroupId || candidate.Visual==null)continue;
                if(leader==null || candidate.SeatNumber<leader.SeatNumber)leader=candidate;
            }
            return leader;
        }
        private void AssignFamilyOffset(IndividualAirportPassenger p)
        {
            if(p==null || string.IsNullOrEmpty(p.FamilyGroupId))return;
            int rank=0; for(int i=0;i<passengers.Count;i++)if(passengers[i].FamilyGroupId==p.FamilyGroupId && passengers[i].SeatNumber<p.SeatNumber)rank++;
            p.FamilyOffset=new Vector3((rank%3-1)*.42f,0f,(rank/3)*.42f);
        }
        private void ChooseWaitingActivity(IndividualAirportPassenger p)
        {
            if(p==null || p.Visual==null)return;
            // Families tend to stay together; adults and solo passengers can browse the cafe/lounge.
            int choice=(p.SeatNumber+activityCursor++)%10;
            if(choice<3 && seatSpots.Count>0)
            {
                int seat=-1;for(int k=0;k<seatSpots.Count;k++){int candidate=(p.SeatNumber+k)%seatSpots.Count;if(!occupiedSeats.Contains(candidate)){seat=candidate;break;}}
                if(seat>=0){occupiedSeats.Add(seat);p.Target=terminalRoot.TransformPoint(seatSpots[seat]);p.Activity=PassengerTerminalActivity.Sitting;p.ActivityTimer=5f+(p.SeatNumber%8)*2f;return;}
            }
            if(choice<6){p.Target=terminalRoot.TransformPoint(new Vector3(4.8f,0f,-3.7f));p.Activity=PassengerTerminalActivity.Browsing;p.ActivityTimer=4f+(p.SeatNumber%6)*2f;}
            else if(choice<8){p.Target=terminalRoot.TransformPoint(new Vector3(7.3f,0f,-1.4f));p.Activity=PassengerTerminalActivity.Walking;p.ActivityTimer=5f+(p.SeatNumber%5)*2f;}
            else {p.Target=terminalRoot.TransformPoint(waitingSpots[p.SeatNumber%waitingSpots.Count]);p.Activity=PassengerTerminalActivity.AtGate;p.ActivityTimer=999f;}
        }
        private void HandleTick(long tick)
        {
            if(clock==null)return;
            foreach(KeyValuePair<string,List<IndividualAirportPassenger>> pair in byFlight)
            {
                InternationalScheduledFlight flight=FindFlight(pair.Key);
                List<IndividualAirportPassenger> list=pair.Value;
                if(flight==null)continue;
                if(flight.Day < clock.Day)
                {
                    for(int i=0;i<list.Count;i++)
                        if(list[i].State!=IndividualPassengerState.Boarded && list[i].State!=IndividualPassengerState.Arrived && list[i].State!=IndividualPassengerState.Missed)
                        { list[i].State=IndividualPassengerState.Missed; DestroyVisual(list[i]); DestroyBaggageVisual(list[i]); }
                    continue;
                }
                if(flight.Phase==ScheduledFlightPhase.CheckIn)
                {
                    // Individual check-in queue: process a small number per simulation tick.
                    int processed=0;
                    for(int i=0;i<list.Count && processed<3;i++)
                    {
                        IndividualAirportPassenger pax=list[i];
                        if(pax.State!=IndividualPassengerState.Ticketed)continue;
                        pax.State=IndividualPassengerState.Registered;
                        pax.HasBaggage=(pax.SeatNumber%5)!=0;
                        SetTarget(pax,new Vector3(-5f + (pax.QueuePosition%4)*.48f,0f,1.1f-(pax.QueuePosition/4)*.34f));
                        if(pax.HasBaggage)CreateBaggageVisual(pax);
                        processed++;
                    }
                    // Passengers leave the counter toward security after registration.
                    for(int i=0;i<list.Count;i++) if(list[i].State==IndividualPassengerState.Registered && (tick+i)%4==0) SetTarget(list[i],new Vector3(1.4f,0f,3.8f));
                }
                else if(flight.Phase==ScheduledFlightPhase.Boarding)
                {
                    // Drain any remaining check-in queue before the security and gate queues.
                    int checkInProcessed=0;
                    for(int i=0;i<list.Count && checkInProcessed<3;i++)
                    {
                        IndividualAirportPassenger pax=list[i];
                        if(pax.State!=IndividualPassengerState.Ticketed)continue;
                        pax.State=IndividualPassengerState.Registered;
                        pax.HasBaggage=(pax.SeatNumber%5)!=0;
                        if(pax.HasBaggage)CreateBaggageVisual(pax);
                        SetTarget(pax,new Vector3(1.4f,0f,3.8f));
                        checkInProcessed++;
                    }
                    int processed=0;
                    for(int i=0;i<list.Count && processed<4;i++)
                    {
                        IndividualAirportPassenger pax=list[i];
                        if(pax.State!=IndividualPassengerState.Registered)continue;
                        pax.State=IndividualPassengerState.SecurityCleared;
                        SetTarget(pax,new Vector3(4f + (pax.SeatNumber%5)*.35f,0f,-1f + (pax.SeatNumber%3)*.3f));
                        processed++;
                    }
                    int boarding=0;
                    for(int i=0;i<list.Count && boarding<3;i++)
                    {
                        IndividualAirportPassenger pax=list[i];
                        if(pax.State!=IndividualPassengerState.SecurityCleared && pax.State!=IndividualPassengerState.WaitingAtGate)continue;
                        pax.State=IndividualPassengerState.Boarding;
                        SetTarget(pax,new Vector3(7f,0f,2f));
                        boarding++;
                    }
                    for(int i=0;i<list.Count;i++)
                    {
                        IndividualAirportPassenger pax=list[i];
                        if(pax.State==IndividualPassengerState.Boarding && pax.Visual!=null && Vector3.Distance(pax.Visual.transform.position,terminalRoot.TransformPoint(new Vector3(7f,.82f,2f)))<.6f)
                        { pax.State=IndividualPassengerState.Boarded; DestroyBaggageVisual(pax); }
                    }
                }
            }
            RefreshCounts();
            if(++saveCountdown>=5){saveCountdown=0;SaveState();}
        }
        private InternationalScheduledFlight FindFlight(string id){if(fleet==null)return null;foreach(InternationalScheduledFlight f in fleet.Flights)if(f.Id==id)return f;return null;}
        private void DestroyVisual(IndividualAirportPassenger p){if(p.Visual!=null){Destroy(p.Visual);p.Visual=null;VisiblePassengers=Mathf.Max(0,VisiblePassengers-1);}}
        private void RefreshCounts(){RegisteredCount=SecurityCount=WaitingCount=BoardingCount=BoardedCount=0;FamiliesCount=AssistedPassengersCount=MissedPassengersCount=BaggageClaimCount=ArrivedPassengersCount=ExitedPassengersCount=0;BaggageAcceptedCount=CheckInQueueCount=SecurityQueueCount=0;HashSet<string> familyIds=new HashSet<string>();foreach(IndividualAirportPassenger p in passengers){if((int)p.State>=(int)IndividualPassengerState.Registered && p.State!=IndividualPassengerState.Missed)RegisteredCount++;if(p.State==IndividualPassengerState.Ticketed)CheckInQueueCount++;if(p.State==IndividualPassengerState.Registered)SecurityQueueCount++;if(p.HasBaggage && p.State!=IndividualPassengerState.Missed)BaggageAcceptedCount++;if((int)p.State>=(int)IndividualPassengerState.SecurityCleared && p.State!=IndividualPassengerState.Missed)SecurityCount++;if(p.State==IndividualPassengerState.WaitingAtGate)WaitingCount++;if(p.State==IndividualPassengerState.Boarding)BoardingCount++;if(p.State==IndividualPassengerState.Boarded || p.State==IndividualPassengerState.Arrived)BoardedCount++;if(!string.IsNullOrEmpty(p.FamilyGroupId))familyIds.Add(p.FamilyGroupId);if(p.RequiresAssistance)AssistedPassengersCount++;if(p.State==IndividualPassengerState.Missed)MissedPassengersCount++;if(p.State==IndividualPassengerState.Arrived)ArrivedPassengersCount++;if(p.State==IndividualPassengerState.BaggageClaim)BaggageClaimCount++;if(p.State==IndividualPassengerState.Exited)ExitedPassengersCount++;}FamiliesCount=familyIds.Count;}
        public string GetActivityLabel(IndividualAirportPassenger passenger){if(passenger==null)return "—";switch(passenger.Activity){case PassengerTerminalActivity.Sitting:return "Сидит в зоне ожидания";case PassengerTerminalActivity.Browsing:return "В кафе";case PassengerTerminalActivity.Walking:return "Гуляет по терминалу";case PassengerTerminalActivity.AtGate:return "У выхода на посадку";default:return "В очереди";}}
        public string GetStateLabel(IndividualPassengerState state){switch(state){case IndividualPassengerState.Registered:return "Зарегистрирован";case IndividualPassengerState.SecurityCleared:return "Контроль пройден";case IndividualPassengerState.WaitingAtGate:return "Ожидает у выхода";case IndividualPassengerState.Boarding:return "Идёт на посадку";case IndividualPassengerState.Boarded:return "На борту";case IndividualPassengerState.Arrived:return "Прибыл";case IndividualPassengerState.Missed:return "Опоздал";case IndividualPassengerState.BaggageClaim:return "Получает багаж";case IndividualPassengerState.Exited:return "Покинул терминал";default:return "Билет оформлен";}}
        private void SaveState()
        {
            PassengerSaveContainer container=new PassengerSaveContainer { Serial=passengerSerial, NameCursor=nameCursor };
            foreach(IndividualAirportPassenger p in passengers) container.Items.Add(new PassengerSaveData { Id=p.Id, Name=p.Name, FlightId=p.FlightId, FlightNumber=p.FlightNumber, Destination=p.Destination, FamilyGroupId=p.FamilyGroupId, SeatNumber=p.SeatNumber, CreatedDay=p.CreatedDay, CreatedMinute=p.CreatedMinute, ProfileType=(int)p.ProfileType, State=(int)p.State, QueuePosition=p.QueuePosition, HasBaggage=p.HasBaggage, RequiresAssistance=p.RequiresAssistance });
            PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(container)); PlayerPrefs.Save();
        }
        private void LoadState()
        {
            if(!PlayerPrefs.HasKey(SaveKey))return;
            try
            {
                PassengerSaveContainer container=JsonUtility.FromJson<PassengerSaveContainer>(PlayerPrefs.GetString(SaveKey)); if(container==null || container.Items==null)return;
                passengerSerial=Mathf.Max(passengerSerial,container.Serial);nameCursor=Mathf.Max(nameCursor,container.NameCursor);
                foreach(PassengerSaveData d in container.Items)
                {
                    if(d==null || string.IsNullOrEmpty(d.Id) || string.IsNullOrEmpty(d.FlightId))continue;
                    IndividualAirportPassenger p=new IndividualAirportPassenger { Id=d.Id,Name=d.Name,FlightId=d.FlightId,FlightNumber=d.FlightNumber,Destination=d.Destination,FamilyGroupId=d.FamilyGroupId,SeatNumber=d.SeatNumber,CreatedDay=d.CreatedDay,CreatedMinute=d.CreatedMinute,QueuePosition=d.QueuePosition,ProfileType=(PassengerProfileType)Mathf.Clamp(d.ProfileType,0,3),State=(IndividualPassengerState)Mathf.Clamp(d.State,0,9),HasBaggage=d.HasBaggage,RequiresAssistance=d.RequiresAssistance,WalkSpeed=d.RequiresAssistance ? .75f : 1.15f };
                    passengers.Add(p);List<IndividualAirportPassenger> list;if(!byFlight.TryGetValue(p.FlightId,out list)){list=new List<IndividualAirportPassenger>();byFlight[p.FlightId]=list;}list.Add(p);generatedFlights.Add(p.FlightId);
                }
                EnsureTerminal();foreach(KeyValuePair<string,List<IndividualAirportPassenger>> pair in byFlight)CreateVisibleAvatars(pair.Value.FindAll(x=>x.State!=IndividualPassengerState.Boarded && x.State!=IndividualPassengerState.Arrived && x.State!=IndividualPassengerState.Missed && x.State!=IndividualPassengerState.Exited));
            }
            catch(Exception ex){Debug.LogWarning("Airport passenger save could not be loaded: "+ex.Message);}
        }
        private void OnApplicationPause(bool paused){if(paused)SaveState();}
        private void OnApplicationQuit(){SaveState();}
        private void OnDestroy(){SaveState();if(fleet!=null)fleet.OnFlightUpdated-=HandleFlightUpdated;if(clock!=null)clock.OnTick-=HandleTick;}
    }
}
