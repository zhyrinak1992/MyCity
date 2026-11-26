using System;
using System.Collections.Generic;
using MyCity.Data;
using UnityEngine;

namespace MyCity.Simulation
{
    /// <summary>Stores explicit family links. Sharing a home never creates kinship automatically.</summary>
    public sealed class CitizenRelationshipSystem : MonoBehaviour
    {
        private const string Spouse = "Spouse";
        private const string ParentChild = "ParentChild";
        private readonly List<CitizenRelationshipRecord> relationships = new List<CitizenRelationshipRecord>();
        private readonly Dictionary<string, CitizenRecord> citizensById = new Dictionary<string, CitizenRecord>();

        public IReadOnlyList<CitizenRelationshipRecord> Relationships { get { return relationships.AsReadOnly(); } }

        public void RegisterCitizen(CitizenRecord citizen)
        {
            if (citizen == null || string.IsNullOrEmpty(citizen.Id)) return;
            citizensById[citizen.Id] = citizen;
        }

        public void Load(IEnumerable<CitizenRecord> citizens, IEnumerable<CitizenRelationshipRecord> saved)
        {
            citizensById.Clear();
            relationships.Clear();
            if (citizens != null)
                foreach (CitizenRecord citizen in citizens)
                    if (citizen != null && !string.IsNullOrEmpty(citizen.Id)) citizensById[citizen.Id] = citizen;
            if (saved == null) return;
            foreach (CitizenRelationshipRecord record in saved)
            {
                if (record == null) continue;
                if (!record.IsActive)
                {
                    if (HasExistingEndpoints(record)) relationships.Add(Clone(record));
                    continue;
                }
                if (record.RelationshipType == Spouse)
                {
                    string a = record.FirstCitizenId;
                    string b = record.SecondCitizenId;
                    if (!CanBeSpouses(a, b) || HasActiveSpouse(a) || HasActiveSpouse(b)) continue;
                    relationships.Add(NormalizeSpouse(record));
                }
                else if (record.RelationshipType == ParentChild)
                {
                    if (!CanBeParentChild(record.ParentCitizenId, record.ChildCitizenId) || WouldCreateParentCycle(record.ParentCitizenId, record.ChildCitizenId)) continue;
                    relationships.Add(CloneParentChild(record));
                }
            }
        }

        public bool AddSpouses(string firstCitizenId, string secondCitizenId, int day, out string error)
        {
            error = null;
            if (!CanBeSpouses(firstCitizenId, secondCitizenId)) { error = "Супругами могут стать два разных совершеннолетних жителя."; return false; }
            if (HasActiveSpouse(firstCitizenId) || HasActiveSpouse(secondCitizenId)) { error = "У одного из жителей уже есть действующая связь супружества."; return false; }
            relationships.Add(new CitizenRelationshipRecord { Id = Guid.NewGuid().ToString("N"), RelationshipType = Spouse,
                FirstCitizenId = string.CompareOrdinal(firstCitizenId, secondCitizenId) <= 0 ? firstCitizenId : secondCitizenId,
                SecondCitizenId = string.CompareOrdinal(firstCitizenId, secondCitizenId) <= 0 ? secondCitizenId : firstCitizenId,
                StartedDay = Math.Max(1, day), IsActive = true });
            return true;
        }

        public bool AddParentChild(string parentCitizenId, string childCitizenId, int day, out string error)
        {
            error = null;
            if (!CanBeParentChild(parentCitizenId, childCitizenId)) { error = "Родитель должен быть старше ребёнка минимум на 16 лет; жители должны существовать."; return false; }
            if (WouldCreateParentCycle(parentCitizenId, childCitizenId)) { error = "Связь создала бы циклическое родство."; return false; }
            if (HasParentLink(parentCitizenId, childCitizenId)) { error = "Такая связь уже существует."; return false; }
            relationships.Add(new CitizenRelationshipRecord { Id = Guid.NewGuid().ToString("N"), RelationshipType = ParentChild,
                ParentCitizenId = parentCitizenId, ChildCitizenId = childCitizenId, StartedDay = Math.Max(1, day), IsActive = true });
            return true;
        }

