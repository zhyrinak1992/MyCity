using System;
using System.Collections.Generic;

namespace MyCity.Data
{
    [Serializable]
    public sealed class BuildingRecord
    {
        public string Id;
        public string DefinitionId;
        public int SectorX;
        public int SectorZ;
        public int GridX;
        public int GridZ;
        public float PositionX;
        public float PositionZ;
        public int ConstructionCost;
        public int RotationQuarterTurns;
        public int Level = 1;
        public float Condition = 1f;
        // Civil-defence shelter capacity; zero means the building is not a designated shelter.
        public int EmergencyShelterCapacity;
        public List<HighRiseFloorRecord> HighRiseFloors = new List<HighRiseFloorRecord>();
        public int ElevatorCars;
        public int ElevatorTripsToday;
        public int HighRiseLastSettlementDay;
        public int FacilityCapacity;
        public long HighRiseDailyRent;
        public long HighRiseDailyPayroll;
    }

    [Serializable]
    public sealed class HighRiseFloorRecord
    {
        public int FloorNumber;
        public string Function;
        public int Capacity;
        public int Occupants;
        public int Tenants;
        public int Visitors;
        public bool IsEngineering;
    }

    [Serializable]
    public sealed class CitizenRecord
    {
        public string Id;
        public string FirstName;
        public string LastName;
        public int Age;
        public int AgeLastAdvancedDay = 1;
        public bool IsDeceased;
        public int DeathDay;
        public string DeathCause;
        public string FuneralStatus;
        public int MorgueAdmissionDay;
        public int BurialDay;
        public string MorgueBuildingId;
        public string CemeteryBuildingId;
        public string HomeBuildingId;
        public string HouseholdId;
        public string WorkplaceBuildingId;
        public string ProfessionId;
        public int MonthlySalary;
        public long WalletBalance;
        public long AccruedWages;
        public string AccruedWageEmployerBuildingId;
        public bool IsEmployed;
        public int DaysWorked;
        public int CurrentSectorX;
        public int CurrentSectorZ;
        public int ScheduleState;
        public int MinutesUntilNextState;
        public float Health = 1f;
        public float Happiness = 0.5f;
        // Persistent household needs, normalized to 0..100 (higher means more urgent).
        public float FoodNeed = 35f;
        public float ClothingNeed = 20f;
        public float HomeGoodsNeed = 25f;
        public float LeisureNeed = 30f;
        public int NeedsLastUpdatedDay = 1;
        public List<string> EventHistory = new List<string>();
    }


    [Serializable]
    public sealed class StoreDeliveryRecord
    {
        public string Id;
        public string ProductId;
        public string ProductDisplayName;
        public string SupplierId;
        public string SupplierDisplayName;
        public int Units;
        public long TotalCost;
        public long SupplierCreditApplied;
        public int OrderedDay;
        public int ArrivalDay;
        public bool IsDelivered;
        // Physical warehouse-dispatch linkage. Empty values mean this is still a legacy supplier delivery.
        public string WarehouseId;
        public string ReservationId;
        public string FleetVehicleId;
        public string LogisticsStatus = "Ожидает поставки";
        public List<RoadPathNodeRecord> RoadPath = new List<RoadPathNodeRecord>();
        public int CurrentPathIndex;
        public int MinutesOnCurrentSegment;
        public int TravelMinutesPerSegment = 2;
        public int RouteDistanceKm;
        public int DispatchedDay;
        public int CompletedDay;
        public long CargoUnitCostAtDispatch;
        public long FreightCost;
        public bool IsCancelled;
        public bool IsAccepted;
        public bool IsRefunded;
        // The explicit flag preserves legacy saves whose serialized records predate receiving reports.
        public bool HasReportedReceivedUnits;
        public int ReportedReceivedUnits = -1;
        // AcceptedUnits is the saleable quantity: physically received minus damaged units.
        public int AcceptedUnits;
        public int ShortUnits;
        public bool IsShortageRefunded;
        // Damage report is separate from shortage: damaged units physically arrived but are not saleable.
        public bool HasReportedDamagedUnits;
        public int ReportedDamagedUnits = -1;
        public int DamagedUnits;
        public string DamageReason;
        public bool IsDamageWrittenOff;
        public int DamageWriteOffUnits;
        public long DamageWriteOffValue;
        // Persisted per-shipment financial reconciliation. Freight is charged once at receipt;
        // its cost is allocated between saleable stock, damaged goods and unallocated trip expense.
        public long GrossPurchaseValue;
        public long ShortageRefundValue;
        public long NetPurchaseValue;
        public long GoodsCostAllocatedToInventory;
        public long GoodsCostWrittenOff;
        public long FreightCostCharged;
        public long FreightAllocatedToInventory;
        public long FreightAllocatedToDamage;
        public long FreightUnallocatedExpense;
        public bool IsFreightSettled;
        public bool IsFreightLedgerPosted;
        public bool IsFinanciallyReconciled;
        public string ReceiptStatus = "Ожидает приёмки";
    }


