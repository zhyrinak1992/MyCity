using System;
using System.Collections.Generic;
using MyCity.Data;
using UnityEngine;

namespace MyCity.Simulation
{
    /// <summary>Persistent ageing, mortality, morgue queues and cemetery capacity management.</summary>
    public sealed class CitizenLifeCycleSystem : MonoBehaviour
    {
        private readonly List<FuneralEventRecord> events = new List<FuneralEventRecord>();
        public IList<FuneralEventRecord> Events { get { return events.AsReadOnly(); } }
        public int DeceasedCount { get; private set; }
        public int WaitingForMorgueCount { get; private set; }
        public int InMorgueCount { get; private set; }
        public int WaitingForBurialCount { get; private set; }
        public int BuriedCount { get; private set; }

        public void Load(IEnumerable<FuneralEventRecord> saved)
        {
            events.Clear();
            HashSet<string> seenCitizens = new HashSet<string>();
            if (saved != null)
            {
                foreach (FuneralEventRecord item in saved)
                {
                    if (item == null || string.IsNullOrEmpty(item.CitizenId) || !seenCitizens.Add(item.CitizenId)) continue;
                    events.Add(Clone(item));
                }
            }
            // Statistics are rebuilt from citizen records after the save payload is restored.
            DeceasedCount = WaitingForMorgueCount = InMorgueCount = WaitingForBurialCount = BuriedCount = 0;
        }

        public void RebuildStatistics(IList<CitizenRecord> citizens)
        {
            if (citizens == null)
            {
                DeceasedCount = WaitingForMorgueCount = InMorgueCount = WaitingForBurialCount = BuriedCount = 0;
                return;
            }
            RefreshStatistics(citizens);
        }

        public void Export(List<FuneralEventRecord> destination)
        {
            if (destination == null) return;
            destination.Clear();
            foreach (FuneralEventRecord item in events) destination.Add(Clone(item));
        }

        public void ProcessDay(IList<CitizenRecord> citizens, IList<BuildingRecord> buildings, int day)
        {
            day = Math.Max(1, day);
            if (citizens == null) return;
            Dictionary<string, BuildingRecord> morgues = new Dictionary<string, BuildingRecord>();
            Dictionary<string, BuildingRecord> cemeteries = new Dictionary<string, BuildingRecord>();
            if (buildings != null)
            {
                foreach (BuildingRecord b in buildings)
                {
                    if (b == null || string.IsNullOrEmpty(b.Id)) continue;
                    string name = b.DefinitionId ?? string.Empty;
                    if (name.IndexOf("Морг", StringComparison.OrdinalIgnoreCase) >= 0) morgues[b.Id] = b;
                    else if (name.IndexOf("Кладбище", StringComparison.OrdinalIgnoreCase) >= 0) cemeteries[b.Id] = b;
                }
            }

            // One simulated year is 360 city days. Legacy residents begin ageing from their first saved day.
            foreach (CitizenRecord citizen in citizens)
            {
                if (citizen == null || citizen.IsDeceased) continue;
                if (citizen.AgeLastAdvancedDay <= 0) citizen.AgeLastAdvancedDay = day;
                int elapsed = Math.Max(0, day - citizen.AgeLastAdvancedDay);
                int years = elapsed / 360;
                if (years <= 0) continue;
                citizen.Age = Math.Min(120, citizen.Age + years);
                citizen.AgeLastAdvancedDay += years * 360;
                if (ShouldDieNaturally(citizen, day)) MarkDeath(citizen, day, "Естественная смерть");
            }

            Dictionary<string, int> morgueUse = new Dictionary<string, int>();
            Dictionary<string, int> cemeteryUse = new Dictionary<string, int>();
            foreach (CitizenRecord citizen in citizens)
            {
                if (citizen == null || !citizen.IsDeceased) continue;
                if (!string.IsNullOrEmpty(citizen.MorgueBuildingId) && !morgues.ContainsKey(citizen.MorgueBuildingId))
                { citizen.MorgueBuildingId = null; citizen.MorgueAdmissionDay = 0; citizen.FuneralStatus = "Ожидает места в морге"; }
                if (!string.IsNullOrEmpty(citizen.CemeteryBuildingId) && !cemeteries.ContainsKey(citizen.CemeteryBuildingId))
                { citizen.CemeteryBuildingId = null; citizen.BurialDay = 0; citizen.FuneralStatus = "Ожидает места на кладбище"; }
                if (!string.IsNullOrEmpty(citizen.MorgueBuildingId) && citizen.FuneralStatus != "Захоронен") Increment(morgueUse, citizen.MorgueBuildingId);
                if (!string.IsNullOrEmpty(citizen.CemeteryBuildingId)) Increment(cemeteryUse, citizen.CemeteryBuildingId);
            }

            foreach (CitizenRecord citizen in citizens)
            {
                if (citizen == null || !citizen.IsDeceased || citizen.FuneralStatus == "Захоронен") continue;
                if (string.IsNullOrEmpty(citizen.MorgueBuildingId))
                {
                    BuildingRecord morgue = FindAvailable(morgues, morgueUse, 20);
                    if (morgue != null)
                    {
                        citizen.MorgueBuildingId = morgue.Id;
                        citizen.MorgueAdmissionDay = day;
                        citizen.FuneralStatus = "В морге";
                        Increment(morgueUse, morgue.Id);
                        UpdateEvent(citizen);
                    }
                    else citizen.FuneralStatus = "Ожидает места в морге";
                }
                if (!string.IsNullOrEmpty(citizen.MorgueBuildingId) && day - citizen.MorgueAdmissionDay >= 2)
                {
                    BuildingRecord cemetery = FindAvailable(cemeteries, cemeteryUse, 250);
                    if (cemetery != null)
                    {
                        citizen.CemeteryBuildingId = cemetery.Id;
                        citizen.BurialDay = day;
                        citizen.FuneralStatus = "Захоронен";
                        Increment(cemeteryUse, cemetery.Id);
                        UpdateEvent(citizen);
                    }
                    else citizen.FuneralStatus = "Ожидает места на кладбище";
                }
            }
            RefreshStatistics(citizens);
        }

