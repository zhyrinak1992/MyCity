using System;
using System.Collections.Generic;
using MyCity.Core;
using MyCity.Data;
using UnityEngine;

namespace MyCity.Simulation
{
    /// <summary>
    /// Supplier inventory, paid orders and road-following cargo vehicles.
    /// Trucks execute persisted road paths, deliver cargo, then return to their supplier.
    /// </summary>
    public sealed class SupplyLogisticsSystem : MonoBehaviour
    {
        private readonly Dictionary<string, SupplierRecord> suppliers = new Dictionary<string, SupplierRecord>();
        private readonly Dictionary<string, SupplyOrderRecord> orders = new Dictionary<string, SupplyOrderRecord>();
        private readonly Dictionary<string, CargoVehicleRecord> vehicles = new Dictionary<string, CargoVehicleRecord>();
        private ProductionChainSystem production;
        private GameClock clock;
        private int currentDay = 1;
        private readonly Dictionary<string, GameObject> truckObjects = new Dictionary<string, GameObject>();
        private readonly HashSet<string> visualInitialized = new HashSet<string>();
        private const float RoadNodeSpacing = 14f;
        private const float TruckSpeed = 9f;
        public event Action<string> OnLogisticsMessage;
        public event Action OnVehicleProgressChanged;
        public int ActiveVehicleCount { get { int count=0; foreach(CargoVehicleRecord v in vehicles.Values) if(v!=null && (v.Status=="В пути" || v.Status=="Возвращается на базу" || v.Status=="Ожидает приёмки")) count++; return count; } }
        public int PendingShipmentCount
        {
            get { int count=0; foreach (SupplyOrderRecord o in orders.Values) if (o != null && !o.IsDelivered) count++; return count; }
        }

        private void OnEnable() { TrySubscribe(); }
        private void Update()
        {
            if (clock == null) TrySubscribe();
            if (production == null) production = GetComponent<ProductionChainSystem>();
            UpdateVehicles(Time.deltaTime);
        }

        private void OnDestroy()
        {
            foreach (GameObject truck in truckObjects.Values) if (truck != null) Destroy(truck);
            truckObjects.Clear();
        }

        private static Vector3 NodePosition(RoadPathNodeRecord node)
        { return new Vector3(node.X * RoadNodeSpacing, 0.72f, node.Z * RoadNodeSpacing); }

        private GameObject CreateTruck(CargoVehicleRecord vehicle, Vector3 position)
        {
            GameObject root = new GameObject("Cargo Truck " + vehicle.Id);
            root.transform.position = position;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Cargo body"; body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.18f, 0f); body.transform.localScale = new Vector3(1.7f, 0.65f, 2.5f);
            GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.name = "Cab"; cabin.transform.SetParent(root.transform, false);
            cabin.transform.localPosition = new Vector3(0f, 0.22f, 1.05f); cabin.transform.localScale = new Vector3(1.55f, 0.72f, 0.8f);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            Material material = new Material(Shader.Find("Standard")); material.color = new Color(0.82f, 0.48f, 0.12f);
            foreach (Renderer renderer in renderers) renderer.sharedMaterial = material;
            Collider[] colliders = root.GetComponentsInChildren<Collider>();
            foreach (Collider collider in colliders) Destroy(collider);
            return root;
        }

        private void EnsureTruckVisual(CargoVehicleRecord vehicle)
        {
            if (vehicle == null || vehicle.RoadPath == null || vehicle.RoadPath.Count < 2 ||
                vehicle.Status == "Доставлено" || vehicle.Status == "В автопарке") return;
            GameObject truck;
            if (!truckObjects.TryGetValue(vehicle.Id, out truck) || truck == null)
            {
                int index = Mathf.Clamp(vehicle.CurrentPathIndex, 0, vehicle.RoadPath.Count - 1);
                int from = vehicle.IsReturning ? vehicle.RoadPath.Count - 1 - index : index;
                int to = vehicle.IsReturning ? Mathf.Max(0, from - 1) : Mathf.Min(vehicle.RoadPath.Count - 1, from + 1);
                Vector3 a = NodePosition(vehicle.RoadPath[from]);
                Vector3 b = NodePosition(vehicle.RoadPath[to]);
                truck = CreateTruck(vehicle, Vector3.Lerp(a, b, Mathf.Clamp01(vehicle.SegmentProgress)));
                truckObjects[vehicle.Id] = truck;
            }
        }

        private void UpdateVehicles(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            List<string> ids = new List<string>(vehicles.Keys); ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                CargoVehicleRecord vehicle = vehicles[id];
                if (vehicle == null || vehicle.RoadPath == null || vehicle.RoadPath.Count < 2 ||
                    vehicle.Status == "В автопарке" || vehicle.Status == "Доставлено") continue;
                if (vehicle.Status == "Ожидает приёмки") continue;
                EnsureTruckVisual(vehicle);
                GameObject truck;
                if (!truckObjects.TryGetValue(id, out truck) || truck == null) continue;
                int maxIndex = vehicle.RoadPath.Count - 1;
                vehicle.CurrentPathIndex = Mathf.Clamp(vehicle.CurrentPathIndex, 0, maxIndex - 1);
                float segmentLength = RoadNodeSpacing;
                float remaining = TruckSpeed * deltaTime;
                bool crossedNode = false;
                while (remaining > 0f)
                {
                    float segmentRemaining = (1f - vehicle.SegmentProgress) * segmentLength;
                    float step = Mathf.Min(remaining, segmentRemaining);
                    vehicle.SegmentProgress += step / segmentLength;
                    remaining -= step;
                    if (vehicle.SegmentProgress >= 0.9999f)
                    {
                        vehicle.SegmentProgress = 0f;
                        vehicle.CurrentPathIndex++;
                        crossedNode = true;
                        if (!vehicle.IsReturning && vehicle.CurrentPathIndex >= maxIndex)
                        {
                            HandleTruckArrival(vehicle);
                            if (vehicle.IsReturning) vehicle.CurrentPathIndex = 0;
                            else break;
                        }
                        else if (vehicle.IsReturning && vehicle.CurrentPathIndex >= maxIndex)
                        {
                            vehicle.Status = "В автопарке";
                            vehicle.LastProcessedDay = clock != null ? clock.Day : currentDay;
                            GameObject returned;
                            if (truckObjects.TryGetValue(id, out returned) && returned != null) Destroy(returned);
                            truckObjects.Remove(id);
                            break;
                        }
                    }
                }
                if (vehicle.Status != "В автопарке" && truck != null)
                {
                    int fromIndex = vehicle.IsReturning ? maxIndex - vehicle.CurrentPathIndex : vehicle.CurrentPathIndex;
                    int toIndex = vehicle.IsReturning ? Mathf.Max(0, fromIndex - 1) : Mathf.Min(maxIndex, fromIndex + 1);
                    Vector3 a = NodePosition(vehicle.RoadPath[fromIndex]);
                    Vector3 b = NodePosition(vehicle.RoadPath[toIndex]);
                    Vector3 position = Vector3.Lerp(a, b, vehicle.SegmentProgress);
                    truck.transform.position = position;
                    Vector3 direction = b - a;
                    if (direction.sqrMagnitude > 0.01f) truck.transform.rotation = Quaternion.LookRotation(direction);
                }
                if (crossedNode && OnVehicleProgressChanged != null) OnVehicleProgressChanged();
            }
        }

        private void HandleTruckArrival(CargoVehicleRecord vehicle)
        {
            SupplyOrderRecord order;
            if (!orders.TryGetValue(vehicle.OrderId, out order) || order == null) return;
            if (!order.IsDelivered)
            {
                if (production == null) production = GetComponent<ProductionChainSystem>();
                order.Status = "Доставлено: ожидает количественной и качественной приёмки";
                order.ActualArrivalDay = clock != null ? clock.Day : currentDay;
                vehicle.Status = "Ожидает приёмки";
                if (vehicle.RoadPath != null && vehicle.RoadPath.Count > 1) { vehicle.CurrentPathIndex = vehicle.RoadPath.Count - 2; vehicle.SegmentProgress = 1f; }
                if (OnLogisticsMessage != null) OnLogisticsMessage("Грузовик доставил " + order.Units + " ед. " + order.ProductId + ". Ожидается приёмка.");
                return;
            }
            vehicle.IsReturning = true;
            vehicle.CurrentPathIndex = 0;
            vehicle.SegmentProgress = 0f;
            vehicle.Status = "Возвращается на базу";
        }
        private void OnDisable() { if (clock != null) clock.OnDayChanged -= HandleDayChanged; clock=null; }

        private void TrySubscribe()
        {
            GameClock active=GameClock.Instance;
            if (active==null || active==clock) return;
            if (clock!=null) clock.OnDayChanged-=HandleDayChanged;
            clock=active; clock.OnDayChanged+=HandleDayChanged;
        }

