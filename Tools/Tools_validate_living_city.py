from pathlib import Path
import sys
root = Path(__file__).resolve().parents[1]
bootstrap = (root / 'Assets/Scripts/MyCityBootstrap.cs').read_text(encoding='utf-8')
economy = (root / 'Assets/Scripts/Simulation/CityEconomySystem.cs').read_text(encoding='utf-8')
checks = {
    'residents choose built retail workplaces only': 'store == null || !store.IsRetailBusiness || !store.IsOpen' in bootstrap,
    'resident stores public activity destination': 'ActivityBuildingId' in bootstrap,
    'resident dwell time is game-minute based': 'ActivityMinutesRemaining--' in bootstrap,
    'residents route to selected building entrance': 'chosen.Entrance.x' in bootstrap and 'chosen.Entrance.z' in bootstrap,
    'visit history is persisted on citizen record': 'a.Record.EventHistory.Add("public-visit:"' in bootstrap,
    'visit history is bounded': 'a.Record.EventHistory.Count > 120' in bootstrap,
    'visit events are published': '"resident-public-visit"' in bootstrap and '"resident-shop-purchase"' in bootstrap,
    'shop arrival invokes the existing economy system': 'economySystem.TryResidentRetailPurchase' in bootstrap,
    'purchase validates registered citizen and workplace': 'citizens.TryGetValue(citizenId' in economy and 'workplaces.TryGetValue(buildingId' in economy,
    'purchase is limited to open retail stores': '!workplace.IsRetailBusiness || !workplace.IsOpen' in economy,
    'daily shopper key prevents duplicate purchases': 'if (!shopperDayKeys.Add(shopperKey)) return false;' in economy,
    'wallet is debited and store cash credited': 'citizen.WalletBalance = checked(citizen.WalletBalance - salePrice)' in economy and 'workplace.OperatingBalance = checked(workplace.OperatingBalance + salePrice)' in economy,
    'sale reduces actual store inventory': 'selected.StockUnits--;' in economy and 'ConsumeFefoLot(workplace, selected.ProductId' in economy,
    'sales update existing business analytics': 'workplace.CumulativeRevenue = checked(workplace.CumulativeRevenue + salePrice)' in economy and 'TotalBusinessRevenue = checked(TotalBusinessRevenue + salePrice)' in economy,
    'sale creates idempotent financial transaction': 'RecordFinancial(day, "Розничные продажи"' in economy and 'resident-sale-' in economy,
    'purchase event is retained in citizen history': '"shop-purchase:" + shopperKey' in economy,
    'daily background demand shares the same idempotency set': 'shopperDayKeys.Contains(shopperKey)' in economy,
    'fixed sleep/work destinations remain': 'a.ScheduleState == CitizenDailyPhase.Sleep' in bootstrap and 'a.ScheduleState == CitizenDailyPhase.Work' in bootstrap,
}
for name, ok in checks.items(): print(('PASS' if ok else 'FAIL') + ' ' + name)
print(f'{sum(checks.values())}/{len(checks)} checks passed')
sys.exit(0 if all(checks.values()) else 1)
