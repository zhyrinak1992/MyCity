using System;
using System.Collections.Generic;
using MyCity.Core;
using MyCity.Data;
using UnityEngine;

namespace MyCity.Simulation
{
    /// <summary>Prototype business ledger and payroll. Daily settlements are idempotent and persisted.</summary>
    public sealed class CityEconomySystem : MonoBehaviour
    {
        private readonly Dictionary<string, CitizenRecord> citizens = new Dictionary<string, CitizenRecord>();
        private readonly Dictionary<string, WorkplaceRecord> workplaces = new Dictionary<string, WorkplaceRecord>();
        private readonly HashSet<string> accruedKeys = new HashSet<string>();
        private readonly HashSet<string> paidKeys = new HashSet<string>();
        private readonly HashSet<string> businessDayKeys = new HashSet<string>();
        private readonly HashSet<string> shopperDayKeys = new HashSet<string>();
        private readonly List<CityFinancialTransactionRecord> financialTransactions = new List<CityFinancialTransactionRecord>();
        private readonly HashSet<string> financialTransactionIds = new HashSet<string>();
        private GameClock clock;

        public long TotalPaid { get; private set; }
        public long TotalBusinessRevenue { get; private set; }
        public long TotalBusinessCosts { get; private set; }
        public int LastProcessedDay { get; private set; }
        public event Action<string> OnPayrollMessage;
        public event Action<string> OnBusinessMessage;
        public event Action OnEconomyDaySettled;

        /// <summary>Cash currently held by registered businesses; this is not the city treasury.</summary>
        public long GetTotalWorkplaceCash()
        {
            long total = 0;
            foreach (WorkplaceRecord workplace in workplaces.Values)
                if (workplace != null) total = checked(total + Math.Max(0, workplace.OperatingBalance));
            return total;
        }

        public int RegisteredWorkplaceCount { get { return workplaces.Count; } }

        private void RecordFinancial(int day, string category, string description, long amount, bool income, string sourceId)
        {
            if (amount <= 0) return;
            string id = string.IsNullOrEmpty(sourceId) ? StableId.NewId().ToString() : sourceId;
            if (!financialTransactionIds.Add(id)) return;
            financialTransactions.Add(new CityFinancialTransactionRecord { Id = id, Day = Math.Max(0, day), Category = category, Description = description, Amount = amount, IsIncome = income, SourceId = sourceId });
        }

        public void ExportFinancialTransactions(List<CityFinancialTransactionRecord> destination)
        {
            if (destination == null) return;
            destination.Clear(); destination.AddRange(financialTransactions);
        }

        /// <summary>Records an external city-level income or expense with a stable idempotency key.</summary>
        public void RecordExternalTransaction(int day, string category, string description, long amount, bool income, string stableId)
        {
            if (amount <= 0 || string.IsNullOrEmpty(stableId)) return;
            RecordFinancial(day, category, description, amount, income, stableId);
        }

        private void OnEnable() { TrySubscribe(); }
        private void Update() { if (clock == null) TrySubscribe(); }
        private void OnDisable() { if (clock != null) clock.OnDayChanged -= HandleDayChanged; clock = null; }
        private void TrySubscribe()
        {
            GameClock current = GameClock.Instance;
            if (current == null || current == clock) return;
            if (clock != null) clock.OnDayChanged -= HandleDayChanged;
            clock = current;
            clock.OnDayChanged += HandleDayChanged;
        }

        public void RegisterCitizen(CitizenRecord citizen)
        {
            if (citizen == null || string.IsNullOrEmpty(citizen.Id)) return;
            // Migration defaults for citizens created before household needs existed.
            citizen.FoodNeed = ClampNeed(citizen.FoodNeed <= 0f ? 35f : citizen.FoodNeed);
            citizen.ClothingNeed = ClampNeed(citizen.ClothingNeed <= 0f ? 20f : citizen.ClothingNeed);
            citizen.HomeGoodsNeed = ClampNeed(citizen.HomeGoodsNeed <= 0f ? 25f : citizen.HomeGoodsNeed);
            citizen.LeisureNeed = ClampNeed(citizen.LeisureNeed <= 0f ? 30f : citizen.LeisureNeed);
            if (citizen.NeedsLastUpdatedDay <= 0) citizen.NeedsLastUpdatedDay = 1;
            citizens[citizen.Id] = citizen;
        }

        public void RegisterWorkplace(WorkplaceRecord workplace)
        {
            if (workplace == null || string.IsNullOrEmpty(workplace.BuildingId)) return;
            if (workplace.IsRetailBusiness)
            {
                // Apply defaults before migrating inventory so legacy JSON fields (which deserialize as zero)
                // cannot accidentally initialize a new store with an empty target stock.
                if (workplace.TargetInventoryUnits <= 0) workplace.TargetInventoryUnits = 100;
                if (workplace.ProductUnitCost <= 0) workplace.ProductUnitCost = 200;
                if (workplace.ProductUnitPrice <= workplace.ProductUnitCost) workplace.ProductUnitPrice = 350;
                if (string.IsNullOrEmpty(workplace.StoreTypeId)) workplace.StoreTypeId = "grocery";
                if (string.IsNullOrEmpty(workplace.ProductDisplayName)) workplace.ProductDisplayName = "Продукты";
                if (workplace.Inventory == null) workplace.Inventory = new List<StoreInventoryRecord>();
                if (workplace.Inventory.Count == 0)
                {
                    // Upgrade v1.4.0 single-SKU stores to the new data-driven multi-product catalog.
                    workplace.Inventory = StoreCatalog.CreateStarterInventory(workplace.StoreTypeId);
                    workplace.InventoryInitialized = true;
                }
                // Preserve a sensible valuation when loading legacy stock without cost-basis fields.
                foreach (StoreInventoryRecord item in workplace.Inventory)
                    if (item != null && item.AverageUnitCost <= 0 && item.StockUnits > 0)
                        item.AverageUnitCost = Math.Max(0, item.WholesalePrice);
                if (!workplace.InventoryInitialized)
                {
                    workplace.InventoryUnits = workplace.TargetInventoryUnits;
                    workplace.InventoryInitialized = true;
                }
                if (workplace.DailyFixedOperatingCost <= 0) workplace.DailyFixedOperatingCost = 800;
                if (workplace.DailyOperatingCostPerEmployee < 0) workplace.DailyOperatingCostPerEmployee = 500;
                if (workplace.StockLots == null) workplace.StockLots = new List<StoreStockLotRecord>();
                // Migrate existing saves once: current stock becomes a legacy lot with a category-based shelf life.
                foreach (StoreInventoryRecord item in workplace.Inventory)
                {
                    if (item == null || item.StockUnits <= 0) continue;
                    int represented = 0;
                    foreach (StoreStockLotRecord lot in workplace.StockLots)
                        if (lot != null && lot.ProductId == item.ProductId && lot.RemainingUnits > 0) represented += lot.RemainingUnits;
                    if (represented < item.StockUnits)
                    {
                        int day = Math.Max(1, LastProcessedDay);
                        workplace.StockLots.Add(new StoreStockLotRecord { LotId = "legacy-" + workplace.BuildingId + "-" + item.ProductId,
                            ProductId = item.ProductId, ReceivedDay = day, ExpiryDay = day + GetShelfLifeDays(item.CategoryId),
                            RemainingUnits = item.StockUnits - represented, UnitCost = Math.Max(0, item.AverageUnitCost), Status = "Мигрирован из старого сохранения" });
                    }
                }
            }
            workplaces[workplace.BuildingId] = workplace;
            if (workplace.IsRetailBusiness)
            {
                WarehouseStorageSystem warehouseSystem = GetComponent<WarehouseStorageSystem>();
                if (warehouseSystem != null) warehouseSystem.RegisterStoreWorkplace(workplace);
            }
        }

