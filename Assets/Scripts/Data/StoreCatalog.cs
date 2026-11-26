using System;
using System.Collections.Generic;

namespace MyCity.Data
{
    [Serializable]
    public sealed class StoreInventoryRecord
    {
        public string ProductId;
        public string DisplayName;
        public string CategoryId;
        public int StockUnits;
        // Units reserved for an approved supplier return; excluded from saleable stock.
        public int ReservedForReturnUnits;
        public int TargetStockUnits;
        public long WholesalePrice;
        public long RetailPrice;
        public long TotalUnitsSold;
        public long LastDailyUnitsSold;
        public long LastDailyRevenue;
        // Weighted-average acquisition cost used for inventory valuation and margin reporting.
        public long AverageUnitCost;
    }

    [Serializable]
    public sealed class StoreCatalogProductDefinition
    {
        public string ProductId;
        public string DisplayName;
        public string CategoryId;
        public int StockUnits;
        public long WholesalePrice;
        public long RetailPrice;
    }

    [Serializable]
    public sealed class StoreCatalogStoreDefinition
    {
        public string StoreTypeId;
        public string DisplayName;
        public List<StoreCatalogProductDefinition> Products = new List<StoreCatalogProductDefinition>();
    }

    [Serializable]
    public sealed class StoreCatalogFile
    {
        public int Version = 1;
        public List<StoreCatalogStoreDefinition> Stores = new List<StoreCatalogStoreDefinition>();
    }

    /// <summary>Data-driven starter assortment. Add products here without adding store-specific economy code.</summary>
    public static class StoreCatalog
    {
        private sealed class ProductSeed
        {
            public readonly string Id, Name, Category;
            public readonly int Stock;
            public readonly long Wholesale, Retail;
            public ProductSeed(string id, string name, string category, int stock, long wholesale, long retail)
            { Id=id; Name=name; Category=category; Stock=stock; Wholesale=wholesale; Retail=retail; }
        }

        private static readonly Dictionary<string, ProductSeed[]> Catalog = new Dictionary<string, ProductSeed[]>
        {
            { "grocery", new[] {
                new ProductSeed("bread", "Хлеб", "food", 80, 35, 65),
                new ProductSeed("milk", "Молоко", "food", 60, 55, 90),
                new ProductSeed("meat", "Мясо", "food", 35, 240, 360),
                new ProductSeed("vegetables", "Овощи", "food", 55, 70, 120),
                new ProductSeed("fruit", "Фрукты", "food", 45, 95, 155) } },
            { "electronics", new[] {
                new ProductSeed("smartphone", "Смартфон", "electronics", 8, 18000, 26000),
                new ProductSeed("television", "Телевизор", "electronics", 5, 32000, 45000),
                new ProductSeed("refrigerator", "Холодильник", "appliances", 4, 42000, 59000),
                new ProductSeed("laptop", "Ноутбук", "electronics", 6, 48000, 65000),
                new ProductSeed("washing_machine", "Стиральная машина", "appliances", 4, 29000, 41000) } },
            { "clothing", new[] {
                new ProductSeed("jacket", "Куртка", "clothing", 20, 2200, 3900),
                new ProductSeed("jeans", "Джинсы", "clothing", 30, 1100, 2100),
                new ProductSeed("shoes", "Обувь", "footwear", 24, 1800, 3200),
                new ProductSeed("shirt", "Рубашка", "clothing", 35, 650, 1250) } },
            { "furniture", new[] {
                new ProductSeed("sofa", "Диван", "furniture", 5, 18000, 26000),
                new ProductSeed("bed", "Кровать", "furniture", 6, 12000, 18500),
                new ProductSeed("table", "Стол", "furniture", 8, 6500, 9800),
                new ProductSeed("chair", "Стул", "furniture", 20, 1800, 2900) } },
            { "cosmetics", new[] {
                new ProductSeed("shampoo", "Шампунь", "care", 40, 180, 320),
                new ProductSeed("soap", "Мыло", "care", 55, 45, 90),
                new ProductSeed("cream", "Крем", "cosmetics", 30, 260, 450),
                new ProductSeed("detergent", "Стиральный порошок", "household_chemicals", 35, 240, 390) } },
            { "books", new[] {
                new ProductSeed("novel", "Художественная книга", "books", 25, 380, 690),
                new ProductSeed("textbook", "Учебник", "books", 18, 520, 890),
                new ProductSeed("notebook", "Тетрадь", "stationery", 60, 35, 70),
                new ProductSeed("pens", "Ручки", "stationery", 50, 22, 50) } }
        };

        public static bool ReplaceCatalog(List<StoreCatalogStoreDefinition> definitions)
        {
            if (definitions == null || definitions.Count == 0) return false;
            Dictionary<string, ProductSeed[]> replacement = new Dictionary<string, ProductSeed[]>();
            foreach (StoreCatalogStoreDefinition store in definitions)
            {
                if (store == null || string.IsNullOrEmpty(store.StoreTypeId) || store.Products == null || store.Products.Count == 0) continue;
                List<ProductSeed> products = new List<ProductSeed>();
                foreach (StoreCatalogProductDefinition product in store.Products)
                {
                    if (product == null || string.IsNullOrEmpty(product.ProductId) || string.IsNullOrEmpty(product.DisplayName)) continue;
                    if (product.StockUnits < 0 || product.WholesalePrice < 0 || product.RetailPrice <= product.WholesalePrice) continue;
                    products.Add(new ProductSeed(product.ProductId, product.DisplayName, product.CategoryId ?? "general",
                        product.StockUnits, product.WholesalePrice, product.RetailPrice));
                }
                if (products.Count > 0) replacement[store.StoreTypeId] = products.ToArray();
            }
            if (replacement.Count == 0) return false;
            Catalog.Clear();
            foreach (KeyValuePair<string, ProductSeed[]> pair in replacement) Catalog[pair.Key] = pair.Value;
            return true;
        }

        public static List<StoreInventoryRecord> CreateStarterInventory(string storeTypeId)
        {
            ProductSeed[] seeds;
            if (string.IsNullOrEmpty(storeTypeId) || !Catalog.TryGetValue(storeTypeId, out seeds))
                seeds = Catalog["grocery"];
            List<StoreInventoryRecord> result = new List<StoreInventoryRecord>(seeds.Length);
            foreach (ProductSeed seed in seeds)
                result.Add(new StoreInventoryRecord { ProductId=seed.Id, DisplayName=seed.Name, CategoryId=seed.Category,
                    StockUnits=seed.Stock, TargetStockUnits=seed.Stock, WholesalePrice=seed.Wholesale, RetailPrice=seed.Retail });
            return result;
        }
    }
}
