using System;
using System.Collections.Generic;
using UnityEngine;

namespace MyCity.Simulation
{
    public enum PassengerServiceStage { Scheduled, CheckInOpen, CheckedIn, BaggageAccepted, SecurityCleared, Boarding, Departed, Arrived, Cancelled }

    [Serializable]
    public sealed class PassengerServiceRecord
    {
        public string FlightId;
        public string FlightNumber;
        public string Destination;
        public int Day;
        public int BookedPassengers;
        public int CheckedInPassengers;
        public int BaggagePieces;
        public int SecurityClearedPassengers;
        public int BoardedPassengers;
        public int WaitingPassengers;
        public PassengerServiceStage Stage;
        public bool ServiceCompleted;
        public string LastUpdate;
    }

    /// <summary>Aggregated airport passenger handling tied to scheduled flight phases.</summary>
    public sealed class AirportPassengerServiceSystem : MonoBehaviour
    {
        private InternationalAirlineFleetSystem fleet;
        private readonly List<PassengerServiceRecord> records = new List<PassengerServiceRecord>();
        private readonly Dictionary<string, PassengerServiceRecord> byFlight = new Dictionary<string, PassengerServiceRecord>();
        public IList<PassengerServiceRecord> Records { get { return records.AsReadOnly(); } }
        public int TotalCheckedIn { get; private set; }
        public int TotalBaggage { get; private set; }
        public int TotalBoarded { get; private set; }
        public int ActiveFlights { get; private set; }
        public event Action<string> OnServiceMessage;

        public void Initialize(InternationalAirlineFleetSystem airlineFleet)
        {
            if (fleet != null) fleet.OnFlightUpdated -= HandleFlightUpdated;
            fleet = airlineFleet;
            if (fleet == null) return;
            fleet.OnFlightUpdated += HandleFlightUpdated;
            foreach (InternationalScheduledFlight flight in fleet.Flights) EnsureRecord(flight);
            RecalculateTotals();
        }

        public PassengerServiceRecord Find(string flightId)
        {
            PassengerServiceRecord record;
            return byFlight.TryGetValue(flightId, out record) ? record : null;
        }

        private void HandleFlightUpdated(InternationalScheduledFlight flight)
        {
            if (flight == null) return;
            PassengerServiceRecord record = EnsureRecord(flight);
            if (record == null) return;
            int passengers = Mathf.Clamp(flight.TicketsSold > 0 ? flight.TicketsSold : flight.PlannedPassengers, 0, flight.Capacity);
            record.BookedPassengers = passengers;
            switch (flight.Phase)
            {
                case ScheduledFlightPhase.Scheduled:
                    record.Stage = PassengerServiceStage.Scheduled;
                    record.WaitingPassengers = passengers;
                    record.LastUpdate = "Рейс ожидает открытия регистрации";
                    break;
                case ScheduledFlightPhase.CheckIn:
                    record.Stage = PassengerServiceStage.CheckInOpen;
                    record.CheckedInPassengers = passengers;
                    record.BaggagePieces = Mathf.RoundToInt(passengers * 0.78f);
                    record.WaitingPassengers = passengers;
                    record.LastUpdate = "Регистрация завершена; багаж принят";
                    break;
                case ScheduledFlightPhase.Boarding:
                    record.Stage = PassengerServiceStage.Boarding;
                    record.CheckedInPassengers = passengers;
                    record.BaggagePieces = Mathf.RoundToInt(passengers * 0.78f);
                    record.SecurityClearedPassengers = Mathf.RoundToInt(passengers * 0.97f);
                    record.WaitingPassengers = Mathf.Max(0, passengers - record.BoardedPassengers);
                    record.LastUpdate = "Пассажиры прошли контроль и направляются к выходу";
                    break;
                case ScheduledFlightPhase.Departed:
                    record.Stage = PassengerServiceStage.Departed;
                    record.CheckedInPassengers = passengers;
                    record.BaggagePieces = Mathf.RoundToInt(passengers * 0.78f);
                    record.SecurityClearedPassengers = Mathf.RoundToInt(passengers * 0.97f);
                    record.BoardedPassengers = record.SecurityClearedPassengers;
                    record.WaitingPassengers = 0;
                    record.ServiceCompleted = true;
                    record.LastUpdate = "Посадка завершена; рейс отправлен";
                    break;
                case ScheduledFlightPhase.Arrived:
                    record.Stage = PassengerServiceStage.Arrived;
                    record.WaitingPassengers = 0;
                    record.ServiceCompleted = true;
                    record.LastUpdate = "Рейс прибыл; обслуживание вылета завершено";
                    break;
                default:
                    record.Stage = PassengerServiceStage.Scheduled;
                    break;
            }
            RecalculateTotals();
            Action<string> handler = OnServiceMessage;
            if (handler != null && (flight.Phase == ScheduledFlightPhase.CheckIn || flight.Phase == ScheduledFlightPhase.Boarding || flight.Phase == ScheduledFlightPhase.Departed))
                handler(flight.FlightNumber + " · " + flight.DestinationCity + ": " + record.LastUpdate + ".");
        }

        private PassengerServiceRecord EnsureRecord(InternationalScheduledFlight flight)
        {
            if (flight == null || string.IsNullOrEmpty(flight.Id)) return null;
            PassengerServiceRecord record;
            if (byFlight.TryGetValue(flight.Id, out record)) return record;
            int passengers = Mathf.Clamp(flight.TicketsSold > 0 ? flight.TicketsSold : flight.PlannedPassengers, 0, flight.Capacity);
            record = new PassengerServiceRecord { FlightId = flight.Id, FlightNumber = flight.FlightNumber,
                Destination = flight.DestinationCity, Day = flight.Day, BookedPassengers = passengers,
                WaitingPassengers = passengers, Stage = PassengerServiceStage.Scheduled, LastUpdate = "Ожидает регистрации" };
            records.Add(record); byFlight.Add(flight.Id, record);
            return record;
        }

        private void RecalculateTotals()
        {
            TotalCheckedIn = 0; TotalBaggage = 0; TotalBoarded = 0; ActiveFlights = 0;
            for (int i = 0; i < records.Count; i++)
            {
                PassengerServiceRecord r = records[i];
                TotalCheckedIn += r.CheckedInPassengers; TotalBaggage += r.BaggagePieces; TotalBoarded += r.BoardedPassengers;
                if (!r.ServiceCompleted && r.Stage != PassengerServiceStage.Arrived && r.Stage != PassengerServiceStage.Cancelled) ActiveFlights++;
            }
        }

        public string GetStageLabel(PassengerServiceStage stage)
        {
            switch (stage)
            {
                case PassengerServiceStage.CheckInOpen: return "Регистрация";
                case PassengerServiceStage.CheckedIn: return "Зарегистрирован";
                case PassengerServiceStage.BaggageAccepted: return "Багаж принят";
                case PassengerServiceStage.SecurityCleared: return "Контроль пройден";
                case PassengerServiceStage.Boarding: return "Посадка";
                case PassengerServiceStage.Departed: return "Отправлен";
                case PassengerServiceStage.Arrived: return "Прибыл";
                case PassengerServiceStage.Cancelled: return "Отменён";
                default: return "Запланирован";
            }
        }
        private void OnDestroy() { if (fleet != null) fleet.OnFlightUpdated -= HandleFlightUpdated; }
    }
}
