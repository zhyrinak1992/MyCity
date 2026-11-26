using UnityEngine;
using System;
using System.Collections.Generic;
using MyCity.Core;
using MyCity.Data;
using MyCity.Saving;
using MyCity.Simulation;

// Runtime vertical slice: construction -> road access -> occupancy -> living residents.
public class MyCityBootstrap : MonoBehaviour
{
    public static MyCityBootstrap Instance;
    private enum Tool { House, Cottage, Apartment, Grocery, Electronics, Clothing, Furniture, Cosmetics, Books, Road, Demolish, Warehouse, HighRise, Morgue, Cemetery }
    private Tool tool = Tool.House;
    private readonly List<PlacedBuilding> buildings = new List<PlacedBuilding>();
    private readonly List<ResidentAgent> residents = new List<ResidentAgent>();
    private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    private Camera cam;
    private Vector3 target = Vector3.zero;
    private float yaw = 42f, pitch = 48f, distance = 48f;
    private int quarterTurns;
    private int selectedWarehouseType;
    private int selectedHighRiseType;
    private bool showHighRiseFloorPlan;
    private bool highRiseInteriorMode;
    private Vector3 savedCameraTarget;
    private float savedCameraDistance, savedCameraPitch;
    private Vector2 highRiseFloorScroll;
    private readonly List<GameObject> highRiseInteriorAgents = new List<GameObject>();
    private readonly Dictionary<string, HighRiseElevatorController> highRiseElevators = new Dictionary<string, HighRiseElevatorController>();
    private float highRiseAgentClock;
    private int money = 250000;
    private GameClock gameClock;
    private WorkScheduleSystem workScheduleSystem;
    private CityEconomySystem economySystem;
    private HouseholdSystem householdSystem;
    private CitizenRelationshipSystem citizenRelationshipSystem;
    private FamilyEconomySystem familyEconomySystem;
    private CitizenLifeCycleSystem lifeCycleSystem;
    private bool showLifeCyclePanel;
    private string familyContributionAmount = "500";
    private string familyExpenseAmount = "300";
    private string familyExpenseDescription = "Общие покупки";
    private int familyResponsibilityCategoryIndex;
    private string familyResponsibilityShare = "50";
    private ProductionChainSystem productionChainSystem;
    private SupplyLogisticsSystem supplyLogisticsSystem;
    private WarehouseStorageSystem warehouseStorageSystem;
    private FleetManagementSystem fleetManagementSystem;
    private DroneThreatSystem droneThreatSystem;
    private AirTransportSystem airTransportSystem;
    private PassengerTransportNetworkSystem passengerTransportNetworkSystem;
    private InternationalAirportNetworkSystem internationalAirportNetworkSystem;
    private InternationalAirlineFleetSystem internationalAirlineFleetSystem;
    private AirlineEconomySystem airlineEconomySystem;
    private AirportPassengerServiceSystem airportPassengerServiceSystem;
    private AirportIndividualPassengerSystem airportIndividualPassengerSystem;
    private AirportBaggageIntelligenceSystem airportBaggageIntelligenceSystem;
    private bool showAirportPassengerPanel;
    private Vector2 airportPassengerScroll;
    private bool showAirlineEconomyPanel;
    private Vector2 airlineEconomyScroll;
    private readonly Dictionary<string, string> airlineFareEdits = new Dictionary<string, string>();
    private bool showInternationalFleetPanel;
    private Vector2 internationalFleetScroll;
    private bool showInternationalAirportPanel;
    private bool showAirTrafficPanel;
    private bool showPassengerNetworkPanel;
    private MayorOfficeSystem mayorOfficeSystem;
    private CityMaintenanceSystem maintenanceSystem;
    private bool showMayorOffice;
    private int mayorMaintenanceBuildingIndex;
    private Vector2 mayorOfficeScroll;
    private RoadNetworkGraph roadNetwork;
    private PedestrianNetworkGraph pedestrianNetwork;
    private readonly List<GameObject> playerRoadObjects = new List<GameObject>();
    private Vector2Int? pendingRoadStart;
    private readonly Dictionary<GameObject, Tuple<Vector2Int, Vector2Int>> playerRoadEdges = new Dictionary<GameObject, Tuple<Vector2Int, Vector2Int>>();
    private readonly Dictionary<GameObject, List<GameObject>> playerRoadSidewalks = new Dictionary<GameObject, List<GameObject>>();
    private int selectedResidentIndex = -1;
    private bool showFamilyPanel;
    private Vector2 familyPanelScroll;
    private bool showFinancePanel;
    private bool showFinanceLedger;
    private Vector2 financeLedgerScroll;
    private bool financeLedgerCurrentDayOnly;
    private sealed class FinanceLedgerLine { public int Day; public string Category; public string Description; public long Amount; public bool IsIncome; }
    private string status = "Выберите участок рядом с дорогой и постройте первый дом.";
    private PlacedBuilding selected;
    private bool showHelp = true;
    private Vector2 storePanelScroll;
    private int selectedStoreTab;
    private int selectedProductionRecipe;
    private readonly Dictionary<string, string> storePriceEdits = new Dictionary<string, string>();
    private readonly Dictionary<string, string> storeReceiptReceivedEdits = new Dictionary<string, string>();
    private readonly Dictionary<string, string> storeReceiptDamagedEdits = new Dictionary<string, string>();
    private readonly Dictionary<string, string> storeReceiptReasonEdits = new Dictionary<string, string>();
    private readonly Dictionary<string, string> storeReturnUnitEdits = new Dictionary<string, string>();
    private bool storeReturnUseCredit;
    private bool showWarehouseDispatcher;
    private int dispatcherSourceIndex;
    private int dispatcherDestinationIndex = 1;
    private string dispatcherProductId = "flour";
    private string dispatcherUnits = "100";
    private readonly Dictionary<string, string> supplierReceivedEdits = new Dictionary<string, string>();
    private readonly Dictionary<string, string> supplierDefectEdits = new Dictionary<string, string>();
    private readonly Dictionary<string, string> supplierInspectionNotes = new Dictionary<string, string>();

    private sealed class PlacedBuilding { public GameObject Root; public string Id; public string Name; public int Level = 1; public float Condition = 1f; public int Capacity; public int BaseCapacity; public int Residents; public int Cost; public bool Residential; public bool Workplace; public int StaffCapacity; public Vector3 Entrance; public Vector3 EntranceLocal; public Vector3 Footprint; public float VisualHeight; public bool IsHighRise; public int ElevatorCars; public int ElevatorTripsToday; public List<HighRiseFloorRecord> HighRiseFloors = new List<HighRiseFloorRecord>(); public GameObject InteriorRoot; public Renderer[] ExteriorRenderers; public Renderer[] InteriorRenderers; public int LastHighRiseSettlementDay; public long DailyHighRiseRent; public long DailyHighRisePayroll; public int FacilityCapacity; }
    private sealed class ResidentAgent { public GameObject Body; public Vector3 Destination; public float Wait; public PlacedBuilding Home; public string Id; public string Name; public CitizenDailyPhase ScheduleState; public string WorkplaceBuildingId; public string ProfessionId; public int MonthlySalary; public CitizenRecord Record; public List<Vector3> Route; public int RouteIndex; public Vector3 RouteTarget; public string ActivityBuildingId; public int ActivityMinutesRemaining; public int PublicVisitsToday; public bool HomeResidentsReleased; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap() { if (FindObjectOfType<MyCityBootstrap>() == null) new GameObject("MyCity_Runtime").AddComponent<MyCityBootstrap>(); }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; Application.targetFrameRate = 60;
        EnsureGameClock();
        workScheduleSystem = gameObject.GetComponent<WorkScheduleSystem>();
        if (workScheduleSystem == null) workScheduleSystem = gameObject.AddComponent<WorkScheduleSystem>();
        economySystem = gameObject.GetComponent<CityEconomySystem>();
        if (economySystem == null) economySystem = gameObject.AddComponent<CityEconomySystem>();
        householdSystem = gameObject.GetComponent<HouseholdSystem>();
        if (householdSystem == null) householdSystem = gameObject.AddComponent<HouseholdSystem>();
        citizenRelationshipSystem = gameObject.GetComponent<CitizenRelationshipSystem>();
        if (citizenRelationshipSystem == null) citizenRelationshipSystem = gameObject.AddComponent<CitizenRelationshipSystem>();
        familyEconomySystem = gameObject.GetComponent<FamilyEconomySystem>();
        if (familyEconomySystem == null) familyEconomySystem = gameObject.AddComponent<FamilyEconomySystem>();
        lifeCycleSystem = gameObject.GetComponent<CitizenLifeCycleSystem>();
        if (lifeCycleSystem == null) lifeCycleSystem = gameObject.AddComponent<CitizenLifeCycleSystem>();
        economySystem.OnPayrollMessage += HandlePayrollMessage;
        economySystem.OnBusinessMessage += HandleBusinessMessage;
        economySystem.OnEconomyDaySettled += SaveCity;
        productionChainSystem = gameObject.GetComponent<ProductionChainSystem>();
        if (productionChainSystem == null) productionChainSystem = gameObject.AddComponent<ProductionChainSystem>();
        warehouseStorageSystem = gameObject.GetComponent<WarehouseStorageSystem>();
        if (warehouseStorageSystem == null) warehouseStorageSystem = gameObject.AddComponent<WarehouseStorageSystem>();
        warehouseStorageSystem.InitializeDefaults();
        fleetManagementSystem = gameObject.GetComponent<FleetManagementSystem>();
        if (fleetManagementSystem == null) fleetManagementSystem = gameObject.AddComponent<FleetManagementSystem>();
        fleetManagementSystem.InitializeDefaults();
        warehouseStorageSystem.SetFleetManagementSystem(fleetManagementSystem);
        warehouseStorageSystem.OnWarehouseOperatingCostCharged += HandleWarehouseOperatingCost;
        fleetManagementSystem.OnOperatingCostCharged += HandleFleetOperatingCost;
        supplyLogisticsSystem = gameObject.GetComponent<SupplyLogisticsSystem>();
        if (supplyLogisticsSystem == null) supplyLogisticsSystem = gameObject.AddComponent<SupplyLogisticsSystem>();
        supplyLogisticsSystem.OnVehicleProgressChanged += SaveCity;
        supplyLogisticsSystem.OnLogisticsMessage += HandleLogisticsMessage;
        mayorOfficeSystem = new MayorOfficeSystem();
        maintenanceSystem = new CityMaintenanceSystem();
        airTransportSystem = gameObject.GetComponent<AirTransportSystem>();
        if (airTransportSystem == null) airTransportSystem = gameObject.AddComponent<AirTransportSystem>();
        airTransportSystem.InitializeDefaults();
        passengerTransportNetworkSystem = gameObject.GetComponent<PassengerTransportNetworkSystem>();
        if (passengerTransportNetworkSystem == null) passengerTransportNetworkSystem = gameObject.AddComponent<PassengerTransportNetworkSystem>();
        passengerTransportNetworkSystem.InitializeDefaults();
        internationalAirportNetworkSystem = gameObject.GetComponent<InternationalAirportNetworkSystem>();
        if (internationalAirportNetworkSystem == null) internationalAirportNetworkSystem = gameObject.AddComponent<InternationalAirportNetworkSystem>();
        internationalAirportNetworkSystem.InitializeDefaults();
        internationalAirlineFleetSystem = gameObject.GetComponent<InternationalAirlineFleetSystem>();
        if (internationalAirlineFleetSystem == null) internationalAirlineFleetSystem = gameObject.AddComponent<InternationalAirlineFleetSystem>();
        internationalAirlineFleetSystem.InitializeDefaults(internationalAirportNetworkSystem);
        airlineEconomySystem = gameObject.GetComponent<AirlineEconomySystem>();
        if (airlineEconomySystem == null) airlineEconomySystem = gameObject.AddComponent<AirlineEconomySystem>();
        airlineEconomySystem.Initialize(internationalAirlineFleetSystem, internationalAirportNetworkSystem);
        airportPassengerServiceSystem = gameObject.GetComponent<AirportPassengerServiceSystem>();
        if (airportPassengerServiceSystem == null) airportPassengerServiceSystem = gameObject.AddComponent<AirportPassengerServiceSystem>();
        airportPassengerServiceSystem.Initialize(internationalAirlineFleetSystem);
        airportIndividualPassengerSystem = gameObject.GetComponent<AirportIndividualPassengerSystem>();
        if (airportIndividualPassengerSystem == null) airportIndividualPassengerSystem = gameObject.AddComponent<AirportIndividualPassengerSystem>();
        airportIndividualPassengerSystem.Initialize(internationalAirlineFleetSystem);
        airportBaggageIntelligenceSystem = gameObject.GetComponent<AirportBaggageIntelligenceSystem>();
        if (airportBaggageIntelligenceSystem == null) airportBaggageIntelligenceSystem = gameObject.AddComponent<AirportBaggageIntelligenceSystem>();
        airportBaggageIntelligenceSystem.Initialize(airportIndividualPassengerSystem);
        airportPassengerServiceSystem.OnServiceMessage += HandleAirportPassengerMessage;
        airlineEconomySystem.OnSettlementMessage += HandleAirlineSettlementMessage;
        droneThreatSystem = gameObject.GetComponent<DroneThreatSystem>();
        if (droneThreatSystem == null) droneThreatSystem = gameObject.AddComponent<DroneThreatSystem>();
        droneThreatSystem.OnThreatMessage += HandleDroneThreatMessage;
        StoreCatalogLoader.TryLoadExternalCatalog();
        CreateMaterials(); CreateWorld(); RestoreBuildings(); RestorePlayerRoads(); RestoreEconomy(); CreateCamera();
    }

    private void OnEnable()
    {
        if (gameClock != null) { gameClock.OnTick -= HandleGameMinute; gameClock.OnTick += HandleGameMinute; gameClock.OnDayChanged -= HandleMayorDayChanged; gameClock.OnDayChanged += HandleMayorDayChanged; HandleMayorDayChanged(gameClock.Day); }
    }

    private void OnDisable()
    {
        if (gameClock != null) { gameClock.OnTick -= HandleGameMinute; gameClock.OnDayChanged -= HandleMayorDayChanged; }
        if (economySystem != null)
        {
            economySystem.OnPayrollMessage -= HandlePayrollMessage;
            economySystem.OnBusinessMessage -= HandleBusinessMessage;
            economySystem.OnEconomyDaySettled -= SaveCity;
        }
        if (droneThreatSystem != null) droneThreatSystem.OnThreatMessage -= HandleDroneThreatMessage;
        if (airlineEconomySystem != null) airlineEconomySystem.OnSettlementMessage -= HandleAirlineSettlementMessage;
        if (airportPassengerServiceSystem != null) airportPassengerServiceSystem.OnServiceMessage -= HandleAirportPassengerMessage;
        if (fleetManagementSystem != null) fleetManagementSystem.OnOperatingCostCharged -= HandleFleetOperatingCost;
        if (warehouseStorageSystem != null) warehouseStorageSystem.OnWarehouseOperatingCostCharged -= HandleWarehouseOperatingCost;
        if (supplyLogisticsSystem != null)
        {
            supplyLogisticsSystem.OnVehicleProgressChanged -= SaveCity;
            supplyLogisticsSystem.OnLogisticsMessage -= HandleLogisticsMessage;
        }
    }

    private void HandleAirlineSettlementMessage(string message) { status = message; }
    private void HandleAirportPassengerMessage(string message) { status = message; }

    private void HandleMayorDayChanged(int day)
    {
        if (mayorOfficeSystem == null) return;
        if (lifeCycleSystem != null)
        {
            List<CitizenRecord> lifeCitizens = new List<CitizenRecord>();
            foreach (ResidentAgent resident in residents) if (resident != null && resident.Record != null) lifeCitizens.Add(resident.Record);
            List<BuildingRecord> lifeBuildings = new List<BuildingRecord>();
            foreach (PlacedBuilding b in buildings) if (b != null) lifeBuildings.Add(new BuildingRecord { Id=b.Id, DefinitionId=b.Name, FacilityCapacity=b.FacilityCapacity });
            lifeCycleSystem.ProcessDay(lifeCitizens, lifeBuildings, day);
            foreach (ResidentAgent resident in residents)
                if (resident != null && resident.Record != null && resident.Record.IsDeceased)
                {
                    if (workScheduleSystem != null && !string.IsNullOrEmpty(resident.Record.WorkplaceBuildingId)) workScheduleSystem.UnassignCitizen(resident.Id);
                    else { resident.Record.WorkplaceBuildingId = null; resident.Record.ProfessionId = null; resident.Record.MonthlySalary = 0; resident.Record.IsEmployed = false; }
                    if (!resident.HomeResidentsReleased && resident.Home != null) { resident.Home.Residents = Mathf.Max(0, resident.Home.Residents - 1); resident.HomeResidentsReleased = true; }
                    if (resident.Body != null) resident.Body.SetActive(false);
                }
        }
        foreach (PlacedBuilding building in buildings)
        {
            if (building == null || !building.IsHighRise) continue;
            building.ElevatorTripsToday = 0;
            if (building.HighRiseFloors != null)
                foreach (HighRiseFloorRecord floor in building.HighRiseFloors)
                    if (floor != null) floor.Visitors = 0;
        }
        List<SupplierRecord> suppliers = new List<SupplierRecord>();
        if (supplyLogisticsSystem != null) supplyLogisticsSystem.GetSupplierContracts(suppliers);
        int livingResidentCount = residents.FindAll(r => r != null && r.Record != null && !r.Record.IsDeceased).Count;
        mayorOfficeSystem.AdvanceToDay(day, livingResidentCount, suppliers.Count);
        if (maintenanceSystem != null) maintenanceSystem.AdvanceToDay(day, CompleteMaintenanceProject);
        if (mayorOfficeSystem.PendingCount > 0) status = "У мэра новые обращения. Откройте приёмную.";
        SaveCity();
    }

    private void CompleteMaintenanceProject(string buildingId)
    {
        PlacedBuilding target = buildings.Find(b => b != null && b.Id == buildingId);
        if (target == null) return;
        target.Condition = 1f;
        status = "Ремонт завершён: " + target.Name + ". Техническое состояние восстановлено.";
    }

    private void HandlePayrollMessage(string message)
    {
        status = message;
        SaveCity(); // Persist balances and transaction keys immediately after each successful transfer.
    }

    private void HandleBusinessMessage(string message)
    {
        status = message;
    }

    private void HandleDroneThreatMessage(string message)
    {
        status = message;
        SaveCity();
    }

    private void HandleLogisticsMessage(string message)
    {
        status = message;
        SaveCity();
    }

    private void HandleWarehouseOperatingCost(long cost, string message)
    {
        money = (int)Math.Max(int.MinValue, (long)money - Math.Max(0L, cost));
        status = message + " Остаток бюджета: " + money + " ₽.";
        SaveCity();
    }

    private void HandleFleetOperatingCost(long cost, string message)
    {
        money = (int)Math.Max(int.MinValue, (long)money - Math.Max(0L, cost));
        status = message + " Остаток бюджета: " + money + " ₽.";
        SaveCity();
    }


    private void HandleGameMinute(long tick)
    {
        if (gameClock != null && gameClock.MinuteOfDay == 0 && economySystem != null)
            economySystem.AdvanceResidentNeeds(gameClock.Day);
        AdvanceHighRiseOperations(tick);
        AdvanceHighRiseElevators();
        AnimateHighRiseInteriorAgents();
        CaptureSupplierRatingMonthIfNeeded();
        // Dock service advances on the same canonical game-minute clock as the city.
        if (warehouseStorageSystem != null)
        {
            warehouseStorageSystem.AdvanceDockService(1, gameClock != null ? gameClock.Day : 0);
            // Persist dock queues, active transfers and vehicle progress every five simulation minutes.
            if (tick % 5 == 0) SaveCity();
        }
        // Phase is derived from the canonical clock, so it cannot drift from city time.
        CitizenDailyPhase phase = CitizenDailySchedule.GetPhase(gameClock.MinuteOfDay);
        foreach (ResidentAgent resident in residents)
        {
            if (resident == null) continue;
            if (resident.ActivityMinutesRemaining > 0) resident.ActivityMinutesRemaining--;
            if (resident.ScheduleState == phase) continue;
            CitizenDailyPhase previousPhase = resident.ScheduleState;
            resident.ScheduleState = phase;
            if (resident.Record != null) resident.Record.ScheduleState = (int)phase;
            QueueResidentElevatorTrips(resident, previousPhase, phase);
            resident.Destination = RandomStreetPoint();
            resident.Wait = 0f;
            resident.ActivityMinutesRemaining = 0;
            resident.ActivityBuildingId = null;
            GameEventBus.Publish(new GameEvent(GameEventType.CitizenStateChanged, tick,
                "resident-schedule", resident.Id, CitizenDailySchedule.GetLabel(phase)));
        }
    }

    // The prototype calendar uses fixed 30-day economic months. Snapshots are immutable
    // and captured once at each month boundary, using only deliveries completed in that period.
    private void CaptureSupplierRatingMonthIfNeeded()
    {
        if (gameClock == null) return;
        int day = Math.Max(1, gameClock.Day);
        if (day % 30 != 0) return;
        int period = day / 30;
        if (period <= 0 || workScheduleSystem == null || workScheduleSystem.Workplaces == null) return;
        foreach (WorkplaceRecord store in workScheduleSystem.Workplaces)
        {
            if (store == null || store.LastSupplierRatingSnapshotPeriod >= period) continue;
            if (store.SupplierRatingHistory == null) store.SupplierRatingHistory = new List<SupplierRatingSnapshotRecord>();
            Dictionary<string, int[]> metrics = new Dictionary<string, int[]>();
            Dictionary<string, string> supplierNames = new Dictionary<string, string>();
            int startDay = (period - 1) * 30 + 1;
            int endDay = period * 30;
            if (store.PendingDeliveries != null)
            {
                for (int i = 0; i < store.PendingDeliveries.Count; i++)
                {
                    StoreDeliveryRecord d = store.PendingDeliveries[i];
                    int completedDay = d != null ? d.CompletedDay : 0;
                    if (d == null || d.IsCancelled || !d.IsAccepted || completedDay < startDay || completedDay > endDay) continue;
                    string key = string.IsNullOrEmpty(d.SupplierId) ? (string.IsNullOrEmpty(d.SupplierDisplayName) ? "unknown" : d.SupplierDisplayName) : d.SupplierId;
                    int[] m; if (!metrics.TryGetValue(key, out m)) { m = new int[6]; metrics.Add(key, m); }
                    if (!string.IsNullOrEmpty(d.SupplierDisplayName)) supplierNames[key] = d.SupplierDisplayName;
                    m[0]++; m[1] += Math.Max(0, d.Units); m[2] += Math.Max(0, d.DamagedUnits); m[3] += Math.Max(0, d.ShortUnits);
                    if (completedDay <= d.ArrivalDay || d.ArrivalDay <= 0) m[4]++;
                    m[5] += Math.Max(0, d.AcceptedUnits);
                }
            }
            if (store.SupplierReturns != null)
            {
                for (int i = 0; i < store.SupplierReturns.Count; i++)
                {
                    StoreReturnRecord r = store.SupplierReturns[i];
                    if (r == null || r.IsCancelled || !(r.IsQualityReturn || r.Reason == "Брак" || r.Reason == "Истёк срок годности") || r.CreatedDay < startDay || r.CreatedDay > endDay) continue;
                    string key = string.IsNullOrEmpty(r.SupplierId) ? (string.IsNullOrEmpty(r.SupplierDisplayName) ? "unknown" : r.SupplierDisplayName) : r.SupplierId;
                    int[] m; if (!metrics.TryGetValue(key, out m)) { m = new int[6]; metrics.Add(key, m); }
                    if (!string.IsNullOrEmpty(r.SupplierDisplayName)) supplierNames[key] = r.SupplierDisplayName;
                    m[2] += Math.Max(0, r.Units);
                }
            }
            foreach (KeyValuePair<string, int[]> pair in metrics)
            {
                int[] m = pair.Value;
                float quality = Mathf.Clamp01(1f - (float)m[2] / Math.Max(1, m[1]));
                float punctuality = m[0] > 0 ? (float)m[4] / m[0] : 0f;
                float completeness = m[0] > 0 ? Mathf.Clamp01((float)(m[1] - m[3]) / Math.Max(1, m[1])) : 0f;
                SupplierRatingSnapshotRecord snapshot = new SupplierRatingSnapshotRecord();
                snapshot.SupplierId = pair.Key; string displayName; snapshot.SupplierDisplayName = supplierNames.TryGetValue(pair.Key, out displayName) ? displayName : (pair.Key == "unknown" ? "Поставщик не указан" : pair.Key);
                snapshot.PeriodNumber = period; snapshot.PeriodStartDay = startDay; snapshot.PeriodEndDay = endDay;
                snapshot.CompletedDeliveries = m[0]; snapshot.AcceptedUnits = m[5]; snapshot.DefectiveUnits = m[2]; snapshot.ShortUnits = m[3]; snapshot.OnTimeDeliveries = m[4];
                snapshot.QualityScore = Mathf.RoundToInt(quality * 100f); snapshot.PunctualityScore = Mathf.RoundToInt(punctuality * 100f); snapshot.CompletenessScore = Mathf.RoundToInt(completeness * 100f);
                snapshot.Score = Mathf.RoundToInt((quality * 0.50f + punctuality * 0.25f + completeness * 0.25f) * 100f);
                snapshot.IsProvisional = m[0] < 3; store.SupplierRatingHistory.Add(snapshot);
            }
            store.LastSupplierRatingSnapshotPeriod = period;
            // Keep a bounded history while preserving the latest 24 economic months.
            if (store.SupplierRatingHistory.Count > 24) store.SupplierRatingHistory.RemoveRange(0, store.SupplierRatingHistory.Count - 24);
        }
        SaveCity();
    }

    private void EnsureGameClock()
    {
        gameClock = GameClock.Instance;
        if (gameClock == null)
        {
            GameObject clockObject = new GameObject("MyCity_GameClock");
            gameClock = clockObject.AddComponent<GameClock>();
            gameClock.SetTime(1, 8 * 60, 8 * 60);
        }
        gameClock.OnTick -= HandleGameMinute;
        gameClock.OnTick += HandleGameMinute;
    }

    private void CreateMaterials()
    {
        materials["grass"] = Mat(new Color(.30f,.43f,.29f)); materials["road"] = Mat(new Color(.13f,.15f,.18f));
        materials["sidewalk"] = Mat(new Color(.58f,.59f,.56f)); materials["line"] = Mat(new Color(.88f,.79f,.53f));
        materials["glass"] = Mat(new Color(.18f,.48f,.62f),.25f,.8f); materials["roof"] = Mat(new Color(.20f,.22f,.25f));
        materials["brick"] = Mat(new Color(.68f,.48f,.36f)); materials["cream"] = Mat(new Color(.82f,.76f,.64f));
        materials["white"] = Mat(new Color(.82f,.84f,.82f)); materials["wood"] = Mat(new Color(.34f,.22f,.14f));
        materials["leaf"] = Mat(new Color(.16f,.34f,.19f)); materials["shop"] = Mat(new Color(.72f,.58f,.36f));
        materials["resident"] = Mat(new Color(.25f,.38f,.72f)); materials["park"] = Mat(new Color(.24f,.48f,.28f));
        materials["streetMetal"] = Mat(new Color(.22f,.25f,.28f), .72f, .55f); materials["streetLamp"] = Mat(new Color(.92f,.78f,.48f), .05f, .8f); materials["bench"] = Mat(new Color(.38f,.24f,.14f), .05f, .35f);
        materials["districtResidential"] = Mat(new Color(.35f,.48f,.34f)); materials["districtCommercial"] = Mat(new Color(.48f,.43f,.30f)); materials["districtIndustrial"] = Mat(new Color(.38f,.40f,.44f)); materials["districtCivic"] = Mat(new Color(.42f,.48f,.50f)); materials["districtBorder"] = Mat(new Color(.76f,.72f,.58f), .1f, .35f);
        materials["metroConcrete"] = Mat(new Color(.34f,.37f,.40f), .18f, .45f); materials["metroTile"] = Mat(new Color(.72f,.76f,.75f), .05f, .55f); materials["metroBlue"] = Mat(new Color(.08f,.28f,.52f), .12f, .65f); materials["metroDark"] = Mat(new Color(.055f,.065f,.075f), .25f, .35f); materials["metroLight"] = Mat(new Color(.92f,.82f,.55f), .05f, .75f);
    }
    private Material Mat(Color c, float metal=0f, float smooth=.25f)
    {
        Shader s=Shader.Find("Universal Render Pipeline/Lit"); if(s==null)s=Shader.Find("Standard");
        Material m=new Material(s); m.color=c; if(m.HasProperty("_Metallic"))m.SetFloat("_Metallic",metal); if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",smooth); return m;
    }
    private GameObject Cube(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent=null)
    { GameObject g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=name; g.transform.position=pos; g.transform.localScale=scale; if(parent!=null){g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;} g.GetComponent<Renderer>().sharedMaterial=mat; return g; }
    private GameObject Sphere(string name, Vector3 pos, Vector3 scale, Material mat)
    { GameObject g=GameObject.CreatePrimitive(PrimitiveType.Sphere); g.name=name; g.transform.position=pos; g.transform.localScale=scale; g.GetComponent<Renderer>().sharedMaterial=mat; return g; }

    private void CreateWorld()
    {
        roadNetwork = new RoadNetworkGraph();
        // Register graph edges from the road segments generated below.
        for (int lane = -3; lane <= 3; lane++)
        {
            int fixedIndex = lane;
            for (int step = -6; step < 6; step++)
            {
                roadNetwork.AddSegment(new Vector2Int(fixedIndex, step), new Vector2Int(fixedIndex, step + 1));
                roadNetwork.AddSegment(new Vector2Int(step, fixedIndex), new Vector2Int(step + 1, fixedIndex));
            }
        }
        RoadRoutePlanner.SetNetwork(roadNetwork);
        pedestrianNetwork = new PedestrianNetworkGraph();
        GameObject ground=GameObject.CreatePrimitive(PrimitiveType.Plane); ground.name="Terrain | starter district"; ground.transform.localScale=new Vector3(14,1,14); ground.GetComponent<Renderer>().sharedMaterial=materials["grass"];
        // A connected street grid with sidewalks, junctions and markings.
        for(int i=-3;i<=3;i++)
        {
            float q=i*14f;
            Cube("Road_NS_"+i,new Vector3(q,.025f,0),new Vector3(7,.05f,190),materials["road"]);
            Cube("Road_EW_"+i,new Vector3(0,.025f,q),new Vector3(190,.05f,7),materials["road"]);
            Cube("Sidewalk_NS_A",new Vector3(q-4.1f,.04f,0),new Vector3(1.1f,.08f,190),materials["sidewalk"]);
            Cube("Sidewalk_NS_B",new Vector3(q+4.1f,.04f,0),new Vector3(1.1f,.08f,190),materials["sidewalk"]);
            Cube("Sidewalk_EW_A",new Vector3(0,.04f,q-4.1f),new Vector3(190,.08f,1.1f),materials["sidewalk"]);
            Cube("Sidewalk_EW_B",new Vector3(0,.04f,q+4.1f),new Vector3(190,.08f,1.1f),materials["sidewalk"]);
            for(int m=-6;m<=6;m++) { float z=m*14f; if(Mathf.Abs(z)>2) Cube("LaneDash",new Vector3(q,.057f,z),new Vector3(.12f,.015f,3.4f),materials["line"]); }
        }
        RebuildPedestrianNetwork();
        // Small landscaped public square at the edge of the first district.
        CreateDistrictVisuals();
        // Original modern high-rise cluster; decorative prototype, separate from the district marker system.
        MyCity.Building.HighRiseDistrictGenerator.Create(new Vector3(49f, 0f, -49f));
        Cube("Pocket_Park",new Vector3(49,.09f,49),new Vector3(18,.18f,18),materials["park"]);
        for(int i=0;i<8;i++) AddTree(new Vector3(43+(i%4)*4,0,43+(i/4)*8));
        CreateStreetFurniture();
        CreateMetroInfrastructure();
        CreateAirportInfrastructure();
        RenderSettings.ambientLight=new Color(.66f,.70f,.76f); RenderSettings.fog=true; RenderSettings.fogColor=new Color(.68f,.73f,.78f); RenderSettings.fogDensity=.0015f;
        Light sun=new GameObject("Sun | Daylight").AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=1.15f; sun.transform.rotation=Quaternion.Euler(48,-32,0);
    }
    private void CreateDistrictVisuals()
    {
        // Visual district identity only. These overlays do not reserve plots or alter zoning, roads, saves or economy.
        CreateDistrictMarker("ЖИЛОЙ РАЙОН", new Vector3(-49f, 0f, 49f), materials["districtResidential"]);
        CreateDistrictMarker("ДЕЛОВОЙ РАЙОН", new Vector3(49f, 0f, -49f), materials["districtCommercial"]);
        CreateDistrictMarker("ПРОМЫШЛЕННЫЙ РАЙОН", new Vector3(49f, 0f, 49f), materials["districtIndustrial"]);
        CreateDistrictMarker("ОБЩЕСТВЕННЫЙ ЦЕНТР", new Vector3(-49f, 0f, -49f), materials["districtCivic"]);
    }

    private void CreateDistrictMarker(string label, Vector3 center, Material zoneMaterial)
    {
        // Four restrained corner pads and a low perimeter make districts readable without covering buildable land.
        float half = 17f;
        Vector3[] corners = {
            new Vector3(center.x-half, .018f, center.z-half), new Vector3(center.x+half, .018f, center.z-half),
            new Vector3(center.x-half, .018f, center.z+half), new Vector3(center.x+half, .018f, center.z+half)
        };
        for (int i=0;i<corners.Length;i++)
            Cube("DistrictCorner_"+label+"_"+i, corners[i], new Vector3(2.2f,.035f,2.2f), zoneMaterial);
        Cube("DistrictBorder_N_"+label, new Vector3(center.x,.025f,center.z+half), new Vector3(31f,.045f,.22f), materials["districtBorder"]);
        Cube("DistrictBorder_S_"+label, new Vector3(center.x,.025f,center.z-half), new Vector3(31f,.045f,.22f), materials["districtBorder"]);
        Cube("DistrictBorder_E_"+label, new Vector3(center.x+half,.025f,center.z), new Vector3(.22f,.045f,31f), materials["districtBorder"]);
        Cube("DistrictBorder_W_"+label, new Vector3(center.x-half,.025f,center.z), new Vector3(.22f,.045f,31f), materials["districtBorder"]);
        GameObject sign = Cube("DistrictSign_"+label, new Vector3(center.x,.9f,center.z+half-1.2f), new Vector3(7f,1.6f,.28f), materials["streetMetal"]);
        TextMesh text = new GameObject("DistrictLabel_"+label).AddComponent<TextMesh>();
        text.text = label; text.fontSize = 48; text.characterSize = .11f; text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center; text.color = new Color(.96f,.93f,.82f);
        text.transform.SetParent(sign.transform,false); text.transform.localPosition = new Vector3(0,0,.16f);
    }