    [Serializable]
    public sealed class StoreStockLotRecord
    {
        public string LotId;
        public string ProductId;
        public string SourceDeliveryId;
        public int ReceivedDay;
        public int ExpiryDay;
        public int RemainingUnits;
        public int ReservedForReturnUnits;
        public long UnitCost;
        public string Status = "В продаже";
        // Quality gate: quarantined lots are excluded from sale and expiry write-off until reviewed.
        public bool IsQuarantined;
        // Partial quarantine count. Legacy IsQuarantined=true means the whole remaining lot is quarantined.
        public int QuarantinedUnits;
        public int QualityInspectionDay;
        public string QualityReason;
        public string DefectCategory;
    }

    [Serializable]
    public sealed class StoreQualityInspectionRecord
    {
        public string Id;
        public string LotId;
        public string ProductId;
        public int Day;
        public int PreviousQuarantinedUnits;
        public int NewQuarantinedUnits;
        public int ChangedUnits;
        public string Action;
        public string Reason;
        public string DefectCategory;
    }

    [Serializable]
    public sealed class StoreReturnLotAllocationRecord
    {
        public string LotId;
        public int Units;
        public bool IsQualityAllocation;
    }

    [Serializable]
    public sealed class StoreReturnRecord
    {
        public string Id;
        public string ProductId;
        public string ProductDisplayName;
        public string SupplierId;
        public string SupplierDisplayName;
        public int Units;
        public string Reason;
        public string SettlementType; // Возврат денег или зачёт в будущих закупках.
        public long UnitCost;
        public long ReturnValue;
        public int CreatedDay;
        public int DispatchedDay;
        public int CompletedDay;
        public string Status = "Оформлен";
        public string TransportStatus = "Ожидает передачи перевозчику";
        public bool IsDispatched;
        public bool IsCancelled;
        public bool IsSupplierAccepted;
        public bool IsSettled;
        public string Notes;
        public bool IsQualityReturn;
        // Reverse-logistics route to the nearest operating distribution warehouse (supplier consolidation hub).
        public string FleetVehicleId;
        public string VehicleTransferId;
        public string DestinationWarehouseId;
        public List<RoadPathNodeRecord> RoadPath = new List<RoadPathNodeRecord>();
        public int CurrentPathIndex;
        public int MinutesOnCurrentSegment;
        public int TravelMinutesPerSegment = 2;
        public int RouteDistanceKm;
        public long ReverseFreightCost;
        public bool IsFreightSettled;
        public List<StoreReturnLotAllocationRecord> LotAllocations = new List<StoreReturnLotAllocationRecord>();
    }

