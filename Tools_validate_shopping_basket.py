from pathlib import Path
p = Path(__file__).parent / 'Assets/Scripts/Simulation/CityEconomySystem.cs'
s = p.read_text(encoding='utf-8')
checks = {
    'daily idempotency checked before basket build': 'if (shopperDayKeys.Contains(shopperKey)) return false;' in s,
    'basket target is one to three products': '1 + StableDemandBucket' in s and '":basket-size"' in s,
    'basket excludes duplicate SKU': 'selectedProductIds.Add(candidate.ProductId)' in s,
    'available stock excludes return reservations': 'candidate.StockUnits - Math.Max(0, candidate.ReservedForReturnUnits) <= 0' in s,
    'wallet affordability validated before commit': 'basketTotal > citizen.WalletBalance - candidate.RetailPrice' in s,
    'failed basket does not mutate economy': 'if (basket.Count == 0) return false;' in s,
    'daily key committed only after non-empty basket': s.index('if (basket.Count == 0) return false;') < s.index('shopperDayKeys.Add(shopperKey);', s.index('public bool TryResidentRetailPurchase')),
    'wallet debited by aggregate basket value': 'citizen.WalletBalance = checked(citizen.WalletBalance - basketTotal);' in s,
    'store credited by aggregate basket value': 'workplace.OperatingBalance = checked(workplace.OperatingBalance + basketTotal);' in s,
    'physical stock decremented per line': 'item.StockUnits--;\n                ConsumeFefoLot' in s,
    'FEFO lot consumed per product': 'ConsumeFefoLot(workplace, item.ProductId' in s,
    'units and revenue analytics updated': 'workplace.LastDailyUnitsSold = checked(workplace.LastDailyUnitsSold + basket.Count);' in s and 'workplace.LastDailyRevenue = checked(workplace.LastDailyRevenue + basketTotal);' in s,
    'one aggregated financial transaction': '"resident-sale-" + shopperKey + "-" + workplace.BuildingId' in s and 'корзина " + basket.Count' in s,
    'receipt and item lines persisted in event history': 'shop-purchase-basket:' in s and 'shop-purchase-item:' in s,
    'event history bounded': 'while (citizen.EventHistory.Count > 120)' in s,
}
for name, ok in checks.items(): print(('PASS' if ok else 'FAIL') + ' ' + name)
print(f'{sum(checks.values())}/{len(checks)} checks passed')
raise SystemExit(0 if all(checks.values()) else 1)