        public bool TryFundWorkplace(string buildingId, long amount)
        {
            WorkplaceRecord workplace;
            if (amount < 0 || !workplaces.TryGetValue(buildingId, out workplace)) return false;
            checked { workplace.OperatingBalance += amount; }
            return true;
        }

        public bool TryOrderStock(string buildingId, string productId, int units)
        {
            WorkplaceRecord workplace;
            if (units <= 0 || !workplaces.TryGetValue(buildingId, out workplace) || !workplace.IsOpen || workplace.Inventory == null) return false;
            StoreInventoryRecord item = workplace.Inventory.Find(x => x != null && x.ProductId == productId);
            if (item == null || item.WholesalePrice <= 0 || units > 1000) return false;
            long grossCost = checked(item.WholesalePrice * units);
            long cost = grossCost;
            int orderedDay = clock != null ? clock.Day : Math.Max(1, LastProcessedDay);
            if (workplace.PendingDeliveries == null) workplace.PendingDeliveries = new List<StoreDeliveryRecord>();

            // Prefer a physical shipment from a stocked regional warehouse. If no suitable
            // warehouse/route/vehicle exists, preserve the legacy supplier path without a partial reservation.
            WarehouseStorageSystem warehouseSystem = GetComponent<WarehouseStorageSystem>();
            StoreDeliveryRecord physicalOrder = new StoreDeliveryRecord
            {
                Id = StableId.NewId().ToString(), ProductId = item.ProductId, ProductDisplayName = item.DisplayName,
                SupplierId = "internal-warehouse", SupplierDisplayName = "Распределительный склад",
                Units = units, TotalCost = cost, OrderedDay = orderedDay, ArrivalDay = int.MaxValue, IsDelivered = false
            };
            string physicalError;
            if (workplace.OperatingBalance >= grossCost && warehouseSystem != null && warehouseSystem.TryDispatchStoreOrder(workplace, physicalOrder, orderedDay, out physicalError))
            {
                workplace.PendingDeliveries.Add(physicalOrder);
                workplace.OperatingBalance -= cost;
                workplace.LastDailyOperatingCost = checked(workplace.LastDailyOperatingCost + cost);
                workplace.CumulativeOperatingCosts = checked(workplace.CumulativeOperatingCosts + cost);
                TotalBusinessCosts = checked(TotalBusinessCosts + cost);
                RecordFinancial(orderedDay, "Закупка товаров", workplace.DisplayName + " · " + item.DisplayName + " · " + units + " ед.", cost, false, "stock-manual-" + physicalOrder.Id);
                Action<string> physicalHandler = OnBusinessMessage;
                if (physicalHandler != null) physicalHandler(workplace.DisplayName + ": заказ " + units + " ед. товара «" + item.DisplayName + "» отправлен со склада физическим рейсом. Оплачено " + cost.ToString("N0") + " ₽.");
                return true;
            }

            int leadDays = GetSupplierLeadDays(workplace.StoreTypeId);
            string supplierId = GetSupplierId(workplace.StoreTypeId);
            string supplierName = GetSupplierName(workplace.StoreTypeId);
            long creditApplied = Math.Min(Math.Max(0, workplace.SupplierCreditBalance), grossCost);
            cost = grossCost - creditApplied;
            if (workplace.OperatingBalance < cost) return false;
            workplace.SupplierCreditBalance -= creditApplied;
            workplace.OperatingBalance -= cost;
            workplace.PendingDeliveries.Add(new StoreDeliveryRecord
            {
                Id = StableId.NewId().ToString(), ProductId = item.ProductId, ProductDisplayName = item.DisplayName,
                SupplierId = supplierId, SupplierDisplayName = supplierName, Units = units, TotalCost = cost,
                SupplierCreditApplied = creditApplied, OrderedDay = orderedDay, ArrivalDay = orderedDay + leadDays, IsDelivered = false
            });
            workplace.LastDailyOperatingCost = checked(workplace.LastDailyOperatingCost + cost);
            workplace.CumulativeOperatingCosts = checked(workplace.CumulativeOperatingCosts + cost);
            TotalBusinessCosts = checked(TotalBusinessCosts + cost);
            RecordFinancial(orderedDay, "Закупка товаров", workplace.DisplayName + " · " + item.DisplayName + " · " + units + " ед.", cost, false, "stock-manual-" + workplace.PendingDeliveries[workplace.PendingDeliveries.Count - 1].Id);
            Action<string> handler = OnBusinessMessage;
            if (handler != null) handler(workplace.DisplayName + ": заказ " + units + " ед. товара «" + item.DisplayName + "» у поставщика «" + supplierName + "». Доставка через " + leadDays + " дн., зачёт " + creditApplied.ToString("N0") + " ₽, оплачено " + cost.ToString("N0") + " ₽.");
            return true;
        }

        /// <summary>Refunds the purchase value of a physically cancelled store order exactly once.</summary>
        public bool RefundCancelledStoreOrder(WorkplaceRecord workplace, StoreDeliveryRecord delivery)
        {
            if (workplace == null || delivery == null || !delivery.IsCancelled || delivery.IsRefunded || delivery.TotalCost <= 0)
                return false;
            workplace.OperatingBalance = checked(workplace.OperatingBalance + delivery.TotalCost);
            workplace.LastDailyOperatingCost = Math.Max(0, workplace.LastDailyOperatingCost - delivery.TotalCost);
            workplace.CumulativeOperatingCosts = Math.Max(0, workplace.CumulativeOperatingCosts - delivery.TotalCost);
            TotalBusinessCosts = Math.Max(0, TotalBusinessCosts - delivery.TotalCost);
            delivery.IsRefunded = true;
            RecordFinancial(clock != null ? clock.Day : LastProcessedDay, "Возврат закупки", workplace.DisplayName + " · отмена заказа " + delivery.Id, delivery.TotalCost, true, "stock-refund-" + delivery.Id);
            Action<string> handler = OnBusinessMessage;
            if (handler != null) handler(workplace.DisplayName + ": заказ " + delivery.Id + " отменён, стоимость товара " + delivery.TotalCost.ToString("N0") + " ₽ возвращена на баланс предприятия.");
            return true;
        }

