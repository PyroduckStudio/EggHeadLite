using System;
using com.pyroduck.eggheads.Runtime.Scripts.Character;
using com.pyroduck.eggheads.Runtime.Scripts.Events;
using com.pyroduck.eggheads.Runtime.Scripts.Combat;
using System.Collections.Generic;
using System.Linq;
using com.pyroduck.eggheads.Runtime.Scripts.Enums;
using UnityEngine;

namespace com.pyroduck.eggheads.Runtime.Scripts.Data
{
    [Serializable]
    public class CharacterSaveData
    {
        public List<VisualPartSaveData> Parts = new List<VisualPartSaveData>();
    }

    [Serializable]
    public class VisualPartSaveData
    {
        public VisualType Type;
        public string PartName;
        public Color Color;
    }

    public static class CharacterSerializer
    {
        public static string SaveToJson(CharacterVisualController visualController, CharacterColorizer colorizer, List<VisualGroupsDataSO> availableGroups)
        {
            var data = new CharacterSaveData();
            if (visualController == null || availableGroups == null)
                return JsonUtility.ToJson(data);

            foreach (var group in availableGroups)
            {
                if (group == null) continue;
                var go = visualController.GetCurrentVisual(group.VisualType);
                if (go == null) continue;

                VisualDataSO match = null;

                // Prefer the explicit tag written by CharacterVisualController.CreateItem(VisualDataSO, …).
                var tag = go.GetComponent<Character.VisualInstanceTag>();
                if (tag != null && tag.Source != null)
                    match = tag.Source;

                // Fallback: legacy name-based lookup so saves produced before this change still work.
                if (match == null)
                {
                    match = group.VisualList.FirstOrDefault(v =>
                        v != null && v.BodyPartPrefab != null &&
                        go.name.Contains(v.BodyPartPrefab.name));
                }

                if (match != null)
                {
                    var partSave = new VisualPartSaveData
                    {
                        Type = group.VisualType,
                        PartName = match.Name,
                        Color = colorizer != null ? colorizer.GetColorForVisualType(group.VisualType) : Color.white
                    };
                    data.Parts.Add(partSave);
                }
            }

            return JsonUtility.ToJson(data);
        }

        public static void LoadFromJson(string json, CharacterVisualController visualController, CharacterColorizer colorizer, List<VisualGroupsDataSO> availableGroups)
        {
            if (string.IsNullOrEmpty(json)) return;
            if (visualController == null || availableGroups == null) return;

            CharacterSaveData data;
            try
            {
                data = JsonUtility.FromJson<CharacterSaveData>(json);
            }
            catch
            {
                return;
            }
            if (data == null || data.Parts == null) return;

            foreach (var partSave in data.Parts)
            {
                var group = availableGroups.FirstOrDefault(g => g.VisualType == partSave.Type);
                if (group != null)
                {
                    var visualData = group.VisualList.FirstOrDefault(v => v.Name == partSave.PartName);
                    if (visualData != null)
                    {
                        visualController.CreateItem(visualData, partSave.Type);

                        if (colorizer != null)
                        {
                            colorizer.RefreshForVisualType(partSave.Type);
                            if (visualData.isColorable)
                            {
                                colorizer.SetColorForVisualType(partSave.Type, partSave.Color);
                            }
                        }
                    }
                }
            }
        }
    }
}