        public bool EndRelationship(string relationshipId)
        {
            for (int i = 0; i < relationships.Count; i++)
                if (relationships[i] != null && relationships[i].Id == relationshipId && relationships[i].IsActive)
                { relationships[i].IsActive = false; return true; }
            return false;
        }

        public void Export(List<CitizenRelationshipRecord> destination)
        {
            if (destination == null) return;
            destination.Clear();
            foreach (CitizenRelationshipRecord record in relationships) destination.Add(Clone(record));
        }

        private bool HasExistingEndpoints(CitizenRelationshipRecord record)
        {
            if (record.RelationshipType == Spouse)
                return citizensById.ContainsKey(record.FirstCitizenId) && citizensById.ContainsKey(record.SecondCitizenId);
            if (record.RelationshipType == ParentChild)
                return citizensById.ContainsKey(record.ParentCitizenId) && citizensById.ContainsKey(record.ChildCitizenId);
            return false;
        }

        private bool CanBeSpouses(string a, string b)
        {
            CitizenRecord first, second;
            return !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && a != b &&
                citizensById.TryGetValue(a, out first) && citizensById.TryGetValue(b, out second) && first.Age >= 18 && second.Age >= 18;
        }

        private bool CanBeParentChild(string parentId, string childId)
        {
            CitizenRecord parent, child;
            return !string.IsNullOrEmpty(parentId) && !string.IsNullOrEmpty(childId) && parentId != childId &&
                citizensById.TryGetValue(parentId, out parent) && citizensById.TryGetValue(childId, out child) &&
                parent.Age >= 18 && parent.Age - child.Age >= 16;
        }

        private bool HasActiveSpouse(string citizenId)
        {
            foreach (CitizenRelationshipRecord r in relationships)
                if (r != null && r.IsActive && r.RelationshipType == Spouse && (r.FirstCitizenId == citizenId || r.SecondCitizenId == citizenId)) return true;
            return false;
        }

        private bool HasParentLink(string parentId, string childId)
        {
            foreach (CitizenRelationshipRecord r in relationships)
                if (r != null && r.IsActive && r.RelationshipType == ParentChild && r.ParentCitizenId == parentId && r.ChildCitizenId == childId) return true;
            return false;
        }

        private bool WouldCreateParentCycle(string parentId, string childId)
        {
            // Adding parent -> child is invalid if parent is already a descendant of child.
            HashSet<string> visited = new HashSet<string>();
            Stack<string> pending = new Stack<string>();
            pending.Push(childId);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (!visited.Add(current)) continue;
                if (current == parentId) return true;
                foreach (CitizenRelationshipRecord r in relationships)
                    if (r != null && r.IsActive && r.RelationshipType == ParentChild && r.ParentCitizenId == current && !string.IsNullOrEmpty(r.ChildCitizenId)) pending.Push(r.ChildCitizenId);
            }
            return false;
        }

        private static CitizenRelationshipRecord NormalizeSpouse(CitizenRelationshipRecord source)
        {
            CitizenRelationshipRecord copy = Clone(source);
            if (string.CompareOrdinal(copy.FirstCitizenId, copy.SecondCitizenId) > 0)
            { string swap = copy.FirstCitizenId; copy.FirstCitizenId = copy.SecondCitizenId; copy.SecondCitizenId = swap; }
            return copy;
        }

        private static CitizenRelationshipRecord CloneParentChild(CitizenRelationshipRecord source) { return Clone(source); }
        private static CitizenRelationshipRecord Clone(CitizenRelationshipRecord source)
        {
            return new CitizenRelationshipRecord { Id = source.Id, RelationshipType = source.RelationshipType,
                FirstCitizenId = source.FirstCitizenId, SecondCitizenId = source.SecondCitizenId,
                ParentCitizenId = source.ParentCitizenId, ChildCitizenId = source.ChildCitizenId,
                StartedDay = source.StartedDay, IsActive = source.IsActive };
        }
    }
}
