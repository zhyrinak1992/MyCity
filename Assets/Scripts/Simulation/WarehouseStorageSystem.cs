using System;
using System.Collections.Generic;
using MyCity.Data;
using UnityEngine;

namespace MyCity.Simulation
{
    /// <summary>
    /// Persistent regional warehouse inventory. Transfers are atomic: source stock is only
    /// deducted after the destination accepts the complete quantity.
    /// </summary>
    public sealed class WarehouseStorageSystem : MonoBehaviour
    {
        private readonly Dictionary<string, WarehouseRecord> warehouses = new Dictionary<string, WarehouseRecord>();
        private ProductionChainSystem production;
        private FleetManagementSystem fleet;
        private readonly Dictionary<string, GameObject> transferTruckObjects = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, WorkplaceRecord> storeWorkplaces = new Dictionary<string, WorkplaceRecord>();
        private const float TransferNodeSpacing = 14f;
        private MyCity.Core.GameClock clock;
        public event Action<long, string> OnWarehouseOperatingCostCharged;
        private void Update() { UpdateTransferTruckVisuals(); }
        private void OnDestroy() { foreach (GameObject truck in transferTruckObjects.Values) if (truck != null) Destroy(truck); transferTruckObjects.Clear(); }

        public int WarehouseCount { get { return warehouses.Count; } }

        public int GetActiveDockCount(string warehouseId)
        { WarehouseRecord w=Find(warehouseId); return w!=null && w.ActiveDockAssignments!=null ? w.ActiveDockAssignments.Count : 0; }

        public int GetQueueCount(string warehouseId)
        { WarehouseRecord w=Find(warehouseId); return w!=null && w.DockQueue!=null ? w.DockQueue.Count : 0; }

        /// <summary>Registers an arriving truck in the FIFO dock queue. Duplicate vehicle requests are idempotent.</summary>
        public bool TryQueueVehicle(string warehouseId, string vehicleId, string operation, int units, int day, out string error)
        {
            error=string.Empty; WarehouseRecord w=Find(warehouseId);
            if(w==null || !w.IsOperating){error="Склад закрыт или не найден.";return false;}
            if(string.IsNullOrEmpty(vehicleId) || (operation!="Разгрузка" && operation!="Погрузка") || units<=0){error="Некорректная заявка на док.";return false;}
            Normalize(w);
            if(w.ActiveDockAssignments.Exists(x=>x!=null && x.VehicleId==vehicleId) || w.DockQueue.Exists(x=>x!=null && x.VehicleId==vehicleId)) return true;
            w.DockQueue.Add(new WarehouseDockQueueRecord{VehicleId=vehicleId,Operation=operation,RequestedUnits=units,EnqueuedAtDay=Math.Max(0,day),Priority=0});
            w.DockQueue.Sort((a,b)=>{int c=b.Priority.CompareTo(a.Priority); if(c!=0)return c; c=a.EnqueuedAtDay.CompareTo(b.EnqueuedAtDay); return c!=0?c:string.CompareOrdinal(a.VehicleId,b.VehicleId);});
            w.LastStatus="Грузовик поставлен в очередь на "+operation.ToLower()+". Ожидают: "+w.DockQueue.Count+".";
            AssignWaitingVehicles(w,day); return true;
        }

        /// <summary>Queues a physical warehouse-to-warehouse shipment. Inventory moves only after dock service completes.</summary>
        public bool TryDispatchWarehouseTransfer(string sourceWarehouseId, string destinationWarehouseId, string productId, int units, string vehicleId, int day, out string error)
        {
            error = string.Empty;
            if (string.IsNullOrEmpty(vehicleId) || sourceWarehouseId == destinationWarehouseId || units <= 0 || string.IsNullOrEmpty(productId))
            { error = "Некорректные параметры межскладской перевозки."; return false; }
            WarehouseRecord source = Find(sourceWarehouseId);
            WarehouseRecord destination = Find(destinationWarehouseId);
            if (source == null || destination == null || !source.IsOperating || !destination.IsOperating)
            { error = "Оба склада должны существовать и работать."; return false; }
            if (GetStock(sourceWarehouseId, productId) < units)
            { error = "На складе отправления недостаточно товара."; return false; }
            if (GetFreeCapacity(destinationWarehouseId) < units)
            { error = "На складе назначения недостаточно свободного места."; return false; }
            List<Vector2Int> route;
            if (!RoadRoutePlanner.TryBuildRoadPath(new Vector2Int(source.RoadNodeX, source.RoadNodeZ), new Vector2Int(destination.RoadNodeX, destination.RoadNodeZ), out route))
            { error = "Между складами нет непрерывного дорожного маршрута."; return false; }
            Normalize(source);
            if (source.ActiveDockAssignments.Exists(x => x != null && x.VehicleId == vehicleId) || source.DockQueue.Exists(x => x != null && x.VehicleId == vehicleId))
            { error = "Этот автомобиль уже зарегистрирован на складе отправления."; return false; }
            if (fleet == null) fleet = GetComponent<FleetManagementSystem>();
            FleetVehicleRecord assignedVehicle = null;
            if (fleet == null || !fleet.TryAssign(vehicleId, units, out assignedVehicle, out error)) return false;
            // Reserve stock only after a suitable physical vehicle has been reserved.
            long sourceUnitCost;
            if (!TryWithdraw(sourceWarehouseId, productId, units, out sourceUnitCost, out error)) { fleet.Release(assignedVehicle.Id, vehicleId); return false; }
            WarehouseTransferRecord transfer = new WarehouseTransferRecord { VehicleId = vehicleId, FleetVehicleId = assignedVehicle.Id, RouteDistanceKm = Math.Max(0, route.Count - 1), SourceWarehouseId = sourceWarehouseId, DestinationWarehouseId = destinationWarehouseId, ProductId = productId, Units = units, CargoUnitCostAtDispatch = sourceUnitCost, Status = "Ожидает погрузки", CreatedDay = Math.Max(0, day), RoadPath = new List<RoadPathNodeRecord>() };
            if (route.Count == 1) route.Add(route[0]);
            foreach (Vector2Int node in route) transfer.RoadPath.Add(new RoadPathNodeRecord { X = node.x, Z = node.y });
            Normalize(source);
            source.Transfers.Add(transfer);
            source.DockQueue.Add(new WarehouseDockQueueRecord { VehicleId = vehicleId, Operation = "Погрузка", RequestedUnits = units, EnqueuedAtDay = Math.Max(0, day), Priority = 0, SourceWarehouseId = sourceWarehouseId, DestinationWarehouseId = destinationWarehouseId, ProductId = productId, IsTransferUnloading = false });
            source.DockQueue.Sort((a, b) => { int c = b.Priority.CompareTo(a.Priority); if (c != 0) return c; c = a.EnqueuedAtDay.CompareTo(b.EnqueuedAtDay); return c != 0 ? c : string.CompareOrdinal(a.VehicleId, b.VehicleId); });
            source.LastStatus = "Межскладская перевозка поставлена в очередь на погрузку; запас зарезервирован.";
            AssignWaitingVehicles(source, day);
            return true;
        }

        private void CompleteDockAssignment(WarehouseRecord warehouse, WarehouseDockAssignmentRecord assignment, int day)
        {
            if (!string.IsNullOrEmpty(assignment.StoreOrderId))
            {
                CompleteStoreLoading(warehouse, assignment, day);
                return;
            }
            if (string.IsNullOrEmpty(assignment.SourceWarehouseId) || string.IsNullOrEmpty(assignment.DestinationWarehouseId) || string.IsNullOrEmpty(assignment.ProductId))
            { warehouse.LastStatus = "Док " + (assignment.DockIndex + 1) + " освободился после " + assignment.Operation.ToLower() + " автомобиля " + assignment.VehicleId + "."; return; }
            WarehouseRecord source = Find(assignment.SourceWarehouseId);
            WarehouseRecord destination = Find(assignment.DestinationWarehouseId);
            if (source == null || destination == null)
            { warehouse.LastStatus = "Межскладская перевозка отменена: склад не найден."; return; }
            WarehouseTransferRecord transfer = FindTransfer(assignment.VehicleId);
            if (transfer == null) { warehouse.LastStatus = "Не найдена запись рейса " + assignment.VehicleId + "."; return; }
            if (!assignment.IsTransferUnloading)
            {
                if (!destination.IsOperating)
                { string rollback; TryStore(source.Id, assignment.ProductId, assignment.RequestedUnits, transfer.CargoUnitCostAtDispatch, out rollback); transfer.Status = "Отменён"; source.LastStatus = "Перевозка отменена: склад назначения закрыт, резерв возвращён."; return; }
                transfer.Status = "В пути"; transfer.CurrentPathIndex = 0; transfer.MinutesOnCurrentSegment = 0;
                source.LastStatus = "Погрузка завершена. Грузовик " + assignment.VehicleId + " отправился в " + destination.DisplayName + ".";
                return;
            }
            string error;
            if (!destination.IsOperating || GetFreeCapacity(destination.Id) < assignment.RequestedUnits || !TryStore(destination.Id, assignment.ProductId, assignment.RequestedUnits, transfer.CargoUnitCostAtDispatch, out error))
            {
                string rollback; TryStore(source.Id, assignment.ProductId, assignment.RequestedUnits, transfer.CargoUnitCostAtDispatch, out rollback);
                transfer.Status = "Отменён"; transfer.CompletedDay = day;
                source.LastStatus = "Разгрузка не состоялась; зарезервированный товар возвращён на склад отправления.";
                destination.LastStatus = "Не удалось принять межскладскую поставку.";
                return;
            }
            transfer.CargoDelivered = true; transfer.Status = "Возвращается на базу"; transfer.CurrentPathIndex = transfer.RoadPath.Count - 1; transfer.MinutesOnCurrentSegment = 0;
            source.LastStatus = "Автомобиль " + assignment.VehicleId + " завершил отправку " + assignment.RequestedUnits + " ед. " + assignment.ProductId + ".";
            destination.LastStatus = "Принята межскладская поставка от " + source.DisplayName + ": " + assignment.RequestedUnits + " ед. " + assignment.ProductId + ".";
            RemoveTransferTruck(assignment.VehicleId);
        }

