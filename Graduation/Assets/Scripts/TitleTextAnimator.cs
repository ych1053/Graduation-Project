using System.Collections;
using TMPro;
using UnityEngine;

public class TitleTextAnimator : MonoBehaviour
{
    public TMP_Text titleText;
    public float characterDelay = 0.15f;

    private const string fullText =
        "<color=red>Treasure</color> <color=blue>Chest</color>";

    private void OnEnable()
    {
        if (titleText == null)
            titleText = GetComponent<TMP_Text>();

        StopAllCoroutines();
        StartCoroutine(WriteTitle());
    }

    private IEnumerator WriteTitle()
    {
        titleText.text = "";

        // 리치 텍스트 태그를 글자로 출력하지 않고, 표시할 글자만 한 글자씩 추가
        string[] pieces =
        {
            "<color=red>Treasure</color>",
            "<color=blue>Chest</color>"
        };

        foreach (string piece in pieces)
        {
            if (piece.StartsWith("<color="))
            {
                int endTag = piece.IndexOf('>');
                int closeTag = piece.IndexOf("</color>");

                string colorTag = piece.Substring(0, endTag + 1);
                string textPart = piece.Substring(endTag + 1, closeTag - endTag - 1);

                foreach (char character in textPart)
                {
                    titleText.text += colorTag + character + "</color>";
                    yield return new WaitForSeconds(characterDelay);
                }
            }
            else
            {
                foreach (char character in piece)
                {
                    titleText.text += character;
                    yield return new WaitForSeconds(characterDelay);
                }
            }
        }
    }
}