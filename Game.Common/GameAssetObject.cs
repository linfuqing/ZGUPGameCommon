using UnityEngine;
using ZG;

public class GameAssetObject : AssetObjectBase, IGameSceneLoader
{
    [System.Serializable]
    internal class LoadedEvent : UnityEngine.Events.UnityEvent<GameObject> { }
    
    private Coroutine __coroutine;

    [SerializeField]
    internal AssetObjectLoader.Space _space;
    [SerializeField]
    internal float _time;
    [SerializeField]
    internal string _fileName;
    [SerializeField]
    internal string _nameOverride;

    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("onLoaded")]
    internal LoadedEvent _onLoaded;

    public override AssetObjectLoader.Space space => _space;

    public override float time => _time;

    public override string fileName => _fileName;

    public override string assetName => string.IsNullOrEmpty(_nameOverride) ? name : _nameOverride;

    public override ZG.AssetManager assetManager => GameAssetManager.instance?.dataManager;

    public GameAssetObject()
    {
        var assetManager = GameAssetManager.instance;
        if ((object)assetManager != null)
            assetManager.SetSceneLoader(this);

        onLoadComplete += __OnLoadComplete;
    }

    protected new void OnEnable()
    {
        if (__coroutine != null)
        {
            var assetManager = GameAssetManager.instance;
            if(assetManager != null)
                assetManager.StopCoroutine(__coroutine);

            __DisableRightNow();
        }
        
        base.OnEnable();
    }

    protected new void OnDisable()
    {
        UnityEngine.Assertions.Assert.IsNull(__coroutine);
        
        var assetManager = GameAssetManager.instance;
        if (assetManager == null)
            __DisableRightNow();
        else
            __coroutine = assetManager.StartCoroutine(__Disable());
    }

    private System.Collections.IEnumerator __Disable()
    {
        yield return null;

        __DisableRightNow();
    }

    private void __DisableRightNow()
    {
        base.OnDisable();

        __coroutine = null;
    }
    
    private void __OnLoadComplete(GameObject gameObject)
    {
        print($"Asset {gameObject.name} load complete.");

        __SetStatic(gameObject);
        
        if(_onLoaded != null)
            _onLoaded.Invoke(gameObject);
    }

    private static void __SetStatic(GameObject gameObject)
    {
        if (gameObject.isStatic)
        {
            StaticBatchingUtility.Combine(gameObject);

            return;
        }
        
        foreach (Transform child in gameObject.transform)
            __SetStatic(child.gameObject);
    }
}
