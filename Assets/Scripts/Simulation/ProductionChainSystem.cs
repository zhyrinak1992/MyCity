using System;
using System.Collections.Generic;
using MyCity.Core;
using MyCity.Data;
using UnityEngine;

namespace MyCity.Simulation
{
    [Serializable]
    public sealed class ProductionInputRequirement
    {
        public string ProductId;
        public int UnitsPerOutput;
        public ProductionInputRequirement(string productId, int unitsPerOutput)
        { ProductId = productId; UnitsPerOutput = unitsPerOutput; }
    }

    public sealed class ProductionRecipeDefinition
    {
        public string Id;
        public string DisplayName;
        public string OutputProductId;
        public int OutputUnitsPerBatch;
        public ProductionInputRequirement[] Inputs;
        public ProductionRecipeDefinition(string id, string name, string output, int outputUnits, params ProductionInputRequirement[] inputs)
        { Id=id; DisplayName=name; OutputProductId=output; OutputUnitsPerBatch=outputUnits; Inputs=inputs; }
    }

    /// <summary>
    /// Deterministic daily production. It consumes real input inventory, respects output storage,
    /// and persists a per-site day key so catch-up or duplicate callbacks cannot produce twice.
    /// </summary>
    public sealed class ProductionChainSystem : MonoBehaviour
    {
        private readonly Dictionary<string, ProductionSiteRecord> sites = new Dictionary<string, ProductionSiteRecord>();
        private readonly HashSet<string> processedDayKeys = new HashSet<string>();
        private GameClock clock;

        private static readonly Dictionary<string, ProductionRecipeDefinition> Recipes =
            new Dictionary<string, ProductionRecipeDefinition>
        {
            { "bakery_bread", new ProductionRecipeDefinition("bakery_bread", "Выпечка хлеба", "bread", 1,
                new ProductionInputRequirement("flour", 1), new ProductionInputRequirement("water", 1)) },
            { "dairy_milk", new ProductionRecipeDefinition("dairy_milk", "Розлив молока", "milk", 1,
                new ProductionInputRequirement("raw_milk", 1), new ProductionInputRequirement("packaging", 1)) },
            { "furniture", new ProductionRecipeDefinition("furniture", "Производство мебели", "furniture_unit", 1,
                new ProductionInputRequirement("timber", 3), new ProductionInputRequirement("metal", 1)) },
            { "fuel_refining", new ProductionRecipeDefinition("fuel_refining", "Переработка топлива", "diesel", 1,
                new ProductionInputRequirement("crude_oil", 2), new ProductionInputRequirement("electricity", 1)) },
            { "building_materials", new ProductionRecipeDefinition("building_materials", "Стройматериалы", "building_materials", 2,
                new ProductionInputRequirement("cement", 1), new ProductionInputRequirement("sand", 2), new ProductionInputRequirement("electricity", 1)) }
        };

        public event Action<string> OnProductionMessage;
        public int RegisteredSiteCount { get { return sites.Count; } }

        private void OnEnable() { TrySubscribe(); }
        private void Update() { if (clock == null) TrySubscribe(); }
        private void OnDisable()
        {
            if (clock != null) clock.OnDayChanged -= HandleDayChanged;
            clock = null;
        }

        private void TrySubscribe()
        {
            GameClock current = GameClock.Instance;
            if (current == null || current == clock) return;
            if (clock != null) clock.OnDayChanged -= HandleDayChanged;
            clock = current;
            clock.OnDayChanged += HandleDayChanged;
        }

        public static ProductionRecipeDefinition FindRecipe(string recipeId)
        {
            ProductionRecipeDefinition recipe;
            return !string.IsNullOrEmpty(recipeId) && Recipes.TryGetValue(recipeId, out recipe) ? recipe : null;
        }

        public bool RegisterSite(ProductionSiteRecord site)
        {
            if (site == null || string.IsNullOrEmpty(site.Id) || string.IsNullOrEmpty(site.BuildingId) ||
                FindRecipe(site.RecipeId) == null || site.DailyCapacityUnits <= 0 || site.StorageCapacityUnits <= 0)
                return false;
            Normalize(site);
            sites[site.Id] = site;
            return true;
        }

        public ProductionSiteRecord FindSite(string siteId)
        {
            ProductionSiteRecord site;
            return !string.IsNullOrEmpty(siteId) && sites.TryGetValue(siteId, out site) ? site : null;
        }

        public bool AddInputStock(string siteId, string productId, int units)
        {
            return AddInputStock(siteId, productId, units, 0);
        }

