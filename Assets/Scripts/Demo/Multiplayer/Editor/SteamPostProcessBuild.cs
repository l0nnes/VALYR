using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace VALYR.Multiplayer.Editor
{
    public static class SteamPostProcessBuild
    {
        [PostProcessBuild(100)]
        public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64)
                return;

            string buildDir = Path.GetDirectoryName(pathToBuiltProject);
            if (string.IsNullOrEmpty(buildDir) || !Directory.Exists(buildDir))
                return;

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string sourceFile = Path.Combine(projectRoot, "steam_appid.txt");
            string destFile = Path.Combine(buildDir, "steam_appid.txt");

            if (File.Exists(sourceFile))
            {
                File.Copy(sourceFile, destFile, true);
                Debug.Log($"[SteamPostProcessBuild] Copied steam_appid.txt to build directory: {destFile}");
            }
            else
            {
                File.WriteAllText(destFile, "480");
                Debug.Log($"[SteamPostProcessBuild] Created default steam_appid.txt (480) in build directory: {destFile}");
            }
        }
    }
}