        /// <summary>Records the physically counted quantity for an active warehouse-to-store shipment.</summary>
        public bool TryReportStoreShipmentReceivedUnits(string buildingId, string orderId, int receivedUnits, out string error)
        {
            error = string.Empty;
            WorkplaceRecord workplace;
            if (string.IsNullOrEmpty(buildingId) || !workplaces.TryGetValue(buildingId, out workplace) || workplace.PendingDeliveries == null)
            { error = "Магазин не найден."; return false; }
            StoreDeliveryRecord delivery = workplace.PendingDeliveries.Find(x => x != null && x.Id == orderId);
            if (delivery == null || string.IsNullOrEmpty(delivery.WarehouseId))
            { error = "Физический магазинный заказ не найден."; return false; }
            if (delivery.IsCancelled || delivery.IsAccepted || delivery.IsDelivered || delivery.LogisticsStatus != "Ожидает приёмки")
            { error = "Количество можно зафиксировать только для рейса, который ещё не принят магазином."; return false; }
            if (receivedUnits < 0 || receivedUnits > delivery.Units)
            { error = "Принятое количество должно быть от 0 до количества в накладной (" + delivery.Units + ")."; return false; }
            if (delivery.HasReportedReceivedUnits && delivery.ReportedReceivedUnits != receivedUnits)
            { error = "Фактическое количество уже зафиксировано. Изменение требует отдельной корректировки приёмки."; return false; }
            if (delivery.HasReportedDamagedUnits && delivery.ReportedDamagedUnits > receivedUnits)
            { error = "Повреждённых единиц не может быть больше фактически полученного количества."; return false; }
            delivery.ReportedReceivedUnits = receivedUnits;
            delivery.HasReportedReceivedUnits = true;
            return true;
        }

        /// <summary>Records damaged units that arrived physically but must not enter saleable stock.</summary>
        public bool TryReportStoreShipmentDamage(string buildingId, string orderId, int damagedUnits, string damageReason, out string error)
        {
            error = string.Empty;
            WorkplaceRecord workplace;
            if (string.IsNullOrEmpty(buildingId) || !workplaces.TryGetValue(buildingId, out workplace) || workplace.PendingDeliveries == null)
            { error = "Магазин не найден."; return false; }
            StoreDeliveryRecord delivery = workplace.PendingDeliveries.Find(x => x != null && x.Id == orderId);
            if (delivery == null || string.IsNullOrEmpty(delivery.WarehouseId))
            { error = "Физический магазинный заказ не найден."; return false; }
            if (delivery.IsCancelled || delivery.IsAccepted || delivery.IsDelivered || delivery.LogisticsStatus != "Ожидает приёмки")
            { error = "Повреждения можно зафиксировать только до приёмки физического рейса."; return false; }
            int maximumReceived = delivery.HasReportedReceivedUnits
                ? delivery.ReportedReceivedUnits
                : Math.Max(0, delivery.Units);
            if (damagedUnits < 0 || damagedUnits > maximumReceived)
            { error = "Количество повреждённых единиц должно быть от 0 до фактически полученного количества (" + maximumReceived + ")."; return false; }
            string normalizedReason = (damageReason ?? string.Empty).Trim();
            if (damagedUnits > 0 && normalizedReason.Length == 0)
            { error = "Укажите причину повреждения груза."; return false; }
            if (normalizedReason.Length > 160)
            { error = "Причина повреждения не должна превышать 160 символов."; return false; }
            if (delivery.HasReportedDamagedUnits &&
                (delivery.ReportedDamagedUnits != damagedUnits || !string.Equals(delivery.DamageReason ?? string.Empty, normalizedReason, StringComparison.Ordinal)))
            { error = "Повреждение уже зафиксировано. Для изменения требуется отдельная корректировка приёмки."; return false; }
            delivery.ReportedDamagedUnits = damagedUnits;
            delivery.HasReportedDamagedUnits = true;
            delivery.DamageReason = normalizedReason;
            return true;
        }

        /// <summary>Records the write-off of damaged units without debiting the already-paid purchase cost again.</summary>
        public bool RecordStoreShipmentDamageWriteOff(WorkplaceRecord workplace, StoreDeliveryRecord delivery, int day)
        {
            if (workplace == null || delivery == null || !delivery.IsAccepted || delivery.DamagedUnits <= 0 || delivery.IsDamageWrittenOff)
                return false;
            if (delivery.DamageWriteOffValue < 0) return false;
            if (workplace.DamageLossLastProcessedDay != Math.Max(0, day))
            {
                workplace.LastDailyDamageLoss = 0;
                workplace.DamageLossLastProcessedDay = Math.Max(0, day);
            }
            workplace.LastDailyDamageLoss = checked(workplace.LastDailyDamageLoss + delivery.DamageWriteOffValue);
            workplace.CumulativeDamageLoss = checked(workplace.CumulativeDamageLoss + delivery.DamageWriteOffValue);
            delivery.DamageWriteOffUnits = delivery.DamagedUnits;
            delivery.GoodsCostWrittenOff = Math.Max(0, delivery.GoodsCostWrittenOff);
            delivery.IsDamageWrittenOff = true;
            Action<string> handler = OnBusinessMessage;
            if (handler != null)
                handler(workplace.DisplayName + ": повреждённый груз по заказу " + delivery.Id + " — " + delivery.DamagedUnits + " ед.; списано по себестоимости " + delivery.DamageWriteOffValue.ToString("N0") + " ₽. Причина: " + (string.IsNullOrEmpty(delivery.DamageReason) ? "не указана" : delivery.DamageReason) + ".");
            return true;
        }

        /// <summary>Posts the freight expense to the business ledger exactly once; the cash debit is performed by the receipt transaction.</summary>
        public bool RecordStoreShipmentFreightCharge(WorkplaceRecord workplace, StoreDeliveryRecord delivery, int day)
        {
            if (workplace == null || delivery == null || !delivery.IsAccepted || !delivery.IsFreightSettled ||
                delivery.FreightCostCharged <= 0 || delivery.IsFreightLedgerPosted)
                return false;
            int processedDay = Math.Max(0, day);
            if (workplace.FreightCostLastProcessedDay != processedDay)
            {
                workplace.LastDailyFreightCost = 0;
                workplace.FreightCostLastProcessedDay = processedDay;
            }
            workplace.LastDailyFreightCost = checked(workplace.LastDailyFreightCost + delivery.FreightCostCharged);
            workplace.CumulativeFreightCost = checked(workplace.CumulativeFreightCost + delivery.FreightCostCharged);
            TotalBusinessCosts = checked(TotalBusinessCosts + delivery.FreightCostCharged);
            delivery.IsFreightLedgerPosted = true;
            Action<string> handler = OnBusinessMessage;
            if (handler != null)
                handler(workplace.DisplayName + ": транспортные расходы по поставке " + delivery.Id + " — " + delivery.FreightCostCharged.ToString("N0") + " ₽; учтены один раз.");
            return true;
        }