    private void CreateMetroInfrastructure()
    {
        // Visual foundation only: two surface entrances and a compact underground station cutaway.
        CreateMetroEntrance("ЦЕНТРАЛЬНАЯ", new Vector3(-28f, 0f, -28f));
        CreateMetroEntrance("ВОКЗАЛЬНАЯ", new Vector3(28f, 0f, 28f));
        Cube("Metro_Underground_Station_Floor", new Vector3(0f, -1.15f, 0f), new Vector3(44f, .35f, 8f), materials["metroConcrete"]);
        Cube("Metro_Underground_Station_BackWall", new Vector3(0f, -.05f, 4.1f), new Vector3(44f, 2.2f, .35f), materials["metroConcrete"]);
        Cube("Metro_Underground_Station_FrontWall", new Vector3(0f, -.05f, -4.1f), new Vector3(44f, 2.2f, .35f), materials["metroConcrete"]);
        Cube("Metro_Platform_Left", new Vector3(0f, -.78f, -2.25f), new Vector3(38f, .22f, 1.35f), materials["metroTile"]);
        Cube("Metro_Platform_Right", new Vector3(0f, -.78f, 2.25f), new Vector3(38f, .22f, 1.35f), materials["metroTile"]);
        Cube("Metro_Safety_Line_Left", new Vector3(0f, -.655f, -1.58f), new Vector3(36f, .025f, .10f), materials["metroBlue"]);
        Cube("Metro_Safety_Line_Right", new Vector3(0f, -.655f, 1.58f), new Vector3(36f, .025f, .10f), materials["metroBlue"]);
        for (int side=-1; side<=1; side+=2)
        {
            float z=side*1.0f;
            Cube("Metro_Track_Bed_"+side, new Vector3(0f,-1.22f,z), new Vector3(42f,.12f,.9f), materials["metroDark"]);
            for(int rail=-1;rail<=1;rail+=2) Cube("Metro_Rail_"+side+"_"+rail, new Vector3(0f,-1.12f,z+rail*.23f), new Vector3(42f,.08f,.075f), materials["metroLight"]);
            for(int sleeper=-20;sleeper<=20;sleeper++) Cube("Metro_Sleeper_"+side+"_"+sleeper, new Vector3(sleeper, -1.16f, z), new Vector3(.16f,.08f,.72f), materials["wood"]);
        }
        for(int x=-18;x<=18;x+=6)
        {
            Cube("Metro_Column_"+x, new Vector3(x,-.05f,0f), new Vector3(.55f,2.0f,.55f), materials["metroConcrete"]);
            Cube("Metro_Ceiling_Light_"+x, new Vector3(x,.78f,0f), new Vector3(2.2f,.08f,.35f), materials["metroLight"]);
        }
        CreateMetroTrain();
    }

    private void CreateMetroEntrance(string stationName, Vector3 center)
    {
        Cube("Metro_Entrance_Base_"+stationName, new Vector3(center.x,.12f,center.z), new Vector3(9f,.24f,7f), materials["sidewalk"]);
        Cube("Metro_Entrance_Roof_"+stationName, new Vector3(center.x,2.45f,center.z), new Vector3(6.8f,.35f,4.8f), materials["metroBlue"]);
        Cube("Metro_Entrance_Left_"+stationName, new Vector3(center.x-3.1f,1.25f,center.z), new Vector3(.35f,2.2f,4.8f), materials["metroConcrete"]);
        Cube("Metro_Entrance_Right_"+stationName, new Vector3(center.x+3.1f,1.25f,center.z), new Vector3(.35f,2.2f,4.8f), materials["metroConcrete"]);
        Cube("Metro_Entrance_Back_"+stationName, new Vector3(center.x,1.25f,center.z+2.2f), new Vector3(6.5f,2.2f,.3f), materials["metroConcrete"]);
        Cube("Metro_Entrance_Glass_"+stationName, new Vector3(center.x,1.15f,center.z-2.15f), new Vector3(5.9f,1.8f,.08f), materials["glass"]);
        Cube("Metro_Entrance_Sign_"+stationName, new Vector3(center.x,3.0f,center.z-1.4f), new Vector3(2.8f,.42f,.22f), materials["metroDark"]);
        TextMesh sign=new GameObject("Metro_Entrance_Label_"+stationName).AddComponent<TextMesh>();
        sign.text="МЕТРО  •  "+stationName; sign.fontSize=42; sign.characterSize=.095f; sign.anchor=TextAnchor.MiddleCenter;
        sign.alignment=TextAlignment.Center; sign.color=Color.white; sign.transform.position=new Vector3(center.x,3.02f,center.z-1.26f);
        Cube("Metro_Stairwell_Dark_"+stationName, new Vector3(center.x,.16f,center.z-3.35f), new Vector3(3.6f,.12f,2.1f), materials["metroDark"]);
        for(int step=0;step<6;step++) Cube("Metro_Stair_"+stationName+"_"+step, new Vector3(center.x,.12f-step*.10f,center.z-2.55f-step*.28f), new Vector3(3.1f,.12f,.28f), materials["metroTile"]);
        Cube("Metro_Vent_"+stationName, new Vector3(center.x+4.2f,.65f,center.z+2.2f), new Vector3(.8f,1.3f,.8f), materials["metroConcrete"]);
        for(int grille=0;grille<4;grille++) Cube("Metro_Vent_Grille_"+stationName+"_"+grille, new Vector3(center.x+3.77f,.25f+grille*.22f,center.z+1.78f), new Vector3(.08f,.07f,.72f), materials["metroDark"]);
        GameObject pole=GameObject.CreatePrimitive(PrimitiveType.Cylinder); pole.name="Metro_M_Pole_"+stationName;
        pole.transform.position=new Vector3(center.x-4.1f,1.15f,center.z-2.6f); pole.transform.localScale=new Vector3(.12f,1.15f,.12f); pole.GetComponent<Renderer>().sharedMaterial=materials["metroBlue"];
        Cube("Metro_M_Sign_"+stationName,new Vector3(center.x-4.1f,2.45f,center.z-2.6f),new Vector3(1.0f,.9f,.18f),materials["metroBlue"]);
    }

    private void CreateMetroTrain()
    {
        for(int car=0;car<2;car++)
        {
            float x=-5f+car*10f;
            Cube("Metro_Train_Car_"+car,new Vector3(x,-.38f,0f),new Vector3(8.6f,1.35f,1.45f),materials["metroBlue"]);
            Cube("Metro_Train_Window_Band_"+car,new Vector3(x,-.18f,.755f),new Vector3(7.4f,.48f,.055f),materials["glass"]);
            for(int w=-3;w<=3;w+=2) Cube("Metro_Train_Window_"+car+"_"+w,new Vector3(x+w,-.17f,.80f),new Vector3(.95f,.43f,.04f),materials["metroTile"]);
            for(int wheel=-3;wheel<=3;wheel+=6) Sphere("Metro_Train_Wheel_"+car+"_"+wheel,new Vector3(x+wheel,-1.0f,.45f),new Vector3(.55f,.55f,.18f),materials["metroDark"]);
        }
    }


    private void CreateAirportInfrastructure()
    {
        // Airport visual foundation only. Runway, apron, terminal and aircraft are static props;
        // flight schedules, passenger simulation and air routes are not implemented in this build.
        Vector3 terminal = new Vector3(28f, 0f, -43f);
        Vector3 runway = new Vector3(58f, 0f, -68f);

        // Runway and shoulders.
        Cube("Airport_Runway_Base", runway + new Vector3(0f, .045f, 0f), new Vector3(78f, .09f, 12f), materials["road"]);
        Cube("Airport_Runway_Centerline", runway + new Vector3(0f, .102f, 0f), new Vector3(68f, .018f, .16f), materials["white"]);
        for (int x = -32; x <= 32; x += 8)
        {
            Cube("Airport_Runway_Dash_" + x, runway + new Vector3(x, .115f, 0f), new Vector3(3.2f, .025f, .22f), materials["white"]);
            for (int side = -1; side <= 1; side += 2)
                Cube("Airport_Runway_EdgeLight_" + x + "_" + side, runway + new Vector3(x, .18f, side * 5.5f), new Vector3(.22f, .18f, .22f), materials["metroLight"]);
        }
        // Taxiway and apron connect the runway to the passenger terminal.
        Cube("Airport_Taxiway", new Vector3(39f, .07f, -57f), new Vector3(7f, .12f, 25f), materials["road"]);
        Cube("Airport_Apron", new Vector3(30f, .08f, -55f), new Vector3(28f, .16f, 18f), materials["road"]);
        for (int stand = 0; stand < 2; stand++)
        {
            float x = 23f + stand * 13f;
            Cube("Airport_Stand_Marking_" + stand, new Vector3(x, .18f, -55f), new Vector3(.18f, .025f, 13f), materials["line"]);
            Cube("Airport_Stand_Stopbar_" + stand, new Vector3(x, .18f, -61f), new Vector3(5f, .025f, .18f), materials["white"]);
        }

        // Passenger terminal: main hall, glazed facade, roof and airside concourse.
        Cube("Airport_Terminal_Foundation", terminal + new Vector3(0f, .22f, 0f), new Vector3(27f, .44f, 13f), materials["sidewalk"]);
        Cube("Airport_Terminal_Main_Hall", terminal + new Vector3(0f, 3.1f, 0f), new Vector3(25f, 5.5f, 11f), materials["metroConcrete"]);
        Cube("Airport_Terminal_Glass_Facade", terminal + new Vector3(0f, 2.9f, -5.58f), new Vector3(22f, 4.5f, .12f), materials["glass"]);
        Cube("Airport_Terminal_Roof", terminal + new Vector3(0f, 5.95f, 0f), new Vector3(28f, .55f, 14f), materials["streetMetal"]);
        Cube("Airport_Terminal_Airside_Concourse", terminal + new Vector3(0f, 1.6f, 7.5f), new Vector3(20f, 2.7f, 3.5f), materials["metroBlue"]);
        Cube("Airport_Terminal_Sign", terminal + new Vector3(0f, 6.8f, -5.9f), new Vector3(12f, 1.0f, .25f), materials["metroDark"]);
        TextMesh airportSign = new GameObject("Airport_Terminal_Label").AddComponent<TextMesh>();
        airportSign.text = "АЭРОПОРТ  •  MY CITY"; airportSign.fontSize = 54; airportSign.characterSize = .12f;
        airportSign.anchor = TextAnchor.MiddleCenter; airportSign.alignment = TextAlignment.Center; airportSign.color = Color.white;
        airportSign.transform.position = terminal + new Vector3(0f, 6.82f, -5.72f);
        // Control tower.
        Cube("Airport_Control_Tower_Base", terminal + new Vector3(14f, 4.2f, 1f), new Vector3(2.4f, 8f, 2.4f), materials["metroConcrete"]);
        Cube("Airport_Control_Tower_Glass", terminal + new Vector3(14f, 8.5f, 1f), new Vector3(3.4f, 1.5f, 3.4f), materials["glass"]);
        Cube("Airport_Control_Tower_Roof", terminal + new Vector3(14f, 9.35f, 1f), new Vector3(3.8f, .25f, 3.8f), materials["streetMetal"]);
        // Static passenger jet on the apron. Long axis is X; wings span Z.
        CreateAirportAircraft(new Vector3(29f, .9f, -55f), "Airport_Aircraft_Demo");
    }

    private void CreateAirportAircraft(Vector3 center, string prefix)
    {
        Material fuselage = materials["white"];
        Material accent = materials["metroBlue"];
        Cube(prefix + "_Fuselage", center, new Vector3(13f, 1.15f, 1.35f), fuselage);
        Cube(prefix + "_Nose", center + new Vector3(6.8f, -.05f, 0f), new Vector3(1.2f, .82f, 1.0f), fuselage);
        Cube(prefix + "_TailFin", center + new Vector3(-5.4f, 1.25f, 0f), new Vector3(1.4f, 2.3f, .22f), accent);
        Cube(prefix + "_HorizontalTail", center + new Vector3(-5.2f, .35f, 0f), new Vector3(3.2f, .18f, 3.2f), fuselage);
        Cube(prefix + "_MainWing", center + new Vector3(.2f, -.12f, 0f), new Vector3(3.4f, .22f, 11.5f), fuselage);
        Cube(prefix + "_LeftEngine", center + new Vector3(1.0f, -.55f, -2.7f), new Vector3(2.1f, .72f, .82f), materials["streetMetal"]);
        Cube(prefix + "_RightEngine", center + new Vector3(1.0f, -.55f, 2.7f), new Vector3(2.1f, .72f, .82f), materials["streetMetal"]);
        Cube(prefix + "_BlueStripe", center + new Vector3(-.2f, .22f, .69f), new Vector3(8.5f, .16f, .035f), accent);
        for (int window = -4; window <= 4; window++)
            Cube(prefix + "_Window_" + window, center + new Vector3(window * .82f, .38f, .69f), new Vector3(.42f, .22f, .045f), materials["glass"]);
        for (int gear = -1; gear <= 1; gear += 2)
            Sphere(prefix + "_LandingGear_" + gear, center + new Vector3(1.8f, -.85f, gear * 1.4f), new Vector3(.42f, .42f, .42f), materials["metroDark"]);
    }

    private void CreateStreetFurniture()
    {
        // Street furniture is decorative only: it does not alter road or pedestrian graph topology.
        // Restrict placement to the starter district's main avenues to keep the mobile scene light.
        for (int avenue = -2; avenue <= 2; avenue += 2)
        {
            float axis = avenue * 14f;
            for (int step = -5; step <= 5; step += 2)
            {
                float along = step * 14f;
                CreateStreetLamp(new Vector3(axis - 5.05f, 0f, along));
                CreateStreetLamp(new Vector3(axis + 5.05f, 0f, along));
                CreateStreetLamp(new Vector3(along, 0f, axis - 5.05f));
                CreateStreetLamp(new Vector3(along, 0f, axis + 5.05f));
            }
        }

        // A small, readable bus stop near the central avenue.
        Vector3 stop = new Vector3(11.5f, 0f, 5.2f);
        Cube("Bus stop | platform", stop + new Vector3(0f,.08f,0f), new Vector3(5.4f,.16f,1.8f), materials["sidewalk"]);
        Cube("Bus stop | roof", stop + new Vector3(0f,2.65f,.15f), new Vector3(4.6f,.18f,1.65f), materials["streetMetal"]);
        Cube("Bus stop | back panel", stop + new Vector3(0f,1.35f,.82f), new Vector3(4.4f,2.45f,.12f), materials["glass"]);
        for (int x = -1; x <= 1; x += 2)
            Cube("Bus stop | support", stop + new Vector3(x*2.05f,1.3f,-.55f), new Vector3(.12f,2.6f,.12f), materials["streetMetal"]);
        Cube("Bus stop | bench seat", stop + new Vector3(0f,.62f,-.38f), new Vector3(2.2f,.16f,.48f), materials["bench"]);
        Cube("Bus stop | bench back", stop + new Vector3(0f,1.02f,-.58f), new Vector3(2.2f,.62f,.14f), materials["bench"]);
        Cube("Bus stop | sign", stop + new Vector3(2.8f,2.15f,-.6f), new Vector3(.55f,.8f,.12f), materials["streetMetal"]);

        // Benches and bins make the pocket park read as a usable public space.
        for (int i = 0; i < 2; i++)
        {
            Vector3 p = new Vector3(45f + i*8f, 0f, 53f);
            Cube("Park bench | seat", p + new Vector3(0f,.62f,0f), new Vector3(2.1f,.16f,.55f), materials["bench"]);
            Cube("Park bench | back", p + new Vector3(0f,1.02f,.22f), new Vector3(2.1f,.62f,.14f), materials["bench"]);
            Cube("Park bin", p + new Vector3(1.65f,.48f,-.25f), new Vector3(.48f,.9f,.48f), materials["streetMetal"]);
        }

        // A modest zebra crossing at the central intersection.
        for (int i = -3; i <= 3; i++)
        {
            Cube("Crosswalk | central", new Vector3(i*.82f,.065f,4.65f), new Vector3(.42f,.025f,2.1f), materials["white"]);
            Cube("Crosswalk | central", new Vector3(4.65f,.065f,i*.82f), new Vector3(2.1f,.025f,.42f), materials["white"]);
        }
    }

    private void CreateStreetLamp(Vector3 p)
    {
        Cube("Street lamp | pole", p + new Vector3(0f,2.15f,0f), new Vector3(.16f,4.3f,.16f), materials["streetMetal"]);
        Cube("Street lamp | arm", p + new Vector3(.42f,4.12f,0f), new Vector3(.95f,.12f,.12f), materials["streetMetal"]);
        Sphere("Street lamp | head", p + new Vector3(.88f,4.02f,0f), new Vector3(.48f,.2f,.34f), materials["streetLamp"]);
    }

    private void AddTree(Vector3 p)
    { GameObject trunk=GameObject.CreatePrimitive(PrimitiveType.Cylinder); trunk.name="Tree trunk"; trunk.transform.position=p+Vector3.up*.8f; trunk.transform.localScale=new Vector3(.25f,.8f,.25f); trunk.GetComponent<Renderer>().sharedMaterial=materials["wood"]; Sphere("Tree crown",p+Vector3.up*2.1f,new Vector3(2.2f,2.5f,2.2f),materials["leaf"]); }
    private void CreateCamera()
    {
        GameObject c=new GameObject("Main Camera"); cam=c.AddComponent<Camera>(); c.tag="MainCamera"; c.AddComponent<AudioListener>(); cam.fieldOfView=52; cam.farClipPlane=900; UpdateCamera();
    }
    private void UpdateCamera()
    { if(cam==null)return; Quaternion r=Quaternion.Euler(pitch,yaw,0); cam.transform.position=target+r*new Vector3(0,0,-distance); cam.transform.rotation=r; }