    [Serializable]
    public sealed class SupplierRatingSnapshotRecord
    {
        public string SupplierId;
        public string SupplierDisplayName;
        public int PeriodNumber;
        public int PeriodStartDay;
        public int PeriodEndDay;
        public int CompletedDeliveries;
        public int AcceptedUnits;
        public int DefectiveUnits;
        public int ShortUnits;
        public int OnTimeDeliveries;
        public int Score;
        public int QualityScore;
        public int PunctualityScore;
        public int CompletenessScore;
        public bool IsProvisional;
    }

    [Serializable]
    public sealed class WorkplaceRecord
    {
        public string Id;
        public string BuildingId;
        public string DisplayName;
        public int RoadNodeX;
        public int RoadNodeZ;
        public string ProfessionId;
        public string StoreTypeId = "grocery";
        public string ProductDisplayName = "Продукты";
        public int StaffCapacity;
        public int StaffCount;
        public int MonthlyPayroll;
        public long OperatingBalance;
        // Prototype daily business model; values are balance-tuning parameters, not final economy rules.
        public long DailyRevenuePerEmployee = 2500;
        public long DailyFixedOperatingCost = 800;
        public long DailyOperatingCostPerEmployee = 500;
        // Prototype retail inventory; defaults are applied on first registration for older saves.
        public int InventoryUnits;
        public int TargetInventoryUnits = 100;
        public long ProductUnitCost = 200;
        public long ProductUnitPrice = 350;
        public long TotalUnitsSold;
        public long LastDailyUnitsSold;
        public bool InventoryInitialized;
        // Full per-product assortment; legacy single-product fields above remain for save migration.
        public List<StoreInventoryRecord> Inventory = new List<StoreInventoryRecord>();
        public List<StoreDeliveryRecord> PendingDeliveries = new List<StoreDeliveryRecord>();
        public List<StoreStockLotRecord> StockLots = new List<StoreStockLotRecord>();
        // Append-only audit trail for quality isolation and release operations.
        public List<StoreQualityInspectionRecord> QualityInspectionHistory = new List<StoreQualityInspectionRecord>();
        public long CumulativeExpiryLoss;
        public long LastDailyExpiryLoss;
        public List<StoreReturnRecord> SupplierReturns = new List<StoreReturnRecord>();
        // Immutable 30-day supplier rating history snapshots.
        public List<SupplierRatingSnapshotRecord> SupplierRatingHistory = new List<SupplierRatingSnapshotRecord>();
        public int LastSupplierRatingSnapshotPeriod;
        public long SupplierCreditBalance;
        public long LastDailyRevenue;
        public long LastDailyOperatingCost;
        public long CumulativeRevenue;
        public long CumulativeOperatingCosts;
        public long LastDailyInventoryStorageCost;
        public long CumulativeInventoryStorageCost;
        // Damage losses classify already-paid stock write-offs; they do not debit the balance twice.
        public long LastDailyDamageLoss;
        public long CumulativeDamageLoss;
        public long LastDailyFreightCost;
        public long CumulativeFreightCost;
        public int FreightCostLastProcessedDay;
        public int DamageLossLastProcessedDay;
        public bool IsOpen = true;
        // Non-retail employers use the same payroll ledger without retail stock simulation.
        public bool IsRetailBusiness = true;
        public long DailyRentIncome;
    }



    [Serializable]
    public sealed class SupplierInventoryRecord
    {
        public string ProductId;
        public int Units;
        public long UnitPrice;
        public int DailyRestockUnits;
        public int MaxUnits;
    }

    [Serializable]
    public sealed class SupplierContractHistoryRecord
    {
        public int Day;
        public int StartDay;
        public int EndDay;
        public int MinimumOrderUnits;
        public long FreightCostPerKm;
        public int DiscountPermille;
        public string ChangeNote;
    }

    [Serializable]
    public sealed class SupplierClaimRecord
    {
        public string Id, OrderId, SupplierId, ProductionSiteId, ClaimType, Status, Notes;
        public int Units, SubmittedDay, ResponseDay;
        public long RequestedAdditionalCompensation, ApprovedAdditionalCompensation;
        public bool IsSettled;
        public long SettledAmount;
        public int SettledDay;
        public string SettlementReference;
    }

