using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using ZG;

public interface IGameAssetUnzipper
{
    string filename { get; }
    
    IEnumerator Execute(AssetBundle assetBundle, AssetManager.DownloadHandler downloadHandler);
}

public interface IGameSceneLoader
{
    bool isDone { get; }
    
    float progress { get; }
}

public class GameAssetManager : MonoBehaviour
{
    private struct Verifier
    {
        public string name
        {
            get;

            private set;
        }

        public int index
        {
            get;

            private set;
        }

        public int count
        {
            get;

            private set;
        }

        public static IEnumerator Start(AssetManager assetManager, string format)
        {
            Verifier verifier = default;

#if UNITY_WEBGL
            assetManager.Verify(verifier.__Change);

            return null;
#else
            var progressbar = GameProgressbar.instance;
            progressbar.ShowProgressBar(GameProgressbar.ProgressbarType.Verify);

            using (var task = Task.Run(() => assetManager.Verify(verifier.__Change)))
            {
                do
                {
                    yield return null;

                    progressbar.UpdateProgressBar(GameProgressbar.ProgressbarType.Verify, verifier.index * 1.0f / verifier.count, verifier.ToString(format));

                } while (!task.IsCompleted);
            }

            progressbar.ClearProgressBar();
#endif
        }

        public string ToString(string format)
        {
            return string.Format(format, name, index, count);
        }

        private void __Change(string name, int index, int count)
        {
            this.name = name;
            this.index = index;
            this.count = count;
        }
    }

    private struct Tachometer
    {
        public const float DELTA_TIME = 1.0f;

        private float __value;
        private float __time;
        private ulong __totalBytesDownload;

        public float value => __value;

        public void Start()
        {
            __time = Time.time;
            __totalBytesDownload = 0;
        }

        public float Update(ulong totalBytesDownload)
        {
            if (totalBytesDownload > __totalBytesDownload)
            {
                float time = Time.unscaledTime, deltaTime = time - __time;
                if (deltaTime > DELTA_TIME)
                {
                    __value = (totalBytesDownload - __totalBytesDownload) / deltaTime;

                    __totalBytesDownload = totalBytesDownload;
                    __time = time;
                }
            }

            return __value;
        }
    }

    public struct AssetPath
    {
        public string value;
        public string filePrefix;
        public string urlPrefix;

        public string filePath
        {
            get
            {
                return string.IsNullOrEmpty(filePrefix) ? value : Path.Combine(filePrefix, value);
            }
        }

        public string url
        {
            get
            {
                string url = value.Replace('\\', '/');
                return string.IsNullOrEmpty(urlPrefix) ? url : $"{urlPrefix}/{url}";
            }
        }

        public AssetPath(string value, string filePrefix = null, string urlPrefix = null)
        {
            this.value = value;
            this.filePrefix = filePrefix;
            this.urlPrefix = urlPrefix;
        }
    }

    public event Action onConfirmCancel;

    public event Action onLoadAssetsStart;
    public event Action onLoadAssetsEnd;
    public event Action onLoadAssetsFinish;

    [UnityEngine.Serialization.FormerlySerializedAs("onComfirm")]
    public StringEvent onConfirm;

    public string verifyProgressFormat = "{1}/{2}";

    public string recompressProgressFormat = "{4:C2}/{5:C2}M({6}/{7})";//{0}:{1:P} 

    public string unzipProgressFormat = "{4:C2}/{5:C2}M({6}/{7})";//{0}:{1:P} 

    public string downloadProgressFormat = "{4:C2}/{5:C2}M({6}/{7}) {8:C2}M/S";//{0}:{1:P} 

    //public float timeout;

    private bool __isMissingConfirm;
    private bool __isConfirm;

    private int __sceneCoroutineIndex = -1;
    private Action __onSceneLoadedComplete;
    private Coroutine __assetCoroutine;

    private Tachometer __tachometer;

    private AssetManager __assetManager;

    private Queue<IGameSceneLoader> __sceneLoaders = new Queue<IGameSceneLoader>();

    public static GameAssetManager instance
    {
        get;

        private set;
    }

    public bool isSceneLoading => __sceneCoroutineIndex != -1;

