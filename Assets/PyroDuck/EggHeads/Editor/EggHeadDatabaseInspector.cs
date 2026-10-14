using System.Collections.Generic;
using System.Text;
using com.pyroduck.eggheads.Runtime.Scripts.Data;
using UnityEditor;
using UnityEngine;

namespace com.pyroduck.eggheads.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="EggHeadDatabaseSO"/> that adds a
    /// "Validate Database" button. The validator surfaces the most common
    /// authoring mistakes before they reach runtime:
    ///
    /// * null entries in <c>visualGroups</c> / <c>allVisuals</c> / <c>characterPrefabs</c>
    /// * <see cref="VisualDataSO"/> assets with missing prefab or icon
    /// * duplicated <see cref="VisualDataSO.Name"/> values inside a group
    /// * visual groups whose <c>VisualList</c> is empty or has null elements
    /// </summary>
    [CustomEditor(typeof(EggHeadDatabaseSO))]
    public class EggHeadDatabaseInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var db = (EggHeadDatabaseSO)target;
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

            if (GUILayout.Button("Validate Database", GUILayout.Height(28)))
            {
                var report = Validate(db);
                if (report.ErrorCount == 0 && report.WarningCount == 0)
                {
                    EditorUtility.DisplayDialog(
                        "EggHead Database",
                        "No issues found. Database looks healthy.",
                        "OK");
                }
                else
                {
                    Debug.Log(report.ToString(), db);
                    EditorUtility.DisplayDialog(
                        "EggHead Database",
                        $"{report.ErrorCount} error(s), {report.WarningCount} warning(s).\n\nSee Console for details.",
                        "OK");
                }
            }
        }

        public static ValidationReport Validate(EggHeadDatabaseSO db)
        {
            var r = new ValidationReport();
            if (db == null)
            {
                r.Error("Database is null.");
                return r;
            }

            if (db.visualGroups == null || db.visualGroups.Count == 0)
                r.Warn("'visualGroups' list is empty.");
            else
            {
                for (int i = 0; i < db.visualGroups.Count; i++)
                {
                    var g = db.visualGroups[i];
                    if (g == null)
                    {
                        r.Error($"visualGroups[{i}] is null.");
                        continue;
                    }

                    if (g.VisualList == null || g.VisualList.Count == 0)
                    {
                        r.Warn($"Group '{g.name}' has an empty VisualList.");
                        continue;
                    }

                    var nameSeen = new HashSet<string>();
                    for (int j = 0; j < g.VisualList.Count; j++)
                    {
                        var v = g.VisualList[j];
                        if (v == null)
                        {
                            r.Error($"Group '{g.name}' → VisualList[{j}] is null.");
                            continue;
                        }

                        if (v.BodyPartPrefab == null)
                            r.Error($"VisualDataSO '{v.name}' has no BodyPartPrefab assigned.");

                        if (v.IconSprite == null)
                            r.Warn($"VisualDataSO '{v.name}' has no IconSprite (UI buttons will be blank).");

                        if (string.IsNullOrEmpty(v.Name))
                            r.Warn($"VisualDataSO '{v.name}' has an empty Name (save/load uses this).");
                        else if (!nameSeen.Add(v.Name))
                            r.Error($"Group '{g.name}' has duplicate VisualDataSO.Name '{v.Name}'.");
                    }
                }
            }

            if (db.allVisuals != null)
            {
                for (int i = 0; i < db.allVisuals.Count; i++)
                {
                    if (db.allVisuals[i] == null)
                        r.Warn($"allVisuals[{i}] is null.");
                }
            }

            if (db.characterPrefabs == null || db.characterPrefabs.Count == 0)
            {
                r.Warn("'characterPrefabs' list is empty. CharacterSelectUI will have no options.");
            }
            else
            {
                var prefabNames = new HashSet<string>();
                for (int i = 0; i < db.characterPrefabs.Count; i++)
                {
                    var prefab = db.characterPrefabs[i];
                    if (prefab == null)
                    {
                        r.Warn($"characterPrefabs[{i}] is null.");
                        continue;
                    }

                    if (!prefabNames.Add(prefab.name))
                        r.Warn($"characterPrefabs contains duplicate prefab name '{prefab.name}'.");
                }
            }

            return r;
        }

        public class ValidationReport
        {
            private readonly StringBuilder _sb = new StringBuilder();
            public int ErrorCount { get; private set; }
            public int WarningCount { get; private set; }

            public void Error(string msg)
            {
                ErrorCount++;
                _sb.Append("[ERROR] ").AppendLine(msg);
            }

            public void Warn(string msg)
            {
                WarningCount++;
                _sb.Append("[WARN]  ").AppendLine(msg);
            }

            public override string ToString() =>
                $"EggHead Database validation — {ErrorCount} errors, {WarningCount} warnings.\n" + _sb;
        }
    }
}
