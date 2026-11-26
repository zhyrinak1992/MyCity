from pathlib import Path
root = Path(__file__).resolve().parents[1]
records = (root / 'Assets/Scripts/Data/CityRecords.cs').read_text(encoding='utf-8')
system = (root / 'Assets/Scripts/Simulation/FamilyEconomySystem.cs').read_text(encoding='utf-8')
bootstrap = (root / 'Assets/Scripts/MyCityBootstrap.cs').read_text(encoding='utf-8')
checks = {
    'family account is serializable': '[Serializable]' in records and 'class FamilyAccountRecord' in records,
    'family transaction ledger is serializable': 'class FamilyFinanceTransactionRecord' in records,
    'responsibility allocations are serializable': 'class FamilyResponsibilityRecord' in records,
    'save schema stores family accounts': 'List<FamilyAccountRecord> FamilyAccounts' in records,
    'save schema stores family transactions': 'List<FamilyFinanceTransactionRecord> FamilyFinanceTransactions' in records,
    'save schema stores responsibility allocations': 'List<FamilyResponsibilityRecord> FamilyResponsibilities' in records,
    'personal wallets are not merged': 'Personal citizen wallets are never merged' in system,
    'contribution validates household membership': 'citizen.HouseholdId != householdId' in system,
    'contribution debits personal wallet': 'citizen.WalletBalance -= amount' in system,
    'contribution credits shared account': 'account.Balance += amount' in system,
    'shared expense checks available balance': 'account.Balance < amount' in system,
    'shared expense debits shared account': 'account.Balance -= amount' in system,
    'expense is recorded in ledger': 'AddTransaction(householdId, "SharedExpense"' in system,
    'responsibility share is bounded': 'sharePercent < 1 || sharePercent > 100' in system,
    'responsibility total cannot exceed 100': 'used + sharePercent > 100' in system,
    'loaded responsibility totals are validated': 'used + item.SharePercent > 100' in system,
    'save schema version is migrated': 'SchemaVersion = 21' in bootstrap,
    'accounts are retained through household rebuild': 'ReconcileHouseholds' in system,
    'family data is loaded from save': 'saved.FamilyAccounts, saved.FamilyFinanceTransactions, saved.FamilyResponsibilities' in bootstrap,
    'family data is exported to save': 'Export(data.FamilyAccounts, data.FamilyFinanceTransactions, data.FamilyResponsibilities)' in bootstrap,
    'family account UI is present': 'СЕМЕЙНЫЙ СЧЁТ' in bootstrap,
    'contribution action is wired': 'familyEconomySystem.Contribute' in bootstrap,
    'shared expense action is wired': 'familyEconomySystem.PaySharedExpense' in bootstrap,
    'responsibility UI is wired': 'familyEconomySystem.SetResponsibility' in bootstrap,
    'recent family operations are displayed': 'ПОСЛЕДНИЕ ОПЕРАЦИИ' in bootstrap,
}
for name, ok in checks.items():
    print(('PASS' if ok else 'FAIL') + ' ' + name)
if not all(checks.values()):
    raise SystemExit(1)
print('RESULT: {}/{}'.format(sum(checks.values()), len(checks)))
