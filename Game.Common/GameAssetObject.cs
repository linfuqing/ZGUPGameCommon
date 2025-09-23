using UnityEngine;

public class GameAssetObject : ZG.AssetObjectBase
{
    private Coroutine __coroutine;
    
    [SerializeField]
    internal Space _space;
    [SerializeField]
    internal float _time;
    [SerializeField]
    internal string _fileName;
    [SerializeField]
    internal string _nameOverride;

    public override Space space => _space;

    public override float time => _time;

    public override string fileName => _fileName;

    public override string assetName => string.IsNullOrEmpty(_nameOverride) ? name : _nameOverride;

    public override ZG.AssetManager assetManager => GameAssetManager.instance?.dataManager;

    protected new void OnEnable()
    {
        if (__coroutine != null)
        {
            var assetManager = GameAssetManager.instance;
            if(assetManager != null)
                assetManager.StopCoroutine(__coroutine);

            __coroutine = null;
            
            base.OnDisable();
        }
        
        base.OnEnable();
    }

    protected new void OnDisable()
    {
        var assetManager = GameAssetManager.instance;
        if(assetManager != null)
            __coroutine = assetManager.StartCoroutine(__Disable());
    }

    private System.Collections.IEnumerator __Disable()
    {
        yield return null;

        base.OnDisable();

        __coroutine = null;
    }
}