    private void Update()
    {
        if(cam==null)return;
        float move=18f*Time.deltaTime*Mathf.Max(1,distance/30f); Vector3 f=Quaternion.Euler(0,yaw,0)*Vector3.forward, r=Quaternion.Euler(0,yaw,0)*Vector3.right;
        if(Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow))target+=f*move; if(Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow))target-=f*move;
        if(Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow))target-=r*move; if(Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow))target+=r*move;
        if(Input.GetMouseButton(1)){yaw+=Input.GetAxis("Mouse X")*3.4f;pitch=Mathf.Clamp(pitch-Input.GetAxis("Mouse Y")*2.4f,18,78);}
        distance=Mathf.Clamp(distance-Input.mouseScrollDelta.y*2.5f,12,125); UpdateCamera();
        if(Input.GetKeyDown(KeyCode.Alpha1))tool=Tool.House; if(Input.GetKeyDown(KeyCode.Alpha2))tool=Tool.Cottage; if(Input.GetKeyDown(KeyCode.Alpha3))tool=Tool.Apartment; if(Input.GetKeyDown(KeyCode.Alpha4))tool=Tool.Grocery; if(Input.GetKeyDown(KeyCode.Alpha5))tool=Tool.Electronics; if(Input.GetKeyDown(KeyCode.Alpha6))tool=Tool.Clothing; if(Input.GetKeyDown(KeyCode.Alpha7))tool=Tool.Furniture; if(Input.GetKeyDown(KeyCode.Alpha8))tool=Tool.Cosmetics; if(Input.GetKeyDown(KeyCode.Alpha9))tool=Tool.Books; if(Input.GetKeyDown(KeyCode.Alpha0)){tool=Tool.Road;pendingRoadStart=null;} if(Input.GetKeyDown(KeyCode.B))tool=Tool.Warehouse; if(Input.GetKeyDown(KeyCode.H))tool=Tool.HighRise;
        if(Input.GetKeyDown(KeyCode.M))tool=Tool.Morgue; if(Input.GetKeyDown(KeyCode.C))tool=Tool.Cemetery;
        if(Input.GetKeyDown(KeyCode.R))quarterTurns=(quarterTurns+1)%4; if(Input.GetKeyDown(KeyCode.X))tool=Tool.Demolish;
        if(Input.GetKeyDown(KeyCode.Escape))tool=Tool.House;
        if(Input.GetMouseButtonDown(0)&&!PointerOverUI())HandleWorldClick(Input.mousePosition);
        if(Input.touchCount==1&&Input.GetTouch(0).phase==TouchPhase.Began)HandleWorldClick(Input.GetTouch(0).position);
        UpdateResidents();
    }

    private void UpdateResidents()
    {
        // A paused city must freeze both simulation time and resident movement.
        if(gameClock == null || gameClock.IsPaused) return;

        float simulationDelta = Time.unscaledDeltaTime * Mathf.Max(0f, gameClock.SpeedMultiplier);
        foreach(ResidentAgent a in residents)
        {
            if(a.Body == null) continue;
            bool hasWorkplace = !string.IsNullOrEmpty(a.WorkplaceBuildingId);
            PlacedBuilding workplace = hasWorkplace ? buildings.Find(b => b != null && b.Id == a.WorkplaceBuildingId) : null;
            bool hasFixedDestination = a.ScheduleState == CitizenDailyPhase.Sleep || (a.ScheduleState == CitizenDailyPhase.Work && workplace != null);
            if (hasFixedDestination)
            {
                Vector3 anchor = a.ScheduleState == CitizenDailyPhase.Sleep
                    ? a.Home.Entrance
                    : workplace.Entrance;
                if (a.Route == null || Vector3.Distance(a.RouteTarget, anchor) > .25f || a.RouteIndex >= a.Route.Count)
                    SetResidentRoute(a, anchor);
            }
            else
            {
                a.Wait -= simulationDelta;
                if (a.Route != null && a.RouteIndex >= a.Route.Count && a.ActivityMinutesRemaining > 0)
                {
                    // Residents stay at the destination while shopping, meeting or using a public service.
                    a.Wait = Mathf.Max(a.Wait, .25f);
                }
                else if (a.Wait <= 0f || (a.Route != null && a.RouteIndex >= a.Route.Count && a.ActivityMinutesRemaining <= 0))
                {
                    Vector3 activityPoint;
                    PlacedBuilding activityBuilding = ChoosePublicActivity(a, out activityPoint);
                    a.ActivityBuildingId = activityBuilding != null ? activityBuilding.Id : null;
                    a.Destination = activityPoint;
                    SetResidentRoute(a, activityPoint);
                    a.Wait = UnityEngine.Random.Range(2f, 6f);
                    a.ActivityMinutesRemaining = 0;
                }
            }
            if (a.Route != null && a.RouteIndex < a.Route.Count)
            {
                Vector3 next = a.Route[a.RouteIndex];
                a.Body.transform.position = Vector3.MoveTowards(a.Body.transform.position, next, simulationDelta * 1.15f);
                if (Vector3.Distance(a.Body.transform.position, next) < .12f)
                {
                    a.RouteIndex++;
                    if (a.RouteIndex >= a.Route.Count && !hasFixedDestination && !string.IsNullOrEmpty(a.ActivityBuildingId))
                    {
                        a.ActivityMinutesRemaining = UnityEngine.Random.Range(10, 46);
                        a.PublicVisitsToday++;
                        string visitDay = gameClock != null ? gameClock.Day.ToString() : "0";
                        if (a.Record != null)
                        {
                            if (a.Record.EventHistory == null) a.Record.EventHistory = new List<string>();
                            a.Record.EventHistory.Add("public-visit:" + a.ActivityBuildingId + ":" + visitDay);
                            if (a.Record.EventHistory.Count > 120) a.Record.EventHistory.RemoveAt(0);
                        }
                        string eventSource = "resident-public-visit";
                        string purchasedProduct;
                        long purchasePrice;
                        if (economySystem != null && economySystem.TryResidentRetailPurchase(a.Id, a.ActivityBuildingId,
                            gameClock != null ? gameClock.Day : 0, out purchasedProduct, out purchasePrice))
                        {
                            eventSource = "resident-shop-purchase";
                            if (a.Record != null && a.Record.EventHistory != null && a.Record.EventHistory.Count > 120)
                                a.Record.EventHistory.RemoveAt(0);
                        }
                        GameEventBus.Publish(new GameEvent(GameEventType.CitizenStateChanged,
                            gameClock != null ? gameClock.Day * 1440L + gameClock.MinuteOfDay : 0L,
                            eventSource, a.Id, a.ActivityBuildingId));
                    }
                }
            }
        }
    }
    private PlacedBuilding ChoosePublicActivity(ResidentAgent resident, out Vector3 destination)
    {
        destination = RandomStreetPoint();
        if (resident == null || buildings == null || buildings.Count == 0) return null;
        // Public-facing destinations are actual built businesses and civic/workplace buildings,
        // not decorative markers. Exclude the citizen's own home and prefer a reachable entrance.
        List<PlacedBuilding> candidates = new List<PlacedBuilding>();
        foreach (PlacedBuilding building in buildings)
        {
            if (building == null || building == resident.Home || building.Residential || building.Root == null) continue;
            if (!building.Workplace) continue;
            WorkplaceRecord store = FindWorkplaceRecord(building.Id);
            if (store == null || !store.IsRetailBusiness || !store.IsOpen) continue;
            candidates.Add(building);
        }
        if (candidates.Count == 0) return null;
        PlacedBuilding chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        destination = new Vector3(chosen.Entrance.x, .65f, chosen.Entrance.z);
        return chosen;
    }

    private void SetResidentRoute(ResidentAgent agent, Vector3 destination)
    {
        if (agent == null || agent.Body == null) return;
        agent.Route = pedestrianNetwork != null ? pedestrianNetwork.BuildRoute(agent.Body.transform.position, destination) : RoadRoutePlanner.Build(agent.Body.transform.position, destination);
        agent.RouteIndex = agent.Route.Count > 1 ? 1 : 0;
        agent.RouteTarget = destination;
    }
    private Vector3 RandomStreetPoint(){int lane=UnityEngine.Random.Range(-3,4)*14; if(UnityEngine.Random.value>.5f)return new Vector3(lane+UnityEngine.Random.Range(-2.4f,2.4f),.65f,UnityEngine.Random.Range(-80,80));return new Vector3(UnityEngine.Random.Range(-80,80),.65f,lane+UnityEngine.Random.Range(-2.4f,2.4f));}
    private bool PointerOverUI(){Vector2 p=new Vector2(Input.mousePosition.x,Screen.height-Input.mousePosition.y);return p.x<340&&p.y<760||p.x>Screen.width-260&&p.y<600||p.y>Screen.height-65;}
    private void HandleWorldClick(Vector2 screen)
    {
        Ray ray=cam.ScreenPointToRay(screen); if(!Physics.Raycast(ray,out RaycastHit hit,1000))return;
        PlacedBuilding clicked = buildings.Find(x => x != null && x.Root != null &&
            (hit.collider.transform.IsChildOf(x.Root.transform) || hit.collider.gameObject == x.Root));
        if (clicked != null && tool != Tool.Demolish)
        {
            if (highRiseInteriorMode) SetHighRiseInteriorMode(false);
            selected = clicked;
            status = clicked.Name + (clicked.Workplace ? " — рабочее место." : clicked.Residential ? " — жилой дом." : ".");
            return;
        }
        if(tool==Tool.Demolish){
            Tuple<Vector2Int, Vector2Int> edge;
            if (playerRoadEdges.TryGetValue(hit.collider.gameObject, out edge))
            {
                roadNetwork.RemoveSegment(edge.Item1, edge.Item2);
                List<GameObject> sidewalkParts;
                if (playerRoadSidewalks.TryGetValue(hit.collider.gameObject, out sidewalkParts))
                {
                    foreach (GameObject part in sidewalkParts) if (part != null) Destroy(part);
                    playerRoadSidewalks.Remove(hit.collider.gameObject);
                }
                playerRoadEdges.Remove(hit.collider.gameObject); playerRoadObjects.Remove(hit.collider.gameObject);
                Destroy(hit.collider.gameObject); RebuildPedestrianNetwork(); InvalidateResidentRoutes(); status = "Построенный дорожный участок удалён. Пешеходная сеть и маршруты обновлены."; return;
            }
            PlacedBuilding b=buildings.Find(x=>hit.collider.transform.IsChildOf(x.Root.transform)||hit.collider.gameObject==x.Root);
            if(b!=null)Demolish(b);else status="Выберите здание или построенный дорожный участок для сноса.";return;
        }
        Vector3 p=hit.point;p.x=Mathf.Round(p.x/2f)*2f;p.z=Mathf.Round(p.z/2f)*2f;
        if (tool == Tool.Road) { HandleRoadPlacement(hit.point); return; }
        if(Mathf.Abs(p.x)>88||Mathf.Abs(p.z)>88){status="За пределами доступного района. Расширение территории будет отдельной системой.";return;}
        if(Mathf.Abs(p.x%14f)<5.4f||Mathf.Abs(p.z%14f)<5.4f){status="Нельзя строить на проезжей части или тротуаре.";return;}
        if(!HasRoadAccess(p)){status="Нет доступа к дороге. Участок должен примыкать к уличной сети.";return;}
        Vector3 size=tool==Tool.HighRise?new Vector3(16,1,16):tool==Tool.Apartment?new Vector3(9,1,9):tool==Tool.Warehouse?new Vector3(12,1,10):new Vector3(7,1,8);
        foreach(PlacedBuilding b in buildings)if(Mathf.Abs(b.Root.transform.position.x-p.x)<(size.x+8)/2f&&Mathf.Abs(b.Root.transform.position.z-p.z)<(size.z+8)/2f){status="Участок занят другим зданием.";return;}
        BuildAt(p);
    }
    private Vector2Int SnapRoadNode(Vector3 world)
    {
        return new Vector2Int(Mathf.RoundToInt(world.x / 14f), Mathf.RoundToInt(world.z / 14f));
    }

    private void HandleRoadPlacement(Vector3 hitPoint)
    {
        Vector2Int node = SnapRoadNode(hitPoint);
        if (Mathf.Abs(node.x) > 12 || Mathf.Abs(node.y) > 12)
        { status = "За пределами доступной территории."; pendingRoadStart = null; return; }
        if (!pendingRoadStart.HasValue)
        {
            if (!roadNetwork.Contains(node))
            { status = "Начало дороги должно примыкать к существующей дорожной сети."; return; }
            pendingRoadStart = node;
            status = "Начало выбрано: " + node.x + ", " + node.y + ". Выберите конечный узел по одной оси.";
            return;
        }

        Vector2Int start = pendingRoadStart.Value;
        pendingRoadStart = null;
        if (start == node || (start.x != node.x && start.y != node.y))
        { status = "Дорога должна быть прямой. Выберите узел на той же горизонтали или вертикали."; return; }
        int length = Mathf.Abs(start.x - node.x) + Mathf.Abs(start.y - node.y);
        if (length > 4)
        { status = "Один участок может иметь длину не более четырёх квартальных отрезков."; return; }
        if (!roadNetwork.Contains(start) && !roadNetwork.Contains(node))
        { status = "Новый участок должен соединяться с существующей сетью."; return; }
        if (roadNetwork.HasSegment(start, node))
        { status = "Такое дорожное соединение уже существует."; return; }
        int cost = length * 5000;
        if (money < cost) { status = "Недостаточно средств. Стоимость: " + cost.ToString("N0") + " ₽."; return; }

        Vector3 a = new Vector3(start.x * 14f, .035f, start.y * 14f);
        Vector3 b = new Vector3(node.x * 14f, .035f, node.y * 14f);
        Vector3 midpoint = (a + b) * .5f;
        Vector3 scale = start.x == node.x
            ? new Vector3(7f, .07f, length * 14f + 7f)
            : new Vector3(length * 14f + 7f, .07f, 7f);
        GameObject road = Cube("Player_Road_" + start.x + "_" + start.y + "_" + node.x + "_" + node.y, midpoint, scale, materials["road"]);
        roadNetwork.AddSegment(start, node);
        RebuildPedestrianNetwork();
        playerRoadEdges[road] = Tuple.Create(start, node);
        playerRoadSidewalks[road] = CreateRoadSidewalkVisuals(start, node);
        playerRoadObjects.Add(road);
        money -= cost;
        InvalidateResidentRoutes();
        status = "Дорога построена. Длина: " + length + ", стоимость: " + cost.ToString("N0") + " ₽.";
    }

    private void InvalidateResidentRoutes()
    {
        foreach (ResidentAgent resident in residents)
        {
            if (resident == null) continue;
            resident.Route = null;
            resident.RouteIndex = 0;
        }
    }

    private void RestorePlayerRoads()
    {
        CitySaveData data;
        string error;
        if (!CitySaveService.Load(out data, out error) || data == null || data.PlayerRoadSegments == null) return;
        foreach (RoadSegmentRecord record in data.PlayerRoadSegments)
        {
            if (record == null) continue;
            Vector2Int a = new Vector2Int(record.StartX, record.StartZ);
            Vector2Int b = new Vector2Int(record.EndX, record.EndZ);
            if (a == b || (a.x != b.x && a.y != b.y) || roadNetwork.HasSegment(a, b)) continue;
            int length = Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
            if (length < 1 || length > 4) continue;
            Vector3 pa = new Vector3(a.x * 14f, .035f, a.y * 14f);
            Vector3 pb = new Vector3(b.x * 14f, .035f, b.y * 14f);
            Vector3 midpoint = (pa + pb) * .5f;
            Vector3 scale = a.x == b.x
                ? new Vector3(7f, .07f, length * 14f + 7f)
                : new Vector3(length * 14f + 7f, .07f, 7f);
            GameObject road = Cube("Player_Road_Restored", midpoint, scale, materials["road"]);
            roadNetwork.AddSegment(a, b);
            playerRoadEdges[road] = Tuple.Create(a, b);
            playerRoadSidewalks[road] = CreateRoadSidewalkVisuals(a, b);
            playerRoadObjects.Add(road);
        }
        RebuildPedestrianNetwork();
        InvalidateResidentRoutes();
        status = "Построенные игроком дороги восстановлены из сохранения.";
    }

    private List<GameObject> CreateRoadSidewalkVisuals(Vector2Int a, Vector2Int b)
    {
        List<GameObject> result = new List<GameObject>();
        Vector3 pa = new Vector3(a.x * 14f, .045f, a.y * 14f);
        Vector3 pb = new Vector3(b.x * 14f, .045f, b.y * 14f);
        Vector3 midpoint = (pa + pb) * .5f;
        float length = Vector2Int.Distance(a, b) * 14f + 1f;
        if (a.x == b.x)
        {
            result.Add(Cube("Player_Sidewalk", midpoint + Vector3.left * 4.1f, new Vector3(1.1f, .08f, length), materials["sidewalk"]));
            result.Add(Cube("Player_Sidewalk", midpoint + Vector3.right * 4.1f, new Vector3(1.1f, .08f, length), materials["sidewalk"]));
        }
        else
        {
            result.Add(Cube("Player_Sidewalk", midpoint + Vector3.forward * 4.1f, new Vector3(length, .08f, 1.1f), materials["sidewalk"]));
            result.Add(Cube("Player_Sidewalk", midpoint + Vector3.back * 4.1f, new Vector3(length, .08f, 1.1f), materials["sidewalk"]));
        }
        return result;
    }

    private void RebuildPedestrianNetwork()
    {
        if (roadNetwork == null) return;
        if (pedestrianNetwork == null) pedestrianNetwork = new PedestrianNetworkGraph();
        pedestrianNetwork.Clear();
        const float sidewalkOffset = 4.1f;
        List<Vector2Int[]> segments = roadNetwork.GetSegments();
        HashSet<Vector2Int> junctions = new HashSet<Vector2Int>();
        foreach (Vector2Int[] segment in segments)
        {
            Vector2Int a = segment[0], b = segment[1];
            junctions.Add(a); junctions.Add(b);
            Vector3 wa = new Vector3(a.x * 14f, .68f, a.y * 14f);
            Vector3 wb = new Vector3(b.x * 14f, .68f, b.y * 14f);
            if (a.x == b.x)
            {
                pedestrianNetwork.AddWalkway(wa + Vector3.left * sidewalkOffset, wb + Vector3.left * sidewalkOffset);
                pedestrianNetwork.AddWalkway(wa + Vector3.right * sidewalkOffset, wb + Vector3.right * sidewalkOffset);
            }
            else
            {
                pedestrianNetwork.AddWalkway(wa + Vector3.forward * sidewalkOffset, wb + Vector3.forward * sidewalkOffset);
                pedestrianNetwork.AddWalkway(wa + Vector3.back * sidewalkOffset, wb + Vector3.back * sidewalkOffset);
            }
        }
        // Corner links let pedestrians change sidewalk sides at street junctions.
        foreach (Vector2Int node in junctions)
        {
            Vector3 center = new Vector3(node.x * 14f, .68f, node.y * 14f);
            Vector3 nw = center + new Vector3(-sidewalkOffset, 0, sidewalkOffset);
            Vector3 ne = center + new Vector3(sidewalkOffset, 0, sidewalkOffset);
            Vector3 sw = center + new Vector3(-sidewalkOffset, 0, -sidewalkOffset);
            Vector3 se = center + new Vector3(sidewalkOffset, 0, -sidewalkOffset);
            pedestrianNetwork.ConnectCrossing(nw, ne); pedestrianNetwork.ConnectCrossing(ne, se);
            pedestrianNetwork.ConnectCrossing(se, sw); pedestrianNetwork.ConnectCrossing(sw, nw);
        }
        // Each placed building receives a stable entrance anchor facing its nearest street.
        foreach (PlacedBuilding building in buildings)
        {
            if (building == null || building.Root == null) continue;
            Vector3 localDoor = building.EntranceLocal;
            Vector3 worldDoor = building.Root.transform.TransformPoint(localDoor);
            Vector3 outward = building.Root.transform.TransformDirection(new Vector3(localDoor.x, 0f, localDoor.z)).normalized;
            Vector3 entrance = worldDoor + outward * 1.15f;
            entrance.y = .68f;
            building.Entrance = entrance;
        }
    }

    private bool HasRoadAccess(Vector3 p){float dx=Mathf.Abs(p.x-Mathf.Round(p.x/14f)*14f),dz=Mathf.Abs(p.z-Mathf.Round(p.z/14f)*14f);return dx<10.5f||dz<10.5f;}

    private void BuildAt(Vector3 p) { BuildAt(p, null); }

    private void BuildAt(Vector3 p, BuildingRecord restoreRecord)
    {
        string name="Жилой дом";int cost=24000,capacity=4;bool residential=true;bool workplace=false;float floors=2.8f;Vector3 footprint=new Vector3(7,1,7);Material facade=materials["cream"];
        if(tool==Tool.Cottage){name="Коттедж";cost=38000;capacity=5;floors=3.5f;footprint=new Vector3(7,1,8);facade=materials["brick"];}
        if(tool==Tool.Morgue){name="Морг";cost=95000;capacity=0;residential=false;workplace=false;floors=4.5f;footprint=new Vector3(10,1,8);facade=materials["white"];}
        if(tool==Tool.Cemetery){name="Кладбище";cost=125000;capacity=0;residential=false;workplace=false;floors=0.35f;footprint=new Vector3(20,1,18);facade=materials["cream"];}
        if(tool==Tool.Apartment){name="Многоквартирный дом";cost=98000;capacity=24;floors=12f;footprint=new Vector3(9,1,9);facade=materials["white"];}
        if(tool==Tool.HighRise)
        {
            residential = selectedHighRiseType != 0;
            workplace = false;
            if(selectedHighRiseType == 0) { name="Офисный небоскрёб"; cost=180000; capacity=0; floors=38f; footprint=new Vector3(14,1,14); facade=materials["glass"]; }
            else if(selectedHighRiseType == 1) { name="Жилой небоскрёб"; cost=220000; capacity=120; floors=44f; footprint=new Vector3(14,1,14); facade=materials["white"]; }
            else { name="Многофункциональный небоскрёб"; cost=250000; capacity=80; floors=50f; footprint=new Vector3(16,1,16); facade=materials["glass"]; }
        }
        int staffCapacity=0;
        bool warehouseBuilding = tool == Tool.Warehouse || (restoreRecord != null && (restoreRecord.DefinitionId == "Складской комплекс" || restoreRecord.DefinitionId == "Оптовая база" || restoreRecord.DefinitionId == "Промышленный склад" || restoreRecord.DefinitionId == "Топливное хранилище"));
        string warehouseTypeId = "distribution"; int warehouseCapacity = 10000; int warehouseDocks = 3;
        if (warehouseBuilding)
        {
            string[] warehouseNames = { "Распределительный центр", "Оптовая база", "Промышленный склад", "Топливное хранилище" };
            string[] warehouseIds = { "distribution", "wholesale", "industrial", "fuel" };
            int[] warehouseCosts = { 85000, 110000, 145000, 190000 };
            int[] warehouseCapacities = { 10000, 16000, 22000, 12000 };
            int[] warehouseDockCounts = { 3, 4, 4, 2 };
            int typeIndex = restoreRecord != null ? Array.IndexOf(new string[] { "Распределительный центр", "Оптовая база", "Промышленный склад", "Топливное хранилище" }, restoreRecord.DefinitionId) : selectedWarehouseType;
            if (typeIndex < 0 || typeIndex >= warehouseNames.Length) typeIndex = 0;
            warehouseTypeId = warehouseIds[typeIndex]; warehouseCapacity = warehouseCapacities[typeIndex]; warehouseDocks = warehouseDockCounts[typeIndex];
            name = warehouseNames[typeIndex]; cost = warehouseCosts[typeIndex]; capacity = 0; residential = false; workplace = false;
            floors = typeIndex == 3 ? 3.2f : 5.2f; footprint = typeIndex == 3 ? new Vector3(14,1,12) : new Vector3(12,1,10); facade = materials["shop"];
        }
        string storeTypeId=null, productDisplayName=null; long wholesaleCost=200, retailPrice=350; int targetStock=100;
        if(tool==Tool.Grocery || tool==Tool.Electronics || tool==Tool.Clothing || tool==Tool.Furniture || tool==Tool.Cosmetics || tool==Tool.Books)
        {
            workplace=true; capacity=0; residential=false; floors=4; footprint=new Vector3(7,1,7); facade=materials["shop"]; staffCapacity=4;
            cost=31000;
            if(tool==Tool.Grocery){name="Продуктовый магазин";storeTypeId="grocery";productDisplayName="Продукты";wholesaleCost=120;retailPrice=220;targetStock=180;}
            else if(tool==Tool.Electronics){name="Магазин техники";storeTypeId="electronics";productDisplayName="Электроника и бытовая техника";wholesaleCost=18000;retailPrice=26000;targetStock=18;cost=52000;}
            else if(tool==Tool.Clothing){name="Магазин одежды и обуви";storeTypeId="clothing";productDisplayName="Одежда и обувь";wholesaleCost=1800;retailPrice=3200;targetStock=70;cost=39000;}
            else if(tool==Tool.Furniture){name="Мебельный магазин";storeTypeId="furniture";productDisplayName="Мебель и товары для дома";wholesaleCost=9500;retailPrice=14500;targetStock=30;cost=47000;}
            else if(tool==Tool.Cosmetics){name="Магазин косметики";storeTypeId="cosmetics";productDisplayName="Косметика и бытовая химия";wholesaleCost=650;retailPrice=1100;targetStock=90;cost=36000;}
            else {name="Книжный магазин";storeTypeId="books";productDisplayName="Книги и канцелярия";wholesaleCost=450;retailPrice=800;targetStock=85;cost=34000;}
        }
        if(restoreRecord == null && money<cost){status="Недостаточно средств городского бюджета.";return;}
        GameObject root=new GameObject(name);root.transform.position=new Vector3(p.x,0,p.z);root.transform.rotation=Quaternion.Euler(0,(restoreRecord != null ? restoreRecord.RotationQuarterTurns : quarterTurns)*90,0);
        float h=floors;Cube("Facade",new Vector3(0,h/2,0),new Vector3(footprint.x,h,footprint.z),facade,root.transform);
        Cube("Roof",new Vector3(0,h+.18f,0),new Vector3(footprint.x+.35f,.36f,footprint.z+.35f),materials["roof"],root.transform);
        if(tool == Tool.HighRise) CreateHighRiseBuildingDetails(root.transform, footprint, h, selectedHighRiseType);
        if(tool == Tool.Cemetery)
        {
            Cube("Cemetery lawn", new Vector3(0,.18f,0), new Vector3(footprint.x-.5f,.18f,footprint.z-.5f), materials["cream"], root.transform);
            for(int grave=0;grave<36;grave++)
            {
                float gx=-7.5f+(grave%6)*3f; float gz=-6f+(grave/6)*2f;
                Cube("Grave marker",new Vector3(gx,.72f,gz),new Vector3(.42f,1.1f,.18f),materials["white"],root.transform);
                Cube("Grave plot",new Vector3(gx,.32f,gz+ .55f),new Vector3(1.15f,.12f,1.35f),materials["road"],root.transform);
            }
        }
        if(tool == Tool.Morgue)
        {
            Cube("Morgue entrance canopy",new Vector3(0,2.2f,-footprint.z/2f-.7f),new Vector3(4.5f,.3f,1.4f),materials["roof"],root.transform);
            Cube("Morgue sign",new Vector3(0,3.4f,-footprint.z/2f-.08f),new Vector3(3.2f,.5f,.12f),materials["white"],root.transform);
        }
        if(warehouseBuilding){
            if (warehouseTypeId == "fuel")
            {
                Cube("Fuel tank A", new Vector3(-3.4f, 2.1f, 0f), new Vector3(4.2f, 4.2f, 4.2f), materials["white"], root.transform);
                Cube("Fuel tank B", new Vector3(3.4f, 2.1f, 0f), new Vector3(4.2f, 4.2f, 4.2f), materials["white"], root.transform);
                Cube("Fuel safety berm", new Vector3(0f, .2f, 0f), new Vector3(13f, .35f, 8f), materials["road"], root.transform);
            }
            Cube("Loading dock canopy",new Vector3(0,2.1f,-footprint.z/2f-1.4f),new Vector3(footprint.x*.72f,.35f,2.8f),materials["roof"],root.transform);
            Cube("Truck apron",new Vector3(0,.045f,-footprint.z/2f-5.2f),new Vector3(footprint.x*1.05f,.08f,7.2f),materials["road"],root.transform);
            for(int dock=-1;dock<=1;dock++){float dx=dock*3.1f;Cube("Loading bay door",new Vector3(dx,1.45f,-footprint.z/2f-.04f),new Vector3(2.5f,2.8f,.12f),materials["white"],root.transform);Cube("Dock bumper",new Vector3(dx, .25f,-footprint.z/2f-.28f),new Vector3(2.8f,.35f,.35f),materials["road"],root.transform);}
            Cube("Warehouse sign",new Vector3(0,4.25f,-footprint.z/2f-.12f),new Vector3(5.2f,.65f,.18f),materials["white"],root.transform);
        }
        // Repeated facade bays and windows create readable architecture rather than a plain block.
        int floorsCount=Mathf.Max(1,Mathf.RoundToInt(h/2.3f));int bays=Mathf.Max(2,Mathf.RoundToInt(footprint.x/1.8f));
        if(tool != Tool.Cemetery) for(int fy=0;fy<floorsCount;fy++)for(int bx=0;bx<bays;bx++){
            float x=-footprint.x/2f+.9f+bx*(footprint.x-1.8f)/Mathf.Max(1,bays-1);float y=.8f+fy*(h-.9f)/Mathf.Max(1,floorsCount);
            Cube("Window",new Vector3(x,y,-footprint.z/2f-.012f),new Vector3(.82f,.92f,.05f),materials["glass"],root.transform);
            Cube("Window side",new Vector3(footprint.x/2f+.012f,y,x-footprint.x/2f),new Vector3(.05f,.92f,.82f),materials["glass"],root.transform);
        }
        if(tool==Tool.Cottage){Cube("Porch",new Vector3(0,.55f,-footprint.z/2f-.75f),new Vector3(2.2f,1.1f,1.5f),materials["wood"],root.transform);}
        if (restoreRecord == null) money-=cost;
        int restoredLevel = restoreRecord != null ? Mathf.Clamp(restoreRecord.Level, 1, 15) : 1; int upgradedCapacity = residential ? Mathf.RoundToInt(capacity * Mathf.Pow(1.10f, restoredLevel - 1)) : capacity; int upgradedStaff = workplace ? staffCapacity + restoredLevel - 1 : staffCapacity;
        bool isHighRise = name.IndexOf("небоскрёб", StringComparison.OrdinalIgnoreCase) >= 0;
        List<HighRiseFloorRecord> floorPlan = isHighRise && restoreRecord != null && restoreRecord.HighRiseFloors != null && restoreRecord.HighRiseFloors.Count > 0
            ? new List<HighRiseFloorRecord>(restoreRecord.HighRiseFloors) : (isHighRise ? CreateHighRiseFloorPlan(selectedHighRiseType, capacity) : new List<HighRiseFloorRecord>());
        PlacedBuilding b=new PlacedBuilding{Root=root,Id=restoreRecord != null && !string.IsNullOrEmpty(restoreRecord.Id) ? restoreRecord.Id : StableId.NewId().ToString(),Name=name,Level=restoredLevel,Condition=restoreRecord != null ? Mathf.Clamp01(restoreRecord.Condition) : 1f,Capacity=upgradedCapacity,BaseCapacity=capacity,Cost=restoreRecord != null && restoreRecord.ConstructionCost > 0 ? restoreRecord.ConstructionCost : cost,Residential=residential,Workplace=workplace,StaffCapacity=upgradedStaff,Footprint=footprint,VisualHeight=h,IsHighRise=isHighRise,HighRiseFloors=floorPlan,ElevatorCars=isHighRise ? (restoreRecord != null && restoreRecord.ElevatorCars > 0 ? restoreRecord.ElevatorCars : (floorPlan.Count >= 45 ? 4 : 3)) : 0,ElevatorTripsToday=restoreRecord != null ? restoreRecord.ElevatorTripsToday : 0,LastHighRiseSettlementDay=restoreRecord != null ? restoreRecord.HighRiseLastSettlementDay : 0,DailyHighRiseRent=restoreRecord != null ? restoreRecord.HighRiseDailyRent : 0,DailyHighRisePayroll=restoreRecord != null ? restoreRecord.HighRiseDailyPayroll : 0,FacilityCapacity=restoreRecord != null && restoreRecord.FacilityCapacity > 0 ? restoreRecord.FacilityCapacity : (name == "Морг" ? 20 : name == "Кладбище" ? 250 : 0)};
        buildings.Add(b);selected=b;ApplyBuildingVisualEvolution(b);
        if (b.IsHighRise) CreateHighRiseInteriorGeometry(b);
        // Door faces the closest street; the same local anchor drives resident navigation.
        float roadX = Mathf.Round(p.x / 14f) * 14f;
        float roadZ = Mathf.Round(p.z / 14f) * 14f;
        Vector3 worldFacing = Mathf.Abs(p.x - roadX) < Mathf.Abs(p.z - roadZ)
            ? new Vector3(Mathf.Sign(roadX - p.x), 0f, 0f)
            : new Vector3(0f, 0f, Mathf.Sign(roadZ - p.z));
        Vector3 localFacing = root.transform.InverseTransformDirection(worldFacing).normalized;
        Vector3 doorLocal = new Vector3(localFacing.x * footprint.x * .5f, .9f, localFacing.z * footprint.z * .5f);
        b.EntranceLocal = new Vector3(doorLocal.x, .15f, doorLocal.z);
        Cube("Building entrance", doorLocal, new Vector3(1.05f, 1.8f, .08f), materials["wood"], root.transform);
        RebuildPedestrianNetwork();
        if(warehouseBuilding && warehouseStorageSystem != null){
            string warehouseId = "warehouse-" + b.Id;
            WarehouseRecord regional = warehouseStorageSystem.Find(warehouseId);
            if (regional == null)
            {
                List<WarehouseRecord> existingWarehouses = new List<WarehouseRecord>(); warehouseStorageSystem.Export(existingWarehouses);
                regional = existingWarehouses.Find(r => r != null && r.BuildingId == b.Id);
            }
            if(regional == null){regional = new WarehouseRecord{Id=warehouseId,DisplayName=name,WarehouseTypeId=warehouseTypeId,CapacityUnits=warehouseCapacity,DockCount=warehouseDocks,IsOperating=true,Inventory=new List<WarehouseInventoryRecord>()};warehouseStorageSystem.Register(regional);}
            else if (regional.Id != warehouseId) { warehouseStorageSystem.Remove(regional.Id); regional.Id = warehouseId; warehouseStorageSystem.Register(regional); }
            regional.BuildingId=b.Id; regional.DisplayName=name; regional.WarehouseTypeId=warehouseTypeId; regional.CapacityUnits=warehouseCapacity; regional.DockCount=warehouseDocks;
            Vector2Int warehouseNode = roadNetwork != null ? roadNetwork.NearestNode(new Vector3(p.x,0f,p.z),14f) : Vector2Int.zero;
            regional.RoadNodeX=warehouseNode.x;regional.RoadNodeZ=warehouseNode.y;
            status=name+" построен. Вместимость: "+warehouseCapacity+" ед.; доков: "+warehouseDocks+". Подъездной узел: "+warehouseNode.x+", "+warehouseNode.y+".";
        }
        if (workplace && workScheduleSystem != null)
        {
            WorkplaceRecord workplaceRecord = new WorkplaceRecord
            {
                Id = "workplace-" + b.Id, BuildingId = b.Id, DisplayName = b.Name,
                RoadNodeX = roadNetwork != null ? roadNetwork.NearestNode(new Vector3(p.x, 0f, p.z), 14f).x : 0,
                RoadNodeZ = roadNetwork != null ? roadNetwork.NearestNode(new Vector3(p.x, 0f, p.z), 14f).y : 0,
                ProfessionId = "retail", StoreTypeId = storeTypeId ?? "grocery", ProductDisplayName = productDisplayName ?? "Продукты",
                StaffCapacity = b.StaffCapacity, IsOpen = true, TargetInventoryUnits = targetStock,
                InventoryUnits = targetStock, InventoryInitialized = true, ProductUnitCost = wholesaleCost, ProductUnitPrice = retailPrice,
                Inventory = StoreCatalog.CreateStarterInventory(storeTypeId ?? "grocery"),
                OperatingBalance = 200000 // Prototype seed capital; balance tuning is a later economy pass.
            };
            workScheduleSystem.RegisterWorkplace(workplaceRecord);
            economySystem.RegisterWorkplace(workplaceRecord);
        }
        if(residential && restoreRecord == null){int moveIn=Mathf.Min(capacity,UnityEngine.Random.Range(Mathf.Max(1,capacity/3),capacity+1));for(int i=0;i<moveIn;i++)AddResident(b);b.Residents=moveIn;GenerateInitialHouseholdFamily(b);DistributeResidentsAcrossHighRiseFloors(b);status=name+": подключён к уличной сети, заселено "+moveIn+" из "+capacity+" мест.";}
        else if (restoreRecord != null) status = name + " восстановлен из сохранения.";
        else status=warehouseBuilding ? "Складской комплекс построен и подключён к складской системе." : workplace ? name+" построен. Ассортимент: "+productDisplayName+"." : "Объект построен.";
        if (b.IsHighRise)
        {
            EnsureHighRiseWorkplace(b);
            AssignHighRiseResidents(b);
        }
        GameEventBus.Publish(new GameEvent(GameEventType.BuildingPlaced,0,"construction",root.GetInstanceID().ToString(),name));
    }
    private void CreateHighRiseInteriorGeometry(PlacedBuilding building)
    {
        if (building == null || building.Root == null || building.HighRiseFloors == null) return;
        Transform old = building.Root.transform.Find("Generated Interior Cutaway");
        if (old != null) Destroy(old.gameObject);
        GameObject interior = new GameObject("Generated Interior Cutaway");
        interior.transform.SetParent(building.Root.transform, false);
        building.InteriorRoot = interior;
        float w = Mathf.Max(8f, building.Footprint.x - 1.2f);
        float d = Mathf.Max(8f, building.Footprint.z - 1.2f);
        const float floorHeight = 2.7f;
        for (int i = 0; i < building.HighRiseFloors.Count; i++)
        {
            HighRiseFloorRecord floor = building.HighRiseFloors[i];
            if (floor == null) continue;
            float y = (floor.FloorNumber - 1) * floorHeight + .18f;
            Material floorMat = floor.IsEngineering ? materials["metroConcrete"] : (floor.Function != null && floor.Function.IndexOf("жил", StringComparison.OrdinalIgnoreCase) >= 0 ? materials["cream"] : materials["white"]);
            Cube("Interior floor " + floor.FloorNumber, new Vector3(0f,y,0f), new Vector3(w,.16f,d), floorMat, interior.transform);
            // A readable cutaway: back and side partitions remain, the street-facing side is open.
            Cube("Interior rear wall " + floor.FloorNumber, new Vector3(0f,y+1.25f,d*.46f), new Vector3(w,2.5f,.16f), materials["cream"], interior.transform);
            Cube("Interior left partition " + floor.FloorNumber, new Vector3(-w*.28f,y+1.2f,0f), new Vector3(.12f,2.4f,d*.82f), materials["white"], interior.transform);
            Cube("Interior right partition " + floor.FloorNumber, new Vector3(w*.28f,y+1.2f,0f), new Vector3(.12f,2.4f,d*.82f), materials["white"], interior.transform);
            // Central elevator core and two simple room fixtures make each floor visibly distinct.
            Cube("Elevator core " + floor.FloorNumber, new Vector3(0f,y+1.05f,d*.18f), new Vector3(1.35f,2.1f,1.35f), materials["streetMetal"], interior.transform);
            if (floor.IsEngineering)
            {
                Cube("Engineering equipment " + floor.FloorNumber, new Vector3(-w*.16f,y+.62f,-d*.18f), new Vector3(1.8f,1.1f,1.2f), materials["metroBlue"], interior.transform);
                Cube("Engineering cabinet " + floor.FloorNumber, new Vector3(w*.17f,y+.55f,-d*.18f), new Vector3(1.1f,.9f,.9f), materials["roof"], interior.transform);
            }
            else if (floor.Function != null && floor.Function.IndexOf("жил", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                Cube("Apartment furniture " + floor.FloorNumber, new Vector3(-w*.15f,y+.42f,-d*.18f), new Vector3(2.1f,.65f,1.1f), materials["wood"], interior.transform);
                Cube("Apartment table " + floor.FloorNumber, new Vector3(w*.17f,y+.38f,-d*.18f), new Vector3(1.1f,.7f,.9f), materials["shop"], interior.transform);
            }
            else
            {
                Cube("Office desks " + floor.FloorNumber, new Vector3(-w*.15f,y+.42f,-d*.18f), new Vector3(2.2f,.65f,1.0f), materials["wood"], interior.transform);
                Cube("Office desks right " + floor.FloorNumber, new Vector3(w*.16f,y+.42f,-d*.18f), new Vector3(2.2f,.65f,1.0f), materials["wood"], interior.transform);
            }
        }
        HighRiseElevatorController oldController;
        if (highRiseElevators.TryGetValue(building.Id, out oldController)) highRiseElevators.Remove(building.Id);
        highRiseElevators[building.Id] = new HighRiseElevatorController(interior.transform, building.ElevatorCars,
            building.HighRiseFloors.Count, floorHeight, 0f, d * .18f, materials["metroBlue"],
            delegate(int trips) { building.ElevatorTripsToday += trips; });
        building.InteriorRenderers = interior.GetComponentsInChildren<Renderer>(true);
        // Lightweight visual staff/resident proxies are created only for floors with activity.
        for (int i = 0; i < building.HighRiseFloors.Count; i++)
        {
            HighRiseFloorRecord floor = building.HighRiseFloors[i];
            if (floor == null || floor.IsEngineering || floor.Function == "Главный холл") continue;
            int visibleAgents = Mathf.Clamp((floor.Occupants + floor.Tenants + floor.Visitors) / 8, 0, 3);
            for (int a = 0; a < visibleAgents; a++)
            {
                GameObject agent = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                agent.name = "Interior activity · floor " + floor.FloorNumber;
                agent.transform.SetParent(interior.transform, false);
                agent.transform.localScale = new Vector3(.28f, .72f, .28f);
                agent.transform.localPosition = new Vector3(-w * .2f + a * .65f, (floor.FloorNumber - 1) * floorHeight + .9f, -d * .22f);
                Renderer agentRenderer = agent.GetComponent<Renderer>();
                agentRenderer.material = floor.Function.IndexOf("жил", StringComparison.OrdinalIgnoreCase) >= 0 ? materials["metroBlue"] : materials["shop"];
                Collider agentCollider = agent.GetComponent<Collider>(); if (agentCollider != null) Destroy(agentCollider);
                highRiseInteriorAgents.Add(agent);
            }
        }
        foreach (Renderer renderer in building.InteriorRenderers) renderer.enabled = false;
        foreach (Collider collider in interior.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        Renderer[] allRenderers = building.Root.GetComponentsInChildren<Renderer>(true);
        List<Renderer> exterior = new List<Renderer>();
        foreach (Renderer renderer in allRenderers)
            if (renderer != null && !renderer.transform.IsChildOf(interior.transform)) exterior.Add(renderer);
        building.ExteriorRenderers = exterior.ToArray();
    }

    private void SetHighRiseInteriorMode(bool enabled)
    {
        if (selected == null || !selected.IsHighRise || selected.InteriorRoot == null) return;
        if (enabled == highRiseInteriorMode) return;
        if (enabled)
        {
            savedCameraTarget = target; savedCameraDistance = distance; savedCameraPitch = pitch;
            foreach (Renderer renderer in selected.ExteriorRenderers) if (renderer != null && (selected.InteriorRoot == null || !renderer.transform.IsChildOf(selected.InteriorRoot.transform))) renderer.enabled = false;
            foreach (Renderer renderer in selected.InteriorRenderers) if (renderer != null) renderer.enabled = true;
            target = selected.Root.transform.position + Vector3.up * (selected.HighRiseFloors.Count * 2.7f * .48f);
            distance = Mathf.Max(17f, selected.Footprint.x * 1.7f); pitch = 17f; yaw = 0f;
            status = "Режим интерьера: открытый разрез этажей. Нажмите «Вернуться в город», чтобы выйти.";
        }
        else
        {
            foreach (Renderer renderer in selected.InteriorRenderers) if (renderer != null) renderer.enabled = false;
            foreach (Renderer renderer in selected.ExteriorRenderers) if (renderer != null) renderer.enabled = true;
            target = savedCameraTarget; distance = savedCameraDistance; pitch = savedCameraPitch;
            status = "Возврат к виду города.";
        }
        highRiseInteriorMode = enabled; UpdateCamera();
    }

    private void DrawHighRiseInteriorPanel()
    {
        if (selected == null || !selected.IsHighRise || selected.HighRiseFloors == null) return;
        float x = 350f, y = 12f, width = 285f;
        GUI.Box(new Rect(x, y, width, showHighRiseFloorPlan ? 350f : 158f), "Высотное здание");
        GUI.Label(new Rect(x + 12f, y + 28f, width - 24f, 22f), selected.Name + " · уровень " + selected.Level + "/15");
        HighRiseElevatorController elevatorStatus;
        int waitingElevatorRequests = highRiseElevators.TryGetValue(selected.Id, out elevatorStatus) && elevatorStatus != null ? elevatorStatus.WaitingRequests : 0;
        int elevatorPassengers = elevatorStatus != null ? elevatorStatus.PassengersInCars : 0;
        GUI.Label(new Rect(x + 12f, y + 50f, width - 24f, 22f), "Этажей: " + selected.HighRiseFloors.Count + " · лифтов: " + selected.ElevatorCars + " · поездок: " + selected.ElevatorTripsToday + " · очередь: " + waitingElevatorRequests + " · пассажиры: " + elevatorPassengers);
        GUI.Label(new Rect(x + 12f, y + 132f, width - 24f, 20f), "Аренда/день: " + selected.DailyHighRiseRent.ToString("N0") + " ₽ · зарплата: " + selected.DailyHighRisePayroll.ToString("N0") + " ₽");
        if (GUI.Button(new Rect(x + 12f, y + 76f, width - 24f, 25f), highRiseInteriorMode ? "Вернуться в город" : "Войти внутрь · 3D-разрез")) SetHighRiseInteriorMode(!highRiseInteriorMode);
        if (GUI.Button(new Rect(x + 12f, y + 105f, width - 24f, 25f), showHighRiseFloorPlan ? "Свернуть план этажей" : "Открыть план этажей")) showHighRiseFloorPlan = !showHighRiseFloorPlan;
        if (!showHighRiseFloorPlan) return;
        highRiseFloorScroll = GUI.BeginScrollView(new Rect(x + 8f, y + 158f, width - 16f, 182f), highRiseFloorScroll, new Rect(0f, 0f, width - 36f, selected.HighRiseFloors.Count * 28f));
        for (int i = 0; i < selected.HighRiseFloors.Count; i++)
        {
            HighRiseFloorRecord floor = selected.HighRiseFloors[i];
            if (floor == null) continue;
            string details = floor.IsEngineering ? " · инженерия" : floor.Capacity > 0 ? " · мест: " + floor.Capacity : string.Empty;
            if (floor.Occupants > 0) details += " · жильцы: " + floor.Occupants;
            if (floor.Tenants > 0) details += " · арендаторы: " + floor.Tenants;
            if (floor.Visitors > 0) details += " · посетители: " + floor.Visitors;
            GUI.Label(new Rect(4f, i * 28f, width - 48f, 25f), "Этаж " + floor.FloorNumber + ": " + floor.Function + details);
        }
        GUI.EndScrollView();
    }

    private List<HighRiseFloorRecord> CreateHighRiseFloorPlan(int type, int residentialCapacity)
    {
        int totalFloors = type == 0 ? 38 : type == 1 ? 44 : 50;
        List<HighRiseFloorRecord> result = new List<HighRiseFloorRecord>(totalFloors);
        for (int floor = 1; floor <= totalFloors; floor++)
        {
            HighRiseFloorRecord record = new HighRiseFloorRecord { FloorNumber = floor, Capacity = 0, Occupants = 0, Tenants = 0, Visitors = 0 };
            if (floor == 1) record.Function = "Главный холл";
            else if (floor == totalFloors || floor % 12 == 0) { record.Function = "Инженерный этаж"; record.IsEngineering = true; }
            else if (type == 0) { record.Function = "Офисы и арендаторы"; record.Capacity = 12; record.Tenants = 2; }
            else if (type == 1) { record.Function = "Жилые квартиры"; record.Capacity = Mathf.Max(1, Mathf.CeilToInt(residentialCapacity / (float)Mathf.Max(1, totalFloors - 2))); }
            else if (floor <= 5) { record.Function = floor == 2 ? "Торговая галерея" : "Коммерческие помещения"; record.Capacity = 10; record.Tenants = 2; }
            else { record.Function = "Жилые квартиры"; record.Capacity = Mathf.Max(1, Mathf.CeilToInt(residentialCapacity / (float)Mathf.Max(1, totalFloors - 7))); }
            result.Add(record);
        }
        return result;
    }

    private void DistributeResidentsAcrossHighRiseFloors(PlacedBuilding building)
    {
        if (building == null || building.HighRiseFloors == null || building.HighRiseFloors.Count == 0) return;
        int remaining = building.Residents;
        foreach (HighRiseFloorRecord floor in building.HighRiseFloors)
        {
            if (floor == null || floor.Function != "Жилые квартиры") continue;
            int assigned = Mathf.Min(remaining, Mathf.Max(0, floor.Capacity));
            floor.Occupants = assigned;
            remaining -= assigned;
        }
    }

    private void AdvanceHighRiseOperations(long tick)
    {
        if (tick <= 0 || tick % 5 != 0) return;
        int minute = gameClock != null ? gameClock.MinuteOfDay : 0;
        bool peak = (minute >= 420 && minute <= 600) || (minute >= 960 && minute <= 1140);
        foreach (PlacedBuilding building in buildings)
        {
            if (building == null || !building.IsHighRise) continue;
            if (building.HighRiseFloors == null) continue;
            foreach (HighRiseFloorRecord floor in building.HighRiseFloors)
            {
                if (floor == null || floor.IsEngineering || floor.Function == "Главный холл") continue;
                if (floor.Function.IndexOf("Коммерческие", StringComparison.OrdinalIgnoreCase) >= 0 || floor.Function.IndexOf("Торговая", StringComparison.OrdinalIgnoreCase) >= 0 || floor.Function.IndexOf("Офисы", StringComparison.OrdinalIgnoreCase) >= 0)
                    floor.Visitors = peak ? Mathf.Min(floor.Capacity, 3 + (int)(tick % 5)) : Mathf.Min(floor.Capacity, 1 + (int)(tick % 3));
            }
        }
    }

    private void AdvanceHighRiseElevators()
    {
        if (gameClock == null) return;
        foreach (PlacedBuilding building in buildings)
        {
            if (building == null || !building.IsHighRise) continue;
            HighRiseElevatorController controller;
            if (highRiseElevators.TryGetValue(building.Id, out controller) && controller != null)
                controller.AdvanceOneMinute(building.HighRiseFloors != null ? building.HighRiseFloors.Count : 1);
        }
    }

    private void QueueResidentElevatorTrips(ResidentAgent resident, CitizenDailyPhase previous, CitizenDailyPhase next)
    {
        if (resident == null) return;
        // Home tower: residents travel between their apartment floor and the lobby at commute transitions.
        PlacedBuilding workplace = FindBuildingById(resident.WorkplaceBuildingId);
        if (resident.Home != null && resident.Home.IsHighRise)
        {
            int homeFloor = ResolveResidentHomeFloor(resident);
            if (workplace == resident.Home)
            {
                int internalWorkFloor = FindFirstFunctionalFloor(resident.Home, "Офисы");
                if (internalWorkFloor <= 1) internalWorkFloor = FindFirstFunctionalFloor(resident.Home, "Коммерческие");
                if (internalWorkFloor <= 1) internalWorkFloor = 2;
                if (next == CitizenDailyPhase.Work) QueueHighRiseElevator(resident.Home, homeFloor, internalWorkFloor);
                else if (next == CitizenDailyPhase.Evening) QueueHighRiseElevator(resident.Home, internalWorkFloor, homeFloor);
            }
            else if (next == CitizenDailyPhase.Work) QueueHighRiseElevator(resident.Home, homeFloor, 1);
            else if (next == CitizenDailyPhase.Evening) QueueHighRiseElevator(resident.Home, 1, homeFloor);
        }
        // Workplace tower: employees arrive at the office floor in the morning and return to the lobby in the evening.
        if (workplace != null && workplace.IsHighRise && resident.Home != workplace)
        {
            int workFloor = FindFirstFunctionalFloor(workplace, "Офисы");
            if (workFloor <= 1) workFloor = FindFirstFunctionalFloor(workplace, "Коммерческие");
            if (workFloor <= 1) workFloor = 2;
            if (next == CitizenDailyPhase.Work) QueueHighRiseElevator(workplace, 1, workFloor);
            else if (next == CitizenDailyPhase.Evening) QueueHighRiseElevator(workplace, workFloor, 1);
        }
    }

    private int ResolveResidentHomeFloor(ResidentAgent resident)
    {
        if (resident == null || resident.Home == null || resident.Home.HighRiseFloors == null) return 2;
        int remaining = 0;
        foreach (ResidentAgent other in residents)
        {
            if (other == null || other.Home != resident.Home) continue;
            if (other == resident) break;
            remaining++;
        }
        foreach (HighRiseFloorRecord floor in resident.Home.HighRiseFloors)
        {
            if (floor == null || floor.Function != "Жилые квартиры") continue;
            if (remaining < Mathf.Max(1, floor.Capacity)) return floor.FloorNumber;
            remaining -= Mathf.Max(1, floor.Capacity);
        }
        return 2;
    }

    private int FindFirstFunctionalFloor(PlacedBuilding building, string function)
    {
        if (building == null || building.HighRiseFloors == null) return 2;
        foreach (HighRiseFloorRecord floor in building.HighRiseFloors)
            if (floor != null && floor.Function != null && floor.Function.IndexOf(function, StringComparison.OrdinalIgnoreCase) >= 0) return floor.FloorNumber;
        return 2;
    }

    private void QueueHighRiseElevator(PlacedBuilding building, int from, int to)
    {
        if (building == null) return;
        HighRiseElevatorController controller;
        if (highRiseElevators.TryGetValue(building.Id, out controller) && controller != null) controller.Enqueue(from, to, 1);
    }

    private PlacedBuilding FindBuildingById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (PlacedBuilding building in buildings) if (building != null && building.Id == id) return building;
        return null;
    }

    private void AnimateHighRiseInteriorAgents()
    {
        if (!highRiseInteriorMode || highRiseInteriorAgents.Count == 0) return;
        highRiseAgentClock += Time.deltaTime;
        for (int i = highRiseInteriorAgents.Count - 1; i >= 0; i--)
        {
            GameObject agent = highRiseInteriorAgents[i];
            if (agent == null) { highRiseInteriorAgents.RemoveAt(i); continue; }
            Vector3 p = agent.transform.localPosition;
            p.x = Mathf.Sin(highRiseAgentClock * .65f + i * 1.7f) * 2.2f;
            p.z = Mathf.Cos(highRiseAgentClock * .48f + i * 1.3f) * 1.6f;
            agent.transform.localPosition = p;
        }
    }

    private void EnsureHighRiseWorkplace(PlacedBuilding building)
    {
        if (building == null || !building.IsHighRise || workScheduleSystem == null || economySystem == null) return;
        if (FindWorkplaceRecord(building.Id) != null) return;
        int staffCapacity = 0;
        long dailyRent = 0;
        foreach (HighRiseFloorRecord floor in building.HighRiseFloors)
        {
            if (floor == null || floor.IsEngineering || floor.Function == "Главный холл") continue;
            bool office = floor.Function.IndexOf("Офисы", StringComparison.OrdinalIgnoreCase) >= 0;
            bool commercial = floor.Function.IndexOf("Коммерческие", StringComparison.OrdinalIgnoreCase) >= 0 ||
                floor.Function.IndexOf("Торговая", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!office && !commercial) continue;
            int tenants = Math.Max(0, floor.Tenants);
            staffCapacity = checked(staffCapacity + tenants * (office ? 4 : 2));
            dailyRent = checked(dailyRent + tenants * (office ? 2200L : 1600L));
        }
        if (staffCapacity <= 0) return;
        Vector2Int node = roadNetwork != null
            ? roadNetwork.NearestNode(building.Root != null ? building.Root.transform.position : Vector3.zero, 14f)
            : Vector2Int.zero;
        WorkplaceRecord employer = new WorkplaceRecord
        {
            Id = "workplace-" + building.Id,
            BuildingId = building.Id,
            DisplayName = building.Name,
            RoadNodeX = node.x,
            RoadNodeZ = node.y,
            ProfessionId = "office",
            StoreTypeId = "office",
            ProductDisplayName = "Услуги",
            StaffCapacity = staffCapacity,
            IsOpen = true,
            IsRetailBusiness = false,
            DailyRentIncome = dailyRent,
            OperatingBalance = 0,
            Inventory = new List<StoreInventoryRecord>(),
            InventoryInitialized = true
        };
        if (workScheduleSystem.RegisterWorkplace(employer)) economySystem.RegisterWorkplace(employer);
    }


    private void AssignHighRiseResidents(PlacedBuilding building)
    {
        if (building == null || !building.IsHighRise || workScheduleSystem == null) return;
        WorkplaceRecord employer = FindWorkplaceRecord(building.Id);
        if (employer == null || !employer.IsOpen) return;
        foreach (ResidentAgent resident in residents)
        {
            if (resident == null || resident.Record == null || resident.Home != building || resident.Record.IsEmployed) continue;
            if (employer.StaffCount >= employer.StaffCapacity) break;
            const int monthlySalary = 27000;
            if (!workScheduleSystem.AssignCitizen(resident.Id, employer.Id, "office", monthlySalary)) continue;
            resident.WorkplaceBuildingId = employer.BuildingId;
            resident.ProfessionId = "office";
            resident.MonthlySalary = monthlySalary;
        }
    }

    private void CreateHighRiseBuildingDetails(Transform parent, Vector3 footprint, float height, int style)
    {
        float w = footprint.x, d = footprint.z;
        Material glass = materials["glass"], trim = materials["roof"], accent = materials["shop"];
        // Podium and curtain-wall articulation. The main volume is already created by BuildAt.
        Cube("High-rise podium", new Vector3(0f, 1.8f, 0f), new Vector3(w + 2.2f, 3.6f, d + 2.2f), materials["shop"], parent);
        int floorBands = Mathf.Max(12, Mathf.RoundToInt(height / 2.8f));
        for (int i=1; i<floorBands; i++)
        {
            float y = 3.6f + (height - 3.6f) * i / floorBands;
            Cube("High-rise floor belt " + i, new Vector3(0f, y, 0f), new Vector3(w + .18f, .10f, d + .18f), trim, parent);
        }
        for (int side=-1; side<=1; side+=2)
        {
            Cube("High-rise facade fin X " + side, new Vector3(side*(w*.5f-.32f), height*.52f, -d*.5f-.10f), new Vector3(.24f, height*.96f, .16f), accent, parent);
            Cube("High-rise facade fin Z " + side, new Vector3(-w*.5f-.10f, height*.52f, side*(d*.5f-.32f)), new Vector3(.16f, height*.96f, .24f), accent, parent);
        }
        if (style == 0)
        {
            Cube("Office tower crown", new Vector3(0f,height+.75f,0f), new Vector3(w*.58f,1.5f,d*.58f), trim, parent);
        }
        else if (style == 1)
        {
            Cube("Residential tower upper setback", new Vector3(0f,height-.8f,0f), new Vector3(w*.78f,1.6f,d*.78f), glass, parent);
            Cube("Residential rooftop garden base", new Vector3(0f,height+.12f,0f), new Vector3(w*.58f,.24f,d*.58f), materials["leaf"], parent);
        }
        else
        {
            Cube("Mixed-use tower upper setback", new Vector3(0f,height-.9f,0f), new Vector3(w*.72f,1.8f,d*.72f), glass, parent);
            Cube("Mixed-use crown", new Vector3(0f,height+.35f,0f), new Vector3(w*.42f,.7f,d*.42f), accent, parent);
        }
        // Repeated facade mullions make the tower legible at city-camera distance.
        int mullions = Mathf.Max(4, Mathf.RoundToInt(w/2.2f));
        for(int i=0;i<=mullions;i++)
        {
            float x = -w*.5f + w*i/mullions;
            Cube("Curtain wall mullion front " + i, new Vector3(x,height*.5f,-d*.5f-.12f), new Vector3(.07f,height,.08f), trim, parent);
        }
    }

    // New occupancy receives an explicit household archetype. Co-residence alone is never interpreted as kinship.
    private void GenerateInitialHouseholdFamily(PlacedBuilding home)
    {
        if (home == null || citizenRelationshipSystem == null) return;
        List<ResidentAgent> members = residents.FindAll(r => r != null && r.Home == home && r.Record != null);
        if (members.Count < 2) return;
        members.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        int stableSeed = 0;
        string key = home.Id ?? string.Empty;
        for (int i = 0; i < key.Length; i++) stableSeed = (stableSeed * 31 + key[i]) & 0x7fffffff;
        int archetype = stableSeed % 10;
        int day = gameClock != null ? gameClock.Day : 1;

        // 0-3: couple with children; 4-6: single-parent household; 7-9: unrelated adult housemates.
        if (archetype <= 3 && members.Count >= 2)
        {
            members[0].Record.Age = 30 + stableSeed % 9;
            members[1].Record.Age = 28 + (stableSeed / 7) % 9;
            citizenRelationshipSystem.AddSpouses(members[0].Id, members[1].Id, day, out _);
            for (int i = 2; i < members.Count; i++)
            {
                members[i].Record.Age = 5 + (stableSeed + i * 7) % 8;
                citizenRelationshipSystem.AddParentChild(members[0].Id, members[i].Id, day, out _);
                citizenRelationshipSystem.AddParentChild(members[1].Id, members[i].Id, day, out _);
            }
        }
        else if (archetype <= 6)
        {
            members[0].Record.Age = 32 + stableSeed % 8;
            for (int i = 1; i < members.Count; i++)
            {
                members[i].Record.Age = 5 + (stableSeed + i * 5) % 8;
                citizenRelationshipSystem.AddParentChild(members[0].Id, members[i].Id, day, out _);
            }
        }
        else
        {
            for (int i = 0; i < members.Count; i++) members[i].Record.Age = 20 + (stableSeed + i * 11) % 41;
        }
    }

    private void AddResident(PlacedBuilding home)
    {
        string[] first={"Анна","Иван","Мария","Алексей","Елена","Михаил","София","Даниил","Ольга","Никита"};string[] last={"Соколов","Иванова","Петров","Смирнова","Кузнецов","Орлова","Волков","Морозова"};
        GameObject body=Capsule("Resident",home.Entrance + new Vector3(UnityEngine.Random.Range(-.6f,.6f),0f,0f),materials["resident"]);body.transform.localScale=new Vector3(.48f,1.25f,.48f);
        string residentName = first[UnityEngine.Random.Range(0,first.Length)]+" "+last[UnityEngine.Random.Range(0,last.Length)];
        CitizenDailyPhase initialPhase = CitizenDailySchedule.GetPhase(gameClock != null ? gameClock.MinuteOfDay : 480);
        CitizenRecord record = new CitizenRecord
        {
            Id = StableId.NewId().ToString(),
            FirstName = residentName,
            LastName = "",
            Age = 25,
            AgeLastAdvancedDay = gameClock != null ? gameClock.Day : 1,
            WalletBalance = 5000, // Prototype starting cash for testing household purchases.
            HomeBuildingId = home.Id,
            CurrentSectorX = 0,
            CurrentSectorZ = 0,
            ScheduleState = (int)initialPhase,
            Health = 1f,
            Happiness = 0.5f,
            EventHistory = new List<string>()
        };
        ResidentAgent agent = new ResidentAgent
        {
            Body = body, Home = home, Id = record.Id, Name = residentName,
            Destination = home.Entrance, Wait = UnityEngine.Random.Range(1,5),
            ScheduleState = initialPhase, MonthlySalary = 0, Record = record
        };
        residents.Add(agent);
        if (citizenRelationshipSystem != null) citizenRelationshipSystem.RegisterCitizen(record);
        if (workScheduleSystem != null) workScheduleSystem.RegisterCitizen(record);
        if (economySystem != null) economySystem.RegisterCitizen(record);
        if (home != null && home.IsHighRise)
        {
            WorkplaceRecord employer = FindWorkplaceRecord(home.Id);
            if (employer != null && workScheduleSystem != null && employer.StaffCount < employer.StaffCapacity &&
                workScheduleSystem.AssignCitizen(record.Id, employer.Id, "office", 27000))
            {
                agent.WorkplaceBuildingId = employer.BuildingId;
                agent.ProfessionId = "office";
                agent.MonthlySalary = 27000;
            }
        }
    }
    private GameObject Capsule(string name,Vector3 pos,Material mat){GameObject g=GameObject.CreatePrimitive(PrimitiveType.Capsule);g.name=name;g.transform.position=pos;g.GetComponent<Renderer>().sharedMaterial=mat;return g;}
    private void Demolish(PlacedBuilding b){money+=Mathf.RoundToInt(b.Cost*.25f);buildings.Remove(b);if(b.Residential){for(int i=residents.Count-1;i>=0;i--)if(residents[i].Home==b){if(residents[i].Body)Destroy(residents[i].Body);residents.RemoveAt(i);}}Destroy(b.Root);selected=null;status="Здание снесено. Возвращено 25% стоимости.";}

    private WorkplaceRecord FindWorkplaceRecord(string buildingId)
    {
        if (workScheduleSystem == null) return null;
        foreach (WorkplaceRecord workplace in workScheduleSystem.Workplaces)
            if (workplace != null && workplace.BuildingId == buildingId) return workplace;
        return null;
    }

    private void RestoreBuildings()
    {
        CitySaveData saved; string error;
        if (!CitySaveService.Load(out saved, out error) || saved == null || saved.Sectors == null) return;
        foreach (SectorRecord sector in saved.Sectors)
        {
            if (sector == null || sector.Buildings == null) continue;
            foreach (BuildingRecord record in sector.Buildings)
            {
                if (record == null) continue;
                string n = record.DefinitionId ?? "";
                if (n == "Коттедж") tool = Tool.Cottage;
                else if (n == "Морг") tool = Tool.Morgue;
                else if (n == "Кладбище") tool = Tool.Cemetery;
                else if (n == "Офисный небоскрёб") { tool = Tool.HighRise; selectedHighRiseType = 0; }
                else if (n == "Жилой небоскрёб") { tool = Tool.HighRise; selectedHighRiseType = 1; }
                else if (n == "Многофункциональный небоскрёб") { tool = Tool.HighRise; selectedHighRiseType = 2; }
                else if (n == "Многоквартирный дом") tool = Tool.Apartment;
                else if (n == "Продуктовый магазин") tool = Tool.Grocery;
                else if (n == "Магазин техники") tool = Tool.Electronics;
                else if (n == "Магазин одежды и обуви") tool = Tool.Clothing;
                else if (n == "Мебельный магазин") tool = Tool.Furniture;
                else if (n == "Магазин косметики") tool = Tool.Cosmetics;
                else if (n == "Книжный магазин") tool = Tool.Books;
                else if (n == "Складской комплекс") tool = Tool.Warehouse;
                else tool = Tool.House;
                float x = record.PositionX != 0f || record.GridX == 0 ? record.PositionX : record.GridX * 2f;
                float z = record.PositionZ != 0f || record.GridZ == 0 ? record.PositionZ : record.GridZ * 2f;
                BuildAt(new Vector3(x, 0f, z), record);
            }
        }
        RebuildPedestrianNetwork();
    }

    private void RestoreEconomy()
    {
        if (economySystem == null) return;
        CitySaveData saved; string error;
        if (!CitySaveService.Load(out saved, out error) || saved == null)
        {
            if (droneThreatSystem != null) droneThreatSystem.Configure(null, null, 173, DroneThreatSystem.RiskPreset.Standard, 0, null, 0);
            return;
        }
        List<CitizenRecord> citizenRecords = new List<CitizenRecord>();
        if (saved.Sectors != null)
            foreach (SectorRecord sector in saved.Sectors)
                if (sector != null && sector.Citizens != null) citizenRecords.AddRange(sector.Citizens);
        List<BuildingRecord> savedBuildings = new List<BuildingRecord>();
        if (saved.Sectors != null) foreach (SectorRecord sector in saved.Sectors) if (sector != null && sector.Buildings != null) savedBuildings.AddRange(sector.Buildings);
        if (productionChainSystem != null) productionChainSystem.Load(saved.ProductionSites);
        if (warehouseStorageSystem != null) warehouseStorageSystem.Load(saved.Warehouses);
        if (fleetManagementSystem != null) fleetManagementSystem.Load(saved.FleetVehicles, saved.FleetTransactions);
        if (warehouseStorageSystem != null) warehouseStorageSystem.SetFleetManagementSystem(fleetManagementSystem);
        if (supplyLogisticsSystem != null) supplyLogisticsSystem.Load(saved.Suppliers, saved.SupplyOrders, saved.CargoVehicles, saved.Day);
        if (droneThreatSystem != null) droneThreatSystem.Configure(citizenRecords, savedBuildings, saved.RandomSeed,
            (DroneThreatSystem.RiskPreset)Mathf.Clamp(saved.DroneRiskPreset, 0, 2), saved.DroneThreatLastProcessedDay,
            saved.DroneThreatHistory, saved.DroneAlertMinutesRemaining);
        if (householdSystem != null) householdSystem.Load(citizenRecords, saved.Day);
        if (citizenRelationshipSystem != null) citizenRelationshipSystem.Load(citizenRecords, saved.CitizenRelationships);
        if (familyEconomySystem != null) familyEconomySystem.Load(citizenRecords, saved.FamilyAccounts, saved.FamilyFinanceTransactions, saved.FamilyResponsibilities);
        if (lifeCycleSystem != null)
        {
            lifeCycleSystem.Load(saved.FuneralEvents);
            lifeCycleSystem.RebuildStatistics(citizenRecords);
        }
        if (householdSystem != null && familyEconomySystem != null) familyEconomySystem.ReconcileHouseholds(householdSystem.Households, saved.Day);
        economySystem.Load(citizenRecords, saved.Workplaces, saved.PayrollAccruedKeys,
            saved.PayrollPaidKeys, saved.BusinessDayKeys, saved.ShopperDayKeys, saved.TotalPayrollPaid,
            saved.TotalBusinessRevenue, saved.TotalBusinessCosts, saved.PayrollLastProcessedDay, saved.CityFinancialTransactions);
        if (workScheduleSystem != null) workScheduleSystem.Load(citizenRecords, saved.Workplaces);
        foreach (PlacedBuilding building in buildings)
            if (building != null && building.IsHighRise) EnsureHighRiseWorkplace(building);
        foreach (CitizenRecord citizen in citizenRecords)
        {
            if (citizen == null) continue;
            PlacedBuilding home = buildings.Find(b => b != null && b.Id == citizen.HomeBuildingId);
            if (home == null && !citizen.IsDeceased) continue;
            RestoreResident(home, citizen);
        }
        foreach (ResidentAgent resident in residents)
        {
            if (resident == null || resident.Record == null || !string.IsNullOrEmpty(resident.WorkplaceBuildingId) || resident.Home == null || !resident.Home.IsHighRise) continue;
            WorkplaceRecord employer = FindWorkplaceRecord(resident.Home.Id);
            if (employer != null && workScheduleSystem != null && employer.StaffCount < employer.StaffCapacity &&
                workScheduleSystem.AssignCitizen(resident.Id, employer.Id, "office", 27000))
            {
                resident.WorkplaceBuildingId = employer.BuildingId;
                resident.ProfessionId = "office";
                resident.MonthlySalary = 27000;
            }
        }
        foreach (PlacedBuilding building in buildings)
            if (building != null && building.IsHighRise) DistributeResidentsAcrossHighRiseFloors(building);
        if (mayorOfficeSystem != null) mayorOfficeSystem.Load(saved.MayorRequests, saved.MayorDailyReports, saved.MayorApprovalScore, saved.MayorLastGeneratedDay);
        if (maintenanceSystem != null) maintenanceSystem.Load(saved.MaintenanceProjects);
        if (saved.Day > 0) gameClock.SetTime(saved.Day, saved.MinuteOfDay, saved.SimulationTick);
        if (saved.CityBudget >= int.MinValue && saved.CityBudget <= int.MaxValue) money = (int)saved.CityBudget;
    }

    private void RestoreResident(PlacedBuilding home, CitizenRecord record)
    {
        if (record == null) return;
        if (record.IsDeceased)
        {
            residents.Add(new ResidentAgent { Home = null, Body = null, Id = record.Id,
                Name = ((record.FirstName ?? "") + " " + (record.LastName ?? "")).Trim(), Record = record });
            return;
        }
        if (home == null) return;
        GameObject body = Capsule("Resident | " + record.FirstName + " " + record.LastName,
            home.Entrance + new Vector3(UnityEngine.Random.Range(-.6f,.6f),0f,0f), materials["resident"]);
        body.transform.localScale = new Vector3(.48f, 1.25f, .48f);
        ResidentAgent agent = new ResidentAgent
        {
            Body = body, Home = home, Id = record.Id,
            Name = ((record.FirstName ?? "") + " " + (record.LastName ?? "")).Trim(),
            Destination = home.Entrance, Wait = 1f,
            ScheduleState = (CitizenDailyPhase)Mathf.Clamp(record.ScheduleState, 0, 3),
            WorkplaceBuildingId = record.WorkplaceBuildingId, ProfessionId = record.ProfessionId,
            MonthlySalary = record.MonthlySalary, Record = record
        };
        residents.Add(agent); home.Residents++;
    }

    private void SaveCity()
    {
        if (gameClock == null) EnsureGameClock();
        SectorRecord sector = new SectorRecord { X = 0, Z = 0, IsDiscovered = true };
        CitySaveData data = new CitySaveData
        {
            SchemaVersion = 21,
            RandomSeed = 173,
            CityId = "my-city-local",
            CityName = "My City",
            Day = gameClock.Day,
            MinuteOfDay = gameClock.MinuteOfDay,
            SimulationTick = gameClock.Tick,
            CityBudget = money,
            Sectors = new List<SectorRecord> { sector }
        };

        if (mayorOfficeSystem != null) { mayorOfficeSystem.Export(data.MayorRequests, data.MayorDailyReports); data.MayorApprovalScore = mayorOfficeSystem.ApprovalScore; data.MayorLastGeneratedDay = gameClock.Day; }
        if (maintenanceSystem != null) maintenanceSystem.Export(data.MaintenanceProjects);

        foreach (KeyValuePair<GameObject, Tuple<Vector2Int, Vector2Int>> roadEntry in playerRoadEdges)
        {
            if (roadEntry.Key == null) continue;
            Vector2Int a = roadEntry.Value.Item1;
            Vector2Int b = roadEntry.Value.Item2;
            data.PlayerRoadSegments.Add(new RoadSegmentRecord { StartX = a.x, StartZ = a.y, EndX = b.x, EndZ = b.y });
        }

        foreach (PlacedBuilding b in buildings)
        {
            if (b == null || b.Root == null) continue;
            sector.Buildings.Add(new BuildingRecord
            {
                Id = b.Id,
                DefinitionId = b.Name,
                GridX = Mathf.RoundToInt(b.Root.transform.position.x / 2f),
                GridZ = Mathf.RoundToInt(b.Root.transform.position.z / 2f),
                PositionX = b.Root.transform.position.x,
                PositionZ = b.Root.transform.position.z,
                ConstructionCost = b.Cost,
                RotationQuarterTurns = Mathf.RoundToInt(b.Root.transform.eulerAngles.y / 90f) % 4,
                Level = Mathf.Max(1, b.Level),
                Condition = Mathf.Clamp01(b.Condition),
                HighRiseFloors = b.IsHighRise ? b.HighRiseFloors : new List<HighRiseFloorRecord>(),
                ElevatorCars = b.ElevatorCars,
                ElevatorTripsToday = b.ElevatorTripsToday,
                HighRiseLastSettlementDay = b.LastHighRiseSettlementDay,
                HighRiseDailyRent = b.DailyHighRiseRent,
                HighRiseDailyPayroll = b.DailyHighRisePayroll,
                FacilityCapacity = b.FacilityCapacity
            });
            if (b.Workplace)
            {
                WorkplaceRecord savedWorkplace = FindWorkplaceRecord(b.Id);
                if (savedWorkplace != null)
                    data.Workplaces.Add(savedWorkplace);
                else
                    data.Workplaces.Add(new WorkplaceRecord
                    {
                        Id = "workplace-" + b.Id, BuildingId = b.Id, DisplayName = b.Name,
                        ProfessionId = "retail", StoreTypeId = "grocery", ProductDisplayName = "Продукты", StaffCapacity = b.StaffCapacity,
                        OperatingBalance = 0, DailyRevenuePerEmployee = 2500,
                        DailyFixedOperatingCost = 800, DailyOperatingCostPerEmployee = 500,
                        InventoryUnits = 100, TargetInventoryUnits = 100, ProductUnitCost = 200, ProductUnitPrice = 350, InventoryInitialized = true,
                        Inventory = StoreCatalog.CreateStarterInventory("grocery"), IsOpen = true
                    });
            }
        }

        foreach (ResidentAgent resident in residents)
        {
            if (resident == null || resident.Record == null) continue;
            if (!resident.Record.IsDeceased) resident.Record.HomeBuildingId = resident.Home != null ? resident.Home.Id : null;
            resident.Record.WorkplaceBuildingId = resident.WorkplaceBuildingId;
            resident.Record.ProfessionId = resident.ProfessionId;
            resident.Record.MonthlySalary = resident.MonthlySalary;
            resident.Record.IsEmployed = !string.IsNullOrEmpty(resident.WorkplaceBuildingId);
            resident.Record.ScheduleState = (int)resident.ScheduleState;
            sector.Citizens.Add(resident.Record);
        }

        if (householdSystem != null) { householdSystem.Rebuild(sector.Citizens, gameClock.Day); householdSystem.Export(data.Households); }
        if (citizenRelationshipSystem != null) citizenRelationshipSystem.Export(data.CitizenRelationships);
        if (lifeCycleSystem != null) lifeCycleSystem.Export(data.FuneralEvents);
        if (familyEconomySystem != null)
        {
            familyEconomySystem.RefreshCitizens(sector.Citizens);
            familyEconomySystem.ReconcileHouseholds(householdSystem != null ? householdSystem.Households : null, gameClock.Day);
            familyEconomySystem.Export(data.FamilyAccounts, data.FamilyFinanceTransactions, data.FamilyResponsibilities);
        }

        if (droneThreatSystem != null)
        {
            data.DroneRiskPreset = (int)droneThreatSystem.Risk;
            data.DroneAlertMinutesRemaining = droneThreatSystem.ActiveAlertMinutes;
            List<CitizenRecord> threatCitizens = new List<CitizenRecord>(sector.Citizens);
            List<BuildingRecord> threatBuildings = new List<BuildingRecord>(sector.Buildings);
            droneThreatSystem.UpdateCityEntities(threatCitizens, threatBuildings);
            data.DroneThreatLastProcessedDay = droneThreatSystem.LastProcessedDay;
            droneThreatSystem.Export(data.DroneThreatHistory);
        }

        if (productionChainSystem != null) productionChainSystem.Export(data.ProductionSites);
        if (warehouseStorageSystem != null) warehouseStorageSystem.Export(data.Warehouses);
        if (fleetManagementSystem != null) fleetManagementSystem.Export(data.FleetVehicles, data.FleetTransactions);
        if (supplyLogisticsSystem != null) supplyLogisticsSystem.Export(data.Suppliers, data.SupplyOrders, data.CargoVehicles);

        if (economySystem != null)
        {
            data.TotalPayrollPaid = economySystem.TotalPaid;
            data.PayrollLastProcessedDay = economySystem.LastProcessedDay;
            data.TotalBusinessRevenue = economySystem.TotalBusinessRevenue;
            data.TotalBusinessCosts = economySystem.TotalBusinessCosts;
            economySystem.Export(data.PayrollAccruedKeys, data.PayrollPaidKeys, data.BusinessDayKeys, data.ShopperDayKeys);
            economySystem.ExportFinancialTransactions(data.CityFinancialTransactions);
        }

        if (CitySaveService.Save(data, out string error))
            status = "Город сохранён на этом устройстве.";
        else
            status = "Ошибка сохранения: " + error;
    }

    private void OnGUI()
    {
        GUI.skin.box.fontSize=15;GUI.skin.button.fontSize=13;GUI.skin.label.fontSize=14;
        GUI.Box(new Rect(12,12,306,106),"");GUI.Label(new Rect(26,22,280,26),"MY CITY   |   Городской симулятор");
        if (droneThreatSystem != null) GUI.Label(new Rect(26, 98, 280, 20), droneThreatSystem.IsAlertActive ? "⚠ ОПАСНОСТЬ БПЛА · " + droneThreatSystem.ActiveAlertMinutes + " мин." : "Гражданская безопасность: контроль угроз");GUI.Label(new Rect(26,50,280,22),"Бюджет: "+money.ToString("N0")+" ₽");int minuteOfDay = gameClock != null ? gameClock.MinuteOfDay : 480; int dayNumber = gameClock != null ? gameClock.Day : 1;
        GUI.Label(new Rect(26,76,280,22),"День "+dayNumber+"   "+(minuteOfDay/60).ToString("00")+":"+(minuteOfDay%60).ToString("00")+"   Жителей: "+residents.FindAll(r => r != null && r.Record != null && !r.Record.IsDeceased).Count);
        GUI.Box(new Rect(12,126,326,650),"Строительство");string[] labels={"1  Жилой дом  ·  24 000 ₽","2  Коттедж  ·  38 000 ₽","3  Многоквартирный  ·  98 000 ₽","4  Продуктовый  ·  31 000 ₽","5  Техника  ·  52 000 ₽","6  Одежда и обувь  ·  39 000 ₽","7  Мебель  ·  47 000 ₽","8  Косметика и химия  ·  36 000 ₽","9  Книжный  ·  34 000 ₽","0  Дорога  ·  от 5 000 ₽/отрезок","X  Снос здания / дороги","B  Выбрать складской тип","H  Высотные здания","M  Морг · 95 000 ₽","C  Кладбище · 125 000 ₽"};Tool[] tools={Tool.House,Tool.Cottage,Tool.Apartment,Tool.Grocery,Tool.Electronics,Tool.Clothing,Tool.Furniture,Tool.Cosmetics,Tool.Books,Tool.Road,Tool.Demolish,Tool.Warehouse,Tool.HighRise,Tool.Morgue,Tool.Cemetery};for(int i=0;i<tools.Length;i++){if(GUI.Button(new Rect(25,158+i*30,300,26),labels[i]+(tool==tools[i]?"   ✓":""))){tool=tools[i];if(tool==Tool.Road)pendingRoadStart=null;}}
        if (tool == Tool.HighRise)
        {
            string[] highRiseTypes = { "Офисная башня · 180 000 ₽", "Жилая башня · 220 000 ₽", "Многофункциональная · 250 000 ₽" };
            for (int i=0;i<highRiseTypes.Length;i++) if (GUI.Button(new Rect(25, 620+i*24, 300, 22), highRiseTypes[i] + (selectedHighRiseType==i ? " ✓" : ""))) selectedHighRiseType=i;
        }
        if (tool == Tool.Warehouse)
        {
            string[] types = { "Распределительный · 85 000 ₽", "Оптовая база · 110 000 ₽", "Промышленный · 145 000 ₽", "Топливное хранилище · 190 000 ₽" };
            for (int i=0;i<types.Length;i++) if (GUI.Button(new Rect(25, 620+i*24, 300, 22), types[i] + (selectedWarehouseType==i ? " ✓" : ""))) selectedWarehouseType=i;
        }
        GUI.Box(new Rect(Screen.width-252,202,240,Mathf.Min(350, 90 + Mathf.Min(8,residents.Count)*29)), "Жители и работа");
        int visibleResidents = Mathf.Min(8, residents.Count);
        for (int i = 0; i < visibleResidents; i++)
        {
            ResidentAgent resident = residents[i];
            string marker = selectedResidentIndex == i ? " ✓" : "";
            string job = resident.Record != null && resident.Record.IsDeceased ? "умер" : (string.IsNullOrEmpty(resident.WorkplaceBuildingId) ? "без работы" : "работает");
            if (GUI.Button(new Rect(Screen.width-242, 230+i*29, 220, 25), resident.Name + " · " + job + marker))
                selectedResidentIndex = i;
        }
        WorkplaceRecord selectedEmployer = selected != null ? FindWorkplaceRecord(selected.Id) : null;
        bool canAssign = selectedResidentIndex >= 0 && selectedResidentIndex < residents.Count &&
            selectedEmployer != null && workScheduleSystem != null && residents[selectedResidentIndex].Record != null && !residents[selectedResidentIndex].Record.IsDeceased;
        GUI.enabled = canAssign;
        if (GUI.Button(new Rect(Screen.width-242, 230+visibleResidents*29, 220, 28), "Назначить к выбранному работодателю"))
        {
            ResidentAgent worker = residents[selectedResidentIndex];
            bool highRiseEmployer = !selectedEmployer.IsRetailBusiness;
            string profession = highRiseEmployer ? "office" : "retail";
            int salary = highRiseEmployer ? 15000 : 45000;
            bool assigned = workScheduleSystem.AssignCitizen(worker.Id, selectedEmployer.Id, profession, salary);
            if (assigned)
            {
                worker.WorkplaceBuildingId = selected.Id;
                worker.ProfessionId = profession;
                worker.MonthlySalary = salary;
                worker.Route = null;
                status = worker.Name + " назначен(а) в " + selected.Name + ". Зарплата: " + salary.ToString("N0") + " ₽/мес.";
            }
            else status = "Не удалось назначить: проверьте выбранного жителя, работодателя и свободное место.";
        }
        GUI.enabled = true;
        GUI.Label(new Rect(Screen.width-242, 238+visibleResidents*29, 220, 38),
            selectedEmployer != null ? "Выбран работодатель: " + selected.Name : "Сначала выберите магазин или офисную высотку.");
        if (GUI.Button(new Rect(Screen.width-238, 470, 214, 26), showLifeCyclePanel ? "Закрыть жизненный цикл" : "Смерть · морги · кладбища")) showLifeCyclePanel = !showLifeCyclePanel;
        DrawLifeCyclePanel();
        DrawFamilyPanel();
        DrawSelectedStorePanel();
        DrawHighRiseInteriorPanel();
        DrawProductionLogisticsPanel();
        DrawSelectedWarehousePanel();
        DrawWarehouseDispatcherPanel();
        DrawAirTrafficPanel();
        DrawPassengerNetworkPanel();
        DrawInternationalAirportPanel();
        DrawInternationalFleetPanel();
        DrawAirlineEconomyPanel();
        DrawAirportPassengerPanel();
        DrawCityFinancePanel();
        DrawCityFinanceLedgerPanel();
        DrawMayorOfficePanel();
        GUI.Box(new Rect(Screen.width-252,12,240,178),"Город и время");
        if (GUI.Button(new Rect(Screen.width-238, 438, 214, 26), showFamilyPanel ? "Закрыть семью" : "Семья · родственные связи")) showFamilyPanel = !showFamilyPanel;
        if (GUI.Button(new Rect(Screen.width-238, 184, 214, 26), showFinancePanel ? "Закрыть финансы" : "Финансы города")) showFinancePanel = !showFinancePanel;
        if (GUI.Button(new Rect(Screen.width-238, 246, 214, 26), showAirTrafficPanel ? "Закрыть табло рейсов" : "Авиация · табло рейсов")) showAirTrafficPanel = !showAirTrafficPanel;
        if (GUI.Button(new Rect(Screen.width-238, 278, 214, 26), showPassengerNetworkPanel ? "Закрыть пассажирскую сеть" : "Пассажирская сеть")) showPassengerNetworkPanel = !showPassengerNetworkPanel;
        if (GUI.Button(new Rect(Screen.width-238, 310, 214, 26), showInternationalAirportPanel ? "Закрыть международные рейсы" : "Международные направления")) { showInternationalAirportPanel = !showInternationalAirportPanel; if(showInternationalAirportPanel) showInternationalFleetPanel=false; }
        if (GUI.Button(new Rect(Screen.width-238, 342, 214, 26), showInternationalFleetPanel ? "Закрыть парк самолётов" : "Флот · расписание рейсов")) { showInternationalFleetPanel = !showInternationalFleetPanel; if(showInternationalFleetPanel) { showInternationalAirportPanel=false; showAirlineEconomyPanel=false; } }
        if (GUI.Button(new Rect(Screen.width-238, 374, 214, 26), showAirlineEconomyPanel ? "Закрыть финансы авиакомпании" : "Экономика авиакомпании")) { showAirlineEconomyPanel = !showAirlineEconomyPanel; if(showAirlineEconomyPanel) { showInternationalAirportPanel=false; showInternationalFleetPanel=false; } }
        if (GUI.Button(new Rect(Screen.width-238, 406, 214, 26), showAirportPassengerPanel ? "Закрыть обслуживание" : "Пассажиры · аэропорт")) { showAirportPassengerPanel = !showAirportPassengerPanel; if(showAirportPassengerPanel) { showInternationalAirportPanel=false; showInternationalFleetPanel=false; showAirlineEconomyPanel=false; } }
        if (GUI.Button(new Rect(Screen.width-238, 216, 214, 26), showMayorOffice ? "Закрыть приёмную" : "Приёмная мэра · " + (mayorOfficeSystem != null ? mayorOfficeSystem.PendingCount : 0))) showMayorOffice = !showMayorOffice;
        if (GUI.Button(new Rect(Screen.width-238, 158, 214, 25), showWarehouseDispatcher ? "Закрыть диспетчерскую" : "Грузовая диспетчерская")) showWarehouseDispatcher = !showWarehouseDispatcher;if(GUI.Button(new Rect(Screen.width-238,43,104,28),(gameClock!=null&&gameClock.IsPaused)?"Продолжить":"Пауза")){if(gameClock!=null)gameClock.SetPaused(!gameClock.IsPaused);}if(GUI.Button(new Rect(Screen.width-126,43,102,28),"Сохранить"))SaveCity();if(GUI.Button(new Rect(Screen.width-238,78,104,28),"Скорость 1×")){if(gameClock!=null)gameClock.SetSpeed(1f);}if(GUI.Button(new Rect(Screen.width-126,78,102,28),"Скорость 3×")){if(gameClock!=null)gameClock.SetSpeed(3f);}if(GUI.Button(new Rect(Screen.width-238,113,214,28),"Повернуть здание  R"))quarterTurns=(quarterTurns+1)%4;GUI.Label(new Rect(Screen.width-238,151,220,24),"Угол: "+(quarterTurns*90)+"°");
        GUI.Box(new Rect(12,Screen.height-76,Mathf.Min(Screen.width-24,850),62),"");GUI.Label(new Rect(24,Screen.height-67,Mathf.Min(Screen.width-50,820),48),status);
        if(showHelp){GUI.Box(new Rect(330,12,310,110),"Управление камерой");GUI.Label(new Rect(344,42,286,72),"WASD — перемещение\nПравая кнопка мыши — обзор\nКолесо — приближение / отдаление\nR — поворот здания, 5 — дорога (2 клика), X — снос");}
    }



    private void DrawLifeCyclePanel()
    {
        if (!showLifeCyclePanel) return;
        float width = Mathf.Min(560f, Screen.width - 32f);
        float height = Mathf.Min(500f, Screen.height - 32f);
        float x = (Screen.width - width) * .5f;
        float y = (Screen.height - height) * .5f;
        GUI.Box(new Rect(x, y, width, height), "ЖИЗНЕННЫЙ ЦИКЛ ЖИТЕЛЕЙ");
        if (GUI.Button(new Rect(x + width - 94f, y + 8f, 82f, 26f), "Закрыть")) { showLifeCyclePanel = false; return; }
        if (lifeCycleSystem == null) return;
        GUI.Label(new Rect(x + 18f, y + 42f, width - 36f, 24f), "Умерло: " + lifeCycleSystem.DeceasedCount + "   В морге: " + lifeCycleSystem.InMorgueCount + "   Захоронено: " + lifeCycleSystem.BuriedCount);
        GUI.Label(new Rect(x + 18f, y + 68f, width - 36f, 24f), "Ожидают морг: " + lifeCycleSystem.WaitingForMorgueCount + "   Ожидают кладбище: " + lifeCycleSystem.WaitingForBurialCount);
        float row = y + 104f;
        GUI.Label(new Rect(x + 18f, row, width - 36f, 24f), "УЧЁТ УМЕРШИХ ЖИТЕЛЕЙ"); row += 28f;
        int shown = 0;
        foreach (ResidentAgent resident in residents)
        {
            if (resident == null || resident.Record == null || !resident.Record.IsDeceased) continue;
            string line = resident.Name + " · день " + resident.Record.DeathDay + " · " + (resident.Record.FuneralStatus ?? "Статус не указан");
            if (!string.IsNullOrEmpty(resident.Record.DeathCause)) line += " · " + resident.Record.DeathCause;
            if (resident.Record.BurialDay > 0) line += " · захоронен: день " + resident.Record.BurialDay;
            GUI.Label(new Rect(x + 20f, row, width - 40f, 22f), line);
            row += 23f; shown++;
            if (shown >= 12) break;
        }
        if (shown == 0) GUI.Label(new Rect(x + 20f, row, width - 40f, 22f), "Пока нет умерших жителей.");
        GUI.Label(new Rect(x + 18f, y + height - 42f, width - 36f, 28f), "Вместимость: морг — 20 мест на здание; кладбище — 250 захоронений на объект.");
    }

    private void DrawFamilyPanel()
    {
        if (!showFamilyPanel) return;
        float width = Mathf.Min(620f, Screen.width - 32f);
        float height = Mathf.Min(560f, Screen.height - 32f);
        float x = (Screen.width - width) * 0.5f;
        float y = (Screen.height - height) * 0.5f;
        GUI.Box(new Rect(x, y, width, height), "СЕМЬЯ И РОДСТВЕННЫЕ СВЯЗИ");
        if (GUI.Button(new Rect(x + width - 94f, y + 8f, 82f, 26f), "Закрыть"))
        { showFamilyPanel = false; return; }
        if (residents.Count == 0)
        {
            GUI.Label(new Rect(x + 18f, y + 48f, width - 36f, 28f), "В городе пока нет жителей.");
            return;
        }
        if (selectedResidentIndex < 0 || selectedResidentIndex >= residents.Count) selectedResidentIndex = 0;
        GUI.Label(new Rect(x + 18f, y + 38f, width - 36f, 24f), "Выберите жителя:");
        float selectorY = y + 64f;
        float buttonWidth = Mathf.Min(180f, (width - 44f) / 3f);
        int visible = Mathf.Min(3, residents.Count);
        for (int i = 0; i < visible; i++)
        {
            int index = i;
            if (GUI.Button(new Rect(x + 18f + i * (buttonWidth + 6f), selectorY, buttonWidth, 28f),
                residents[index].Name + (selectedResidentIndex == index ? " ✓" : ""))) selectedResidentIndex = index;
        }
        if (residents.Count > visible)
        {
            int nextIndex = (selectedResidentIndex + 1) % residents.Count;
            if (GUI.Button(new Rect(x + width - 112f, selectorY, 94f, 28f), "Следующий ▶")) selectedResidentIndex = nextIndex;
        }

        ResidentAgent selectedResident = residents[selectedResidentIndex];
        CitizenRecord person = selectedResident.Record;
        float contentY = y + 104f;
        float contentHeight = height - 118f;
        familyPanelScroll = GUI.BeginScrollView(new Rect(x + 12f, contentY, width - 24f, contentHeight),
            familyPanelScroll, new Rect(0f, 0f, width - 52f, 1100f));
        float row = 0f;
        GUI.Label(new Rect(8f, row, width - 70f, 24f), selectedResident.Name + (person != null ? " · " + person.Age + " лет" : "")); row += 28f;
        if (person != null && person.IsDeceased)
        {
            GUI.Label(new Rect(8f, row, width - 70f, 22f), "Статус: умер · игровой день " + person.DeathDay); row += 22f;
            GUI.Label(new Rect(8f, row, width - 70f, 22f), "Причина: " + (person.DeathCause ?? "не указана")); row += 22f;
            GUI.Label(new Rect(8f, row, width - 70f, 22f), "Похороны: " + (person.FuneralStatus ?? "не оформлены")); row += 24f;
        }
        else if (person != null)
        {
            GUI.Label(new Rect(8f, row, width - 70f, 22f), "Статус: жив"); row += 24f;
        }
        GUI.Label(new Rect(8f, row, width - 70f, 22f), "СУПРУГ / СУПРУГА"); row += 23f;
        bool spouseFound = false;
        if (citizenRelationshipSystem != null)
        foreach (CitizenRelationshipRecord relation in citizenRelationshipSystem.Relationships)
        {
            if (relation == null || !relation.IsActive || relation.RelationshipType != "Spouse") continue;
            string otherId = relation.FirstCitizenId == selectedResident.Id ? relation.SecondCitizenId :
                (relation.SecondCitizenId == selectedResident.Id ? relation.FirstCitizenId : null);
            if (string.IsNullOrEmpty(otherId)) continue;
            ResidentAgent other = FindResidentAgent(otherId);
            GUI.Label(new Rect(18f, row, width - 90f, 22f), other != null ? other.Name + (other.Record != null && other.Record.IsDeceased ? " · умер" : "") : "Житель " + otherId);
            row += 23f; spouseFound = true;
        }
        if (!spouseFound) { GUI.Label(new Rect(18f, row, width - 90f, 22f), "Не указаны"); row += 23f; }

        GUI.Label(new Rect(8f, row, width - 70f, 22f), "РОДИТЕЛИ"); row += 23f;
        bool parentFound = false;
        if (citizenRelationshipSystem != null)
        foreach (CitizenRelationshipRecord relation in citizenRelationshipSystem.Relationships)
        {
            if (relation == null || !relation.IsActive || relation.RelationshipType != "ParentChild" || relation.ChildCitizenId != selectedResident.Id) continue;
            ResidentAgent parent = FindResidentAgent(relation.ParentCitizenId);
            GUI.Label(new Rect(18f, row, width - 90f, 22f), parent != null ? parent.Name + (parent.Record != null && parent.Record.IsDeceased ? " · умер" : "") : "Житель " + relation.ParentCitizenId);
            row += 23f; parentFound = true;
        }
        if (!parentFound) { GUI.Label(new Rect(18f, row, width - 90f, 22f), "Не указаны"); row += 23f; }

        GUI.Label(new Rect(8f, row, width - 70f, 22f), "ДЕТИ"); row += 23f;
        bool childFound = false;
        if (citizenRelationshipSystem != null)
        foreach (CitizenRelationshipRecord relation in citizenRelationshipSystem.Relationships)
        {
            if (relation == null || !relation.IsActive || relation.RelationshipType != "ParentChild" || relation.ParentCitizenId != selectedResident.Id) continue;
            ResidentAgent child = FindResidentAgent(relation.ChildCitizenId);
            GUI.Label(new Rect(18f, row, width - 90f, 22f), child != null ? child.Name + (child.Record != null && child.Record.IsDeceased ? " · умер" : "") : "Житель " + relation.ChildCitizenId);
            row += 23f; childFound = true;
        }
        if (!childFound) { GUI.Label(new Rect(18f, row, width - 90f, 22f), "Не указаны"); row += 23f; }

        GUI.Label(new Rect(8f, row + 4f, width - 70f, 22f), "СОВМЕСТНО ПРОЖИВАЮТ (НЕ ОБЯЗАТЕЛЬНО РОДСТВЕННИКИ)"); row += 29f;
        bool coResidentFound = false;
        if (person != null && householdSystem != null)
        {
            HouseholdRecord household = householdSystem.GetForCitizen(person.Id);
            if (household != null)
            foreach (string memberId in household.MemberCitizenIds)
            {
                if (memberId == selectedResident.Id) continue;
                ResidentAgent member = FindResidentAgent(memberId);
                GUI.Label(new Rect(18f, row, width - 90f, 22f), member != null ? member.Name : "Житель " + memberId);
                row += 23f; coResidentFound = true;
            }
        }
        if (!coResidentFound) { GUI.Label(new Rect(18f, row, width - 90f, 22f), "Других жильцов нет"); row += 23f; }
        GUI.Label(new Rect(8f, row + 8f, width - 70f, 40f), "Родство отображается только по явно сохранённым семейным связям. Общий адрес сам по себе не означает родство.");
        row += 58f;
        HouseholdRecord currentHousehold = person != null && householdSystem != null ? householdSystem.GetForCitizen(person.Id) : null;
        FamilyAccountRecord familyAccount = currentHousehold != null && familyEconomySystem != null ? familyEconomySystem.GetAccount(currentHousehold.Id) : null;
        GUI.Label(new Rect(8f, row, width - 70f, 24f), "СЕМЕЙНЫЙ СЧЁТ"); row += 25f;
        GUI.Label(new Rect(18f, row, width - 90f, 22f), "Общий баланс: " + (familyAccount != null ? familyAccount.Balance.ToString("N0") : "0") + " ₽"); row += 25f;
        GUI.Label(new Rect(18f, row, 150f, 22f), "Внести с личного счёта:");
        familyContributionAmount = GUI.TextField(new Rect(172f, row, 100f, 24f), familyContributionAmount);
        if (GUI.Button(new Rect(280f, row, 125f, 24f), "Пополнить"))
        {
            long amount = 0; string error = "проверьте данные";
            if (currentHousehold != null && long.TryParse(familyContributionAmount, out amount) && familyEconomySystem.Contribute(currentHousehold.Id, selectedResident.Id, amount, gameClock.Day, out error)) { status = "Взнос в семейный счёт зачислен."; SaveCity(); }
            else status = "Не удалось внести средства: " + (currentHousehold == null ? "нет домохозяйства" : (long.TryParse(familyContributionAmount, out amount) ? error : "проверьте сумму"));
        }
        row += 31f;
        GUI.Label(new Rect(18f, row, 150f, 22f), "Общий расход:");
        familyExpenseAmount = GUI.TextField(new Rect(172f, row, 100f, 24f), familyExpenseAmount);
        row += 27f;
        familyExpenseDescription = GUI.TextField(new Rect(18f, row, Mathf.Min(270f, width - 100f), 24f), familyExpenseDescription);
        if (GUI.Button(new Rect(296f, row, 120f, 24f), "Оплатить"))
        {
            long amount = 0; string error = "проверьте данные";
            if (currentHousehold != null && long.TryParse(familyExpenseAmount, out amount) && familyEconomySystem.PaySharedExpense(currentHousehold.Id, selectedResident.Id, "Общие расходы", familyExpenseDescription, amount, gameClock.Day, out error)) { status = "Общий расход оплачен из семейного счёта."; SaveCity(); }
            else status = "Не удалось оплатить: " + (currentHousehold == null ? "нет домохозяйства" : (long.TryParse(familyExpenseAmount, out amount) ? error : "проверьте сумму"));
        }
        row += 34f;
        GUI.Label(new Rect(8f, row, width - 70f, 22f), "ФИНАНСОВЫЕ ОБЯЗАННОСТИ"); row += 24f;
        string[] responsibilityCategories = { "Продукты", "Жильё и коммунальные услуги", "Одежда", "Досуг" };
        familyResponsibilityCategoryIndex = GUI.SelectionGrid(new Rect(18f, row, width - 90f, 48f), familyResponsibilityCategoryIndex, responsibilityCategories, 2); row += 53f;
        GUI.Label(new Rect(18f, row, 150f, 22f), "Доля обязанностей, %:");
        familyResponsibilityShare = GUI.TextField(new Rect(172f, row, 70f, 24f), familyResponsibilityShare);
        row += 27f;
        if (GUI.Button(new Rect(18f, row, 230f, 26f), "Назначить " + selectedResident.Name))
        {
            string error = "проверьте данные"; int share = 0;
            if (currentHousehold != null && int.TryParse(familyResponsibilityShare, out share) && familyEconomySystem.SetResponsibility(currentHousehold.Id, responsibilityCategories[familyResponsibilityCategoryIndex], selectedResident.Id, share, out error)) { status = "Финансовая обязанность назначена."; SaveCity(); }
            else status = "Не удалось назначить обязанность: " + (currentHousehold == null ? "нет домохозяйства" : (int.TryParse(familyResponsibilityShare, out share) ? error : "проверьте долю"));
        }
        row += 32f;
        if (currentHousehold != null && familyEconomySystem != null)
        {
            IList<FamilyResponsibilityRecord> duties = familyEconomySystem.GetResponsibilities(currentHousehold.Id);
            foreach (FamilyResponsibilityRecord duty in duties)
            {
                ResidentAgent owner = FindResidentAgent(duty.CitizenId);
                GUI.Label(new Rect(18f, row, width - 90f, 21f), duty.Category + " — " + (owner != null ? owner.Name : duty.CitizenId) + ": " + duty.SharePercent + "%"); row += 22f;
            }
            IList<FamilyFinanceTransactionRecord> ledger = familyEconomySystem.GetTransactions(currentHousehold.Id);
            GUI.Label(new Rect(8f, row, width - 70f, 22f), "ПОСЛЕДНИЕ ОПЕРАЦИИ"); row += 23f;
            int count = Mathf.Min(5, ledger.Count);
            for (int i = 0; i < count; i++)
            {
                FamilyFinanceTransactionRecord tx = ledger[i];
                GUI.Label(new Rect(18f, row, width - 90f, 21f), "День " + tx.Day + " · " + tx.Description + " · " + tx.Amount.ToString("N0") + " ₽"); row += 22f;
            }
        }
        GUI.Label(new Rect(8f, row + 5f, width - 70f, 40f), "Личные кошельки остаются отдельными. Взносы списываются с кошелька участника; общие расходы оплачиваются только из семейного счёта.");
        GUI.EndScrollView();
    }

    private ResidentAgent FindResidentAgent(string citizenId)
    {
        if (string.IsNullOrEmpty(citizenId)) return null;
        for (int i = 0; i < residents.Count; i++)
            if (residents[i] != null && residents[i].Id == citizenId) return residents[i];
        return null;
    }

    private void DrawAirTrafficPanel()
    {
        if (!showAirTrafficPanel || airTransportSystem == null) return;
        float x = Mathf.Max(350f, Screen.width - 520f);
        float y = 286f;
        GUI.Box(new Rect(x, y, 500f, 260f), "АЭРОПОРТ · ОПЕРАЦИОННОЕ ТАБЛО РЕЙСОВ");
        GUI.Label(new Rect(x+14,y+30,470,22), "Рейсов сегодня: "+airTransportSystem.ScheduledToday+"   В полёте: "+airTransportSystem.ActiveFlights+"   План пассажиров: "+airTransportSystem.PlannedPassengers);
        IList<FlightOperation> flights = airTransportSystem.Flights;
        for (int i=0;i<flights.Count;i++)
        {
            FlightOperation f=flights[i];
            string time=(f.DepartureMinute/60).ToString("00")+":"+(f.DepartureMinute%60).ToString("00");
            string line=f.FlightNumber+"  "+f.Destination+"  "+time+"  ·  "+airTransportSystem.GetPhaseLabel(f.Phase)+"  ·  "+f.PlannedPassengers+"/"+f.Capacity;
            GUI.Label(new Rect(x+14,y+62+i*39,472,30),line);
        }
        GUI.Label(new Rect(x+14,y+225,472,22), "Расписание демонстрационное; рейсы пока не отображаются движущимися самолётами.");
    }

    private void DrawPassengerNetworkPanel()
    {
        if (!showPassengerNetworkPanel || passengerTransportNetworkSystem == null) return;
        float x = Mathf.Max(350f, Screen.width - 540f);
        float y = 560f;
        GUI.Box(new Rect(x, y, 520f, 250f), "ПАССАЖИРСКАЯ СЕТЬ · УЗЛЫ И ПЕРЕСАДКИ");
        GUI.Label(new Rect(x+12, y+28, 496, 22), "Пассажиропоток: " + passengerTransportNetworkSystem.TotalPassengersPerHour + "/ч   Перегружено узлов: " + passengerTransportNetworkSystem.OverloadedNodes);
        IList<PassengerTransportNode> nodes = passengerTransportNetworkSystem.Nodes;
        for (int i=0; i<nodes.Count; i++)
        {
            PassengerTransportNode node = nodes[i];
            string line = passengerTransportNetworkSystem.GetTypeLabel(node.Type) + " · " + node.Name +
                " · " + node.PassengersPerHour + "/ч · " + node.TransferCount + " перес. · " +
                Mathf.RoundToInt(node.Utilization*100f) + "% загрузки";
            GUI.Label(new Rect(x+12, y+56+i*32, 496, 26), line);
        }
        GUI.Label(new Rect(x+12, y+190, 496, 48), "Прототип агрегированного спроса: пассажиры пока не прокладывают индивидуальные маршруты и не перемещаются в транспорте.");
    }

    private Vector2 internationalAirportScroll;
    private void DrawInternationalAirportPanel()
    {
        if (!showInternationalAirportPanel || internationalAirportNetworkSystem == null) return;
        float x = 18f;
        float y = 190f;
        float w = Mathf.Min(560f, Screen.width - 36f);
        GUI.Box(new Rect(x, y, w, 520f), "МЕЖДУНАРОДНЫЙ АЭРОПОРТ · СЕТЬ НАПРАВЛЕНИЙ");
        GUI.Label(new Rect(x+12, y+28, w-24, 22), "Открыто маршрутов: " + internationalAirportNetworkSystem.OpenRouteCount +
            " / " + internationalAirportNetworkSystem.Destinations.Count + "   Плановый спрос: " + internationalAirportNetworkSystem.TotalDailyDemand + " пасс./день");
        internationalAirportScroll = GUI.BeginScrollView(new Rect(x+10,y+54,w-20,450), internationalAirportScroll,
            new Rect(0,0,w-44,internationalAirportNetworkSystem.Destinations.Count*54+8));
        IList<InternationalDestination> destinations = internationalAirportNetworkSystem.Destinations;
        for(int i=0;i<destinations.Count;i++)
        {
            InternationalDestination d=destinations[i];
            float rowY=i*54;
            string routeState=d.IsRouteOpen?"ОТКРЫТ":"ЗАКРЫТ";
            GUI.Label(new Rect(4,rowY+2,300,22),d.City+" · "+d.Country+"  ["+routeState+"]");
            GUI.Label(new Rect(4,rowY+24,350,20),internationalAirportNetworkSystem.GetRegionLabel(d.Region)+" · "+d.DistanceKm+" км · "+(d.FlightMinutes/60)+" ч "+(d.FlightMinutes%60)+" мин · спрос "+d.BaseDemandPerDay+"/день");
            string button=d.IsRouteOpen?"Закрыть":"Открыть маршрут";
            if(GUI.Button(new Rect(w-174,rowY+8,142,30),button))
            {
                internationalAirportNetworkSystem.SetRouteOpen(d.Id,!d.IsRouteOpen);
                status=(d.IsRouteOpen?"Маршрут открыт: ":"Маршрут закрыт: ")+d.City;
            }
        }
        GUI.EndScrollView();
        GUI.Label(new Rect(x+12,y+496,w-24,20),"Маршруты — прототип управления сетью; рейсы, самолёты и продажи билетов пока не запускаются.");
    }


    private void DrawInternationalFleetPanel()
    {
        if (!showInternationalFleetPanel || internationalAirlineFleetSystem == null || internationalAirportNetworkSystem == null) return;
        float x = Mathf.Max(350f, Screen.width - 650f);
        float y = 190f;
        float w = Mathf.Min(630f, Screen.width - x - 18f);
        if (w < 400f) { x = 18f; w = Mathf.Min(630f, Screen.width - 36f); }
        GUI.Box(new Rect(x,y,w,540f), "АВИАКОМПАНИЯ · ПАРК И МЕЖДУНАРОДНОЕ РАСПИСАНИЕ");
        GUI.Label(new Rect(x+12,y+28,w-24,22), "Самолётов: "+internationalAirlineFleetSystem.Aircraft.Count+
            " · занято: "+internationalAirlineFleetSystem.AssignedAircraftCount+
            " · рейсов сегодня: "+internationalAirlineFleetSystem.ScheduledToday);
        float contentHeight=internationalAirlineFleetSystem.Aircraft.Count*36f+internationalAirportNetworkSystem.Destinations.Count*54f+90f;
        internationalFleetScroll=GUI.BeginScrollView(new Rect(x+10,y+54,w-20,470),internationalFleetScroll,new Rect(0,0,w-44,contentHeight));
        float row=0;
        GUI.Label(new Rect(4,row,w-50,24),"ПАРК ВОЗДУШНЫХ СУДОВ"); row+=27;
        IList<AircraftRecord> aircraft=internationalAirlineFleetSystem.Aircraft;
        for(int i=0;i<aircraft.Count;i++)
        {
            AircraftRecord a=aircraft[i];
            GUI.Label(new Rect(4,row,w-50,28),a.Registration+" · "+a.Model+" · "+a.PassengerCapacity+" мест · "+a.RangeKm+" км · "+internationalAirlineFleetSystem.GetAircraftStateLabel(a.State));
            row+=36;
        }
        row+=8; GUI.Label(new Rect(4,row,w-50,24),"МЕЖДУНАРОДНЫЕ НАПРАВЛЕНИЯ И НАЗНАЧЕНИЕ БОРТОВ"); row+=28;
        IList<InternationalDestination> destinations=internationalAirportNetworkSystem.Destinations;
        for(int i=0;i<destinations.Count;i++)
        {
            InternationalDestination d=destinations[i];
            string assigned="";
            IList<InternationalScheduledFlight> flights=internationalAirlineFleetSystem.Flights;
            for(int j=0;j<flights.Count;j++) if(flights[j].DestinationId==d.Id && flights[j].Day==(gameClock!=null?gameClock.Day:1)) { assigned=" · "+flights[j].FlightNumber+" · "+internationalAirlineFleetSystem.GetFlightPhaseLabel(flights[j].Phase); break; }
            GUI.Label(new Rect(4,row,w-190,22),d.City+" · "+(d.IsRouteOpen?"маршрут открыт":"закрыт")+" · спрос "+d.BaseDemandPerDay+"/день"+assigned);
            GUI.Label(new Rect(4,row+21,w-190,20),d.DistanceKm+" км · "+(d.FlightMinutes/60)+" ч "+(d.FlightMinutes%60)+" мин");
            GUI.enabled=d.IsRouteOpen;
            if(GUI.Button(new Rect(w-178,row+4,154,30),"Назначить самолёт"))
            {
                string result; internationalAirlineFleetSystem.TryAssignAircraft(d.Id,out result); status=result;
            }
            GUI.enabled=true;
            row+=54;
        }
        row+=6; GUI.Label(new Rect(4,row,w-50,24),"РАСПИСАНИЕ НА СЕГОДНЯ"); row+=26;
        IList<InternationalScheduledFlight> today=internationalAirlineFleetSystem.Flights;
        int shown=0;
        for(int i=0;i<today.Count;i++) if(today[i].Day==(gameClock!=null?gameClock.Day:1))
        {
            InternationalScheduledFlight f=today[i];
            string time=(f.DepartureMinute/60).ToString("00")+":"+(f.DepartureMinute%60).ToString("00");
            GUI.Label(new Rect(4,row,w-50,24),f.FlightNumber+" · "+f.DestinationCity+" · "+time+" · "+f.PlannedPassengers+"/"+f.Capacity+" · "+internationalAirlineFleetSystem.GetFlightPhaseLabel(f.Phase));
            row+=25; shown++;
        }
        if(shown==0) GUI.Label(new Rect(4,row,w-50,24),"Пока нет назначенных международных рейсов.");
        GUI.EndScrollView();
        GUI.Label(new Rect(x+12,y+516,w-24,20),"Расчётные игровые характеристики. Билеты, денежные расчёты и 3D-полёт — следующие этапы.");
    }

    private void DrawAirlineEconomyPanel()
    {
        if (!showAirlineEconomyPanel || airlineEconomySystem == null || internationalAirlineFleetSystem == null || internationalAirportNetworkSystem == null) return;
        float x = Mathf.Max(300f, Screen.width - 650f); float y = 190f; float w = Mathf.Min(630f, Screen.width - x - 18f);
        if (w < 400f) { x = 18f; w = Mathf.Min(630f, Screen.width - 36f); }
        GUI.Box(new Rect(x,y,w,540f), "АВИАКОМПАНИЯ · ТАРИФЫ И ФИНАНСОВЫЙ РЕЗУЛЬТАТ");
        GUI.Label(new Rect(x+12,y+28,w-24,22), "Выручка: "+airlineEconomySystem.TotalRevenue.ToString("N0")+" ₽ · расходы: "+airlineEconomySystem.TotalCosts.ToString("N0")+" ₽ · результат: "+airlineEconomySystem.NetResult.ToString("N0")+" ₽");
        float height = internationalAirportNetworkSystem.Destinations.Count*52f + internationalAirlineFleetSystem.Flights.Count*48f + 100f;
        airlineEconomyScroll = GUI.BeginScrollView(new Rect(x+10,y+54,w-20,470),airlineEconomyScroll,new Rect(0,0,w-44,height));
        float row=0; GUI.Label(new Rect(4,row,w-50,22),"ТАРИФЫ МЕЖДУНАРОДНЫХ НАПРАВЛЕНИЙ · ₽ ЗА БИЛЕТ"); row+=26;
        foreach (InternationalDestination d in internationalAirportNetworkSystem.Destinations)
        {
            int fare=airlineEconomySystem.GetFare(d.Id);
            GUI.Label(new Rect(4,row,w-220,24),d.City+" · рекомендовано "+airlineEconomySystem.GetRecommendedFare(d).ToString("N0")+" ₽");
            string edit; if (!airlineFareEdits.TryGetValue(d.Id,out edit)) edit=fare.ToString();
            edit=GUI.TextField(new Rect(w-210,row,92,24),edit); airlineFareEdits[d.Id]=edit; int parsed;
            if (GUI.Button(new Rect(w-110,row,94,24),"Применить") && int.TryParse(edit,out parsed) && airlineEconomySystem.TrySetFare(d.Id,parsed)) airlineFareEdits[d.Id]=parsed.ToString();
            row+=30;
        }
        row+=8; GUI.Label(new Rect(4,row,w-50,22),"ДОХОДНОСТЬ МАРШРУТОВ · ПО ВЫПОЛНЕННЫМ РЕЙСАМ"); row+=26;
        foreach (InternationalDestination d in internationalAirportNetworkSystem.Destinations)
        {
            AirlineRoutePerformance perf=airlineEconomySystem.GetRoutePerformance(d.Id);
            if (perf.Flights<=0) continue;
            float load=perf.Capacity>0 ? 100f*perf.Passengers/perf.Capacity : 0f;
            GUI.Label(new Rect(4,row,w-50,34),perf.Destination+" · рейсов "+perf.Flights+" · загрузка "+load.ToString("0")+"% · выручка "+perf.Revenue.ToString("N0")+" · расходы "+perf.Costs.ToString("N0")+" · итог "+perf.Result.ToString("N0")+" ₽"); row+=36;
        }
        row+=8; GUI.Label(new Rect(4,row,w-50,22),"ФАКТИЧЕСКИ ВЫПОЛНЕННЫЕ РЕЙСЫ"); row+=26;
        foreach (InternationalScheduledFlight f in internationalAirlineFleetSystem.Flights)
        {
            if (!f.FinanciallySettled) continue;
            GUI.Label(new Rect(4,row,w-50,42),f.FlightNumber+" · "+f.DestinationCity+" · "+f.TicketsSold+"/"+f.Capacity+" билетов · "+f.TicketFare.ToString("N0")+" ₽\nВыручка "+f.TicketRevenue.ToString("N0")+" · расходы "+f.OperatingCost.ToString("N0")+" · итог "+f.OperatingResult.ToString("N0")+" ₽");
            row+=46;
        }
        if (airlineEconomySystem.SettledFlightCount==0) GUI.Label(new Rect(4,row,w-50,24),"Финансовые проводки появятся при фактическом вылете первого рейса.");
        GUI.EndScrollView();
        GUI.Label(new Rect(x+12,y+516,w-24,20),"Билеты агрегированы по рейсу; расчётная игровая модель, не реальная платёжная система.");
    }

    private void DrawAirportPassengerPanel()
    {
        if (!showAirportPassengerPanel || airportPassengerServiceSystem == null) return;
        float x = Mathf.Max(300f, Screen.width - 650f); float y = 190f; float w = Mathf.Min(630f, Screen.width - x - 18f);
        if (w < 400f) { x = 18f; w = Mathf.Min(630f, Screen.width - 36f); }
        GUI.Box(new Rect(x,y,w,610f), "АЭРОПОРТ · ПАССАЖИРСКОЕ ОБСЛУЖИВАНИЕ");
        GUI.Label(new Rect(x+12,y+28,w-24,22), "Активные рейсы: "+airportPassengerServiceSystem.ActiveFlights+" · зарегистрировано: "+airportPassengerServiceSystem.TotalCheckedIn+" · багаж: "+airportPassengerServiceSystem.TotalBaggage+" · на борту: "+airportPassengerServiceSystem.TotalBoarded);
        airportPassengerScroll = GUI.BeginScrollView(new Rect(x+10,y+54,w-20,470),airportPassengerScroll,new Rect(0,0,w-44,Mathf.Max(500,airportPassengerServiceSystem.Records.Count*92+airportIndividualPassengerSystem.Passengers.Count*24+100)));
        float row=0;
        foreach (PassengerServiceRecord r in airportPassengerServiceSystem.Records)
        {
            GUI.Label(new Rect(4,row,w-50,22),r.FlightNumber+" · "+r.Destination+" · "+airportPassengerServiceSystem.GetStageLabel(r.Stage)); row+=23;
            GUI.Label(new Rect(14,row,w-60,22),"Билеты: "+r.BookedPassengers+" · регистрация: "+r.CheckedInPassengers+" · багаж: "+r.BaggagePieces+" · контроль: "+r.SecurityClearedPassengers); row+=22;
            GUI.Label(new Rect(14,row,w-60,22),"Посадка: "+r.BoardedPassengers+" · ожидают: "+r.WaitingPassengers+" · "+r.LastUpdate); row+=30;
            GUI.Box(new Rect(4,row,w-60,1),""); row+=8;
        }
        if (airportPassengerServiceSystem.Records.Count==0) GUI.Label(new Rect(4,row,w-50,24),"Пассажирские операции появятся после назначения первого рейса.");
        row += 12;
        GUI.Label(new Rect(4,row,w-50,22),"ИНДИВИДУАЛЬНЫЕ ПАССАЖИРЫ"); row += 24;
        if (airportIndividualPassengerSystem != null)
        {
            int shown = 0;
            foreach (IndividualAirportPassenger passenger in airportIndividualPassengerSystem.Passengers)
            {
                if (shown >= 80) break;
                GUI.Label(new Rect(8,row,w-60,20),passenger.Id+" · "+passenger.Name+" · "+passenger.FlightNumber+" · место "+passenger.SeatNumber+" · "+airportIndividualPassengerSystem.GetStateLabel(passenger.State));
                row += 21; shown++;
            }
            if (airportIndividualPassengerSystem.Passengers.Count == 0) GUI.Label(new Rect(8,row,w-60,20),"Индивидуальные пассажиры появятся после назначения рейса.");
        }
        GUI.EndScrollView();
        GUI.Label(new Rect(x+12,y+516,w-24,20),"Индивидуальные пассажиры: "+(airportIndividualPassengerSystem!=null?airportIndividualPassengerSystem.TotalPassengers:0)+" · видимых персонажей: "+(airportIndividualPassengerSystem!=null?airportIndividualPassengerSystem.VisiblePassengers:0)+".");
        if (airportIndividualPassengerSystem != null) GUI.Label(new Rect(x+12,y+536,w-24,20),"Регистрация: "+airportIndividualPassengerSystem.RegisteredCount+" · контроль: "+airportIndividualPassengerSystem.SecurityCount+" · посадка: "+airportIndividualPassengerSystem.BoardedCount+" · семьи: "+airportIndividualPassengerSystem.FamiliesCount+" · опоздали: "+airportIndividualPassengerSystem.MissedPassengersCount);
        if (airportIndividualPassengerSystem != null) GUI.Label(new Rect(x+12,y+556,w-24,20),"Очередь регистрации: "+airportIndividualPassengerSystem.CheckInQueueCount+" · очередь контроля: "+airportIndividualPassengerSystem.SecurityQueueCount+" · багаж принят: "+airportIndividualPassengerSystem.BaggageAcceptedCount+" · видимые чемоданы: "+airportIndividualPassengerSystem.ActiveBaggageVisuals);
        if (airportIndividualPassengerSystem != null) GUI.Label(new Rect(x+12,y+576,w-24,20),"Прилёт: "+airportIndividualPassengerSystem.ArrivedPassengersCount+" · получают багаж: "+airportIndividualPassengerSystem.BaggageClaimCount+" · покинули терминал: "+airportIndividualPassengerSystem.ExitedPassengersCount);
    }

    // Visual evolution is deterministic and rebuilt from the saved level on load.
    // Each tier adds a small architectural layer; it never changes gameplay footprint.
    private void ApplyBuildingVisualEvolution(PlacedBuilding building)
    {
        if (building == null || building.Root == null) return;
        Transform old = building.Root.transform.Find("Upgrade Visuals");
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
        }
        if (building.Level <= 1) return;
        GameObject visuals = new GameObject("Upgrade Visuals");
        visuals.transform.SetParent(building.Root.transform, false);
        float w = Mathf.Max(4f, building.Footprint.x);
        float d = Mathf.Max(4f, building.Footprint.z);
        float h = Mathf.Max(2.5f, building.VisualHeight);
        int tier = building.Level;
        string buildingName = building.Name ?? string.Empty;
        bool isCottage = buildingName.IndexOf("Коттедж", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isApartment = buildingName.IndexOf("Многоквартир", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isResidential = building.Residential;
        bool isWarehouse = buildingName.IndexOf("база", StringComparison.OrdinalIgnoreCase) >= 0 || buildingName.IndexOf("склад", StringComparison.OrdinalIgnoreCase) >= 0 || buildingName.IndexOf("центр", StringComparison.OrdinalIgnoreCase) >= 0 || buildingName.IndexOf("хранилище", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isFuel = buildingName.IndexOf("топлив", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isIndustrialWarehouse = buildingName.IndexOf("промышлен", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isWholesale = buildingName.IndexOf("оптов", StringComparison.OrdinalIgnoreCase) >= 0;
        bool isRetail = building.Workplace && !isWarehouse;
        bool isHighRise = buildingName.IndexOf("небоскрёб", StringComparison.OrdinalIgnoreCase) >= 0;

        // Distinct architectural profiles. The stable DefinitionId/Name is already persisted in BuildingRecord.
        if (isResidential && isCottage)
        {
            if (tier >= 2) Cube("Cottage roof eaves", new Vector3(0f, h + .12f, 0f), new Vector3(w + .85f, .22f, d + .85f), materials["wood"], visuals.transform);
            if (tier >= 4) { Cube("Cottage dormer left", new Vector3(-w*.22f, h+.48f, -d*.12f), new Vector3(1.15f,.85f,.85f), materials["white"], visuals.transform); Cube("Cottage dormer right", new Vector3(w*.22f, h+.48f, -d*.12f), new Vector3(1.15f,.85f,.85f), materials["white"], visuals.transform); }
            if (tier >= 7) { Cube("Cottage garden terrace", new Vector3(0f,.18f,d*.5f+.75f), new Vector3(w*.72f,.22f,1.5f), materials["leaf"], visuals.transform); }
            if (tier >= 11) Cube("Cottage chimney", new Vector3(w*.28f,h+.85f,d*.2f), new Vector3(.65f,1.7f,.65f), materials["brick"], visuals.transform);
        }
        else if (isResidential && isApartment)
        {
            if (tier >= 2) for (int floorIndex=1; floorIndex<=Mathf.Min(5,Mathf.RoundToInt(h/2.3f)); floorIndex++)
                for (int side=-1; side<=1; side+=2) Cube("Apartment balcony", new Vector3(side*w*.27f,.8f+floorIndex*2.1f,-d*.5f-.48f), new Vector3(1.65f,.16f,1.0f), materials["white"], visuals.transform);
            if (tier >= 6) Cube("Apartment entrance canopy", new Vector3(0f,2.45f,-d*.5f-.72f), new Vector3(w*.58f,.28f,1.4f), materials["glass"], visuals.transform);
            if (tier >= 10) { Cube("Apartment rooftop penthouse", new Vector3(0f,h+.72f,0f), new Vector3(w*.38f,1.25f,d*.38f), materials["white"], visuals.transform); }
            if (tier >= 13) for (int side=-1;side<=1;side+=2) Cube("Apartment facade fin", new Vector3(side*(w*.5f-.12f),h*.5f,-d*.5f-.14f), new Vector3(.28f,h,.2f), materials["shop"], visuals.transform);
        }
        else if (isResidential)
        {
            if (tier >= 2) for (int side=-1;side<=1;side+=2) Cube("Residential balcony", new Vector3(side*w*.22f,h*.58f,-d*.5f-.38f), new Vector3(1.55f,.16f,.75f), materials["white"], visuals.transform);
            if (tier >= 7) Cube("Residential entrance portico", new Vector3(0f,1.45f,-d*.5f-.65f), new Vector3(w*.52f,.22f,1.25f), materials["roof"], visuals.transform);
        }
        else if (isRetail)
        {
            if (tier >= 2) { Cube("Retail display window", new Vector3(0f,1.55f,-d*.5f-.08f), new Vector3(w*.62f,1.55f,.10f), materials["glass"], visuals.transform); Cube("Retail awning", new Vector3(0f,2.55f,-d*.5f-.62f), new Vector3(w*.78f,.24f,1.15f), materials["shop"], visuals.transform); }
            if (tier >= 5) Cube("Retail sign fascia", new Vector3(0f,3.25f,-d*.5f-.13f), new Vector3(w*.72f,.48f,.16f), materials["white"], visuals.transform);
            if (tier >= 9) { for(int side=-1;side<=1;side+=2) Cube("Retail facade pilaster", new Vector3(side*w*.42f,2.0f,-d*.5f-.12f), new Vector3(.22f,3.8f,.18f), materials["cream"], visuals.transform); }
            if (tier >= 13) Cube("Retail rooftop brand feature", new Vector3(0f,h+1.15f,0f), new Vector3(w*.34f,.32f,d*.28f), materials["shop"], visuals.transform);
        }
        else if (isHighRise)
        {
            if (tier >= 2) Cube("High-rise upgraded podium canopy", new Vector3(0f,3.9f,-d*.5f-.7f), new Vector3(w*.72f,.28f,1.4f), materials["white"], visuals.transform);
            if (tier >= 4) for(int side=-1;side<=1;side+=2) Cube("High-rise upgraded corner light", new Vector3(side*(w*.5f-.2f),h*.5f,-d*.5f-.16f), new Vector3(.12f,h*.92f,.12f), materials["shop"], visuals.transform);
            if (tier >= 7) Cube("High-rise sky lobby", new Vector3(0f,h*.68f,0f), new Vector3(w*1.015f,.28f,d*1.015f), materials["white"], visuals.transform);
            if (tier >= 10) Cube("High-rise rooftop garden", new Vector3(0f,h+.18f,0f), new Vector3(w*.46f,.22f,d*.46f), materials["leaf"], visuals.transform);
            if (tier >= 13) Cube("High-rise landmark crown upgrade", new Vector3(0f,h+1.1f,0f), new Vector3(w*.34f,1.4f,d*.34f), materials["shop"], visuals.transform);
        }
        else if (isWarehouse)
        {
            if (tier >= 2) for(int bay=-1;bay<=1;bay++) Cube("Warehouse dock frame", new Vector3(bay*w*.25f,1.55f,-d*.5f-.18f), new Vector3(2.9f,3.0f,.22f), materials["white"], visuals.transform);
            if (isFuel)
            {
                if (tier >= 4) { Cube("Fuel pipe main", new Vector3(0f,4.7f,d*.32f), new Vector3(.24f,.24f,d*.72f), materials["white"], visuals.transform); Cube("Fuel safety tower", new Vector3(w*.38f,2.6f,d*.32f), new Vector3(.42f,5.2f,.42f), materials["roof"], visuals.transform); }
                if (tier >= 9) for(int side=-1;side<=1;side+=2) Cube("Fuel tank reinforcement", new Vector3(side*3.4f,2.1f,0f), new Vector3(4.45f,4.45f,.18f), materials["roof"], visuals.transform);
            }
            else if (isIndustrialWarehouse)
            {
                if (tier >= 4) for(int stack=0;stack<3;stack++) Cube("Industrial exhaust stack", new Vector3(-w*.3f+stack*w*.3f,h+.85f,d*.25f), new Vector3(.55f,1.7f,.55f), materials["roof"], visuals.transform);
                if (tier >= 9) Cube("Industrial conveyor canopy", new Vector3(0f,3.8f,d*.5f+.9f), new Vector3(w*.62f,.24f,1.8f), materials["white"], visuals.transform);
            }
            else if (isWholesale)
            {
                if (tier >= 4) for(int side=-1;side<=1;side+=2) Cube("Wholesale side annex", new Vector3(side*w*.36f,1.3f,0f), new Vector3(2.1f,2.6f,d*.72f), materials["white"], visuals.transform);
                if (tier >= 9) Cube("Wholesale dispatch tower", new Vector3(w*.36f,h+.9f,0f), new Vector3(1.2f,1.8f,1.2f), materials["shop"], visuals.transform);
            }
            else
            {
                if (tier >= 4) Cube("Distribution sorting annex", new Vector3(0f,1.45f,d*.34f), new Vector3(w*.58f,2.9f,2.2f), materials["white"], visuals.transform);
                if (tier >= 9) for(int side=-1;side<=1;side+=2) Cube("Distribution ventilation", new Vector3(side*w*.32f,h+.55f,d*.25f), new Vector3(.8f,.8f,.8f), materials["glass"], visuals.transform);
            }
            if (tier >= 13) Cube("Warehouse logistics tower", new Vector3(w*.38f,h+1.1f,-d*.3f), new Vector3(1.25f,2.2f,1.25f), materials["white"], visuals.transform);
        }

        // Levels 2–5: refined facade, cornice and entrance canopy.
        Cube("Facade cornice", new Vector3(0f, h - .12f, 0f), new Vector3(w + .28f, .16f, d + .28f), materials["white"], visuals.transform);
        Cube("Entrance canopy", new Vector3(0f, 2.15f, -d * .5f - .48f), new Vector3(Mathf.Min(w * .72f, 5.2f), .22f, 1.15f), materials["roof"], visuals.transform);
        if (tier >= 3)
        {
            for (int side = -1; side <= 1; side += 2)
                Cube("Facade vertical trim", new Vector3(side * (w * .5f - .18f), h * .5f, -d * .5f - .055f), new Vector3(.16f, h - .35f, .12f), materials["white"], visuals.transform);
        }
        if (tier >= 4)
        {
            Cube("Roof parapet front", new Vector3(0f, h + .34f, -d * .5f), new Vector3(w + .45f, .42f, .18f), materials["white"], visuals.transform);
            Cube("Roof parapet rear", new Vector3(0f, h + .34f, d * .5f), new Vector3(w + .45f, .42f, .18f), materials["white"], visuals.transform);
        }
        if (tier >= 5)
        {
            Cube("Facade accent", new Vector3(0f, h * .52f, -d * .5f - .09f), new Vector3(w * .72f, .18f, .12f), materials["shop"], visuals.transform);
        }

        // Levels 6–10: rooftop extension and energy/utility equipment.
        if (tier >= 6)
        {
            float annexW = Mathf.Min(w * .48f, 4.2f);
            Cube("Rooftop annex", new Vector3(0f, h + .82f, 0f), new Vector3(annexW, 1.25f, Mathf.Min(d * .48f, 4f)), materials["cream"], visuals.transform);
            Cube("Rooftop annex roof", new Vector3(0f, h + 1.48f, 0f), new Vector3(annexW + .18f, .14f, Mathf.Min(d * .48f, 4f) + .18f), materials["roof"], visuals.transform);
        }
        if (tier >= 8)
        {
            for (int i = -1; i <= 1; i++)
                Cube("Solar panel", new Vector3(i * Mathf.Min(1.15f, w * .16f), h + .43f, d * .22f), new Vector3(.92f, .10f, 1.35f), materials["glass"], visuals.transform);
        }
        if (tier >= 10)
        {
            Cube("Rooftop service mast", new Vector3(w * .34f, h + 1.25f, d * .30f), new Vector3(.16f, 2.1f, .16f), materials["roof"], visuals.transform);
        }

        // Levels 11–15: landmark crown, corner lighting and rooftop greenery.
        if (tier >= 11)
        {
            Cube("Landmark crown", new Vector3(0f, h + 1.68f, 0f), new Vector3(Mathf.Min(w * .62f, 5.2f), .28f, Mathf.Min(d * .62f, 5.2f)), materials["shop"], visuals.transform);
        }
        if (tier >= 13)
        {
            for (int side = -1; side <= 1; side += 2)
                Cube("Corner light", new Vector3(side * (w * .5f - .12f), h * .56f, -d * .5f - .13f), new Vector3(.22f, .34f, .12f), materials["white"], visuals.transform);
            Cube("Rooftop garden", new Vector3(0f, h + .58f, d * .34f), new Vector3(Mathf.Min(w * .54f, 4.8f), .22f, 1.15f), materials["leaf"], visuals.transform);
        }
        if (tier >= 15)
        {
            Cube("Signature rooftop feature", new Vector3(0f, h + 2.05f, 0f), new Vector3(.42f, 1.25f, .42f), materials["white"], visuals.transform);
            Cube("Signature rooftop cap", new Vector3(0f, h + 2.72f, 0f), new Vector3(1.15f, .18f, 1.15f), materials["shop"], visuals.transform);
        }
    }

    private long GetBuildingUpgradeCost(PlacedBuilding building)
    {
        if (building == null) return long.MaxValue;
        return Math.Max(1000L, (long)Math.Round(Math.Max(1, building.Cost) * (building.Level <= 1 ? 0.45 : 0.70 + 0.18 * (building.Level - 2))));
    }

    private void UpgradeBuilding(PlacedBuilding building)
    {
        if (building == null || building.Level >= 15) { status = "Для этого здания уже достигнут максимальный уровень."; return; }
        long cost = GetBuildingUpgradeCost(building);
        if (money < cost) { status = "Недостаточно средств городского бюджета для улучшения."; return; }
        money -= (int)cost;
        building.Level = Mathf.Clamp(building.Level + 1, 1, 15);
        ApplyBuildingVisualEvolution(building);
        if (building.Residential)
            building.Capacity = Mathf.Max(building.BaseCapacity, Mathf.RoundToInt(building.BaseCapacity * Mathf.Pow(1.10f, building.Level - 1)));
        if (building.Workplace)
        {
            building.StaffCapacity = Mathf.Max(1, building.StaffCapacity + 1);
            WorkplaceRecord workplace = FindWorkplaceRecord(building.Id);
            if (workplace != null) workplace.StaffCapacity = building.StaffCapacity;
        }
        if (economySystem != null) economySystem.RecordExternalCityExpense(gameClock != null ? gameClock.Day : 1, "Модернизация зданий", "Улучшение: " + building.Name + " · уровень " + building.Level, cost, "upgrade-" + building.Id + "-L" + building.Level);
        status = building.Name + " улучшено до уровня " + building.Level + ". " + (building.Residential ? "Вместимость увеличена." : (building.Workplace ? "Увеличено число рабочих мест." : "Уровень объекта повышен."));
        SaveCity();
    }

    private void DrawMayorOfficePanel()
    {
        if (!showMayorOffice || mayorOfficeSystem == null) return;
        float w = Mathf.Min(720f, Screen.width - 24f), h = Mathf.Min(590f, Screen.height - 24f);
        float x = Mathf.Max(12f, (Screen.width-w)*0.5f), y = Mathf.Max(12f, (Screen.height-h)*0.5f);
        GUI.Box(new Rect(x,y,w,h), "Приёмная мэра · обращения и решения");
        GUI.Label(new Rect(x+16,y+28,w-32,22), "Поддержка жителей: " + mayorOfficeSystem.ApprovalScore + "/100 · Ожидают решения: " + mayorOfficeSystem.PendingCount);
        if (mayorOfficeSystem.Reports.Count > 0) { MayorDailyReportRecord report = mayorOfficeSystem.Reports[mayorOfficeSystem.Reports.Count - 1]; GUI.Label(new Rect(x+16,y+49,w-32,20), "Последний отчёт · день " + report.Day + ": " + report.Summary + " Расходы: " + report.MoneySpent.ToString("N0") + " ₽"); }
        GUI.Label(new Rect(x+16,y+72,w-32,18), "Объект будущего ремонта:");
        List<string> maintenanceTargets = new List<string>();
        for (int bi=0; bi<buildings.Count; bi++) if (buildings[bi] != null) maintenanceTargets.Add(buildings[bi].Name + " · " + (buildings[bi].Condition*100f).ToString("0") + "%");
        if (maintenanceTargets.Count > 0)
        {
            mayorMaintenanceBuildingIndex = Mathf.Clamp(mayorMaintenanceBuildingIndex, 0, maintenanceTargets.Count-1);
            mayorMaintenanceBuildingIndex = GUI.SelectionGrid(new Rect(x+16,y+91,w-32,26), mayorMaintenanceBuildingIndex, maintenanceTargets.ToArray(), Mathf.Min(3, maintenanceTargets.Count));
        }
        else GUI.Label(new Rect(x+16,y+94,w-32,22), "Сначала постройте хотя бы одно здание.");
        if (buildings.Count > 0)
        {
            PlacedBuilding upgradeTarget = buildings[Mathf.Clamp(mayorMaintenanceBuildingIndex, 0, buildings.Count - 1)];
            long upgradeCost = GetBuildingUpgradeCost(upgradeTarget);
            string benefit = upgradeTarget.Residential ? "вместимость +10%" : (upgradeTarget.Workplace ? "+1 место для сотрудника" : "повышение уровня объекта");
            GUI.Label(new Rect(x+16,y+116,w-220,20), "Уровень " + upgradeTarget.Level + "/15 · " + benefit + " · " + upgradeCost.ToString("N0") + " ₽");
            GUI.enabled = upgradeTarget.Level < 15 && money >= upgradeCost;
            if (GUI.Button(new Rect(x+w-196,y+112,180,26), upgradeTarget.Level >= 15 ? "Максимальный уровень" : "Улучшить здание")) UpgradeBuilding(upgradeTarget);
            GUI.enabled = true;
        }
        Rect view = new Rect(x+12,y+145,w-24,h-191);
        IList<MayorRequestRecord> items = mayorOfficeSystem.Requests;
        float content = Mathf.Max(view.height, items.Count*142f+8f);
        mayorOfficeScroll = GUI.BeginScrollView(view, mayorOfficeScroll, new Rect(0,0,view.width-18,content));
        float cy=6;
        if (items.Count==0) GUI.Label(new Rect(8,cy,view.width-40,28), "Обращений пока нет. Они будут поступать по мере развития города.");
        for(int i=items.Count-1;i>=0;i--)
        {
            MayorRequestRecord r=items[i]; if(r==null) continue;
            GUI.Box(new Rect(5,cy,view.width-32,134), GUIContent.none);
            GUI.Label(new Rect(14,cy+6,view.width-58,20), "День "+r.CreatedDay+" · "+r.SenderType+" · "+r.Category+" · Приоритет "+r.Priority);
            GUI.Label(new Rect(14,cy+28,view.width-58,20), r.Title+" — "+r.SenderName);
            GUI.Label(new Rect(14,cy+49,view.width-58,34), r.Message);
            GUI.Label(new Rect(14,cy+84,view.width-350,20), "Срок: день "+r.DueDay+" · Стоимость решения: "+r.SuggestedCost.ToString("N0")+" ₽ · "+r.Status);
            if(r.Status=="Ожидает решения")
            {
                GUI.enabled = r.SuggestedCost <= money && (!((r.Category == "Ремонт" || r.Category == "Инфраструктура" || r.Category == "Благоустройство")) || buildings.Count > 0);
                if(GUI.Button(new Rect(view.width-294,cy+104,132,24), r.SuggestedCost>0?"Одобрить и выполнить":"Принять решение"))
                {
                    long cost; string error;
                    if(mayorOfficeSystem.Resolve(r.Id,"Одобрено",true,money,gameClock!=null?gameClock.Day:1,out cost,out error))
                    {
                        money -= (int)cost;
                        if(cost>0 && economySystem!=null) economySystem.RecordExternalCityExpense(gameClock!=null?gameClock.Day:1,"Обращения мэру",r.Title,cost,r.Id);
                        if (maintenanceSystem != null && (r.Category == "Ремонт" || r.Category == "Инфраструктура" || r.Category == "Благоустройство"))
                        {
                            string targetId = buildings.Count > 0 && buildings[Mathf.Clamp(mayorMaintenanceBuildingIndex, 0, buildings.Count-1)] != null ? buildings[Mathf.Clamp(mayorMaintenanceBuildingIndex, 0, buildings.Count-1)].Id : null;
                            CityMaintenanceProjectRecord project; string projectError;
                            if (maintenanceSystem.TryCreateFromRequest(r, targetId, gameClock!=null?gameClock.Day:1, out project, out projectError)) status="Ремонтный проект создан: "+project.Title+". Работы займут 3 игровых дня.";
                            else status="Решение принято, но проект не создан: "+projectError;
                        }
                        else status="Решение принято: "+r.Title+". Расход: "+cost.ToString("N0")+" ₽."; SaveCity();
                    }
                    else status=error;
                }
                GUI.enabled=true;
                if(GUI.Button(new Rect(view.width-154,cy+104,130,24),"Отложить"))
                {
                    long cost; string error;
                    if(mayorOfficeSystem.Resolve(r.Id,"Отложено до пересмотра",false,money,gameClock!=null?gameClock.Day:1,out cost,out error)) { status="Обращение отложено. Поддержка жителей немного снизилась."; SaveCity(); }
                    else status=error;
                }
            }
            else GUI.Label(new Rect(view.width-270,cy+106,245,20), "Решение: "+(r.Decision??r.Status));
            cy+=142;
        }
        GUI.EndScrollView();
        if (maintenanceSystem != null && maintenanceSystem.Projects.Count > 0)
        {
            IList<CityMaintenanceProjectRecord> projects = maintenanceSystem.Projects;
            CityMaintenanceProjectRecord latest = projects[projects.Count-1];
            GUI.Label(new Rect(x+176,y+h-36,w-300,24), "Проект: "+latest.Title+" · "+latest.Status+" · "+latest.ProgressDays+"/"+latest.RequiredDays+" дней");
        }
        if(GUI.Button(new Rect(x+16,y+h-36,150,24),"Сохранить решения")) SaveCity();
        if(GUI.Button(new Rect(x+w-112,y+h-36,96,24),"Закрыть")) showMayorOffice=false;
    }

    private void DrawCityFinanceLedgerPanel()
    {
        if (!showFinanceLedger) return;
        float w = Mathf.Min(680f, Screen.width - 28f);
        float h = Mathf.Min(520f, Screen.height - 28f);
        float x = Mathf.Max(14f, (Screen.width - w) * 0.5f);
        float y = Mathf.Max(14f, (Screen.height - h) * 0.5f);
        GUI.Box(new Rect(x, y, w, h), "Финансовый журнал города");
        int currentDay = gameClock != null ? gameClock.Day : 1;
        financeLedgerCurrentDayOnly = GUI.Toggle(new Rect(x + 16, y + 30, 190, 22), financeLedgerCurrentDayOnly, "Только текущий игровой день");
        List<FinanceLedgerLine> lines = new List<FinanceLedgerLine>();
        List<SupplierRecord> suppliersForLedger = new List<SupplierRecord>();
        if (supplyLogisticsSystem != null)
        {
            supplyLogisticsSystem.GetSupplierContracts(suppliersForLedger);
            for (int i = 0; i < suppliersForLedger.Count; i++)
            {
                SupplierRecord supplier = suppliersForLedger[i];
                if (supplier == null || supplier.FinancialHistory == null) continue;
                for (int j = 0; j < supplier.FinancialHistory.Count; j++)
                {
                    SupplierFinancialTransactionRecord tx = supplier.FinancialHistory[j];
                    if (tx == null || tx.Amount <= 0 || (financeLedgerCurrentDayOnly && tx.Day != currentDay)) continue;
                    lines.Add(new FinanceLedgerLine { Day = tx.Day, Category = "Поставщики · " + (tx.Type ?? "Операция"), Description = supplier.DisplayName + " · " + (tx.Description ?? tx.OrderId ?? ""), Amount = tx.Amount, IsIncome = tx.Type == "Возврат за брак/недостачу" || tx.Type == "Дополнительная компенсация" || tx.Type == "Возврат оплаты" });
                }
            }
        }
        List<FleetVehicleRecord> fleetVehicles = new List<FleetVehicleRecord>();
        List<FleetTransactionRecord> fleetTransactions = new List<FleetTransactionRecord>();
        if (fleetManagementSystem != null) fleetManagementSystem.Export(fleetVehicles, fleetTransactions);
        for (int i = 0; i < fleetTransactions.Count; i++)
        {
            FleetTransactionRecord tx = fleetTransactions[i];
            if (tx == null || tx.TotalCost <= 0 || (financeLedgerCurrentDayOnly && tx.Day != currentDay)) continue;
            string vehicleName = tx.VehicleId;
            FleetVehicleRecord vehicle = fleetVehicles.Find(v => v != null && v.Id == tx.VehicleId);
            if (vehicle != null && !string.IsNullOrEmpty(vehicle.DisplayName)) vehicleName = vehicle.DisplayName;
            lines.Add(new FinanceLedgerLine { Day = tx.Day, Category = "Транспорт · эксплуатация", Description = vehicleName + " · рейс " + tx.TransferId + " · " + tx.DistanceKm + " км", Amount = tx.TotalCost, IsIncome = false });
        }
        if (economySystem != null)
        {
            List<CityFinancialTransactionRecord> economyTransactions = new List<CityFinancialTransactionRecord>();
            economySystem.ExportFinancialTransactions(economyTransactions);
            for (int i = 0; i < economyTransactions.Count; i++)
            {
                CityFinancialTransactionRecord tx = economyTransactions[i];
                if (tx == null || tx.Amount <= 0 || (financeLedgerCurrentDayOnly && tx.Day != currentDay)) continue;
                lines.Add(new FinanceLedgerLine { Day = tx.Day, Category = "Предприятия · " + (tx.Category ?? "Операция"), Description = tx.Description ?? string.Empty, Amount = tx.Amount, IsIncome = tx.IsIncome });
            }
        }
                lines.Sort((a, b) => { int byDay = b.Day.CompareTo(a.Day); return byDay != 0 ? byDay : string.Compare(a.Category, b.Category, StringComparison.Ordinal); });
        GUI.Label(new Rect(x + 220, y + 30, w - 360, 22), "Записей: " + lines.Count + " · день " + currentDay);
        Rect viewport = new Rect(x + 14, y + 58, w - 28, h - 104);
        float contentHeight = Mathf.Max(viewport.height, lines.Count * 43f + 8f);
        financeLedgerScroll = GUI.BeginScrollView(viewport, financeLedgerScroll, new Rect(0, 0, viewport.width - 18, contentHeight));
        if (lines.Count == 0) GUI.Label(new Rect(8, 8, viewport.width - 30, 28), "За выбранный период операций в доступных журналах нет.");
        for (int i = 0; i < lines.Count; i++)
        {
            FinanceLedgerLine line = lines[i]; float rowY = 6 + i * 43f;
            GUI.Label(new Rect(8, rowY, 58, 20), "День " + line.Day);
            GUI.Label(new Rect(72, rowY, 240, 20), line.Category);
            GUI.Label(new Rect(316, rowY, viewport.width - 430, 34), line.Description);
            GUI.Label(new Rect(viewport.width - 112, rowY, 100, 20), (line.IsIncome ? "+" : "−") + line.Amount.ToString("N0") + " ₽");
            GUI.Box(new Rect(6, rowY + 36, viewport.width - 28, 1), GUIContent.none);
        }
        GUI.EndScrollView();
        GUI.Label(new Rect(x + 16, y + h - 39, w - 150, 26), "Включены поставщики, транспорт, продажи, закупки, хранение, содержание предприятий и зарплаты.");
        if (GUI.Button(new Rect(x + w - 112, y + h - 36, 96, 24), "Закрыть")) showFinanceLedger = false;
    }

    private void DrawCityFinancePanel()
    {
        if (!showFinancePanel) return;
        float w = Mathf.Min(430f, Screen.width - 28f);
        float h = 252f;
        float x = Mathf.Max(14f, (Screen.width - w) * 0.5f);
        float y = Mathf.Max(14f, (Screen.height - h) * 0.5f);
        GUI.Box(new Rect(x, y, w, h), "Финансы города");
        long businessRevenue = economySystem != null ? economySystem.TotalBusinessRevenue : 0;
        long businessCosts = economySystem != null ? economySystem.TotalBusinessCosts : 0;
        long payroll = economySystem != null ? economySystem.TotalPaid : 0;
        long companyCash = economySystem != null ? economySystem.GetTotalWorkplaceCash() : 0;
        long operatingResult = businessRevenue - businessCosts;
        GUI.Label(new Rect(x + 16, y + 32, w - 32, 22), "Городская казна: " + money.ToString("N0") + " ₽");
        GUI.Label(new Rect(x + 16, y + 58, w - 32, 22), "Средства предприятий: " + companyCash.ToString("N0") + " ₽");
        GUI.Label(new Rect(x + 16, y + 91, w - 32, 22), "Выручка бизнеса за всё время: " + businessRevenue.ToString("N0") + " ₽");
        GUI.Label(new Rect(x + 16, y + 117, w - 32, 22), "Учтённые расходы бизнеса: " + businessCosts.ToString("N0") + " ₽");
        GUI.Label(new Rect(x + 16, y + 143, w - 32, 22), "Результат бизнеса: " + operatingResult.ToString("N0") + " ₽");
        GUI.Label(new Rect(x + 16, y + 169, w - 32, 22), "Выплачено зарплат: " + payroll.ToString("N0") + " ₽");
        GUI.Label(new Rect(x + 16, y + 198, w - 32, 36), "Показатели накопительные. Казна города и кассы предприятий
разделены и не складываются в один баланс.");
        if (GUI.Button(new Rect(x + 16, y + h - 31, 150, 24), "Финансовый журнал")) showFinanceLedger = true;
        if (GUI.Button(new Rect(x + w - 112, y + h - 31, 96, 24), "Закрыть")) showFinancePanel = false;
    }

    private void DrawSelectedWarehousePanel()
    {
        if (selected == null || warehouseStorageSystem == null) return;
        List<WarehouseRecord> warehouseRecords = new List<WarehouseRecord>();
        warehouseStorageSystem.Export(warehouseRecords);
        WarehouseRecord warehouse = warehouseRecords.Find(r => r != null && r.BuildingId == selected.Id);
        if (warehouse == null) return;
        float x = 350f, y = 126f, w = 370f, h = Mathf.Min(330f, Screen.height - 220f);
        GUI.Box(new Rect(x, y, w, h), "Складской комплекс");
        GUI.Label(new Rect(x + 12, y + 28, w - 24, 22), warehouse.DisplayName);
        GUI.Label(new Rect(x + 12, y + 52, w - 24, 22), "Заполнено: " + warehouseStorageSystem.GetTotalStock(warehouse.Id) + " / " + warehouse.CapacityUnits + " ед.");
        GUI.Label(new Rect(x + 12, y + 76, w - 24, 22), "Свободно: " + warehouseStorageSystem.GetFreeCapacity(warehouse.Id) + " ед. · Доков: " + warehouse.DockCount);
        GUI.Label(new Rect(x + 12, y + 98, w - 24, 20), "Занято доков: " + warehouseStorageSystem.GetActiveDockCount(warehouse.Id) + " · Очередь: " + warehouseStorageSystem.GetQueueCount(warehouse.Id));
        GUI.Label(new Rect(x + 12, y + 119, w - 24, 20), "Расходы сегодня: " + warehouse.LastDailyOperatingCost.ToString("N0") + " ₽ · Всего: " + warehouse.CumulativeOperatingCosts.ToString("N0") + " ₽");
        if (GUI.Button(new Rect(x + 12, y + 122, 160, 27), warehouse.IsOperating ? "Приостановить склад" : "Открыть склад"))
        { warehouse.IsOperating = !warehouse.IsOperating; SaveCity(); status = warehouse.IsOperating ? "Склад возобновил работу." : "Склад приостановлен."; }
        GUI.Label(new Rect(x + 12, y + 158, w - 24, 20), "Остатки по номенклатуре:");
        float rowY = y + 182;
        if (warehouse.Inventory != null)
        {
            foreach (WarehouseInventoryRecord item in warehouse.Inventory)
            {
                if (item == null) continue;
                GUI.Label(new Rect(x + 16, rowY, w - 32, 19), item.ProductId + " — " + item.Units + " ед.");
                rowY += 21;
                if (rowY > y + h - 28) break;
            }
        }
        GUI.Label(new Rect(x + 12, y + h - 25, w - 24, 20), "Состояние: " + warehouse.LastStatus);
    }

    private void DrawProductionLogisticsPanel()
    {
        if (selected == null || !selected.Workplace || productionChainSystem == null || supplyLogisticsSystem == null) return;
        float x = 350f, y = 126f, w = 370f, h = Mathf.Min(1000f, Screen.height - 140f);
        GUI.Box(new Rect(x, y, w, h), "Производство и грузовая логистика");
        ProductionSiteRecord site = FindProductionSiteForBuilding(selected.Id);
        if (site == null)
        {
            GUI.Label(new Rect(x + 12, y + 30, w - 24, 42), "Назначьте выбранному объекту производственный профиль. Площадка привяжется к ближайшему узлу дорожной сети.");
            string[] recipes = { "Хлебопекарня", "Молочное производство", "Мебельная фабрика", "Нефтепереработка", "Стройматериалы" };
            string[] recipeIds = { "bakery_bread", "dairy_milk", "furniture", "fuel_refining", "building_materials" };
            for (int i = 0; i < recipes.Length; i++)
            {
                int row = i;
                if (GUI.Button(new Rect(x + 12, y + 78 + i * 32, w - 24, 27), recipes[row] + (selectedProductionRecipe == row ? "  ✓" : "")))
                {
                    selectedProductionRecipe = row;
                    Vector2Int node = roadNetwork != null ? roadNetwork.NearestNode(selected.Entrance, 14f) : Vector2Int.zero;
                    if (roadNetwork == null || !roadNetwork.Contains(node)) { status = "Для производственной площадки нет доступного дорожного узла."; return; }
                    ProductionSiteRecord created = new ProductionSiteRecord {
                        Id = "production-" + selected.Id, BuildingId = selected.Id, DisplayName = selected.Name,
                        RoadNodeX = node.x, RoadNodeZ = node.y, RecipeId = recipeIds[row],
                        DailyCapacityUnits = 20, StorageCapacityUnits = 500, IsOperating = true,
                        OperatingFunds = 100000, LastStatus = "Ожидание сырья"
                    };
                    if (productionChainSystem.RegisterSite(created)) { SaveCity(); status = "Производственный профиль создан. Узел: " + node.x + ", " + node.y + "."; }
                    else status = "Не удалось зарегистрировать производственную площадку.";
                }
            }
            return;
        }

        ProductionRecipeDefinition recipe = ProductionChainSystem.FindRecipe(site.RecipeId);
        GUI.Label(new Rect(x + 12, y + 27, w - 24, 22), site.DisplayName + " · " + (recipe != null ? recipe.DisplayName : site.RecipeId));
        GUI.Label(new Rect(x + 12, y + 49, w - 24, 20), "Касса: " + site.OperatingFunds.ToString("N0") + " ₽ · Выпуск: " + site.TotalUnitsProduced + " ед.");
        GUI.Label(new Rect(x + 12, y + 69, w - 24, 20), "Статус: " + site.LastStatus);
        GUI.Label(new Rect(x + 12, y + 91, w - 24, 20), "Входящий склад сырья:");
        int rowY = 114;
        if (recipe != null && recipe.Inputs != null)
        {
            for (int i = 0; i < recipe.Inputs.Length; i++)
            {
                ProductionInputRequirement input = recipe.Inputs[i];
                int stock = productionChainSystem.GetInputStock(site.Id, input.ProductId);
                GUI.Label(new Rect(x + 12, y + rowY, 330, 20), input.ProductId + ": " + stock + " ед. на складе предприятия");
                List<SupplierQuoteRecord> quotes = new List<SupplierQuoteRecord>();
                supplyLogisticsSystem.GetSupplierQuotes(site.Id, input.ProductId, 10, quotes);
                if (quotes.Count == 0)
                {
                    GUI.Label(new Rect(x + 18, y + rowY + 19, 330, 18), "Нет доступных предложений на 10 ед.");
                }
                else
                {
                    SupplierQuoteRecord recommended = quotes[0];
                    GUI.Label(new Rect(x + 18, y + rowY + 19, 330, 18), "Лучшее: " + recommended.SupplierDisplayName + " · " + recommended.TotalCost.ToString("N0") + " ₽ · " + recommended.RouteDistanceKm + " км");
                    GUI.enabled = true;
                    if (GUI.Button(new Rect(x + 12, y + rowY + 39, 104, 23), "Автовыбор"))
                    {
                        string chosen; long total; string error;
                        if (supplyLogisticsSystem.TryPlaceBestValueOrderByRoad(site.Id, input.ProductId, 10, out chosen, out total, out error))
                        { SaveCity(); status = "Автовыбор: " + chosen + ". Полная стоимость " + total.ToString("N0") + " ₽."; }
                        else status = error;
                    }
                    int visibleQuotes = Mathf.Min(2, quotes.Count);
                    for (int q = 0; q < visibleQuotes; q++)
                    {
                        SupplierQuoteRecord quote = quotes[q];
                        int quoteIndex = q;
                        string buttonLabel = (quoteIndex == 0 ? "1. " : "2. ") + quote.TotalCost.ToString("N0") + " ₽";
                        if (GUI.Button(new Rect(x + 122 + q * 112, y + rowY + 39, 106, 23), buttonLabel))
                        {
                            string error;
                            if (supplyLogisticsSystem.TryPlaceOrderByRoad(quote.SupplierId, site.Id, input.ProductId, 10, out error))
                            { SaveCity(); status = "Выбран поставщик " + quote.SupplierDisplayName + ". Стоимость " + quote.TotalCost.ToString("N0") + " ₽."; }
                            else status = error;
                        }
                    }
                    GUI.Label(new Rect(x + 18, y + rowY + 63, 420, 17), "Надёжность: " + recommended.ReliabilityScore + "/100" + (recommended.IsProvisional ? " · предварительный" : "") +
                        " · мин. партия " + recommended.MinimumOrderUnits + " · скидка " + (1000 - recommended.ContractDiscountPermille) / 10f + "% · доставка " + recommended.FreightCostPerKm + " ₽/км");
                }
                rowY += quotes.Count > 0 ? 83 : 44;
            }
        }
        GUI.Label(new Rect(x + 12, y + rowY + 3, w - 24, 20), "Выход: " + (recipe != null ? recipe.OutputProductId : "—") + " · " + (recipe != null ? productionChainSystem.GetOutputStock(site.Id, recipe.OutputProductId) : 0) + " ед.");
        GUI.Label(new Rect(x + 12, y + rowY + 26, w - 24, 20), "Активные рейсы: " + supplyLogisticsSystem.ActiveVehicleCount + " · Ожидают: " + supplyLogisticsSystem.PendingShipmentCount);
        List<SupplyOrderRecord> pendingInspections = new List<SupplyOrderRecord>();
        supplyLogisticsSystem.GetPendingInspectionOrders(site.Id, pendingInspections);
        if (pendingInspections.Count > 0)
        {
            int inspectY = rowY + 52;
            GUI.Label(new Rect(x + 12, y + inspectY, w - 24, 20), "Приёмка прибывших поставок:");
            inspectY += 22;
            for (int pi = 0; pi < pendingInspections.Count; pi++)
            {
                SupplyOrderRecord pending = pendingInspections[pi];
                string receivedText; if (!supplierReceivedEdits.TryGetValue(pending.Id, out receivedText)) receivedText = pending.Units.ToString();
                string defectText; if (!supplierDefectEdits.TryGetValue(pending.Id, out defectText)) defectText = "0";
                string noteText; if (!supplierInspectionNotes.TryGetValue(pending.Id, out noteText)) noteText = string.Empty;
                GUI.Label(new Rect(x + 12, y + inspectY, w - 24, 18), pending.ProductId + " · заказано " + pending.Units + " · прибыло в день " + pending.ActualArrivalDay);
                supplierReceivedEdits[pending.Id] = GUI.TextField(new Rect(x + 12, y + inspectY + 19, 62, 22), receivedText);
                supplierDefectEdits[pending.Id] = GUI.TextField(new Rect(x + 80, y + inspectY + 19, 62, 22), defectText);
                supplierInspectionNotes[pending.Id] = GUI.TextField(new Rect(x + 148, y + inspectY + 19, 150, 22), noteText);
                GUI.Label(new Rect(x + 12, y + inspectY + 42, 62, 16), "Получено");
                GUI.Label(new Rect(x + 80, y + inspectY + 42, 62, 16), "Брак");
                GUI.Label(new Rect(x + 148, y + inspectY + 42, 150, 16), "Примечание");
                GUI.enabled = true;
                if (GUI.Button(new Rect(x + 306, y + inspectY + 19, 110, 23), "Принять груз"))
                {
                    int received, defective;
                    if (!int.TryParse(supplierReceivedEdits[pending.Id], out received) || !int.TryParse(supplierDefectEdits[pending.Id], out defective)) status = "Введите целые количества полученного товара и брака.";
                    else
                    {
                        string inspectionError;
                        if (supplyLogisticsSystem.TryInspectAndAcceptOrder(pending.Id, received, defective, supplierInspectionNotes[pending.Id], out inspectionError)) { SaveCity(); status = "Приёмка завершена. Принято " + (received - defective) + " ед.; брак " + defective + "; недостача " + (pending.Units - received) + "."; }
                        else status = inspectionError;
                    }
                }
                inspectY += 66;
            }
        }
        List<SupplierRecord> supplierRecords = new List<SupplierRecord>();
        supplyLogisticsSystem.GetSupplierContracts(supplierRecords);
        int ratingY = rowY + 54 + (pendingInspections.Count > 0 ? 22 + pendingInspections.Count * 66 : 0);
        GUI.Label(new Rect(x + 12, y + ratingY, w - 24, 20), "Промышленный рейтинг поставщиков:");
        ratingY += 21;
        for (int si = 0; si < Mathf.Min(3, supplierRecords.Count); si++)
        {
            SupplierRecord supplierRecord = supplierRecords[si];
            string ratingLabel = supplierRecord.DisplayName + " · пока нет месячного снимка";
            if (supplierRecord.RatingHistory != null && supplierRecord.RatingHistory.Count > 0)
            {
                SupplierRatingSnapshotRecord latestRating = supplierRecord.RatingHistory[supplierRecord.RatingHistory.Count - 1];
                ratingLabel = supplierRecord.DisplayName + " · " + latestRating.Score + "/100 · период " + latestRating.PeriodNumber + (latestRating.IsProvisional ? " · предварительный" : "");
            }
            GUI.Label(new Rect(x + 18, y + ratingY, w - 30, 18), ratingLabel);
            ratingY += 18;
        }
        ratingY += 6;
        GUI.Label(new Rect(x + 12, y + ratingY, w - 24, 20), "УПРАВЛЕНИЕ ДОГОВОРАМИ · условия и продление");
        ratingY += 22;
        for (int si = 0; si < supplierRecords.Count; si++)
        {
            SupplierRecord contractSupplier = supplierRecords[si];
            if (contractSupplier == null) continue;
            int today = gameClock != null ? gameClock.Day : 1;
            bool activeContract = today >= contractSupplier.ContractStartDay &&
                (contractSupplier.ContractEndDay <= 0 || today <= contractSupplier.ContractEndDay);
            string expiry = contractSupplier.ContractEndDay <= 0 ? "бессрочно" : "до дня " + contractSupplier.ContractEndDay;
            GUI.Label(new Rect(x + 12, y + ratingY, w - 24, 18), contractSupplier.DisplayName + " · " + (activeContract ? "действует" : "не действует") + " · " + expiry);
            GUI.Label(new Rect(x + 18, y + ratingY + 18, w - 24, 17), "Мин. партия " + contractSupplier.MinimumOrderUnits + " · скидка " + (1000 - contractSupplier.ContractDiscountPermille) / 10f + "% · фрахт " + contractSupplier.FreightCostPerKm + " ₽/км");
            int buttonY = ratingY + 36;
            if (GUI.Button(new Rect(x + 12, y + buttonY, 72, 22), "Мин. −"))
            { string e; if (supplyLogisticsSystem.TryAdjustSupplierContract(contractSupplier.Id, -1, 0, 0, 0, out e)) { SaveCity(); status = "Минимальная партия уменьшена: " + contractSupplier.DisplayName; } else status = e; }
            if (GUI.Button(new Rect(x + 88, y + buttonY, 72, 22), "Мин. +"))
            { string e; if (supplyLogisticsSystem.TryAdjustSupplierContract(contractSupplier.Id, 1, 0, 0, 0, out e)) { SaveCity(); status = "Минимальная партия увеличена: " + contractSupplier.DisplayName; } else status = e; }
            if (GUI.Button(new Rect(x + 164, y + buttonY, 72, 22), "Скидка +"))
            { string e; if (supplyLogisticsSystem.TryAdjustSupplierContract(contractSupplier.Id, 0, -10, 0, 0, out e)) { SaveCity(); status = "Скидка согласована: " + contractSupplier.DisplayName; } else status = e; }
            GUI.enabled = contractSupplier.ContractEndDay > 0;
            if (GUI.Button(new Rect(x + 240, y + buttonY, 112, 22), contractSupplier.ContractEndDay > 0 ? "Продлить 30 дн." : "Бессрочно"))
            { string e; if (supplyLogisticsSystem.TryAdjustSupplierContract(contractSupplier.Id, 0, 0, 0, 30, out e)) { SaveCity(); status = "Договор продлён: " + contractSupplier.DisplayName; } else status = e; }
            GUI.enabled = true;
            if (GUI.Button(new Rect(x + 12, y + buttonY + 24, 164, 21), "Фрахт −1 ₽/км"))
            { string e; if (supplyLogisticsSystem.TryAdjustSupplierContract(contractSupplier.Id, 0, 0, -1, 0, out e)) { SaveCity(); status = "Тариф перевозки снижен: " + contractSupplier.DisplayName; } else status = e; }
            if (GUI.Button(new Rect(x + 184, y + buttonY + 24, 164, 21), "Фрахт +1 ₽/км"))
            { string e; if (supplyLogisticsSystem.TryAdjustSupplierContract(contractSupplier.Id, 0, 0, 1, 0, out e)) { SaveCity(); status = "Тариф перевозки повышен: " + contractSupplier.DisplayName; } else status = e; }
            ratingY += 87;
            if (contractSupplier.Claims == null) contractSupplier.Claims = new List<SupplierClaimRecord>();
            if (contractSupplier.Negotiations == null) contractSupplier.Negotiations = new List<SupplierNegotiationRecord>();
            GUI.Label(new Rect(x + 18, y + ratingY, w - 30, 17), "Претензии: " + contractSupplier.Claims.Count + " · переговоры: " + contractSupplier.Negotiations.Count);
            if (contractSupplier.FinancialHistory == null) contractSupplier.FinancialHistory = new List<SupplierFinancialTransactionRecord>();
            SupplierSettlementSummaryRecord settlement = new SupplierSettlementSummaryRecord();
            supplyLogisticsSystem.GetSupplierSettlementSummary(contractSupplier.Id, settlement);
            GUI.Label(new Rect(x + 18, y + ratingY + 17, w - 30, 17), "Оплачено " + settlement.TotalPaid.ToString("N0") + " ₽ · возвраты " + settlement.TotalReturned.ToString("N0") + " ₽ · компенсации " + settlement.TotalCompensation.ToString("N0") + " ₽");
            GUI.Label(new Rect(x + 18, y + ratingY + 34, w - 30, 17), "Чистые расходы " + settlement.NetPaid.ToString("N0") + " ₽ · не сверено операций: " + settlement.UnreconciledTransactionCount + " · сальдо " + settlement.UnreconciledNet.ToString("N0") + " ₽");
            if (GUI.Button(new Rect(x + 18, y + ratingY + 52, 170, 20), "Сверить расчёты"))
            { string batch, e; if (supplyLogisticsSystem.TryReconcileSupplierAccount(contractSupplier.Id, out batch, out e)) { SaveCity(); status = "Сверка выполнена · " + batch; } else status = e; }
            ratingY += 75;
            if (contractSupplier.Claims.Count > 0)
            {
                SupplierClaimRecord claim = contractSupplier.Claims[contractSupplier.Claims.Count - 1];
                GUI.Label(new Rect(x + 18, y + ratingY, w - 30, 17), claim.Id + " · " + claim.ClaimType + " · " + claim.Status);
                GUI.Label(new Rect(x + 18, y + ratingY + 17, w - 30, 17), "Требование " + claim.RequestedAdditionalCompensation + " ₽ · одобрено " + claim.ApprovedAdditionalCompensation + " ₽" + (claim.IsSettled ? " · выплачено " + claim.SettledAmount + " ₽" : string.Empty));
                if (claim.Status == "На рассмотрении" && GUI.Button(new Rect(x + 18, y + ratingY + 34, 112, 20), "Ответ поставщика"))
                { string e; if (supplyLogisticsSystem.TryRespondToSupplierClaim(contractSupplier.Id, claim.Id, out e)) { SaveCity(); status = "Получен ответ по претензии " + claim.Id; } else status = e; }
                if (claim.Status == "Признана" && GUI.Button(new Rect(x + 136, y + ratingY + 34, 112, 20), "Урегулировать"))
                { string e; if (supplyLogisticsSystem.TrySettleSupplierClaim(contractSupplier.Id, claim.Id, out e)) { SaveCity(); status = "Претензия урегулирована без повторного возврата товара."; } else status = e; }
                ratingY += 58;
            }
            if (GUI.Button(new Rect(x + 18, y + ratingY, 150, 21), "Переговоры: +2% скидки"))
            {
                string negotiationId, e; int requestedDiscount = Mathf.Max(1, contractSupplier.ContractDiscountPermille - 20);
                if (supplyLogisticsSystem.TryStartSupplierNegotiation(contractSupplier.Id, requestedDiscount, contractSupplier.FreightCostPerKm, contractSupplier.MinimumOrderUnits, 90, "Пересмотр условий сотрудничества", out negotiationId, out e)) { SaveCity(); status = "Ответ поставщика: " + contractSupplier.Negotiations[contractSupplier.Negotiations.Count - 1].Status; } else status = e;
            }
            if (contractSupplier.Negotiations.Count > 0)
            {
                SupplierNegotiationRecord negotiation = contractSupplier.Negotiations[contractSupplier.Negotiations.Count - 1];
                GUI.Label(new Rect(x + 176, y + ratingY, w - 190, 20), negotiation.Status);
                if ((negotiation.Status == "Встречное предложение" || negotiation.Status == "Предварительное предложение" || negotiation.Status == "Принято поставщиком") && GUI.Button(new Rect(x + 176, y + ratingY + 20, 132, 20), "Принять условия"))
                { string e; if (supplyLogisticsSystem.TryAcceptSupplierCounterOffer(contractSupplier.Id, negotiation.Id, out e)) { SaveCity(); status = "Условия переговоров применены."; } else status = e; }
            }
            ratingY += 44;
        }
        GUI.Label(new Rect(x + 12, y + ratingY, w - 24, 20), "КАЛЕНДАРЬ ПЛАТЕЖЕЙ ПОСТАВЩИКАМ");
        ratingY += 22;
        List<SupplierPaymentScheduleRecord> paymentSchedule = new List<SupplierPaymentScheduleRecord>();
        supplyLogisticsSystem.GetAllSupplierPaymentSchedule(paymentSchedule);
        if (paymentSchedule.Count == 0) { GUI.Label(new Rect(x + 18, y + ratingY, w - 30, 18), "Открытых платежей нет."); ratingY += 20; }
        else
        {
            for (int pi = 0; pi < paymentSchedule.Count; pi++)
            {
                SupplierPaymentScheduleRecord payment = paymentSchedule[pi];
                string paymentState = payment.IsOverdue ? "ПРОСРОЧЕНО: " + payment.OverdueDays + " дн." : "срок: день " + payment.DueDay;
                GUI.Label(new Rect(x + 18, y + ratingY, w - 30, 18), payment.SupplierName + " · заказ " + payment.OrderId + " · " + paymentState);
                GUI.Label(new Rect(x + 24, y + ratingY + 17, w - 38, 17), "Основной долг " + payment.PrincipalOutstanding.ToString("N0") + " ₽ · штраф " + payment.PenaltyOutstanding.ToString("N0") + " ₽ · всего " + payment.TotalOutstanding.ToString("N0") + " ₽");
                if (GUI.Button(new Rect(x + 18, y + ratingY + 35, 150, 20), "Погасить полностью"))
                { string e; if (supplyLogisticsSystem.TryPaySupplierOrderBalance(payment.SupplierId, payment.OrderId, payment.TotalOutstanding, out e)) { SaveCity(); status = "Платёж по заказу " + payment.OrderId + " погашен."; } else status = e; }
                ratingY += 59;
            }
        }
        List<SupplyOrderRecord> claimOrders = new List<SupplyOrderRecord>();
        List<CargoVehicleRecord> claimVehicles = new List<CargoVehicleRecord>();
        supplyLogisticsSystem.Export(null, claimOrders, claimVehicles);
        int claimButtons = 0;
        for (int oi = claimOrders.Count - 1; oi >= 0 && claimButtons < 2; oi--)
        {
            SupplyOrderRecord claimOrder = claimOrders[oi];
            if (claimOrder == null || !claimOrder.IsQualityInspected || (claimOrder.ShortUnits <= 0 && claimOrder.DefectiveUnits <= 0)) continue;
            SupplierRecord owner = supplierRecords.Find(v => v != null && v.Id == claimOrder.SupplierId);
            if (owner == null || (owner.Claims != null && owner.Claims.Exists(c => c != null && c.OrderId == claimOrder.Id))) continue;
            GUI.Label(new Rect(x + 18, y + ratingY, w - 30, 18), "Заказ " + claimOrder.Id + " · брак " + claimOrder.DefectiveUnits + " · недостача " + claimOrder.ShortUnits);
            if (GUI.Button(new Rect(x + 18, y + ratingY + 18, 140, 20), "Оформить претензию"))
            { string e; if (supplyLogisticsSystem.TrySubmitSupplierClaim(claimOrder.Id, 0, "Требование по качеству/комплектности поставки", out e)) { SaveCity(); status = "Претензия зарегистрирована по заказу " + claimOrder.Id; } else status = e; }
            ratingY += 42; claimButtons++;
        }
        List<WarehouseRecord> warehouseRecords = new List<WarehouseRecord>();
        if (warehouseStorageSystem != null) warehouseStorageSystem.Export(warehouseRecords);
        WarehouseRecord warehouse = warehouseRecords.Find(r => r != null && r.WarehouseTypeId == "distribution");
        float warehouseY = y + ratingY + 8;
        if (warehouse != null)
        {
            GUI.Label(new Rect(x + 12, warehouseY, w - 24, 20), warehouse.DisplayName + " · занято " + warehouseStorageSystem.GetTotalStock(warehouse.Id) + "/" + warehouse.CapacityUnits + " ед.");
            if (recipe != null)
            {
                string outputId = recipe.OutputProductId;
                int outputStock = productionChainSystem.GetOutputStock(site.Id, outputId);
                GUI.Label(new Rect(x + 12, warehouseY + 20, w - 24, 18), "На складе " + outputId + ": " + warehouseStorageSystem.GetStock(warehouse.Id, outputId) + " ед.");
                GUI.enabled = outputStock >= 10 && warehouseStorageSystem.GetFreeCapacity(warehouse.Id) >= 10;
                if (GUI.Button(new Rect(x + 12, warehouseY + 40, 165, 24), "Отправить 10 ед. на склад"))
                {
                    string transferError;
                    if (warehouseStorageSystem.TryStoreProductionOutput(warehouse.Id, site.Id, outputId, 10, out transferError)) { SaveCity(); status = "Готовая продукция передана на центральный склад."; }
                    else status = transferError;
                }
                GUI.enabled = true;
                if (recipe.Inputs != null && recipe.Inputs.Length > 0)
                {
                    string inputId = recipe.Inputs[0].ProductId;
                    int availableInput = warehouseStorageSystem.GetStock(warehouse.Id, inputId);
                    GUI.Label(new Rect(x + 12, warehouseY + 65, w - 24, 18), "Доступно сырья " + inputId + ": " + availableInput + " ед.");
                    GUI.enabled = availableInput >= 10;
                    if (GUI.Button(new Rect(x + 190, warehouseY + 40, 165, 24), "Выдать 10 ед. сырья"))
                    {
                        string supplyError;
                        if (warehouseStorageSystem.TrySupplyProduction(warehouse.Id, site.Id, inputId, 10, out supplyError)) { SaveCity(); status = "Сырьё выдано с центрального склада производству."; }
                        else status = supplyError;
                    }
                    GUI.enabled = true;
                }
            }
        }
        if (GUI.Button(new Rect(x + 12, warehouseY + 91, w - 24, 26), site.IsOperating ? "Приостановить производство" : "Возобновить производство"))
        { site.IsOperating = !site.IsOperating; SaveCity(); status = site.IsOperating ? "Производство возобновлено." : "Производство приостановлено."; }
    }

    private ProductionSiteRecord FindProductionSiteForBuilding(string buildingId)
    {
        if (productionChainSystem == null) return null;
        List<ProductionSiteRecord> records = new List<ProductionSiteRecord>();
        productionChainSystem.Export(records);
        return records.Find(r => r != null && r.BuildingId == buildingId);
    }

    private static string GetSupplierForProduct(string productId)
    {
        if (productId == "flour" || productId == "water" || productId == "raw_milk" || productId == "packaging") return "supplier_food";
        if (productId == "timber" || productId == "metal" || productId == "cement" || productId == "sand") return "supplier_industry";
        if (productId == "crude_oil" || productId == "electricity") return "supplier_energy";
        return null;
    }

    private void DrawWarehouseDispatcherPanel()
    {
        if (!showWarehouseDispatcher || warehouseStorageSystem == null) return;
        List<WarehouseRecord> warehouses = new List<WarehouseRecord>();
        warehouseStorageSystem.Export(warehouses);
        warehouses.RemoveAll(w => w == null);
        if (warehouses.Count < 2)
        {
            GUI.Box(new Rect(740, 126, 360, 150), "Грузовая диспетчерская");
            GUI.Label(new Rect(754, 158, 330, 52), "Для межскладских перевозок постройте минимум два склада. Сейчас зарегистрировано: " + warehouses.Count + ".");
            return;
        }
        dispatcherSourceIndex = Mathf.Clamp(dispatcherSourceIndex, 0, warehouses.Count - 1);
        dispatcherDestinationIndex = Mathf.Clamp(dispatcherDestinationIndex, 0, warehouses.Count - 1);
        float x = Mathf.Min(740f, Screen.width - 372f), y = 205f, w = 360f, h = 340f;
        GUI.Box(new Rect(x, y, w, h), "Межскладская диспетчерская");
        GUI.Label(new Rect(x + 12, y + 28, 100, 22), "Отправитель:");
        if (GUI.Button(new Rect(x + 112, y + 27, 236, 25), warehouses[dispatcherSourceIndex].DisplayName + "  ◀▶"))
            dispatcherSourceIndex = (dispatcherSourceIndex + 1) % warehouses.Count;
        GUI.Label(new Rect(x + 12, y + 59, 100, 22), "Получатель:");
        if (GUI.Button(new Rect(x + 112, y + 58, 236, 25), warehouses[dispatcherDestinationIndex].DisplayName + "  ◀▶"))
            dispatcherDestinationIndex = (dispatcherDestinationIndex + 1) % warehouses.Count;
        WarehouseRecord source = warehouses[dispatcherSourceIndex];
        WarehouseRecord destination = warehouses[dispatcherDestinationIndex];
        GUI.Label(new Rect(x + 12, y + 91, w - 24, 20), "Товар (ID):");
        dispatcherProductId = GUI.TextField(new Rect(x + 112, y + 89, 236, 25), dispatcherProductId);
        GUI.Label(new Rect(x + 12, y + 122, w - 24, 20), "Количество:");
        dispatcherUnits = GUI.TextField(new Rect(x + 112, y + 120, 236, 25), dispatcherUnits);
        GUI.Label(new Rect(x + 12, y + 154, w - 24, 38), "Отправитель: " + warehouseStorageSystem.GetStock(source.Id, dispatcherProductId) + " ед. · свободно у получателя: " + warehouseStorageSystem.GetFreeCapacity(destination.Id));
        GUI.enabled = source.Id != destination.Id && source.IsOperating && destination.IsOperating;
        if (GUI.Button(new Rect(x + 12, y + 198, w - 24, 30), "Создать межскладский рейс"))
        {
            int units;
            if (!int.TryParse(dispatcherUnits, out units) || units <= 0 || string.IsNullOrEmpty(dispatcherProductId)) status = "Укажите корректный товар и положительное количество.";
            else
            {
                string error;
                string vehicleId = Guid.NewGuid().ToString("N");
                if (warehouseStorageSystem.TryDispatchWarehouseTransfer(source.Id, destination.Id, dispatcherProductId.Trim(), units, vehicleId, gameClock != null ? gameClock.Day : 1, out error))
                { SaveCity(); status = "Рейс " + vehicleId.Substring(0, 8) + " поставлен на погрузку. Проверен дорожный маршрут."; }
                else status = error;
            }
        }
        GUI.enabled = true;
        GUI.Label(new Rect(x + 12, y + 235, w - 24, 42), "Автопарк: " + (fleetManagementSystem != null ? fleetManagementSystem.AvailableCount + "/" + fleetManagementSystem.VehicleCount : "не подключён") + " свободно. Подбор машины выполняется по грузоподъёмности.");
        GUI.Label(new Rect(x + 12, y + 281, w - 24, 22), "Складов: " + warehouses.Count + " · FIFO · расходы рейса учитываются по завершении");
    }

    private void CreateStoreReturnFromUi(WorkplaceRecord store, StoreInventoryRecord item, string unitsText, string reason, string settlement)
    {
        int units;
        if (!int.TryParse(unitsText, out units) || units <= 0) { status = "Введите корректное количество для возврата."; return; }
        if (warehouseStorageSystem == null) { status = "Система складской логистики не подключена."; return; }
        string error;
        if (warehouseStorageSystem.TryCreateStoreReturn(store, item.ProductId, units, reason, settlement, gameClock != null ? gameClock.Day : 0, out error))
        { SaveCity(); status = "Возврат оформлен: " + item.DisplayName + ", " + units + " шт. Товар зарезервирован."; }
        else status = error;
    }

    private void DrawSelectedStorePanel()
    {
        if (selected == null || !selected.Workplace || workScheduleSystem == null) return;
        WorkplaceRecord store = FindWorkplaceRecord(selected.Id);
        if (store == null) return;

        float panelWidth = 390f;
        float panelHeight = Mathf.Min(430f, Mathf.Max(270f, Screen.height * 0.52f));
        float x = Screen.width - panelWidth - 12f;
        float y = Screen.height - panelHeight - 88f;
        GUI.Box(new Rect(x, y, panelWidth, panelHeight), "Карточка торгового объекта");
        GUI.Label(new Rect(x + 12, y + 25, panelWidth - 24, 22), store.DisplayName + " · " + store.StoreTypeId);
        GUI.Label(new Rect(x + 12, y + 47, panelWidth - 24, 20), "Касса: " + store.OperatingBalance.ToString("N0") + " ₽   Персонал: " + store.StaffCount + "/" + store.StaffCapacity);
        GUI.Label(new Rect(x + 12, y + 67, panelWidth - 24, 20), "Сегодня: " + store.LastDailyRevenue.ToString("N0") + " ₽   Продано: " + store.LastDailyUnitsSold + " ед.");

        string[] tabs = { "Обзор", "Ассортимент", "Закупки", "Финансы", "Возвраты" };
        float tabWidth = (panelWidth - 24f) / tabs.Length;
        for (int i = 0; i < tabs.Length; i++)
            if (GUI.Toggle(new Rect(x + 12 + i * tabWidth, y + 91, tabWidth - 3, 25), selectedStoreTab == i, tabs[i], GUI.skin.button)) selectedStoreTab = i;

        Rect viewRect = new Rect(x + 10, y + 122, panelWidth - 20, panelHeight - 132);
        int skuCount = store.Inventory != null ? store.Inventory.Count : 0;
        int pendingReceiptCount = 0;
        if (selectedStoreTab == 2 && store.PendingDeliveries != null)
            for (int receiptIndex = 0; receiptIndex < store.PendingDeliveries.Count; receiptIndex++)
                if (store.PendingDeliveries[receiptIndex] != null && !store.PendingDeliveries[receiptIndex].IsAccepted && !store.PendingDeliveries[receiptIndex].IsCancelled) pendingReceiptCount++;
        float contentHeight = selectedStoreTab == 1 ? Mathf.Max(viewRect.height, 12 + skuCount * 76f) :
            (selectedStoreTab == 2 ? Mathf.Max(viewRect.height, 120 + skuCount * 62f + pendingReceiptCount * 100f) :
             selectedStoreTab == 3 ? Mathf.Max(viewRect.height, 430 + (store.PendingDeliveries != null ? Math.Min(20, store.PendingDeliveries.Count) : 0) * 190f) :
             selectedStoreTab == 4 ? Mathf.Max(viewRect.height, 390 + skuCount * 112f + (store.SupplierReturns != null ? store.SupplierReturns.Count : 0) * 104f + (store.QualityInspectionHistory != null ? Math.Min(12, store.QualityInspectionHistory.Count) * 19f : 0) + (store.SupplierRatingHistory != null ? Math.Min(6, store.SupplierRatingHistory.Count) * 18f : 0) + (store.SupplierRatingHistory != null ? store.SupplierRatingHistory.Count * 58f : 0)) :
             Mathf.Max(viewRect.height, 360f));
        storePanelScroll = GUI.BeginScrollView(viewRect, storePanelScroll,
            new Rect(0, 0, viewRect.width - 18, contentHeight));
        float cy = 6f;

        if (selectedStoreTab == 0)
        {
            GUI.Label(new Rect(4, cy, 340, 22), "Состояние: " + (store.IsOpen ? "работает" : "закрыт")); cy += 28;
            if (GUI.Button(new Rect(4, cy, 160, 28), store.IsOpen ? "Закрыть магазин" : "Открыть магазин"))
            { store.IsOpen = !store.IsOpen; SaveCity(); status = store.DisplayName + (store.IsOpen ? " открыт." : " закрыт."); } cy += 36;
            if (GUI.Button(new Rect(4, cy, 200, 28), "Внести в кассу 10 000 ₽"))
            { if (economySystem != null && economySystem.TryFundWorkplace(store.BuildingId, 10000)) { SaveCity(); status = "В кассу внесено 10 000 ₽."; } } cy += 38;
            GUI.Label(new Rect(4, cy, 340, 22), "Товарных позиций: " + (store.Inventory != null ? store.Inventory.Count : 0)); cy += 24;
            GUI.Label(new Rect(4, cy, 340, 22), "Накопленная выручка: " + store.CumulativeRevenue.ToString("N0") + " ₽"); cy += 24;
            GUI.Label(new Rect(4, cy, 340, 22), "Накопленные расходы: " + store.CumulativeOperatingCosts.ToString("N0") + " ₽"); cy += 24;
            GUI.Label(new Rect(4, cy, 340, 22), "Всего продано: " + store.TotalUnitsSold + " ед.");
        }
        else if (selectedStoreTab == 1)
        {
            if (store.Inventory != null)
            {
                for (int i = 0; i < store.Inventory.Count; i++)
                {
                    StoreInventoryRecord item = store.Inventory[i]; if (item == null) continue;
                    float rowY = cy + i * 76f;
                    GUI.Label(new Rect(4, rowY, 340, 20), item.DisplayName + "  ·  " + item.CategoryId);
                    GUI.Label(new Rect(4, rowY + 21, 180, 18), "Остаток " + item.StockUnits + " / " + item.TargetStockUnits);
                    string editKey = store.BuildingId + ":" + item.ProductId; string editValue;
                    if (!storePriceEdits.TryGetValue(editKey, out editValue) || GUI.GetNameOfFocusedControl() != "price_" + editKey) editValue = item.RetailPrice.ToString();
                    GUI.SetNextControlName("price_" + editKey);
                    storePriceEdits[editKey] = GUI.TextField(new Rect(190, rowY + 20, 82, 22), editValue);
                    if (GUI.Button(new Rect(278, rowY + 20, 78, 22), "Сохранить"))
                    {
                        long parsed;
                        if (long.TryParse(storePriceEdits[editKey], out parsed) && parsed > item.WholesalePrice)
                        { item.RetailPrice = parsed; SaveCity(); status = "Цена товара «" + item.DisplayName + "» обновлена."; }
                        else status = "Розничная цена должна быть выше закупочной: " + item.WholesalePrice.ToString("N0") + " ₽.";
                    }
                    GUI.Label(new Rect(4, rowY + 43, 180, 18), "Закупка: " + item.WholesalePrice.ToString("N0") + " ₽");
                }
                contentHeight = Mathf.Max(viewRect.height, 12 + store.Inventory.Count * 76f);
            }
        }
        else if (selectedStoreTab == 2)
        {
            GUI.Label(new Rect(4, cy, 340, 22), "Заказы поставщикам за счёт кассы магазина"); cy += 24;
            GUI.Label(new Rect(4, cy, 340, 20), "Ожидают доставки: " + CityEconomySystem.CountPendingDeliveries(store)); cy += 24;
            if (store.Inventory != null)
            {
                for (int i = 0; i < store.Inventory.Count; i++)
                {
                    StoreInventoryRecord item = store.Inventory[i]; if (item == null) continue;
                    float rowY = cy + i * 62f;
                    GUI.Label(new Rect(4, rowY, 340, 20), item.DisplayName + " · " + item.StockUnits + " шт.");
                    GUI.Label(new Rect(4, rowY + 21, 120, 18), "Цель: " + item.TargetStockUnits);
                    if (GUI.Button(new Rect(128, rowY + 18, 62, 24), "+10")) { item.TargetStockUnits = Mathf.Min(10000, item.TargetStockUnits + 10); SaveCity(); }
                    if (GUI.Button(new Rect(194, rowY + 18, 62, 24), "+50")) { item.TargetStockUnits = Mathf.Min(10000, item.TargetStockUnits + 50); SaveCity(); }
                    if (GUI.Button(new Rect(260, rowY + 18, 96, 24), "Заказать 1"))
                    {
                        if (economySystem != null && economySystem.TryOrderStock(store.BuildingId, item.ProductId, 1)) status = "Заказ поставщику принят: " + item.DisplayName + ".";
                        else status = "Недостаточно средств или товар не найден.";
                        SaveCity();
                    }
                }
                contentHeight = Mathf.Max(viewRect.height, 12 + store.Inventory.Count * 62f);
            }
            float deliveryY = cy + 54 + (store.Inventory != null ? store.Inventory.Count * 62f : 0f) + 12f;
            GUI.Label(new Rect(4, deliveryY, 350, 22), "Приёмка розничных поставок"); deliveryY += 26;
            if (store.PendingDeliveries != null)
            {
                for (int d = 0; d < store.PendingDeliveries.Count; d++)
                {
                    StoreDeliveryRecord delivery = store.PendingDeliveries[d];
                    if (delivery == null || delivery.IsAccepted || delivery.IsCancelled) continue;
                    string receiptKey = store.BuildingId + ":" + delivery.Id;
                    GUI.Label(new Rect(4, deliveryY, 350, 20), delivery.ProductDisplayName + " · накладная " + delivery.Units + " шт.");
                    GUI.Label(new Rect(4, deliveryY + 19, 350, 20), "Статус: " + delivery.LogisticsStatus + " · поставщик: " + delivery.SupplierDisplayName);
                    if (delivery.LogisticsStatus == "Ожидает приёмки")
                    {
                        string receivedText, damagedText, reasonText;
                        if (!storeReceiptReceivedEdits.TryGetValue(receiptKey, out receivedText)) receivedText = delivery.Units.ToString();
                        if (!storeReceiptDamagedEdits.TryGetValue(receiptKey, out damagedText)) damagedText = "0";
                        if (!storeReceiptReasonEdits.TryGetValue(receiptKey, out reasonText)) reasonText = "";
                        GUI.Label(new Rect(4, deliveryY + 43, 94, 18), "Получено:");
                        storeReceiptReceivedEdits[receiptKey] = GUI.TextField(new Rect(96, deliveryY + 41, 58, 22), receivedText);
                        GUI.Label(new Rect(164, deliveryY + 43, 82, 18), "Повреждено:");
                        storeReceiptDamagedEdits[receiptKey] = GUI.TextField(new Rect(246, deliveryY + 41, 58, 22), damagedText);
                        GUI.Label(new Rect(4, deliveryY + 68, 60, 18), "Причина:");
                        storeReceiptReasonEdits[receiptKey] = GUI.TextField(new Rect(66, deliveryY + 66, 238, 22), reasonText);
                        if (GUI.Button(new Rect(310, deliveryY + 41, 76, 47), "Принять"))
                        {
                            int received, damaged;
                            if (!int.TryParse(storeReceiptReceivedEdits[receiptKey], out received) ||
                                !int.TryParse(storeReceiptDamagedEdits[receiptKey], out damaged) ||
                                received < 0 || received > delivery.Units || damaged < 0 || damaged > received)
                            {
                                status = "Проверьте приёмку: получено от 0 до " + delivery.Units + ", повреждено от 0 до полученного.";
                            }
                            else
                            {
                                delivery.HasReportedReceivedUnits = true;
                                delivery.ReportedReceivedUnits = received;
                                delivery.HasReportedDamagedUnits = true;
                                delivery.ReportedDamagedUnits = damaged;
                                delivery.DamageReason = storeReceiptReasonEdits[receiptKey];
                                delivery.LogisticsStatus = "В пути в магазин";
                                delivery.CurrentPathIndex = Math.Max(0, delivery.RoadPath.Count - 2);
                                delivery.MinutesOnCurrentSegment = 0;
                                SaveCity();
                                status = "Акт приёмки сохранён. Поступление товара будет проведено логистикой.";
                            }
                        }
                        deliveryY += 96;
                    }
                    else deliveryY += 46;
                }
            }
            GUI.Label(new Rect(4, deliveryY + 4, 350, 36), "Автопополнение также оформляет заказы с учётом срока доставки.");
            contentHeight = Mathf.Max(contentHeight, deliveryY + 48);
        }
        else if (selectedStoreTab == 4)
        {
            GUI.Label(new Rect(4, cy, 350, 20), "АНАЛИТИКА КАЧЕСТВА ПОСТАВЩИКОВ"); cy += 23;
            int totalReturnedUnits = 0, qualityReturnedUnits = 0;
            long totalReturnValue = 0;
            Dictionary<string, int> supplierReturnUnits = new Dictionary<string, int>();
            Dictionary<string, int> defectCounts = new Dictionary<string, int>();
            if (store.SupplierReturns != null)
            {
                for (int a = 0; a < store.SupplierReturns.Count; a++)
                {
                    StoreReturnRecord record = store.SupplierReturns[a]; if (record == null || record.IsCancelled) continue;
                    totalReturnedUnits += Math.Max(0, record.Units);
                    totalReturnValue += Math.Max(0L, record.ReturnValue);
                    if (record.IsQualityReturn || record.Reason == "Брак" || record.Reason == "Истёк срок годности") qualityReturnedUnits += Math.Max(0, record.Units);
                    string supplierName = string.IsNullOrEmpty(record.SupplierDisplayName) ? "Поставщик не указан" : record.SupplierDisplayName;
                    int previous; supplierReturnUnits.TryGetValue(supplierName, out previous); supplierReturnUnits[supplierName] = previous + Math.Max(0, record.Units);
                }
            }
            if (store.QualityInspectionHistory != null)
            {
                for (int a = 0; a < store.QualityInspectionHistory.Count; a++)
                {
                    StoreQualityInspectionRecord inspection = store.QualityInspectionHistory[a];
                    if (inspection == null || string.IsNullOrEmpty(inspection.DefectCategory) || inspection.Action != "Изоляция") continue;
                    int previous; defectCounts.TryGetValue(inspection.DefectCategory, out previous); defectCounts[inspection.DefectCategory] = previous + Math.Max(0, inspection.ChangedUnits);
                }
            }
            GUI.Label(new Rect(8, cy, 350, 18), "Возвращено: " + totalReturnedUnits + " ед. · по качеству: " + qualityReturnedUnits + " ед."); cy += 19;
            GUI.Label(new Rect(8, cy, 350, 18), "Сумма возвратов: " + totalReturnValue.ToString("N0") + " ₽"); cy += 21;
            GUI.Label(new Rect(8, cy, 350, 18), "Возвраты по поставщикам:"); cy += 19;
            if (supplierReturnUnits.Count == 0) { GUI.Label(new Rect(16, cy, 330, 18), "Данных пока недостаточно."); cy += 19; }
            else
            {
                List<string> supplierNames = new List<string>(supplierReturnUnits.Keys); supplierNames.Sort(StringComparer.Ordinal);
                for (int a = 0; a < supplierNames.Count; a++) { string name = supplierNames[a]; GUI.Label(new Rect(16, cy, 350, 18), name + " — " + supplierReturnUnits[name] + " ед."); cy += 19; }
            }
            GUI.Label(new Rect(8, cy, 350, 18), "Причины выявленных дефектов (изоляции):"); cy += 19;
            if (defectCounts.Count == 0) { GUI.Label(new Rect(16, cy, 330, 18), "Проверок с классификацией пока нет."); cy += 19; }
            else
            {
                List<string> categories = new List<string>(defectCounts.Keys); categories.Sort(StringComparer.Ordinal);
                for (int a = 0; a < categories.Count; a++) { string category = categories[a]; GUI.Label(new Rect(16, cy, 350, 18), category + " — " + defectCounts[category] + " ед."); cy += 19; }
            }
            cy += 6;
            GUI.Label(new Rect(4, cy, 350, 20), "РЕЙТИНГ ПОСТАВЩИКОВ"); cy += 22;
            // Score is derived from completed receipt reports; pending and cancelled deliveries are excluded.
            // Per supplier: quality 50%, on-time arrival 25%, delivery completeness 25%.
            Dictionary<string, int[]> supplierMetrics = new Dictionary<string, int[]>();
            if (store.PendingDeliveries != null)
            {
                for (int a = 0; a < store.PendingDeliveries.Count; a++)
                {
                    StoreDeliveryRecord delivery = store.PendingDeliveries[a];
                    if (delivery == null || delivery.IsCancelled || !delivery.IsAccepted) continue;
                    string key = string.IsNullOrEmpty(delivery.SupplierDisplayName) ? "Поставщик не указан" : delivery.SupplierDisplayName;
                    int[] metric;
                    if (!supplierMetrics.TryGetValue(key, out metric)) { metric = new int[6]; supplierMetrics.Add(key, metric); }
                    metric[0]++;
                    metric[1] += Math.Max(0, delivery.Units);
                    metric[2] += Math.Max(0, delivery.DamagedUnits);
                    metric[3] += Math.Max(0, delivery.ShortUnits);
                    if (delivery.CompletedDay > 0 && delivery.ArrivalDay > 0 && delivery.CompletedDay <= delivery.ArrivalDay) metric[4]++;
                    metric[5] += Math.Max(0, delivery.AcceptedUnits);
                }
            }
            // Later quality returns are included as additional quality incidents, not as initial receipt damage.
            if (store.SupplierReturns != null)
            {
                for (int a = 0; a < store.SupplierReturns.Count; a++)
                {
                    StoreReturnRecord ret = store.SupplierReturns[a];
                    if (ret == null || ret.IsCancelled || !(ret.IsQualityReturn || ret.Reason == "Брак" || ret.Reason == "Истёк срок годности")) continue;
                    string key = string.IsNullOrEmpty(ret.SupplierDisplayName) ? "Поставщик не указан" : ret.SupplierDisplayName;
                    int[] metric;
                    if (!supplierMetrics.TryGetValue(key, out metric)) { metric = new int[6]; supplierMetrics.Add(key, metric); }
                    metric[2] += Math.Max(0, ret.Units);
                }
            }
            if (supplierMetrics.Count == 0)
            {
                GUI.Label(new Rect(12, cy, 350, 18), "Рейтинг появится после завершённых приёмок."); cy += 20;
            }
            else
            {
                List<string> ratedSuppliers = new List<string>(supplierMetrics.Keys); ratedSuppliers.Sort(StringComparer.Ordinal);
                for (int a = 0; a < ratedSuppliers.Count; a++)
                {
                    string supplier = ratedSuppliers[a]; int[] m = supplierMetrics[supplier];
                    float quality = Mathf.Clamp01(1f - (float)m[2] / Mathf.Max(1, m[1]));
                    float punctuality = m[0] > 0 ? (float)m[4] / m[0] : 0f;
                    float completeness = m[0] > 0 ? Mathf.Clamp01((float)(m[1] - m[3]) / Mathf.Max(1, m[1])) : 0f;
                    int score = Mathf.RoundToInt((quality * 0.50f + punctuality * 0.25f + completeness * 0.25f) * 100f);
                    bool enoughHistory = m[0] >= 3;
                    GUI.Label(new Rect(12, cy, 350, 18), supplier + " — " + (enoughHistory ? score + "/100" : "предварительно: " + score + "/100")); cy += 18;
                    GUI.Label(new Rect(20, cy, 350, 17), "Качество " + Mathf.RoundToInt(quality * 100f) + "% · вовремя " + Mathf.RoundToInt(punctuality * 100f) + "% · комплектность " + Mathf.RoundToInt(completeness * 100f) + "%"); cy += 18;
                    GUI.Label(new Rect(20, cy, 350, 17), "Поставок: " + m[0] + " · принято: " + m[5] + " ед. · дефекты: " + m[2] + " ед. · недостача: " + m[3] + " ед."); cy += 19;
                    if (!enoughHistory) { GUI.Label(new Rect(20, cy, 350, 17), "Для полноценной оценки нужно минимум 3 завершённые поставки."); cy += 18; }
                }
            }
            GUI.Label(new Rect(8, cy, 350, 17), "Вес: качество 50%, сроки 25%, комплектность 25%."); cy += 21;
            GUI.Label(new Rect(4, cy, 350, 19), "ИСТОРИЯ ОЦЕНОК · последние периоды"); cy += 20;
            if (store.SupplierRatingHistory == null || store.SupplierRatingHistory.Count == 0)
            { GUI.Label(new Rect(12, cy, 340, 17), "История появится после закрытия первого 30-дневного месяца."); cy += 19; }
            else
            {
                int shown = 0;
                for (int h = store.SupplierRatingHistory.Count - 1; h >= 0 && shown < 6; h--)
                {
                    SupplierRatingSnapshotRecord snap = store.SupplierRatingHistory[h]; if (snap == null) continue;
                    string label = string.IsNullOrEmpty(snap.SupplierDisplayName) ? snap.SupplierId : snap.SupplierDisplayName;
                    GUI.Label(new Rect(12, cy, 350, 17), "М" + snap.PeriodNumber + " · " + label + " · " + (snap.IsProvisional ? "предв. " : "") + snap.Score + "/100"); cy += 18; shown++;
                }
            }
            cy += 8;
            GUI.Label(new Rect(4, cy, 350, 20), "СРАВНЕНИЕ ПОСТАВЩИКОВ · ПОСЛЕДНИЙ ПЕРИОД"); cy += 22;
            if (store.SupplierRatingHistory == null || store.SupplierRatingHistory.Count == 0)
            {
                GUI.Label(new Rect(12, cy, 340, 17), "Сравнение появится после первого закрытого периода."); cy += 19;
            }
            else
            {
                HashSet<string> shownSupplierIds = new HashSet<string>();
                for (int h = store.SupplierRatingHistory.Count - 1; h >= 0; h--)
                {
                    SupplierRatingSnapshotRecord latest = store.SupplierRatingHistory[h];
                    if (latest == null) continue;
                    string supplierId = string.IsNullOrEmpty(latest.SupplierId) ? latest.SupplierDisplayName : latest.SupplierId;
                    if (string.IsNullOrEmpty(supplierId) || !shownSupplierIds.Add(supplierId)) continue;
                    SupplierRatingSnapshotRecord previous = null;
                    for (int p = h - 1; p >= 0; p--)
                    {
                        SupplierRatingSnapshotRecord candidate = store.SupplierRatingHistory[p];
                        if (candidate == null) continue;
                        string candidateId = string.IsNullOrEmpty(candidate.SupplierId) ? candidate.SupplierDisplayName : candidate.SupplierId;
                        if (candidateId == supplierId) { previous = candidate; break; }
                    }
                    string supplierLabel = string.IsNullOrEmpty(latest.SupplierDisplayName) ? supplierId : latest.SupplierDisplayName;
                    string changeLabel = previous == null ? "новый в истории" : (latest.Score > previous.Score ? "+" : "") + (latest.Score - previous.Score) + " п. к М" + previous.PeriodNumber;
                    GUI.Label(new Rect(12, cy, 340, 17), supplierLabel + " · " + latest.Score + "/100 · " + changeLabel); cy += 18;
                    GUI.Box(new Rect(12, cy, 300, 9), "");
                    GUI.Box(new Rect(12, cy, Mathf.Clamp(latest.Score, 0, 100) * 3f, 9), ""); cy += 12;
                    if (previous != null)
                    {
                        GUI.Label(new Rect(20, cy, 330, 16), "М" + previous.PeriodNumber + ": " + previous.Score + " → М" + latest.PeriodNumber + ": " + latest.Score + " · качество " + latest.QualityScore + "% · сроки " + latest.PunctualityScore + "%"); cy += 17;
                    }
                    else cy += 4;
                }
            }
            cy += 6;
            GUI.Label(new Rect(4, cy, 350, 20), "Возврат поставщику · оформление и сверка"); cy += 24;
            GUI.Label(new Rect(4, cy, 350, 20), "Зачёт поставщику: " + store.SupplierCreditBalance.ToString("N0") + " ₽"); cy += 23;
            storeReturnUseCredit = GUI.Toggle(new Rect(4, cy, 350, 22), storeReturnUseCredit, "Зачесть сумму в будущих закупках"); cy += 26;
            GUI.Label(new Rect(4, cy, 350, 32), "Оформление резервирует товар. При передаче он списывается со склада магазина."); cy += 34;
            if (store.Inventory != null)
            {
                for (int i = 0; i < store.Inventory.Count; i++)
                {
                    StoreInventoryRecord item = store.Inventory[i]; if (item == null) continue;
                    string key = store.BuildingId + ":return:" + item.ProductId; string unitsText;
                    if (!storeReturnUnitEdits.TryGetValue(key, out unitsText)) unitsText = "1";
                    float rowY = cy;
                    GUI.Box(new Rect(2, rowY, viewRect.width - 24, 104), "");
                    GUI.Label(new Rect(8, rowY + 5, 340, 18), item.DisplayName + " · доступно " + Math.Max(0, item.StockUnits - item.ReservedForReturnUnits) + " шт.");
                    storeReturnUnitEdits[key] = GUI.TextField(new Rect(8, rowY + 27, 54, 22), unitsText);
                    string settlement = storeReturnUseCredit ? "Зачёт" : "Возврат денег";
                    if (GUI.Button(new Rect(68, rowY + 27, 92, 24), "Истёк срок")) CreateStoreReturnFromUi(store, item, storeReturnUnitEdits[key], "Истёк срок годности", settlement);
                    if (GUI.Button(new Rect(164, rowY + 27, 72, 24), "Брак")) CreateStoreReturnFromUi(store, item, storeReturnUnitEdits[key], "Брак", settlement);
                    if (GUI.Button(new Rect(240, rowY + 27, 104, 24), "Излишки")) CreateStoreReturnFromUi(store, item, storeReturnUnitEdits[key], "Излишки", settlement);
                    GUI.Label(new Rect(8, rowY + 56, 340, 18), "Себестоимость возврата: " + Math.Max(0, item.AverageUnitCost > 0 ? item.AverageUnitCost : item.WholesalePrice).ToString("N0") + " ₽/шт.");
                    cy += 110;
                }
            }
            cy += 4;
            GUI.Label(new Rect(4, cy, 350, 20), "История возвратов"); cy += 24;
            if (store.SupplierReturns == null || store.SupplierReturns.Count == 0) GUI.Label(new Rect(4, cy, 350, 20), "Возвратов пока нет.");
            else
            {
                for (int r = store.SupplierReturns.Count - 1; r >= 0; r--)
                {
                    StoreReturnRecord ret = store.SupplierReturns[r]; if (ret == null) continue;
                    GUI.Box(new Rect(2, cy, viewRect.width - 24, 98), "");
                    GUI.Label(new Rect(8, cy + 4, 340, 18), ret.ProductDisplayName + " · " + ret.Units + " шт. · " + ret.ReturnValue.ToString("N0") + " ₽");
                    GUI.Label(new Rect(8, cy + 22, 340, 18), ret.Reason + " · " + ret.Status + " · " + ret.SettlementType);
                    string routeInfo = ret.IsDispatched ? (ret.DestinationWarehouseId + " · " + ret.TransportStatus + (ret.ReverseFreightCost > 0 ? " · рейс " + ret.ReverseFreightCost.ToString("N0") + " ₽" : "")) : ret.TransportStatus;
                    GUI.Label(new Rect(8, cy + 40, 340, 18), routeInfo);
                    if (!ret.IsDispatched && !ret.IsCancelled && GUI.Button(new Rect(8, cy + 66, 150, 23), "В обратную логистику"))
                    { string error; if (warehouseStorageSystem.DispatchStoreReturn(store, ret.Id, gameClock != null ? gameClock.Day : 0, out error)) { SaveCity(); status = "Возврат передан в обратную логистику."; } else status = error; }
                    else if (ret.IsDispatched && !ret.IsSettled && GUI.Button(new Rect(8, cy + 66, 190, 23), "Подтвердить у поставщика"))
                    { string error; if (warehouseStorageSystem.ConfirmStoreReturnSettlement(store, ret.Id, gameClock != null ? gameClock.Day : 0, out error)) { SaveCity(); status = "Возврат закрыт. Расчёт с поставщиком проведён."; } else status = error; }
                    else if (!ret.IsDispatched && !ret.IsCancelled && GUI.Button(new Rect(164, cy + 66, 100, 23), "Отменить"))
                    { string error; if (warehouseStorageSystem.CancelStoreReturn(store, ret.Id, out error)) { SaveCity(); status = "Возврат отменён, резерв товара освобождён."; } else status = error; }
                    else GUI.Label(new Rect(8, cy + 68, 340, 18), ret.TransportStatus + (ret.IsSettled ? " · расчёт проведён" : ""));
                    cy += 104;
                }
            }
            cy += 8;
            GUI.Label(new Rect(4, cy, 350, 20), "ПАРТИИ ТОВАРА И СРОКИ ГОДНОСТИ"); cy += 24;
            if (store.StockLots == null || store.StockLots.Count == 0) GUI.Label(new Rect(4, cy, 350, 20), "Партии пока не сформированы.");
            else
            {
                for (int l = store.StockLots.Count - 1; l >= 0; l--)
                {
                    StoreStockLotRecord lot = store.StockLots[l]; if (lot == null) continue;
                    string productName = store.Inventory != null ? (store.Inventory.Find(x => x != null && x.ProductId == lot.ProductId)?.DisplayName ?? lot.ProductId) : lot.ProductId;
                    GUI.Box(new Rect(2, cy, viewRect.width - 24, 82), "");
                    GUI.Label(new Rect(8, cy + 3, 340, 18), productName + " · партия " + (string.IsNullOrEmpty(lot.LotId) ? "—" : lot.LotId.Substring(0, Math.Min(8, lot.LotId.Length))));
                    int quarantinedUnits = lot.IsQuarantined ? lot.RemainingUnits : Mathf.Clamp(lot.QuarantinedUnits, 0, lot.RemainingUnits);
                    GUI.Label(new Rect(8, cy + 21, 340, 18), "Остаток: " + lot.RemainingUnits + " · карантин: " + quarantinedUnits + " · резерв: " + lot.ReservedForReturnUnits);
                    GUI.Label(new Rect(8, cy + 39, viewRect.width - 40, 16), lot.Status + " · дефект: " + (string.IsNullOrEmpty(lot.DefectCategory) ? "не указан" : lot.DefectCategory));
                    string[] defectCategories = { "Упаковка", "Срок", "Производство", "Хранение" };
                    for (int d = 0; d < defectCategories.Length; d++)
                    {
                        int buttonX = 8 + d * 82;
                        string category = defectCategories[d];
                        if (GUI.Button(new Rect(buttonX, cy + 57, 78, 20), "+1 " + category))
                        { string error; if (warehouseStorageSystem.SetStoreLotQuarantinedUnits(store, lot.LotId, quarantinedUnits + 1, gameClock != null ? gameClock.Day : 0, "Единица направлена на проверку качества", category, out error)) { SaveCity(); status = "Единица изолирована. Категория дефекта: " + category + "."; } else status = error; }
                    }
                    if (GUI.Button(new Rect(336, cy + 57, 76, 20), "−1 допуск"))
                    { string error; if (warehouseStorageSystem.SetStoreLotQuarantinedUnits(store, lot.LotId, quarantinedUnits - 1, gameClock != null ? gameClock.Day : 0, "Единица допущена после проверки", string.Empty, out error)) { SaveCity(); status = "Одна единица возвращена в доступный остаток."; } else status = error; }
                    cy += 86;
                }
                cy += 8;
                GUI.Label(new Rect(4, cy, 350, 20), "ИСТОРИЯ ПРОВЕРОК КАЧЕСТВА"); cy += 22;
                if (store.QualityInspectionHistory == null || store.QualityInspectionHistory.Count == 0)
                    GUI.Label(new Rect(8, cy, 340, 18), "Проверок пока не было.");
                else
                {
                    int shown = 0;
                    for (int h = store.QualityInspectionHistory.Count - 1; h >= 0 && shown < 8; h--)
                    {
                        StoreQualityInspectionRecord inspection = store.QualityInspectionHistory[h];
                        if (inspection == null) continue;
                        string productName = store.Inventory != null ? (store.Inventory.Find(x => x != null && x.ProductId == inspection.ProductId)?.DisplayName ?? inspection.ProductId) : inspection.ProductId;
                        string line = "День " + inspection.Day + " · " + inspection.Action + " " + inspection.ChangedUnits + " ед. · " + productName + (string.IsNullOrEmpty(inspection.DefectCategory) ? string.Empty : " · " + inspection.DefectCategory);
                        GUI.Label(new Rect(8, cy, viewRect.width - 40, 18), line);
                        cy += 19;
                        if (!string.IsNullOrEmpty(inspection.Reason))
                        {
                            GUI.Label(new Rect(16, cy, viewRect.width - 48, 18), "Причина: " + inspection.Reason);
                            cy += 19;
                        }
                        shown++;
                    }
                }
            }
            contentHeight = Mathf.Max(contentHeight, cy + 12);
        }
        else
        {
            GUI.Label(new Rect(4, cy, 340, 22), "Финансовые показатели магазина"); cy += 28;
            string[] finance = {
                "Касса сейчас: " + store.OperatingBalance.ToString("N0") + " ₽",
                "Выручка за последний день: " + store.LastDailyRevenue.ToString("N0") + " ₽",
                "Расходы за последний день: " + store.LastDailyOperatingCost.ToString("N0") + " ₽",
                "Накопленная выручка: " + store.CumulativeRevenue.ToString("N0") + " ₽",
                "Накопленные расходы: " + store.CumulativeOperatingCosts.ToString("N0") + " ₽",
                "Транспортные расходы всего: " + store.CumulativeFreightCost.ToString("N0") + " ₽",
                "Ущерб от повреждений: " + store.CumulativeDamageLoss.ToString("N0") + " ₽",
                "Продано за всё время: " + store.TotalUnitsSold + " ед."
            };
            for (int i = 0; i < finance.Length; i++) GUI.Label(new Rect(4, cy + i * 24, 350, 22), finance[i]);
            cy += finance.Length * 24 + 12;
            GUI.Label(new Rect(4, cy, 350, 22), "История поставок · финансовые акты"); cy += 26;
            if (store.PendingDeliveries == null || store.PendingDeliveries.Count == 0)
            {
                GUI.Label(new Rect(4, cy, 350, 22), "Поставок пока нет."); cy += 24;
            }
            else
            {
                int shown = 0;
                for (int d = store.PendingDeliveries.Count - 1; d >= 0 && shown < 20; d--)
                {
                    StoreDeliveryRecord delivery = store.PendingDeliveries[d];
                    if (delivery == null) continue;
                    float rowY = cy + shown * 184f;
                    string actStatus = delivery.IsFinanciallyReconciled ? "СВЕРКА ЗАВЕРШЕНА" :
                        delivery.IsAccepted ? "ТРЕБУЕТ ПРОВЕРКИ" : delivery.IsCancelled ? "ОТМЕНЕНА" : delivery.LogisticsStatus;
                    GUI.Box(new Rect(2, rowY, viewRect.width - 24, 178), "");
                    GUI.Label(new Rect(9, rowY + 5, 340, 20), (delivery.ProductDisplayName ?? "Товар") + " · акт " + (delivery.Id ?? "—").Substring(0, Math.Min(8, (delivery.Id ?? "—").Length)));
                    GUI.Label(new Rect(9, rowY + 25, 340, 18), "Статус: " + actStatus);
                    int received = delivery.HasReportedReceivedUnits ? Math.Max(0, delivery.ReportedReceivedUnits) : (delivery.IsAccepted ? delivery.Units : 0);
                    int damaged = delivery.HasReportedDamagedUnits ? Math.Max(0, delivery.ReportedDamagedUnits) : delivery.DamagedUnits;
                    int accepted = delivery.IsAccepted ? delivery.AcceptedUnits : Math.Max(0, received - damaged);
                    int shortage = delivery.IsAccepted ? delivery.ShortUnits : Math.Max(0, delivery.Units - received);
                    GUI.Label(new Rect(9, rowY + 45, 340, 18), "Заказано: " + delivery.Units + " · получено: " + received + " · пригодно: " + accepted);
                    GUI.Label(new Rect(9, rowY + 64, 340, 18), "Недостача: " + shortage + " · повреждено: " + damaged);
                    GUI.Label(new Rect(9, rowY + 83, 340, 18), "Закупка: " + delivery.GrossPurchaseValue.ToString("N0") + " ₽ · возврат: " + delivery.ShortageRefundValue.ToString("N0") + " ₽");
                    GUI.Label(new Rect(9, rowY + 102, 340, 18), "Чистая закупка: " + delivery.NetPurchaseValue.ToString("N0") + " ₽ · фрахт: " + delivery.FreightCostCharged.ToString("N0") + " ₽");
                    GUI.Label(new Rect(9, rowY + 121, 340, 18), "Себестоимость запаса: " + delivery.GoodsCostAllocatedToInventory.ToString("N0") + " ₽");
                    GUI.Label(new Rect(9, rowY + 140, 340, 18), "Ущерб: " + delivery.GoodsCostWrittenOff.ToString("N0") + " ₽ · фрахт в запасе: " + delivery.FreightAllocatedToInventory.ToString("N0") + " ₽");
                    GUI.Label(new Rect(9, rowY + 158, 340, 18), "Фрахт не распределён: " + delivery.FreightUnallocatedExpense.ToString("N0") + " ₽");
                    shown++;
                }
                cy += shown * 184f;
                if (store.PendingDeliveries.Count > 20) GUI.Label(new Rect(4, cy, 350, 20), "Показаны последние 20 поставок.");
            }
            cy += 24;
            GUI.Label(new Rect(4, cy, 350, 42), "Акт показывает управленческий учёт прототипа. Для незавершённых поставок суммы сверки могут быть нулевыми до приёмки.");
            contentHeight = Mathf.Max(contentHeight, cy + 56);
        }
        GUI.EndScrollView();
    }

}