        /// <summary>Refunds the purchase value of missing units exactly once after physical receipt.</summary>
        public bool RefundStoreShipmentShortage(WorkplaceRecord workplace, StoreDeliveryRecord delivery)
        {
            if (workplace == null || delivery == null || !delivery.IsAccepted || delivery.ShortUnits <= 0 || delivery.IsShortageRefunded || delivery.Units <= 0)
                return false;
            long refund = checked((long)((decimal)delivery.TotalCost * delivery.ShortUnits / delivery.Units));
            delivery.ShortageRefundValue = Math.Max(0, refund);
            if (refund <= 0) { delivery.IsShortageRefunded = true; return true; }
            workplace.OperatingBalance = checked(workplace.OperatingBalance + refund);
            workplace.LastDailyOperatingCost = Math.Max(0, workplace.LastDailyOperatingCost - refund);
            workplace.CumulativeOperatingCosts = Math.Max(0, workplace.CumulativeOperatingCosts - refund);
            TotalBusinessCosts = Math.Max(0, TotalBusinessCosts - refund);
            delivery.IsShortageRefunded = true;
            Action<string> handler = OnBusinessMessage;
            if (handler != null) handler(workplace.DisplayName + ": недостача по заказу " + delivery.Id + " — " + delivery.ShortUnits + " ед.; возвращено " + refund.ToString("N0") + " ₽.");
            return true;
        }

        public bool TryCancelStoreOrder(string buildingId, string orderId, out string error)
        {
            error = string.Empty;
            WorkplaceRecord workplace;
            if (string.IsNullOrEmpty(buildingId) || !workplaces.TryGetValue(buildingId, out workplace) || workplace.PendingDeliveries == null)
            { error = "Магазин не найден."; return false; }
            StoreDeliveryRecord delivery = workplace.PendingDeliveries.Find(x => x != null && x.Id == orderId);
            if (delivery == null) { error = "Заказ не найден."; return false; }
            WarehouseStorageSystem warehouseSystem = GetComponent<WarehouseStorageSystem>();
            if (warehouseSystem == null) { error = "Складская логистика не подключена."; return false; }
            int day = clock != null ? clock.Day : Math.Max(1, LastProcessedDay);
            if (!warehouseSystem.TryCancelStoreOrder(workplace, delivery, day, out error)) return false;
            if (delivery.IsRefunded == false && delivery.LogisticsStatus == "Отменён до погрузки")
                RefundCancelledStoreOrder(workplace, delivery);
            return true;
        }

        private void HandleDayChanged(int day)
        {
            if (day <= LastProcessedDay) return;
            ProcessBusinessDay(day);
            // The prototype calendar uses 30-day months. Payroll is accrued once at month close.
            if (day > 1 && (day - 1) % 30 == 0)
                AccrueMonthlyWages((day - 1) / 30);
            TryPayOutstanding();
            LastProcessedDay = day;
            Action settled = OnEconomyDaySettled;
            if (settled != null) settled();
        }

