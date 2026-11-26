using System;
using System.Collections.Generic;

namespace MyCity.Data
{
    public enum CityFacilityCategory
    {
        CivilDefense, AirDefenseAbstract, DefenseIndustry, OilRefinery, GeneralIndustry,
        DistributionCenter, WholesaleWarehouse, FuelStorage, MarketplacePickup, ParcelLocker,
        CourierDepot, Retail, RailFreight, Energy, WaterUtility
    }

    [Serializable]
    public sealed class CityFacilityDefinition
    {
        public string Id;
        public string DisplayName;
        public CityFacilityCategory Category;
        public int FootprintWidth;
        public int FootprintDepth;
        public int StaffCapacity;
        public bool RequiresRoadAccess;
        public bool RequiresElectricity;
        public bool RequiresWater;
        public bool IsPlaceOfEmployment;
        public bool IsPubliclyAccessible;
    }

    /// <summary>Starter data catalogue for regional facilities. This defines game entities, not operational real-world infrastructure.</summary>
    public static class CityFacilityCatalog
    {
        private static readonly CityFacilityDefinition[] Items =
        {
            F("siren_station", "Сирена оповещения", CityFacilityCategory.CivilDefense, 1, 1, 0, false, true, false, false, false),
            F("civil_shelter", "Гражданское укрытие", CityFacilityCategory.CivilDefense, 2, 2, 2, true, true, false, false, true),
            F("air_defense_facility", "Объект ПВО (обобщённый)", CityFacilityCategory.AirDefenseAbstract, 4, 4, 18, true, true, false, true, false),
            F("defense_factory", "Оборонное предприятие", CityFacilityCategory.DefenseIndustry, 6, 5, 120, true, true, true, true, false),
            F("oil_refinery", "Нефтеперерабатывающий завод", CityFacilityCategory.OilRefinery, 8, 8, 180, true, true, true, true, false),
            F("industrial_factory", "Промышленный завод", CityFacilityCategory.GeneralIndustry, 5, 5, 90, true, true, true, true, false),
            F("distribution_center", "Распределительный центр", CityFacilityCategory.DistributionCenter, 7, 5, 80, true, true, false, true, false),
            F("wholesale_warehouse", "Оптовый склад", CityFacilityCategory.WholesaleWarehouse, 5, 4, 35, true, true, false, true, false),
            F("fuel_storage", "Топливное хранилище", CityFacilityCategory.FuelStorage, 5, 5, 24, true, true, false, true, false),
            F("wildberries_pickup", "Пункт выдачи Wildberries", CityFacilityCategory.MarketplacePickup, 2, 2, 6, true, true, false, true, true),
            F("ozon_pickup", "Пункт выдачи Ozon", CityFacilityCategory.MarketplacePickup, 2, 2, 6, true, true, false, true, true),
            F("parcel_locker", "Постамат", CityFacilityCategory.ParcelLocker, 1, 1, 0, true, true, false, false, true),
            F("courier_depot", "Курьерское депо", CityFacilityCategory.CourierDepot, 4, 3, 30, true, true, false, true, false),
            F("rail_freight_terminal", "Грузовой железнодорожный терминал", CityFacilityCategory.RailFreight, 8, 4, 55, true, true, false, true, false),
            F("power_substation", "Электроподстанция", CityFacilityCategory.Energy, 3, 3, 12, true, true, false, true, false),
            F("water_utility", "Объект водоснабжения", CityFacilityCategory.WaterUtility, 4, 4, 20, true, true, true, true, false)
        };

        public static IReadOnlyList<CityFacilityDefinition> All { get { return Items; } }

        public static CityFacilityDefinition Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (CityFacilityDefinition item in Items)
                if (string.Equals(item.Id, id, StringComparison.Ordinal)) return item;
            return null;
        }

        private static CityFacilityDefinition F(string id, string name, CityFacilityCategory category, int w, int d, int staff, bool road, bool power, bool water, bool jobs, bool publicAccess)
        {
            return new CityFacilityDefinition { Id=id, DisplayName=name, Category=category, FootprintWidth=w, FootprintDepth=d, StaffCapacity=staff, RequiresRoadAccess=road, RequiresElectricity=power, RequiresWater=water, IsPlaceOfEmployment=jobs, IsPubliclyAccessible=publicAccess };
        }
    }
}