    public float speed => __tachometer.value;

    public string sceneName
    {
        get;

        private set;
    }

    public string nextSceneName
    {
        get;

        private set;
    }

    public AssetManager dataManager
    {
        get
        {
            return __assetManager;
        }
    }

    public AssetManager sceneManager
    {
        get
        {
            return __assetManager;
        }
    }

    public static string GetURL(string path)
    {
        switch (Application.platform)
        {
            case RuntimePlatform.Android:
                return "jar:file://" + path;
            default:
                return __GetStreamingAssetsURL(path);
        }
    }

    public static string GetStreamingAssetsURL(string path)
    {
        if (string.IsNullOrEmpty(path))
            return __GetStreamingAssetsURL(Application.streamingAssetsPath);

        return __GetStreamingAssetsURL($"{Application.streamingAssetsPath}/{path}");
    }

    public static IEnumerator InitLanguage(
        string path, 
        string url, 
        IAssetBundleFactory factory, 
        Action<GameObject> onComplete)
    {
        string language = GameLanguage.overrideLanguage, 
            persistentDataPath = Path.Combine(Application.persistentDataPath, language), 
            //languagePackageResourcePath = GameConstantManager.Get(LanguagePackageResourcePath), 
            folder = Path.GetDirectoryName(path), 
            filename = Path.GetFileName(folder), 
            filepath = Path.Combine(folder, filename);
        var assetManager = new AssetManager(Path.Combine(
            persistentDataPath, 
            filepath), 
            factory);

        string fullFilepath = Path.Combine(language, filepath);
        var assetPath = new ZG.AssetPath(
            GetStreamingAssetsURL(fullFilepath), 
            string.Empty, 
            AssetUtility.RetrievePack(fullFilepath));
        
        //UnityEngine.Debug.LogError(assetPath.url);

        yield return assetManager.GetOrDownload(null, null, assetPath);

        //string url = GameConstantManager.Get(GameConstantManager.KEY_CDN_URL);
        if (!string.IsNullOrEmpty(url))
        {
            assetPath = new ZG.AssetPath(
                $"{url}/{Application.platform}/{language}/{folder}/{filename}", 
                string.Empty, 
                null);
            yield return assetManager.GetOrDownload(null, null, assetPath);
        }

        string name = Path.GetFileName(path);
        var loader = new AssetBundleLoader<GameObject>(name.ToLower(), name, assetManager);

        yield return loader;

        var languagePackage = Instantiate(loader.value);

        //DontDestroyOnLoad(languagePackage);
        
        if(onComplete != null)
            onComplete.Invoke(languagePackage);
    }

    public void SetSceneLoader(IGameSceneLoader loader)
    {
        if (!isSceneLoading)
            return;
        
        __sceneLoaders.Enqueue(loader);
    }

    [Preserve]
    public void ConfirmOk()
    {
        __isConfirm = true;
    }

    [Preserve]
    public void ConfirmCancel()
    {
        if (onConfirmCancel != null)
            onConfirmCancel();
    }

    public void ClearProgressBarAll(bool isError)
    {
        GameProgressbar.instance.ClearProgressBarAll(StopCoroutine, isError);

        __sceneCoroutineIndex = -1;

        if (__assetCoroutine != null)
        {
            StopCoroutine(__assetCoroutine);

            __assetCoroutine = null;
        }
    }

    public IEnumerator Init(
        bool isWaitingForSceneLoaders, 
        string defaultSceneName, 
        string path, 
        string url, 
        IAssetBundleFactory factory = null, 
        IEnumerator sceneActivation = null, 
        IGameAssetUnzipper[] unzippers = null, 
        params AssetPath[] paths)
    {
        var progressBar = GameProgressbar.instance;
        progressBar.ShowProgressBar(GameProgressbar.ProgressbarType.Other);

        string language = GameLanguage.overrideLanguage;

        string persistentDataPath = Path.Combine(Application.persistentDataPath, language);
        __assetManager = new AssetManager(Path.Combine(persistentDataPath, path), factory);

        string assetURL = url == null ? null : $"{url}/{Application.platform}/{language}";

        yield return __LoadAssets(assetURL, paths, unzippers);

        __onSceneLoadedComplete = null;
        
        nextSceneName = defaultSceneName;

        yield return __LoadScene(isWaitingForSceneLoaders, -1, sceneActivation);

        progressBar.ClearProgressBar(GameProgressbar.ProgressbarType.Other);
    }

