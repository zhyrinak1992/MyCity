using System.Collections.Generic;
using MyCity.Core;
using MyCity.Data;
using UnityEngine;

namespace MyCity.Simulation
{
    /// <summary>Updates persistent citizen records when the canonical game clock enters a new daily phase.</summary>
    public sealed class CitizenSimulationSystem : MonoBehaviour
    {
        private readonly List<CitizenRecord> citizens = new List<CitizenRecord>();
        private GameClock subscribedClock;

        public IList<CitizenRecord> Citizens { get { return citizens.AsReadOnly(); } }

        private void OnEnable() { TrySubscribe(); }
        private void Update() { if (subscribedClock == null) TrySubscribe(); }

        private void TrySubscribe()
        {
            GameClock current = GameClock.Instance;
            if (current == null || current == subscribedClock) return;
            Unsubscribe();
            subscribedClock = current;
            subscribedClock.OnTick += HandleTick;
        }

        private void Unsubscribe()
        {
            if (subscribedClock != null) subscribedClock.OnTick -= HandleTick;
            subscribedClock = null;
        }

        private void OnDisable() { Unsubscribe(); }

        public void Register(CitizenRecord citizen)
        {
            if (citizen == null || string.IsNullOrEmpty(citizen.Id)) return;
            if (citizens.Exists(c => c.Id == citizen.Id)) return;
            if (citizen.EventHistory == null) citizen.EventHistory = new List<string>();
            citizens.Add(citizen);
        }

        public void LoadCitizens(IEnumerable<CitizenRecord> records)
        {
            citizens.Clear();
            if (records == null) return;
            foreach (CitizenRecord record in records) Register(record);
        }

        private void HandleTick(long tick)
        {
            if (subscribedClock == null) return;
            CitizenDailyPhase phase = CitizenDailySchedule.GetPhase(subscribedClock.MinuteOfDay);
            string label = CitizenDailySchedule.GetLabel(phase);
            for (int i = 0; i < citizens.Count; i++)
            {
                CitizenRecord citizen = citizens[i];
                if (citizen == null || citizen.ScheduleState == (int)phase) continue;
                citizen.ScheduleState = (int)phase;
                if (citizen.EventHistory == null) citizen.EventHistory = new List<string>();
                citizen.EventHistory.Add("tick:" + tick + "|phase:" + label);
                GameEventBus.Publish(new GameEvent(GameEventType.CitizenStateChanged, tick,
                    "citizen-simulation", citizen.Id, label));
            }
        }
    }
}
