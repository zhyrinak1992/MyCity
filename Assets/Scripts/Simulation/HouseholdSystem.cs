using System;
using System.Collections.Generic;
using MyCity.Data;
using UnityEngine;

namespace MyCity.Simulation
{
    /// <summary>Builds persistent household groups from shared homes. Wallets remain individually owned.</summary>
    public sealed class HouseholdSystem : MonoBehaviour
    {
        private readonly Dictionary<string, HouseholdRecord> households = new Dictionary<string, HouseholdRecord>();
        public int HouseholdCount { get { return households.Count; } }
        public IEnumerable<HouseholdRecord> Households { get { return households.Values; } }

        public void Rebuild(IEnumerable<CitizenRecord> citizens, int day)
        {
            households.Clear();
            if (citizens == null) return;
            foreach (CitizenRecord citizen in citizens)
            {
                if (citizen == null || citizen.IsDeceased || string.IsNullOrEmpty(citizen.Id)) continue;
                string homeId = citizen.HomeBuildingId;
                string householdId = string.IsNullOrEmpty(homeId) ? "person:" + citizen.Id : "home:" + homeId;
                citizen.HouseholdId = householdId;
                HouseholdRecord household;
                if (!households.TryGetValue(householdId, out household))
                {
                    household = new HouseholdRecord { Id = householdId, HomeBuildingId = homeId, LastAggregatedDay = Math.Max(1, day) };
                    households.Add(householdId, household);
                }
                household.MemberCitizenIds.Add(citizen.Id);
                household.MemberCount++;
                if (citizen.Age >= 18) household.AdultCount++; else household.ChildCount++;
                household.CombinedPersonalWallets = checked(household.CombinedPersonalWallets + Math.Max(0L, citizen.WalletBalance));
                household.FoodNeed += Mathf.Clamp(citizen.FoodNeed, 0f, 100f);
                household.ClothingNeed += Mathf.Clamp(citizen.ClothingNeed, 0f, 100f);
                household.HomeGoodsNeed += Mathf.Clamp(citizen.HomeGoodsNeed, 0f, 100f);
                household.LeisureNeed += Mathf.Clamp(citizen.LeisureNeed, 0f, 100f);
            }
            foreach (HouseholdRecord household in households.Values)
            {
                if (household.MemberCount <= 0) continue;
                BuildHouseholdRoles(household, citizens);
                float divisor = household.MemberCount;
                household.FoodNeed /= divisor;
                household.ClothingNeed /= divisor;
                household.HomeGoodsNeed /= divisor;
                household.LeisureNeed /= divisor;
            }
        }

        private static void BuildHouseholdRoles(HouseholdRecord household, IEnumerable<CitizenRecord> citizens)
        {
            household.Members.Clear();
            household.HouseholdHeadCitizenId = null;
            Dictionary<string, CitizenRecord> byId = new Dictionary<string, CitizenRecord>();
            foreach (CitizenRecord citizen in citizens)
                if (citizen != null && !string.IsNullOrEmpty(citizen.Id)) byId[citizen.Id] = citizen;

            CitizenRecord oldestAdult = null;
            for (int i = 0; i < household.MemberCitizenIds.Count; i++)
            {
                CitizenRecord member;
                if (!byId.TryGetValue(household.MemberCitizenIds[i], out member)) continue;
                if (member.Age >= 18 && (oldestAdult == null || member.Age > oldestAdult.Age ||
                    (member.Age == oldestAdult.Age && string.CompareOrdinal(member.Id, oldestAdult.Id) < 0)))
                    oldestAdult = member;
            }

            if (oldestAdult != null) household.HouseholdHeadCitizenId = oldestAdult.Id;
            for (int i = 0; i < household.MemberCitizenIds.Count; i++)
            {
                CitizenRecord member;
                if (!byId.TryGetValue(household.MemberCitizenIds[i], out member)) continue;
                HouseholdMemberRecord role = new HouseholdMemberRecord { CitizenId = member.Id };
                if (oldestAdult == null)
                    role.Role = "Несовершеннолетний участник";
                else if (member.Id == oldestAdult.Id)
                    role.Role = "Ответственный взрослый";
                else if (member.Age < 18)
                {
                    role.Role = "Несовершеннолетний участник";
                    role.GuardianCitizenId = oldestAdult.Id;
                }
                else
                    role.Role = "Другой взрослый участник";
                household.Members.Add(role);
            }
        }

        public void Export(List<HouseholdRecord> destination)
        {
            if (destination == null) return;
            destination.Clear();
            foreach (HouseholdRecord household in households.Values) destination.Add(household);
        }

        public void Load(IEnumerable<CitizenRecord> citizens, int day)
        {
            // Recompute from authoritative citizen records; never trust stale aggregate totals from a save.
            Rebuild(citizens, day);
        }

        public HouseholdRecord GetForCitizen(string citizenId)
        {
            foreach (HouseholdRecord household in households.Values)
                if (household.MemberCitizenIds.Contains(citizenId)) return household;
            return null;
        }
    }
}