    [Serializable]
    public sealed class SupplierNegotiationRecord
    {
        public string Id, Status, Reason, ResponseNote;
        public int Day, RequestedDiscountPermille, RequestedMinimumOrderUnits, RequestedContractDays;
        public long RequestedFreightCostPerKm;
        public int CounterDiscountPermille, CounterMinimumOrderUnits, CounterContractDays;
        public long CounterFreightCostPerKm;
    }

    [Serializable]
    public sealed class SupplierFinancialTransactionRecord
    {
        public string Id, Type, OrderId, ClaimId, Description;
        public int Day;
        public long Amount;
        public bool IsReconciled;
        public int ReconciledDay;
        public string ReconciliationBatchId;
    }

    [Serializable]
    public sealed class SupplierSettlementSummaryRecord
    {
        public string SupplierId, SupplierName;
        public long TotalPaid, TotalReturned, TotalCompensation, NetPaid, TotalOutstanding;
        public long UnreconciledPaid, UnreconciledCredits, UnreconciledNet;
        public int TransactionCount, UnreconciledTransactionCount, ReconciledTransactionCount;
        public int LastTransactionDay, LastReconciliationDay;
    }

    [Serializable]
    public sealed class SupplierRecord
    {
        public string Id;
        public string DisplayName;
        public bool IsOperating = true;
        public int LastRestockDay;
        public int RoadNodeX;
        public int RoadNodeZ;
        public List<SupplierInventoryRecord> Inventory = new List<SupplierInventoryRecord>();
        // Industrial procurement performance. Quality remains provisional until a formal inspection is recorded.
        public int CompletedDeliveries;
        public int OnTimeDeliveries;
        public int AcceptedUnits;
        public int DefectiveUnits;
        public int ShortUnits;
        public long CumulativePurchaseValue;
        public int LastCompletedDeliveryDay;
        // Contract terms. Zero values in legacy saves are normalized to safe defaults.
        public int ContractStartDay = 1;
        public int ContractEndDay; // 0 = no fixed end date
        public int MinimumOrderUnits = 1;
        public long FreightCostPerKm = 18;
        public int ContractDiscountPermille = 1000; // 1000 = list price
        public List<SupplierRatingSnapshotRecord> RatingHistory = new List<SupplierRatingSnapshotRecord>();
        public int LastRatingSnapshotPeriod;
        public int SnapshotCompletedDeliveries;
        public int SnapshotAcceptedUnits;
        public int SnapshotDefectiveUnits;
        public int SnapshotShortUnits;
        public int SnapshotOnTimeDeliveries;
        // Append-only snapshots of negotiated contract terms.
        public List<SupplierContractHistoryRecord> ContractHistory = new List<SupplierContractHistoryRecord>();
        public List<SupplierClaimRecord> Claims = new List<SupplierClaimRecord>();
        public List<SupplierNegotiationRecord> Negotiations = new List<SupplierNegotiationRecord>();
        public List<SupplierFinancialTransactionRecord> FinancialHistory = new List<SupplierFinancialTransactionRecord>();
        // Payment terms: 1000 = full prepayment; 0 = full deferral. CreditLimit is the maximum open payable.
        public int PaymentPrepaymentPermille = 1000;
        public int PaymentDeferralDays = 0;
        public long SupplierCreditLimit = 0;
        // Late fee: daily rate and lifetime cap as permille of the original deferred principal.
        public int LateFeePermillePerDay = 5;
        public int LateFeeMaxPermille = 100;
    }

