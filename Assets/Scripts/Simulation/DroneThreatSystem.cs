using System;
using System.Collections.Generic;
using MyCity.Core;
using MyCity.Data;
using UnityEngine;

namespace MyCity.Simulation
{
    /// <summary>
    /// Abstract civilian UAV-hazard simulation. It models public alerts, shelter coverage,
    /// emergency-service response and economic disruption; it contains no real-world tactics.
    /// </summary>
    public sealed class DroneThreatSystem : MonoBehaviour
    {
        public enum RiskPreset { Calm = 0, Standard = 1, High = 2 }

        [Serializable]
        public sealed class DroneThreatRecord
        {
            public string Id;
            public int Day;
            public int Severity; // 1-3
            public int AffectedResidents;
            public int ShelteredResidents;
            public int PropertyLoss;
            public int ServiceDisruptionMinutes;
            public bool AlertIssued;
            public string Outcome;
        }

        private readonly List<CitizenRecord> citizens = new List<CitizenRecord>();
        private readonly List<BuildingRecord> buildings = new List<BuildingRecord>();
        private readonly List<DroneThreatRecord> history = new List<DroneThreatRecord>();
        private GameClock clock;
        private int citySeed = 173;
        private int lastProcessedDay;
        private RiskPreset risk = RiskPreset.Standard;
        private int activeAlertMinutes;

        public IReadOnlyList<DroneThreatRecord> History { get { return history; } }
        public bool IsAlertActive { get { return activeAlertMinutes > 0; } }
        public int ActiveAlertMinutes { get { return activeAlertMinutes; } }
        public int LastProcessedDay { get { return lastProcessedDay; } }
        public RiskPreset Risk { get { return risk; } }
        public string LatestMessage { get; private set; } = "Угроза БПЛА: ожидание событий симуляции.";
        public event Action<string> OnThreatMessage;

        private void OnEnable() { TrySubscribe(); }
        private void Update() { if (clock == null) TrySubscribe(); }
        private void OnDisable()
        {
            if (clock != null) { clock.OnDayChanged -= HandleDayChanged; clock.OnTick -= HandleTick; }
            clock = null;
        }

        private void TrySubscribe()
        {
            GameClock current = GameClock.Instance;
            if (current == null || current == clock) return;
            if (clock != null) { clock.OnDayChanged -= HandleDayChanged; clock.OnTick -= HandleTick; }
            clock = current;
            clock.OnDayChanged += HandleDayChanged;
            clock.OnTick += HandleTick;
        }

        public void Configure(IEnumerable<CitizenRecord> citizenRecords, IEnumerable<BuildingRecord> buildingRecords,
            int seed, RiskPreset preset, int processedDay, List<DroneThreatRecord> savedHistory, int alertMinutes)
        {
            citizens.Clear(); buildings.Clear(); history.Clear();
            if (citizenRecords != null) foreach (CitizenRecord item in citizenRecords) if (item != null) citizens.Add(item);
            if (buildingRecords != null) foreach (BuildingRecord item in buildingRecords) if (item != null) buildings.Add(item);
            if (savedHistory != null) history.AddRange(savedHistory);
            citySeed = seed == 0 ? 173 : seed;
            risk = preset;
            lastProcessedDay = Math.Max(0, processedDay);
            activeAlertMinutes = Math.Max(0, alertMinutes);
        }

        public void UpdateCityEntities(IEnumerable<CitizenRecord> citizenRecords, IEnumerable<BuildingRecord> buildingRecords)
        {
            citizens.Clear(); buildings.Clear();
            if (citizenRecords != null) foreach (CitizenRecord item in citizenRecords) if (item != null) citizens.Add(item);
            if (buildingRecords != null) foreach (BuildingRecord item in buildingRecords) if (item != null) buildings.Add(item);
        }

        public void Export(List<DroneThreatRecord> destination)
        {
            if (destination == null) return;
            destination.Clear();
            destination.AddRange(history);
        }

        public void SetRiskPreset(RiskPreset preset) { risk = preset; }

        private void HandleTick(long tick)
        {
            if (activeAlertMinutes <= 0) return;
            activeAlertMinutes--;
            if (activeAlertMinutes == 0) Publish("Оповещение об угрозе БПЛА завершено. Экстренные службы продолжают проверку города.");
        }

        private void HandleDayChanged(int day)
        {
            if (day <= lastProcessedDay) return;
            lastProcessedDay = day;
            // Stable per-city/per-day roll: reloading a save cannot reroll the same day.
            System.Random random = new System.Random(unchecked(citySeed * 397 ^ day * 7919));
            double chance = risk == RiskPreset.Calm ? 0.012 : (risk == RiskPreset.High ? 0.075 : 0.035);
            if (random.NextDouble() >= chance) return;

            int severity = random.Next(1, 101) <= (risk == RiskPreset.High ? 28 : 14) ? 3 :
                (random.Next(1, 101) <= 42 ? 2 : 1);
            int shelterCapacity = 0;
            foreach (BuildingRecord building in buildings)
                if (building != null) shelterCapacity += Math.Max(0, building.EmergencyShelterCapacity);

            int population = citizens.Count;
            int affected = population == 0 ? 0 : Math.Max(1, (int)Math.Ceiling(population * (0.25 + severity * 0.15)));
            int sheltered = Math.Min(affected, shelterCapacity);
            int uncovered = Math.Max(0, affected - sheltered);
            int loss = severity == 1 ? random.Next(0, 2500) : (severity == 2 ? random.Next(2500, 15000) : random.Next(15000, 60000));
            if (shelterCapacity > 0) loss = (int)Math.Round(loss * 0.8f);
            int disruption = severity * random.Next(30, 121);
            activeAlertMinutes = severity * 45;

            DroneThreatRecord record = new DroneThreatRecord
            {
                Id = "uav-alert-" + citySeed + "-" + day,
                Day = day,
                Severity = severity,
                AffectedResidents = affected,
                ShelteredResidents = sheltered,
                PropertyLoss = loss,
                ServiceDisruptionMinutes = disruption,
                AlertIssued = true,
                Outcome = uncovered == 0 ? "Жители размещены в укрытиях; службы проводят проверку." :
                    (sheltered > 0 ? "Часть жителей укрыта; остальным рекомендовано оставаться в безопасных помещениях." :
                    "Укрытия не обеспечивают защиту населения; требуется развитие гражданской инфраструктуры.")
            };
            history.Add(record);
            if (history.Count > 100) history.RemoveAt(0);
            Publish("ВНИМАНИЕ: угроза БПЛА. Уровень " + severity + ". Оповещение жителей, укрытие и работа экстренных служб. " +
                "Укрыто " + sheltered + " из " + affected + "; предварительный ущерб " + loss.ToString("N0") + " ₽. " + record.Outcome);
        }

        private void Publish(string message)
        {
            LatestMessage = message;
            Action<string> handler = OnThreatMessage;
            if (handler != null) handler(message);
        }
    }
}
