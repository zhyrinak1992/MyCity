using System;
using System.Collections.Generic;
using MyCity.Data;

namespace MyCity.Simulation
{
    /// <summary>Сохраняемая очередь небольших ремонтных проектов, связанных с обращениями мэру.</summary>
    public sealed class CityMaintenanceSystem
    {
        private readonly List<CityMaintenanceProjectRecord> projects = new List<CityMaintenanceProjectRecord>();
        public IList<CityMaintenanceProjectRecord> Projects { get { return projects.AsReadOnly(); } }

        public void Load(List<CityMaintenanceProjectRecord> saved)
        {
            projects.Clear();
            if (saved != null) for (int i = 0; i < saved.Count; i++) if (saved[i] != null) projects.Add(saved[i]);
        }

        public void Export(List<CityMaintenanceProjectRecord> target)
        {
            target.Clear(); target.AddRange(projects);
        }

        public bool TryCreateFromRequest(MayorRequestRecord request, string buildingId, int day, out CityMaintenanceProjectRecord project, out string error)
        {
            project = null; error = null;
            if (request == null || string.IsNullOrEmpty(request.Id)) { error = "Обращение не найдено."; return false; }
            if (string.IsNullOrEmpty(buildingId)) { error = "В городе пока нет здания, которое можно назначить объектом работ."; return false; }
            if (!IsMaintenanceCategory(request.Category)) return false;
            if (!string.IsNullOrEmpty(request.ProjectId))
            {
                project = projects.Find(x => x != null && x.Id == request.ProjectId);
                return project != null;
            }
            string id = "maintenance-" + request.Id;
            project = projects.Find(x => x != null && x.Id == id);
            if (project != null) { request.ProjectId = project.Id; return true; }
            project = new CityMaintenanceProjectRecord
            {
                Id = id, RequestId = request.Id, BuildingId = buildingId,
                Title = "Ремонт: " + (request.Title ?? "городской объект"),
                Status = "В очереди", CreatedDay = Math.Max(1, day), DueDay = Math.Max(1, day) + 3,
                RequiredDays = 3, Budget = Math.Max(0, request.ActualCost)
            };
            projects.Add(project); request.ProjectId = project.Id; return true;
        }

        public void AdvanceToDay(int day, Action<string> applyCompletion)
        {
            for (int i = 0; i < projects.Count; i++)
            {
                CityMaintenanceProjectRecord p = projects[i];
                if (p == null || p.Status == "Завершён" || p.Status == "Отменён" || day <= p.CreatedDay || day <= p.LastProgressDay) continue;
                p.LastProgressDay = day; p.Status = "Выполняется"; p.ProgressDays = Math.Min(Math.Max(1, p.RequiredDays), p.ProgressDays + 1);
                if (p.ProgressDays >= Math.Max(1, p.RequiredDays))
                {
                    if (!p.CompletionApplied && applyCompletion != null) applyCompletion(p.BuildingId);
                    p.CompletionApplied = true; p.Status = "Завершён";
                }
            }
        }

        private static bool IsMaintenanceCategory(string category)
        {
            return category == "Ремонт" || category == "Инфраструктура" || category == "Благоустройство";
        }
    }
}
