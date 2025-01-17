using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public partial class GameBuildConfig
{
    private enum GameBuildChannel
    {
        None,

        //PC
        Steam,

        //Mobile
        OneFun,
        OneFun_zh_CN,
        OneFun_in_ID,
        FN4399,
        FN3839,
        Wechat
    }
    
    
    [Serializable]
    private class Channels : ZG.Map<GameBuildChannel, Channel>
    {
        public static GameBuildChannel GetUniqueValue(GameBuildChannel key, IEnumerable<GameBuildChannel> keys)
        {
            if (keys != null)
            {
                foreach (GameBuildChannel temp in keys)
                {
                    if (temp == key)
                        return GetUniqueValue(key + 1, keys);
                }
            }

            return key;
        }

        public Channels() : base(GetUniqueValue)
        {

        }
    }

    [HideInInspector, SerializeField, UnityEngine.Serialization.FormerlySerializedAs("channels")]
    private Channels __channels;

    void OnValidate()
    {
        if ((channels == null || channels.Length < 1) && __channels != null && __channels.Count > 0)
        {
            EditorApplication.delayCall += () =>
            {
                Channel channel;
                var channels = new List<Channel>(__channels.Count);
                foreach (var pair in __channels)
                {
                    channel = pair.Value;
                    channel.name = pair.Key.ToString();
                    channels.Add(channel);
                }

                this.channels = channels.ToArray();
                
                EditorUtility.SetDirty(this);
            };
        }
    }
}