        private void ProcessBusinessDay(int day)
        {
            List<WorkplaceRecord> orderedWorkplaces = new List<WorkplaceRecord>(workplaces.Values);
            orderedWorkplaces.Sort((a, b) => string.CompareOrdinal(a.BuildingId, b.BuildingId));
            int openStoreCount = 0;
            for (int i = 0; i < orderedWorkplaces.Count; i++)
                if (orderedWorkplaces[i] != null && orderedWorkplaces[i].IsOpen) openStoreCount++;
            int openStoreIndex = -1;
            foreach (WorkplaceRecord workplace in orderedWorkplaces)
            {
                if (workplace == null || string.IsNullOrEmpty(workplace.BuildingId)) continue;
                if (workplace.IsOpen) openStoreIndex++;
                string key = day + ":" + workplace.BuildingId;
                if (!businessDayKeys.Add(key)) continue;
                if (workplace.DamageLossLastProcessedDay != day)
                {
                    workplace.LastDailyDamageLoss = 0;
                    workplace.DamageLossLastProcessedDay = day;
                }
                if (workplace.FreightCostLastProcessedDay != day)
                {
                    workplace.LastDailyFreightCost = 0;
                    workplace.FreightCostLastProcessedDay = day;
                }

                // Offices and other non-retail employers participate in the same cash and payroll
                // ledger, but must never receive a fictitious retail assortment or stock expenses.
                if (!workplace.IsRetailBusiness)
                {
                    long rent = Math.Max(0L, workplace.DailyRentIncome);
                    workplace.OperatingBalance = checked(workplace.OperatingBalance + rent);
                    workplace.LastDailyRevenue = rent;
                    workplace.LastDailyOperatingCost = 0;
                    workplace.LastDailyUnitsSold = 0;
                    workplace.CumulativeRevenue = checked(workplace.CumulativeRevenue + rent);
                    TotalBusinessRevenue = checked(TotalBusinessRevenue + rent);
                    RecordFinancial(day, "Аренда помещений", workplace.DisplayName + " · арендный доход", rent, true,
                        "workplace-rent-" + day + "-" + workplace.BuildingId);
                    continue;
                }

                long revenue = 0, costs = 0, unitsSold = 0, storageCost = 0;
                if (workplace.Inventory == null) workplace.Inventory = StoreCatalog.CreateStarterInventory(workplace.StoreTypeId);
                workplace.LastDailyExpiryLoss = 0;
                ProcessArrivingDeliveries(workplace, day);
                ProcessExpiredLots(workplace, day);
                if (workplace.IsOpen)
                {
                    // Automatic replenishment creates supplier orders; goods are not teleported into stock.
                    foreach (StoreInventoryRecord item in workplace.Inventory)
                    {
                        if (item == null || item.WholesalePrice <= 0) continue;
                        int pendingUnits = 0;
                        if (workplace.PendingDeliveries != null)
                            foreach (StoreDeliveryRecord pending in workplace.PendingDeliveries)
                                if (pending != null && !pending.IsDelivered && !pending.IsCancelled && !pending.IsAccepted && pending.ProductId == item.ProductId) pendingUnits += pending.Units;
                        int saleableStock = Math.Max(0, item.StockUnits - Math.Max(0, item.ReservedForReturnUnits));
                        int missing = Math.Max(0, item.TargetStockUnits - saleableStock - pendingUnits);
                        long availableFunds = checked(workplace.OperatingBalance + Math.Max(0, workplace.SupplierCreditBalance));
                        int affordable = (int)Math.Min((long)missing, availableFunds / item.WholesalePrice);
                        if (affordable <= 0) continue;
                        long grossPurchaseCost = checked((long)affordable * item.WholesalePrice);
                        long supplierCreditApplied = Math.Min(Math.Max(0, workplace.SupplierCreditBalance), grossPurchaseCost);
                        long purchaseCost = grossPurchaseCost - supplierCreditApplied;
                        workplace.SupplierCreditBalance -= supplierCreditApplied;
                        workplace.OperatingBalance -= purchaseCost;
                        if (workplace.PendingDeliveries == null) workplace.PendingDeliveries = new List<StoreDeliveryRecord>();
                        int leadDays = GetSupplierLeadDays(workplace.StoreTypeId);
                        StoreDeliveryRecord automaticOrder = new StoreDeliveryRecord
                        {
                            Id = StableId.NewId().ToString(), ProductId = item.ProductId, ProductDisplayName = item.DisplayName,
                            SupplierId = GetSupplierId(workplace.StoreTypeId), SupplierDisplayName = GetSupplierName(workplace.StoreTypeId),
                            Units = affordable, TotalCost = purchaseCost, SupplierCreditApplied = supplierCreditApplied, OrderedDay = day, ArrivalDay = day + leadDays, IsDelivered = false
                        };

                        // Automatic replenishment uses the same physical warehouse route as a manual order
                        // whenever local warehouse stock, a road path and a suitable vehicle are available.
                        // If dispatch cannot be committed, the untouched order falls back to the supplier calendar.
                        WarehouseStorageSystem warehouseSystem = GetComponent<WarehouseStorageSystem>();
                        string dispatchError;
                        bool dispatchedPhysically = supplierCreditApplied == 0 && warehouseSystem != null &&
                            warehouseSystem.TryDispatchStoreOrder(workplace, automaticOrder, day, out dispatchError);
                        if (dispatchedPhysically)
                        {
                            automaticOrder.SupplierId = "internal-warehouse";
                            automaticOrder.SupplierDisplayName = "Распределительный склад";
                            automaticOrder.ArrivalDay = int.MaxValue;
                        }
                        workplace.PendingDeliveries.Add(automaticOrder);
                        costs = checked(costs + purchaseCost);
                        RecordFinancial(day, "Автопополнение запасов", workplace.DisplayName + " · " + item.DisplayName + " · " + affordable + " ед.", purchaseCost, false, "stock-auto-" + automaticOrder.Id);
                    }

                    // Daily carrying cost is charged against average on-hand stock, by product category.
                    foreach (StoreInventoryRecord item in workplace.Inventory)
                    {
                        if (item == null || item.StockUnits <= 0) continue;
                        long due = checked((long)item.StockUnits * GetDailyStorageRate(item.CategoryId));
                        long paidStorage = Math.Min(workplace.OperatingBalance, due);
                        workplace.OperatingBalance -= paidStorage;
                        storageCost = checked(storageCost + paidStorage);
                    }
                    workplace.LastDailyInventoryStorageCost = storageCost;
                    workplace.CumulativeInventoryStorageCost = checked(workplace.CumulativeInventoryStorageCost + storageCost);
                    costs = checked(costs + storageCost);
                    RecordFinancial(day, "Хранение запасов", workplace.DisplayName + " · ежедневное хранение", storageCost, false, "storage-" + day + "-" + workplace.BuildingId);

                    int staff = Math.Max(0, workplace.StaffCount);
                    long overhead = checked(workplace.DailyFixedOperatingCost + workplace.DailyOperatingCostPerEmployee * staff);
                    long overheadPaid = Math.Min(workplace.OperatingBalance, overhead);
                    workplace.OperatingBalance -= overheadPaid;
                    costs = checked(costs + overheadPaid);
                    RecordFinancial(day, "Содержание предприятия", workplace.DisplayName + " · ежедневные операционные расходы", overheadPaid, false, "overhead-" + day + "-" + workplace.BuildingId);

                    foreach (StoreInventoryRecord item in workplace.Inventory)
                        if (item != null) { item.LastDailyUnitsSold = 0; item.LastDailyRevenue = 0; }

                    foreach (CitizenRecord citizen in citizens.Values)
                    {
                        if (citizen == null || citizen.IsDeceased || string.IsNullOrEmpty(citizen.Id)) continue;
                        string shopperKey = day + ":" + citizen.Id;
                        if (shopperDayKeys.Contains(shopperKey) || openStoreCount <= 0) continue;
                        if (StableDemandBucket(citizen.Id, day, "store-choice") % openStoreCount != openStoreIndex) continue;
                        shopperDayKeys.Add(shopperKey);
                        if (StableDemandBucket(citizen.Id, day, workplace.StoreTypeId) != 0 || workplace.Inventory.Count == 0) continue;

                        // Select one SKU deterministically; residents buy no more than one item per day in this prototype.
                        int productIndex = StableDemandBucket(citizen.Id, day, workplace.StoreTypeId + ":sku") % workplace.Inventory.Count;
                        StoreInventoryRecord item = workplace.Inventory[productIndex];
                        if (item == null || item.StockUnits - Math.Max(0, item.ReservedForReturnUnits) <= 0 || item.RetailPrice <= 0 || citizen.WalletBalance < item.RetailPrice) continue;
                        citizen.WalletBalance -= item.RetailPrice;
                        workplace.OperatingBalance = checked(workplace.OperatingBalance + item.RetailPrice);
                        item.StockUnits--;
                        ConsumeFefoLot(workplace, item.ProductId, day);
                        item.TotalUnitsSold = checked(item.TotalUnitsSold + 1);
                        item.LastDailyUnitsSold = checked(item.LastDailyUnitsSold + 1);
                        item.LastDailyRevenue = checked(item.LastDailyRevenue + item.RetailPrice);
                        unitsSold++;
                        revenue = checked(revenue + item.RetailPrice);
                        if (citizen.EventHistory == null) citizen.EventHistory = new List<string>();
                        citizen.EventHistory.Add("shop-purchase:" + shopperKey + ":" + workplace.StoreTypeId + ":" + item.ProductId + ":" + item.RetailPrice);
                    }
                }

                workplace.LastDailyRevenue = revenue;
                workplace.LastDailyOperatingCost = costs;
                workplace.LastDailyUnitsSold = unitsSold;
                workplace.TotalUnitsSold = checked(workplace.TotalUnitsSold + unitsSold);
                workplace.CumulativeRevenue = checked(workplace.CumulativeRevenue + revenue);
                workplace.CumulativeOperatingCosts = checked(workplace.CumulativeOperatingCosts + costs);
                TotalBusinessRevenue = checked(TotalBusinessRevenue + revenue);
                RecordFinancial(day, "Продажи", workplace.DisplayName + " · розничная выручка · " + unitsSold + " ед.", revenue, true, "sales-" + day + "-" + workplace.BuildingId);
                TotalBusinessCosts = checked(TotalBusinessCosts + costs);

                Action<string> handler = OnBusinessMessage;
                if (handler != null)
                    handler(workplace.DisplayName + ": продано " + unitsSold + " ед. из ассортимента (" +
                        (workplace.Inventory != null ? workplace.Inventory.Count : 0) + " SKU), выручка " +
                        revenue.ToString("N0") + " ₽, закупка и расходы " + costs.ToString("N0") + " ₽.");
            }
        }


