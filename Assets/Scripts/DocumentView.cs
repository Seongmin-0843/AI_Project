using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 진행 중인 거래의 신청서·계약서·조회 기록을 2D 화면에 펼친다. 찍은 도장은 계약서에 도장 자국으로 표시.
// 종이 그림은 Resources/Portraits/서류 (1024x1536). 글자·칸 자리는 모두 그 그림의 픽셀 좌표(왼쪽 위 0,0)로 적는다.
public class DocumentView : MonoBehaviour
{
    public GameObject panel;
    public RectTransform applicationFields, contractFields;
    [Tooltip("조회 기록 칸 (M6). 조회 대상 거래에서만 보이고, 조회 전에는 lookupHint만 보인다")]
    public RectTransform lookupFields;
    public Text lookupHint;
    [Tooltip("조회 기록 칸의 [기억 복원] 버튼 (복원 거래에서 조회 후, 복원을 끝내기 전까지 보임)")]
    public GameObject restoreButton;
    [Tooltip("글꼴과 글자색을 여기서 가져온다")]
    public FieldRow rowTemplate;
    public FindingPanel findings;
    public Text debugLabel;
    [Tooltip("계약서에 찍히는 도장 자국 (승인도장자국 / 반려도장자국)")]
    public RawImage stampImage;
    [Tooltip("[규정 안내문] 버튼 (M7). 규정 심사가 있는 스테이지 3부터 보인다")]
    public Button rulesButton;
    [Header("글자 크기")]
    public int valueFontSize = 26, labelFontSize = 22, headingFontSize = 29, noteFontSize = 22, sealFontSize = 19;

    // 지금까지 누적된 규정 문장 (DeskFlow가 하루를 시작할 때 넣음)
    public List<string> Rules { get; set; } = new List<string>();

    RectTransform rulesSheet, consentSheet; // 계약서 / 신청서 위에 겹쳐 펴는 종이

    void Awake() => panel.SetActive(false);

    public void Close() => panel.SetActive(false);

    // 종이 그림에서 화면에 보이는 부분 (둘레의 회색 바탕은 뺌)
    public static readonly Rect Crop = Rect.MinMaxRect(55f, 62f, 970f, 1483f);