        public void InitializeDefaults()
        {
            if (suppliers.Count>0) return;
            SupplierRecord food = NewSupplier("supplier_food","Оптовая продовольственная база",
                Item("flour",500,45,100), Item("water",2000,3,500), Item("raw_milk",300,35,80), Item("packaging",1000,8,200));
            food.RoadNodeX = -6; food.RoadNodeZ = 0;
            SupplierRecord industry = NewSupplier("supplier_industry","Промышленная оптовая база",
                Item("timber",500,120,80), Item("metal",500,180,80), Item("cement",800,65,120), Item("sand",2000,12,300));
            industry.RoadNodeX = 0; industry.RoadNodeZ = -6;
            SupplierRecord energy = NewSupplier("supplier_energy","Топливно-сырьевой поставщик",
                Item("crude_oil",2000,90,250), Item("electricity",10000,7,2000));
            energy.RoadNodeX = 6; energy.RoadNodeZ = 0;
            suppliers[food.Id]=food; suppliers[industry.Id]=industry; suppliers[energy.Id]=energy;
            // Competing offers for shared raw materials. They use the same established logistics hubs,
            // so the alternative catalogue does not invent new road nodes or break existing routes.
            SupplierRecord foodPartner = NewSupplier("supplier_food_partner", "Региональный продовольственный поставщик",
                Item("flour", 420, 42, 90), Item("water", 1600, 4, 420), Item("packaging", 800, 7, 160));
            foodPartner.RoadNodeX = food.RoadNodeX; foodPartner.RoadNodeZ = food.RoadNodeZ;
            SupplierRecord industryPartner = NewSupplier("supplier_industry_partner", "Региональная промышленная база",
                Item("timber", 420, 126, 90), Item("metal", 420, 172, 70), Item("cement", 650, 68, 100));
            industryPartner.RoadNodeX = industry.RoadNodeX; industryPartner.RoadNodeZ = industry.RoadNodeZ;
            suppliers[foodPartner.Id] = foodPartner; suppliers[industryPartner.Id] = industryPartner;
        }

        private void EnsureAlternativeSuppliers()
        {
            if (!suppliers.ContainsKey("supplier_food_partner"))
            {
                SupplierRecord partner = NewSupplier("supplier_food_partner", "Региональный продовольственный поставщик",
                    Item("flour", 420, 42, 90), Item("water", 1600, 4, 420), Item("packaging", 800, 7, 160));
                partner.RoadNodeX = -6; partner.RoadNodeZ = 0;
                SupplierRecord hub; if (suppliers.TryGetValue("supplier_food", out hub)) { partner.RoadNodeX = hub.RoadNodeX; partner.RoadNodeZ = hub.RoadNodeZ; }
                suppliers[partner.Id] = partner;
            }
            if (!suppliers.ContainsKey("supplier_industry_partner"))
            {
                SupplierRecord partner = NewSupplier("supplier_industry_partner", "Региональная промышленная база",
                    Item("timber", 420, 126, 90), Item("metal", 420, 172, 70), Item("cement", 650, 68, 100));
                partner.RoadNodeX = 0; partner.RoadNodeZ = -6;
                SupplierRecord hub; if (suppliers.TryGetValue("supplier_industry", out hub)) { partner.RoadNodeX = hub.RoadNodeX; partner.RoadNodeZ = hub.RoadNodeZ; }
                suppliers[partner.Id] = partner;
            }
        }

