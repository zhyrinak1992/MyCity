using System;
using System.Collections.Generic;
using MyCity.Core;
using UnityEngine;

namespace MyCity.Simulation
{
    public enum AircraftOperationalState { Available, Assigned, InFlight, Turnaround, Maintenance }
    public enum ScheduledFlightPhase { Scheduled, CheckIn, Boarding, Departed, Arrived, Turnaround }

    [Serializable]
    public sealed class AircraftRecord
    {
        public string Id;
        public string Registration;
        public string Model;
        public int PassengerCapacity;
        public int RangeKm;
        public int CruiseSpeedKmh;
        public int OperatingCostPerFlight;
        public AircraftOperationalState State;
        public string AssignedDestinationId;
    }

    [Serializable]
    public sealed class InternationalScheduledFlight
    {
        public string Id;
        public string FlightNumber;
        public string AircraftId;
        public string DestinationId;
        public string DestinationCity;
        public int Day;
        public int DepartureMinute;
        public int DurationMinutes;
        public int PlannedPassengers;
        public int Capacity;
        public ScheduledFlightPhase Phase;
        public int TicketFare;
        public int TicketsSold;
        public long TicketRevenue;
        public long OperatingCost;
        public long OperatingResult;
        public bool FinanciallySettled;
    }

    /// <summary>
    /// Playable management slice: aircraft inventory, range/capacity validation, persistent route
    /// assignment and deterministic daily departure/arrival phases. Aircraft are not yet rendered
    /// as moving 3D objects; ticket sales and cash settlement are intentionally deferred.
    /// </summary>
    public sealed class InternationalAirlineFleetSystem : MonoBehaviour
    {
        private readonly List<AircraftRecord> aircraft = new List<AircraftRecord>();
        private readonly List<InternationalScheduledFlight> flights = new List<InternationalScheduledFlight>();
        private InternationalAirportNetworkSystem network;
        private GameClock clock;
        private int nextFlightNumber = 601;

        public IList<AircraftRecord> Aircraft { get { return aircraft.AsReadOnly(); } }
        public IList<InternationalScheduledFlight> Flights { get { return flights.AsReadOnly(); } }
        public int AssignedAircraftCount { get { int n=0; for(int i=0;i<aircraft.Count;i++) if(aircraft[i].State!=AircraftOperationalState.Available)n++; return n; } }
        public int ScheduledToday { get { int n=0; for(int i=0;i<flights.Count;i++) if(clock==null || flights[i].Day==clock.Day)n++; return n; } }
        public event Action<InternationalScheduledFlight> OnFlightUpdated;

        private void OnEnable() { TrySubscribe(); }
        private void Update() { if(clock==null || network==null) TrySubscribe(); }
        private void OnDisable()
        {
            if(clock!=null) { clock.OnTick-=HandleTick; clock.OnDayChanged-=HandleDayChanged; }
            clock=null;
        }

        public void InitializeDefaults(InternationalAirportNetworkSystem routeNetwork)
        {
            network=routeNetwork;
            if(aircraft.Count==0)
            {
                AddAircraft("MC-A320-01","RA-MC01","Aerojet A320",180,6100,830,145000);
                AddAircraft("MC-B737-02","RA-MC02","Aerojet B737",189,5900,840,148000);
                AddAircraft("MC-A321-03","RA-MC03","Aerojet A321",220,7400,835,178000);
                AddAircraft("MC-B787-04","RA-MC04","Aerojet B787",290,13600,900,390000);
                AddAircraft("MC-A350-05","RA-MC05","Aerojet A350",320,15000,905,425000);
                AddAircraft("MC-E190-06","RA-MC06","Aerojet E190",100,4500,820,98000);
            }
            TrySubscribe();
        }

        private void AddAircraft(string id,string registration,string model,int seats,int range,int speed,int cost)
        {
            aircraft.Add(new AircraftRecord { Id=id, Registration=registration, Model=model,
                PassengerCapacity=seats, RangeKm=range, CruiseSpeedKmh=speed,
                OperatingCostPerFlight=cost, State=AircraftOperationalState.Available });
        }

        public bool TryAssignAircraft(string destinationId, out string message)
        {
            message="";
            if(network==null) { message="Сеть международных направлений не подключена."; return false; }
            InternationalDestination destination=null;
            IList<InternationalDestination> destinations=network.Destinations;
            for(int i=0;i<destinations.Count;i++) if(destinations[i].Id==destinationId) { destination=destinations[i]; break; }
            if(destination==null || !destination.IsRouteOpen) { message="Сначала откройте международный маршрут."; return false; }
            for(int i=0;i<aircraft.Count;i++)
            {
                AircraftRecord a=aircraft[i];
                if(a.State!=AircraftOperationalState.Available || a.RangeKm<destination.DistanceKm) continue;
                int assignedOnDay=0;
                for(int f=0;f<flights.Count;f++) if(flights[f].Day==(clock!=null?clock.Day:1)) assignedOnDay++;
                int earliest=clock!=null?clock.MinuteOfDay+90:360;
                int departure=Mathf.Max(360,earliest)+assignedOnDay*75;
                if(departure>1320) { message="На сегодня нет свободного окна расписания. Попробуйте завтра."; return false; }
                int passengers=Mathf.Min(a.PassengerCapacity,destination.BaseDemandPerDay);
                InternationalScheduledFlight flight=new InternationalScheduledFlight {
                    Id="intl-flight-"+nextFlightNumber, FlightNumber="MC"+nextFlightNumber,
                    AircraftId=a.Id, DestinationId=destination.Id, DestinationCity=destination.City,
                    Day=clock!=null?clock.Day:1, DepartureMinute=departure,
                    DurationMinutes=destination.FlightMinutes, PlannedPassengers=passengers,
                    Capacity=a.PassengerCapacity, Phase=ScheduledFlightPhase.Scheduled };
                nextFlightNumber++;
                flights.Add(flight);
                a.State=AircraftOperationalState.Assigned;
                a.AssignedDestinationId=destination.Id;
                Action<InternationalScheduledFlight> handler=OnFlightUpdated; if(handler!=null) handler(flight);
                GameEventBus.Publish(new GameEvent(GameEventType.FlightPhaseChanged,clock!=null?clock.Tick:0,
                    "international-fleet",flight.Id,"SCHEDULED:"+destination.City));
                message="Борт "+a.Registration+" назначен на рейс "+flight.FlightNumber+" · "+destination.City+".";
                return true;
            }
            message="Нет свободного самолёта с достаточной дальностью. Проверьте парк.";
            return false;
        }