        private static bool ShouldDieNaturally(CitizenRecord citizen, int day)
        {
            if (citizen.Age < 70) return false;
            int percent = citizen.Age >= 100 ? 35 : citizen.Age >= 95 ? 24 : citizen.Age >= 90 ? 16 : citizen.Age >= 85 ? 10 : citizen.Age >= 80 ? 6 : citizen.Age >= 75 ? 3 : 1;
            uint hash = 2166136261;
            string key = (citizen.Id ?? string.Empty) + "|" + citizen.Age + "|" + (day / 360);
            for (int i = 0; i < key.Length; i++) { hash ^= key[i]; hash *= 16777619; }
            return (hash % 100) < percent;
        }

        private void MarkDeath(CitizenRecord citizen, int day, string cause)
        {
            citizen.IsDeceased = true;
            citizen.DeathDay = day;
            citizen.DeathCause = cause;
            citizen.FuneralStatus = "Ожидает места в морге";
            if (citizen.EventHistory == null) citizen.EventHistory = new List<string>();
            citizen.EventHistory.Add("Смерть · игровой день " + day + " · " + cause);
            events.Add(new FuneralEventRecord { Id = Guid.NewGuid().ToString("N"), CitizenId = citizen.Id, DeathDay = day, Cause = cause, Status = citizen.FuneralStatus });
        }

        private void UpdateEvent(CitizenRecord citizen)
        {
            for (int i = events.Count - 1; i >= 0; i--)
                if (events[i] != null && events[i].CitizenId == citizen.Id)
                {
                    events[i].MorgueAdmissionDay = citizen.MorgueAdmissionDay;
                    events[i].BurialDay = citizen.BurialDay;
                    events[i].MorgueBuildingId = citizen.MorgueBuildingId;
                    events[i].CemeteryBuildingId = citizen.CemeteryBuildingId;
                    events[i].Status = citizen.FuneralStatus;
                    return;
                }
        }

        private static BuildingRecord FindAvailable(Dictionary<string, BuildingRecord> facilities, Dictionary<string, int> usage, int defaultCapacity)
        {
            foreach (BuildingRecord b in facilities.Values)
            {
                int used; usage.TryGetValue(b.Id, out used);
                int capacity = b.FacilityCapacity > 0 ? b.FacilityCapacity : defaultCapacity;
                if (used < capacity) return b;
            }
            return null;
        }

        private static void Increment(Dictionary<string, int> values, string key) { int n; values.TryGetValue(key, out n); values[key] = n + 1; }

        private void RefreshStatistics(IList<CitizenRecord> citizens)
        {
            DeceasedCount = WaitingForMorgueCount = InMorgueCount = WaitingForBurialCount = BuriedCount = 0;
            foreach (CitizenRecord c in citizens)
            {
                if (c == null || !c.IsDeceased) continue;
                DeceasedCount++;
                if (c.FuneralStatus == "Ожидает места в морге") WaitingForMorgueCount++;
                else if (c.FuneralStatus == "В морге") InMorgueCount++;
                else if (c.FuneralStatus == "Ожидает места на кладбище") WaitingForBurialCount++;
                else if (c.FuneralStatus == "Захоронен") BuriedCount++;
            }
        }

        private static FuneralEventRecord Clone(FuneralEventRecord x)
        {
            return new FuneralEventRecord { Id=x.Id, CitizenId=x.CitizenId, DeathDay=x.DeathDay, MorgueAdmissionDay=x.MorgueAdmissionDay,
                BurialDay=x.BurialDay, Cause=x.Cause, MorgueBuildingId=x.MorgueBuildingId, CemeteryBuildingId=x.CemeteryBuildingId, Status=x.Status };
        }
    }
}