        public bool AddInputStock(string siteId, string productId, int units, long landedUnitCost)
        {
            ProductionSiteRecord site;
            if (units <= 0 || string.IsNullOrEmpty(productId) || !sites.TryGetValue(siteId, out site)) return false;
            ProductionInventoryRecord item = FindOrCreate(site.Inputs, productId);
            int oldUnits = Math.Max(0, item.Units);
            long oldValue = checked((long)oldUnits * Math.Max(0, item.AverageUnitCost));
            long newValue = checked((long)units * Math.Max(0, landedUnitCost));
            int totalUnits = checked(oldUnits + units);
            item.AverageUnitCost = totalUnits > 0 ? checked(oldValue + newValue) / totalUnits : 0;
            item.Units = totalUnits;
            return true;
        }

        public int GetInputStock(string siteId, string productId)
        {
            ProductionSiteRecord site;
            if (!sites.TryGetValue(siteId, out site)) return 0;
            ProductionInventoryRecord item = Find(site.Inputs, productId);
            return item != null ? item.Units : 0;
        }

        public bool RemoveOutputStock(string siteId, string productId, int units)
        {
            long ignored;
            return TryRemoveOutputStock(siteId, productId, units, out ignored);
        }

        /// <summary>Removes finished goods and returns the inventory valuation used for warehouse receipt.</summary>
        public bool TryRemoveOutputStock(string siteId, string productId, int units, out long averageUnitCost)
        {
            averageUnitCost = 0;
            ProductionSiteRecord site;
            if (units <= 0 || !sites.TryGetValue(siteId, out site)) return false;
            ProductionInventoryRecord item = Find(site.Outputs, productId);
            if (item == null || item.Units < units) return false;
            averageUnitCost = Math.Max(0, item.AverageUnitCost);
            item.Units -= units;
            if (item.Units == 0) item.AverageUnitCost = 0;
            return true;
        }

        public bool AddOutputStock(string siteId, string productId, int units)
        {
            return AddOutputStock(siteId, productId, units, 0);
        }

        public bool AddOutputStock(string siteId, string productId, int units, long unitCost)
        {
            ProductionSiteRecord site;
            if (units <= 0 || unitCost < 0 || string.IsNullOrEmpty(productId) || !sites.TryGetValue(siteId, out site)) return false;
            if (units > Math.Max(0, site.StorageCapacityUnits - TotalUnits(site.Outputs))) return false;
            ProductionInventoryRecord item = FindOrCreate(site.Outputs, productId);
            int oldUnits = Math.Max(0, item.Units);
            long oldValue = checked((long)oldUnits * Math.Max(0, item.AverageUnitCost));
            int totalUnits = checked(oldUnits + units);
            item.AverageUnitCost = checked(oldValue + checked((long)units * unitCost)) / totalUnits;
            item.Units = totalUnits;
            return true;
        }

        public int GetOutputStock(string siteId, string productId)
        {
            ProductionSiteRecord site;
            if (!sites.TryGetValue(siteId, out site)) return 0;
            ProductionInventoryRecord item = Find(site.Outputs, productId);
            return item != null ? item.Units : 0;
        }

        public void Load(List<ProductionSiteRecord> savedSites)
        {
            sites.Clear();
            processedDayKeys.Clear();
            if (savedSites == null) return;
            foreach (ProductionSiteRecord site in savedSites)
                if (site != null) RegisterSite(site);
        }

        public void Export(List<ProductionSiteRecord> destination)
        {
            if (destination == null) return;
            destination.Clear();
            List<string> ids = new List<string>(sites.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids) destination.Add(sites[id]);
        }

        private void HandleDayChanged(int day)
        {
            List<string> ids = new List<string>(sites.Keys);
            ids.Sort(StringComparer.Ordinal);
            foreach (string id in ids)
            {
                ProductionSiteRecord site = sites[id];
                string key = day + ":" + site.Id;
                if (!processedDayKeys.Add(key) || day <= site.LastProcessedDay) continue;
                ProcessSiteDay(site, day);
            }
        }