    [Serializable]
    public sealed class SupplierQuoteRecord
    {
        public string SupplierId;
        public string SupplierDisplayName;
        public int AvailableUnits;
        public long UnitPrice;
        public int RouteDistanceKm;
        public int EstimatedTravelDays;
        public long GoodsCost;
        public long TransportCost;
        public long TotalCost;
        public int ReliabilityScore;
        public bool IsProvisional;
        public int MinimumOrderUnits;
        public int ContractEndDay;
        public int ContractDiscountPermille;
        public long FreightCostPerKm;
    }

    [Serializable]
    public sealed class SupplyOrderRecord
    {
        public string Id;
        public string SupplierId;
        public string ProductionSiteId;
        public string ProductId;
        public int Units;
        public long TotalCost;
        public int OrderedDay;
        public int ArrivalDay;
        public bool IsDelivered;
        public string Status;
        public string VehicleId;
        public int RouteDistanceKm;
        public long TransportCost;
        public List<RoadPathNodeRecord> RoadPath = new List<RoadPathNodeRecord>();
        // Persisted movement state for the supplier truck associated with this order.
        public int CurrentPathIndex;
        public float SegmentProgress;
        public bool IsReturning;
        public int CompletedDay;
        public int ReceivedUnits;
        public int AcceptedUnits;
        public int DefectiveUnits;
        public int ShortUnits;
        public bool IsQualityInspected;
        public int InspectionDay;
        public int ActualArrivalDay;
        public string InspectionNotes;
        public long GoodsCost;
        public long UnitGoodsPrice;
        public long RefundedGoodsCost;
        public long PrepaidAmount;
        public long OutstandingPayable;
        public int PaymentDueDay;
        public int LastPaymentDay;
        public long PaymentPenaltyAccrued;
        public long PaymentPenaltyPaid;
        public int LastPenaltyDay;
        public int LastPaymentNoticeDay;
        public bool PaymentOverdueNoticeSent;
        public int ContractDiscountPermille = 1000;
    }

    [Serializable]
    public sealed class SupplierPaymentScheduleRecord
    {
        public string SupplierId, SupplierName, OrderId, ProductId, ProductionSiteId;
        public int DueDay, OverdueDays;
        public long PrincipalOutstanding, PenaltyOutstanding, TotalOutstanding;
        public bool IsOverdue;
    }

    [Serializable]
    public sealed class CargoVehicleRecord
    {
        public string Id;
        public string OrderId;
        public string VehicleType;
        public int CapacityUnits;
        public int LoadedUnits;
        public int RouteDistanceKm;
        public int DispatchedDay;
        public int ArrivalDay;
        public int LastProcessedDay;
        public string Status;
        public List<RoadPathNodeRecord> RoadPath = new List<RoadPathNodeRecord>();
        public int CurrentPathIndex;
        public float SegmentProgress;
        public bool IsReturning;
    }

    [Serializable]
    public sealed class RoadPathNodeRecord
    {
        public int X;
        public int Z;
    }

    [Serializable]
    public sealed class WarehouseInventoryRecord
    {
        public string ProductId;
        public int Units;
        // Units reserved for outbound orders. Reservations survive save/load and are not sellable or withdrawable.
        public int ReservedUnits;
        public long AverageUnitCost;
    }

    [Serializable]
    public sealed class WarehouseRecord
    {
        public string Id;
        public string DisplayName;
        public string BuildingId;
        public string WarehouseTypeId = "distribution";
        public int RoadNodeX;
        public int RoadNodeZ;
        public int CapacityUnits = 10000;
        public int DockCount = 2;
        public bool IsOperating = true;
        public List<WarehouseInventoryRecord> Inventory = new List<WarehouseInventoryRecord>();
        public List<WarehouseStockReservationRecord> StockReservations = new List<WarehouseStockReservationRecord>();
        public List<WarehouseDockAssignmentRecord> ActiveDockAssignments = new List<WarehouseDockAssignmentRecord>();
        public List<WarehouseDockQueueRecord> DockQueue = new List<WarehouseDockQueueRecord>();
        public List<WarehouseTransferRecord> Transfers = new List<WarehouseTransferRecord>();
        public string LastStatus = "Готов к работе";
        public long DailyBaseOperatingCost;
        public long DailyCostPerStoredUnit;
        public long LastDailyOperatingCost;
        public long CumulativeOperatingCosts;
        public int LastSettledDay;
    }

