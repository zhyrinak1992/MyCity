using System;
using System.Collections.Generic;
using MyCity.Core;
using UnityEngine;

namespace MyCity.Simulation
{
    public enum FlightPhase
    {
        Scheduled, CheckIn, Boarding, TaxiOut, EnRoute, Landing, TaxiIn, Turnaround
    }

    [Serializable]
    public sealed class FlightOperation
    {
        public string Id;
        public string FlightNumber;
        public string Destination;
        public int DepartureMinute;
        public int FlightDurationMinutes;
        public int TurnaroundMinutes;
        public int Capacity;
        public int PlannedPassengers;
        public int Day;
        public FlightPhase Phase;
        public int MinutesToDeparture;
        public int MinutesToArrival;
        public bool IsOutbound;
    }

    /// <summary>
    /// First operational air-traffic slice. Flight phases are derived from the canonical
    /// city clock, so schedules remain deterministic and do not drift with frame rate.
    /// </summary>
    public sealed class AirTransportSystem : MonoBehaviour
    {
        private readonly List<FlightOperation> flights = new List<FlightOperation>();
        private GameClock clock;
        private int lastMinute = -1;
        public IList<FlightOperation> Flights { get { return flights.AsReadOnly(); } }
        public int ScheduledToday { get { return flights.Count; } }
        public int ActiveFlights { get { int n=0; for(int i=0;i<flights.Count;i++) if(flights[i].Phase==FlightPhase.EnRoute || flights[i].Phase==FlightPhase.Landing) n++; return n; } }
        public int PlannedPassengers { get { int n=0; for(int i=0;i<flights.Count;i++) n+=flights[i].PlannedPassengers; return n; } }
        public event Action<FlightOperation> OnFlightPhaseChanged;

        private void OnEnable() { TrySubscribe(); }
        private void Update() { if (clock == null) TrySubscribe(); }
        private void OnDisable() { if(clock!=null) clock.OnTick-=HandleTick; clock=null; }

        public void InitializeDefaults()
        {
            if(flights.Count>0) return;
            Add("MC101","Североград",360,85,45,120,94);
            Add("MC204","Приморск",510,110,50,160,132);
            Add("MC315","Столичный узел",750,65,40,100,78);
            Add("MC422","Заречье",990,95,45,140,116);
            Refresh(clock!=null?clock.Day:1,clock!=null?clock.MinuteOfDay:480,false);
        }

        private void Add(string number,string destination,int departure,int duration,int turnaround,int capacity,int passengers)
        {
            flights.Add(new FlightOperation { Id="flight-"+number, FlightNumber=number, Destination=destination,
                DepartureMinute=departure, FlightDurationMinutes=duration, TurnaroundMinutes=turnaround,
                Capacity=capacity, PlannedPassengers=Mathf.Clamp(passengers,0,capacity), Day=1, IsOutbound=true });
        }

        private void TrySubscribe()
        {
            GameClock current=GameClock.Instance;
            if(current==null || current==clock) return;
            if(clock!=null) clock.OnTick-=HandleTick;
            clock=current; clock.OnTick+=HandleTick;
            Refresh(clock.Day,clock.MinuteOfDay,true);
        }

        private void HandleTick(long tick)
        {
            if(clock==null || clock.MinuteOfDay==lastMinute) return;
            Refresh(clock.Day,clock.MinuteOfDay,true);
        }

        private void Refresh(int day,int minute,bool notify)
        {
            lastMinute=minute;
            for(int i=0;i<flights.Count;i++)
            {
                FlightOperation f=flights[i];
                f.Day=day;
                int elapsed=minute-f.DepartureMinute;
                FlightPhase next;
                if(elapsed < -90) next=FlightPhase.Scheduled;
                else if(elapsed < -45) next=FlightPhase.CheckIn;
                else if(elapsed < -15) next=FlightPhase.Boarding;
                else if(elapsed < 0) next=FlightPhase.TaxiOut;
                else if(elapsed < f.FlightDurationMinutes-10) next=FlightPhase.EnRoute;
                else if(elapsed < f.FlightDurationMinutes) next=FlightPhase.Landing;
                else if(elapsed < f.FlightDurationMinutes+10) next=FlightPhase.TaxiIn;
                else if(elapsed < f.FlightDurationMinutes+f.TurnaroundMinutes) next=FlightPhase.Turnaround;
                else next=FlightPhase.Scheduled;
                bool changed=f.Phase!=next;
                f.Phase=next;
                f.MinutesToDeparture=Mathf.Max(0,-elapsed);
                f.MinutesToArrival=next==FlightPhase.EnRoute || next==FlightPhase.Landing ? Mathf.Max(0,f.FlightDurationMinutes-elapsed) : 0;
                if(notify && changed)
                {
                    Action<FlightOperation> handler=OnFlightPhaseChanged;
                    if(handler!=null) handler(f);
                    GameEventBus.Publish(new GameEvent(GameEventType.FlightPhaseChanged,
                        clock!=null?clock.Tick:0,"air-traffic",f.Id,f.FlightNumber+":"+next.ToString()));
                }
            }
        }

        public string GetPhaseLabel(FlightPhase phase)
        {
            switch(phase)
            {
                case FlightPhase.CheckIn:return "Регистрация";
                case FlightPhase.Boarding:return "Посадка";
                case FlightPhase.TaxiOut:return "Руление на вылет";
                case FlightPhase.EnRoute:return "В полёте";
                case FlightPhase.Landing:return "Заход на посадку";
                case FlightPhase.TaxiIn:return "Руление к терминалу";
                case FlightPhase.Turnaround:return "Подготовка борта";
                default:return "По расписанию";
            }
        }
    }
}
