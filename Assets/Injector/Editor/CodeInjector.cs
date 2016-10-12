#if UNITY_4 || UNITY_4_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_4 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7 || UNITY_4_8 || UNITY_4_9
#define UNITY_4_X
#endif

#region Using Directives

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

#endregion

[InitializeOnLoad]
public static class CodeInjector
{
    private static string _projectName
    {
        get { return Application.dataPath; }
    }

    #region Settings

    public static List<string> CustomAssembliesList
    {
        get
        {
            List<string> result = new List<string>();
            foreach (string s in _assembliesSettingList)
            {
                if (!s.EndsWith(".dll"))
                {
                    result.Add(s + ".dll");
                }
                else
                {
                    result.Add(s);
                }
            }
            return result;
        }
    }

    private static List<string> _assembliesSettingList;
    private static List<string> _selectTypes;
    private static List<string> _skipTypes;

    public static bool CreateBackup
    {
        set
        {
            EditorPrefs.SetBool(_projectName + "CodeInjector CreateBackup", value);
        }
        get { return EditorPrefs.GetBool(_projectName + "CodeInjector CreateBackup", true); }
    }

    #endregion

    public static CodeInjectorSetup CodeInjectorSetupSettings()
    {
        CodeInjectorSetup setup = new CodeInjectorSetup();
        BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;

        if (buildTarget == BuildTarget.Android)
        {
            string enginePath = PathEx.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines");
            if (EditorUserBuildSettings.development)
            {
                enginePath = PathEx.Combine(enginePath, "androiddevelopmentplayer");
            }
            else
                enginePath = PathEx.Combine(enginePath, "androidplayer");
            enginePath = PathEx.Combine(enginePath, "Managed");
            // ÃÌº”Unity“˝«Ê“¿¿µ
            setup.AddAssemblySearchDirectory(enginePath);
            setup.BuildTarget = "Android";
        }
        else if (buildTarget == BuildTarget.iPhone)
        {
            string enginePath = PathEx.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines");
            enginePath = PathEx.Combine(enginePath, "iossupport");
            enginePath = PathEx.Combine(enginePath, "Managed");
            // ÃÌº”Unity“˝«Ê“¿¿µ
            setup.AddAssemblySearchDirectory(enginePath);
            setup.BuildTarget = "iPhone";
        }
        else
        {
            string enginePath = Path.Combine(EditorApplication.applicationContentsPath, "Managed");
            // ÃÌº”Unity“˝«Ê“¿¿µ
            setup.AddAssemblySearchDirectory(enginePath);
            setup.BuildTarget = "Standalone";
        }

        return setup;
    }


    private static readonly string[] editorAssemblies =
    {
        "Assembly-CSharp-Editor.dll", "Assembly-CSharp-firstpass.dll"
    };

    private static bool DoCodeInjector(string fromPath, string resultPath)
    {
        float progress = 0.0f;
        EditorUtility.DisplayProgressBar("CodeInjector", "Obfuscating and protecting code...", progress);

        CodeInjectorSetup setup = CodeInjector.CodeInjectorSetupSettings();

        string[] files = Directory.GetFiles(fromPath, "*.dll", SearchOption.TopDirectoryOnly);
        for (int index = 0; index < files.Length; index++)
        {
            if (editorAssemblies.Contains(Path.GetFileName(files[index])))
            {
            }
            else
            {
                setup.AddAssembly(files[index]);
            }
        }
        // ‘¥¬∑æ∂ÃÌº”Œ™“¿¿µ
        setup.AddAssemblySearchDirectory(fromPath);
        setup.OutputDirectory = resultPath;

        progress = 0.25f;
        EditorUtility.DisplayProgressBar("CodeInjector", "injectoring and generating code...", progress);

        EditorUtility.ClearProgressBar();
        setup.Run();

        EditorUtility.ClearProgressBar();
        return true;
    }