    [Serializable]
    public sealed class WarehouseStockReservationRecord
    {
        public string ReservationId;
        public string ProductId;
        public int Units;
        public int CreatedDay;
    }

    [Serializable]
    public sealed class WarehouseTransferRecord
    {
        public string VehicleId;
        public string SourceWarehouseId;
        public string DestinationWarehouseId;
        public string ProductId;
        public int Units;
        public string Status;
        public List<RoadPathNodeRecord> RoadPath = new List<RoadPathNodeRecord>();
        public int CurrentPathIndex;
        public int MinutesOnCurrentSegment;
        public int TravelMinutesPerSegment = 2;
        public int CreatedDay;
        public int CompletedDay;
        public bool CargoDelivered;
        public string FleetVehicleId;
        public int RouteDistanceKm;
        public long FuelCost;
        public long DriverCost;
        public long MaintenanceCost;
        public long TotalTransportCost;
        // Cost basis captured at dispatch; freight is allocated to the delivered cargo.
        public long CargoUnitCostAtDispatch;
        public long FreightCostPerUnit;
        public long TotalCargoLandedCost;
    }

    [Serializable]
    public sealed class FleetVehicleRecord
    {
        public string Id;
        public string DisplayName;
        public string VehicleClass;
        public int CapacityUnits;
        public float FuelLitresPer100Km;
        public long DriverCostPerKm;
        public long MaintenanceCostPerKm;
        public string Status = "Свободен";
        public string ActiveTransferId;
        public long TotalDistanceKm;
        public long TotalOperatingCost;
        public int CompletedTrips;
    }

    [Serializable]
    public sealed class FleetTransactionRecord
    {
        public string Id;
        public string VehicleId;
        public string TransferId;
        public int DistanceKm;
        public long FuelCost;
        public long DriverCost;
        public long MaintenanceCost;
        public long TotalCost;
        public int Day;
    }

    [Serializable]
    public sealed class WarehouseDockAssignmentRecord
    {
        public int DockIndex;
        public string VehicleId;
        public string Operation;
        public int RequestedUnits;
        public int RemainingServiceMinutes;
        public int StartedAtDay;
        public string SourceWarehouseId;
        public string DestinationWarehouseId;
        public string ProductId;
        public bool IsTransferUnloading;
        // Stable retail order key; empty for generic dock jobs and inter-warehouse transfers.
        public string StoreOrderId;
    }

    [Serializable]
    public sealed class WarehouseDockQueueRecord
    {
        public string VehicleId;
        public string Operation;
        public int RequestedUnits;
        public int EnqueuedAtDay;
        public int Priority;
        public string SourceWarehouseId;
        public string DestinationWarehouseId;
        public string ProductId;
        public bool IsTransferUnloading;
        // Stable retail order key survives save/load while the truck waits for a dock.
        public string StoreOrderId;
    }

    [Serializable]
    public sealed class ProductionInventoryRecord
    {
        public string ProductId;
        public int Units;
        // Weighted-average landed cost per unit, including inbound freight.
        public long AverageUnitCost;
    }