        /// <summary>Executes one real retail purchase when a resident reaches a store. The daily shopper key
        /// is shared with the scheduled business-day demand pass, so a citizen cannot buy twice in one day.</summary>
        /// <summary>Advances persistent household needs once per city day. Repeated calls are idempotent.</summary>
        public void AdvanceResidentNeeds(int day)
        {
            int currentDay = Math.Max(1, day);
            foreach (CitizenRecord citizen in citizens.Values)
            {
                if (citizen == null || currentDay <= citizen.NeedsLastUpdatedDay) continue;
                int elapsed = Math.Min(30, currentDay - Math.Max(1, citizen.NeedsLastUpdatedDay));
                citizen.FoodNeed = ClampNeed(citizen.FoodNeed + elapsed * 4.0f);
                citizen.ClothingNeed = ClampNeed(citizen.ClothingNeed + elapsed * 0.7f);
                citizen.HomeGoodsNeed = ClampNeed(citizen.HomeGoodsNeed + elapsed * 0.9f);
                citizen.LeisureNeed = ClampNeed(citizen.LeisureNeed + elapsed * 1.5f);
                citizen.NeedsLastUpdatedDay = currentDay;
            }
        }

        private static float ClampNeed(float value) { return Math.Max(0f, Math.Min(100f, value)); }

        private static float GetNeedForCategory(CitizenRecord citizen, string categoryId)
        {
            if (citizen == null || string.IsNullOrEmpty(categoryId)) return 0f;
            switch (categoryId.ToLowerInvariant())
            {
                case "food": return citizen.FoodNeed;
                case "clothing": return citizen.ClothingNeed;
                case "furniture": case "household": case "cosmetics": return citizen.HomeGoodsNeed;
                case "electronics": case "appliances": return citizen.HomeGoodsNeed * 0.8f;
                case "books": case "leisure": return citizen.LeisureNeed;
                default: return 0f;
            }
        }

        private static void SatisfyNeed(CitizenRecord citizen, string categoryId)
        {
            if (citizen == null || string.IsNullOrEmpty(categoryId)) return;
            switch (categoryId.ToLowerInvariant())
            {
                case "food": citizen.FoodNeed = ClampNeed(citizen.FoodNeed - 24f); break;
                case "clothing": citizen.ClothingNeed = ClampNeed(citizen.ClothingNeed - 28f); break;
                case "furniture": case "household": case "cosmetics": citizen.HomeGoodsNeed = ClampNeed(citizen.HomeGoodsNeed - 22f); break;
                case "electronics": case "appliances": citizen.HomeGoodsNeed = ClampNeed(citizen.HomeGoodsNeed - 18f); break;
                case "books": case "leisure": citizen.LeisureNeed = ClampNeed(citizen.LeisureNeed - 20f); break;
            }
        }

        public bool TryResidentRetailPurchase(string citizenId, string buildingId, int day, out string productName, out long price)
        {
            productName = null;
            price = 0;
            CitizenRecord citizen;
            WorkplaceRecord workplace;
            if (string.IsNullOrEmpty(citizenId) || string.IsNullOrEmpty(buildingId) ||
                !citizens.TryGetValue(citizenId, out citizen) || citizen == null ||
                !workplaces.TryGetValue(buildingId, out workplace) || workplace == null ||
                !workplace.IsRetailBusiness || !workplace.IsOpen || workplace.Inventory == null || workplace.Inventory.Count == 0)
                return false;

            string shopperKey = Math.Max(0, day) + ":" + citizen.Id;
            if (shopperDayKeys.Contains(shopperKey)) return false;

            // Build the basket before mutating any balances or stock. This keeps a failed visit atomic.
            int basketTarget = 1 + StableDemandBucket(citizen.Id, Math.Max(0, day), workplace.StoreTypeId + ":basket-size") % 3;
            int start = StableDemandBucket(citizen.Id, Math.Max(0, day), workplace.StoreTypeId + ":visitor-sku") % workplace.Inventory.Count;
            List<StoreInventoryRecord> candidates = new List<StoreInventoryRecord>();
            for (int offset = 0; offset < workplace.Inventory.Count; offset++)
            {
                StoreInventoryRecord candidate = workplace.Inventory[(start + offset) % workplace.Inventory.Count];
                if (candidate != null && !string.IsNullOrEmpty(candidate.ProductId) && candidate.RetailPrice > 0 &&
                    candidate.StockUnits - Math.Max(0, candidate.ReservedForReturnUnits) > 0 &&
                    candidate.RetailPrice <= citizen.WalletBalance)
                    candidates.Add(candidate);
            }
            // Sort by household need first; stable SKU order breaks ties without random economic outcomes.
            candidates.Sort(delegate(StoreInventoryRecord a, StoreInventoryRecord b)
            {
                int needOrder = GetNeedForCategory(citizen, b.CategoryId).CompareTo(GetNeedForCategory(citizen, a.CategoryId));
                if (needOrder != 0) return needOrder;
                return string.CompareOrdinal(a.ProductId, b.ProductId);
            });
            List<StoreInventoryRecord> basket = new List<StoreInventoryRecord>();
            HashSet<string> selectedProductIds = new HashSet<string>();
            long basketTotal = 0;
            for (int i = 0; i < candidates.Count && basket.Count < basketTarget; i++)
            {
                StoreInventoryRecord candidate = candidates[i];
                if (!selectedProductIds.Add(candidate.ProductId) || basketTotal > citizen.WalletBalance - candidate.RetailPrice) continue;
                basket.Add(candidate);
                basketTotal = checked(basketTotal + candidate.RetailPrice);
            }
            if (basket.Count == 0) return false;
            shopperDayKeys.Add(shopperKey);

            // Commit all basket lines together: one wallet debit, physical stock decrements and FEFO consumption.
            citizen.WalletBalance = checked(citizen.WalletBalance - basketTotal);
            workplace.OperatingBalance = checked(workplace.OperatingBalance + basketTotal);
            List<string> basketNames = new List<string>();
            foreach (StoreInventoryRecord item in basket)
            {
                item.StockUnits--;
                ConsumeFefoLot(workplace, item.ProductId, Math.Max(0, day));
                item.TotalUnitsSold = checked(item.TotalUnitsSold + 1);
                item.LastDailyUnitsSold = checked(item.LastDailyUnitsSold + 1);
                item.LastDailyRevenue = checked(item.LastDailyRevenue + item.RetailPrice);
                SatisfyNeed(citizen, item.CategoryId);
                basketNames.Add(item.DisplayName);
            }
            workplace.TotalUnitsSold = checked(workplace.TotalUnitsSold + basket.Count);
            workplace.LastDailyUnitsSold = checked(workplace.LastDailyUnitsSold + basket.Count);
            workplace.LastDailyRevenue = checked(workplace.LastDailyRevenue + basketTotal);
            workplace.CumulativeRevenue = checked(workplace.CumulativeRevenue + basketTotal);
            TotalBusinessRevenue = checked(TotalBusinessRevenue + basketTotal);

            string transactionId = "resident-sale-" + shopperKey + "-" + workplace.BuildingId;
            RecordFinancial(day, "Розничные продажи", workplace.DisplayName + " · " + citizen.FirstName + " " + citizen.LastName +
                " · корзина " + basket.Count + " поз. (" + string.Join(", ", basketNames.ToArray()) + ")", basketTotal, true, transactionId);
            if (citizen.EventHistory == null) citizen.EventHistory = new List<string>();
            citizen.EventHistory.Add("shop-purchase-basket:" + shopperKey + ":" + workplace.StoreTypeId + ":" + basket.Count + ":" + basketTotal);
            foreach (StoreInventoryRecord item in basket)
                citizen.EventHistory.Add("shop-purchase-item:" + shopperKey + ":" + item.ProductId + ":" + item.RetailPrice);
            while (citizen.EventHistory.Count > 120) citizen.EventHistory.RemoveAt(0);
            productName = string.Join(", ", basketNames.ToArray());
            price = basketTotal;
            Action<string> handler = OnBusinessMessage;
            if (handler != null) handler(citizen.FirstName + " " + citizen.LastName + " купил(а) " + basket.Count +
                " поз. в магазине «" + workplace.DisplayName + "»: " + productName + " на " + basketTotal.ToString("N0") + " ₽.");
            return true;
        }

