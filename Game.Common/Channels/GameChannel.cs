using System;
using System.Collections.Generic;
using UnityEngine;

public interface IGameChannelReport
{
    public static IGameChannelReport instance;

    bool Violation(int id, string name, string type, string content, string contact);
}

public interface IGameChannelToken
{
    void Get(Action<string> onComplete);
}

public struct GameChannelToken : IGameChannelToken
{
    public void Get(Action<string> onComplete)
    {
        onComplete.Invoke(string.Empty);
    }
}

public class GameChannel : MonoBehaviour
{
    public static class Shared
    {
        public static readonly Dictionary<string, string> keywords = new Dictionary<string, string>();
    }
    
    public delegate void LoginCallback(
        int userType, 
        string channel, 
        string channelUser, 
        string channelUsername, 
        IGameChannelToken token);

    public static GameChannel singleton
    {
        get;
        
        protected set;
    }

    public virtual string language
    {
        get
        {
            return GameLanguage.overrideLanguage;
        }
    }
    
    public ZG.ActiveEvent onActive;

    protected void Awake()
    {
        if (onActive != null)
            onActive.Invoke(singleton == null);
    }

    public virtual bool CanSwitchUser()
    {
        return false;
    }

    public virtual void SwitchUser()
    {
        
    }

    public virtual void RequestUserInfo(LoginCallback onLogin, Action onLogout)
    {
        if(onLogin != null)
            onLogin(0, name, SystemInfo.deviceUniqueIdentifier, string.Empty, default(GameChannelToken));
    }

    public virtual void Activate(string channelUser, string channelUsername)
    {

    }

    public virtual void Submit(int serverIndex, string serverName)
    {

    }
}
