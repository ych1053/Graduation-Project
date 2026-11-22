using System;
using System.Collections;
using System.Text;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class GlobalRanking : MonoBehaviour
{
    [Serializable] private class Record { public string record_id; public string player1_name; public string player2_name; public int elapsed_ms; }
    [Serializable] private class TopResponse { public bool ok; public Record[] records; }
    [Serializable] private class SubmitResponse { public bool ok; public string record_id; }
    private string currentRecordId = "";

    [SerializeField] private Round2EndingChest ending;
    [SerializeField] private TMP_Text rankingText;
    [SerializeField] private string apiUrl = "https://ych1053.dothome.co.kr/ranking/";
    [SerializeField] private string submitKey;
    private bool requested;
    public bool PresentationComplete { get; private set; }
    [SerializeField, Min(0.1f)] private float slideSeconds = 0.6f;

    private void Start()
    {
        if (!rankingText || !ending) { Debug.LogError("GlobalRanking 연결을 확인하세요.", this); enabled = false; return; }
        rankingText.richText = false;
        rankingText.text = "";
    }

    private void Update()
    {
        if (requested || !ending || !ending.SkyReady) return;
        requested = true;
        StartCoroutine(ShowRanking());
    }

    private IEnumerator ShowRanking()
    {
        rankingText.text = "기록을 불러오는 중...";
        if (!PhotonNetwork.InRoom) yield break;
        if (!PhotonNetwork.IsMasterClient)
        {
            while (PhotonNetwork.InRoom && !PhotonNetwork.CurrentRoom.CustomProperties.ContainsKey("rank_json"))
                yield return null;
            if (PhotonNetwork.InRoom)
                yield return Reveal((string)PhotonNetwork.CurrentRoom.CustomProperties["rank_json"],
                    PhotonNetwork.CurrentRoom.CustomProperties["rank_message"] as string ?? "");
            yield break;
        }
        string message = "";
        if (PhotonNetwork.IsMasterClient)
        {
            object startValue;
            if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("run_start", out startValue) && startValue is double)
            {
                double seconds = ending.ClearTimeOnNetwork - (double)startValue;
                if (seconds < -2147483) seconds += 4294967.296;
                if (seconds >= 1 && seconds <= 86400)
                {
                    string[] names = new string[2];
                    var players = PhotonNetwork.PlayerList;
                    Array.Sort(players, (a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
                    for (int i = 0; i < Math.Min(2, players.Length); i++)
                        names[i] = players[i].NickName;
                    var payload = new Record {
                        player1_name = string.IsNullOrWhiteSpace(names[0]) ? "Player 1" : names[0],
                        player2_name = string.IsNullOrWhiteSpace(names[1]) ? "Player 2" : names[1],
                        elapsed_ms = (int)Math.Round(seconds * 1000)
                    };
                    byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));
                    using (var request = new UnityWebRequest(apiUrl + "submit.php", "POST"))
                    {
                        request.uploadHandler = new UploadHandlerRaw(data);
                        request.downloadHandler = new DownloadHandlerBuffer();
                        request.SetRequestHeader("Content-Type", "application/json");
                        request.SetRequestHeader("X-Ranking-Key", submitKey);
                        request.timeout = 15;
                        yield return request.SendWebRequest();
                        if (request.result != UnityWebRequest.Result.Success)
                            message = "이번 기록을 저장하지 못했습니다.";
                        else
                        {
                            var saved = JsonUtility.FromJson<SubmitResponse>(request.downloadHandler.text);
                            if (saved != null && saved.ok) currentRecordId = saved.record_id ?? "";
                        }
                    }
                }
                else message = "측정 시간이 올바르지 않아 이번 기록은 저장하지 않았습니다.";
            }
            else message = "시작 기록이 없습니다. Round_1부터 플레이하세요.";
        }

        using (var request = UnityWebRequest.Get(apiUrl + "top.php"))
        {
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Publish("", "랭킹을 불러오지 못했습니다. " + message);
                yield break;
            }
            Publish(request.downloadHandler.text, message);
        }
    }

    private void Publish(string json, string message)
    {
        PhotonNetwork.CurrentRoom.SetCustomProperties(new ExitGames.Client.Photon.Hashtable {
            { "rank_json", json }, { "rank_message", message }, { "rank_current_id", currentRecordId }
        });
        StartCoroutine(Reveal(json, message));
    }

    private IEnumerator Reveal(string json, string message)
    {
            while (!ending.SkyReady) yield return null;
            TopResponse response = null;
            try { response = JsonUtility.FromJson<TopResponse>(json); }
            catch (Exception) { }
            if (response == null || !response.ok || response.records == null)
            {
                rankingText.text = "랭킹을 불러오지 못했습니다.\n" + message;
                PresentationComplete = true;
                yield break;
            }
            if (!PhotonNetwork.IsMasterClient)
                currentRecordId = PhotonNetwork.CurrentRoom.CustomProperties["rank_current_id"] as string ?? "";
            Canvas.ForceUpdateCanvases();
            RectTransform area = rankingText.rectTransform;
            float rowHeight = Mathf.Max(48f, rankingText.fontSize * 1.8f);
            rankingText.text = "Ranking\n" + message;
            for (int i = Math.Min(5, response.records.Length) - 1; i >= 0; i--)
            {
                var row = response.records[i];
                var time = TimeSpan.FromMilliseconds(row.elapsed_ms);
                string newLine = string.Format("{0}위  {1:00}:{2:00}.{3:00}  {4}, {5}",
                    i + 1, (int)time.TotalMinutes, time.Seconds, time.Milliseconds / 10,
                    row.player1_name, row.player2_name);
                var item = new GameObject("Rank_" + (i + 1), typeof(RectTransform), typeof(TextMeshProUGUI));
                item.transform.SetParent(area, false);
                var label = item.GetComponent<TextMeshProUGUI>();
                label.font = rankingText.font;
                label.fontSharedMaterial = rankingText.fontSharedMaterial;
                label.fontSize = rankingText.fontSize;
                label.alignment = TextAlignmentOptions.Center;
                label.richText = false;
                label.enableWordWrapping = false;
                label.raycastTarget = false;
                label.text = newLine;
                label.color = !string.IsNullOrEmpty(currentRecordId) && row.record_id == currentRecordId
                    ? Color.yellow : Color.white;
                RectTransform rect = label.rectTransform;   
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(area.rect.width, rowHeight);
                Vector2 destination = new Vector2(0, -rowHeight * (i + 0.8f));
                Canvas canvas = rankingText.GetComponentInParent<Canvas>();
                float height = canvas ? ((RectTransform)canvas.rootCanvas.transform).rect.height : 1080f;
                Vector2 origin = destination + Vector2.up * (height + area.rect.height);
                float elapsed = 0;
                while (elapsed < slideSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    rect.anchoredPosition = Vector2.Lerp(origin, destination,
                        Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / slideSeconds)));
                    yield return null;
                }
                rect.anchoredPosition = destination;
                yield return new WaitForSecondsRealtime(0.8f);
            }
            if (response.records.Length == 0) rankingText.text = "아직 기록이 없습니다.\n" + message;
            PresentationComplete = true;
    }

}
