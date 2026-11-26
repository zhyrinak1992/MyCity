using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using MyCity.Data;
using UnityEngine;

namespace MyCity.Saving
{
    public static class CitySaveService
    {
        private const string SaveFileName = "mycity-save.json";

        public static string SavePath { get { return Path.Combine(Application.persistentDataPath, SaveFileName); } }

        public static bool Save(CitySaveData data, out string error)
        {
            error = string.Empty;
            try
            {
                if (data == null) throw new ArgumentNullException("data");
                string json = JsonUtility.ToJson(data, true);
                string tempPath = SavePath + ".tmp";
                File.WriteAllText(tempPath, json, Encoding.UTF8);
                string backupPath = SavePath + ".bak";
                if (File.Exists(SavePath))
                {
                    // Keep the previous known-good file before replacing the active save.
                    File.Copy(SavePath, backupPath, true);
                    try { File.Replace(tempPath, SavePath, null); }
                    catch (PlatformNotSupportedException)
                    {
                        File.Delete(SavePath);
                        File.Move(tempPath, SavePath);
                    }
                    catch (IOException)
                    {
                        File.Delete(SavePath);
                        File.Move(tempPath, SavePath);
                    }
                }
                else File.Move(tempPath, SavePath);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool Load(out CitySaveData data, out string error)
        {
            data = null;
            error = string.Empty;
            string[] candidates = { SavePath, SavePath + ".bak" };
            string lastError = "Сохранение и резервная копия не найдены.";
            foreach (string candidate in candidates)
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    string json = File.ReadAllText(candidate, Encoding.UTF8);
                    CitySaveData parsed = JsonUtility.FromJson<CitySaveData>(json);
                    if (parsed == null || (parsed.SchemaVersion < 1 || parsed.SchemaVersion > 19))
                    {
                        lastError = "Версия или структура файла не поддерживается: " + Path.GetFileName(candidate);
                        continue;
                    }
                    if (parsed.Sectors == null) parsed.Sectors = new System.Collections.Generic.List<SectorRecord>();
                    if (parsed.Workplaces == null) parsed.Workplaces = new System.Collections.Generic.List<WorkplaceRecord>();
                    if (parsed.ProductionSites == null) parsed.ProductionSites = new System.Collections.Generic.List<ProductionSiteRecord>();
                    if (parsed.Warehouses == null) parsed.Warehouses = new System.Collections.Generic.List<WarehouseRecord>();
                    if (parsed.Suppliers == null) parsed.Suppliers = new System.Collections.Generic.List<SupplierRecord>();
                    if (parsed.SupplyOrders == null) parsed.SupplyOrders = new System.Collections.Generic.List<SupplyOrderRecord>();
                    if (parsed.CargoVehicles == null) parsed.CargoVehicles = new System.Collections.Generic.List<CargoVehicleRecord>();
                    if (parsed.PlayerRoadSegments == null) parsed.PlayerRoadSegments = new System.Collections.Generic.List<RoadSegmentRecord>();
                    if (parsed.FleetVehicles == null) parsed.FleetVehicles = new System.Collections.Generic.List<FleetVehicleRecord>();
                    if (parsed.FleetTransactions == null) parsed.FleetTransactions = new System.Collections.Generic.List<FleetTransactionRecord>();
                    if (parsed.DroneThreatHistory == null) parsed.DroneThreatHistory = new System.Collections.Generic.List<MyCity.Simulation.DroneThreatSystem.DroneThreatRecord>();
                    if (parsed.SchemaVersion < 19) parsed.SchemaVersion = 19;
                    data = parsed;
                    if (candidate != SavePath) error = "Основное сохранение повреждено или отсутствует; загружена резервная копия.";
                    return true;
                }
                catch (Exception ex) { lastError = Path.GetFileName(candidate) + ": " + ex.Message; }
            }
            error = lastError;
            return false;
        }
    }
}