    [Serializable]
    public sealed class ProductionSiteRecord
    {
        public string Id;
        public string BuildingId;
        public string DisplayName;
        public int RoadNodeX;
        public int RoadNodeZ;
        public string RecipeId;
        public int DailyCapacityUnits = 20;
        public int StorageCapacityUnits = 500;
        public bool IsOperating = true;
        public int LastProcessedDay;
        public long TotalUnitsProduced;
        public long TotalUnitsConsumed;
        public long OperatingFunds = 100000;
        public long DailyBaseOperatingCost = 1500;
        public long DailyCostPerOutputUnit = 20;
        public long LastDailyOperatingCost;
        public long CumulativeOperatingCosts;
        public long TotalRawMaterialPurchases;
        public long LastDailyMaterialCost;
        public long CumulativeMaterialCost;
        public long LastDailyOutputCost;
        public long CumulativeOutputCost;
        public List<ProductionInventoryRecord> Inputs = new List<ProductionInventoryRecord>();
        public List<ProductionInventoryRecord> Outputs = new List<ProductionInventoryRecord>();
        public string LastStatus;
    }

    [Serializable]
    public sealed class HouseholdRecord
    {
        public string Id;
        public string HomeBuildingId;
        public List<string> MemberCitizenIds = new List<string>();
        // Co-residence roles; these do not assert legal or biological kinship.
        public List<HouseholdMemberRecord> Members = new List<HouseholdMemberRecord>();
        public string HouseholdHeadCitizenId;
        public int MemberCount;
        public int AdultCount;
        public int ChildCount;
        public long CombinedPersonalWallets;
        public float FoodNeed;
        public float ClothingNeed;
        public float HomeGoodsNeed;
        public float LeisureNeed;
        public int LastAggregatedDay;
    }

    [Serializable]
    public sealed class HouseholdMemberRecord
    {
        public string CitizenId;
        public string Role;
        public string GuardianCitizenId;
    }


    [Serializable]
    public sealed class FamilyAccountRecord
    {
        public string Id;
        public string HouseholdId;
        public long Balance;
        public int CreatedDay = 1;
        public int LastUpdatedDay = 1;
    }

    [Serializable]
    public sealed class FamilyFinanceTransactionRecord
    {
        public string Id;
        public string HouseholdId;
        public int Day;
        public string Type; // Contribution or SharedExpense
        public string Category;
        public string Description;
        public long Amount;
        public string CitizenId;
    }

    [Serializable]
    public sealed class FamilyResponsibilityRecord
    {
        public string HouseholdId;
        public string Category;
        public string CitizenId;
        public int SharePercent;
    }

    public sealed class SectorRecord
    {
        public int X;
        public int Z;
        public bool IsDiscovered;
        public List<BuildingRecord> Buildings = new List<BuildingRecord>();
        public List<CitizenRecord> Citizens = new List<CitizenRecord>();
    }

    [Serializable]
    public sealed class RoadSegmentRecord
    {
        public int StartX;
        public int StartZ;
        public int EndX;
        public int EndZ;
    }

    [Serializable]

    [Serializable]
    public sealed class MayorRequestRecord
    {
        public string Id;
        public int CreatedDay;
        public int DueDay;
        public string SenderType;
        public string SenderName;
        public string Category;
        public string Title;
        public string Message;
        public int Priority;
        public long SuggestedCost;
        public string Status = "Ожидает решения";
        public string Decision;
        public int ResolvedDay;
        public long ActualCost;
        public int RelationshipDelta;
        public string ProjectId;
    }

    [Serializable]
    public sealed class CityMaintenanceProjectRecord
    {
        public string Id;
        public string RequestId;
        public string BuildingId;
        public string Title;
        public string Status = "В очереди";
        public int CreatedDay;
        public int DueDay;
        public int ProgressDays;
        public int LastProgressDay;
        public int RequiredDays = 3;
        public long Budget;
        public bool CompletionApplied;
    }

    [Serializable]
    public sealed class MayorDailyReportRecord
    {
        public int Day;
        public int RequestsReceived;
        public int RequestsResolved;
        public int RequestsDeferred;
        public long MoneySpent;
        public int ResidentApprovalDelta;
        public string Summary;
    }

    [Serializable]
    public sealed class CityFinancialTransactionRecord
    {
        public string Id;
        public int Day;
        public string Category;
        public string Description;
        public long Amount;
        public bool IsIncome;
        public string SourceId;
    }

