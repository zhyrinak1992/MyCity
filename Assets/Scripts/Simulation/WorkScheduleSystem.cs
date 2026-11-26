using System.Collections.Generic;
using MyCity.Core;
using MyCity.Data;
using UnityEngine;

namespace MyCity.Simulation
{
    /// <summary>
    /// Lightweight employment registry and attendance model.
    /// All attendance transitions are driven by the canonical city clock.
    /// </summary>
    public sealed class WorkScheduleSystem : MonoBehaviour
    {
        private readonly List<WorkplaceRecord> workplaces = new List<WorkplaceRecord>();
        private readonly Dictionary<string, CitizenRecord> citizens = new Dictionary<string, CitizenRecord>();
        private GameClock clock;

        public IList<WorkplaceRecord> Workplaces { get { return workplaces.AsReadOnly(); } }

        private void OnEnable() { TrySubscribe(); }
        private void Update() { if (clock == null) TrySubscribe(); }
        private void OnDisable() { if (clock != null) clock.OnTick -= HandleTick; clock = null; }

        private void TrySubscribe()
        {
            GameClock current = GameClock.Instance;
            if (current == null || current == clock) return;
            if (clock != null) clock.OnTick -= HandleTick;
            clock = current;
            clock.OnTick += HandleTick;
        }

        public bool RegisterWorkplace(WorkplaceRecord workplace)
        {
            if (workplace == null || string.IsNullOrEmpty(workplace.Id) ||
                string.IsNullOrEmpty(workplace.BuildingId) || workplace.StaffCapacity < 0)
                return false;

            for (int i = 0; i < workplaces.Count; i++)
                if (workplaces[i].Id == workplace.Id) return false;

            if (workplaces.Exists(w => w.BuildingId == workplace.BuildingId)) return false;
            workplace.StaffCount = 0;
            workplace.MonthlyPayroll = 0;
            workplaces.Add(workplace);
            return true;
        }

        public void RegisterCitizen(CitizenRecord citizen)
        {
            if (citizen == null || string.IsNullOrEmpty(citizen.Id)) return;
            citizens[citizen.Id] = citizen;
            RecountStaff();
        }

        public bool AssignCitizen(string citizenId, string workplaceId, string professionId, int monthlySalary)
        {
            CitizenRecord citizen;
            WorkplaceRecord workplace = workplaces.Find(w => w.Id == workplaceId && w.IsOpen);
            if (!citizens.TryGetValue(citizenId, out citizen) || workplace == null ||
                string.IsNullOrEmpty(professionId) || monthlySalary < 0)
                return false;

            if (citizen.WorkplaceBuildingId == workplace.BuildingId)
            {
                citizen.ProfessionId = professionId;
                citizen.MonthlySalary = monthlySalary;
                citizen.IsEmployed = true;
                RecountStaff();
                return true;
            }

            if (workplace.StaffCount >= workplace.StaffCapacity) return false;

            citizen.WorkplaceBuildingId = workplace.BuildingId;
            citizen.ProfessionId = professionId;
            citizen.MonthlySalary = monthlySalary;
            citizen.IsEmployed = true;
            RecountStaff();
            GameEventBus.Publish(new GameEvent(GameEventType.EmploymentChanged,
                clock != null ? clock.Tick : 0, "work-schedule", citizenId, workplace.BuildingId));
            return true;
        }

        public void UnassignCitizen(string citizenId)
        {
            CitizenRecord citizen;
            if (!citizens.TryGetValue(citizenId, out citizen)) return;
            string previous = citizen.WorkplaceBuildingId;
            citizen.WorkplaceBuildingId = null;
            citizen.ProfessionId = null;
            citizen.MonthlySalary = 0;
            citizen.IsEmployed = false;
            RecountStaff();
            GameEventBus.Publish(new GameEvent(GameEventType.EmploymentChanged,
                clock != null ? clock.Tick : 0, "work-schedule", citizenId, "unemployed:" + previous));
        }

        public void Load(IEnumerable<CitizenRecord> citizenRecords, IEnumerable<WorkplaceRecord> workplaceRecords)
        {
            citizens.Clear();
            workplaces.Clear();
            if (workplaceRecords != null)
                foreach (WorkplaceRecord w in workplaceRecords)
                    if (w != null && !string.IsNullOrEmpty(w.Id)) workplaces.Add(w);
            if (citizenRecords != null)
                foreach (CitizenRecord c in citizenRecords)
                    if (c != null && !string.IsNullOrEmpty(c.Id)) citizens[c.Id] = c;
            RecountStaff();
        }

        private void RecountStaff()
        {
            for (int i = 0; i < workplaces.Count; i++)
            {
                workplaces[i].StaffCount = 0;
                workplaces[i].MonthlyPayroll = 0;
            }

            foreach (CitizenRecord citizen in citizens.Values)
            {
                if (citizen == null || !citizen.IsEmployed) continue;
                WorkplaceRecord workplace = workplaces.Find(w =>
                    w.BuildingId == citizen.WorkplaceBuildingId && w.IsOpen);
                if (workplace == null) continue;
                workplace.StaffCount++;
                long payroll = (long)workplace.MonthlyPayroll + Mathf.Max(0, citizen.MonthlySalary);
                workplace.MonthlyPayroll = payroll > int.MaxValue ? int.MaxValue : (int)payroll;
            }
        }

        private void HandleTick(long tick)
        {
            if (clock == null) return;
            bool isWorkHours = clock.MinuteOfDay >= 9 * 60 && clock.MinuteOfDay < 17 * 60;
            foreach (CitizenRecord citizen in citizens.Values)
            {
                if (citizen == null || !citizen.IsEmployed) continue;
                string desired = isWorkHours ? "at_work" : "off_duty";
                // Attendance is represented as a state transition, not per-frame polling.
                if (citizen.EventHistory == null) citizen.EventHistory = new List<string>();
                string marker = "attendance:" + desired;
                if (citizen.EventHistory.Count > 0 &&
                    citizen.EventHistory[citizen.EventHistory.Count - 1].EndsWith(marker))
                    continue;
                citizen.EventHistory.Add("tick:" + tick + "|" + marker);
                if (isWorkHours && clock.MinuteOfDay == 9 * 60) citizen.DaysWorked++;
                GameEventBus.Publish(new GameEvent(GameEventType.WorkAttendanceChanged,
                    tick, "work-schedule", citizen.Id, desired));
            }
        }
    }
}
