using System;
using System.Collections.Generic;
using MyCity.Data;

namespace MyCity.Simulation
{
    /// <summary>Сохраняемая очередь обращений и ежедневные отчёты мэра.</summary>
    public sealed class MayorOfficeSystem
    {
        private readonly List<MayorRequestRecord> requests = new List<MayorRequestRecord>();
        private readonly List<MayorDailyReportRecord> reports = new List<MayorDailyReportRecord>();
        private int approvalScore = 50;
        private int lastGeneratedDay;
        private int receivedToday;
        private int resolvedToday;
        private int deferredToday;
        private long spentToday;
        private int approvalDeltaToday;

        public int ApprovalScore { get { return approvalScore; } }
        public IList<MayorRequestRecord> Requests { get { return requests.AsReadOnly(); } }
        public IList<MayorDailyReportRecord> Reports { get { return reports.AsReadOnly(); } }
        public int PendingCount { get { int n = 0; for (int i=0;i<requests.Count;i++) if (requests[i] != null && requests[i].Status == "Ожидает решения") n++; return n; } }

        public void Load(List<MayorRequestRecord> savedRequests, List<MayorDailyReportRecord> savedReports, int savedApproval, int savedLastDay)
        {
            requests.Clear(); reports.Clear();
            if (savedRequests != null) for (int i=0;i<savedRequests.Count;i++) if (savedRequests[i] != null) requests.Add(savedRequests[i]);
            if (savedReports != null) for (int i=0;i<savedReports.Count;i++) if (savedReports[i] != null) reports.Add(savedReports[i]);
            approvalScore = Clamp(savedApproval, 0, 100); lastGeneratedDay = Math.Max(0, savedLastDay);
            receivedToday = resolvedToday = deferredToday = approvalDeltaToday = 0; spentToday = 0;
        }

        public void Export(List<MayorRequestRecord> targetRequests, List<MayorDailyReportRecord> targetReports)
        {
            targetRequests.Clear(); targetReports.Clear();
            targetRequests.AddRange(requests); targetReports.AddRange(reports);
        }

        public void AdvanceToDay(int day, int residentCount, int supplierCount)
        {
            if (day <= 0 || day <= lastGeneratedDay) return;
            if (lastGeneratedDay > 0) CloseDay(lastGeneratedDay);
            lastGeneratedDay = day;
            // Небольшой поток обращений, растущий вместе с городом; не создаём фиктивных заявителей без населения/поставщиков.
            int count = residentCount > 0 ? 1 : 0;
            if (supplierCount > 0 && day % 3 == 0) count++;
            if (residentCount >= 8 && day % 2 == 0) count++;
            for (int i=0;i<count;i++) AddGeneratedRequest(day, i, residentCount, supplierCount);
        }

        private void AddGeneratedRequest(int day, int slot, int residents, int suppliers)
        {
            int seed = Math.Abs((day * 7919 + slot * 104729 + residents * 97 + suppliers * 13) % 6);
            MayorRequestRecord r = new MayorRequestRecord { Id = "mayor-" + day + "-" + slot, CreatedDay = day, DueDay = day + (seed == 0 ? 1 : 3), Priority = seed == 0 ? 3 : (seed == 1 ? 2 : 1), Status = "Ожидает решения" };
            switch (seed)
            {
                case 0: r.SenderType="Житель"; r.SenderName="Житель района"; r.Category="Инфраструктура"; r.Title="Неисправное уличное освещение"; r.Message="Жители просят проверить освещение на улице и организовать ремонт."; r.SuggestedCost=12000; break;
                case 1: r.SenderType="Житель"; r.SenderName="Инициативная группа"; r.Category="Благоустройство"; r.Title="Небольшое улучшение общественной зоны"; r.Message="Предлагаем установить дополнительные скамейки и урны."; r.SuggestedCost=8000; break;
                case 2: r.SenderType="Предприниматель"; r.SenderName="Местный предприниматель"; r.Category="Бизнес"; r.Title="Просьба улучшить подъезд к объекту"; r.Message="Плохой подъезд мешает поставкам и работе предприятия."; r.SuggestedCost=18000; break;
                case 3: r.SenderType="Поставщик"; r.SenderName="Поставщик города"; r.Category="Поставки"; r.Title="Запрос по условиям сотрудничества"; r.Message="Поставщик просит подтвердить дальнейшие условия сотрудничества."; r.SuggestedCost=0; break;
                case 4: r.SenderType="Городская служба"; r.SenderName="Служба эксплуатации"; r.Category="Ремонт"; r.Title="Плановый ремонт объекта"; r.Message="Служба сообщает о необходимости диагностики и небольшого ремонта городской инфраструктуры."; r.SuggestedCost=15000; break;
                default: r.SenderType="Житель"; r.SenderName="Житель города"; r.Category="Обращение"; r.Title="Предложение по развитию района"; r.Message="Житель предлагает рассмотреть небольшое улучшение района."; r.SuggestedCost=10000; break;
            }
            if (r.SenderType == "Поставщик" && suppliers <= 0) { r.SenderType="Житель"; r.SenderName="Житель города"; r.Category="Обращение"; r.Title="Предложение по развитию района"; r.Message="Житель предлагает рассмотреть небольшое улучшение района."; }
            if (requests.Find(x => x != null && x.Id == r.Id) == null) { requests.Add(r); receivedToday++; }
        }

        public bool Resolve(string requestId, string decision, bool approve, long cityBudget, int day, out long cost, out string error)
        {
            cost = 0; error = null;
            MayorRequestRecord r = requests.Find(x => x != null && x.Id == requestId);
            if (r == null) { error="Обращение не найдено."; return false; }
            if (r.Status != "Ожидает решения") { error="Это обращение уже обработано."; return false; }
            if (approve && r.SuggestedCost > cityBudget) { error="В городской казне недостаточно средств. Можно отложить решение."; return false; }
            r.Decision = decision; r.ResolvedDay = day;
            if (approve)
            {
                r.Status="Решено"; r.ActualCost=r.SuggestedCost; r.RelationshipDelta=2; cost=r.ActualCost;
                resolvedToday++; spentToday += cost; approvalDeltaToday += 2;
            }
            else
            {
                r.Status="Отложено"; r.RelationshipDelta=-1; deferredToday++; approvalDeltaToday--;
            }
            approvalScore = Clamp(approvalScore + r.RelationshipDelta, 0, 100);
            return true;
        }

        private void CloseDay(int day)
        {
            if (receivedToday == 0 && resolvedToday == 0 && deferredToday == 0) return;
            reports.Add(new MayorDailyReportRecord { Day=day, RequestsReceived=receivedToday, RequestsResolved=resolvedToday, RequestsDeferred=deferredToday, MoneySpent=spentToday, ResidentApprovalDelta=approvalDeltaToday, Summary="Получено обращений: " + receivedToday + "; решено: " + resolvedToday + "; отложено: " + deferredToday + "." });
            if (reports.Count > 180) reports.RemoveAt(0);
            receivedToday = resolvedToday = deferredToday = approvalDeltaToday = 0; spentToday = 0;
        }
        private static int Clamp(int v, int min, int max) { return v < min ? min : (v > max ? max : v); }
    }
}
