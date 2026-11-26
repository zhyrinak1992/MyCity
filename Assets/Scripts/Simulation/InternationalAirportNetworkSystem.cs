using System;
using System.Collections.Generic;
using MyCity.Core;
using UnityEngine;

namespace MyCity.Simulation
{
    public enum InternationalRegion { Europe, MiddleEast, Asia, NorthAmerica, SouthAmerica, Africa, Oceania }

    [Serializable]
    public sealed class InternationalDestination
    {
        public string Id;
        public string City;
        public string Country;
        public InternationalRegion Region;
        public int DistanceKm;
        public int BaseDemandPerDay;
        public bool IsRouteOpen;
        public int FlightMinutes;
        public string RecommendedAircraft;
        public int MinimumRangeKm;
    }

    /// <summary>
    /// Data-driven international destination catalogue and route-opening prototype.
    /// Distances and flight durations are game-balance estimates, not live aviation data.
    /// Routes are network records only; airline fleets, slots, ticket sales and physical aircraft
    /// movement are not yet simulated by this slice.
    /// </summary>
    public sealed class InternationalAirportNetworkSystem : MonoBehaviour
    {
        private readonly List<InternationalDestination> destinations = new List<InternationalDestination>();
        private GameClock clock;
        private int lastDay = -1;
        public IList<InternationalDestination> Destinations { get { return destinations.AsReadOnly(); } }
        public int OpenRouteCount { get { int n=0; for(int i=0;i<destinations.Count;i++) if(destinations[i].IsRouteOpen)n++; return n; } }
        public int TotalDailyDemand { get { int n=0; for(int i=0;i<destinations.Count;i++) if(destinations[i].IsRouteOpen)n+=destinations[i].BaseDemandPerDay; return n; } }
        public event Action<InternationalDestination> OnRouteChanged;

        private void OnEnable() { TrySubscribe(); }
        private void Update() { if(clock==null) TrySubscribe(); }
        private void OnDisable() { if(clock!=null) clock.OnDayChanged-=HandleDayChanged; clock=null; }

        public void InitializeDefaults()
        {
            if(destinations.Count>0) return;
            // Distances are deliberately rounded game estimates from the fictional home city.
            Add("paris","Париж","Франция",InternationalRegion.Europe,2480,180,210,"Среднемагистральный",3000);
            Add("london","Лондон","Великобритания",InternationalRegion.Europe,2500,190,215,"Среднемагистральный",3000);
            Add("rome","Рим","Италия",InternationalRegion.Europe,2370,120,205,"Среднемагистральный",3000);
            Add("berlin","Берлин","Германия",InternationalRegion.Europe,1610,145,165,"Среднемагистральный",2500);
            Add("amsterdam","Амстердам","Нидерланды",InternationalRegion.Europe,2150,150,190,"Среднемагистральный",3000);
            Add("madrid","Мадрид","Испания",InternationalRegion.Europe,3440,110,270,"Среднемагистральный",4000);
            Add("istanbul","Стамбул","Турция",InternationalRegion.MiddleEast,1760,170,175,"Среднемагистральный",2500);
            Add("dubai","Дубай","ОАЭ",InternationalRegion.MiddleEast,3680,220,285,"Дальнемагистральный",4500);
            Add("doha","Доха","Катар",InternationalRegion.MiddleEast,3540,105,275,"Дальнемагистральный",4500);
            Add("tokyo","Токио","Япония",InternationalRegion.Asia,7480,160,545,"Дальнемагистральный",8000);
            Add("beijing","Пекин","Китай",InternationalRegion.Asia,5800,155,440,"Дальнемагистральный",6500);
            Add("seoul","Сеул","Южная Корея",InternationalRegion.Asia,6600,130,480,"Дальнемагистральный",7000);
            Add("bangkok","Бангкок","Таиланд",InternationalRegion.Asia,7060,145,520,"Дальнемагистральный",8000);
            Add("singapore","Сингапур","Сингапур",InternationalRegion.Asia,8400,135,610,"Дальнемагистральный",9000);
            Add("new-york","Нью-Йорк","США",InternationalRegion.NorthAmerica,7510,175,550,"Дальнемагистральный",8500);
            Add("toronto","Торонто","Канада",InternationalRegion.NorthAmerica,7210,95,530,"Дальнемагистральный",8000);
            Add("miami","Майами","США",InternationalRegion.NorthAmerica,9230,100,665,"Дальнемагистральный",10000);
            Add("sao-paulo","Сан-Паулу","Бразилия",InternationalRegion.SouthAmerica,11800,70,820,"Дальнемагистральный",12000);
            Add("cairo","Каир","Египет",InternationalRegion.Africa,2900,105,235,"Среднемагистральный",3500);
            Add("cape-town","Кейптаун","ЮАР",InternationalRegion.Africa,10100,55,710,"Дальнемагистральный",11000);
            Add("sydney","Сидней","Австралия",InternationalRegion.Oceania,14500,65,990,"Дальнемагистральный",15000);
            Add("auckland","Окленд","Новая Зеландия",InternationalRegion.Oceania,16400,40,1110,"Дальнемагистральный",17000);
            TrySubscribe();
        }

        private void Add(string id,string city,string country,InternationalRegion region,int km,int demand,int minutes,string aircraft,int range)
        {
            destinations.Add(new InternationalDestination { Id=id, City=city, Country=country, Region=region,
                DistanceKm=km, BaseDemandPerDay=demand, FlightMinutes=minutes, RecommendedAircraft=aircraft,
                MinimumRangeKm=range, IsRouteOpen=false });
        }

        public bool SetRouteOpen(string destinationId,bool isOpen)
        {
            InternationalDestination d=destinations.Find(x=>x.Id==destinationId);
            if(d==null || d.IsRouteOpen==isOpen) return false;
            d.IsRouteOpen=isOpen;
            Action<InternationalDestination> handler=OnRouteChanged;
            if(handler!=null) handler(d);
            GameEventBus.Publish(new GameEvent(GameEventType.InternationalRouteChanged,
                clock!=null?clock.Tick:0,"international-airport",d.Id,(isOpen?"OPEN:":"CLOSED:")+d.City));
            return true;
        }

        public string GetRegionLabel(InternationalRegion region)
        {
            switch(region)
            {
                case InternationalRegion.Europe:return "Европа";
                case InternationalRegion.MiddleEast:return "Ближний Восток";
                case InternationalRegion.Asia:return "Азия";
                case InternationalRegion.NorthAmerica:return "Северная Америка";
                case InternationalRegion.SouthAmerica:return "Южная Америка";
                case InternationalRegion.Africa:return "Африка";
                default:return "Океания";
            }
        }

        private void TrySubscribe()
        {
            GameClock current=GameClock.Instance;
            if(current==null || current==clock) return;
            if(clock!=null) clock.OnDayChanged-=HandleDayChanged;
            clock=current; clock.OnDayChanged+=HandleDayChanged;
            lastDay=clock.Day;
        }
        private void HandleDayChanged(int day)
        {
            if(day==lastDay)return;
            lastDay=day;
            // Demand has a deterministic light seasonal/day-cycle variation for future expansion.
            GameEventBus.Publish(new GameEvent(GameEventType.InternationalDemandUpdated,
                clock!=null?clock.Tick:0,"international-airport",null,TotalDailyDemand.ToString()));
        }
    }
}
