using System;
using System.Collections.Generic;
using MyCity.Core;
using UnityEngine;

namespace MyCity.Simulation
{
    public enum AirportBaggageState { Accepted, Sorting, RoutedToFlight, Loaded, InTransit, Unloaded, Carousel, Claimed, Delayed, Misrouted, Lost, Recovered }

    [Serializable]
    public sealed class AirportBaggageRecord
    {
        public string Id;
        public string PassengerId;
        public string PassengerName;
        public string FlightId;
        public string FlightNumber;
        public string Destination;
        public int TagNumber;
        public int CreatedDay;
        public int CreatedMinute;
        public int LastUpdateMinute;
        public AirportBaggageState State;
        public int DelayMinutes;
        public bool WasMisrouted;
        public bool WasRecovered;
    }

    /// <summary>Tracks each checked bag independently and reconciles it with its passenger and flight.</summary>
    public sealed class AirportBaggageIntelligenceSystem : MonoBehaviour
    {
        private const string SaveKey = "MyCity.AirportBaggageIntelligence.v1";
        private const int MaxRecords = 5000;
        private readonly List<AirportBaggageRecord> records = new List<AirportBaggageRecord>();
        private readonly HashSet<string> knownPassengerIds = new HashSet<string>();
        private AirportIndividualPassengerSystem passengerSystem;
        private GameClock clock;
        private int saveCountdown;

        [Serializable] private sealed class SaveContainer { public List<AirportBaggageRecord> Items = new List<AirportBaggageRecord>(); }

        public IList<AirportBaggageRecord> Records { get { return records.AsReadOnly(); } }
        public int TotalTracked { get { return records.Count; } }
        public int AcceptedCount { get; private set; }
        public int SortingCount { get; private set; }
        public int RoutedCount { get; private set; }
        public int LoadedCount { get; private set; }
        public int InTransitCount { get; private set; }
        public int UnloadedCount { get; private set; }
        public int CarouselCount { get; private set; }
        public int ClaimedCount { get; private set; }
        public int DelayedCount { get; private set; }
        public int MisroutedCount { get; private set; }
        public int LostCount { get; private set; }
        public int RecoveredCount { get; private set; }
        public event Action<string> OnBaggageMessage;

        public void Initialize(AirportIndividualPassengerSystem passengers)
        {
            if (clock != null) clock.OnTick -= HandleTick;
            passengerSystem = passengers;
            clock = GameClock.Instance;
            LoadState();
            if (clock != null) clock.OnTick += HandleTick;
            ReconcilePassengers();
            RefreshCounts();
        }