        /// <summary>Advances dock service in simulation minutes and releases completed docks.</summary>
        public void AdvanceDockService(int simulationMinutes, int day)
        {
            if(simulationMinutes<=0)return;
            foreach(WarehouseRecord w in warehouses.Values)
            {
                Normalize(w); if(!w.IsOperating)continue;
                for(int i=w.ActiveDockAssignments.Count-1;i>=0;i--)
                {
                    WarehouseDockAssignmentRecord a=w.ActiveDockAssignments[i]; if(a==null){w.ActiveDockAssignments.RemoveAt(i);continue;}
                    a.RemainingServiceMinutes=Math.Max(0,a.RemainingServiceMinutes-simulationMinutes);
                    if(a.RemainingServiceMinutes==0){CompleteDockAssignment(w,a,day);w.ActiveDockAssignments.RemoveAt(i);}
                }
                AssignWaitingVehicles(w,day);
            }
            AdvanceTransfers(day);
            AdvanceStoreShipments(day);
            AdvanceStoreReturns(day);
        }

        private void AdvanceTransfers(int day)
        {
            List<WarehouseTransferRecord> active = GetAllTransfers();
            foreach (WarehouseTransferRecord transfer in active)
            {
                if (transfer == null || (transfer.Status != "В пути" && transfer.Status != "Возвращается на базу") || transfer.RoadPath == null || transfer.RoadPath.Count < 2) continue;
                transfer.MinutesOnCurrentSegment++;
                int segmentMinutes = Math.Max(1, transfer.TravelMinutesPerSegment);
                if (transfer.MinutesOnCurrentSegment >= segmentMinutes)
                {
                    transfer.MinutesOnCurrentSegment = 0;
                    if (transfer.Status == "В пути")
                    {
                        transfer.CurrentPathIndex++;
                        if (transfer.CurrentPathIndex >= transfer.RoadPath.Count - 1)
                        {
                            WarehouseRecord destination = Find(transfer.DestinationWarehouseId);
                            if (destination == null || !destination.IsOperating)
                            { RollbackTransfer(transfer, day, "Склад назначения недоступен в пути."); continue; }
                            string error;
                            if (!TryQueueTransferUnloading(transfer, day, out error))
                            { RollbackTransfer(transfer, day, error); continue; }
                            transfer.Status = "Ожидает разгрузки";
                        }
                    }
                    else
                    {
                        transfer.CurrentPathIndex--;
                        if (transfer.CurrentPathIndex <= 0)
                        { transfer.CurrentPathIndex = 0; transfer.Status = "Завершён"; transfer.CompletedDay = day; if(fleet==null)fleet=GetComponent<FleetManagementSystem>(); if(fleet!=null) { FleetTransactionRecord tx=fleet.CompleteTrip(transfer.FleetVehicleId,transfer.VehicleId,transfer.RouteDistanceKm*2,day,false); if(tx!=null) SettleTransferFreight(transfer,tx); } RemoveTransferTruck(transfer.VehicleId); }
                    }
                }
            }
        }

        private void SettleTransferFreight(WarehouseTransferRecord transfer, FleetTransactionRecord tx)
        {
            if (transfer == null || tx == null || transfer.Units <= 0) return;
            // The round-trip freight is capitalised into the delivered stock, not charged again to city funds.
            transfer.FuelCost = tx.FuelCost;
            transfer.DriverCost = tx.DriverCost;
            transfer.MaintenanceCost = tx.MaintenanceCost;
            transfer.TotalTransportCost = tx.TotalCost;
            transfer.FreightCostPerUnit = (tx.TotalCost + transfer.Units - 1) / transfer.Units;
            transfer.TotalCargoLandedCost = checked((long)transfer.Units * (transfer.CargoUnitCostAtDispatch + transfer.FreightCostPerUnit));
            WarehouseRecord destination = Find(transfer.DestinationWarehouseId);
            WarehouseInventoryRecord item = destination != null ? FindItem(destination.Inventory, transfer.ProductId) : null;
            if (item == null) return;
            long deliveredValue = checked((long)transfer.Units * (transfer.CargoUnitCostAtDispatch + transfer.FreightCostPerUnit));
            long existingValue = checked((long)Math.Max(0, item.Units - transfer.Units) * Math.Max(0, item.AverageUnitCost));
            item.AverageUnitCost = item.Units > 0 ? checked(existingValue + deliveredValue) / item.Units : 0;
            destination.LastStatus += " Себестоимость партии с доставкой: " + transfer.TotalCargoLandedCost + " ₽ (фрахт " + tx.TotalCost + " ₽).";
        }

        private bool TryQueueTransferUnloading(WarehouseTransferRecord transfer, int day, out string error)
        {
            error = string.Empty; WarehouseRecord destination = Find(transfer.DestinationWarehouseId);
            if (destination == null || !destination.IsOperating) { error = "Склад назначения закрыт."; return false; }
            Normalize(destination);
            if (destination.ActiveDockAssignments.Exists(x => x != null && x.VehicleId == transfer.VehicleId) || destination.DockQueue.Exists(x => x != null && x.VehicleId == transfer.VehicleId)) return true;
            destination.DockQueue.Add(new WarehouseDockQueueRecord { VehicleId = transfer.VehicleId, Operation = "Разгрузка", RequestedUnits = transfer.Units, EnqueuedAtDay = Math.Max(0, day), Priority = 0, SourceWarehouseId = transfer.SourceWarehouseId, DestinationWarehouseId = transfer.DestinationWarehouseId, ProductId = transfer.ProductId, IsTransferUnloading = true });
            destination.DockQueue.Sort((a,b) => { int c=b.Priority.CompareTo(a.Priority); if(c!=0)return c; c=a.EnqueuedAtDay.CompareTo(b.EnqueuedAtDay); return c!=0?c:string.CompareOrdinal(a.VehicleId,b.VehicleId); });
            AssignWaitingVehicles(destination, day); destination.LastStatus = "Грузовик прибыл и ожидает разгрузки."; return true;
        }

        private void RollbackTransfer(WarehouseTransferRecord transfer, int day, string reason)
        {
            WarehouseRecord source = Find(transfer.SourceWarehouseId); if (source != null) { string ignored; TryStore(source.Id, transfer.ProductId, transfer.Units, transfer.CargoUnitCostAtDispatch, out ignored); source.LastStatus = reason + " Резерв возвращён."; }
            WarehouseRecord destination = Find(transfer.DestinationWarehouseId); if(destination!=null) { Normalize(destination); destination.DockQueue.RemoveAll(x=>x!=null && x.VehicleId==transfer.VehicleId); destination.ActiveDockAssignments.RemoveAll(x=>x!=null && x.VehicleId==transfer.VehicleId); }
            transfer.Status = "Отменён"; transfer.CompletedDay = day; if(fleet==null)fleet=GetComponent<FleetManagementSystem>(); if(fleet!=null)fleet.Release(transfer.FleetVehicleId,transfer.VehicleId); RemoveTransferTruck(transfer.VehicleId);
        }

        private WarehouseTransferRecord FindTransfer(string vehicleId)
        { foreach(WarehouseRecord w in warehouses.Values) { Normalize(w); WarehouseTransferRecord t=w.Transfers.Find(x=>x!=null && x.VehicleId==vehicleId); if(t!=null)return t; } return null; }

        private List<WarehouseTransferRecord> GetAllTransfers()
        { List<WarehouseTransferRecord> result=new List<WarehouseTransferRecord>(); foreach(WarehouseRecord w in warehouses.Values){Normalize(w);result.AddRange(w.Transfers);} return result; }

        private void UpdateTransferTruckVisuals()
        {
            List<WarehouseTransferRecord> active=GetAllTransfers(); HashSet<string> live=new HashSet<string>();
            foreach(WarehouseTransferRecord t in active)
            {
                if(t==null || (t.Status!="В пути" && t.Status!="Ожидает разгрузки" && t.Status!="Возвращается на базу")) continue; live.Add(t.VehicleId);
                if(t.RoadPath==null || t.RoadPath.Count<2) continue;
                GameObject truck; if(!transferTruckObjects.TryGetValue(t.VehicleId,out truck) || truck==null)
                { truck=CreateTransferTruck(t.VehicleId); transferTruckObjects[t.VehicleId]=truck; }
                bool returning=t.Status=="Возвращается на базу";
                int index=returning?Mathf.Clamp(t.CurrentPathIndex-1,0,t.RoadPath.Count-2):Mathf.Clamp(t.CurrentPathIndex,0,t.RoadPath.Count-2);
                RoadPathNodeRecord a=returning?t.RoadPath[index+1]:t.RoadPath[index], b=returning?t.RoadPath[index]:t.RoadPath[index+1];
                float progress=t.Status=="Ожидает разгрузки"?1f:Mathf.Clamp01(t.MinutesOnCurrentSegment/(float)Math.Max(1,t.TravelMinutesPerSegment));
                Vector3 from=new Vector3(a.X*TransferNodeSpacing,.72f,a.Z*TransferNodeSpacing), to=new Vector3(b.X*TransferNodeSpacing,.72f,b.Z*TransferNodeSpacing);
                truck.transform.position=Vector3.Lerp(from,to,progress); Vector3 direction=to-from; if(direction.sqrMagnitude>.01f)truck.transform.rotation=Quaternion.LookRotation(direction);
            }
            foreach (WorkplaceRecord workplace in storeWorkplaces.Values)
            {
                if (workplace == null || workplace.PendingDeliveries == null) continue;
                foreach (StoreDeliveryRecord delivery in workplace.PendingDeliveries)
                {
                    if (delivery == null || (delivery.LogisticsStatus != "В пути в магазин" && delivery.LogisticsStatus != "Возвращается на склад")) continue;
                    live.Add(delivery.Id);
                    if (delivery.RoadPath == null || delivery.RoadPath.Count < 2) continue;
                    GameObject truck;
                    if (!transferTruckObjects.TryGetValue(delivery.Id, out truck) || truck == null)
                    { truck = CreateTransferTruck(delivery.Id); transferTruckObjects[delivery.Id] = truck; }
                    bool returning = delivery.LogisticsStatus == "Возвращается на склад";
                    int index = returning ? Mathf.Clamp(delivery.CurrentPathIndex - 1, 0, delivery.RoadPath.Count - 2) : Mathf.Clamp(delivery.CurrentPathIndex, 0, delivery.RoadPath.Count - 2);
                    RoadPathNodeRecord a = returning ? delivery.RoadPath[index + 1] : delivery.RoadPath[index];
                    RoadPathNodeRecord b = returning ? delivery.RoadPath[index] : delivery.RoadPath[index + 1];
                    float progress = Mathf.Clamp01(delivery.MinutesOnCurrentSegment / (float)Math.Max(1, delivery.TravelMinutesPerSegment));
                    Vector3 from = new Vector3(a.X * TransferNodeSpacing, .72f, a.Z * TransferNodeSpacing);
                    Vector3 to = new Vector3(b.X * TransferNodeSpacing, .72f, b.Z * TransferNodeSpacing);
                    truck.transform.position = Vector3.Lerp(from, to, progress);
                    Vector3 direction = to - from; if (direction.sqrMagnitude > .01f) truck.transform.rotation = Quaternion.LookRotation(direction);
                }
            }
            List<string> stale=new List<string>(); foreach(string id in transferTruckObjects.Keys)if(!live.Contains(id))stale.Add(id); foreach(string id in stale)RemoveTransferTruck(id);
        }