    public static void DoCodeInjectorFolder(string folderPath)
    {
        DoCodeInjectorFolder(folderPath, false);
    }
    public static void DoCodeInjectorFolder(string folderPath, bool createBackup)
    {
        DirectoryInfo assemblyDir = new DirectoryInfo(folderPath);
        string outputPath = assemblyDir.Parent.FullName + Path.DirectorySeparatorChar + "CodeInjectored";

        DoCodeInjector(folderPath, outputPath);

        if (createBackup)
        {
            // Create backup folder
            DirectoryInfo backupDir = assemblyDir.Parent.CreateSubdirectory("CodeInjector Backups");
            CopyFilesFromDirectory(assemblyDir, backupDir);
        }

        // Copy from CodeInjectored to Managed
        DirectoryInfo codeInjectoredDir = new DirectoryInfo(outputPath);
        CopyFilesFromDirectory(codeInjectoredDir, assemblyDir);

        // Delete CodeInjectored
        codeInjectoredDir.Delete(true);

        Debug.Log(CodeInjectorReporter.LoggedError
                      ? "CodeInjector: Failed to injector and generate the assemblies."
                      : "CodeInjector: Finished injectoring and generating assemblies.");
    }

    private static bool DoCodeInjectorBuild(string plaformTag)
    {
        string scriptsPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library");
        scriptsPath = Path.Combine(scriptsPath, "ScriptAssemblies");

        List<string> files = new List<string>();
        files.AddRange(Directory.GetFiles(scriptsPath, "*.dll"));

        string tmpPath = FileUtil.GetUniqueTempPathInProject() + "-Tmp" + plaformTag;
        Directory.CreateDirectory(tmpPath);

        foreach (string file in files)
        {
            File.Copy(file, Path.Combine(tmpPath, Path.GetFileName(file)), true);
        }

        string codeInjectoredTmp = FileUtil.GetUniqueTempPathInProject() + "-CodeInjectored" + plaformTag;
        Directory.CreateDirectory(codeInjectoredTmp);

        if (!DoCodeInjector(tmpPath, codeInjectoredTmp))
        {
            return false;
        }

        string[] scripts = Directory.GetFiles(scriptsPath, "*.dll");
        foreach (string script in scripts)
        {
            var tmpFile = Path.Combine(codeInjectoredTmp, Path.GetFileName(script));
            if (File.Exists(tmpFile))
            {
                File.Copy(tmpFile, script, true);
            }
        }

        // Delete temporary files
        Directory.Delete(tmpPath, true);
        Directory.Delete(codeInjectoredTmp, true);

        return true;
    }

    // Post build
    [PostProcessBuild(1000)]
    private static void OnPostprocessBuildPlayer(BuildTarget buildTarget, string buildPath)
    {
        Debug.Log("PostProcessBuild::OnPostprocessBuildPlayer");

#if UNITY_4_X
        bool windowsOrLinux = (buildTarget == BuildTarget.StandaloneWindows || buildTarget == BuildTarget.StandaloneWindows64 ||
                               buildTarget == BuildTarget.StandaloneLinux || buildTarget == BuildTarget.StandaloneLinux64 || buildTarget == BuildTarget.StandaloneLinuxUniversal);
#else
        bool windowsOrLinux = (buildTarget == BuildTarget.StandaloneWindows ||
                               buildTarget == BuildTarget.StandaloneWindows64);
#endif

        if (windowsOrLinux)
        {
            var buildDir = new FileInfo(buildPath).Directory;

            DirectoryInfo dataDir = buildDir.GetDirectories(Path.GetFileNameWithoutExtension(buildPath) + "_Data")[0];

            if (CreateBackup)
            {
                // Create backup
                DirectoryInfo backupDir = new DirectoryInfo(dataDir.FullName + " Backup");
                if (!CopyFilesFromDirectory(dataDir, backupDir, true))
                {
                    Debug.LogError("CodeInjector: Failed to create backup, stopping post-build injection and protection.");
                    return;
                }
            }

            DirectoryInfo managedDir = new DirectoryInfo(dataDir.FullName + Path.DirectorySeparatorChar + "Managed");

            DoCodeInjectorFolder(managedDir.FullName);
        }
        else if (buildTarget == BuildTarget.StandaloneOSXIntel)
        {
            FileInfo buildFileInfo = new FileInfo(buildPath);

            if (CreateBackup)
            {
                // Create backup
                DirectoryInfo appDir = new DirectoryInfo(buildFileInfo.FullName);
                DirectoryInfo backupDir = new DirectoryInfo(buildFileInfo.FullName + " Backup");
                if (!CopyFilesFromDirectory(appDir, backupDir, true))
                {
                    Debug.LogError("CodeInjector: Failed to create backup, stopping post-build injection and protection.");
                    return;
                }
            }

            DirectoryInfo dataDir =
                new DirectoryInfo(buildFileInfo.FullName + Path.DirectorySeparatorChar + "Contents" +
                                  Path.DirectorySeparatorChar + "Data");

            DirectoryInfo managedDir = new DirectoryInfo(dataDir.FullName + Path.DirectorySeparatorChar + "Managed");

            DoCodeInjectorFolder(managedDir.FullName);
        }
        else if (buildTarget == BuildTarget.Android || buildTarget == BuildTarget.iPhone)
        {
            _hasMidCodeInjectored = false;
        }
        else
        {
            Debug.LogWarning("CodeInjector: Post-build injection is not yet implemented for: " + buildTarget);
            return;
        }

        Debug.Log("CodeInjector: Post-build injection and protection finished.");
    }

