using System;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

// PhotonView가 없는 수신 관리자. 씬/네트워크 오브젝트 초기화와 독립적으로 유지한다.
public class SessionChat : MonoBehaviour, IOnEventCallback
{
    private const byte ChatEvent = 71;
    private static SessionChat instance;
    private readonly StringBuilder history = new StringBuilder();
    public static bool IsTyping { get; set; }
    public static int EscapeConsumedFrame { get; set; } = -1;
    public static float BlockUntil { get; set; }
    public static bool InputBlocked => IsTyping || Time.unscaledTime < BlockUntil;
    public event Action Changed;
    public string History => history.ToString();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    { instance = null; IsTyping = false; BlockUntil = 0; EscapeConsumedFrame = -1; }

    public static SessionChat Ensure()
    {
        if (!instance)
        {
            var go = new GameObject("SessionChat (persistent)");
            instance = go.AddComponent<SessionChat>();
            DontDestroyOnLoad(go);
        }
        return instance;
    }
    private void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    private void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    public bool Send(string message)
    {
        message = Clean(message, 300);
        if (!PhotonNetwork.InRoom || message.Length == 0) return false;
        return PhotonNetwork.RaiseEvent(ChatEvent, message,
            new RaiseEventOptions { Receivers = ReceiverGroup.All, CachingOption = EventCaching.DoNotCache },
            SendOptions.SendReliable);
    }
    public void OnEvent(EventData data)
    {
        if (data.Code != ChatEvent || !(data.CustomData is string message) || !PhotonNetwork.InRoom) return;
        var player = PhotonNetwork.CurrentRoom.GetPlayer(data.Sender);
        if (player == null) return;
        message = Clean(message, 300);
        if (message.Length == 0) return;
        string name = Clean(player.NickName, 32);
        if (name.Length == 0) name = "Player " + data.Sender;
        history.Append('[').Append(name).Append("] ").Append(message).Append('\n');
        Changed?.Invoke();
    }
    private static string Clean(string text, int limit)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var result = new StringBuilder();
        foreach (char c in text)
        {
            if (result.Length >= limit) break;
            if (!char.IsControl(c)) result.Append(c);
        }
        // UTF-16 보조 문자 중간에서 자르지 않는다.
        if (result.Length > 0 && char.IsHighSurrogate(result[result.Length - 1])) result.Length--;
        return result.ToString().Trim();
    }
}
