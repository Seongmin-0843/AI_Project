using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 전체 화면 이미지 + 화면 아래 자막. 클릭 또는 스페이스바로 다음 줄, 마지막 줄 뒤에 onDone.
// 시작: 검은 화면에서 페이드 인 / 이미지가 바뀔 때: 검게 페이드 아웃 → 인 / 끝: 페이드 아웃 후 게임 화면으로 서서히 전환.
// 자막은 한 글자씩 나오고, 나오는 중에 클릭하면 그 줄이 바로 다 나온다.
public class CutscenePlayer : MonoBehaviour
{
    public GameObject panel;
    public RawImage image;
    public AspectRatioFitter fitter;
    public Text speaker, line;
    [Tooltip("페이드 한 번에 걸리는 시간(초)")]
    public float fadeTime = 0.6f;
    [Tooltip("자막이 1초에 나오는 글자 수")]
    public float charsPerSecond = 25f;

    CanvasGroup group, subtitle; // 전체(검은 배경 포함) / 자막 띠
    List<CutLine> lines;
    Action onDone, pendingCover, onEnd;
    bool playing, ending; // ending: 끝내는 페이드 중 (스킵을 두 번 누르지 않게)
    GameObject skipButton;

    public bool IsPlaying => playing;

    void Awake()
    {
        // 숨길 때 SetActive 대신 투명도를 쓴다 (꺼지면 이 오브젝트의 코루틴이 멈추므로)
        panel.SetActive(true);
        group = Group(panel);
        subtitle = Group(speaker.transform.parent.gameObject);
        skipButton = panel.transform.Find("SkipButton")?.gameObject;
        SetVisible(false);
    }

    // 화면이 완전히 검어졌을 때 한 번만 (스킵으로 덮는 도중에 끝나도 빠지지 않게)
    void Cover()
    {
        var c = pendingCover;
        pendingCover = null;
        c?.Invoke();
    }

    static CanvasGroup Group(GameObject go) => go.TryGetComponent<CanvasGroup>(out var g) ? g : go.AddComponent<CanvasGroup>();

    void SetVisible(bool on)
    {
        group.alpha = on ? 1f : 0f;
        group.blocksRaycasts = on; // 보이는 동안 뒤쪽 UI·3D 클릭을 막음
    }

    // fromGame: 게임 화면에서 검게 페이드한 뒤 시작 (false면 검은 화면에서 바로 시작 — 게임을 켜자마자 나오는 인트로)
    // onCovered: 화면이 완전히 검어졌을 때 한 번 (그 사이 책상 정리 등)
    // onEnding: 컷씬이 끝나고 검어졌을 때 (여기서 띄운 화면이 검은 화면에서 바로 밝아지며 나온다 — 결과 화면)
    // 대사가 없어도 fromGame이면 검게 → 다시 밝게 전환만 한다 (다음 날로 넘어갈 때)
    public void Play(List<CutLine> cutscene, Action done, bool fromGame = true, Action onCovered = null, Action onEnding = null)
    {
        lines = cutscene ?? new List<CutLine>();
        if (lines.Count == 0 && !fromGame) { onCovered?.Invoke(); onEnding?.Invoke(); done?.Invoke(); return; }
        onDone = done;
        pendingCover = onCovered;
        onEnd = onEnding;
        playing = true;
        ending = false;
        if (skipButton != null) skipButton.SetActive(lines.Count > 0); // 대사 없는 전환에는 스킵 버튼 없음
        StopAllCoroutines();
        StartCoroutine(Run(fromGame));
    }

    // [건너뛰기] 버튼: 남은 대사를 건너뛰고 끝내는 페이드로 바로 간다
    public void Skip()
    {
        if (!playing || ending) return;
        StopAllCoroutines();
        StartCoroutine(End());
    }

    IEnumerator Run(bool fromGame)
    {
        SetBrightness(0f);
        SetVisible(true);
        if (fromGame)
            for (float t = 0f; t < fadeTime; t += Dt)
            {
                group.alpha = t / fadeTime; // 게임 화면 → 검은 화면
                yield return null;
            }
        group.alpha = 1f;
        Cover();
        for (int i = 0; i < lines.Count; i++)
        {
            bool newImage = i == 0 || (lines[i].Image != null && lines[i].Image != image.texture);
            if (newImage && i > 0) yield return Fade(1f, 0f); // 이미지가 바뀌면 검게 → 바꾸고 → 다시 밝게
            ShowLine(lines[i]);
            if (newImage) yield return Fade(0f, 1f);

            yield return TypeLine(lines[i].Text);
            yield return null; // 글자를 다 띄운 클릭이 다음 줄 넘김으로 겹치지 않게
            while (!Pressed()) yield return null;
            yield return null; // 다음 줄로 넘긴 클릭이 다음 줄 타자를 바로 끝내지 않게
        }
        yield return End();
    }

    IEnumerator End()
    {
        ending = true;
        group.alpha = 1f; // 검게 덮는 페이드 도중에 스킵해도 덮은 상태에서 끝냄
        Cover();
        yield return Fade(image.color.r, 0f); // 지금 밝기에서 검게 (스킵하면 페이드 도중일 수도 있음)
        if (skipButton != null) skipButton.SetActive(false); // 밝아지는 다음 화면 위에 겹쳐 보이지 않게
        onEnd?.Invoke();
        for (float t = 0f; t < fadeTime; t += Dt)
        {
            group.alpha = 1f - t / fadeTime; // 검은 화면 → 게임 화면 (또는 onEnding이 띄운 화면)
            yield return null;
        }
        SetVisible(false);
        playing = false;
        onDone?.Invoke(); // 게임 화면이 다 드러난 뒤에 다음 화면(지시문 등)을 띄움 — 미리 띄우면 전환 중에 비쳐 보임
    }

    IEnumerator Fade(float from, float to)
    {
        for (float t = 0f; t < fadeTime; t += Dt)
        {
            SetBrightness(Mathf.Lerp(from, to, t / fadeTime));
            yield return null;
        }
        SetBrightness(to);
    }

    // 이미지와 자막을 함께 어둡게·밝게 (배경은 항상 검정)
    void SetBrightness(float k)
    {
        image.color = new Color(k, k, k, 1f);
        subtitle.alpha = k;
    }

    IEnumerator TypeLine(string text)
    {
        line.text = "";
        float shown = 0f;
        while (shown < text.Length)
        {
            if (Pressed()) break; // 나오는 중에 클릭하면 바로 다 보여 줌
            shown += charsPerSecond * Dt;
            line.text = text.Substring(0, Mathf.Min(text.Length, (int)shown));
            yield return null;
        }
        line.text = text;
    }

    // 게임 시작 직후처럼 한 프레임이 길어도 페이드·타자가 건너뛰지 않게 프레임 시간을 1/30초로 제한
    static float Dt => Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

    static bool Pressed() =>
        (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
        (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);

    void ShowLine(CutLine l)
    {
        if (l.Image != null)
        {
            image.texture = l.Image;
            fitter.aspectRatio = (float)l.Image.width / l.Image.height;
        }
        image.enabled = image.texture != null;
        speaker.text = l.Speaker;
        speaker.gameObject.SetActive(l.Speaker.Length > 0);
        line.text = "";
    }
}