    // 종이 그림의 픽셀 사각형 자리에 맞춤
    public static void At(RectTransform rt, float x0, float y0, float x1, float y1)
    {
        rt.anchorMin = new Vector2((x0 - Crop.xMin) / Crop.width, 1f - (y1 - Crop.yMin) / Crop.height);
        rt.anchorMax = new Vector2((x1 - Crop.xMin) / Crop.width, 1f - (y0 - Crop.yMin) / Crop.height);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    public void Show(Transaction tx)
    {
        findings.ClearSelection(); // 발견 목록은 거래가 끝날 때까지 유지, 선택만 해제
        ShowApplication(tx);
        ShowContract(tx);
        if (lookupFields != null) ShowLookup(tx);
        ShowRules();
        ShowConsent(tx);

        if (stampImage != null)
        {
            stampImage.gameObject.SetActive(tx.Stamp != Stamp.None);
            stampImage.texture = MarkTexture(tx.Stamp == Stamp.Approve);
        }

        var t = tx.Template;
        if (debugLabel != null) debugLabel.text = $"[{t.Id}] 정답 {t.Answer} {string.Join(", ", t.Errors.ConvertAll(e => $"{e.Kind}@{e.Doc}"))}";
        panel.SetActive(true);
    }

    public static Texture2D MarkTexture(bool approve) => Resources.Load<Texture2D>(approve ? "Portraits/승인도장자국" : "Portraits/반려도장자국");

    void ShowApplication(Transaction tx)
    {
        var t = tx.Template;
        var c = applicationFields;
        const string doc = "신청서";
        Clear(c);
        HeadRow(c, doc, 0, "Application.Name", "신청인", tx.AppName);
        HeadRow(c, doc, 1, null, "기억 번호", tx.Number); // 번호는 서류끼리 늘 같아 고를 항목이 아님
        if (t.Lookup) HeadRow(c, doc, 2, "Application.Age", "기억 저장 시기", $"{t.Age}세 때"); // 조회 기록과 비교할 칸 (스테이지 2부터)
        else HeadRow(c, doc, 2, null, "거래 유형", "기억 판매");

        Heading(c, "기억 정보", 583f);
        if (t.Lookup) Row(c, doc, "Application.Kind", "기억 종류", t.MemoryKind, 629f, 686f);
        Row(c, doc, "Application.Desc", "기억 설명", t.AppDesc, t.Lookup ? 686f : 629f, t.Lookup ? 806f : 749f);

        Heading(c, "거래 정보", 890f);
        if (t.Lookup) Row(c, doc, null, "거래 유형", "기억 판매", 934f, 986f);
        string consent = t.Consent == "Y" ? "첨부" : t.Consent == "N" ? "없음" : "해당 없음";
        Row(c, doc, "Application.Attachment", "첨부 서류", $"보호자 동의서: {consent}", t.Lookup ? 986f : 934f, t.Lookup ? 1038f : 986f);
        if (t.Consent == "Y") MakeButton(c, "보기", 780f, 990f, 915f, 1034f, () => consentSheet.gameObject.SetActive(true));

        Review(c, t.Lookup);
        SignRow(c, doc, "Application.Sign", t.AppSign ? tx.AppName : "");
        Seal(c, tx.Stamp == Stamp.None ? "검토\n대기" : "검토\n완료");
    }

    void ShowContract(Transaction tx)
    {
        var t = tx.Template;
        var c = contractFields;
        const string doc = "계약서";
        Clear(c);
        HeadRow(c, doc, 0, "Contract.Name", "계약자", tx.ConName);
        HeadRow(c, doc, 1, null, "기억 번호", tx.Number);
        HeadRow(c, doc, 2, null, "거래 유형", "기억 판매");
        Heading(c, "기억 정보", 583f);
        Row(c, doc, "Contract.Desc", "기억 설명", t.ConDesc, 629f, 749f);
        Heading(c, "계약 내용", 890f);
        Note(c, "계약자는 위 기억의 거래에 동의합니다.", 930f);
        Review(c, t.Lookup);
        SignRow(c, doc, "Contract.Sign", t.ConSign ? tx.ConName : "");
        if (tx.Stamp == Stamp.None) Seal(c, "검토\n대기"); // 찍은 뒤에는 도장 자국(stampImage)
    }

    void ShowLookup(Transaction tx)
    {
        var t = tx.Template;
        var c = lookupFields;
        const string doc = "조회 기록";
        Layout(t.Lookup);
        c.parent.gameObject.SetActive(t.Lookup);
        lookupHint.gameObject.SetActive(!tx.LookedUp);
        var rec = tx.Record; // 번호를 잘못 입력했으면 다른 사람의 기록
        Clear(c);
        Seal(c, tx.LookedUp ? "조회\n완료" : "조회\n대기");
        if (tx.LookedUp)
        {
            HeadRow(c, doc, 0, null, "기억 번호", tx.LookupNumber); // 오입력 확인용으로 보기만 함
            if (!rec.Template.Registered) HeadRow(c, doc, 1, "Lookup.Unregistered", "등록 여부", "등록되지 않은 번호");
            else
            {
                HeadRow(c, doc, 1, "Lookup.Owner", "소유자", rec.Owner);
                HeadRow(c, doc, 2, "Lookup.Age", "기억 저장 시기", $"{rec.Template.RecordAge}세 때");
                Heading(c, "기억 정보", 583f);
                Row(c, doc, "Lookup.Kind", "기억 종류", rec.Template.RecordKind, 629f, 686f);
                if (rec.IsRestore)
                {
                    Heading(c, "기억 복원", 890f); // 복원 전에는 이 아래에 [기억 복원] 버튼
                    if (rec.RestoreDone) Row(c, doc, "Restore.Result", "복원된 기억", rec.Memory.Result, 934f, 1084f);
                }
            }
        }
        if (restoreButton != null) restoreButton.SetActive(rec.IsRestore && tx.LookedUp && rec.Template.Registered && !rec.RestoreDone);
    }

    // ── 규정 안내문·보호자 동의서 (M7): 같은 종이를 계약서·신청서 위에 겹쳐 편다 ──

    // [규정 안내문] 버튼: 계약서 자리에 규정을 폈다 접는다. 고른 항목은 그대로라 규정 → 서류 항목을 이어서 고를 수 있다
    public void ToggleRules() => SetRules(!rulesSheet.gameObject.activeSelf);

    void SetRules(bool on)
    {
        rulesSheet.gameObject.SetActive(on);
        rulesButton.GetComponentInChildren<Text>().text = on ? "계약서 보기" : "규정 안내문";
    }

    void ShowRules()
    {
        if (rulesButton == null) return; // 메뉴 NUMB > Add Missing Objects 실행 전
        rulesButton.gameObject.SetActive(Rules.Count >= 3);
        var c = Sheet(ref rulesSheet, contractFields, "거래 규정 안내문");
        SetRules(false);
        const string doc = "거래";
        HeadRow(c, doc, 0, null, "시행 기관", "기억 추출소");
        HeadRow(c, doc, 1, null, "책임자", "오틸리아");
        HeadRow(c, doc, 2, null, "적용 규정", $"규정 1~{Rules.Count}");
        // 규정 1·2는 서류끼리 비교하는 규정이라 글만, 규정 위반의 근거가 되는 3부터 고를 수 있다
        float[] tops = { 540f, 685f, 852f, 973f }, bottoms = { 685f, 830f, 973f, 1094f };
        for (int i = 0; i < Rules.Count && i < tops.Length; i++)
            Row(c, doc, i < 2 ? null : $"Rule.{i + 1}", $"규정 {i + 1}", Rules[i], tops[i], bottoms[i], noteFontSize);
        Heading(c, "심사 방법", 1152f);
        Note(c, "규정을 어긴 거래는 규정과 해당 항목을\n함께 골라 기록하고 반려하세요.", 1190f);
        SignRow(c, doc, null, "오틸리아");
        Seal(c, "규정\n시행");
    }

    // 동의서 내용은 신청서와 늘 같다 (비교할 항목 없음, 있는지만 확인하는 서류)
    void ShowConsent(Transaction tx)
    {
        var c = Sheet(ref consentSheet, applicationFields, "보호자 동의서");
        if (tx.Template.Consent != "Y") return;
        const string doc = "동의서";
        HeadRow(c, doc, 0, null, "거래자", tx.AppName);
        HeadRow(c, doc, 1, null, "기억 번호", tx.Number);
        HeadRow(c, doc, 2, null, "저장 시기", $"{tx.Template.Age}세 때");
        Heading(c, "기억 정보", 583f);
        Row(c, doc, null, "기억 설명", tx.Template.AppDesc, 629f, 749f);
        Heading(c, "동의 내용", 890f);
        Note(c, "보호자로서 위 기억의 거래 내용을 확인했으며,\n거래에 동의합니다.", 930f);
        MakeButton(c, "신청서 보기", 365f, 1170f, 660f, 1250f, () => consentSheet.gameObject.SetActive(false));
        string[] given = { "영호", "미숙", "정희", "상훈" };
        SignRow(c, doc, null, (tx.AppName.StartsWith("남궁") ? "남궁" : tx.AppName.Substring(0, 1)) + given[int.Parse(tx.Number.Substring(2)) % given.Length]);
        Seal(c, "동의\n완료");
    }

    // fields가 놓인 종이를 똑같이 덮는 종이 (처음 한 번 만들고, 부를 때마다 비우고 접어 둠)
    RectTransform Sheet(ref RectTransform sheet, RectTransform fields, string title)
    {
        if (sheet == null)
        {
            var paper = fields.parent.GetComponent<RawImage>();
            var copy = new GameObject(title, typeof(RectTransform)).AddComponent<RawImage>();
            copy.texture = paper.texture;
            copy.uvRect = paper.uvRect;
            sheet = copy.rectTransform;
            sheet.SetParent(paper.transform, false);
            sheet.anchorMin = Vector2.zero;
            sheet.anchorMax = Vector2.one;
            sheet.offsetMin = sheet.offsetMax = Vector2.zero;
        }
        sheet.gameObject.SetActive(false);
        Clear(sheet);
        var t = MakeText(sheet, title, 30, TextAnchor.MiddleCenter);
        t.fontStyle = FontStyle.Bold;
        At(t.rectTransform, 300f, 100f, 725f, 178f);
        return sheet;
    }

    // 종이 위의 작은 버튼
    void MakeButton(RectTransform c, string label, float x0, float y0, float x1, float y1, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(c, false);
        At((RectTransform)go.transform, x0, y0, x1, y1);
        go.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.21f);
        go.GetComponent<Button>().onClick.AddListener(action);
        var t = MakeText(go.transform, label, noteFontSize, TextAnchor.MiddleCenter);
        t.color = new Color(0.94f, 0.91f, 0.86f);
        Anchor(t.rectTransform, 0f, 1f, 0f, 0f);
    }

