using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.Events;
using ZG;

public interface IGameUserData
{
    public enum UserStatus
    {
        Error, 
        None, 
        Ok, 
        New
    }
    
    IEnumerator Activate(
        string code,
        string channel,
        string channelUser,
        Action<UserStatus> onComplete);

    IEnumerator Check(
        string channel,
        string channelUser,
        Action<UserStatus> onComplete);
    
    IEnumerator Bind(
        int userID,
        string channelUser,
        string channel,
        Action<bool?> onComplete);

    IEnumerator Unbind(
        string channel,
        string channelUser,
        Action<bool?> onComplete);
    
    Coroutine StartCoroutine(IEnumerator enumerator);
}

public abstract class GameUser : MonoBehaviour
{
    public static class Shared
    {
        internal static Action<Action<bool?>> _unbind;

        internal static Action<int, Action<bool?>> _bind;

        internal static Action _onActivated;

        internal static Action<bool> _onloginStatusChanged;

        public static int userType
        {
            get;

            internal set;
        }

        public static string channelName
        {
            get;

            internal set;
        }

        public static string channelUser
        {
            get;

            internal set;
        }

        public static string channelUsername
        {
            get;

            internal set;
        }

        public static IGameChannelToken token
        {
            get;

            internal set;
        }

        public static event Action onActivated
        {
            add
            {
                _onActivated += value;
            }

            remove
            {
                _onActivated -= value;
            }
        }

        public static event Action<bool> onloginStatusChanged
        {
            add
            {
                _onloginStatusChanged += value;
            }

            remove
            {
                _onloginStatusChanged -= value;
            }
        }

        public static void Bind(int userID, Action<bool?> onComplete)
        {
            if (_bind == null)
                onComplete(false);
            else
                _bind(userID, onComplete);
        }

        public static void Unbind(Action<bool?> onComplete)
        {
            if (_bind == null)
                onComplete(false);
            else
                _unbind(onComplete);
        }
    }

    [Tooltip("当前平台可否切换用户")]
    public ActiveEvent onCanSwitch;

    [Tooltip("单一渠道")]
    public UnityEvent onSingleton;

    [Tooltip("渠道选择")]
    public UnityEvent onChannel;

    [Tooltip("登录失败")]
    public UnityEvent onFail;

    [Tooltip("登录中")]
    public UnityEvent onWaiting;

    [Tooltip("登录成功")]
    public UnityEvent onLogin;

    [Tooltip("需要激活码")]
    public UnityEvent onEnable;

    [Tooltip("激活码失败")]
    public UnityEvent onDisable;

    public GameChannel[] channels;

    private const string __NAME_SPACE_CHANNEL = "ClientChannel";

    [Preserve]
    public int channelIndex
    {
        set
        {
            __SetChannelIndex(value);
        }
    }

    public GameChannel channel
    {
        get;

        private set;
    }

    public abstract IGameUserData userData
    {
        get;
    }

    [Preserve]
    public void Switch()
    {
        if(channel != null)
            channel.SwitchUser();
    }

    [Preserve]
    public void Login()
    {
        var channel = GameChannel.singleton;

        this.channel = channel;

        if (channel != null)
        {
            if (onSingleton != null)
                onSingleton.Invoke();

            channel.RequestUserInfo(__OnLogin, Login);
        }
        else if (channels == null || channels.Length < 1)
            __OnLogin(0, "Device", SystemInfo.deviceUniqueIdentifier, string.Empty, default(GameChannelToken));
        else
        {
            string name = PlayerPrefs.GetString(__NAME_SPACE_CHANNEL);
            if (string.IsNullOrEmpty(name) || !__SetChannelIndex(channels.IndexOf(name)))
            {
                if (onChannel != null)
                    onChannel.Invoke();
            }
        }
    }

    [Preserve]
    public void Activate(string code)
    {
        var userData = this.userData;
        if (userData == null)
            __Login();
        else
            StartCoroutine(userData.Activate(code, Shared.channelName, Shared.channelUser, __OnActivate));
    }

    private void __OnCheck(IGameUserData.UserStatus userStatus)
    {
        switch (userStatus)
        {
            case IGameUserData.UserStatus.Ok:
                __Login();
                break;
            case IGameUserData.UserStatus.New:
                if(Shared._onActivated != null)
                    Shared._onActivated();

                __Login();
                break;
            case IGameUserData.UserStatus.None:
                if (onEnable != null)
                    onEnable.Invoke();
                break;
            default:
                channel = null;

                if (onFail != null)
                    onFail.Invoke();
                break;
        }
    }

    private void __OnActivate(IGameUserData.UserStatus userStatus)
    {
        switch (userStatus)
        {
            case IGameUserData.UserStatus.Ok:
                __Login();
                break;
            case IGameUserData.UserStatus.New:
                if (Shared._onActivated != null)
                    Shared._onActivated();

                __Login();
                break;
            default:
                if (onDisable != null)
                    onDisable.Invoke();
                break;
        }
    }

    private void __OnLogin(int userType, string channelName, string channelUser, string channelUsername, IGameChannelToken token)
    {
        /*GameClientMain main = GameClientMain.instance;
        if (this == null)
        {
            if (main != null)
                main.Shutdown(true);

            return;
        }*/

        Shared.userType = userType;
        Shared.channelName = channelName;
        Shared.channelUser = channelUser;
        Shared.channelUsername = channelUsername;
        Shared.token = token;

        Shared._bind = null;
        Shared._unbind = null;

        if (Shared._onloginStatusChanged != null)
            Shared._onloginStatusChanged(channelUser != null);

        if (this == null)
            return;

        if (channelUser == null)
        {
            channel = null;

            if (onFail != null)
                onFail.Invoke();

            return;
        }

        var userData = this.userData;

        Shared._bind = (int userID, Action<bool?> onComplete) =>
        {
            if (userData == null)
                onComplete(false);
            else
                userData.StartCoroutine(userData.Bind(userID, Shared.channelUser, Shared.channelName, onComplete));
        };

        Shared._unbind = (Action<bool?> onComplete) =>
        {

            if (userData == null)
                onComplete(false);
            else
                userData.StartCoroutine(userData.Unbind(Shared.channelName, Shared.channelUser, onComplete));
        };

        if (userData == null)
            __Login();
        else
        {
            if (onWaiting != null)
                onWaiting.Invoke();

            StartCoroutine(userData.Check(channelName, channelUser, __OnCheck));
        }
    }

    private void __Login()
    {
        var channel = this.channel;
        if (channel != null)
        {
            PlayerPrefs.SetString(__NAME_SPACE_CHANNEL, channel.name);
            
            channel.Activate(Shared.channelUser, Shared.channelUsername);
            
            if(onCanSwitch != null)
                onCanSwitch.Invoke(channel.CanSwitchUser());
        }

        if (onLogin != null)
            onLogin.Invoke();
    }

    private bool __SetChannelIndex(int value)
    {
        var channel = value < 0 || channels == null || channels.Length < value ? null : channels[value];

        this.channel = channel;

        //string name = __channel == null ? null : __channel.name;

        //__channel = string.IsNullOrEmpty(name) ? null : Instantiate(__channel);
        if (channel == null)
            return false;

        channel.RequestUserInfo(__OnLogin, Login);

        return true;
    }
}