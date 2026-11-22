using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ExitSmokeScreen : MonoBehaviour
{
    public static bool IsPlaying { get; private set; }
    private Image cover;
    private RectTransform[] clouds;
    private CanvasGroup group;
    private Camera transitionCamera;
    private Texture2D texture;
    private Sprite sprite;
    private Vector2 origin;
    private float start, duration;
    private bool arrived;
    private string target;
    private Color green = new Color(0.025f, 0.11f, 0.045f);
    public static void Begin(Vector3 position, float seconds, string scene)
    {
        if (IsPlaying) return;
        var go = new GameObject("ExitSmokeScreen"); DontDestroyOnLoad(go);
        go.AddComponent<ExitSmokeScreen>().Build(position, seconds, scene);
    }
    private Image Image(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(transform, false); return go.GetComponent<Image>();
    }
    private void Build(Vector3 position, float seconds, string scene)
    {
        IsPlaying = true; target = scene; start = Time.unscaledTime; duration = Mathf.Max(1, seconds);
        Camera cam = null;
        foreach (var c in Camera.allCameras)
            if (c.isActiveAndEnabled && !c.targetTexture) { cam = c; break; }
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
        var cameraObject = new GameObject("TransitionCamera");
        cameraObject.transform.SetParent(transform, false);
        transitionCamera = cameraObject.AddComponent<Camera>();
        transitionCamera.clearFlags = CameraClearFlags.SolidColor;
        transitionCamera.backgroundColor = green;
        transitionCamera.cullingMask = 0;
        transitionCamera.depth = -100;
        gameObject.AddComponent<GraphicRaycaster>(); group = gameObject.AddComponent<CanvasGroup>();
        Vector3 point = cam ? cam.WorldToScreenPoint(position) : new Vector3(Screen.width / 2f, Screen.height / 2f, 1);
        origin = point.z > 0 ? new Vector2(point.x - Screen.width / 2f, point.y - Screen.height / 2f) : Vector2.zero;
        texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
        {
            float r = Vector2.Distance(new Vector2(x, y), Vector2.one * 63.5f) / 64;
            texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1-r) * Mathf.Lerp(0.4f, 1, Mathf.PerlinNoise(x/17f,y/17f))));
        }
        texture.Apply(); sprite = Sprite.Create(texture, new Rect(0,0,128,128), Vector2.one * 0.5f);
        clouds = new RectTransform[24];
        for (int i=0;i<24;i++)
        {
            var image = Image("Smoke"+i); image.sprite = sprite;
            var tint = Color.Lerp(green, new Color(0.12f,0.22f,0.1f), (i%4)/6f);
            tint.a = 0.08f; image.color = tint;
            image.raycastTarget = false; clouds[i] = image.rectTransform;
        }
        cover = Image("Cover"); cover.color = new Color(green.r,green.g,green.b,0);
        cover.rectTransform.anchorMin = Vector2.zero; cover.rectTransform.anchorMax = Vector2.one;
        cover.rectTransform.offsetMin = cover.rectTransform.offsetMax = Vector2.zero;
        SceneManager.sceneLoaded += Loaded;
    }
    private void Update()
    {
        if (arrived || !cover) return;
        float t = Mathf.Clamp01((Time.unscaledTime-start)/duration), size = Mathf.Max(Screen.width,Screen.height);
        for (int i=0;i<24;i++)
        {
            float a = i*2.39996f+t*0.2f;
            clouds[i].anchoredPosition = origin*(1-t)+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*size*0.65f*t*(0.25f+(i%6)/7f);
            clouds[i].sizeDelta = Vector2.one*Mathf.Lerp(80,size*1.8f,t);
            var color = clouds[i].GetComponent<Image>().color;
            color.a = Mathf.Lerp(0.08f, 0.5f, t);
            clouds[i].GetComponent<Image>().color = color;
        }
        cover.color = new Color(green.r,green.g,green.b,Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.55f,1,t)));
    }
    private void Loaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != target) { Destroy(gameObject); return; }
        arrived = true; StartCoroutine(Fade());
    }
    private IEnumerator Fade()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        float t=0;
        while(t<1) { t+=Time.unscaledDeltaTime; group.alpha=1-t; yield return null; }
        Destroy(gameObject);
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= Loaded; IsPlaying=false;
        if(sprite) Destroy(sprite); if(texture) Destroy(texture);
    }
}