    // 조회 기록 칸이 없는 거래(스테이지 1)는 빈자리 없이 신청서·계약서를 오른쪽, 발견 목록을 왼쪽으로 반씩 당겨 가운데로 모은다
    RectTransform[] cols;
    float[] baseX;
    float shift;
    void Layout(bool lookup)
    {
        if (cols == null)
        {
            cols = new[] { (RectTransform)applicationFields.parent, (RectTransform)contractFields.parent, (RectTransform)findings.transform };
            baseX = System.Array.ConvertAll(cols, c => c.anchoredPosition.x);
            var lp = (RectTransform)lookupFields.parent;
            shift = (lp.anchoredPosition.x + lp.rect.width / 2f - (cols[1].anchoredPosition.x + cols[1].rect.width / 2f)) / 2f; // (조회 칸 폭 + 간격) / 2
        }
        float s = lookup ? 0f : shift;
        for (int i = 0; i < cols.Length; i++)
            cols[i].anchoredPosition = new Vector2(baseX[i] + (i < 2 ? s : -s), cols[i].anchoredPosition.y);
    }

    // ── 종이 위에 그리기 ──────────────────────────────

    const float Left = 99f, Split = 275f, Right = 927f; // 표의 왼쪽 끝, 이름표와 값 사이, 오른쪽 끝
    static readonly Color Line = new Color(0.15f, 0.15f, 0.15f), LabelFill = new Color(0.8f, 0.77f, 0.69f, 0.6f), SealInk = new Color(0.55f, 0.14f, 0.12f);

