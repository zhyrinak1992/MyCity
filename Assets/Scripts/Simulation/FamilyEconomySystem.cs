using System;
using System.Collections.Generic;
using MyCity.Data;
using UnityEngine;

namespace MyCity.Simulation
{
    /// <summary>Shared household account. Personal citizen wallets are never merged or rewritten by this system.</summary>
    public sealed class FamilyEconomySystem : MonoBehaviour
    {
        private readonly Dictionary<string, FamilyAccountRecord> accounts = new Dictionary<string, FamilyAccountRecord>();
        private readonly List<FamilyFinanceTransactionRecord> transactions = new List<FamilyFinanceTransactionRecord>();
        private readonly List<FamilyResponsibilityRecord> responsibilities = new List<FamilyResponsibilityRecord>();
        private readonly Dictionary<string, CitizenRecord> citizens = new Dictionary<string, CitizenRecord>();

        public void Load(IEnumerable<CitizenRecord> people, IEnumerable<FamilyAccountRecord> savedAccounts,
            IEnumerable<FamilyFinanceTransactionRecord> savedTransactions, IEnumerable<FamilyResponsibilityRecord> savedResponsibilities)
        {
            citizens.Clear(); accounts.Clear(); transactions.Clear(); responsibilities.Clear();
            if (people != null) foreach (CitizenRecord person in people)
                if (person != null && !string.IsNullOrEmpty(person.Id)) citizens[person.Id] = person;
            if (savedAccounts != null) foreach (FamilyAccountRecord account in savedAccounts)
            {
                if (account == null || string.IsNullOrEmpty(account.HouseholdId) || account.Balance < 0) continue;
                accounts[account.HouseholdId] = Clone(account);
            }
            if (savedTransactions != null) foreach (FamilyFinanceTransactionRecord tx in savedTransactions)
            {
                if (tx == null || string.IsNullOrEmpty(tx.HouseholdId) || tx.Amount <= 0) continue;
                transactions.Add(Clone(tx));
            }
            if (savedResponsibilities != null) foreach (FamilyResponsibilityRecord item in savedResponsibilities)
            {
                if (item == null || string.IsNullOrEmpty(item.HouseholdId) || string.IsNullOrEmpty(item.Category) ||
                    !citizens.ContainsKey(item.CitizenId) || citizens[item.CitizenId].HouseholdId != item.HouseholdId || item.SharePercent < 1 || item.SharePercent > 100) continue;
                int used = 0;
                foreach (FamilyResponsibilityRecord accepted in responsibilities)
                    if (accepted.HouseholdId == item.HouseholdId && accepted.Category == item.Category) used += accepted.SharePercent;
                if (used + item.SharePercent > 100) continue;
                responsibilities.Add(Clone(item));
            }
        }

        public void RefreshCitizens(IEnumerable<CitizenRecord> people)
        {
            citizens.Clear();
            if (people != null) foreach (CitizenRecord person in people)
                if (person != null && !string.IsNullOrEmpty(person.Id)) citizens[person.Id] = person;
        }

        public void ReconcileHouseholds(IEnumerable<HouseholdRecord> households, int day)
        {
            if (households == null) return;
            foreach (HouseholdRecord household in households)
            {
                if (household == null || string.IsNullOrEmpty(household.Id)) continue;
                if (!accounts.ContainsKey(household.Id))
                    accounts.Add(household.Id, new FamilyAccountRecord { Id = Guid.NewGuid().ToString("N"), HouseholdId = household.Id, CreatedDay = Math.Max(1, day), LastUpdatedDay = Math.Max(1, day) });
            }
            // Keep accounts for temporarily empty homes: a household may be rebuilt during loading or a move.
        }

        public FamilyAccountRecord GetAccount(string householdId)
        {
            FamilyAccountRecord account;
            return !string.IsNullOrEmpty(householdId) && accounts.TryGetValue(householdId, out account) ? account : null;
        }

        public bool Contribute(string householdId, string citizenId, long amount, int day, out string error)
        {
            error = null;
            CitizenRecord citizen; FamilyAccountRecord account = GetAccount(householdId);
            if (account == null || !citizens.TryGetValue(citizenId, out citizen) || citizen.HouseholdId != householdId)
            { error = "Внести деньги может только участник этого домохозяйства."; return false; }
            if (amount <= 0) { error = "Укажите сумму больше нуля."; return false; }
            if (citizen.WalletBalance < amount) { error = "На личном кошельке недостаточно средств."; return false; }
            try { checked { account.Balance += amount; } } catch (OverflowException) { error = "Превышен допустимый баланс счёта."; return false; }
            citizen.WalletBalance -= amount; account.LastUpdatedDay = Math.Max(1, day);
            AddTransaction(householdId, "Contribution", "Пополнение", "Взнос участника", amount, citizenId, day);
            return true;
        }