        private GameObject CreateTransferTruck(string id)
        {
            GameObject root=new GameObject("Inter-warehouse truck "+id);
            GameObject body=GameObject.CreatePrimitive(PrimitiveType.Cube); body.transform.SetParent(root.transform,false); body.transform.localPosition=new Vector3(0,.18f,0); body.transform.localScale=new Vector3(1.7f,.65f,2.5f);
            GameObject cab=GameObject.CreatePrimitive(PrimitiveType.Cube); cab.transform.SetParent(root.transform,false); cab.transform.localPosition=new Vector3(0,.22f,1.05f); cab.transform.localScale=new Vector3(1.5f,.7f,.8f);
            Renderer[] renderers=root.GetComponentsInChildren<Renderer>(); Material mat=new Material(Shader.Find("Standard")); mat.color=new Color(.18f,.42f,.78f); foreach(Renderer r in renderers)r.sharedMaterial=mat;
            Collider[] colliders=root.GetComponentsInChildren<Collider>(); foreach(Collider c in colliders)Destroy(c); return root;
        }

        private void RemoveTransferTruck(string id)
        { GameObject truck; if(transferTruckObjects.TryGetValue(id,out truck)){if(truck!=null)Destroy(truck);transferTruckObjects.Remove(id);} }

        public bool ReleaseVehicleFromDock(string warehouseId,string vehicleId,int day)
        {
            WarehouseRecord w=Find(warehouseId); if(w==null)return false; Normalize(w);
            int removed=w.ActiveDockAssignments.RemoveAll(x=>x!=null && x.VehicleId==vehicleId);
            removed+=w.DockQueue.RemoveAll(x=>x!=null && x.VehicleId==vehicleId);
            if(removed>0){w.LastStatus="Автомобиль "+vehicleId+" покинул складскую площадку.";AssignWaitingVehicles(w,day);}
            return removed>0;
        }

        private static void AssignWaitingVehicles(WarehouseRecord w,int day)
        {
            while(w.IsOperating && w.ActiveDockAssignments.Count<w.DockCount && w.DockQueue.Count>0)
            {
                WarehouseDockQueueRecord q=w.DockQueue[0]; w.DockQueue.RemoveAt(0); if(q==null)continue;
                bool[] used=new bool[w.DockCount]; foreach(WarehouseDockAssignmentRecord a in w.ActiveDockAssignments)if(a!=null && a.DockIndex>=0 && a.DockIndex<used.Length)used[a.DockIndex]=true;
                int dock=Array.FindIndex(used,x=>!x); if(dock<0){w.DockQueue.Insert(0,q);break;}
                int service=Math.Max(5,(int)Math.Ceiling(q.RequestedUnits/20.0));
                w.ActiveDockAssignments.Add(new WarehouseDockAssignmentRecord{DockIndex=dock,VehicleId=q.VehicleId,Operation=q.Operation,RequestedUnits=q.RequestedUnits,RemainingServiceMinutes=service,StartedAtDay=Math.Max(0,day),SourceWarehouseId=q.SourceWarehouseId,DestinationWarehouseId=q.DestinationWarehouseId,ProductId=q.ProductId,IsTransferUnloading=q.IsTransferUnloading,StoreOrderId=q.StoreOrderId});
                w.LastStatus="Автомобиль "+q.VehicleId+" занял док "+(dock+1)+".";
            }
        }

        private void Awake() { production = GetComponent<ProductionChainSystem>(); fleet = GetComponent<FleetManagementSystem>(); TrySubscribeClock(); }
        private void OnEnable() { TrySubscribeClock(); }
        private void OnDisable() { if (clock != null) clock.OnDayChanged -= HandleDayChanged; clock = null; }
        private void UpdateClockSubscription() { if (clock == null) TrySubscribeClock(); }
        private void TrySubscribeClock()
        {
            MyCity.Core.GameClock current = MyCity.Core.GameClock.Instance;
            if (current == null || current == clock) return;
            if (clock != null) clock.OnDayChanged -= HandleDayChanged;
            clock = current; clock.OnDayChanged += HandleDayChanged;
        }
        private void HandleDayChanged(int day) { SettleDailyWarehouseCosts(day); }
        public void SettleDailyWarehouseCosts(int day)
        {
            List<string> ids = new List<string>(warehouses.Keys); ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                WarehouseRecord w = Find(id); if (w == null || !w.IsOperating || day <= w.LastSettledDay) continue;
                Normalize(w);
                long stored = TotalUnits(w.Inventory);
                long cost = checked(Math.Max(0L, w.DailyBaseOperatingCost) + checked(stored * Math.Max(0L, w.DailyCostPerStoredUnit)));
                w.LastDailyOperatingCost = cost; w.CumulativeOperatingCosts = checked(w.CumulativeOperatingCosts + cost); w.LastSettledDay = day;
                if (cost > 0 && OnWarehouseOperatingCostCharged != null)
                    OnWarehouseOperatingCostCharged(cost, w.DisplayName + ": ежедневные расходы склада " + cost.ToString("N0") + " ₽ (база и хранение " + stored + " ед.).");
            }
        }

        public void SetFleetManagementSystem(FleetManagementSystem system) { fleet = system; }

        public void RegisterStoreWorkplace(WorkplaceRecord workplace)
        {
            if (workplace == null || string.IsNullOrEmpty(workplace.BuildingId)) return;
            storeWorkplaces[workplace.BuildingId] = workplace;
        }

        /// <summary>Sets the quarantined quantity within a stock lot while keeping aggregate saleable stock in sync.</summary>
        public bool SetStoreLotQuarantinedUnits(WorkplaceRecord workplace, string lotId, int targetQuarantinedUnits, int day, string reason, out string error)
        {
            return SetStoreLotQuarantinedUnits(workplace, lotId, targetQuarantinedUnits, day, reason, string.Empty, out error);
        }