    [MenuItem("DllInjector/Run")]
    private static void TestMidCodeInjectoring()
    {
        MidCodeInjectoring();
    }

    private static bool _hasMidCodeInjectored;

    [PostProcessScene]
    private static void MidCodeInjectoring()
    {
        if (_hasMidCodeInjectored) return;
        Debug.Log("PostProcessBuild::OnPostProcessScene");

        // Don't CodeInjector when in Editor and pressing Play
        if (Application.isPlaying || EditorApplication.isPlaying) return;
        //if (!EditorApplication.isCompiling) return;

        BuildTarget buildTarget = EditorUserBuildSettings.activeBuildTarget;

        if (buildTarget == BuildTarget.Android)
        {
            if (DoCodeInjectorBuild("Android"))
            {
                _hasMidCodeInjectored = true;
            }
            else
            {
                Debug.LogWarning("CodeInjector: Failed to inject Android build!");
            }
        }
        else if (buildTarget == BuildTarget.iPhone)
        {
            if (DoCodeInjectorBuild("iOS"))
            {
                _hasMidCodeInjectored = true;
            }
            else
            {
                Debug.LogWarning("CodeInjector: Failed to inject iOS build!");
            }
        }
    }

    // Helper methods
    private static bool CopyFilesFromDirectory(DirectoryInfo source, DirectoryInfo destination)
    {
        return CopyFilesFromDirectory(source, destination, false, true);
    }
    private static bool CopyFilesFromDirectory(DirectoryInfo source, DirectoryInfo destination, bool copyDirectories)
    {
        return CopyFilesFromDirectory(source, destination, copyDirectories, true);
    }

    private static bool CopyFilesFromDirectory(DirectoryInfo source, DirectoryInfo destination,
                                               bool copyDirectories, bool replace)
    {
        if (!source.Exists)
        {
            Debug.LogError("CodeInjector: Cannot copy from " + source + " since it doesn't exists!");
            return false;
        }

        if (!destination.Exists)
        {
            destination.Create();
        }

        // Copy all files.
        FileInfo[] files = source.GetFiles();
        for (int index = 0; index < files.Length; index++)
        {
            FileInfo file = files[index];
            file.CopyTo(Path.Combine(destination.FullName,
                                     file.Name), replace);
        }

        if (copyDirectories)
        {
            DirectoryInfo[] dirs = source.GetDirectories();
            for (int index = 0; index < dirs.Length; index++)
            {
                DirectoryInfo directory = dirs[index];
                string destinationDir = Path.Combine(destination.FullName, directory.Name);

                CopyFilesFromDirectory(directory, new DirectoryInfo(destinationDir), true, replace);
            }
        }

        return true;
    }
}

public class CodeInjectorReporter
{
    public static bool LoggedError { get; set; }
}