        public static int CountPendingDeliveries(WorkplaceRecord workplace)
        {
            if (workplace == null || workplace.PendingDeliveries == null) return 0;
            int count = 0;
            foreach (StoreDeliveryRecord delivery in workplace.PendingDeliveries)
                if (delivery != null && !delivery.IsDelivered && !delivery.IsCancelled && !delivery.IsAccepted) count++;
            return count;
        }

        private static long GetDailyStorageRate(string categoryId)
        {
            switch (categoryId)
            {
                case "food": return 2;
                case "electronics": case "appliances": return 3;
                case "furniture": return 2;
                case "clothing": case "books": case "cosmetics": return 1;
                default: return 1;
            }
        }

        private static int GetSupplierLeadDays(string storeTypeId)
        {
            switch (storeTypeId)
            {
                case "grocery": case "cosmetics": case "books": return 1;
                case "clothing": case "electronics": return 2;
                case "furniture": return 3;
                default: return 2;
            }
        }

        private static string GetSupplierId(string storeTypeId)
        {
            switch (storeTypeId)
            {
                case "grocery": return "food_wholesaler";
                case "electronics": return "electronics_distributor";
                case "clothing": return "clothing_wholesaler";
                case "furniture": return "furniture_warehouse";
                case "cosmetics": return "household_goods_supplier";
                case "books": return "book_distributor";
                default: return "general_supplier";
            }
        }

        private static string GetSupplierName(string storeTypeId)
        {
            switch (storeTypeId)
            {
                case "grocery": return "Продуктовая оптовая база";
                case "electronics": return "Дистрибьютор электроники";
                case "clothing": return "Оптовый склад одежды";
                case "furniture": return "Мебельный распределительный центр";
                case "cosmetics": return "Поставщик косметики и бытовой химии";
                case "books": return "Книжный дистрибьютор";
                default: return "Универсальный поставщик";
            }
        }

        private static int GetShelfLifeDays(string categoryId)
        {
            switch (categoryId)
            {
                case "food": return 14;
                case "cosmetics": return 540;
                case "household": return 900;
                case "electronics": return 3650;
                case "clothing": return 1825;
                case "books": return 3650;
                default: return 365;
            }
        }

        private static void ConsumeFefoLot(WorkplaceRecord workplace, string productId, int day)
        {
            if (workplace.StockLots == null) return;
            StoreStockLotRecord lot = null;
            foreach (StoreStockLotRecord candidate in workplace.StockLots)
                if (candidate != null && candidate.ProductId == productId && candidate.RemainingUnits > 0 && !candidate.IsQuarantined && Math.Max(0, candidate.RemainingUnits - Math.Max(0, candidate.QuarantinedUnits) - Math.Max(0, candidate.ReservedForReturnUnits)) > 0 && candidate.ExpiryDay >= day &&
                    (lot == null || candidate.ExpiryDay < lot.ExpiryDay || (candidate.ExpiryDay == lot.ExpiryDay && candidate.ReceivedDay < lot.ReceivedDay))) lot = candidate;
            if (lot != null) lot.RemainingUnits--;
        }

        private static void ProcessExpiredLots(WorkplaceRecord workplace, int day)
        {
            if (workplace.StockLots == null || workplace.Inventory == null) return;
            foreach (StoreStockLotRecord lot in workplace.StockLots)
            {
                if (lot == null || lot.RemainingUnits <= 0 || lot.IsQuarantined || lot.ExpiryDay >= day) continue;
                int quarantined = Math.Max(0, Math.Min(lot.RemainingUnits, lot.QuarantinedUnits));
                StoreInventoryRecord item = workplace.Inventory.Find(x => x != null && x.ProductId == lot.ProductId);
                int expired = Math.Max(0, lot.RemainingUnits - quarantined - Math.Max(0, lot.ReservedForReturnUnits));
                if (item != null)
                {
                    int removable = Math.Min(expired, Math.Max(0, item.StockUnits - Math.Max(0, item.ReservedForReturnUnits)));
                    item.StockUnits -= removable;
                    long loss = checked((long)removable * Math.Max(0, lot.UnitCost));
                    workplace.LastDailyExpiryLoss = checked(workplace.LastDailyExpiryLoss + loss);
                    workplace.CumulativeExpiryLoss = checked(workplace.CumulativeExpiryLoss + loss);
                    lot.RemainingUnits -= removable;
                    lot.QuarantinedUnits = Math.Min(Math.Max(0, lot.QuarantinedUnits), lot.RemainingUnits);
                }
                if (lot.RemainingUnits <= 0) lot.Status = "Просрочена и списана";
                else if (lot.RemainingUnits <= lot.ReservedForReturnUnits) lot.Status = "Просрочена · зарезервирована под возврат";
            }
        }

        private static void ProcessArrivingDeliveries(WorkplaceRecord workplace, int day)
        {
            if (workplace.PendingDeliveries == null || workplace.Inventory == null) return;
            foreach (StoreDeliveryRecord delivery in workplace.PendingDeliveries)
            {
                // Physical warehouse shipments must never be received by the legacy calendar path.
                // Their inventory is added only by the physical shipment acceptance handler.
                if (delivery == null || delivery.IsDelivered || delivery.IsCancelled || delivery.IsAccepted ||
                    !string.IsNullOrEmpty(delivery.WarehouseId) || !string.IsNullOrEmpty(delivery.ReservationId) ||
                    delivery.ArrivalDay > day) continue;
                StoreInventoryRecord item = workplace.Inventory.Find(x => x != null && x.ProductId == delivery.ProductId);
                if (item == null) continue;
                int arrivingUnits = Math.Max(0, delivery.Units);
                long arrivingUnitCost = arrivingUnits > 0 ? delivery.TotalCost / arrivingUnits : 0;
                long oldValue = checked((long)Math.Max(0, item.StockUnits) * Math.Max(0, item.AverageUnitCost));
                long newValue = checked((long)arrivingUnits * Math.Max(0, arrivingUnitCost));
                int newUnits = checked(item.StockUnits + arrivingUnits);
                item.AverageUnitCost = newUnits > 0 ? checked(oldValue + newValue) / newUnits : 0;
                item.StockUnits = newUnits;
                delivery.IsDelivered = true;
            }
        }