    static void Clear(RectTransform container)
    {
        foreach (Transform child in container) Destroy(child.gameObject);
    }

    // 종이 그림에 칸이 그려져 있는 위쪽 표 세 줄
    void HeadRow(RectTransform c, string doc, int i, string id, string label, string value) =>
        MakeRow(c, doc, id, label, value, 330f + 48f * i, 378f + 48f * i, Right);

    // 칸을 직접 그리는 줄
    void Row(RectTransform c, string doc, string id, string label, string value, float y0, float y1, int size = 0)
    {
        MakeFill(c, LabelFill, Left, y0, Split, y1);
        MakeRow(c, doc, id, label, value, y0, y1, Right, size);
        MakeLine(c, Left, Right, y0);
        MakeLine(c, Left, Right, y1);
        MakeFill(c, Line, Split - 1f, y0, Split + 1f, y1);
    }

    void SignRow(RectTransform c, string doc, string id, string name) =>
        MakeRow(c, doc, id, "서명", name.Length > 0 ? $"<i>{name}</i>" : "", 1350f, 1440f, 745f);

    // 항목 하나 = 클릭할 수 있는 줄. 오브젝트 이름 = 항목 ID (id가 없으면 비교 대상이 아닌 글)
    void MakeRow(RectTransform c, string doc, string id, string label, string value, float y0, float y1, float right, int size = 0)
    {
        var go = new GameObject(id ?? "Info", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(c, false);
        At((RectTransform)go.transform, Left, y0, right, y1);
        go.GetComponent<Image>().color = Color.clear;
        if (id == null) go.GetComponent<Image>().raycastTarget = false;
        else
        {
            var row = go.AddComponent<FieldRow>();
            row.Id = id;
            row.Label = $"{doc} {label}";
            row.panel = findings;
        }
        float split = (Split - Left) / (right - Left);
        var l = MakeText(go.transform, label.Replace("기억 저장 시기", "저장 시기"), labelFontSize, TextAnchor.MiddleLeft);
        l.resizeTextForBestFit = true; // 긴 이름표는 칸에 맞게 줄임
        l.resizeTextMinSize = 12;
        l.resizeTextMaxSize = labelFontSize;
        l.verticalOverflow = VerticalWrapMode.Truncate;
        l.fontStyle = FontStyle.Bold;
        Anchor(l.rectTransform, 0f, split, 6f, 1f);
        var v = MakeText(go.transform, value, size > 0 ? size : valueFontSize, TextAnchor.MiddleLeft);
        v.color = new Color(0.05f, 0.05f, 0.06f); // 글꼴이 가늘어 더 진하게
        Anchor(v.rectTransform, split, 1f, 14f, 4f);
        // 한 줄을 넘치면 가운데 띄어쓰기에서 나눠, 끝 글자만 다음 줄로 떨어지지 않게 (두 줄까지)
        float width = (right - Split) / Crop.width * c.rect.width - 18f;
        if (size > 0 || v.preferredWidth <= width) return; // 작은 글씨(규정 문장)는 자동 줄바꿈에 맡김
        int space = -1;
        for (int i = 0; i < value.Length; i++)
            if (value[i] == ' ' && (space < 0 || Mathf.Abs(i - value.Length / 2) < Mathf.Abs(space - value.Length / 2))) space = i;
        if (space >= 0) v.text = value.Substring(0, space) + "\n" + value.Substring(space + 1);
    }

    void Heading(RectTransform c, string text, float y)
    {
        var t = MakeText(c, text, headingFontSize, TextAnchor.MiddleCenter);
        t.fontStyle = FontStyle.Bold;
        At(t.rectTransform, 400f, y - 35f, 625f, y + 35f);
        MakeLine(c, 98f, 396f, y);
        MakeLine(c, 629f, 927f, y);
    }

    void Review(RectTransform c, bool lookup)
    {
        Heading(c, "검토 사항", 1152f);
        Note(c, lookup ? "서류와 조회된 기억 정보의 일치 여부를\n확인해 주세요." : "신청서와 계약서의 일치 여부를\n확인해 주세요.", 1190f);
    }

    void Note(RectTransform c, string text, float y)
    {
        var t = MakeText(c, text, noteFontSize, TextAnchor.UpperLeft);
        At(t.rectTransform, 119f, y, 910f, y + 110f);
    }

    // 오른쪽 아래 동그라미 안 글씨
    void Seal(RectTransform c, string text)
    {
        var t = MakeText(c, text, sealFontSize, TextAnchor.MiddleCenter);
        t.fontStyle = FontStyle.Bold;
        t.color = SealInk;
        At(t.rectTransform, 797f, 1340f, 907f, 1450f);
    }

    Text MakeText(Transform parent, string content, int size, TextAnchor anchor)
    {
        var source = rowTemplate.GetComponentInChildren<Text>(true);
        var t = new GameObject("Text", typeof(RectTransform)).AddComponent<Text>();
        t.transform.SetParent(parent, false);
        t.font = source.font;
        t.color = source.color;
        t.fontSize = size;
        t.alignment = anchor;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.text = content;
        return t;
    }

    static void MakeLine(RectTransform c, float x0, float x1, float y) => MakeFill(c, Line, x0, y - 1f, x1, y + 1f);

    static void MakeFill(RectTransform c, Color color, float x0, float y0, float x1, float y1)
    {
        var image = new GameObject("Line", typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(c, false);
        image.color = color;
        image.raycastTarget = false;
        At(image.rectTransform, x0, y0, x1, y1);
    }

    static void Anchor(RectTransform rt, float x0, float x1, float padLeft, float padRight)
    {
        rt.anchorMin = new Vector2(x0, 0f);
        rt.anchorMax = new Vector2(x1, 1f);
        rt.offsetMin = new Vector2(padLeft, 0f);
        rt.offsetMax = new Vector2(-padRight, 0f);
    }
}
