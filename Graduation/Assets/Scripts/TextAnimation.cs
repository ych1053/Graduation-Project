using UnityEngine;
using TMPro;
using System.Collections;

public class TextDelayAndWobbleManager : MonoBehaviour
{
    [Header("제어할 텍스트 오브젝트")]
    public GameObject targetTextObject;

    private TMP_Text textComponent;

    void Awake()
    {
        if (targetTextObject != null)
        {
            textComponent = targetTextObject.GetComponent<TMP_Text>();
            // 시작하자마자 텍스트 오브젝트를 숨깁니다.
            targetTextObject.SetActive(false);
        }
    }

    void Start()
    {
        // 매니저 오브젝트는 켜져 있으므로 코루틴이 멈추지 않고 정상 작동합니다.
        StartCoroutine(ShowTextAfterDelay());
    }

    IEnumerator ShowTextAfterDelay()
    {
        // 2초 동안 대기합니다.
        yield return new WaitForSeconds(2.5f);

        // 1초 뒤 텍스트 오브젝트를 활성화합니다.
        if (targetTextObject != null)
        {
            targetTextObject.SetActive(true);
        }
    }

    void Update()
    {
        // 텍스트 오브젝트가 아직 활성화되지 않았다면 흔들림 연산을 하지 않음
        if (targetTextObject == null || !targetTextObject.activeSelf) return;
        if (textComponent == null) return;

        // 텍스트 메시 정보 업데이트
        textComponent.ForceMeshUpdate();
        var textInfo = textComponent.textInfo;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            var charInfo = textInfo.characterInfo[i];

            // 공백이나 보이지 않는 문자는 건너뜀
            if (!charInfo.isVisible) continue;

            var meshInfo = textInfo.meshInfo[charInfo.materialReferenceIndex];

            // 글자 하나를 구성하는 4개의 꼭짓점 인덱스 가져오기
            int vertexIndex = charInfo.vertexIndex;

            // 위아래 흔들림 계산 (Mathf.Sin 활용)
            float wobble = Mathf.Sin(Time.time * 10f + i * 0.5f) * 5f;

            Vector3[] vertices = meshInfo.vertices;

            // 4개의 꼭짓점 Y축 좌표에 변형 주기
            vertices[vertexIndex + 0].y += wobble;
            vertices[vertexIndex + 1].y += wobble;
            vertices[vertexIndex + 2].y += wobble;
            vertices[vertexIndex + 3].y += wobble;
        }

        // 변경된 정점 데이터를 메시에 반영
        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            var meshInfo = textInfo.meshInfo[i];
            meshInfo.mesh.vertices = meshInfo.vertices;
            textComponent.UpdateGeometry(meshInfo.mesh, i);
        }
    }
}