        public void GetSupplierQuotes(string productionSiteId, string productId, int units, List<SupplierQuoteRecord> destination)
        {
            if (destination == null) return;
            destination.Clear();
            if (units <= 0 || production == null) production = GetComponent<ProductionChainSystem>();
            ProductionSiteRecord site = production != null ? production.FindSite(productionSiteId) : null;
            if (site == null || units <= 0) return;
            List<string> ids = new List<string>(suppliers.Keys); ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                SupplierRecord supplier = suppliers[id];
                if (supplier == null || !supplier.IsOperating || !IsContractActive(supplier, currentDay) || units < supplier.MinimumOrderUnits) continue;
                SupplierInventoryRecord stock = FindItem(supplier, productId);
                if (stock == null || stock.Units < units || stock.UnitPrice < 0) continue;
                List<Vector2Int> path;
                if (!RoadRoutePlanner.TryBuildRoadPath(new Vector2Int(supplier.RoadNodeX, supplier.RoadNodeZ),
                    new Vector2Int(site.RoadNodeX, site.RoadNodeZ), out path)) continue;
                int distance = RoadRoutePlanner.GetRoadDistanceKm(path); if (distance <= 0) continue;
                try
                {
                    long discountedUnitPrice = checked(stock.UnitPrice * supplier.ContractDiscountPermille) / 1000L;
                    long goods = checked(discountedUnitPrice * units);
                    long freight = checked((long)distance * supplier.FreightCostPerKm);
                    long total = checked(goods + freight);
                    long deposit = checked(total * Mathf.Clamp(supplier.PaymentPrepaymentPermille, 0, 1000) / 1000L);
                    long exposure = 0; foreach (SupplyOrderRecord openOrder in orders.Values)
                        if (openOrder != null && openOrder.SupplierId == supplier.Id) exposure = checked(exposure + Math.Max(0L, openOrder.OutstandingPayable));
                    long newDebt = total - deposit;
                    if (deposit > site.OperatingFunds || (newDebt > 0 && (supplier.SupplierCreditLimit <= 0 || checked(exposure + newDebt) > supplier.SupplierCreditLimit))) continue;
                    SupplierQuoteRecord quote = new SupplierQuoteRecord();
                    quote.SupplierId = supplier.Id; quote.SupplierDisplayName = supplier.DisplayName;
                    quote.AvailableUnits = stock.Units; quote.UnitPrice = stock.UnitPrice;
                    quote.RouteDistanceKm = distance; quote.EstimatedTravelDays = Math.Max(1, 1 + distance / 250);
                    quote.GoodsCost = goods; quote.TransportCost = freight; quote.TotalCost = total;
                    quote.MinimumOrderUnits = supplier.MinimumOrderUnits; quote.ContractEndDay = supplier.ContractEndDay;
                    quote.ContractDiscountPermille = supplier.ContractDiscountPermille; quote.FreightCostPerKm = supplier.FreightCostPerKm;
                    quote.IsProvisional = supplier.CompletedDeliveries < 3;
                    quote.ReliabilityScore = supplier.CompletedDeliveries > 0 ?
                        Mathf.Clamp(Mathf.RoundToInt(100f * supplier.OnTimeDeliveries / supplier.CompletedDeliveries), 0, 100) : 70;
                    destination.Add(quote);
                }
                catch (OverflowException) { }
            }
            destination.Sort(delegate(SupplierQuoteRecord a, SupplierQuoteRecord b)
            {
                int cost = a.TotalCost.CompareTo(b.TotalCost); if (cost != 0) return cost;
                int reliability = b.ReliabilityScore.CompareTo(a.ReliabilityScore); if (reliability != 0) return reliability;
                int distance = a.RouteDistanceKm.CompareTo(b.RouteDistanceKm); if (distance != 0) return distance;
                return string.CompareOrdinal(a.SupplierId, b.SupplierId);
            });
        }

        private void AddSupplierFinancialEntry(SupplierRecord supplier, string type, long amount, string orderId, string claimId, string description)
        {
            if (supplier == null || amount <= 0) return;
            if (supplier.FinancialHistory == null) supplier.FinancialHistory = new List<SupplierFinancialTransactionRecord>();
            int day = clock != null ? clock.Day : currentDay;
            supplier.FinancialHistory.Add(new SupplierFinancialTransactionRecord
            {
                Id = "SFT-" + supplier.Id + "-" + day + "-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Type = type ?? string.Empty, Amount = amount, Day = day,
                OrderId = orderId ?? string.Empty, ClaimId = claimId ?? string.Empty,
                Description = description ?? string.Empty
            });
            while (supplier.FinancialHistory.Count > 500) supplier.FinancialHistory.RemoveAt(0);
        }

        private static SupplierRecord NewSupplier(string id,string name,params SupplierInventoryRecord[] items)
        { return new SupplierRecord { Id=id, DisplayName=name, IsOperating=true, Inventory=new List<SupplierInventoryRecord>(items) }; }
        private static SupplierInventoryRecord Item(string id,int units,long price,int restock)
        { return new SupplierInventoryRecord { ProductId=id, Units=units, UnitPrice=price, DailyRestockUnits=restock, MaxUnits=checked(units*3) }; }

        /// <summary>Automatically selects the lowest landed-cost operating supplier with sufficient stock and a valid road route.</summary>
        public bool TryPlaceBestValueOrderByRoad(string productionSiteId, string productId, int units,
            out string selectedSupplierId, out long quotedTotalCost, out string error)
        {
            selectedSupplierId = string.Empty; quotedTotalCost = 0; error = string.Empty;
            const int truckCapacityUnits = 8000;
            if (units <= 0 || units > truckCapacityUnits) { error = "Количество должно быть от 1 до 8 000 условных грузовых единиц."; return false; }
            if (production == null) production = GetComponent<ProductionChainSystem>();
            ProductionSiteRecord site = production != null ? production.FindSite(productionSiteId) : null;
            if (site == null) { error = "Производственная площадка не найдена."; return false; }

            SupplierRecord bestSupplier = null; int bestDistance = int.MaxValue; long bestCost = long.MaxValue;
            List<Vector2Int> bestPath = null;
            List<string> ids = new List<string>(suppliers.Keys); ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                SupplierRecord candidate = suppliers[id];
                if (candidate == null || !candidate.IsOperating || !IsContractActive(candidate, clock != null ? clock.Day : currentDay) || units < candidate.MinimumOrderUnits) continue;
                SupplierInventoryRecord stock = FindItem(candidate, productId);
                if (stock == null || stock.Units < units || stock.UnitPrice < 0) continue;
                List<Vector2Int> path;
                if (!RoadRoutePlanner.TryBuildRoadPath(new Vector2Int(candidate.RoadNodeX, candidate.RoadNodeZ),
                    new Vector2Int(site.RoadNodeX, site.RoadNodeZ), out path)) continue;
                int distance = RoadRoutePlanner.GetRoadDistanceKm(path); if (distance <= 0) continue;
                long landedCost;
                try { landedCost = checked(checked(checked(stock.UnitPrice * candidate.ContractDiscountPermille) / 1000L * units) + checked((long)distance * candidate.FreightCostPerKm)); }
                catch (OverflowException) { continue; }
                long deposit = checked(landedCost * Mathf.Clamp(candidate.PaymentPrepaymentPermille, 0, 1000) / 1000L);
                long exposure = 0; foreach (SupplyOrderRecord openOrder in orders.Values)
                    if (openOrder != null && openOrder.SupplierId == candidate.Id) exposure = checked(exposure + Math.Max(0L, openOrder.OutstandingPayable));
                long newDebt = landedCost - deposit;
                if (deposit > site.OperatingFunds || (newDebt > 0 && (candidate.SupplierCreditLimit <= 0 || checked(exposure + newDebt) > candidate.SupplierCreditLimit))) continue;
                int candidateReliability = candidate.CompletedDeliveries > 0 ?
                    Mathf.RoundToInt(100f * candidate.OnTimeDeliveries / candidate.CompletedDeliveries) : 70;
                int bestReliability = bestSupplier != null && bestSupplier.CompletedDeliveries > 0 ?
                    Mathf.RoundToInt(100f * bestSupplier.OnTimeDeliveries / bestSupplier.CompletedDeliveries) : 70;
                bool better = landedCost < bestCost ||
                    (landedCost == bestCost && candidateReliability > bestReliability) ||
                    (landedCost == bestCost && candidateReliability == bestReliability && distance < bestDistance) ||
                    (landedCost == bestCost && candidateReliability == bestReliability && distance == bestDistance &&
                     (bestSupplier == null || string.CompareOrdinal(candidate.Id, bestSupplier.Id) < 0));
                if (!better) continue;
                bestSupplier = candidate; bestDistance = distance; bestCost = landedCost; bestPath = path;
            }
            if (bestSupplier == null) { error = "Нет работающего поставщика с нужным остатком, доступным маршрутом и приемлемой стоимостью."; return false; }
            if (!TryPlaceOrderInternal(bestSupplier.Id, productionSiteId, productId, units, bestDistance, bestPath, out error)) return false;
            selectedSupplierId = bestSupplier.Id; quotedTotalCost = bestCost; return true;
        }

        public bool TryPlaceOrder(string supplierId,string productionSiteId,string productId,int units,int routeDistanceKm,out string error)
        {
            return TryPlaceOrderInternal(supplierId, productionSiteId, productId, units, routeDistanceKm, null, out error);
        }

        public bool TryPlaceOrderByRoad(string supplierId, string productionSiteId, string productId, int units, out string error)
        {
            error = string.Empty;
            SupplierRecord supplier;
            if (!suppliers.TryGetValue(supplierId, out supplier) || !supplier.IsOperating)
            { error = "Поставщик недоступен."; return false; }
            if (production == null) production = GetComponent<ProductionChainSystem>();
            ProductionSiteRecord site = production != null ? production.FindSite(productionSiteId) : null;
            if (site == null) { error = "Производственная площадка не найдена."; return false; }
            List<Vector2Int> path;
            if (!RoadRoutePlanner.TryBuildRoadPath(new Vector2Int(supplier.RoadNodeX, supplier.RoadNodeZ),
                new Vector2Int(site.RoadNodeX, site.RoadNodeZ), out path))
            { error = "Нет непрерывного маршрута по существующим дорогам. Заказ не оформлен."; return false; }
            int distanceKm = RoadRoutePlanner.GetRoadDistanceKm(path);
            if (distanceKm <= 0) { error = "Маршрут не имеет допустимой длины."; return false; }
            return TryPlaceOrderInternal(supplierId, productionSiteId, productId, units, distanceKm, path, out error);
        }

        private static List<RoadPathNodeRecord> ToRecords(List<Vector2Int> path)
        {
            List<RoadPathNodeRecord> result = new List<RoadPathNodeRecord>();
            if (path != null) foreach (Vector2Int node in path)
                result.Add(new RoadPathNodeRecord { X = node.x, Z = node.y });
            return result;
        }

        private bool TryPlaceOrderInternal(string supplierId,string productionSiteId,string productId,int units,int routeDistanceKm,List<Vector2Int> roadPath,out string error)
        {
            error=string.Empty;
            const int truckCapacityUnits = 8000;
            if (units<=0 || units>truckCapacityUnits) { error="Количество должно быть от 1 до 8 000 условных грузовых единиц — грузоподъёмность одной машины."; return false; }
            SupplierRecord supplier;
            if (!suppliers.TryGetValue(supplierId,out supplier) || !supplier.IsOperating) { error="Поставщик недоступен."; return false; }
            if (!IsContractActive(supplier, clock != null ? clock.Day : currentDay)) { error="Договор с поставщиком не действует на текущий игровой день."; return false; }
            if (units < supplier.MinimumOrderUnits) { error="Количество меньше минимальной партии по договору: " + supplier.MinimumOrderUnits + " ед."; return false; }
            SupplierInventoryRecord stock=FindItem(supplier,productId);
            if (stock==null || stock.Units<units) { error="У поставщика недостаточно товара на складе."; return false; }
            if (production==null) production=GetComponent<ProductionChainSystem>();
            ProductionSiteRecord site=production!=null?production.FindSite(productionSiteId):null;
            if (site==null) { error="Производственная площадка не найдена."; return false; }

            int distance=Math.Max(1,routeDistanceKm);
            long discountedUnitPrice=checked(stock.UnitPrice*supplier.ContractDiscountPermille)/1000L;
            long goodsCost=checked(discountedUnitPrice*units);
            long transportCost=checked((long)distance*supplier.FreightCostPerKm);
            long totalCost=checked(goodsCost+transportCost);
            int prepaymentPermille = Mathf.Clamp(supplier.PaymentPrepaymentPermille, 0, 1000);
            long prepayment = checked(totalCost * prepaymentPermille / 1000L);
            long payable = totalCost - prepayment;
            long currentExposure = 0;
            foreach (SupplyOrderRecord existing in orders.Values)
                if (existing != null && existing.SupplierId == supplierId) currentExposure = checked(currentExposure + Math.Max(0L, existing.OutstandingPayable));
            if (payable > 0 && (supplier.SupplierCreditLimit <= 0 || checked(currentExposure + payable) > supplier.SupplierCreditLimit))
            { error = "Лимит отсрочки поставщика будет превышен. Увеличьте лимит или внесите большую предоплату."; return false; }
            if (site.OperatingFunds < prepayment) { error = "Недостаточно средств для предоплаты по условиям договора."; return false; }

            stock.Units-=units;
            site.OperatingFunds-=prepayment;
            site.TotalRawMaterialPurchases=checked(site.TotalRawMaterialPurchases+totalCost);
            int travelDays=Math.Max(1,1+(distance/250));
            int day=clock!=null?clock.Day:currentDay;
            string orderId=Guid.NewGuid().ToString("N");
            string vehicleId=Guid.NewGuid().ToString("N");
            SupplyOrderRecord order=new SupplyOrderRecord {
                Id=orderId, SupplierId=supplierId, ProductionSiteId=productionSiteId,
                ProductId=productId, Units=units, TotalCost=totalCost, OrderedDay=day,
                PrepaidAmount=prepayment, OutstandingPayable=payable, PaymentDueDay=payable>0?checked(day+Math.Max(1,supplier.PaymentDeferralDays)):0,
                ArrivalDay=checked(day+travelDays), IsDelivered=false, Status="Отправлено грузовым автомобилем",
                VehicleId=vehicleId, RouteDistanceKm=distance, TransportCost=transportCost, GoodsCost=goodsCost,
                UnitGoodsPrice=discountedUnitPrice, ContractDiscountPermille=supplier.ContractDiscountPermille,
                RoadPath=ToRecords(roadPath), CurrentPathIndex=0, SegmentProgress=0f, IsReturning=false
            };
            CargoVehicleRecord vehicle=new CargoVehicleRecord {
                Id=vehicleId, OrderId=orderId, VehicleType="Грузовой автомобиль",
                CapacityUnits=truckCapacityUnits, LoadedUnits=units, RouteDistanceKm=distance,
                DispatchedDay=day, ArrivalDay=order.ArrivalDay, LastProcessedDay=day, Status="В пути",
                RoadPath=ToRecords(roadPath), CurrentPathIndex=0, SegmentProgress=0f, IsReturning=false
            };
            if (prepayment > 0) AddSupplierFinancialEntry(supplier, "Оплата заказа", prepayment, orderId, string.Empty,
                "Предоплата по условиям договора; заказ " + orderId);
            if (payable > 0) AddSupplierFinancialEntry(supplier, "Начисление задолженности", payable, orderId, string.Empty,
                "Отсрочка платежа до игрового дня " + order.PaymentDueDay + "; заказ " + orderId);
            orders[order.Id]=order;
            vehicles[vehicle.Id]=vehicle;
            if(OnLogisticsMessage!=null) OnLogisticsMessage("Грузовой автомобиль отправлен: "+units+" ед. "+productId+". Путь "+distance+" км; прибытие — день "+order.ArrivalDay+".");
            return true;
        }

        public bool TrySetSupplierPaymentTerms(string supplierId, int prepaymentPermille, int deferralDays, long creditLimit, out string error)
        {
            error = string.Empty; SupplierRecord supplier;
            if (!suppliers.TryGetValue(supplierId ?? string.Empty, out supplier) || supplier == null) { error = "Поставщик не найден."; return false; }
            if (prepaymentPermille < 0 || prepaymentPermille > 1000 || deferralDays < 0 || deferralDays > 365 || creditLimit < 0)
            { error = "Некорректные условия оплаты."; return false; }
            if (prepaymentPermille < 1000 && (deferralDays <= 0 || creditLimit <= 0))
            { error = "Для отсрочки укажите срок и положительный кредитный лимит."; return false; }
            supplier.PaymentPrepaymentPermille = prepaymentPermille;
            supplier.PaymentDeferralDays = deferralDays;
            supplier.SupplierCreditLimit = creditLimit;
            return true;
        }

        public bool TrySetSupplierLateFeeTerms(string supplierId, int dailyPermille, int maxPermille, out string error)
        {
            error = string.Empty; SupplierRecord supplier;
            if (!suppliers.TryGetValue(supplierId ?? string.Empty, out supplier) || supplier == null) { error = "Поставщик не найден."; return false; }
            if (dailyPermille < 0 || dailyPermille > 100 || maxPermille < 0 || maxPermille > 1000) { error = "Ставка должна быть 0–100‰ в день, предел — 0–1000‰."; return false; }
            supplier.LateFeePermillePerDay = dailyPermille; supplier.LateFeeMaxPermille = maxPermille; return true;
        }

        public void GetSupplierPaymentSchedule(string supplierId, List<SupplierPaymentScheduleRecord> destination)
        {
            if (destination == null) return; destination.Clear(); SupplierRecord supplier;
            if (!suppliers.TryGetValue(supplierId ?? string.Empty, out supplier) || supplier == null) return;
            int day = clock != null ? clock.Day : currentDay;
            foreach (SupplyOrderRecord order in orders.Values)
            {
                if (order == null || order.SupplierId != supplier.Id) continue;
                long principal = Math.Max(0L, order.OutstandingPayable); long penalty = Math.Max(0L, order.PaymentPenaltyAccrued - order.PaymentPenaltyPaid);
                if (principal + penalty <= 0) continue;
                int overdue = order.PaymentDueDay > 0 && day > order.PaymentDueDay ? day - order.PaymentDueDay : 0;
                destination.Add(new SupplierPaymentScheduleRecord { SupplierId=supplier.Id, SupplierName=supplier.DisplayName, OrderId=order.Id, ProductId=order.ProductId, ProductionSiteId=order.ProductionSiteId, DueDay=order.PaymentDueDay, OverdueDays=overdue, PrincipalOutstanding=principal, PenaltyOutstanding=penalty, TotalOutstanding=checked(principal+penalty), IsOverdue=overdue>0 });
            }
            destination.Sort(delegate(SupplierPaymentScheduleRecord a, SupplierPaymentScheduleRecord b) { int c=a.DueDay.CompareTo(b.DueDay); return c!=0?c:StringComparer.Ordinal.Compare(a.OrderId,b.OrderId); });
        }

        public void GetAllSupplierPaymentSchedule(List<SupplierPaymentScheduleRecord> destination)
        {
            if (destination == null) return; destination.Clear(); List<string> ids=new List<string>(suppliers.Keys); ids.Sort(StringComparer.Ordinal);
            for(int i=0;i<ids.Count;i++) { List<SupplierPaymentScheduleRecord> part=new List<SupplierPaymentScheduleRecord>(); GetSupplierPaymentSchedule(ids[i],part); destination.AddRange(part); }
        }

        public bool TryPaySupplierOrderBalance(string supplierId, string orderId, long amount, out string error)
        {
            error = string.Empty; SupplierRecord supplier; SupplyOrderRecord order;
            if (!suppliers.TryGetValue(supplierId ?? string.Empty, out supplier) || supplier == null || !orders.TryGetValue(orderId ?? string.Empty, out order) || order == null || order.SupplierId != supplierId)
            { error = "Поставщик или заказ не найдены."; return false; }
            long penaltyOutstanding = Math.Max(0L, order.PaymentPenaltyAccrued - order.PaymentPenaltyPaid);
            long totalOutstanding = checked(Math.Max(0L, order.OutstandingPayable) + penaltyOutstanding);
            if (amount <= 0 || amount > totalOutstanding) { error = "Сумма платежа должна быть положительной и не превышать общий долг с учётом штрафа."; return false; }
            if (production == null) production = GetComponent<ProductionChainSystem>();
            ProductionSiteRecord site = production != null ? production.FindSite(order.ProductionSiteId) : null;
            if (site == null) { error = "Производственная площадка не найдена."; return false; }
            if (site.OperatingFunds < amount) { error = "Недостаточно средств для погашения задолженности."; return false; }
            site.OperatingFunds -= amount;
            long penaltyPayment = Math.Min(amount, penaltyOutstanding);
            if (penaltyPayment > 0) { order.PaymentPenaltyPaid = checked(order.PaymentPenaltyPaid + penaltyPayment); AddSupplierFinancialEntry(supplier, "Погашение штрафа за просрочку", penaltyPayment, orderId, string.Empty, "Погашение штрафа по заказу " + orderId); }
            long principalPayment = amount - penaltyPayment;
            if (principalPayment > 0) { order.OutstandingPayable -= principalPayment; AddSupplierFinancialEntry(supplier, "Погашение задолженности", principalPayment, orderId, string.Empty, "Платёж по отсроченному заказу " + orderId); }
            order.LastPaymentDay = clock != null ? clock.Day : currentDay;
            return true;
        }

        public void Load(List<SupplierRecord> savedSuppliers,List<SupplyOrderRecord> savedOrders,List<CargoVehicleRecord> savedVehicles,int day)
        {
            suppliers.Clear(); orders.Clear(); vehicles.Clear(); currentDay=Math.Max(1,day);
            if(savedSuppliers!=null) foreach(SupplierRecord supplier in savedSuppliers)
                if(supplier!=null && !string.IsNullOrEmpty(supplier.Id))
                {
                    Normalize(supplier);
                    if (supplier.RoadNodeX == 0 && supplier.RoadNodeZ == 0)
                    {
                        if (supplier.Id == "supplier_food") { supplier.RoadNodeX = -6; supplier.RoadNodeZ = 0; }
                        else if (supplier.Id == "supplier_industry") { supplier.RoadNodeX = 0; supplier.RoadNodeZ = -6; }
                        else if (supplier.Id == "supplier_energy") { supplier.RoadNodeX = 6; supplier.RoadNodeZ = 0; }
                    }
                    suppliers[supplier.Id]=supplier;
                }
            if(suppliers.Count==0) InitializeDefaults();
            else EnsureAlternativeSuppliers();
            if(savedOrders!=null) foreach(SupplyOrderRecord order in savedOrders)
                if(order!=null && !string.IsNullOrEmpty(order.Id))
                {
                    if(order.RoadPath==null) order.RoadPath=new List<RoadPathNodeRecord>();
                    if(order.Units>0 && order.UnitGoodsPrice<=0)
                    {
                        order.GoodsCost=Math.Max(0L,order.TotalCost-order.TransportCost);
                        order.UnitGoodsPrice=order.GoodsCost/order.Units;
                        if(order.ContractDiscountPermille<=0) order.ContractDiscountPermille=1000;
                    }
                    orders[order.Id]=order;
                }
            if(savedVehicles!=null) foreach(CargoVehicleRecord vehicle in savedVehicles)
                if(vehicle!=null && !string.IsNullOrEmpty(vehicle.Id))
                { if(vehicle.RoadPath==null) vehicle.RoadPath=new List<RoadPathNodeRecord>(); vehicles[vehicle.Id]=vehicle; }
            foreach (CargoVehicleRecord vehicle in vehicles.Values) EnsureTruckVisual(vehicle);
            foreach(SupplyOrderRecord order in orders.Values)
                if(order!=null && !string.IsNullOrEmpty(order.VehicleId) && !vehicles.ContainsKey(order.VehicleId))
                    vehicles[order.VehicleId]=new CargoVehicleRecord {
                        Id=order.VehicleId, OrderId=order.Id, VehicleType="Грузовой автомобиль",
                        CapacityUnits=8000, LoadedUnits=order.IsDelivered?0:order.Units,
                        RouteDistanceKm=order.RouteDistanceKm, DispatchedDay=order.OrderedDay,
                        ArrivalDay=order.ArrivalDay, LastProcessedDay=order.IsDelivered?order.ArrivalDay:order.OrderedDay,
                        Status=order.IsDelivered?"Доставлено":"В пути",
                        RoadPath=order.RoadPath != null ? new List<RoadPathNodeRecord>(order.RoadPath) : new List<RoadPathNodeRecord>()
                    };
            foreach (CargoVehicleRecord vehicle in vehicles.Values) EnsureTruckVisual(vehicle);
        }

        public void Export(List<SupplierRecord> supplierDestination,List<SupplyOrderRecord> orderDestination,List<CargoVehicleRecord> vehicleDestination)
        {
            if(supplierDestination!=null) { supplierDestination.Clear(); List<string> ids=new List<string>(suppliers.Keys); ids.Sort(StringComparer.Ordinal); foreach(string id in ids) supplierDestination.Add(suppliers[id]); }
            if(orderDestination!=null) { orderDestination.Clear(); List<string> ids=new List<string>(orders.Keys); ids.Sort(StringComparer.Ordinal); foreach(string id in ids) orderDestination.Add(orders[id]); }
            if(vehicleDestination!=null) { vehicleDestination.Clear(); List<string> ids=new List<string>(vehicles.Keys); ids.Sort(StringComparer.Ordinal); foreach(string id in ids) vehicleDestination.Add(vehicles[id]); }
        }

        public int GetSupplierStock(string supplierId,string productId)
        { SupplierRecord s; if(!suppliers.TryGetValue(supplierId,out s)) return 0; SupplierInventoryRecord i=FindItem(s,productId); return i!=null?i.Units:0; }

        public bool TryUpdateSupplierContract(string supplierId, int startDay, int endDay, int minimumOrderUnits,
            long freightCostPerKm, int discountPermille, out string error)
        {
            error = string.Empty;
            SupplierRecord supplier;
            if (string.IsNullOrEmpty(supplierId) || !suppliers.TryGetValue(supplierId, out supplier) || supplier == null)
            { error = "Поставщик не найден."; return false; }
            if (startDay < 1 || (endDay > 0 && endDay < startDay) || minimumOrderUnits < 1 ||
                freightCostPerKm < 1 || discountPermille < 1 || discountPermille > 1000)
            { error = "Некорректные условия договора: проверьте даты, минимальную партию, тариф и скидку."; return false; }
            supplier.ContractStartDay = startDay;
            supplier.ContractEndDay = endDay;
            supplier.MinimumOrderUnits = minimumOrderUnits;
            supplier.FreightCostPerKm = freightCostPerKm;
            supplier.ContractDiscountPermille = discountPermille;
            if (supplier.ContractHistory == null) supplier.ContractHistory = new List<SupplierContractHistoryRecord>();
            supplier.ContractHistory.Add(new SupplierContractHistoryRecord {
                Day = clock != null ? clock.Day : currentDay, StartDay = startDay, EndDay = endDay,
                MinimumOrderUnits = minimumOrderUnits, FreightCostPerKm = freightCostPerKm,
                DiscountPermille = discountPermille, ChangeNote = "Условия договора изменены игроком"
            });
            while (supplier.ContractHistory.Count > 100) supplier.ContractHistory.RemoveAt(0);
            return true;
        }

        public bool TryAdjustSupplierContract(string supplierId, int minimumOrderDelta, int discountDeltaPermille,
            long freightDelta, int extendEndDayBy, out string error)
        {
            error = string.Empty;
            SupplierRecord supplier;
            if (string.IsNullOrEmpty(supplierId) || !suppliers.TryGetValue(supplierId, out supplier) || supplier == null)
            { error = "Поставщик не найден."; return false; }
            int today = clock != null ? clock.Day : currentDay;
            int startDay = supplier.ContractStartDay;
            int endDay = supplier.ContractEndDay;
            if (extendEndDayBy > 0 && endDay > 0)
            {
                if (endDay < today) { startDay = today; endDay = checked(today + extendEndDayBy); }
                else endDay = checked(endDay + extendEndDayBy);
            }
            return TryUpdateSupplierContract(supplierId, startDay, endDay,
                Mathf.Max(1, supplier.MinimumOrderUnits + minimumOrderDelta),
                Math.Max(1L, supplier.FreightCostPerKm + freightDelta),
                Mathf.Clamp(supplier.ContractDiscountPermille + discountDeltaPermille, 1, 1000), out error);
        }

        public void GetSupplierContractHistory(string supplierId, List<SupplierContractHistoryRecord> destination)
        {
            if (destination == null) return;
            destination.Clear();
            SupplierRecord supplier;
            if (!string.IsNullOrEmpty(supplierId) && suppliers.TryGetValue(supplierId, out supplier) &&
                supplier != null && supplier.ContractHistory != null) destination.AddRange(supplier.ContractHistory);
        }

        public bool TrySubmitSupplierClaim(string orderId, long requestedAdditionalCompensation, string notes, out string error)
        {
            error = string.Empty; SupplyOrderRecord order;
            if (string.IsNullOrEmpty(orderId) || !orders.TryGetValue(orderId, out order) || order == null) { error = "Заказ не найден."; return false; }
            if (!order.IsQualityInspected || (order.ShortUnits <= 0 && order.DefectiveUnits <= 0)) { error = "Претензия доступна только после приёмки с недостачей или браком."; return false; }
            SupplierRecord supplier; if (!suppliers.TryGetValue(order.SupplierId, out supplier) || supplier == null) { error = "Поставщик не найден."; return false; }
            if (supplier.Claims == null) supplier.Claims = new List<SupplierClaimRecord>();
            if (supplier.Claims.Exists(c => c != null && c.OrderId == orderId)) { error = "По этому заказу претензия уже зарегистрирована."; return false; }
            string type = order.ShortUnits > 0 && order.DefectiveUnits > 0 ? "Недостача и брак" : (order.ShortUnits > 0 ? "Недостача" : "Брак");
            supplier.Claims.Add(new SupplierClaimRecord { Id="CLM-"+order.Id, OrderId=order.Id, SupplierId=supplier.Id, ProductionSiteId=order.ProductionSiteId, ClaimType=type, Units=order.ShortUnits+order.DefectiveUnits, RequestedAdditionalCompensation=Math.Max(0L,requestedAdditionalCompensation), SubmittedDay=clock!=null?clock.Day:currentDay, Status="На рассмотрении", Notes=notes??string.Empty });
            while(supplier.Claims.Count>100) supplier.Claims.RemoveAt(0); return true;
        }

        public bool TryRespondToSupplierClaim(string supplierId, string claimId, out string error)
        {
            error=string.Empty; SupplierRecord supplier;
            if(!suppliers.TryGetValue(supplierId??string.Empty,out supplier)||supplier==null||supplier.Claims==null){error="Поставщик или претензии не найдены.";return false;}
            SupplierClaimRecord claim=supplier.Claims.Find(c=>c!=null&&c.Id==claimId);
            if(claim==null||claim.Status!="На рассмотрении"){error="Претензия не найдена или уже рассмотрена.";return false;}
            SupplyOrderRecord order; orders.TryGetValue(claim.OrderId,out order);
            int denominator=order!=null?Math.Max(1,order.Units):Math.Max(1,claim.Units);
            int impact=order!=null?(order.DefectiveUnits*2+order.ShortUnits)*100/denominator:0;
            bool accepts=impact>=10||supplier.CompletedDeliveries<=2||supplier.DefectiveUnits+supplier.ShortUnits>=5;
            claim.ResponseDay=clock!=null?clock.Day:currentDay;
            if(accepts){claim.Status="Признана";claim.ApprovedAdditionalCompensation=claim.RequestedAdditionalCompensation;claim.Notes=(claim.Notes??string.Empty)+"\nПоставщик признал нарушение.";}
            else{claim.Status="Отклонена";claim.ApprovedAdditionalCompensation=0;claim.Notes=(claim.Notes??string.Empty)+"\nПоставщик отклонил дополнительную компенсацию.";} return true;
        }

        public bool TrySettleSupplierClaim(string supplierId,string claimId,out string error)
        {
            error=string.Empty; SupplierRecord supplier;
            if(!suppliers.TryGetValue(supplierId??string.Empty,out supplier)||supplier==null||supplier.Claims==null){error="Поставщик или претензии не найдены.";return false;}
            SupplierClaimRecord c=supplier.Claims.Find(x=>x!=null&&x.Id==claimId);
            if(c==null||c.Status!="Признана"||c.IsSettled){error="Можно урегулировать только признанную претензию.";return false;}
            // Дополнительная компенсация — отдельная операция сверх сторно стоимости
            // непринятых единиц, которое уже выполнено при физической приёмке.
            ProductionSiteRecord site = production != null ? production.FindSite(c.ProductionSiteId) : null;
            if (site == null) { error = "Производственная площадка для зачисления компенсации не найдена."; return false; }
            long payout = Math.Max(0L, c.ApprovedAdditionalCompensation);
            try { site.OperatingFunds = checked(site.OperatingFunds + payout); }
            catch (OverflowException) { error = "Сумма компенсации превышает допустимый финансовый лимит."; return false; }
            if (payout > 0) AddSupplierFinancialEntry(supplier, "Дополнительная компенсация", payout, c.OrderId, c.Id,
                "Дополнительное возмещение ущерба сверх возврата стоимости непринятых единиц.");
            c.SettledAmount = payout;
            c.SettledDay = clock != null ? clock.Day : currentDay;
            c.SettlementReference = "SUP-CLAIM-" + c.Id + "-" + c.SettledDay;
            c.IsSettled = true;
            c.Status = "Урегулирована";
            return true;
        }

        public bool TryStartSupplierNegotiation(string supplierId,int requestedDiscountPermille,long requestedFreightCostPerKm,int requestedMinimumOrderUnits,int requestedContractDays,string reason,out string negotiationId,out string error)
        {
            negotiationId=string.Empty;error=string.Empty;SupplierRecord supplier;
            if(!suppliers.TryGetValue(supplierId??string.Empty,out supplier)||supplier==null){error="Поставщик не найден.";return false;}
            if(requestedDiscountPermille<1||requestedDiscountPermille>1000||requestedFreightCostPerKm<1||requestedMinimumOrderUnits<1||requestedContractDays<0){error="Некорректные параметры предложения.";return false;}
            if(supplier.Negotiations==null)supplier.Negotiations=new List<SupplierNegotiationRecord>();
            int rating=supplier.RatingHistory!=null&&supplier.RatingHistory.Count>0?supplier.RatingHistory[supplier.RatingHistory.Count-1].Score:50;
            bool ready=supplier.CompletedDeliveries>=3&&supplier.RatingHistory!=null&&supplier.RatingHistory.Count>0;
            int discount=requestedDiscountPermille;long freight=requestedFreightCostPerKm;string status="Предварительное предложение",note="Недостаточно истории поставок для уверенной оценки.";
            if(ready){int concession=rating>=85?180:rating>=70?120:rating>=50?60:20;int minDiscount=1000-concession;
                if(requestedDiscountPermille<minDiscount){discount=Math.Min(1000,requestedDiscountPermille+50);status="Встречное предложение";note="Поставщик предлагает меньшую скидку.";}
                else if(requestedFreightCostPerKm<Math.Max(1L,supplier.FreightCostPerKm*80/100)){freight=Math.Max(requestedFreightCostPerKm,supplier.FreightCostPerKm*90/100);status="Встречное предложение";note="Поставщик предлагает более высокий тариф.";}
                else{status="Принято поставщиком";note="Условия приняты поставщиком.";}}
            negotiationId="NEG-"+supplier.Id+"-"+(clock!=null?clock.Day:currentDay)+"-"+(supplier.Negotiations.Count+1);
            supplier.Negotiations.Add(new SupplierNegotiationRecord{Id=negotiationId,Day=clock!=null?clock.Day:currentDay,Status=status,Reason=reason??string.Empty,RequestedDiscountPermille=requestedDiscountPermille,RequestedFreightCostPerKm=requestedFreightCostPerKm,RequestedMinimumOrderUnits=requestedMinimumOrderUnits,RequestedContractDays=requestedContractDays,CounterDiscountPermille=discount,CounterFreightCostPerKm=freight,CounterMinimumOrderUnits=requestedMinimumOrderUnits,CounterContractDays=requestedContractDays,ResponseNote=note});
            while(supplier.Negotiations.Count>100)supplier.Negotiations.RemoveAt(0);return true;
        }

        public bool TryAcceptSupplierCounterOffer(string supplierId,string negotiationId,out string error)
        {
            error=string.Empty;SupplierRecord supplier;
            if(!suppliers.TryGetValue(supplierId??string.Empty,out supplier)||supplier==null||supplier.Negotiations==null){error="Поставщик или переговоры не найдены.";return false;}
            SupplierNegotiationRecord n=supplier.Negotiations.Find(x=>x!=null&&x.Id==negotiationId);
            if(n==null||(n.Status!="Встречное предложение"&&n.Status!="Предварительное предложение"&&n.Status!="Принято поставщиком")){error="Предложение нельзя принять.";return false;}
            int today=clock!=null?clock.Day:currentDay;int end=n.CounterContractDays>0?checked(today+n.CounterContractDays):supplier.ContractEndDay;
            if(!TryUpdateSupplierContract(supplierId,today,end,n.CounterMinimumOrderUnits,n.CounterFreightCostPerKm,n.CounterDiscountPermille,out error))return false;
            n.Status="Принято игроком";return true;
        }

        public bool GetSupplierSettlementSummary(string supplierId, SupplierSettlementSummaryRecord summary)
        {
            if (summary == null) return false;
            SupplierRecord supplier;
            if (string.IsNullOrEmpty(supplierId) || !suppliers.TryGetValue(supplierId, out supplier) || supplier == null) return false;
            summary.SupplierId = supplier.Id; summary.SupplierName = supplier.DisplayName;
            summary.TotalPaid = summary.TotalReturned = summary.TotalCompensation = summary.TotalOutstanding = 0;
            summary.UnreconciledPaid = summary.UnreconciledCredits = 0;
            summary.TransactionCount = summary.UnreconciledTransactionCount = summary.ReconciledTransactionCount = 0;
            summary.LastTransactionDay = summary.LastReconciliationDay = 0;
            if (supplier.FinancialHistory == null) supplier.FinancialHistory = new List<SupplierFinancialTransactionRecord>();
            for (int i = 0; i < supplier.FinancialHistory.Count; i++)
            {
                SupplierFinancialTransactionRecord tx = supplier.FinancialHistory[i];
                if (tx == null || tx.Amount <= 0) continue;
                summary.TransactionCount++;
                if (tx.Day > summary.LastTransactionDay) summary.LastTransactionDay = tx.Day;
                bool payment = tx.Type == "Оплата заказа" || tx.Type == "Погашение задолженности";
                bool refund = tx.Type == "Возврат за брак/недостачу";
                bool compensation = tx.Type == "Дополнительная компенсация";
                if (payment) summary.TotalPaid = checked(summary.TotalPaid + tx.Amount);
                else if (refund) summary.TotalReturned = checked(summary.TotalReturned + tx.Amount);
                else if (compensation) summary.TotalCompensation = checked(summary.TotalCompensation + tx.Amount);
                else if (tx.Type == "Начисление задолженности") summary.TotalOutstanding = checked(summary.TotalOutstanding + tx.Amount);
                if (tx.IsReconciled)
                {
                    summary.ReconciledTransactionCount++;
                    if (tx.ReconciledDay > summary.LastReconciliationDay) summary.LastReconciliationDay = tx.ReconciledDay;
                }
                else
                {
                    summary.UnreconciledTransactionCount++;
                    if (payment) summary.UnreconciledPaid = checked(summary.UnreconciledPaid + tx.Amount);
                    else if (tx.Type == "Начисление задолженности") summary.TotalOutstanding = Math.Max(0L, summary.TotalOutstanding);
                    else if (refund || compensation) summary.UnreconciledCredits = checked(summary.UnreconciledCredits + tx.Amount);
                }
            }
            long accruedPayables = 0, settledPayables = 0;
            for (int i = 0; i < supplier.FinancialHistory.Count; i++)
            {
                SupplierFinancialTransactionRecord tx = supplier.FinancialHistory[i]; if (tx == null || tx.Amount <= 0) continue;
                if (tx.Type == "Начисление задолженности") accruedPayables = checked(accruedPayables + tx.Amount);
                else if (tx.Type == "Погашение задолженности") settledPayables = checked(settledPayables + tx.Amount);
            }
            long penalties = 0, penaltiesPaid = 0;
            for (int i = 0; i < supplier.FinancialHistory.Count; i++) { SupplierFinancialTransactionRecord tx=supplier.FinancialHistory[i]; if(tx==null||tx.Amount<=0) continue; if(tx.Type=="Штраф за просрочку") penalties=checked(penalties+tx.Amount); else if(tx.Type=="Погашение штрафа за просрочку") penaltiesPaid=checked(penaltiesPaid+tx.Amount); }
            summary.TotalOutstanding = Math.Max(0L, checked(accruedPayables - settledPayables + penalties - penaltiesPaid));
            summary.NetPaid = summary.TotalPaid - summary.TotalReturned - summary.TotalCompensation;
            summary.UnreconciledNet = summary.UnreconciledPaid - summary.UnreconciledCredits;
            return true;
        }

        public bool TryReconcileSupplierAccount(string supplierId, out string batchId, out string error)
        {
            batchId = string.Empty; error = string.Empty;
            SupplierRecord supplier;
            if (string.IsNullOrEmpty(supplierId) || !suppliers.TryGetValue(supplierId, out supplier) || supplier == null)
            { error = "Поставщик не найден."; return false; }
            if (supplier.FinancialHistory == null) supplier.FinancialHistory = new List<SupplierFinancialTransactionRecord>();
            List<SupplierFinancialTransactionRecord> pending = new List<SupplierFinancialTransactionRecord>();
            for (int i = 0; i < supplier.FinancialHistory.Count; i++)
            {
                SupplierFinancialTransactionRecord tx = supplier.FinancialHistory[i];
                if (tx != null && !tx.IsReconciled) pending.Add(tx);
            }
            if (pending.Count == 0) { error = "Новых операций для сверки нет."; return false; }
            int day = clock != null ? clock.Day : currentDay;
            batchId = "REC-" + supplier.Id + "-" + day + "-" + (supplier.FinancialHistory.Count + 1);
            for (int i = 0; i < pending.Count; i++)
            {
                pending[i].IsReconciled = true;
                pending[i].ReconciledDay = day;
                pending[i].ReconciliationBatchId = batchId;
            }
            return true;
        }

        public void GetSupplierContracts(List<SupplierRecord> destination)
        {
            if (destination == null) return;
            destination.Clear();
            List<string> ids = new List<string>(suppliers.Keys); ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids) if (suppliers[id] != null) destination.Add(suppliers[id]);
        }

        private void ProcessSupplierPaymentCalendar(int day)
        {
            List<string> ids=new List<string>(orders.Keys); ids.Sort(StringComparer.Ordinal);
            for(int i=0;i<ids.Count;i++)
            {
                SupplyOrderRecord order=orders[ids[i]]; SupplierRecord supplier;
                if(order==null || order.OutstandingPayable<=0 || order.PaymentDueDay<=0 || day<=order.PaymentDueDay || !suppliers.TryGetValue(order.SupplierId,out supplier) || supplier==null) continue;
                int overdueDays=day-order.PaymentDueDay;
                if(supplier.LateFeePermillePerDay>0 && order.LastPenaltyDay<day)
                {
                    long cap=checked(Math.Max(0L,order.TotalCost-order.PrepaidAmount)*supplier.LateFeeMaxPermille/1000L);
                    long accrued=Math.Max(0L,order.PaymentPenaltyAccrued); long room=Math.Max(0L,cap-accrued);
                    long fee=Math.Min(room,Math.Max(1L,checked(order.OutstandingPayable*supplier.LateFeePermillePerDay/1000L)));
                    if(fee>0) { order.PaymentPenaltyAccrued=checked(order.PaymentPenaltyAccrued+fee); AddSupplierFinancialEntry(supplier,"Штраф за просрочку",fee,order.Id,string.Empty,"Просрочка платежа на день "+overdueDays); }
                    order.LastPenaltyDay=day;
                }
                if(!order.PaymentOverdueNoticeSent && OnLogisticsMessage!=null) { OnLogisticsMessage("Просрочен платёж поставщику "+supplier.DisplayName+" по заказу "+order.Id+". Дней просрочки: "+overdueDays+"."); order.PaymentOverdueNoticeSent=true; order.LastPaymentNoticeDay=day; }
            }
            foreach(SupplyOrderRecord order in orders.Values)
            {
                if(order==null || order.OutstandingPayable<=0 || order.PaymentDueDay<=0 || day>=order.PaymentDueDay || order.LastPaymentNoticeDay==day-1) continue;
                if(order.PaymentDueDay-day==2 && OnLogisticsMessage!=null) { SupplierRecord supplier; string name=suppliers.TryGetValue(order.SupplierId,out supplier)&&supplier!=null?supplier.DisplayName:"поставщику"; OnLogisticsMessage("Через 2 игровых дня наступит срок оплаты заказа "+order.Id+" ("+name+")."); order.LastPaymentNoticeDay=day; }
            }
        }

        private void HandleDayChanged(int day)
        {
            currentDay=day;
            ProcessSupplierPaymentCalendar(day);
            CreateSupplierRatingSnapshots(day);
            foreach(SupplierRecord supplier in suppliers.Values)
            {
                if(supplier==null || !supplier.IsOperating || supplier.Inventory==null) continue;
                if (day <= supplier.LastRestockDay) continue;
                foreach(SupplierInventoryRecord item in supplier.Inventory)
                    if(item!=null && item.DailyRestockUnits>0)
                    {
                        int cap = item.MaxUnits > 0 ? item.MaxUnits : int.MaxValue;
                        item.Units = Math.Min(cap, checked(item.Units + item.DailyRestockUnits));
                    }
                supplier.LastRestockDay = day;
            }
            List<string> ids=new List<string>(orders.Keys); ids.Sort(StringComparer.Ordinal);
            foreach(string id in ids)
            {
                SupplyOrderRecord order=orders[id];
                if(order==null || order.IsDelivered || day<order.ArrivalDay || (order.Status ?? string.Empty).StartsWith("Доставлено:", StringComparison.Ordinal)) continue;
                CargoVehicleRecord movingVehicle;
                if (!string.IsNullOrEmpty(order.VehicleId) && vehicles.TryGetValue(order.VehicleId, out movingVehicle) && movingVehicle != null && movingVehicle.RoadPath != null && movingVehicle.RoadPath.Count > 1) continue;
                if(production==null) production=GetComponent<ProductionChainSystem>();
                order.Status="Доставлено: ожидает количественной и качественной приёмки";
                order.ActualArrivalDay=day;
                CargoVehicleRecord arrivedVehicle;
                if(!string.IsNullOrEmpty(order.VehicleId) && vehicles.TryGetValue(order.VehicleId,out arrivedVehicle))
                { arrivedVehicle.Status="Ожидает приёмки"; arrivedVehicle.LastProcessedDay=day; }
                if(OnLogisticsMessage!=null) OnLogisticsMessage("Поставка "+order.ProductId+" прибыла. Требуется приёмка: "+order.Units+" ед.");
            }
        }

        public void GetPendingInspectionOrders(string productionSiteId, List<SupplyOrderRecord> destination)
        {
            if (destination == null) return;
            destination.Clear();
            foreach (SupplyOrderRecord order in orders.Values)
                if (order != null && order.ProductionSiteId == productionSiteId && !order.IsDelivered &&
                    (order.Status ?? string.Empty).StartsWith("Доставлено:", StringComparison.Ordinal)) destination.Add(order);
            destination.Sort(delegate(SupplyOrderRecord a, SupplyOrderRecord b) { return a.ArrivalDay.CompareTo(b.ArrivalDay); });
        }

        public bool TryInspectAndAcceptOrder(string orderId, int physicallyReceivedUnits, int defectiveUnits,
            string inspectionNotes, out string error)
        {
            error = string.Empty;
            SupplyOrderRecord order;
            if (string.IsNullOrEmpty(orderId) || !orders.TryGetValue(orderId, out order) || order == null)
            { error = "Заказ не найден."; return false; }
            if (order.IsDelivered || order.IsQualityInspected || !(order.Status ?? string.Empty).StartsWith("Доставлено:", StringComparison.Ordinal))
            { error = "Эта поставка уже принята или ещё не прибыла."; return false; }
            if (physicallyReceivedUnits < 0 || physicallyReceivedUnits > order.Units)
            { error = "Фактически полученное количество должно быть от 0 до количества в заказе."; return false; }
            if (defectiveUnits < 0 || defectiveUnits > physicallyReceivedUnits)
            { error = "Количество брака не может превышать фактически полученное количество."; return false; }
            if (production == null) production = GetComponent<ProductionChainSystem>();
            ProductionSiteRecord site = production != null ? production.FindSite(order.ProductionSiteId) : null;
            if (site == null) { error = "Производственная площадка недоступна для приёмки."; return false; }

            int acceptedUnits = physicallyReceivedUnits - defectiveUnits;
            int shortUnits = order.Units - physicallyReceivedUnits;
            long rejectedGoodsValue = checked(order.UnitGoodsPrice * (order.Units - acceptedUnits));
            long acceptedInventoryValue = checked(order.UnitGoodsPrice * acceptedUnits + order.TransportCost);
            long landedUnitCost = acceptedUnits > 0 ? acceptedInventoryValue / acceptedUnits : 0;
            if (acceptedUnits > 0 && !production.AddInputStock(order.ProductionSiteId, order.ProductId, acceptedUnits, landedUnitCost))
            { error = "Не удалось зачислить принятый товар на склад предприятия."; return false; }

            // The order is prepaid in this prototype. Goods value for missing/defective units is credited back;
            // freight remains payable because the transport service was performed.
            site.OperatingFunds = checked(site.OperatingFunds + rejectedGoodsValue);
            site.TotalRawMaterialPurchases = Math.Max(0L, site.TotalRawMaterialPurchases - rejectedGoodsValue);
            SupplierRecord refundSupplier;
            if (rejectedGoodsValue > 0 && suppliers.TryGetValue(order.SupplierId, out refundSupplier))
                AddSupplierFinancialEntry(refundSupplier, "Возврат за брак/недостачу", rejectedGoodsValue, order.Id, string.Empty,
                    "Сторно стоимости непринятых единиц; перевозка не возвращается.");
            order.ReceivedUnits = physicallyReceivedUnits;
            order.AcceptedUnits = acceptedUnits;
            order.DefectiveUnits = defectiveUnits;
            order.ShortUnits = shortUnits;
            order.RefundedGoodsCost = rejectedGoodsValue;
            order.IsQualityInspected = true;
            order.IsDelivered = true;
            order.InspectionDay = clock != null ? clock.Day : currentDay;
            order.ActualArrivalDay = order.ActualArrivalDay > 0 ? order.ActualArrivalDay : order.InspectionDay;
            order.InspectionNotes = inspectionNotes ?? string.Empty;
            order.CompletedDay = order.InspectionDay;
            order.Status = shortUnits > 0 || defectiveUnits > 0 ? "Принято частично: зафиксированы недостача/брак" : "Принято полностью после проверки";

            SupplierRecord supplier;
            if (suppliers.TryGetValue(order.SupplierId, out supplier) && supplier != null)
            {
                supplier.CompletedDeliveries = checked(supplier.CompletedDeliveries + 1);
                supplier.AcceptedUnits = checked(supplier.AcceptedUnits + acceptedUnits);
                supplier.DefectiveUnits = checked(supplier.DefectiveUnits + defectiveUnits);
                supplier.ShortUnits = checked(supplier.ShortUnits + shortUnits);
                if (order.ActualArrivalDay <= order.ArrivalDay) supplier.OnTimeDeliveries = checked(supplier.OnTimeDeliveries + 1);
                supplier.CumulativePurchaseValue = checked(supplier.CumulativePurchaseValue + order.TotalCost - rejectedGoodsValue);
                supplier.LastCompletedDeliveryDay = order.InspectionDay;
            }
            CargoVehicleRecord vehicle;
            if (!string.IsNullOrEmpty(order.VehicleId) && vehicles.TryGetValue(order.VehicleId, out vehicle) && vehicle != null)
            {
                vehicle.LoadedUnits = 0;
                vehicle.IsReturning = true;
                vehicle.CurrentPathIndex = 0;
                vehicle.SegmentProgress = 0f;
                vehicle.Status = vehicle.RoadPath != null && vehicle.RoadPath.Count > 1 ? "Возвращается на базу" : "Доставлено";
            }
            if (OnLogisticsMessage != null) OnLogisticsMessage("Приёмка " + order.ProductId + ": принято " + acceptedUnits + ", брак " + defectiveUnits + ", недостача " + shortUnits + " ед.");
            return true;
        }

        private void CreateSupplierRatingSnapshots(int day)
        {
            if (day <= 0 || day % 30 != 0) return;
            int period = day / 30;
            foreach (SupplierRecord supplier in suppliers.Values)
            {
                if (supplier == null || supplier.LastRatingSnapshotPeriod >= period) continue;
                int deliveries = Math.Max(0, supplier.CompletedDeliveries - supplier.SnapshotCompletedDeliveries);
                int accepted = Math.Max(0, supplier.AcceptedUnits - supplier.SnapshotAcceptedUnits);
                int defective = Math.Max(0, supplier.DefectiveUnits - supplier.SnapshotDefectiveUnits);
                int shortUnits = Math.Max(0, supplier.ShortUnits - supplier.SnapshotShortUnits);
                if (deliveries == 0)
                {
                    supplier.LastRatingSnapshotPeriod = period;
                    supplier.SnapshotCompletedDeliveries = supplier.CompletedDeliveries;
                    supplier.SnapshotAcceptedUnits = supplier.AcceptedUnits;
                    supplier.SnapshotDefectiveUnits = supplier.DefectiveUnits;
                    supplier.SnapshotShortUnits = supplier.ShortUnits;
                    supplier.SnapshotOnTimeDeliveries = supplier.OnTimeDeliveries;
                    continue;
                }
                int received = accepted + defective;
                int totalExpected = received + shortUnits;
                int qualityScore = received > 0 ? Mathf.Clamp(Mathf.RoundToInt(100f * accepted / received), 0, 100) : (totalExpected > 0 ? 0 : 100);
                int completenessScore = totalExpected > 0 ? Mathf.Clamp(Mathf.RoundToInt(100f * received / totalExpected), 0, 100) : 100;
                int punctualityScore = deliveries > 0 ? Mathf.Clamp(Mathf.RoundToInt(100f * (supplier.OnTimeDeliveries - supplier.SnapshotOnTimeDeliveries) / deliveries), 0, 100) : 100;
                int score = Mathf.Clamp(Mathf.RoundToInt(qualityScore * 0.5f + punctualityScore * 0.25f + completenessScore * 0.25f), 0, 100);
                if (supplier.RatingHistory == null) supplier.RatingHistory = new List<SupplierRatingSnapshotRecord>();
                supplier.RatingHistory.Add(new SupplierRatingSnapshotRecord {
                    SupplierId=supplier.Id, SupplierDisplayName=supplier.DisplayName, PeriodNumber=period,
                    PeriodStartDay=(period-1)*30+1, PeriodEndDay=day, CompletedDeliveries=deliveries,
                    AcceptedUnits=accepted, DefectiveUnits=defective, ShortUnits=shortUnits, OnTimeDeliveries=Math.Max(0,supplier.OnTimeDeliveries-supplier.SnapshotOnTimeDeliveries),
                    Score=score, QualityScore=qualityScore, PunctualityScore=punctualityScore,
                    CompletenessScore=completenessScore, IsProvisional=deliveries<3
                });
                while (supplier.RatingHistory.Count > 24) supplier.RatingHistory.RemoveAt(0);
                supplier.LastRatingSnapshotPeriod=period;
                supplier.SnapshotCompletedDeliveries=supplier.CompletedDeliveries;
                supplier.SnapshotAcceptedUnits=supplier.AcceptedUnits;
                supplier.SnapshotDefectiveUnits=supplier.DefectiveUnits;
                supplier.SnapshotShortUnits=supplier.ShortUnits;
                supplier.SnapshotOnTimeDeliveries=supplier.OnTimeDeliveries;
            }
        }

        public void GetSupplierRatingHistory(string supplierId, List<SupplierRatingSnapshotRecord> destination)
        {
            if (destination == null) return;
            destination.Clear();
            SupplierRecord supplier;
            if (!string.IsNullOrEmpty(supplierId) && suppliers.TryGetValue(supplierId, out supplier) && supplier != null && supplier.RatingHistory != null)
                destination.AddRange(supplier.RatingHistory);
        }

        private static SupplierInventoryRecord FindItem(SupplierRecord supplier,string productId)
        { return supplier.Inventory!=null?supplier.Inventory.Find(x=>x!=null && x.ProductId==productId):null; }
        private static void Normalize(SupplierRecord supplier)
        {
            if(supplier.Inventory==null) supplier.Inventory=new List<SupplierInventoryRecord>();
            if(supplier.ContractStartDay<=0) supplier.ContractStartDay=1;
            if(supplier.MinimumOrderUnits<=0) supplier.MinimumOrderUnits=1;
            if(supplier.FreightCostPerKm<=0) supplier.FreightCostPerKm=18;
            if(supplier.ContractDiscountPermille<=0 || supplier.ContractDiscountPermille>1000) supplier.ContractDiscountPermille=1000;
            if(supplier.PaymentPrepaymentPermille<0 || supplier.PaymentPrepaymentPermille>1000) supplier.PaymentPrepaymentPermille=1000;
            if(supplier.PaymentDeferralDays<0) supplier.PaymentDeferralDays=0;
            if(supplier.SupplierCreditLimit<0) supplier.SupplierCreditLimit=0;
            if(supplier.LateFeePermillePerDay<0 || supplier.LateFeePermillePerDay>100) supplier.LateFeePermillePerDay=5;
            if(supplier.LateFeeMaxPermille<0 || supplier.LateFeeMaxPermille>1000) supplier.LateFeeMaxPermille=100;
            if(supplier.RatingHistory==null) supplier.RatingHistory=new List<SupplierRatingSnapshotRecord>();
            if(supplier.ContractHistory==null) supplier.ContractHistory=new List<SupplierContractHistoryRecord>();
            if(supplier.Claims==null) supplier.Claims=new List<SupplierClaimRecord>();
            if(supplier.Negotiations==null) supplier.Negotiations=new List<SupplierNegotiationRecord>();
            if(supplier.FinancialHistory==null) supplier.FinancialHistory=new List<SupplierFinancialTransactionRecord>();
            if(supplier.RatingHistory.Count==0 && supplier.LastRatingSnapshotPeriod==0)
            {
                supplier.SnapshotCompletedDeliveries=supplier.CompletedDeliveries;
                supplier.SnapshotAcceptedUnits=supplier.AcceptedUnits;
                supplier.SnapshotDefectiveUnits=supplier.DefectiveUnits;
                supplier.SnapshotShortUnits=supplier.ShortUnits;
                supplier.SnapshotOnTimeDeliveries=supplier.OnTimeDeliveries;
            }
        }
        private static bool IsContractActive(SupplierRecord supplier, int day)
        { return supplier!=null && day>=supplier.ContractStartDay && (supplier.ContractEndDay<=0 || day<=supplier.ContractEndDay); }
    }
}