    public IEnumerator Init(
        string defaultSceneName,
        string scenePath,
        string path,
        string url,
        IAssetBundleFactory factory = null, 
        IEnumerator sceneActivation = null, 
        params IGameAssetUnzipper[] unzippers)
    {
        return Init(
            false, 
            defaultSceneName, 
            path, 
            url, 
            factory,
            sceneActivation, 
            unzippers, 
            new AssetPath(scenePath, GameLanguage.overrideLanguage));
    }
    
    public bool StopLoadingScene()
    {
        if (__sceneCoroutineIndex == -1)
            return false;

        var coroutine = GameProgressbar.instance.ClearProgressBar(GameProgressbar.ProgressbarType.LoadScene, __sceneCoroutineIndex);

        StopCoroutine(coroutine);

        __sceneCoroutineIndex = -1;

        return true;
    }

    public bool LoadScene(string name, Action onComplete, IEnumerator activation = null, bool isWaitingForSceneLoaders = true)
    {
        if (string.IsNullOrEmpty(nextSceneName) && sceneName == name)
            return false;
        
        __onSceneLoadedComplete = onComplete;
        
        if (nextSceneName == name)
            return true;

        //StopLoadingScene();

        nextSceneName = name;

        if (__sceneCoroutineIndex == -1)
        {
            var progressbar = GameProgressbar.instance;

            __sceneCoroutineIndex = progressbar == null ? -1 : progressbar.BeginCoroutine();

            var coroutine = StartCoroutine(__LoadScene(isWaitingForSceneLoaders, __sceneCoroutineIndex, activation));

            if(progressbar != null)
                progressbar.EndCoroutine(__sceneCoroutineIndex, coroutine);
        }

        return true;
    }

    public void LoadAssets(
        bool isVerified, 
        Action onComplete, 
        string url, 
        AssetPath[] paths, 
        IGameAssetUnzipper[] unzippers)
    {
        __assetCoroutine = StartCoroutine(__LoadAssets(
            isVerified, 
            __assetCoroutine, 
            onComplete, 
            url, 
            paths, 
            unzippers));
    }

