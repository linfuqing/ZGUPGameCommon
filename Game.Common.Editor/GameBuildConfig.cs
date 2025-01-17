using System;
using System.IO;
using UnityEngine;
using UnityEditor;

[CreateAssetMenu(menuName = "Game/Game Build Config", fileName = "GameBuildConfig")]
public partial class GameBuildConfig : ScriptableObject
{
    [Serializable]
    public class Channel
    {
        [Serializable]
        public struct ConfigFolder
        {
            public BuildTarget buildTarget;

            public bool isClearBeforeApply;

            public string sourcePath;
            public string destinationPath;

            public void Apply(string folderPath, BuildTarget buildTarget)
            {
                if (this.buildTarget != 0 && this.buildTarget != buildTarget)
                    return;

                var path = Path.Combine(Application.dataPath, destinationPath);
                if (isClearBeforeApply)
                {
                    if (Directory.Exists(path))
                        Directory.Delete(path, true);
                }

                CopyFolder(Path.Combine(folderPath, sourcePath), path);
            }
        }

        [Serializable]
        public struct SplashScreenLogo
        {
            public float duration;

            public Sprite sprite;

            public PlayerSettings.SplashScreenLogo Create() => PlayerSettings.SplashScreenLogo.Create(duration, sprite);
        }

        public string name;
        public string productName;
        public string packageName;
        public ConfigFolder[] configFolders;
        public SplashScreenLogo[] splashScreenLogos;

        public static void CopyFolder(string sourceFolder, string destinationFolder)
        {
            try
            {
                //如果目标路径不存在,则创建目标路径
                if (!Directory.Exists(destinationFolder))
                    Directory.CreateDirectory(destinationFolder);

                //得到原文件根目录下的所有文件
                string[] files = Directory.GetFiles(sourceFolder);
                foreach (string file in files)
                    File.Copy(file, Path.Combine(destinationFolder, Path.GetFileName(file)), true);//复制文件

                //得到原文件根目录下的所有文件夹
                string[] folders = Directory.GetDirectories(sourceFolder);
                foreach (string folder in folders)
                    CopyFolder(folder, Path.Combine(destinationFolder, Path.GetFileName(folder)));//构建目标路径,递归复制文件
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }

        public void ApplyConfigFolders(string folderPath, BuildTarget buildTarget)
        {
            if (configFolders == null)
                return;

            foreach(var configFolder in configFolders)
                configFolder.Apply(folderPath, buildTarget);
        }

        public void ApplySplashScreen()
        {
            int numSplashScreenLogos = splashScreenLogos.Length;
            var logos = new PlayerSettings.SplashScreenLogo[numSplashScreenLogos + 1];
            logos[0] = PlayerSettings.SplashScreenLogo.CreateWithUnityLogo();
            for (int i = 0; i < numSplashScreenLogos; ++i)
                logos[i + 1] = splashScreenLogos[i].Create();

            PlayerSettings.SplashScreen.logos = logos;
        }
    }

    public Channel[] channels;
}