        public bool TryUnassignAircraft(string destinationId,out string message)
        {
            for(int i=flights.Count-1;i>=0;i--)
            {
                InternationalScheduledFlight f=flights[i];
                if(f.DestinationId!=destinationId || f.Phase==ScheduledFlightPhase.Departed || f.Phase==ScheduledFlightPhase.Arrived) continue;
                AircraftRecord a=FindAircraft(f.AircraftId);
                if(a!=null) { a.State=AircraftOperationalState.Available; a.AssignedDestinationId=null; }
                flights.RemoveAt(i);
                message="Назначение рейса отменено."; return true;
            }
            message="Нет отменяемого рейса по этому направлению."; return false;
        }

        public AircraftRecord FindAircraft(string id)
        { for(int i=0;i<aircraft.Count;i++) if(aircraft[i].Id==id)return aircraft[i]; return null; }

        public string GetAircraftStateLabel(AircraftOperationalState state)
        {
            switch(state)
            {
                case AircraftOperationalState.Assigned:return "Назначен";
                case AircraftOperationalState.InFlight:return "В полёте";
                case AircraftOperationalState.Turnaround:return "Оборот";
                case AircraftOperationalState.Maintenance:return "Обслуживание";
                default:return "Свободен";
            }
        }
        public string GetFlightPhaseLabel(ScheduledFlightPhase phase)
        {
            switch(phase)
            {
                case ScheduledFlightPhase.CheckIn:return "Регистрация";
                case ScheduledFlightPhase.Boarding:return "Посадка";
                case ScheduledFlightPhase.Departed:return "В пути";
                case ScheduledFlightPhase.Arrived:return "Прибыл";
                case ScheduledFlightPhase.Turnaround:return "Подготовка борта";
                default:return "Запланирован";
            }
        }

        private void TrySubscribe()
        {
            GameClock current=GameClock.Instance;
            if(current!=null && current!=clock)
            {
                if(clock!=null) { clock.OnTick-=HandleTick; clock.OnDayChanged-=HandleDayChanged; }
                clock=current; clock.OnTick+=HandleTick; clock.OnDayChanged+=HandleDayChanged;
            }
        }
        private void HandleDayChanged(int day)
        {
            for(int i=0;i<aircraft.Count;i++)
            {
                AircraftRecord a=aircraft[i];
                if(a.State==AircraftOperationalState.InFlight || a.State==AircraftOperationalState.Turnaround)
                    a.State=AircraftOperationalState.Available;
            }
            for(int i=0;i<flights.Count;i++)
                if(flights[i].Day<day && flights[i].Phase!=ScheduledFlightPhase.Arrived) flights[i].Phase=ScheduledFlightPhase.Arrived;
        }
        private void HandleTick(long tick)
        {
            if(clock==null)return;
            for(int i=0;i<flights.Count;i++)
            {
                InternationalScheduledFlight f=flights[i];
                if(f.Day!=clock.Day)continue;
                int elapsed=clock.MinuteOfDay-f.DepartureMinute;
                ScheduledFlightPhase next;
                if(elapsed < -90) next=ScheduledFlightPhase.Scheduled;
                else if(elapsed < -45) next=ScheduledFlightPhase.CheckIn;
                else if(elapsed < 0) next=ScheduledFlightPhase.Boarding;
                else if(elapsed < f.DurationMinutes) next=ScheduledFlightPhase.Departed;
                else next=ScheduledFlightPhase.Arrived;
                if(next==f.Phase)continue;
                f.Phase=next;
                AircraftRecord a=FindAircraft(f.AircraftId);
                if(a!=null)
                {
                    if(next==ScheduledFlightPhase.Departed)a.State=AircraftOperationalState.InFlight;
                    else if(next==ScheduledFlightPhase.Arrived)a.State=AircraftOperationalState.Turnaround;
                }
                Action<InternationalScheduledFlight> handler=OnFlightUpdated;if(handler!=null)handler(f);
                GameEventBus.Publish(new GameEvent(GameEventType.FlightPhaseChanged,clock.Tick,
                    "international-fleet",f.Id,next.ToString()));
            }
        }
    }
}