    private IEnumerator __LoadAssets(string url, AssetPath[] paths, IGameAssetUnzipper[] unzippers)
    {
        var progressbar = GameProgressbar.instance;

        /*(IAssetPack, ulong)[] assetPacks = null;
        IAssetPack assetPack;
        IAssetPackHeader assetPackHeader;
        ulong fileSize, size = 0;
        int i, j, length = paths.Length;
        for (i = 0; i < length; ++i)
        {
            assetPack = AssetUtility.RetrievePack(paths[i].filePath);

            if (assetPacks == null)
                assetPacks = new (IAssetPack, ulong)[length];
            else
            {
                for(j = 0; j < i; ++j)
                {
                    if(assetPacks[j].Item1 == assetPack)
                        break;
                }

                if (j < i)
                {
                    assetPacks[i] = (assetPack, 0);

                    continue;
                }
            }

            assetPackHeader = assetPack == null ? null : assetPack.header;
            if (assetPackHeader == null)
                continue;

            while (!assetPackHeader.isDone)
                yield return null;

            fileSize = assetPackHeader.fileSize;

            size += (ulong)Math.Round(fileSize * (double)(1.0f - assetPack.downloadProgress));

            assetPacks[i] = (assetPack, fileSize);
        }

        if(size > 0)
        {
            progressbar.ShowProgressBar(GameProgressbar.ProgressbarType.Download);

            (IAssetPack, ulong) assetPackAndFileSize;
            ulong bytesNeedToDownload = 0L;
            uint downloadedBytes;
            bool isDone;
            do
            {
                yield return null;

                isDone = true;
                for (i = 0; i < length; ++i)
                {
                    assetPackAndFileSize = assetPacks[i];
                    assetPack = assetPackAndFileSize.Item1;
                    if (assetPack == null)
                        continue;

                    bytesNeedToDownload += (ulong)Math.Round(assetPackAndFileSize.Item2 * (double)(1.0f - assetPack.downloadProgress));

                    isDone &= assetPack.isDone;
                }

                downloadedBytes = (uint)(size - bytesNeedToDownload);

                __Download("Packs", downloadedBytes * 1.0f / size, downloadedBytes, downloadedBytes, size, 0, 1);

            } while (!isDone);

            progressbar.ClearProgressBar(GameProgressbar.ProgressbarType.Download);
        }*/

        progressbar.ShowProgressBar(GameProgressbar.ProgressbarType.Unzip);

        string folder;
        AssetPath path;
        int length = paths.Length;
        var assetPaths = new ZG.AssetPath[length];
        for (int i = 0; i < length; ++i)
        {
            path = paths[i];
            folder = Path.GetDirectoryName(path.value);

            if (!string.IsNullOrEmpty(folder))
                __assetManager.LoadFrom(path.value);

            assetPaths[i] = new ZG.AssetPath(
                GetStreamingAssetsURL(path.filePath), 
                folder, 
                AssetUtility.RetrievePack(path.filePath)/*assetPacks == null ? null : assetPacks[i].Item1*/);
        }

        __tachometer.Start();

        yield return __assetManager.GetOrDownload(
            null,
            __Unzip,
            assetPaths);

        progressbar.ClearProgressBar(GameProgressbar.ProgressbarType.Unzip);

        /*if (progressbarInfo.onInfo != null)
            progressbarInfo.onInfo.Invoke(string.Empty);*/

        if (!string.IsNullOrEmpty(url))
        {
            progressbar.ShowProgressBar(GameProgressbar.ProgressbarType.Download);

            /*if (!string.IsNullOrEmpty(suffix))
                url = $"{url}/{suffix}";*/

            for (int i = 0; i < length; ++i)
            {
                path = paths[i];

                assetPaths[i] = new ZG.AssetPath($"{url}/{path.url}", assetPaths[i].folder, null);
            }

            __tachometer.Start();

            yield return __assetManager.GetOrDownload(
                __Confirm,
                __Download,
                assetPaths);

            progressbar.ClearProgressBar(GameProgressbar.ProgressbarType.Download);

            /*if (progressbarInfo.onInfo != null)
                progressbarInfo.onInfo.Invoke(string.Empty);*/
        }
        
        if(unzippers != null)
        {
            string filename;
            AssetManager.Asset asset;
            AssetBundle assetBundle;
            foreach (var unzipper in unzippers)
            {
                filename = unzipper.filename;
                if (string.IsNullOrEmpty(filename) ||
                    !__assetManager.Get(
                        filename,
                        out asset) ||
                    asset.data.type == AssetManager.AssetType.UncompressedRuntime)
                {
                    yield return unzipper.Execute(null, __Unzip);
                    
                    continue;
                }

                progressbar.ShowProgressBar(GameProgressbar.ProgressbarType.Verify);

                assetBundle = null;
                yield return __assetManager.LoadAssetBundleAsync(filename, null, x => assetBundle = x);

                yield return __assetManager.Write(filename, unzipper.Execute(assetBundle, __Unzip));

                if(assetBundle != null)
                    assetBundle.Unload(true);
                
                progressbar.ClearProgressBar(GameProgressbar.ProgressbarType.Verify);
            }
        }
    }

    private IEnumerator __LoadAssets(
        bool isVerified, 
        Coroutine coroutine, 
        Action onComplete, 
        string url, 
        AssetPath[] paths,
        IGameAssetUnzipper[] unzippers)
    {
        if (coroutine != null)
            yield return coroutine;

        if (isVerified)
            yield return Verifier.Start(__assetManager, verifyProgressFormat);
        else if (onLoadAssetsStart != null)
            onLoadAssetsStart();

        yield return __LoadAssets(url, paths, unzippers);

        if (!isVerified)
        {
            if (onLoadAssetsEnd != null)
                onLoadAssetsEnd();
        }

        __isMissingConfirm = false;

        if (onComplete != null)
            onComplete();

        var progressbar = GameProgressbar.instance;
        progressbar.ShowProgressBar(GameProgressbar.ProgressbarType.Verify);

        __tachometer.Start();

        yield return __assetManager.Recompress(__Recompress);

        progressbar.ClearProgressBar(GameProgressbar.ProgressbarType.Verify);

        if (!isVerified)
        {
            if (onLoadAssetsFinish != null)
                onLoadAssetsFinish();
        }

        __assetCoroutine = null;
    }

