using System.Collections.Generic;
using MyCity.Core;
using MyCity.Data;
using UnityEngine;

namespace MyCity.Building
{
    public sealed class BuildingPlacementService
    {
        private readonly HashSet<string> occupiedCells = new HashSet<string>();

        public bool CanPlace(BuildingDefinition definition, int gridX, int gridZ, int quarterTurns, out string reason)
        {
            reason = string.Empty;
            if (definition == null) { reason = "Не выбрано здание."; return false; }

            int width = (quarterTurns % 2 == 0) ? definition.FootprintWidth : definition.FootprintDepth;
            int depth = (quarterTurns % 2 == 0) ? definition.FootprintDepth : definition.FootprintWidth;

            for (int x = 0; x < width; x++)
                for (int z = 0; z < depth; z++)
                    if (occupiedCells.Contains(CellKey(gridX + x, gridZ + z)))
                    {
                        reason = "Участок уже занят.";
                        return false;
                    }
            return true;
        }

        public bool Place(BuildingDefinition definition, int gridX, int gridZ, int quarterTurns,
            int sectorX, int sectorZ, out BuildingRecord record, out string reason)
        {
            record = null;
            if (!CanPlace(definition, gridX, gridZ, quarterTurns, out reason)) return false;

            int width = (quarterTurns % 2 == 0) ? definition.FootprintWidth : definition.FootprintDepth;
            int depth = (quarterTurns % 2 == 0) ? definition.FootprintDepth : definition.FootprintWidth;
            for (int x = 0; x < width; x++)
                for (int z = 0; z < depth; z++)
                    occupiedCells.Add(CellKey(gridX + x, gridZ + z));

            record = new BuildingRecord
            {
                Id = StableId.NewId().ToString(),
                DefinitionId = definition.Id,
                GridX = gridX,
                GridZ = gridZ,
                RotationQuarterTurns = ((quarterTurns % 4) + 4) % 4,
                SectorX = sectorX,
                SectorZ = sectorZ
            };
            GameEventBus.Publish(new GameEvent(GameEventType.BuildingPlaced, 0, "construction", record.Id, definition.Id));
            return true;
        }

        private static string CellKey(int x, int z) { return x + ":" + z; }
    }
}