    [Serializable]
    public sealed class CitizenRelationshipRecord
    {
        public string Id;
        // Supported values: Spouse, ParentChild. ParentChild is directed from ParentCitizenId to ChildCitizenId.
        public string RelationshipType;
        public string FirstCitizenId;
        public string SecondCitizenId;
        public string ParentCitizenId;
        public string ChildCitizenId;
        public int StartedDay = 1;
        public bool IsActive = true;
    }


    [Serializable]
    public sealed class FuneralEventRecord
    {
        public string Id;
        public string CitizenId;
        public int DeathDay;
        public int MorgueAdmissionDay;
        public int BurialDay;
        public string Cause;
        public string MorgueBuildingId;
        public string CemeteryBuildingId;
        public string Status;
    }

    public sealed class CitySaveData
    {
        public int SchemaVersion = 1;
        public string CityId;
        public string CityName = "My City";
        public long SimulationTick;
        public int Day = 1;
        public int MinuteOfDay;
        public int RandomSeed;
        public long CityBudget;
        public List<SectorRecord> Sectors = new List<SectorRecord>();
        public List<WorkplaceRecord> Workplaces = new List<WorkplaceRecord>();
        public List<HouseholdRecord> Households = new List<HouseholdRecord>();
        public List<CitizenRelationshipRecord> CitizenRelationships = new List<CitizenRelationshipRecord>();
        public List<FamilyAccountRecord> FamilyAccounts = new List<FamilyAccountRecord>();
        public List<FamilyFinanceTransactionRecord> FamilyFinanceTransactions = new List<FamilyFinanceTransactionRecord>();
        public List<FamilyResponsibilityRecord> FamilyResponsibilities = new List<FamilyResponsibilityRecord>();
        public List<FuneralEventRecord> FuneralEvents = new List<FuneralEventRecord>();
        public List<ProductionSiteRecord> ProductionSites = new List<ProductionSiteRecord>();
        public List<WarehouseRecord> Warehouses = new List<WarehouseRecord>();
        public List<FleetVehicleRecord> FleetVehicles = new List<FleetVehicleRecord>();
        public List<FleetTransactionRecord> FleetTransactions = new List<FleetTransactionRecord>();
        public List<CityFinancialTransactionRecord> CityFinancialTransactions = new List<CityFinancialTransactionRecord>();
        public List<MayorRequestRecord> MayorRequests = new List<MayorRequestRecord>();
        public List<MayorDailyReportRecord> MayorDailyReports = new List<MayorDailyReportRecord>();
        public int MayorApprovalScore = 50;
        public int MayorLastGeneratedDay;
        public List<CityMaintenanceProjectRecord> MaintenanceProjects = new List<CityMaintenanceProjectRecord>();
        public List<SupplierRecord> Suppliers = new List<SupplierRecord>();
        public List<SupplyOrderRecord> SupplyOrders = new List<SupplyOrderRecord>();
        public List<CargoVehicleRecord> CargoVehicles = new List<CargoVehicleRecord>();
        public List<RoadSegmentRecord> PlayerRoadSegments = new List<RoadSegmentRecord>();
        public long TotalPayrollPaid;
        public long TotalBusinessRevenue;
        public long TotalBusinessCosts;
        public int PayrollLastProcessedDay;
        public List<string> PayrollAccruedKeys = new List<string>();
        public List<string> PayrollPaidKeys = new List<string>();
        public List<string> BusinessDayKeys = new List<string>();
        public List<string> ShopperDayKeys = new List<string>();
        public int DroneRiskPreset = 1;
        public int DroneThreatLastProcessedDay;
        public int DroneAlertMinutesRemaining;
        public List<MyCity.Simulation.DroneThreatSystem.DroneThreatRecord> DroneThreatHistory = new List<MyCity.Simulation.DroneThreatSystem.DroneThreatRecord>();
    }
}
