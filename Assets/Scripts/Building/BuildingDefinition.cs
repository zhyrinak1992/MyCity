using UnityEngine;

namespace MyCity.Building
{
    [CreateAssetMenu(menuName = "My City/Building Definition", fileName = "BuildingDefinition")]
    public sealed class BuildingDefinition : ScriptableObject
    {
        public string Id;
        public string DisplayName;
        [Min(1)] public int FootprintWidth = 2;
        [Min(1)] public int FootprintDepth = 2;
        [Min(1)] public int MaximumLevels = 1;
        [Min(0)] public int ConstructionCost = 100;
        [Min(0)] public int MonthlyMaintenance;
        [Min(0)] public int Capacity;
        public GameObject Prefab;
    }
}