        private void HandleTick(long tick)
        {
            if (passengerSystem == null) return;
            ReconcilePassengers();
            int now = clock != null ? clock.MinuteOfDay : 0;
            for (int i = 0; i < records.Count; i++)
            {
                AirportBaggageRecord bag = records[i];
                IndividualAirportPassenger pax = FindPassenger(bag.PassengerId);
                if (pax == null) continue;
                switch (bag.State)
                {
                    case AirportBaggageState.Accepted:
                        bag.State = AirportBaggageState.Sorting; bag.LastUpdateMinute = now; break;
                    case AirportBaggageState.Sorting:
                        if (Elapsed(bag, now) >= 2) { bag.State = AirportBaggageState.RoutedToFlight; bag.LastUpdateMinute = now; }
                        break;
                    case AirportBaggageState.RoutedToFlight:
                        if (pax.State == IndividualPassengerState.Boarding || pax.State == IndividualPassengerState.Boarded)
                        {
                            if (!bag.WasMisrouted && bag.TagNumber % 41 == 0) { bag.State = AirportBaggageState.Misrouted; bag.WasMisrouted = true; bag.LastUpdateMinute = now; }
                            else { bag.State = AirportBaggageState.Loaded; bag.LastUpdateMinute = now; }
                        }
                        else if (pax.State == IndividualPassengerState.Missed) { bag.State = AirportBaggageState.Misrouted; bag.WasMisrouted = true; bag.LastUpdateMinute = now; }
                        break;
                    case AirportBaggageState.Loaded:
                        if (pax.State == IndividualPassengerState.Arrived) { bag.State = AirportBaggageState.InTransit; bag.LastUpdateMinute = now; }
                        break;
                    case AirportBaggageState.InTransit:
                        if (Elapsed(bag, now) >= 3) { bag.State = AirportBaggageState.Unloaded; bag.LastUpdateMinute = now; }
                        break;
                    case AirportBaggageState.Unloaded:
                        if (Elapsed(bag, now) >= 1)
                        {
                            if (bag.TagNumber % 97 == 0) { bag.State = AirportBaggageState.Lost; bag.LastUpdateMinute = now; Notify("Багаж " + bag.TagNumber + " не поступил на ленту. Начат поиск."); }
                            else if (bag.TagNumber % 17 == 0) { bag.State = AirportBaggageState.Delayed; bag.DelayMinutes = 5 + bag.TagNumber % 8; bag.LastUpdateMinute = now; }
                            else { bag.State = AirportBaggageState.Carousel; bag.LastUpdateMinute = now; }
                        }
                        break;
                    case AirportBaggageState.Carousel:
                        if (pax.State == IndividualPassengerState.Exited) { bag.State = AirportBaggageState.Claimed; bag.LastUpdateMinute = now; }
                        break;
                    case AirportBaggageState.Misrouted:
                        if (Elapsed(bag, now) >= 30) { bag.State = AirportBaggageState.Recovered; bag.WasRecovered = true; bag.LastUpdateMinute = now; Notify("Багаж " + bag.TagNumber + " найден и направлен на правильный рейс."); }
                        break;
                    case AirportBaggageState.Delayed:
                        if (Elapsed(bag, now) >= Mathf.Max(5, bag.DelayMinutes)) { bag.State = AirportBaggageState.Carousel; bag.LastUpdateMinute = now; Notify("Задержанный багаж " + bag.TagNumber + " поступил на ленту."); }
                        break;
                    case AirportBaggageState.Lost:
                        if (Elapsed(bag, now) >= 60) { bag.State = AirportBaggageState.Recovered; bag.WasRecovered = true; bag.LastUpdateMinute = now; Notify("Багаж " + bag.TagNumber + " найден службой розыска."); }
                        break;
                }
            }
            RefreshCounts();
            if (++saveCountdown >= 5) { saveCountdown = 0; SaveState(); }
        }

        private void ReconcilePassengers()
        {
            if (passengerSystem == null) return;
            IList<IndividualAirportPassenger> paxList = passengerSystem.Passengers;
            for (int i = 0; i < paxList.Count && records.Count < MaxRecords; i++)
            {
                IndividualAirportPassenger pax = paxList[i];
                if (pax == null || !pax.HasBaggage || string.IsNullOrEmpty(pax.Id) || !knownPassengerIds.Add(pax.Id)) continue;
                int tag = StableTag(pax.Id);
                records.Add(new AirportBaggageRecord { Id = "BAG-" + tag.ToString("D7"), PassengerId = pax.Id, PassengerName = pax.Name,
                    FlightId = pax.FlightId, FlightNumber = pax.FlightNumber, Destination = pax.Destination, TagNumber = tag,
                    CreatedDay = pax.CreatedDay, CreatedMinute = pax.CreatedMinute, LastUpdateMinute = clock != null ? clock.MinuteOfDay : 0,
                    State = AirportBaggageState.Accepted });
            }
        }