        public bool SetStoreLotQuarantinedUnits(WorkplaceRecord workplace, string lotId, int targetQuarantinedUnits, int day, string reason, string defectCategory, out string error)
        {
            error = string.Empty;
            if (workplace == null || workplace.StockLots == null) { error = "Партии магазина не найдены."; return false; }
            StoreStockLotRecord lot = workplace.StockLots.Find(x => x != null && x.LotId == lotId);
            if (lot == null) { error = "Партия не найдена."; return false; }
            int remaining = Math.Max(0, lot.RemainingUnits);
            int current = lot.IsQuarantined ? remaining : Math.Max(0, Math.Min(remaining, lot.QuarantinedUnits));
            int reserved = Math.Max(0, lot.ReservedForReturnUnits);
            int maxQuarantine = Math.Max(0, remaining - reserved);
            int target = Math.Max(0, Math.Min(maxQuarantine, targetQuarantinedUnits));
            if (target == current) { error = "Количество в карантине не изменилось."; return false; }
            StoreInventoryRecord item = workplace.Inventory != null ? workplace.Inventory.Find(x => x != null && x.ProductId == lot.ProductId) : null;
            if (item == null) { error = "Товар партии отсутствует в ассортименте."; return false; }
            int delta = target - current;
            string recordedCategory = !string.IsNullOrEmpty(defectCategory) ? defectCategory : (lot.DefectCategory ?? string.Empty);
            if (delta > 0)
            {
                if (item.StockUnits < delta) { error = "Доступный остаток меньше количества для карантина."; return false; }
                item.StockUnits -= delta;
            }
            else item.StockUnits = checked(item.StockUnits + -delta);
            lot.IsQuarantined = false;
            lot.QuarantinedUnits = target;
            lot.Status = target > 0 ? (target == remaining ? "Карантин качества" : "Частичный карантин качества") : (lot.ExpiryDay < day ? "Просрочена · проверка завершена" : "В продаже");
            lot.QualityInspectionDay = Math.Max(0, day);
            lot.QualityReason = reason ?? string.Empty;
            if (delta > 0 && !string.IsNullOrEmpty(defectCategory)) lot.DefectCategory = defectCategory;
            if (target == 0) lot.DefectCategory = string.Empty;
            if (workplace.QualityInspectionHistory == null)
                workplace.QualityInspectionHistory = new List<StoreQualityInspectionRecord>();
            workplace.QualityInspectionHistory.Add(new StoreQualityInspectionRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                LotId = lot.LotId,
                ProductId = lot.ProductId,
                Day = Math.Max(0, day),
                PreviousQuarantinedUnits = current,
                NewQuarantinedUnits = target,
                ChangedUnits = Math.Abs(delta),
                Action = delta > 0 ? "Изоляция" : "Допуск",
                Reason = reason ?? string.Empty,
                DefectCategory = recordedCategory
            });
            return true;
        }

        /// <summary>Compatibility wrapper for saves and callers that quarantine an entire lot.</summary>
        public bool SetStoreLotQuarantine(WorkplaceRecord workplace, string lotId, bool quarantine, int day, string reason, out string error)
        {
            StoreStockLotRecord lot = workplace != null && workplace.StockLots != null ? workplace.StockLots.Find(x => x != null && x.LotId == lotId) : null;
            if (lot == null) { error = "Партия не найдена."; return false; }
            return SetStoreLotQuarantinedUnits(workplace, lotId, quarantine ? lot.RemainingUnits : 0, day, reason, out error);
        }

        /// <summary>Creates a supplier-return request and reserves saleable store stock without removing it yet.</summary>
        public bool TryCreateStoreReturn(WorkplaceRecord workplace, string productId, int units, string reason, string settlementType, int day, out string error)
        {
            error = string.Empty;
            if (workplace == null || string.IsNullOrEmpty(productId) || units <= 0) { error = "Некорректные параметры возврата."; return false; }
            if (reason != "Истёк срок годности" && reason != "Брак" && reason != "Излишки") { error = "Укажите причину возврата."; return false; }
            if (settlementType != "Возврат денег" && settlementType != "Зачёт") { error = "Укажите способ расчёта с поставщиком."; return false; }
            if (workplace.Inventory == null) { error = "Ассортимент магазина не найден."; return false; }
            StoreInventoryRecord item = workplace.Inventory.Find(x => x != null && x.ProductId == productId);
            if (item == null) { error = "Товар отсутствует в ассортименте магазина."; return false; }
            int available = reason == "Брак" ? 0 : Math.Max(0, item.StockUnits - Math.Max(0, item.ReservedForReturnUnits));
            if (reason == "Брак" && workplace.StockLots != null)
                foreach (StoreStockLotRecord qualityLot in workplace.StockLots)
                    if (qualityLot != null && qualityLot.ProductId == productId && (qualityLot.IsQuarantined || qualityLot.QuarantinedUnits > 0))
                        available += Math.Max(0, (qualityLot.IsQuarantined ? qualityLot.RemainingUnits : qualityLot.QuarantinedUnits) - qualityLot.ReservedForReturnUnits);
            if (units > available) { error = "Недостаточно доступного количества для выбранной причины. Доступно: " + available + " шт."; return false; }
            if (workplace.StockLots == null) workplace.StockLots = new List<StoreStockLotRecord>();
            int represented = 0;
            foreach (StoreStockLotRecord existing in workplace.StockLots)
                if (existing != null && existing.ProductId == item.ProductId) represented += Math.Max(0, existing.RemainingUnits);
            if (represented < item.StockUnits)
                workplace.StockLots.Add(new StoreStockLotRecord { LotId = "legacy-return-" + workplace.BuildingId + "-" + item.ProductId,
                    ProductId = item.ProductId, ReceivedDay = Math.Max(0, day), ExpiryDay = Math.Max(0, day) + 365,
                    RemainingUnits = item.StockUnits - represented, UnitCost = Math.Max(0, item.AverageUnitCost), Status = "Партия старого остатка" });
            List<StoreStockLotRecord> candidates = workplace.StockLots.FindAll(x => x != null && x.ProductId == item.ProductId &&
                x.RemainingUnits > x.ReservedForReturnUnits && (reason == "Брак" ? (x.IsQuarantined ? x.RemainingUnits : x.QuarantinedUnits) > x.ReservedForReturnUnits : !x.IsQuarantined && (reason != "Истёк срок годности" || x.ExpiryDay < day)));
            candidates.Sort((a,b) => { int c = a.ExpiryDay.CompareTo(b.ExpiryDay); return c != 0 ? c : a.ReceivedDay.CompareTo(b.ReceivedDay); });
            int lotAvailable = 0; foreach (StoreStockLotRecord lot in candidates) lotAvailable += lot.RemainingUnits - Math.Max(0, lot.ReservedForReturnUnits);
            if (units > lotAvailable) { error = reason == "Истёк срок годности" ? "В партиях нет указанного количества уже просроченного товара." : "Недостаточно свободного количества в партиях товара."; return false; }
            if (workplace.SupplierReturns == null) workplace.SupplierReturns = new List<StoreReturnRecord>();
            StoreReturnRecord record = new StoreReturnRecord { Id = Guid.NewGuid().ToString("N"), ProductId = item.ProductId, ProductDisplayName = item.DisplayName, SupplierId = GetStoreSupplierId(workplace.StoreTypeId), SupplierDisplayName = GetStoreSupplierName(workplace.StoreTypeId), Units = units, Reason = reason, SettlementType = settlementType, CreatedDay = Math.Max(0, day), Status = "Оформлен", TransportStatus = "Ожидает передачи перевозчику", IsQualityReturn = reason == "Брак", LotAllocations = new List<StoreReturnLotAllocationRecord>() };
            int left = units; long totalValue = 0;
            foreach (StoreStockLotRecord lot in candidates)
            {
                int eligible = reason == "Брак" ? (lot.IsQuarantined ? lot.RemainingUnits : Math.Max(0, lot.QuarantinedUnits)) : Math.Max(0, lot.RemainingUnits - Math.Max(0, lot.QuarantinedUnits));
                int take = Math.Min(left, eligible - Math.Max(0, lot.ReservedForReturnUnits));
                if (take <= 0) continue;
                lot.ReservedForReturnUnits = checked(lot.ReservedForReturnUnits + take);
                record.LotAllocations.Add(new StoreReturnLotAllocationRecord { LotId = lot.LotId, Units = take, IsQualityAllocation = reason == "Брак" });
                totalValue = checked(totalValue + (long)take * Math.Max(0, lot.UnitCost)); left -= take;
                if (left == 0) break;
            }
            record.UnitCost = units > 0 ? totalValue / units : 0; record.ReturnValue = totalValue;
            if (!record.IsQualityReturn) item.ReservedForReturnUnits = checked(Math.Max(0, item.ReservedForReturnUnits) + units);
            workplace.SupplierReturns.Add(record);
            return true;
        }

        /// <summary>Cancels a return before dispatch and releases its stock reservation.</summary>
        public bool CancelStoreReturn(WorkplaceRecord workplace, string returnId, out string error)
        {
            error = string.Empty; StoreReturnRecord record = FindStoreReturn(workplace, returnId);
            if (record == null || record.IsDispatched || record.IsSettled || record.IsCancelled) { error = "Этот возврат нельзя отменить."; return false; }
            StoreInventoryRecord item = workplace.Inventory != null ? workplace.Inventory.Find(x => x != null && x.ProductId == record.ProductId) : null;
            if (item != null && !record.IsQualityReturn) item.ReservedForReturnUnits = Math.Max(0, item.ReservedForReturnUnits - record.Units);
            ReleaseReturnLotReservations(workplace, record);
            record.IsCancelled = true; record.Status = "Отменён"; record.TransportStatus = "Передача отменена";
            return true;
        }

        /// <summary>Hands a return shipment to the reverse-logistics workflow and removes it from saleable stock.</summary>
        public bool DispatchStoreReturn(WorkplaceRecord workplace, string returnId, int day, out string error)
        {
            error = string.Empty; StoreReturnRecord record = FindStoreReturn(workplace, returnId);
            if (record == null || record.IsDispatched || record.IsSettled || record.IsCancelled) { error = "Возврат не найден, отменён или уже передан."; return false; }
            StoreInventoryRecord item = workplace.Inventory != null ? workplace.Inventory.Find(x => x != null && x.ProductId == record.ProductId) : null;
            if (item == null) { error = "Товар отсутствует в ассортименте магазина."; return false; }
            int saleableReturnUnits = 0;
            if (record.LotAllocations != null)
                foreach (StoreReturnLotAllocationRecord allocation in record.LotAllocations)
                {
                    StoreStockLotRecord allocatedLot = workplace.StockLots != null ? workplace.StockLots.Find(x => x != null && x.LotId == allocation.LotId) : null;
                    if (allocatedLot != null && !allocation.IsQualityAllocation) saleableReturnUnits += allocation.Units;
                }
            if (item.StockUnits < saleableReturnUnits || (!record.IsQualityReturn && item.ReservedForReturnUnits < record.Units)) { error = "Остаток изменился: передача возврата невозможна."; return false; }
            if (fleet == null) fleet = GetComponent<FleetManagementSystem>();
            if (fleet == null) { error = "Автопарк не подключён."; return false; }
            Vector2Int origin = new Vector2Int(workplace.RoadNodeX, workplace.RoadNodeZ);
            WarehouseRecord destination = null; List<Vector2Int> bestRoute = null; int bestDistance = int.MaxValue;
            foreach (WarehouseRecord candidate in warehouses.Values)
            {
                if (candidate == null || !candidate.IsOperating) continue;
                List<Vector2Int> candidateRoute;
                if (!RoadRoutePlanner.TryBuildRoadPath(origin, new Vector2Int(candidate.RoadNodeX, candidate.RoadNodeZ), out candidateRoute)) continue;
                int distance = RoadRoutePlanner.GetRoadDistanceKm(candidateRoute);
                if (distance < bestDistance || (distance == bestDistance && (destination == null || string.CompareOrdinal(candidate.Id, destination.Id) < 0)))
                { destination = candidate; bestRoute = candidateRoute; bestDistance = distance; }
            }
            if (destination == null || bestRoute == null) { error = "Нет доступного дорожного маршрута до распределительного узла поставщика."; return false; }
            string transferId = "return-" + record.Id;
            if (record.LotAllocations == null || record.LotAllocations.Count == 0) { error = "Для возврата не найдены исходные партии товара. Отмените и оформите возврат заново."; return false; }
            foreach (StoreReturnLotAllocationRecord allocation in record.LotAllocations)
            {
                StoreStockLotRecord lot = workplace.StockLots != null ? workplace.StockLots.Find(x => x != null && x.LotId == allocation.LotId) : null;
                if (lot == null || lot.RemainingUnits < allocation.Units || lot.ReservedForReturnUnits < allocation.Units) { error = "Остаток исходной партии изменился. Передача невозможна."; return false; }
            }
            FleetVehicleRecord assigned;
            if (!fleet.TryAssign(transferId, record.Units, out assigned, out error)) return false;
            foreach (StoreReturnLotAllocationRecord allocation in record.LotAllocations)
            {
                StoreStockLotRecord lot = workplace.StockLots.Find(x => x != null && x.LotId == allocation.LotId);
                lot.RemainingUnits -= allocation.Units; lot.ReservedForReturnUnits -= allocation.Units;
                if (allocation.IsQualityAllocation && !lot.IsQuarantined) lot.QuarantinedUnits = Math.Max(0, lot.QuarantinedUnits - allocation.Units);
                if (lot.RemainingUnits == 0) lot.Status = "Полностью возвращена поставщику";
            }
            item.StockUnits -= saleableReturnUnits;
            if (!record.IsQualityReturn) item.ReservedForReturnUnits -= record.Units;
            record.FleetVehicleId = assigned.Id; record.VehicleTransferId = transferId; record.DestinationWarehouseId = destination.Id;
            record.RouteDistanceKm = bestDistance; record.CurrentPathIndex = 0; record.MinutesOnCurrentSegment = 0; record.TravelMinutesPerSegment = 2;
            record.RoadPath = new List<RoadPathNodeRecord>();
            if (bestRoute.Count == 1) bestRoute.Add(bestRoute[0]);
            foreach (Vector2Int node in bestRoute) record.RoadPath.Add(new RoadPathNodeRecord { X = node.x, Z = node.y });
            record.IsDispatched = true; record.DispatchedDay = Math.Max(0, day); record.Status = "Передан в обратную логистику"; record.TransportStatus = "Ожидает отправления";
            return true;
        }

        /// <summary>Confirms supplier receipt and posts the agreed refund or supplier credit exactly once.</summary>
        public bool ConfirmStoreReturnSettlement(WorkplaceRecord workplace, string returnId, int day, out string error)
        {
            error = string.Empty; StoreReturnRecord record = FindStoreReturn(workplace, returnId);
            if (record == null || !record.IsDispatched || record.IsSettled || record.TransportStatus != "Ожидает подтверждения поставщика") { error = "Возврат ещё не доставлен в распределительный узел или уже закрыт."; return false; }
            record.IsSupplierAccepted = true; record.IsSettled = true; record.CompletedDay = Math.Max(0, day); record.TransportStatus = "Доставлен поставщику"; record.Status = "Закрыт";
            if (record.SettlementType == "Возврат денег") workplace.OperatingBalance = checked(workplace.OperatingBalance + record.ReturnValue);
            else workplace.SupplierCreditBalance = checked(workplace.SupplierCreditBalance + record.ReturnValue);
            return true;
        }

        private static void ReleaseReturnLotReservations(WorkplaceRecord workplace, StoreReturnRecord record)
        {
            if (workplace == null || workplace.StockLots == null || record == null || record.LotAllocations == null) return;
            foreach (StoreReturnLotAllocationRecord allocation in record.LotAllocations)
            {
                StoreStockLotRecord lot = workplace.StockLots.Find(x => x != null && x.LotId == allocation.LotId);
                if (lot != null) lot.ReservedForReturnUnits = Math.Max(0, lot.ReservedForReturnUnits - allocation.Units);
            }
        }

        private void AdvanceStoreReturns(int day)
        {
            if (fleet == null) fleet = GetComponent<FleetManagementSystem>();
            List<string> ids = new List<string>(storeWorkplaces.Keys); ids.Sort(StringComparer.Ordinal);
            foreach (string buildingId in ids)
            {
                WorkplaceRecord workplace = storeWorkplaces[buildingId];
                if (workplace == null || workplace.SupplierReturns == null) continue;
                foreach (StoreReturnRecord record in workplace.SupplierReturns)
                {
                    if (record == null || !record.IsDispatched || record.IsSettled || record.IsCancelled ||
                        record.TransportStatus == "Ожидает подтверждения поставщика" || record.TransportStatus == "Доставлен поставщику" ||
                        record.RoadPath == null || record.RoadPath.Count < 2) continue;
                    record.TransportStatus = "В пути к распределительному узлу";
                    record.MinutesOnCurrentSegment++;
                    if (record.MinutesOnCurrentSegment < Math.Max(1, record.TravelMinutesPerSegment)) continue;
                    record.MinutesOnCurrentSegment = 0; record.CurrentPathIndex++;
                    if (record.CurrentPathIndex >= record.RoadPath.Count - 1)
                    {
                        record.CurrentPathIndex = record.RoadPath.Count - 1;
                        FleetTransactionRecord tx = fleet != null ? fleet.CompleteTrip(record.FleetVehicleId, record.VehicleTransferId, record.RouteDistanceKm, day, false) : null;
                        if (tx != null) { record.ReverseFreightCost = tx.TotalCost; record.IsFreightSettled = true; workplace.OperatingBalance = checked(workplace.OperatingBalance - tx.TotalCost); }
                        record.TransportStatus = "Ожидает подтверждения поставщика";
                        record.Status = "Доставлен в распределительный узел";
                    }
                }
            }
        }

        private static StoreReturnRecord FindStoreReturn(WorkplaceRecord workplace, string returnId)
        { return workplace != null && workplace.SupplierReturns != null ? workplace.SupplierReturns.Find(x => x != null && x.Id == returnId) : null; }

        private static string GetStoreSupplierId(string storeTypeId)
        {
            switch (storeTypeId) { case "grocery": return "food_wholesaler"; case "electronics": return "electronics_distributor"; case "clothing": return "clothing_wholesaler"; case "furniture": return "furniture_warehouse"; case "cosmetics": return "household_goods_supplier"; case "books": return "book_distributor"; default: return "general_supplier"; }
        }
        private static string GetStoreSupplierName(string storeTypeId)
        {
            switch (storeTypeId) { case "grocery": return "Продуктовая оптовая база"; case "electronics": return "Дистрибьютор электроники"; case "clothing": return "Оптовый склад одежды"; case "furniture": return "Мебельный распределительный центр"; case "cosmetics": return "Поставщик косметики и бытовой химии"; case "books": return "Книжный дистрибьютор"; default: return "Универсальный поставщик"; }
        }

        /// <summary>Starts a physical warehouse-to-store order. All resources are reserved before the order is committed.</summary>
        public bool TryDispatchStoreOrder(WorkplaceRecord workplace, StoreDeliveryRecord delivery, int day, out string error)
        {
            error = string.Empty;
            if (workplace == null || delivery == null || string.IsNullOrEmpty(workplace.BuildingId) ||
                string.IsNullOrEmpty(delivery.Id) || string.IsNullOrEmpty(delivery.ProductId) || delivery.Units <= 0)
            { error = "Некорректный магазинный заказ."; return false; }
            if (fleet == null) fleet = GetComponent<FleetManagementSystem>();
            if (fleet == null) { error = "Автопарк не подключён."; return false; }
            Vector2Int storeNode = new Vector2Int(workplace.RoadNodeX, workplace.RoadNodeZ);
            WarehouseRecord source; List<Vector2Int> route;
            if (!TryFindBestWarehouseForStoreOrder(delivery.ProductId, delivery.Units, storeNode, out source, out route, out error)) return false;
            string reservationId = "store-" + delivery.Id;
            if (!TryReserveStock(source.Id, delivery.ProductId, delivery.Units, reservationId, out error)) return false;
            FleetVehicleRecord vehicle;
            if (!fleet.TryAssign(delivery.Id, delivery.Units, out vehicle, out error))
            { ReleaseReservedStock(source.Id, reservationId); return false; }
            Normalize(source);
            if (source.DockQueue.Exists(x => x != null && x.StoreOrderId == delivery.Id) || source.ActiveDockAssignments.Exists(x => x != null && x.StoreOrderId == delivery.Id))
            { fleet.Release(vehicle.Id, delivery.Id); ReleaseReservedStock(source.Id, reservationId); error = "Заказ уже поставлен в складскую очередь."; return false; }
            delivery.WarehouseId = source.Id;
            delivery.ReservationId = reservationId;
            delivery.FleetVehicleId = vehicle.Id;
            delivery.LogisticsStatus = "Ожидает погрузки";
            delivery.RouteDistanceKm = RoadRoutePlanner.GetRoadDistanceKm(route);
            delivery.CurrentPathIndex = 0;
            delivery.MinutesOnCurrentSegment = 0;
            delivery.TravelMinutesPerSegment = 2;
            delivery.DispatchedDay = Math.Max(0, day);
            delivery.RoadPath = new List<RoadPathNodeRecord>();
            if (route.Count == 1) route.Add(route[0]);
            foreach (Vector2Int node in route) delivery.RoadPath.Add(new RoadPathNodeRecord { X = node.x, Z = node.y });
            source.DockQueue.Add(new WarehouseDockQueueRecord
            {
                VehicleId = delivery.Id, Operation = "Погрузка", RequestedUnits = delivery.Units,
                EnqueuedAtDay = Math.Max(0, day), Priority = 1, SourceWarehouseId = source.Id,
                ProductId = delivery.ProductId, StoreOrderId = delivery.Id
            });
            source.DockQueue.Sort((a,b) => { int c=b.Priority.CompareTo(a.Priority); if(c!=0)return c; c=a.EnqueuedAtDay.CompareTo(b.EnqueuedAtDay); return c!=0?c:string.CompareOrdinal(a.VehicleId,b.VehicleId); });
            storeWorkplaces[workplace.BuildingId] = workplace;
            source.LastStatus = "Магазинный заказ " + delivery.Id + " зарезервирован и поставлен в очередь на погрузку.";
            AssignWaitingVehicles(source, day);
            return true;
        }

        /// <summary>Safely cancels a store shipment. Before loading, reservations and fleet assignment are released;
        /// after loading, the truck is routed back and the cargo is restored only when it reaches the source warehouse.</summary>
        public bool TryCancelStoreOrder(WorkplaceRecord workplace, StoreDeliveryRecord delivery, int day, out string error)
        {
            error = string.Empty;
            if (workplace == null || delivery == null || string.IsNullOrEmpty(delivery.Id))
            { error = "Не указан магазинный заказ."; return false; }
            if (delivery.IsAccepted || delivery.LogisticsStatus == "Завершён")
            { error = "Принятый или завершённый заказ нельзя отменить."; return false; }
            if (delivery.IsCancelled) return true;

            WarehouseRecord source = Find(delivery.WarehouseId);
            if (string.IsNullOrEmpty(delivery.WarehouseId) || source == null)
            { error = "У заказа нет действующего складского отправления."; return false; }
            Normalize(source);

            // Cargo has left the dock: do not recreate stock immediately. Return it physically first.
            if (delivery.LogisticsStatus == "В пути в магазин" || delivery.LogisticsStatus == "Возвращается на склад")
            {
                delivery.IsCancelled = true;
                delivery.LogisticsStatus = "Возвращается на склад";
                delivery.CurrentPathIndex = Math.Max(0, Math.Min(delivery.CurrentPathIndex, delivery.RoadPath != null ? delivery.RoadPath.Count - 1 : 0));
                source.LastStatus = "Отмена заказа " + delivery.Id + ": грузовик возвращается с товаром на склад.";
                return true;
            }

            // The truck is still queued or being serviced at the loading dock; no stock has been consumed.
            source.DockQueue.RemoveAll(x => x != null && x.StoreOrderId == delivery.Id);
            source.ActiveDockAssignments.RemoveAll(x => x != null && x.StoreOrderId == delivery.Id);
            ReleaseReservedStock(source.Id, delivery.ReservationId);
            if (fleet == null) fleet = GetComponent<FleetManagementSystem>();
            if (fleet != null && !string.IsNullOrEmpty(delivery.FleetVehicleId)) fleet.Release(delivery.FleetVehicleId, delivery.Id);
            delivery.IsCancelled = true;
            delivery.LogisticsStatus = "Отменён до погрузки";
            delivery.CompletedDay = Math.Max(0, day);
            source.LastStatus = "Заказ " + delivery.Id + " отменён до погрузки; резерв снят, автомобиль освобождён.";
            AssignWaitingVehicles(source, day);
            return true;
        }

        private void CompleteStoreLoading(WarehouseRecord warehouse, WarehouseDockAssignmentRecord assignment, int day)
        {
            WorkplaceRecord workplace; StoreDeliveryRecord delivery;
            if (!TryFindStoreOrder(assignment.StoreOrderId, out workplace, out delivery))
            { warehouse.LastStatus = "Не найден магазинный заказ " + assignment.StoreOrderId + "."; return; }
            if (delivery.IsCancelled || delivery.IsAccepted) { warehouse.LastStatus = "Магазинный заказ уже отменён или принят."; return; }
            long unitCost; string error;
            if (!TryConsumeReservedStock(delivery.WarehouseId, delivery.ReservationId, out unitCost, out error))
            {
                ReleaseReservedStock(delivery.WarehouseId, delivery.ReservationId);
                fleet.Release(delivery.FleetVehicleId, delivery.Id);
                delivery.IsCancelled = true; delivery.LogisticsStatus = "Отменён: ошибка погрузки";
                warehouse.LastStatus = "Погрузка отменена: " + error;
                return;
            }
            delivery.CargoUnitCostAtDispatch = unitCost;
            delivery.LogisticsStatus = "В пути в магазин";
            delivery.CurrentPathIndex = 0; delivery.MinutesOnCurrentSegment = 0;
            warehouse.LastStatus = "Заказ " + delivery.Id + ": погрузка завершена, грузовик отправлен в магазин.";
        }

        private bool TryFindStoreOrder(string orderId, out WorkplaceRecord workplace, out StoreDeliveryRecord delivery)
        {
            workplace = null; delivery = null;
            foreach (WorkplaceRecord candidate in storeWorkplaces.Values)
            {
                if (candidate == null || candidate.PendingDeliveries == null) continue;
                StoreDeliveryRecord found = candidate.PendingDeliveries.Find(x => x != null && x.Id == orderId);
                if (found != null) { workplace = candidate; delivery = found; return true; }
            }
            return false;
        }

        private void AdvanceStoreShipments(int day)
        {
            List<string> buildingIds = new List<string>(storeWorkplaces.Keys); buildingIds.Sort(StringComparer.Ordinal);
            foreach (string buildingId in buildingIds)
            {
                WorkplaceRecord workplace = storeWorkplaces[buildingId];
                if (workplace == null || workplace.PendingDeliveries == null) continue;
                foreach (StoreDeliveryRecord delivery in workplace.PendingDeliveries)
                {
                    if (delivery == null || string.IsNullOrEmpty(delivery.WarehouseId) ||
                        (delivery.IsCancelled && delivery.LogisticsStatus != "Возвращается на склад") ||
                        (delivery.IsAccepted && delivery.LogisticsStatus != "Возвращается на склад") ||
                        delivery.RoadPath == null || delivery.RoadPath.Count < 2) continue;
                    if (delivery.LogisticsStatus != "В пути в магазин" && delivery.LogisticsStatus != "Возвращается на склад") continue;
                    delivery.MinutesOnCurrentSegment++;
                    if (delivery.MinutesOnCurrentSegment < Math.Max(1, delivery.TravelMinutesPerSegment)) continue;
                    delivery.MinutesOnCurrentSegment = 0;
                    if (delivery.LogisticsStatus == "В пути в магазин")
                    {
                        delivery.CurrentPathIndex++;
                        if (delivery.CurrentPathIndex >= delivery.RoadPath.Count - 1)
                        {
                            // Physical arrival is not automatic acceptance: the player must
                            // reconcile the received and damaged quantities in the store receipt UI.
                            delivery.LogisticsStatus = "Ожидает приёмки";
                            delivery.CurrentPathIndex = delivery.RoadPath.Count - 1;
                            delivery.MinutesOnCurrentSegment = 0;
                        }
                    }
                    else
                    {
                        delivery.CurrentPathIndex--;
                        if (delivery.CurrentPathIndex <= 0)
                        {
                            delivery.CurrentPathIndex = 0;
                            if (delivery.IsCancelled && !delivery.IsAccepted)
                            {
                                WarehouseRecord source = Find(delivery.WarehouseId);
                                string returnError;
                                if (source == null || !TryStore(delivery.WarehouseId, delivery.ProductId, delivery.Units, delivery.CargoUnitCostAtDispatch, out returnError))
                                {
                                    // Keep the truck and cargo in the return state; retry when storage capacity is available.
                                    delivery.LogisticsStatus = "Возвращается на склад";
                                    delivery.CurrentPathIndex = 0;
                                    continue;
                                }
                                delivery.LogisticsStatus = "Отменён: груз возвращён на склад";
                                delivery.CompletedDay = Math.Max(0, day);
                                int travelledKm = 0;
                                if (delivery.RoadPath != null && delivery.RoadPath.Count > 1)
                                {
                                    List<Vector2Int> travelledPath = new List<Vector2Int>();
                                    int lastTravelledIndex = Mathf.Clamp(delivery.CurrentPathIndex, 0, delivery.RoadPath.Count - 1);
                                    for (int pathIndex = 0; pathIndex <= lastTravelledIndex; pathIndex++)
                                        travelledPath.Add(new Vector2Int(delivery.RoadPath[pathIndex].X, delivery.RoadPath[pathIndex].Z));
                                    travelledKm = RoadRoutePlanner.GetRoadDistanceKm(travelledPath) * 2;
                                }
                                if (fleet != null) fleet.CompleteTrip(delivery.FleetVehicleId, delivery.Id, travelledKm, day, true);
                                CityEconomySystem economy = GetComponent<CityEconomySystem>();
                                if (economy != null) economy.RefundCancelledStoreOrder(workplace, delivery);
                                if (source != null) source.LastStatus = "Отменённый заказ " + delivery.Id + ": груз возвращён на склад.";
                            }
                            else
                            {
                                delivery.LogisticsStatus = "Завершён";
                                delivery.CompletedDay = Math.Max(0, day);
                                if (fleet != null) fleet.CompleteTrip(delivery.FleetVehicleId, delivery.Id, delivery.RouteDistanceKm * 2, day, false);
                            }
                        }
                    }
                }
            }
        }

        private bool AcceptStoreShipment(WorkplaceRecord workplace, StoreDeliveryRecord delivery, int day)
        {
            if (workplace == null || delivery == null || delivery.IsAccepted || workplace.Inventory == null) return false;
            StoreInventoryRecord item = workplace.Inventory.Find(x => x != null && x.ProductId == delivery.ProductId);
            if (item == null) return false;
            FleetVehicleRecord vehicle = fleet != null ? fleet.Find(delivery.FleetVehicleId) : null;
            if (vehicle == null) return false;

            int receivedUnits = delivery.HasReportedReceivedUnits
                ? delivery.ReportedReceivedUnits
                : Math.Max(0, delivery.Units);
            int damagedUnits = delivery.HasReportedDamagedUnits
                ? delivery.ReportedDamagedUnits
                : 0;
            // Never silently clamp an inconsistent report: leave the truck awaiting receipt so
            // the receipt data can be corrected through the dedicated correction workflow.
            if (receivedUnits < 0 || receivedUnits > delivery.Units || damagedUnits < 0 || damagedUnits > receivedUnits)
                return false;
            int saleableUnits = receivedUnits - damagedUnits;
            int missingUnits = Math.Max(0, delivery.Units - receivedUnits);
            int roundTripKm = Math.Max(0, delivery.RouteDistanceKm * 2);
            long fuel = (long)Math.Ceiling(vehicle.FuelLitresPer100Km * roundTripKm / 100f * FleetManagementSystem.FuelPricePerLitre);
            long driver = (long)roundTripKm * vehicle.DriverCostPerKm;
            long maintenance = (long)roundTripKm * vehicle.MaintenanceCostPerKm;
            delivery.FreightCost = checked(fuel + driver + maintenance);

            // Reconcile the exact amounts paid for goods and the actual completed-trip freight.
            // Integer remainders are assigned deterministically so every ruble is accounted for.
            long grossPurchase = Math.Max(0, delivery.TotalCost);
            long shortageRefund = delivery.Units > 0
                ? checked((long)((decimal)grossPurchase * missingUnits / delivery.Units))
                : 0;
            long netPurchase = Math.Max(0, grossPurchase - shortageRefund);
            long goodsForInventory = receivedUnits > 0
                ? checked((long)((decimal)netPurchase * saleableUnits / receivedUnits))
                : 0;
            long goodsForDamage = Math.Max(0, netPurchase - goodsForInventory);
            long freightForInventory = receivedUnits > 0
                ? checked((long)((decimal)delivery.FreightCost * saleableUnits / receivedUnits))
                : 0;
            long freightForDamage = damagedUnits > 0
                ? Math.Max(0, delivery.FreightCost - freightForInventory)
                : 0;
            long freightUnallocated = receivedUnits == 0 ? Math.Max(0, delivery.FreightCost) : 0;
            // When no damaged units exist, all freight belongs to saleable stock. When no units
            // arrived, the trip remains a pure logistics expense and is not capitalized.
            if (damagedUnits == 0 && receivedUnits > 0)
                freightForInventory = Math.Max(0, delivery.FreightCost);

            long lotValue = checked(goodsForInventory + freightForInventory);
            if (saleableUnits > 0)
            {
                int oldUnits = Math.Max(0, item.StockUnits);
                long oldValue = checked((long)oldUnits * Math.Max(0, item.AverageUnitCost));
                int newUnits = checked(oldUnits + saleableUnits);
                item.AverageUnitCost = newUnits > 0 ? checked(oldValue + lotValue) / newUnits : 0;
                item.StockUnits = newUnits;
                if (workplace.StockLots == null) workplace.StockLots = new List<StoreStockLotRecord>();
                int receivedDay = Math.Max(1, delivery.CompletedDay > 0 ? delivery.CompletedDay : delivery.ArrivalDay);
                int shelfLife = item.CategoryId == "food" ? 14 : item.CategoryId == "cosmetics" ? 540 :
                    (item.CategoryId == "clothing" ? 1825 : (item.CategoryId == "electronics" || item.CategoryId == "books" ? 3650 : 365));
                workplace.StockLots.Add(new StoreStockLotRecord { LotId = "delivery-" + delivery.Id, ProductId = delivery.ProductId,
                    SourceDeliveryId = delivery.Id, ReceivedDay = receivedDay, ExpiryDay = receivedDay + shelfLife,
                    RemainingUnits = saleableUnits, UnitCost = saleableUnits > 0 ? lotValue / saleableUnits : 0, Status = "В продаже" });
            }

            delivery.GrossPurchaseValue = grossPurchase;
            delivery.ShortageRefundValue = shortageRefund;
            delivery.NetPurchaseValue = netPurchase;
            delivery.GoodsCostAllocatedToInventory = goodsForInventory;
            delivery.GoodsCostWrittenOff = goodsForDamage;
            delivery.FreightCostCharged = Math.Max(0, delivery.FreightCost);
            delivery.FreightAllocatedToInventory = freightForInventory;
            delivery.FreightAllocatedToDamage = freightForDamage;
            delivery.FreightUnallocatedExpense = freightUnallocated;
            delivery.IsFreightSettled = true;
            delivery.AcceptedUnits = saleableUnits;
            delivery.DamagedUnits = damagedUnits;
            delivery.ShortUnits = missingUnits;
            delivery.DamageWriteOffUnits = damagedUnits;
            delivery.DamageWriteOffValue = checked(goodsForDamage + freightForDamage);
            if (damagedUnits > 0 && string.IsNullOrEmpty(delivery.DamageReason))
                delivery.DamageReason = "Причина не указана";

            if (damagedUnits > 0 && missingUnits > 0)
                delivery.ReceiptStatus = "Принят с недостачей и повреждением";
            else if (damagedUnits > 0)
                delivery.ReceiptStatus = "Принят с повреждением";
            else if (missingUnits > 0)
                delivery.ReceiptStatus = "Принят с недостачей";
            else
                delivery.ReceiptStatus = "Принят полностью";

            delivery.IsAccepted = true;
            delivery.IsDelivered = true;
            delivery.LogisticsStatus = delivery.ReceiptStatus;
            delivery.CompletedDay = Math.Max(0, day);

            CityEconomySystem economy = GetComponent<CityEconomySystem>();
            if (damagedUnits > 0)
            {
                if (economy != null)
                    economy.RecordStoreShipmentDamageWriteOff(workplace, delivery, day);
                else
                {
                    // Keep the persistent write-off classification even if the economy component
                    // is temporarily absent from this scene object.
                    if (workplace.DamageLossLastProcessedDay != Math.Max(0, day))
                    {
                        workplace.LastDailyDamageLoss = 0;
                        workplace.DamageLossLastProcessedDay = Math.Max(0, day);
                    }
                    workplace.LastDailyDamageLoss = checked(workplace.LastDailyDamageLoss + delivery.DamageWriteOffValue);
                    workplace.CumulativeDamageLoss = checked(workplace.CumulativeDamageLoss + delivery.DamageWriteOffValue);
                    delivery.IsDamageWrittenOff = true;
                }
            }
            if (missingUnits > 0 && economy != null)
                economy.RefundStoreShipmentShortage(workplace, delivery);

            // Freight is an actual operating expense. Charge it once at receipt; the balance may
            // become negative, representing a payable rather than silently losing the expense.
            if (delivery.FreightCostCharged > 0)
            {
                workplace.OperatingBalance = checked(workplace.OperatingBalance - delivery.FreightCostCharged);
                workplace.LastDailyOperatingCost = checked(workplace.LastDailyOperatingCost + delivery.FreightCostCharged);
                workplace.CumulativeOperatingCosts = checked(workplace.CumulativeOperatingCosts + delivery.FreightCostCharged);
                CityEconomySystem finance = GetComponent<CityEconomySystem>();
                if (finance != null) finance.RecordStoreShipmentFreightCharge(workplace, delivery, day);
            }
            delivery.IsFinanciallyReconciled =
                delivery.GrossPurchaseValue == delivery.ShortageRefundValue + delivery.NetPurchaseValue &&
                delivery.NetPurchaseValue == delivery.GoodsCostAllocatedToInventory + delivery.GoodsCostWrittenOff &&
                delivery.FreightCostCharged == delivery.FreightAllocatedToInventory + delivery.FreightAllocatedToDamage + delivery.FreightUnallocatedExpense &&
                (delivery.FreightCostCharged <= 0 || delivery.IsFreightLedgerPosted) &&
                (missingUnits == 0 || delivery.IsShortageRefunded) &&
                (damagedUnits == 0 || delivery.IsDamageWrittenOff);
            return delivery.IsFinanciallyReconciled;
        }

        public void InitializeDefaults()
        {
            if (warehouses.Count > 0) return;
            Register(new WarehouseRecord
            {
                Id = "warehouse-regional-01",
                DisplayName = "Центральный распределительный склад",
                RoadNodeX = 0,
                RoadNodeZ = 0,
                CapacityUnits = 10000,
                DockCount = 2,
                DailyBaseOperatingCost = 1200,
                DailyCostPerStoredUnit = 1,
                IsOperating = true,
                Inventory = new List<WarehouseInventoryRecord>(),
                LastStatus = "Готов к работе"
            });
        }

        public bool Register(WarehouseRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.Id) || record.CapacityUnits <= 0 || record.DockCount <= 0)
                return false;
            Normalize(record);
            warehouses[record.Id] = record;
            return true;
        }

        public bool Remove(string id) { return !string.IsNullOrEmpty(id) && warehouses.Remove(id); }

        public WarehouseRecord Find(string id)
        {
            WarehouseRecord record;
            return !string.IsNullOrEmpty(id) && warehouses.TryGetValue(id, out record) ? record : null;
        }

        public int GetStock(string warehouseId, string productId)
        {
            WarehouseRecord warehouse = Find(warehouseId);
            WarehouseInventoryRecord item = warehouse != null ? FindItem(warehouse.Inventory, productId) : null;
            return item != null ? Math.Max(0, item.Units) : 0;
        }

        /// <summary>Physical stock not already committed to another outbound order.</summary>
        public int GetAvailableStock(string warehouseId, string productId)
        {
            WarehouseRecord warehouse = Find(warehouseId);
            WarehouseInventoryRecord item = warehouse != null ? FindItem(warehouse.Inventory, productId) : null;
            return item != null ? Math.Max(0, item.Units - Math.Max(0, item.ReservedUnits)) : 0;
        }

        /// <summary>Finds the nearest operating warehouse with enough unreserved stock and a valid road route.</summary>
        public bool TryFindBestWarehouseForStoreOrder(string productId, int units, Vector2Int destinationNode, out WarehouseRecord bestWarehouse, out List<Vector2Int> bestRoute, out string error)
        {
            bestWarehouse = null; bestRoute = null; error = string.Empty;
            if (string.IsNullOrEmpty(productId) || units <= 0) { error = "Некорректный товар или объём заказа."; return false; }
            int bestDistance = int.MaxValue;
            List<string> ids = new List<string>(warehouses.Keys); ids.Sort(StringComparer.Ordinal);
            for (int i = 0; i < ids.Count; i++)
            {
                WarehouseRecord candidate = warehouses[ids[i]];
                if (candidate == null || !candidate.IsOperating || GetAvailableStock(candidate.Id, productId) < units) continue;
                List<Vector2Int> route;
                if (!RoadRoutePlanner.TryBuildRoadPath(new Vector2Int(candidate.RoadNodeX, candidate.RoadNodeZ), destinationNode, out route) || route == null || route.Count < 2) continue;
                int distance = RoadRoutePlanner.GetRoadDistanceKm(route);
                if (distance < bestDistance) { bestDistance = distance; bestWarehouse = candidate; bestRoute = route; }
            }
            if (bestWarehouse == null) { error = "Нет работающего склада с достаточным свободным запасом и доступным дорожным маршрутом."; return false; }
            return true;
        }

        /// <summary>Reserves stock atomically for a pending shipment. Repeating the same reservation key is idempotent.</summary>
        public bool TryReserveStock(string warehouseId, string productId, int units, string reservationId, out string error)
        {
            error = string.Empty;
            WarehouseRecord warehouse = Find(warehouseId);
            if (warehouse == null || !warehouse.IsOperating) { error = "Склад недоступен или остановлен."; return false; }
            if (string.IsNullOrEmpty(productId) || string.IsNullOrEmpty(reservationId) || units <= 0) { error = "Некорректные параметры резерва."; return false; }
            Normalize(warehouse);
            WarehouseInventoryRecord item = FindItem(warehouse.Inventory, productId);
            if (item == null) { error = "Товар отсутствует на складе."; return false; }
            // A reservation ledger is kept on the warehouse record so it survives serialization.
            // The same key cannot reserve twice; callers must use a stable order id.
            WarehouseStockReservationRecord existing = warehouse.StockReservations.Find(x => x != null && x.ReservationId == reservationId);
            if (existing != null)
            {
                if (existing.ProductId == productId && existing.Units == units) return true;
                error = "Идентификатор резерва уже занят другим товаром или количеством."; return false;
            }
            foreach (WarehouseRecord otherWarehouse in warehouses.Values)
                if (otherWarehouse != null && otherWarehouse.Id != warehouseId && otherWarehouse.StockReservations != null && otherWarehouse.StockReservations.Exists(x => x != null && x.ReservationId == reservationId))
                { error = "Идентификатор резерва уже используется на другом складе."; return false; }
            if (GetAvailableStock(warehouseId, productId) < units) { error = "Недостаточно свободного, не зарезервированного товара."; return false; }
            item.ReservedUnits = checked(Math.Max(0, item.ReservedUnits) + units);
            warehouse.StockReservations.Add(new WarehouseStockReservationRecord { ReservationId = reservationId, ProductId = productId, Units = units, CreatedDay = clock != null ? clock.Day : 0 });
            warehouse.LastStatus = "Зарезервировано " + units + " ед. " + productId + " под заказ " + reservationId + ".";
            return true;
        }

        public bool ReleaseReservedStock(string warehouseId, string reservationId)
        {
            WarehouseRecord warehouse = Find(warehouseId);
            if (warehouse == null || string.IsNullOrEmpty(reservationId)) return false;
            WarehouseStockReservationRecord reservation = warehouse.StockReservations.Find(x => x != null && x.ReservationId == reservationId);
            if (reservation == null) return false;
            WarehouseInventoryRecord item = FindItem(warehouse.Inventory, reservation.ProductId);
            if (item != null) item.ReservedUnits = Math.Max(0, item.ReservedUnits - reservation.Units);
            warehouse.StockReservations.Remove(reservation);
            warehouse.LastStatus = "Снят резерв заказа " + reservationId + ".";
            return true;
        }

        /// <summary>Consumes previously reserved stock at dispatch; unreserved stock is never touched.</summary>
        public bool TryConsumeReservedStock(string warehouseId, string reservationId, out long averageUnitCost, out string error)
        {
            averageUnitCost = 0; error = string.Empty;
            if (string.IsNullOrEmpty(warehouseId) || string.IsNullOrEmpty(reservationId)) { error = "Не указан склад или идентификатор резерва."; return false; }
            WarehouseRecord warehouse = Find(warehouseId);
            if (warehouse == null) { error = "Склад не найден."; return false; }
            Normalize(warehouse);
            WarehouseStockReservationRecord reservation = warehouse.StockReservations.Find(x => x != null && x.ReservationId == reservationId);
            WarehouseInventoryRecord item = reservation != null ? FindItem(warehouse.Inventory, reservation.ProductId) : null;
            if (reservation == null || item == null || reservation.Units <= 0 || item.ReservedUnits < reservation.Units || item.Units < reservation.Units) { error = "Резерв или физический остаток недостаточен."; return false; }
            averageUnitCost = Math.Max(0, item.AverageUnitCost);
            item.ReservedUnits -= reservation.Units; item.Units -= reservation.Units; warehouse.StockReservations.Remove(reservation);
            if (item.Units == 0) item.AverageUnitCost = 0;
            warehouse.LastStatus = "Отпущен зарезервированный товар: " + reservation.ProductId + " — " + reservation.Units + " ед.";
            return true;
        }

        public int GetTotalStock(string warehouseId)
        {
            WarehouseRecord warehouse = Find(warehouseId);
            return warehouse != null ? TotalUnits(warehouse.Inventory) : 0;
        }

        public int GetFreeCapacity(string warehouseId)
        {
            WarehouseRecord warehouse = Find(warehouseId);
            return warehouse != null ? Math.Max(0, warehouse.CapacityUnits - TotalUnits(warehouse.Inventory)) : 0;
        }

        public bool TryStore(string warehouseId, string productId, int units, out string error)
        {
            return TryStore(warehouseId, productId, units, 0, out error);
        }

        /// <summary>Stores a lot and preserves its unit cost using weighted-average inventory valuation.</summary>
        public bool TryStore(string warehouseId, string productId, int units, long landedUnitCost, out string error)
        {
            error = string.Empty;
            WarehouseRecord warehouse = Find(warehouseId);
            if (warehouse == null || !warehouse.IsOperating) { error = "Склад недоступен или остановлен."; return false; }
            if (string.IsNullOrEmpty(productId) || units <= 0 || landedUnitCost < 0) { error = "Некорректное наименование товара, количество или себестоимость."; return false; }
            if (units > GetFreeCapacity(warehouseId)) { error = "Недостаточно свободной вместимости склада."; return false; }
            WarehouseInventoryRecord item = FindOrCreate(warehouse.Inventory, productId);
            int oldUnits = Math.Max(0, item.Units);
            long oldValue = checked((long)oldUnits * Math.Max(0, item.AverageUnitCost));
            long lotValue = checked((long)units * landedUnitCost);
            int totalUnits = checked(oldUnits + units);
            item.AverageUnitCost = totalUnits > 0 ? checked(oldValue + lotValue) / totalUnits : 0;
            item.Units = totalUnits;
            warehouse.LastStatus = "Принято на хранение: " + productId + " — " + units + " ед.; учтена себестоимость партии.";
            return true;
        }

        public bool TryWithdraw(string warehouseId, string productId, int units, out string error)
        {
            long ignored;
            return TryWithdraw(warehouseId, productId, units, out ignored, out error);
        }

        /// <summary>Withdraws stock and returns its current average unit cost for downstream costing.</summary>
        public bool TryWithdraw(string warehouseId, string productId, int units, out long averageUnitCost, out string error)
        {
            averageUnitCost = 0;
            error = string.Empty;
            WarehouseRecord warehouse = Find(warehouseId);
            if (warehouse == null || !warehouse.IsOperating) { error = "Склад недоступен или остановлен."; return false; }
            if (string.IsNullOrEmpty(productId) || units <= 0) { error = "Некорректное наименование товара или количество."; return false; }
            WarehouseInventoryRecord item = FindItem(warehouse.Inventory, productId);
            if (item == null || item.Units - Math.Max(0, item.ReservedUnits) < units) { error = "На складе недостаточно свободного, не зарезервированного товара."; return false; }
            averageUnitCost = Math.Max(0, item.AverageUnitCost);
            item.Units -= units;
            if (item.Units == 0) item.AverageUnitCost = 0;
            warehouse.LastStatus = "Отпущено со склада: " + productId + " — " + units + " ед.";
            return true;
        }

        public bool TrySupplyProduction(string warehouseId, string siteId, string productId, int units, out string error)
        {
            error = string.Empty;
            if (production == null) production = GetComponent<ProductionChainSystem>();
            if (production == null || production.FindSite(siteId) == null) { error = "Производственная площадка не найдена."; return false; }
            long unitCost;
            if (!TryWithdraw(warehouseId, productId, units, out unitCost, out error)) return false;
            try
            {
                if (production.AddInputStock(siteId, productId, units, unitCost)) return true;
            }
            catch (Exception ex) { error = "Не удалось передать сырьё: " + ex.Message; }
            string rollbackError;
            TryStore(warehouseId, productId, units, unitCost, out rollbackError);
            if (string.IsNullOrEmpty(error)) error = "Производственная площадка не приняла сырьё.";
            return false;
        }

        public bool TryStoreProductionOutput(string warehouseId, string siteId, string productId, int units, out string error)
        {
            error = string.Empty;
            if (production == null) production = GetComponent<ProductionChainSystem>();
            long unitCost;
            if (production == null || !production.TryRemoveOutputStock(siteId, productId, units, out unitCost))
            { error = "На производстве недостаточно готовой продукции."; return false; }
            if (TryStore(warehouseId, productId, units, unitCost, out error)) return true;
            production.AddOutputStock(siteId, productId, units, unitCost);
            return false;
        }

        public void Load(List<WarehouseRecord> saved)
        {
            warehouses.Clear();
            storeWorkplaces.Clear();
            if (saved != null) foreach (WarehouseRecord record in saved) Register(record);
            InitializeDefaults();
        }

        public void Export(List<WarehouseRecord> destination)
        {
            if (destination == null) return;
            destination.Clear();
            List<string> ids = new List<string>(warehouses.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids) destination.Add(warehouses[id]);
        }

        private static void Normalize(WarehouseRecord record)
        {
            if (record.Inventory == null) record.Inventory = new List<WarehouseInventoryRecord>();
            if (record.StockReservations == null) record.StockReservations = new List<WarehouseStockReservationRecord>();
            // Rebuild reserved totals from the persisted reservation ledger for backward compatibility.
            foreach (WarehouseInventoryRecord inventoryItem in record.Inventory) if (inventoryItem != null) inventoryItem.ReservedUnits = 0;
            for (int i = record.StockReservations.Count - 1; i >= 0; i--)
            {
                WarehouseStockReservationRecord reservation = record.StockReservations[i];
                if (reservation == null || string.IsNullOrEmpty(reservation.ReservationId) || string.IsNullOrEmpty(reservation.ProductId) || reservation.Units <= 0)
                { record.StockReservations.RemoveAt(i); continue; }
                WarehouseInventoryRecord reservedItem = FindOrCreate(record.Inventory, reservation.ProductId);
                reservedItem.ReservedUnits = checked(reservedItem.ReservedUnits + reservation.Units);
            }
            if (record.Transfers == null) record.Transfers = new List<WarehouseTransferRecord>();
            if (record.ActiveDockAssignments == null) record.ActiveDockAssignments = new List<WarehouseDockAssignmentRecord>();
            if (record.DockQueue == null) record.DockQueue = new List<WarehouseDockQueueRecord>();
            if (string.IsNullOrEmpty(record.WarehouseTypeId)) record.WarehouseTypeId = "distribution";
            if (record.LastStatus == null) record.LastStatus = "Готов к работе";
            if (record.DockCount <= 0) record.DockCount = 1;
            if (record.DailyBaseOperatingCost == 0 && record.DailyCostPerStoredUnit == 0)
            {
                if (record.WarehouseTypeId == "wholesale") { record.DailyBaseOperatingCost = 1800; record.DailyCostPerStoredUnit = 1; }
                else if (record.WarehouseTypeId == "industrial") { record.DailyBaseOperatingCost = 2400; record.DailyCostPerStoredUnit = 2; }
                else if (record.WarehouseTypeId == "fuel") { record.DailyBaseOperatingCost = 3200; record.DailyCostPerStoredUnit = 2; }
                else { record.DailyBaseOperatingCost = 1200; record.DailyCostPerStoredUnit = 1; }
            }
        }

        private static WarehouseInventoryRecord FindItem(List<WarehouseInventoryRecord> items, string id)
        { return items != null ? items.Find(x => x != null && x.ProductId == id) : null; }

        private static WarehouseInventoryRecord FindOrCreate(List<WarehouseInventoryRecord> items, string id)
        {
            WarehouseInventoryRecord item = FindItem(items, id);
            if (item != null) return item;
            item = new WarehouseInventoryRecord { ProductId = id, Units = 0, ReservedUnits = 0 };
            items.Add(item);
            return item;
        }

        private static int TotalUnits(List<WarehouseInventoryRecord> items)
        {
            long total = 0;
            if (items != null) foreach (WarehouseInventoryRecord item in items)
                if (item != null) total += Math.Max(0, item.Units);
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }
    }
}
