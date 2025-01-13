using System;
using UnityEngine;

public class GameChannelSingleton : GameChannel
{
    protected new void Awake()
    {
        singleton = this;

        base.Awake();
    }

    public override void RequestUserInfo(LoginCallback onLogin, Action onLogout)
    {
        if (onLogin != null)
            onLogin(
                0,
                GameConstantManager.Get(GameConstantManager.KEY_CHANNEL), 
                SystemInfo.deviceUniqueIdentifier, 
                string.Empty, 
                default(GameChannelToken));
    }
}