    private IEnumerator __LoadScene(bool isWaitingForSceneLoaders, int coroutineIndex, IEnumerator activation)
    {
        var progressbar = GameProgressbar.instance;

        if(progressbar != null)
            progressbar.ShowProgressBar(GameProgressbar.ProgressbarType.LoadScene, coroutineIndex);

        //等待断开连接的对象调用OnDestroy
        yield return null;

        while (this.nextSceneName != null)
        {
            string sceneName = this.sceneName;
            if (!string.IsNullOrEmpty(sceneName))
            {
                var scene = SceneManager.GetSceneByName(Path.GetFileNameWithoutExtension(sceneName));
                if (scene.IsValid())
                {
                    while (!scene.isLoaded)
                        yield return null;

                    var gameObjects = scene.GetRootGameObjects();
                    if (gameObjects != null)
                    {
                        foreach (var gameObject in gameObjects)
                            Destroy(gameObject);
                    }
                    //yield return SceneManager.UnloadSceneAsync(Path.GetFileNameWithoutExtension(sceneName), UnloadSceneOptions.UnloadAllEmbeddedSceneObjects);

                    //等待场景对象调用OnDestroy
                    yield return null;
                }

                if (__assetManager != null)
                    __assetManager.UnloadAssetBundle(sceneName);
                
                yield return Resources.UnloadUnusedAssets();
            
                GC.Collect();
            }

            string nextSceneName = this.nextSceneName;

            this.sceneName = nextSceneName;

            AssetBundle assetBundle = null;
            if (__assetManager != null)
                yield return __assetManager.LoadAssetBundleAsync(
                    nextSceneName, 
                    isWaitingForSceneLoaders ? __LoadingSceneAndWaitingForLoaders : __LoadingScene, 
                    x => assetBundle = x);

            var asyncOperation = SceneManager.LoadSceneAsync(Path.GetFileNameWithoutExtension(nextSceneName), LoadSceneMode.Single);
            if (asyncOperation != null)
            {
                asyncOperation.allowSceneActivation = activation == null;
                
                while (!asyncOperation.isDone)
                {
                    if(progressbar != null)
                        progressbar.UpdateProgressBar(
                            GameProgressbar.ProgressbarType.LoadScene, 
                            asyncOperation.progress * 0.1f + (isWaitingForSceneLoaders ? 0.1f : 0.9f));

                    if (activation != null && !activation.MoveNext())
                        asyncOperation.allowSceneActivation = true;

                    yield return null;
                }
            }

            int doneCount = 0, loadingCount;
            float progress;
            while (__sceneLoaders.TryDequeue(out var sceneLoader))
            {
                do
                {
                    yield return null;

                    if (isWaitingForSceneLoaders && progressbar != null)
                    {
                        progress = sceneLoader.progress;
                        foreach (var temp in __sceneLoaders)
                            progress += temp.progress;

                        loadingCount = __sceneLoaders.Count + 1;
                        progressbar.UpdateProgressBar(
                            GameProgressbar.ProgressbarType.LoadScene,
                            (doneCount + progress) * 0.8f / (doneCount + loadingCount) + 0.2f);
                    }

                } while (!sceneLoader.isDone);

                ++doneCount;
            }
            
            /*if (assetBundle != null)
                assetBundle.Unload(false);*/

            //Caching.ClearCache();

            if (nextSceneName == this.nextSceneName)
                this.nextSceneName = null;
        }

        if(progressbar != null)
            progressbar.ClearProgressBar(GameProgressbar.ProgressbarType.LoadScene, coroutineIndex);

        if (coroutineIndex == __sceneCoroutineIndex)
            __sceneCoroutineIndex = -1;

        if (__onSceneLoadedComplete != null)
        {
            __onSceneLoadedComplete();

            __onSceneLoadedComplete = null;
        }
    }

