using System;
using System.IO;
using UnityEngine;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public class GameBuildProcessor : EditorWindow
{
    public struct Processor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder
        {
            get
            {
                return -1;
            }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report == null || buildConfig == null)
                return;

            var buildChannel = GameBuildProcessor.buildChannel;
            if (buildChannel < 1)
                return;

            var channel = buildConfig.channels[buildChannel - 1];

            if (!string.IsNullOrEmpty(channel.productName))
                PlayerSettings.productName = channel.productName;

            if(!string.IsNullOrEmpty(channel.packageName))
                PlayerSettings.applicationIdentifier = channel.packageName;

            string dataPath = Application.dataPath, folderPath = dataPath;
            folderPath = folderPath.Substring(0, folderPath.Length - "Assets".Length);
            folderPath += "Platform";
            channel.ApplyConfigFolders(folderPath, report.summary.platform);
            channel.ApplySplashScreen();
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report == null)
                return;

            BuildSummary summary = report.summary;
            switch (summary.platformGroup)
            {
                case BuildTargetGroup.iOS:
#if UNITY_IOS
                string path = Path.Combine(Path.GetFullPath(pathToBuiltProject), "Info.plist");
                PlistDocument plistDocument = new PlistDocument();
                plistDocument.ReadFromFile(path);
                PlistElementDict plistElementDict = plistDocument.root;
                PlistElementArray plistElementArray = plistElementDict?.CreateArray("LSApplicationQueriesSchemes");
                if (plistElementArray != null)
                {
                    plistElementArray.AddString("weixin");

                    plistDocument.WriteToFile(path);
                }
#endif
                    break;
            }
        }
    }

    private const string __NAME_SPACE_BUILD_CHANNEL = "GameBuildProcessorChannel";
    private const string __NAME_SPACE_BUILD_CONFIG = "GameBuildProcessorConfig";

    public static int buildChannel
    {
        get
        {
            return EditorPrefs.GetInt(__NAME_SPACE_BUILD_CHANNEL);
        }

        set
        {
            EditorPrefs.SetInt(__NAME_SPACE_BUILD_CHANNEL, value);
        }
    }

    public static GameBuildConfig buildConfig
    {
        get
        {
            return AssetDatabase.LoadAssetAtPath<GameBuildConfig>(EditorPrefs.GetString(__NAME_SPACE_BUILD_CONFIG));
        }

        set
        {
            EditorPrefs.SetString(__NAME_SPACE_BUILD_CONFIG, AssetDatabase.GetAssetPath(value));
        }
    }

    void OnGUI()
    {
        buildConfig = (GameBuildConfig)EditorGUILayout.ObjectField(buildConfig, typeof(GameBuildConfig), false);
        int numChannels = buildConfig == null || buildConfig.channels == null ? 0 : buildConfig.channels.Length;
        if (numChannels > 0)
        {
            string[] channelNames = new string[numChannels + 1];
            channelNames[0] = "None";
            for (int i = 0; i < numChannels; ++i)
                channelNames[i + 1] = buildConfig.channels[i].name;
            
            buildChannel = EditorGUILayout.Popup(buildChannel, channelNames);
        }
    }

    [MenuItem("Window/Game/Game Build Processor")]
    public static void GetWindow()
    {
        GetWindow<GameBuildProcessor>();
    }

    [MenuItem("Assets/Build/DetailedBuildAndReportWin64")]
    public static void DetailedBuildAndReportWin64()
    {
        string path = EditorUtility.SaveFilePanel("Build", string.Empty, PlayerSettings.productName, "exe");

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = new[] { AssetDatabase.GetAssetPath(Selection.activeObject) };
        buildPlayerOptions.locationPathName = path;
        buildPlayerOptions.target = BuildTarget.StandaloneWindows64;

        buildPlayerOptions.options = BuildOptions.DetailedBuildReport;

        var buildReport = BuildPipeline.BuildPlayer(buildPlayerOptions);

        Debug.Log(buildReport);
    }
}
