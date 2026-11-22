using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 결과 화면 내용. 세 줄 표(획득 / 놓친 / 최종 금액) + 위쪽 제목·한마디 + 아래쪽 덧붙임
public class DayReport
{
    public bool Success;
    public string Headline, Phrase, Note;
    public int Earned, Lost, Total;

    // 결과 화면 그림이 씬에 없을 때(Add Missing Objects 전) 쓰는 글
    public string Plain => $"<b>{Headline}</b>\n\n{Phrase}\n\n획득 금액: {Earned:N0}\n놓친 금액: {Lost:N0}\n최종 획득 금액: {Total:N0}\n\n{Note}";
}

// 결과 화면 전체 그림 (Resources/Portraits/업무결과_성공·실패). "업무 결과" 제목과 표 선은 그림에 그려져 있다.
public class ResultArt : MonoBehaviour
{
    public RawImage art;
    public Text headline, phrase;
    [Tooltip("오른쪽 위 도장 그림 (업무결과_승인 / 업무결과_반려). 자리·크기는 이 오브젝트를 옮겨서 조정")]
    public Graphic stamp;
    [Tooltip("도장이 처음 나타날 때 배율")]
    public float stampStart = 2.6f;
    [Tooltip("위에서부터 획득 금액, 놓친 금액, 최종 획득 금액")]
    public Text[] values;
    [Tooltip("최종 획득 금액 이름표 (성공·실패 색을 함께 바꿈)")]
    public Text totalLabel;

    public void Set(DayReport r)
    {
        art.texture = Resources.Load<Texture2D>(r.Success ? "Portraits/업무결과_성공" : "Portraits/업무결과_실패");
        headline.text = r.Headline;
        phrase.text = r.Phrase;
        values[0].text = $"{r.Earned:N0}";
        values[1].text = $"{r.Lost:N0}";
        values[2].text = $"{r.Total:N0}";
        totalLabel.color = values[2].color = r.Success ? new Color(0.11f, 0.25f, 0.13f) : new Color(0.33f, 0.09f, 0.2f);
        stamp.gameObject.SetActive(false); // 결과 화면이 다 밝아진 뒤 PlayStamp로 찍음
        if (stamp is RawImage image) image.texture = Resources.Load<Texture2D>(r.Success ? "Portraits/업무결과_승인" : "Portraits/업무결과_반려");
    }

    public void PlayStamp()
    {
        if (stamp is RawImage && isActiveAndEnabled) StartCoroutine(StampRun());
    }

    // 크게 나타났다가 쾅 작아지고(살짝 눌림) 제 크기로
    IEnumerator StampRun()
    {
        yield return new WaitForSeconds(0.3f);
        stamp.gameObject.SetActive(true);
        yield return Scale(stampStart, 0.9f, 0.22f, 0f);
        yield return Scale(0.9f, 1f, 0.12f, 1f);
    }

    IEnumerator Scale(float from, float to, float time, float alpha0)
    {
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float k = t / time;
            Apply(Mathf.Lerp(from, to, k * k * k), Mathf.Lerp(alpha0, 1f, k));
            yield return null;
        }
        Apply(to, 1f);
    }

    void Apply(float scale, float alpha)
    {
        stamp.rectTransform.localScale = Vector3.one * scale;
        stamp.color = new Color(1f, 1f, 1f, alpha);
    }
}