    private void __LoadingScene(float progress)
    {
        GameProgressbar.instance.UpdateProgressBar(GameProgressbar.ProgressbarType.LoadScene, progress * 0.9f);
    }

    private void __LoadingSceneAndWaitingForLoaders(float progress)
    {
        GameProgressbar.instance.UpdateProgressBar(GameProgressbar.ProgressbarType.LoadScene, progress * 0.1f);
    }
    
    private void __Recompress(
        string name,
        float progress,
        uint bytesDownload,
        ulong totalBytesDownload,
        ulong totalBytes,
        int index,
        int count)
    {
        GameProgressbar.instance.UpdateProgressBar(
            GameProgressbar.ProgressbarType.Verify,
            //(index + progress) / count,
            (float)(totalBytesDownload * 1.0 / totalBytes),
            __GetProgressInfo(
                recompressProgressFormat,
                name,
                progress,
                bytesDownload,
                totalBytesDownload,
                totalBytes,
                index,
                count));
    }

    private void __Unzip(
        string name, 
        float progress, 
        uint bytesDownload, 
        ulong totalBytesDownload, 
        ulong totalBytes, 
        int index, 
        int count)
    {
        GameProgressbar.instance.UpdateProgressBar(
            GameProgressbar.ProgressbarType.Unzip,
               //(index + progress) / count,
            (float)(totalBytesDownload * 1.0 / totalBytes),
            __GetProgressInfo(
                unzipProgressFormat, 
                name, 
                progress, 
                bytesDownload, 
                totalBytesDownload, 
                totalBytes, 
                index, 
                count));
    }

    private void __Download(string name, float progress, uint bytesDownload, ulong totalBytesDownload, ulong totalBytes, int index, int count)
    {
        GameProgressbar.instance.UpdateProgressBar(
               GameProgressbar.ProgressbarType.Download,
               //(index + progress) / count,
               (float)(totalBytesDownload * 1.0 / totalBytes),
               __GetProgressInfo(
                   downloadProgressFormat, 
                   name, 
                   progress, 
                   bytesDownload, 
                   totalBytesDownload, 
                   totalBytes, 
                   index, 
                   count));
    }

    private IEnumerator __Confirm(ulong size)
    {
        if (__isMissingConfirm)
        {
            __isConfirm = true;

            yield break;
        }

        __isMissingConfirm = true;

#if !UNITY_WEBGL
        if (Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork)
#endif
        {
            __isConfirm = false;

            if (onConfirm != null)
                onConfirm.Invoke((size / (1024.0 * 1024.0)).ToString("F2"));

            while (!__isConfirm)
                yield return null;
        }
    }

    private string __GetProgressInfo(
        string format, 
        string name, 
        float progress, 
        uint bytesDownload, 
        ulong totalBytesDownload, 
        ulong totalBytes, 
        int index, 
        int count)
    {
        float m = 1024.0f * 1024.0f;

        return string.Format(
            format,
            name, 
            progress,
            bytesDownload,
            bytesDownload / progress, 
            totalBytesDownload * 1.0 / m,
            totalBytes * 1.0 / m,
            index,
            count,
            __tachometer.Update(totalBytesDownload) / m);
    }

    private static string __GetStreamingAssetsURL(string path)
    {
        switch (Application.platform)
        {
            case RuntimePlatform.WindowsEditor:
            case RuntimePlatform.WindowsPlayer:
            case RuntimePlatform.WindowsServer:
                return "file:///" + path;
            case RuntimePlatform.OSXEditor:
            case RuntimePlatform.OSXPlayer:
            case RuntimePlatform.IPhonePlayer:
            case RuntimePlatform.LinuxPlayer:
            case RuntimePlatform.LinuxServer:
            case RuntimePlatform.LinuxEditor:
                return "file://" + path;
        }

        return path;
    }

    void OnEnable()
    {
        instance = this;
    }

    void OnDisable()
    {
        instance = null;
    }
}
