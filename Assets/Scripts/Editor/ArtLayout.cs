using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Example 폴더의 예시 그림대로 서류·단말기·결과 화면에 Resources/Portraits의 그림을 입힌다.
// 화면마다 한 번만: 이미 그림(RawImage)이 있으면 건드리지 않아 손으로 조정한 배치가 남는다.
public static partial class DeskSceneBuilder
{
    const string ArtPath = "Assets/Resources/Portraits/";

    static Texture2D Art(string name)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtPath}{name}.png");
        if (tex == null) Debug.LogError($"그림이 없습니다: {ArtPath}{name}.png");
        return tex;
    }

    static void ApplyArt(DeskFlow flow)
    {
        // 압축하지 않고, 도장 자국은 밉맵을 켜도록(CutsceneImageImport) 예전 설정으로 들어온 그림을 다시 가져온다
        foreach (var name in new[] { "서류", "기억단말기", "승인도장자국", "반려도장자국", "업무결과_성공", "업무결과_실패", "Ottilia_text", "발견목록" })
            if (AssetImporter.GetAtPath($"{ArtPath}{name}.png") is TextureImporter t && t.textureCompression != TextureImporterCompression.Uncompressed)
                AssetDatabase.ImportAsset(t.assetPath, ImportAssetOptions.ForceUpdate);
        DocumentArt(flow.view);
        FindingArt(flow.view.findings);
        TerminalArt(flow.terminal);
        ResultPanelArt(flow);
        ResultStampArt(flow.resultArt);
    }

    // Image(단색)를 같은 자리의 RawImage(그림)로 바꿈
    static RawImage Swap(GameObject go, Texture2D tex)
    {
        if (go.TryGetComponent<Image>(out var old)) Object.DestroyImmediate(old);
        var raw = go.AddComponent<RawImage>();
        raw.texture = tex;
        return raw;
    }

    // 1920x1080 그림의 픽셀 사각형 자리 (왼쪽 위 0,0)
    static void Px(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        rt.pivot = new Vector2(0.5f, 0.5f);
        Place(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2((x0 + x1) / 2f, -(y0 + y1) / 2f), new Vector2(x1 - x0, y1 - y0));
    }

    static void FullScreen(RectTransform rt)
    {
        rt.pivot = new Vector2(0.5f, 0.5f);
        Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920f, 1080f));
    }

    // ── 서류 ──────────────────────────────

    static void DocumentArt(DocumentView view)
    {
        var app = (RectTransform)view.applicationFields.parent;
        var con = (RectTransform)view.contractFields.parent;
        var look = (RectTransform)view.lookupFields.parent;
        var tex = Art("서류");
        if (tex == null || app.GetComponent<RawImage>() != null) return;

        Paper(app, view.applicationFields, "기억 거래 신청서", -686f, tex);
        Paper(con, view.contractFields, "기억 거래 계약서", -146f, tex);
        Paper(look, view.lookupFields, "기억 조회 기록", 394f, tex);

        // 조회 전 안내, [기억 복원] 버튼
        view.lookupHint.fontSize = 22;
        DocumentView.At(view.lookupHint.rectTransform, 119f, 540f, 905f, 830f);
        if (view.restoreButton != null) DocumentView.At((RectTransform)view.restoreButton.transform, 300f, 950f, 725f, 1060f);

        // 발견 목록은 좁아진 폭에 맞춰 글씨를 줄이고 줄을 높인다
        var finding = (RectTransform)view.findings.transform;
        Place(finding, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(810f, 30f), new Vector2(284f, 860f));
        view.findings.itemTemplate.GetComponentInChildren<Text>(true).fontSize = 20;
        if (view.findings.itemTemplate.TryGetComponent<LayoutElement>(out var item)) item.minHeight = 88f;

        // 계약서 도장 자국: 예전 글자 자국 대신 그림
        var old = con.Find("StampMark");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var stamp = new GameObject("StampImage", typeof(RectTransform)).AddComponent<RawImage>();
        stamp.transform.SetParent(con, false);
        stamp.raycastTarget = false;
        stamp.color = new Color(1f, 1f, 1f, 0.9f);
        stamp.texture = Art("승인도장자국");
        DocumentView.At(stamp.rectTransform, 570f, 1276f, 910f, 1533f); // 그림 비율 1452:1083
        var center = (stamp.rectTransform.anchorMin + stamp.rectTransform.anchorMax) / 2f;
        Place(stamp.rectTransform, center, center, new Vector2(36f, 36f), new Vector2(210f, 157f));
        stamp.gameObject.SetActive(false);
        view.stampImage = stamp;
        EditorUtility.SetDirty(view);
        Debug.Log("[변경] 서류 화면: 서류 그림과 도장 자국 적용");
    }

    static void Paper(RectTransform paper, RectTransform fields, string title, float x, Texture2D tex)
    {
        var raw = Swap(paper.gameObject, tex);
        var c = DocumentView.Crop;
        raw.uvRect = new Rect(c.xMin / 1024f, 1f - c.yMax / 1536f, c.width / 1024f, c.height / 1536f);
        Place(paper, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 30f), new Vector2(532f, 860f));

        if (fields.TryGetComponent<LayoutGroup>(out var layout)) Object.DestroyImmediate(layout);
        fields.pivot = new Vector2(0.5f, 0.5f);
        Place(fields, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var label = paper.Find("Title").GetComponent<Text>();
        label.text = title;
        label.fontSize = 30;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        DocumentView.At(label.rectTransform, 300f, 100f, 725f, 178f);
        EditorUtility.SetDirty(label);
    }

    // ── 발견 목록 ──────────────────────────────

    static void FindingArt(FindingPanel panel)
    {
        var rt = (RectTransform)panel.transform;
        var tex = Art("발견목록");
        if (tex == null || panel.GetComponent<RawImage>() != null) return;

        // 그림(1024x1536)에서 종이 부분만. 둘레의 그림자는 뺌
        const float x0 = 265f, y0 = 58f, w = 495f, h = 1449f;
        var raw = Swap(panel.gameObject, tex);
        raw.uvRect = new Rect(x0 / 1024f, 1f - (y0 + h) / 1536f, w / 1024f, h / 1536f);
        Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(810f, 30f), new Vector2(288f, 860f));
        void At(RectTransform r, float left, float top, float right, float bottom)
        {
            r.pivot = new Vector2(0.5f, 0.5f);
            Place(r, new Vector2((left - x0) / w, 1f - (bottom - y0) / h), new Vector2((right - x0) / w, 1f - (top - y0) / h), Vector2.zero, Vector2.zero);
        }

        var title = rt.Find("Title");
        if (title != null) title.gameObject.SetActive(false); // 제목은 그림에 있음

        // 줄 간격 = 그림에 그려진 선 간격(107px). 다섯 줄이 넘으면 그 아래 빈자리에 이어 쓴다
        At(panel.listContainer, 306f, 371f, 720f, 1340f);
        panel.listContainer.GetComponent<VerticalLayoutGroup>().spacing = 0f;
        var item = panel.itemTemplate;
        Transparent(item);
        item.GetComponent<LayoutElement>().minHeight = 107f / h * 860f;
        var text = item.GetComponentInChildren<Text>(true);
        text.color = new Color(0.05f, 0.05f, 0.06f);
        text.resizeTextForBestFit = true; // 긴 항목 이름도 두 줄 안에
        text.resizeTextMinSize = 14;
        text.resizeTextMaxSize = text.fontSize = 20;
        text.rectTransform.offsetMin = new Vector2(22f, 3f); // 그림의 동그라미 오른쪽부터
        text.rectTransform.offsetMax = new Vector2(0f, -3f);

        var record = rt.Find("RecordButton");
        At((RectTransform)record, 390f, 1360f, 636f, 1450f);
        record.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.21f);
        var label = record.GetComponentInChildren<Text>(true);
        label.color = new Color(0.94f, 0.91f, 0.86f);
        label.fontSize = 28;

        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(item);
        Debug.Log("[변경] 발견 목록: 발견목록 그림 적용");
    }

    // ── 단말기 ──────────────────────────────

    static void TerminalArt(TerminalPanel terminal)
    {
        var box = terminal.panel.transform.Find("Box") as RectTransform;
        var tex = Art("기억단말기");
        if (box == null || tex == null || box.GetComponent<RawImage>() != null) return;

        terminal.panel.GetComponent<Image>().color = new Color(0.93f, 0.91f, 0.89f); // 16:9가 아닌 화면의 바깥쪽
        Swap(box.gameObject, tex);
        FullScreen(box);
        var title = box.Find("Title");
        if (title != null) title.gameObject.SetActive(false); // 제목은 그림에 있음

        var ink = new Color(0.14f, 0.13f, 0.13f);
        var prefix = box.Find("Prefix").GetComponent<Text>();
        prefix.color = ink;
        Px(prefix.rectTransform, 320f, 545f, 410f, 640f);

        var input = terminal.input;
        input.GetComponent<Image>().color = Color.clear; // 입력칸 테두리는 그림에 있음
        input.transition = Selectable.Transition.None;
        input.textComponent.color = ink;
        input.customCaretColor = true;
        input.caretColor = ink;
        Px((RectTransform)input.transform, 410f, 545f, 1300f, 640f);

        terminal.message.color = new Color(0.62f, 0.12f, 0.16f);
        terminal.message.alignment = TextAnchor.MiddleLeft;
        Px(terminal.message.rectTransform, 306f, 728f, 1621f, 788f);

        Invisible(box.Find("LookupButton"), 1327f, 545f, 1621f, 641f); // 글자는 그림에 있음
        Invisible(box.Find("CloseButton"), 73f, 967f, 213f, 1029f);
        EditorUtility.SetDirty(input);
        Debug.Log("[변경] 단말기 화면: 기억단말기 그림 적용");
    }

    // 그림에 그려진 버튼 위에 놓는 투명 버튼 (마우스를 올리면 살짝 어두워짐)
    static void Invisible(Transform tr, float x0, float y0, float x1, float y1)
    {
        Transparent(tr.GetComponent<Button>());
        foreach (var text in tr.GetComponentsInChildren<Text>(true)) text.enabled = false;
        Px((RectTransform)tr, x0, y0, x1, y1);
    }

    static void Transparent(Button button)
    {
        button.GetComponent<Image>().sprite = null;
        button.GetComponent<Image>().color = Color.white;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.clear, selectedColor = Color.clear, disabledColor = Color.clear,
            highlightedColor = new Color(0f, 0f, 0f, 0.12f), pressedColor = new Color(0f, 0f, 0f, 0.25f),
            colorMultiplier = 1f, fadeDuration = 0.1f,
        };
        EditorUtility.SetDirty(button);
    }

    // ── 결과 화면 ──────────────────────────────

    static void ResultPanelArt(DeskFlow flow)
    {
        var paper = flow.result.panel.transform.Find("Paper") as RectTransform;
        var tex = Art("업무결과_성공");
        if (paper == null || tex == null || paper.GetComponent<RawImage>() != null) return;

        flow.result.panel.GetComponent<Image>().color = new Color(0.1f, 0.05f, 0.04f);
        var result = flow.result.panel.AddComponent<ResultArt>();
        result.art = Swap(paper.gameObject, tex);
        FullScreen(paper);

        var ink = new Color(0.23f, 0.13f, 0.11f);
        var wine = new Color(0.3f, 0.12f, 0.2f);
        ResultText(paper, "Logo", "N U M B", 40, wine, TextAnchor.MiddleCenter, 700f, 122f, 1220f, 178f);
        ResultText(paper, "LogoSub", "M E M O R Y   E X T R A C T I O N   I N S T I T U T I O N", 15, wine, TextAnchor.MiddleCenter, 600f, 178f, 1320f, 204f);
        result.headline = ResultText(paper, "Headline", "1일차 통과", 32, ink, TextAnchor.MiddleCenter, 600f, 212f, 1320f, 258f);
        result.headline.fontStyle = FontStyle.Bold;
        result.phrase = ResultText(paper, "Phrase", "", 24, ink, TextAnchor.MiddleCenter, 560f, 258f, 1360f, 300f);

        string[] labels = { "획득 금액", "놓친 금액", "최종 획득 금액" };
        float[] tops = { 472f, 578f, 688f }, bottoms = { 576f, 682f, 798f };
        result.values = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            int size = i == 2 ? 48 : 42;
            var label = ResultText(paper, $"Label{i}", labels[i], size, ink, TextAnchor.MiddleLeft, 451f, tops[i], 930f, bottoms[i]);
            result.values[i] = ResultText(paper, $"Value{i}", "0", size, ink, TextAnchor.MiddleRight, 980f, tops[i], 1488f, bottoms[i]);
            if (i < 2) continue;
            result.totalLabel = label;
            label.fontStyle = result.values[i].fontStyle = FontStyle.Bold;
        }

        // 표 아래 덧붙임 (빚, 야근 금액, 놓친 오류)
        var body = flow.result.body;
        body.color = ink;
        body.fontSize = 24;
        body.alignment = TextAnchor.MiddleCenter;
        Px(body.rectTransform, 420f, 806f, 1500f, 880f);

        var button = paper.Find("Button");
        Invisible(button, 746f, 965f, 1176f, 1041f);
        var caption = flow.result.buttonLabel;
        caption.enabled = true;
        caption.color = new Color(0.93f, 0.85f, 0.78f);
        button.SetAsLastSibling();

        flow.resultArt = result;
        EditorUtility.SetDirty(body);
        EditorUtility.SetDirty(caption);
        EditorUtility.SetDirty(flow);
        Debug.Log("[변경] 결과 화면: 업무결과 그림 적용");
    }

    static Text ResultText(RectTransform paper, string name, string content, int size, Color color, TextAnchor anchor, float x0, float y0, float x1, float y1)
    {
        var text = MakeText(paper, name, content, size, color, anchor);
        text.verticalOverflow = VerticalWrapMode.Overflow;
        Px(text.rectTransform, x0, y0, x1, y1);
        return text;
    }

    // 오른쪽 위 도장 그림 (예전 글자 도장이 있으면 바꿈)
    static void ResultStampArt(ResultArt result)
    {
        if (result == null || result.stamp is RawImage) return;
        foreach (var name in new[] { "업무결과_승인", "업무결과_반려" })
            if (AssetImporter.GetAtPath($"{ArtPath}{name}.png") is TextureImporter t && (!t.mipmapEnabled || t.textureCompression != TextureImporterCompression.Uncompressed))
                AssetDatabase.ImportAsset(t.assetPath, ImportAssetOptions.ForceUpdate);
        var tex = Art("업무결과_승인");
        if (tex == null) return;
        if (result.stamp != null) Object.DestroyImmediate(result.stamp.gameObject);
        var stamp = new GameObject("StampImage", typeof(RectTransform)).AddComponent<RawImage>();
        stamp.transform.SetParent(result.art.transform, false);
        stamp.texture = tex;
        stamp.raycastTarget = false;
        Px(stamp.rectTransform, 1355f, 125f, 1645f, 415f);
        stamp.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 9f);
        result.stamp = stamp;
        EditorUtility.SetDirty(result);
        Debug.Log("[변경] 결과 화면: 도장 그림 적용");
    }
}
