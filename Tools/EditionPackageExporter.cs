using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class EditionPackageExporter
{
    [Serializable] private sealed class Job { public string[] paths; public string output; }
    [Serializable] private sealed class Config { public Job[] jobs; }
    public static void Export()
    {
        var config = JsonUtility.FromJson<Config>(File.ReadAllText("edition-export.json"));
        foreach (var job in config.jobs)
        {
            foreach (string path in job.paths)
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                    throw new InvalidOperationException("Asset not imported: " + path);
            // Explicit lists preserve the Lite subset even when this export
            // project's shared folder also contains Full-only code/content.
            AssetDatabase.ExportPackage(job.paths, job.output, ExportPackageOptions.Default);
            Debug.Log("EXPORTED: " + job.output);
        }
    }
}
