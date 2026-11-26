using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyCity.Simulation
{
    [Serializable]
    public sealed class AirlineLedgerEntry
    {
        public string Id;
        public int Day;
        public string FlightNumber;
        public string Destination;
        public string Category;
        public long Amount;
        public bool IsIncome;
    }

    [Serializable]
    public sealed class AirlineRoutePerformance
    {
        public string DestinationId;
        public string Destination;
        public int Flights;
        public int Passengers;
        public long Revenue;
        public long Costs;
        public long Result { get { return Revenue - Costs; } }
        public float LoadFactor { get { return Flights <= 0 ? 0f : (float)Passengers / Math.Max(1, Capacity); } }
        public int Capacity;
    }

    /// <summary>Separate airline cash ledger. Settles each flight once at actual departure.</summary>
    public sealed class AirlineEconomySystem : MonoBehaviour
    {
        private InternationalAirlineFleetSystem fleet;
        private InternationalAirportNetworkSystem network;
        private readonly List<AirlineLedgerEntry> ledger = new List<AirlineLedgerEntry>();
        private readonly HashSet<string> settledFlights = new HashSet<string>();
        private readonly Dictionary<string, int> fareOverrides = new Dictionary<string, int>();
        public IList<AirlineLedgerEntry> Ledger { get { return ledger.AsReadOnly(); } }
        public long TotalRevenue { get; private set; }
        public long TotalCosts { get; private set; }
        public long NetResult { get { return TotalRevenue - TotalCosts; } }
        public int SettledFlightCount { get { return settledFlights.Count; } }
        public event Action<string> OnSettlementMessage;

        public void Initialize(InternationalAirlineFleetSystem airlineFleet, InternationalAirportNetworkSystem routeNetwork)
        {
            if (fleet != null) fleet.OnFlightUpdated -= HandleFlightUpdated;
            fleet = airlineFleet; network = routeNetwork;
            if (fleet != null) fleet.OnFlightUpdated += HandleFlightUpdated;
            // Reconcile flights already departed before this component subscribed.
            if (fleet != null) foreach (InternationalScheduledFlight f in fleet.Flights)
                if (f != null && f.Phase == ScheduledFlightPhase.Departed) SettleFlight(f);
        }

        public int GetRecommendedFare(InternationalDestination destination)
        {
            if (destination == null) return 0;
            int band = destination.DistanceKm < 3000 ? 6500 : destination.DistanceKm < 6000 ? 11500 : 18500;
            float demandFactor = Mathf.Clamp(0.9f + destination.BaseDemandPerDay / 300f, 0.9f, 1.65f);
            return Mathf.Max(1000, Mathf.RoundToInt(band * demandFactor / 100f) * 100);
        }
        public int GetFare(string destinationId)
        {
            int value; if (fareOverrides.TryGetValue(destinationId, out value)) return value;
            return GetRecommendedFare(FindDestination(destinationId));
        }
        public bool TrySetFare(string destinationId, int fare)
        {
            if (FindDestination(destinationId) == null || fare < 1000 || fare > 1000000) return false;
            fareOverrides[destinationId] = fare; return true;
        }
        public void ApplyFareToFlight(InternationalScheduledFlight flight)
        {
            if (flight == null || flight.Phase != ScheduledFlightPhase.Scheduled) return;
            InternationalDestination d = FindDestination(flight.DestinationId); if (d == null) return;
            int fare = GetFare(d.Id);
            // Demand is elastic: expensive tickets reduce expected sales, discount fares increase them.
            int recommended = Mathf.Max(1, GetRecommendedFare(d));
            float priceRatio = (float)fare / recommended;
            float elasticity = Mathf.Clamp(1.25f - (priceRatio - 1f) * 0.62f, 0.35f, 1.45f);
            int demand = Mathf.RoundToInt(d.BaseDemandPerDay * elasticity);
            flight.TicketFare = fare;
            flight.TicketsSold = Mathf.Clamp(Mathf.Min(demand, flight.Capacity), 0, flight.Capacity);
            flight.PlannedPassengers = flight.TicketsSold;
        }
        public AirlineRoutePerformance GetRoutePerformance(string destinationId)
        {
            AirlineRoutePerformance result = new AirlineRoutePerformance { DestinationId = destinationId };
            InternationalDestination d = FindDestination(destinationId); result.Destination = d != null ? d.City : destinationId;
            if (fleet == null) return result;
            foreach (InternationalScheduledFlight f in fleet.Flights)
            {
                if (f == null || f.DestinationId != destinationId || !f.FinanciallySettled) continue;
                result.Flights++; result.Passengers += f.TicketsSold; result.Revenue += f.TicketRevenue;
                result.Costs += f.OperatingCost; result.Capacity += f.Capacity;
            }
            return result;
        }
        private InternationalDestination FindDestination(string id)
        {
            if (network == null) return null;
            foreach (InternationalDestination d in network.Destinations) if (d.Id == id) return d;
            return null;
        }
        private void HandleFlightUpdated(InternationalScheduledFlight flight)
        {
            if (flight == null) return;
            if (flight.Phase == ScheduledFlightPhase.Scheduled && flight.TicketFare <= 0) ApplyFareToFlight(flight);
            if (flight.Phase == ScheduledFlightPhase.Departed) SettleFlight(flight);
        }
        private void SettleFlight(InternationalScheduledFlight flight)
        {
            if (flight == null || flight.Phase != ScheduledFlightPhase.Departed || settledFlights.Contains(flight.Id)) return;
            ApplyFareToFlight(flight);
            AircraftRecord aircraft = fleet != null ? fleet.FindAircraft(flight.AircraftId) : null;
            long revenue = (long)flight.TicketsSold * Math.Max(0, flight.TicketFare);
            long cost = aircraft != null ? Math.Max(0, aircraft.OperatingCostPerFlight) : 0;
            flight.TicketRevenue = revenue; flight.OperatingCost = cost; flight.OperatingResult = revenue - cost;
            flight.FinanciallySettled = true;
            settledFlights.Add(flight.Id);
            TotalRevenue = checked(TotalRevenue + revenue); TotalCosts = checked(TotalCosts + cost);
            int day = flight.Day;
            ledger.Add(new AirlineLedgerEntry { Id = flight.Id + "-ticket", Day = day, FlightNumber = flight.FlightNumber,
                Destination = flight.DestinationCity, Category = "Продажа билетов", Amount = revenue, IsIncome = true });
            ledger.Add(new AirlineLedgerEntry { Id = flight.Id + "-cost", Day = day, FlightNumber = flight.FlightNumber,
                Destination = flight.DestinationCity, Category = "Эксплуатация рейса", Amount = cost, IsIncome = false });
            Action<string> h = OnSettlementMessage;
            if (h != null) h(flight.FlightNumber + " · " + flight.DestinationCity + ": выручка " + revenue.ToString("N0") + " ₽, расходы " + cost.ToString("N0") + " ₽, результат " + flight.OperatingResult.ToString("N0") + " ₽.");
        }
        private void OnDestroy() { if (fleet != null) fleet.OnFlightUpdated -= HandleFlightUpdated; }
    }
}