        private static int StableDemandBucket(string citizenId, int day, string buildingId)
        {
            unchecked
            {
                uint hash = 2166136261;
                string value = citizenId + "|" + day + "|" + buildingId;
                for (int i = 0; i < value.Length; i++)
                {
                    hash ^= value[i];
                    hash *= 16777619;
                }
                return (int)(hash & 0x7fffffff);
            }
        }

        private void AccrueMonthlyWages(int completedMonth)
        {
            foreach (CitizenRecord citizen in citizens.Values)
            {
                if (citizen == null || !citizen.IsEmployed || citizen.MonthlySalary <= 0) continue;
                WorkplaceRecord workplace;
                if (!workplaces.TryGetValue(citizen.WorkplaceBuildingId, out workplace) || !workplace.IsOpen) continue;
                string key = completedMonth + ":" + workplace.BuildingId + ":" + citizen.Id;
                if (!accruedKeys.Add(key)) continue;
                citizen.AccruedWages = checked(citizen.AccruedWages + citizen.MonthlySalary);
                if (string.IsNullOrEmpty(citizen.AccruedWageEmployerBuildingId))
                    citizen.AccruedWageEmployerBuildingId = workplace.BuildingId;
                if (citizen.EventHistory == null) citizen.EventHistory = new List<string>();
                citizen.EventHistory.Add("payroll-accrued:" + key + ":" + citizen.MonthlySalary);
            }
        }

        private void TryPayOutstanding()
        {
            foreach (CitizenRecord citizen in citizens.Values)
            {
                if (citizen == null || citizen.AccruedWages <= 0 || string.IsNullOrEmpty(citizen.AccruedWageEmployerBuildingId)) continue;
                WorkplaceRecord workplace;
                if (!workplaces.TryGetValue(citizen.AccruedWageEmployerBuildingId, out workplace) || !workplace.IsOpen) continue;
                long amount = citizen.AccruedWages;
                if (workplace.OperatingBalance < amount) continue;
                string paymentKey = (clock != null ? clock.Day : LastProcessedDay) + ":" + workplace.BuildingId + ":" + citizen.Id + ":" + amount;
                if (paidKeys.Contains(paymentKey)) continue;
                workplace.OperatingBalance -= amount;
                citizen.WalletBalance = checked(citizen.WalletBalance + amount);
                citizen.AccruedWages = 0;
                citizen.AccruedWageEmployerBuildingId = null;
                paidKeys.Add(paymentKey);
                TotalPaid = checked(TotalPaid + amount);
                RecordFinancial(clock != null ? clock.Day : LastProcessedDay, "Зарплата", citizen.FirstName + " " + citizen.LastName + " · " + workplace.DisplayName, amount, false, "payroll-" + paymentKey);
                if (citizen.EventHistory == null) citizen.EventHistory = new List<string>();
                citizen.EventHistory.Add("payroll-paid:" + paymentKey);
                Action<string> handler = OnPayrollMessage;
                if (handler != null) handler(citizen.FirstName + " " + citizen.LastName + ": выплачено " + amount.ToString("N0") + " ₽.");
            }
        }

        public void Load(IEnumerable<CitizenRecord> citizenRecords, IEnumerable<WorkplaceRecord> workplaceRecords,
            IEnumerable<string> savedAccruedKeys, IEnumerable<string> savedPaidKeys, IEnumerable<string> savedBusinessDayKeys, IEnumerable<string> savedShopperDayKeys,
            long totalPaid, long totalBusinessRevenue, long totalBusinessCosts, int lastProcessedDay, IEnumerable<CityFinancialTransactionRecord> savedFinancialTransactions = null)
        {
            citizens.Clear(); workplaces.Clear(); accruedKeys.Clear(); paidKeys.Clear(); businessDayKeys.Clear(); shopperDayKeys.Clear(); financialTransactions.Clear(); financialTransactionIds.Clear();
            if (citizenRecords != null) foreach (CitizenRecord c in citizenRecords) RegisterCitizen(c);
            if (workplaceRecords != null) foreach (WorkplaceRecord w in workplaceRecords) RegisterWorkplace(w);
            if (savedAccruedKeys != null) foreach (string key in savedAccruedKeys) if (!string.IsNullOrEmpty(key)) accruedKeys.Add(key);
            if (savedPaidKeys != null) foreach (string key in savedPaidKeys) if (!string.IsNullOrEmpty(key)) paidKeys.Add(key);
            if (savedBusinessDayKeys != null) foreach (string key in savedBusinessDayKeys) if (!string.IsNullOrEmpty(key)) businessDayKeys.Add(key);
            if (savedShopperDayKeys != null) foreach (string key in savedShopperDayKeys) if (!string.IsNullOrEmpty(key)) shopperDayKeys.Add(key);
            TotalPaid = Math.Max(0, totalPaid);
            TotalBusinessRevenue = Math.Max(0, totalBusinessRevenue);
            TotalBusinessCosts = Math.Max(0, totalBusinessCosts);
            LastProcessedDay = Math.Max(0, lastProcessedDay);
            if (savedFinancialTransactions != null) foreach (CityFinancialTransactionRecord tx in savedFinancialTransactions) if (tx != null && !string.IsNullOrEmpty(tx.Id) && tx.Amount > 0 && financialTransactionIds.Add(tx.Id)) financialTransactions.Add(tx);
        }

        public void Export(List<string> accrued, List<string> paid, List<string> businessDays, List<string> shopperDays)
        {
            accrued.Clear(); paid.Clear(); businessDays.Clear(); shopperDays.Clear();
            foreach (string key in accruedKeys) accrued.Add(key);
            foreach (string key in paidKeys) paid.Add(key);
            foreach (string key in businessDayKeys) businessDays.Add(key);
            foreach (string key in shopperDayKeys) shopperDays.Add(key);
        }

        public void RecordExternalCityExpense(int day, string category, string description, long amount, string sourceId)
        {
            if (amount <= 0) return;
            string id = "city-expense-" + (sourceId ?? Guid.NewGuid().ToString("N"));
            if (!financialTransactionIds.Add(id)) return;
            financialTransactions.Add(new CityFinancialTransactionRecord
            {
                Id = id, Day = Math.Max(1, day), Category = category ?? "Городские расходы",
                Description = description ?? string.Empty, Amount = amount, IsIncome = false,
                SourceId = sourceId ?? string.Empty
            });
        }
    }

}