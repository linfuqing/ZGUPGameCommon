using UnityEngine;

public class GameChannelSelector : MonoBehaviour
{
    System.Collections.IEnumerator Start()
    {
        while (!GameConstantManager.isInit)
            yield return null;

        string channelAPI = GameConstantManager.Get(GameConstantManager.KEY_CHANNEL_API);

        foreach (Transform child in transform)
            child.gameObject.SetActive(child.name == channelAPI);
    }
}
