using System;
using System.Collections.Generic;
using MyCity.Data;
using UnityEngine;

namespace MyCity.Simulation
{
    /// <summary>Finite regional fleet with capacity matching and auditable trip operating costs.</summary>
    public sealed class FleetManagementSystem : MonoBehaviour
    {
        public const float FuelPricePerLitre = 65f;
        private readonly Dictionary<string, FleetVehicleRecord> vehicles = new Dictionary<string, FleetVehicleRecord>();
        private readonly List<FleetTransactionRecord> transactions = new List<FleetTransactionRecord>();
        public event Action<long, string> OnOperatingCostCharged;
        public int VehicleCount { get { return vehicles.Count; } }
        public int AvailableCount { get { int n=0; foreach(FleetVehicleRecord v in vehicles.Values) if(v!=null && v.Status=="Свободен") n++; return n; } }
        public long TotalOperatingCost { get; private set; }

        public void InitializeDefaults()
        {
            if(vehicles.Count>0) return;
            Add("truck-light-01","Фургон 01","Лёгкий",500,18f,22,10);
            Add("truck-light-02","Фургон 02","Лёгкий",500,18f,22,10);
            Add("truck-medium-01","Грузовик 01","Средний",1500,28f,35,18);
            Add("truck-medium-02","Грузовик 02","Средний",1500,28f,35,18);
            Add("truck-heavy-01","Тягач 01","Тяжёлый",4000,42f,52,28);
        }
        private void Add(string id,string name,string type,int capacity,float fuel,long driver,long maintenance)
        { vehicles[id]=new FleetVehicleRecord{Id=id,DisplayName=name,VehicleClass=type,CapacityUnits=capacity,FuelLitresPer100Km=fuel,DriverCostPerKm=driver,MaintenanceCostPerKm=maintenance,Status="Свободен"}; }

        public bool TryAssign(string transferId,int units,out FleetVehicleRecord assigned,out string error)
        {
            assigned=null; error=string.Empty; FleetVehicleRecord best=null;
            foreach(FleetVehicleRecord v in vehicles.Values)
                if(v!=null && v.Status=="Свободен" && v.CapacityUnits>=units && (best==null || v.CapacityUnits<best.CapacityUnits || (v.CapacityUnits==best.CapacityUnits && string.CompareOrdinal(v.Id,best.Id)<0))) best=v;
            if(best==null){error=AvailableCount==0?"Нет свободных грузовиков в автопарке.":"В автопарке нет свободной машины подходящей грузоподъёмности.";return false;}
            best.Status="В рейсе"; best.ActiveTransferId=transferId; assigned=best; return true;
        }

        public void CompleteTrip(string vehicleId,string transferId,int distanceKm,int day)
        { CompleteTrip(vehicleId, transferId, distanceKm, day, true); }

        /// <summary>Completes a trip and optionally charges the general city budget. Warehouse transfers settle freight against their cargo instead.</summary>
        public FleetTransactionRecord CompleteTrip(string vehicleId,string transferId,int distanceKm,int day,bool chargeCityBudget)
        {
            FleetVehicleRecord v=Find(vehicleId); if(v==null || v.Status!="В рейсе" || v.ActiveTransferId!=transferId)return null;
            int km=Math.Max(0,distanceKm); long fuel=(long)Math.Ceiling(v.FuelLitresPer100Km*km/100f*FuelPricePerLitre);
            long driver=(long)km*v.DriverCostPerKm; long maintenance=(long)km*v.MaintenanceCostPerKm; long total=checked(fuel+driver+maintenance);
            FleetTransactionRecord tx=new FleetTransactionRecord{Id=Guid.NewGuid().ToString("N"),VehicleId=v.Id,TransferId=transferId,DistanceKm=km,FuelCost=fuel,DriverCost=driver,MaintenanceCost=maintenance,TotalCost=total,Day=Math.Max(0,day)};
            transactions.Add(tx); TotalOperatingCost=checked(TotalOperatingCost+total); v.TotalDistanceKm+=km; v.TotalOperatingCost=checked(v.TotalOperatingCost+total); v.CompletedTrips++;
            v.Status="Свободен"; v.ActiveTransferId=null;
            if(chargeCityBudget && OnOperatingCostCharged!=null) OnOperatingCostCharged(total,"Рейс "+transferId.Substring(0,Math.Min(8,transferId.Length))+": топливо "+fuel+" ₽, водитель "+driver+" ₽, обслуживание "+maintenance+" ₽.");
            return tx;
        }
        public void Release(string vehicleId,string transferId)
        { FleetVehicleRecord v=Find(vehicleId); if(v!=null && v.ActiveTransferId==transferId){v.Status="Свободен";v.ActiveTransferId=null;} }
        public FleetVehicleRecord Find(string id){FleetVehicleRecord v;return !string.IsNullOrEmpty(id)&&vehicles.TryGetValue(id,out v)?v:null;}
        public void Load(List<FleetVehicleRecord> saved,List<FleetTransactionRecord> savedTransactions)
        { vehicles.Clear();transactions.Clear();TotalOperatingCost=0;if(saved!=null)foreach(FleetVehicleRecord v in saved)if(v!=null&&!string.IsNullOrEmpty(v.Id))vehicles[v.Id]=v;if(savedTransactions!=null)transactions.AddRange(savedTransactions);foreach(FleetTransactionRecord t in transactions)if(t!=null)TotalOperatingCost+=Math.Max(0,t.TotalCost);InitializeDefaults(); }
        public void Export(List<FleetVehicleRecord> vehicleOutput,List<FleetTransactionRecord> transactionOutput)
        { if(vehicleOutput!=null){vehicleOutput.Clear();List<string> ids=new List<string>(vehicles.Keys);ids.Sort(StringComparer.Ordinal);foreach(string id in ids)vehicleOutput.Add(vehicles[id]);}if(transactionOutput!=null){transactionOutput.Clear();transactionOutput.AddRange(transactions);} }
    }
}
