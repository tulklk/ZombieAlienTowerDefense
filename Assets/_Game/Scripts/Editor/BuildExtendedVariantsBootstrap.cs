using System.IO;
using UnityEditor;
using UnityEngine;

namespace AlienDefense.EditorTools
{
    /// <summary>One-shot bootstrap: runs Build Variants 5+7+Boss when request file exists.</summary>
    public static class BuildExtendedVariantsBootstrap
    {
        private const string RequestRelative = "Assets/_Game/EditorReports/BuildExtendedVariants.request";

        [InitializeOnLoadMethod]
        private static void OnLoad()
        {
            EditorApplication.delayCall += TryRun;
        }

        private static void TryRun()
        {
            string absolute = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? "",
                RequestRelative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolute) && !File.Exists(RequestRelative))
            {
                return;
            }

            string path = File.Exists(absolute) ? absolute : RequestRelative;
            try
            {
                File.Delete(path);
                string meta = path + ".meta";
                if (File.Exists(meta))
                {
                    File.Delete(meta);
                }
            }
            catch
            {
                // ignore
            }

            Debug.Log("[BuildExtendedVariantsBootstrap] Running Step 10a then 10b.");
            ZombieVariantBuilder.BuildExtendedVariantsPhaseA();
            EditorApplication.delayCall += ZombieVariantBuilder.BuildExtendedVariantsPhaseB;
        }
    }
}