        private static void ProcessSiteDay(ProductionSiteRecord site, int day)
        {
            if (site == null) return;
            if (!site.IsOperating)
            {
                site.LastStatus = "Производство остановлено";
                site.LastProcessedDay = day;
                return;
            }
            ProductionRecipeDefinition recipe = FindRecipe(site.RecipeId);
            if (recipe == null)
            {
                site.LastStatus = "Неизвестный производственный рецепт";
                site.LastProcessedDay = day;
                return;
            }

            int outputStock = GetUnits(site.Outputs, recipe.OutputProductId);
            int freeCapacity = Math.Max(0, site.StorageCapacityUnits - TotalUnits(site.Outputs));
            int capacity = Math.Min(site.DailyCapacityUnits, freeCapacity);
            int possibleBatches = capacity / Math.Max(1, recipe.OutputUnitsPerBatch);
            foreach (ProductionInputRequirement input in recipe.Inputs)
                possibleBatches = Math.Min(possibleBatches, GetUnits(site.Inputs, input.ProductId) / input.UnitsPerOutput);

            if (possibleBatches <= 0)
            {
                site.LastDailyOperatingCost = 0;
                site.LastStatus = freeCapacity <= 0 ? "Склад готовой продукции заполнен" : "Простой: недостаточно сырья или ресурсов";
                site.LastProcessedDay = day;
                return;
            }

            int produced = checked(possibleBatches * recipe.OutputUnitsPerBatch);
            long operatingCost = checked(Math.Max(0L, site.DailyBaseOperatingCost) + checked((long)produced * Math.Max(0L, site.DailyCostPerOutputUnit)));
            if (site.OperatingFunds < operatingCost)
            {
                site.LastDailyOperatingCost = 0;
                site.LastStatus = "Простой: недостаточно средств на ежедневные расходы производства";
                site.LastProcessedDay = day;
                return;
            }
            site.OperatingFunds -= operatingCost;
            site.LastDailyOperatingCost = operatingCost;
            site.CumulativeOperatingCosts = checked(site.CumulativeOperatingCosts + operatingCost);
            int consumed = 0;
            long materialCost = 0;
            foreach (ProductionInputRequirement input in recipe.Inputs)
            {
                int use = checked(possibleBatches * input.UnitsPerOutput);
                ProductionInventoryRecord inputItem = Find(site.Inputs, input.ProductId);
                materialCost = checked(materialCost + checked((long)use * Math.Max(0, inputItem.AverageUnitCost)));
                inputItem.Units -= use;
                if (inputItem.Units == 0) inputItem.AverageUnitCost = 0;
                consumed = checked(consumed + use);
            }
            ProductionInventoryRecord outputItem = FindOrCreate(site.Outputs, recipe.OutputProductId);
            long priorOutputValue = checked((long)Math.Max(0, outputItem.Units) * Math.Max(0, outputItem.AverageUnitCost));
            long productionCost = checked(materialCost + operatingCost);
            int newOutputUnits = checked(outputStock + produced);
            outputItem.AverageUnitCost = newOutputUnits > 0 ? checked(priorOutputValue + productionCost) / newOutputUnits : 0;
            outputItem.Units = newOutputUnits;
            site.LastDailyMaterialCost = materialCost;
            site.CumulativeMaterialCost = checked(site.CumulativeMaterialCost + materialCost);
            site.LastDailyOutputCost = productionCost;
            site.CumulativeOutputCost = checked(site.CumulativeOutputCost + productionCost);
            site.TotalUnitsProduced = checked(site.TotalUnitsProduced + produced);
            site.TotalUnitsConsumed = checked(site.TotalUnitsConsumed + consumed);
            site.LastStatus = "Произведено: " + produced + " ед.; использовано ресурсов: " + consumed;
            site.LastProcessedDay = day;
        }

        private static void Normalize(ProductionSiteRecord site)
        {
            if (site.Inputs == null) site.Inputs = new List<ProductionInventoryRecord>();
            if (site.Outputs == null) site.Outputs = new List<ProductionInventoryRecord>();
            if (site.DailyCapacityUnits <= 0) site.DailyCapacityUnits = 20;
            if (site.StorageCapacityUnits <= 0) site.StorageCapacityUnits = 500;
            if (site.LastStatus == null) site.LastStatus = "Ожидание запуска";
        }

        private static ProductionInventoryRecord Find(List<ProductionInventoryRecord> items, string id)
        {
            if (items == null) return null;
            return items.Find(x => x != null && x.ProductId == id);
        }
        private static ProductionInventoryRecord FindOrCreate(List<ProductionInventoryRecord> items, string id)
        {
            ProductionInventoryRecord item = Find(items, id);
            if (item != null) return item;
            item = new ProductionInventoryRecord { ProductId = id, Units = 0 };
            items.Add(item);
            return item;
        }
        private static int GetUnits(List<ProductionInventoryRecord> items, string id)
        { ProductionInventoryRecord item = Find(items, id); return item != null ? Math.Max(0, item.Units) : 0; }
        private static int TotalUnits(List<ProductionInventoryRecord> items)
        {
            long total = 0;
            if (items != null) foreach (ProductionInventoryRecord item in items)
                if (item != null) total += Math.Max(0, item.Units);
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }
    }
}