        private int StableTag(string value)
        {
            unchecked { int hash = 17; for (int i = 0; i < value.Length; i++) hash = hash * 31 + value[i]; return Mathf.Abs(hash == int.MinValue ? 0 : hash) % 9000000 + 1000000; }
        }
        private int Elapsed(AirportBaggageRecord bag, int now) { return now >= bag.LastUpdateMinute ? now - bag.LastUpdateMinute : (1440 - bag.LastUpdateMinute) + now; }
        private IndividualAirportPassenger FindPassenger(string id)
        {
            if (passengerSystem == null) return null;
            IList<IndividualAirportPassenger> list = passengerSystem.Passengers;
            for (int i = 0; i < list.Count; i++) if (list[i] != null && list[i].Id == id) return list[i];
            return null;
        }
        private void RefreshCounts()
        {
            AcceptedCount = SortingCount = RoutedCount = LoadedCount = InTransitCount = UnloadedCount = CarouselCount = ClaimedCount = DelayedCount = MisroutedCount = LostCount = RecoveredCount = 0;
            for (int i = 0; i < records.Count; i++)
            {
                switch (records[i].State)
                {
                    case AirportBaggageState.Accepted: AcceptedCount++; break;
                    case AirportBaggageState.Sorting: SortingCount++; break;
                    case AirportBaggageState.RoutedToFlight: RoutedCount++; break;
                    case AirportBaggageState.Loaded: LoadedCount++; break;
                    case AirportBaggageState.InTransit: InTransitCount++; break;
                    case AirportBaggageState.Unloaded: UnloadedCount++; break;
                    case AirportBaggageState.Carousel: CarouselCount++; break;
                    case AirportBaggageState.Claimed: ClaimedCount++; break;
                    case AirportBaggageState.Delayed: DelayedCount++; break;
                    case AirportBaggageState.Misrouted: MisroutedCount++; break;
                    case AirportBaggageState.Lost: LostCount++; break;
                    case AirportBaggageState.Recovered: RecoveredCount++; break;
                }
            }
        }
        public string GetStateLabel(AirportBaggageState state)
        {
            switch (state)
            {
                case AirportBaggageState.Accepted: return "Принят на регистрации";
                case AirportBaggageState.Sorting: return "Сортировка";
                case AirportBaggageState.RoutedToFlight: return "Направлен на рейс";
                case AirportBaggageState.Loaded: return "Загружен в самолёт";
                case AirportBaggageState.InTransit: return "В пути";
                case AirportBaggageState.Unloaded: return "Выгружен";
                case AirportBaggageState.Carousel: return "На ленте выдачи";
                case AirportBaggageState.Claimed: return "Получен пассажиром";
                case AirportBaggageState.Delayed: return "Задержан";
                case AirportBaggageState.Misrouted: return "Ошибочная сортировка";
                case AirportBaggageState.Lost: return "Утерян";
                case AirportBaggageState.Recovered: return "Найден / перенаправлен";
                default: return "—";
            }
        }
        private void Notify(string message) { Action<string> handler = OnBaggageMessage; if (handler != null) handler(message); }
        private void SaveState()
        {
            SaveContainer data = new SaveContainer { Items = new List<AirportBaggageRecord>(records) };
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data)); PlayerPrefs.Save();
        }
        private void LoadState()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            try
            {
                SaveContainer data = JsonUtility.FromJson<SaveContainer>(PlayerPrefs.GetString(SaveKey));
                if (data == null || data.Items == null) return;
                for (int i = 0; i < data.Items.Count && records.Count < MaxRecords; i++)
                {
                    AirportBaggageRecord item = data.Items[i];
                    if (item == null || string.IsNullOrEmpty(item.PassengerId) || !knownPassengerIds.Add(item.PassengerId)) continue;
                    records.Add(item);
                }
            }
            catch (Exception ex) { Debug.LogWarning("Airport baggage state could not be loaded: " + ex.Message); }
        }
        private void OnApplicationPause(bool paused) { if (paused) SaveState(); }
        private void OnApplicationQuit() { SaveState(); }
        private void OnDestroy() { SaveState(); if (clock != null) clock.OnTick -= HandleTick; }
    }
}