        public bool PaySharedExpense(string householdId, string citizenId, string category, string description, long amount, int day, out string error)
        {
            error = null; FamilyAccountRecord account = GetAccount(householdId);
            CitizenRecord citizen;
            if (account == null || !citizens.TryGetValue(citizenId, out citizen) || citizen.HouseholdId != householdId)
            { error = "Расход может оформить только участник этого домохозяйства."; return false; }
            if (amount <= 0) { error = "Укажите сумму больше нуля."; return false; }
            if (account.Balance < amount) { error = "В семейном счёте недостаточно средств."; return false; }
            account.Balance -= amount; account.LastUpdatedDay = Math.Max(1, day);
            AddTransaction(householdId, "SharedExpense", string.IsNullOrEmpty(category) ? "Общие расходы" : category,
                string.IsNullOrEmpty(description) ? "Семейная покупка" : description, amount, citizenId, day);
            return true;
        }

        public bool SetResponsibility(string householdId, string category, string citizenId, int sharePercent, out string error)
        {
            error = null; CitizenRecord citizen;
            if (string.IsNullOrEmpty(householdId) || string.IsNullOrEmpty(category) ||
                !citizens.TryGetValue(citizenId, out citizen) || citizen.HouseholdId != householdId)
            { error = "Ответственным можно назначить только участника этого домохозяйства."; return false; }
            if (sharePercent < 1 || sharePercent > 100) { error = "Доля должна быть от 1 до 100 процентов."; return false; }
            int used = 0;
            foreach (FamilyResponsibilityRecord item in responsibilities)
                if (item.HouseholdId == householdId && item.Category == category && item.CitizenId != citizenId) used += item.SharePercent;
            if (used + sharePercent > 100) { error = "Сумма долей по этой статье не может превышать 100%."; return false; }
            responsibilities.RemoveAll(delegate(FamilyResponsibilityRecord item) { return item.HouseholdId == householdId && item.Category == category && item.CitizenId == citizenId; });
            responsibilities.Add(new FamilyResponsibilityRecord { HouseholdId = householdId, Category = category, CitizenId = citizenId, SharePercent = sharePercent });
            return true;
        }

        public IList<FamilyFinanceTransactionRecord> GetTransactions(string householdId)
        {
            List<FamilyFinanceTransactionRecord> result = new List<FamilyFinanceTransactionRecord>();
            for (int i = transactions.Count - 1; i >= 0; i--)
                if (transactions[i].HouseholdId == householdId) result.Add(transactions[i]);
            return result;
        }

        public IList<FamilyResponsibilityRecord> GetResponsibilities(string householdId)
        {
            List<FamilyResponsibilityRecord> result = new List<FamilyResponsibilityRecord>();
            foreach (FamilyResponsibilityRecord item in responsibilities) if (item.HouseholdId == householdId) result.Add(item);
            return result;
        }

        public void Export(List<FamilyAccountRecord> savedAccounts, List<FamilyFinanceTransactionRecord> savedTransactions,
            List<FamilyResponsibilityRecord> savedResponsibilities)
        {
            if (savedAccounts != null) { savedAccounts.Clear(); foreach (FamilyAccountRecord item in accounts.Values) savedAccounts.Add(Clone(item)); }
            if (savedTransactions != null) { savedTransactions.Clear(); foreach (FamilyFinanceTransactionRecord item in transactions) savedTransactions.Add(Clone(item)); }
            if (savedResponsibilities != null) { savedResponsibilities.Clear(); foreach (FamilyResponsibilityRecord item in responsibilities) savedResponsibilities.Add(Clone(item)); }
        }

        private void AddTransaction(string householdId, string type, string category, string description, long amount, string citizenId, int day)
        {
            transactions.Add(new FamilyFinanceTransactionRecord { Id = Guid.NewGuid().ToString("N"), HouseholdId = householdId,
                Type = type, Category = category, Description = description, Amount = amount, CitizenId = citizenId, Day = Math.Max(1, day) });
            // Bound save growth while retaining a useful recent ledger.
            if (transactions.Count > 5000) transactions.RemoveRange(0, transactions.Count - 5000);
        }
        private static FamilyAccountRecord Clone(FamilyAccountRecord x) { return new FamilyAccountRecord { Id=x.Id, HouseholdId=x.HouseholdId, Balance=x.Balance, CreatedDay=x.CreatedDay, LastUpdatedDay=x.LastUpdatedDay }; }
        private static FamilyFinanceTransactionRecord Clone(FamilyFinanceTransactionRecord x) { return new FamilyFinanceTransactionRecord { Id=x.Id, HouseholdId=x.HouseholdId, Day=x.Day, Type=x.Type, Category=x.Category, Description=x.Description, Amount=x.Amount, CitizenId=x.CitizenId }; }
        private static FamilyResponsibilityRecord Clone(FamilyResponsibilityRecord x) { return new FamilyResponsibilityRecord { HouseholdId=x.HouseholdId, Category=x.Category, CitizenId=x.CitizenId, SharePercent=x.SharePercent }; }
    }
}
