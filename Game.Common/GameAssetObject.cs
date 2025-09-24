using UnityEngine;

public class GameAssetObject : ZG.AssetObjectBase, IGameSceneLoader
{
    [System.Serializable]
    internal class LoadedEvent : UnityEngine.Events.UnityEvent<GameObject> { }
    
    private Coroutine __coroutine;

    [SerializeField]
    internal Space _space;
    [SerializeField]
    internal float _time;
    [SerializeField]
    internal string _fileName;
    [SerializeField]
    internal string _nameOverride;

    [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("onLoaded")]
    internal LoadedEvent _onLoaded;

    public bool isDone
    {
        get;

        private set;
    }

    public override Space space => _space;

    public override float time => _time;

    public override string fileName => _fileName;

    public override string assetName => string.IsNullOrEmpty(_nameOverride) ? name : _nameOverride;

    public override ZG.AssetManager assetManager => GameAssetManager.instance?.dataManager;

    public GameAssetObject()
    {
        var assetManager = GameAssetManager.instance;
        if (assetManager != null)
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

        isDone = false;

        __coroutine = null;
    }
    
    private void __OnLoadComplete(GameObject gameObject)
    {
        UnityEngine.Assertions.Assert.IsFalse(isDone);
        
        isDone = true;
        
        if(_onLoaded != null)
            _onLoaded.Invoke(gameObject);
    }
}
