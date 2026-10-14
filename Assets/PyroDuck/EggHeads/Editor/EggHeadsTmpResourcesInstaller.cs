using System;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace com.pyroduck.eggheads.Editor
{
    /// <summary>
    /// Imports TMP's project-level essential resources when EggHeads is installed
    /// into a project that does not already have TMP settings.
    /// </summary>
    [InitializeOnLoad]
    internal static class EggHeadsTmpResourcesInstaller
    {
        private const string TmpSettingsResourceName = "TMP Settings";

        static EggHeadsTmpResourcesInstaller()
        {
            EditorApplication.update += WaitForEditorAndImport;
        }

        private static void WaitForEditorAndImport()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            EditorApplication.update -= WaitForEditorAndImport;

            if (Resources.Load<TMP_Settings>(TmpSettingsResourceName) != null)
                return;

            try
            {
                TMP_PackageResourceImporter.ImportResources(
                    importEssentials: true,
                    importExamples: false,
                    interactive: false);

                Debug.Log("[EggHeads] Imported TextMesh Pro Essential Resources.");
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[EggHeads] TextMesh Pro Essential Resources could not be imported automatically. " +
                    "Use Window > TextMeshPro > Import TMP Essential Resources to import them manually.\n" +
                    exception);
            }
        }
    }
}
