using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 튜토리얼 서류에서 지금 할 일을 가리킨다.
// 1일차 2장 (1장 승인, 2장 계약서 이름 오류 반려), 2일차 1장 (기억 조회·복원 후 승인).
// 3D 물체는 빛나며 깜빡이고, 서류 화면 안에서는 화살표가 위에서 가리킨다. 위쪽 띠에 안내 문구.
// 단계는 매 프레임 지금 상태를 보고 정한다 → 순서를 벗어나거나 되돌려도 다음 할 일을 다시 가리킴. 클릭은 막지 않는다.
public class TutorialGuide : MonoBehaviour
{
    public DeskFlow flow;
    public DocumentView view;
    public FindingPanel findings;
    public ReasonDialog reasons;
    public Clickable pile, approveStamp, rejectStamp, approveTray, rejectTray;
    [Tooltip("비워 두면 씬의 Terminal 오브젝트를 찾아 씀")]
    public Clickable terminalDesk;
    public RectTransform closeButton, recordButton, confirmButton;
    public RectTransform arrow;
    [Tooltip("오틸리아 안내 창. 비어 있으면 안내 없이 진행 (메뉴 NUMB > Add Missing Objects로 추가)")]
    public OttiliaTalk talk;
    bool fromTop; // 이번 프레임 대상은 위에서 가리킴 (서류 화면 안이라도)

    static readonly Color Highlight = new Color(1f, 0.85f, 0.15f);

    const string NameKey = "Application.Name+Contract.Name";
    static readonly string[] OrderHints =
    {
        "카드 앞의 시점을 보세요. 가장 먼저 일어난 일을 클릭하세요.",
        "다음으로 일어난 일을 클릭하세요.",
        "마지막으로 일어난 일을 클릭하세요.",
    };
    Clickable glowing;
    Vector3 arrowScale;
    // 안내 문구는 아래쪽 안내 줄(DeskFlow.feedback)에 쓴다. 튜토리얼 동안은 이 오브젝트 아래로 옮겨 서류 화면 등 다른 창 위에 띄우고,
    // 어두운 네모 배경을 깔고, 서류 화면의 아래 버튼([닫기])과 겹치지 않게 조금 올린다. 끝나면 원래 자리로
    static readonly Color HintColor = new Color(0.95f, 0.93f, 0.88f);
    Transform feedbackParent;
    int feedbackIndex;
    Vector2 feedbackPos;
    Color feedbackColor;
    RectTransform hintBack;
    bool guiding;

    readonly HashSet<string> said = new HashSet<string>();        // 이미 나온 오틸리아 안내
    Transaction lastTutorial;

    // 화살표 그림은 코드로 만든 흰 삼각형을 쓴다 (기본 DropdownArrow는 어두운 회색이라 색을 입히면 탁해짐).
    // 크기는 씬의 TutorialGuide/Arrow 오브젝트 Width·Height 또는 Scale로 조절
    void Awake()
    {
        var arrowImage = arrow.GetComponent<Image>();
        arrowImage.sprite = MakeArrowSprite();
        arrowImage.color = Highlight;
        arrowScale = arrow.localScale;
        var fb = flow.feedback;
        feedbackParent = fb.transform.parent;
        feedbackIndex = fb.transform.GetSiblingIndex();
        feedbackPos = fb.rectTransform.anchoredPosition;
        feedbackColor = fb.color;
        fb.verticalOverflow = VerticalWrapMode.Overflow; // 긴 문구는 위로 한 줄 더

        var back = new GameObject("HintBack", typeof(RectTransform), typeof(Image));
        back.transform.SetParent(transform, false);
        back.transform.SetAsFirstSibling(); // 화살표·문구보다 뒤
        var backImage = back.GetComponent<Image>();
        backImage.color = new Color(0.1f, 0.1f, 0.12f, 0.9f);
        backImage.raycastTarget = false;
        hintBack = (RectTransform)back.transform;
        hintBack.pivot = new Vector2(0.5f, 0f);
        back.SetActive(false);
        if (talk != null) talk.gameObject.SetActive(false);
        if (terminalDesk == null) terminalDesk = GameObject.Find("Terminal")?.GetComponent<Clickable>();
    }

    void Update()
    {
        Clickable glow = null;
        fromTop = false;
        RectTransform point = null;
        string hint = null;
        var tx = flow.Current;
        if (tx != null && tx.Template.Tutorial && !flow.Busy)
        {
            lastTutorial = tx;
            if (tx.IsRestore) RestoreSteps(tx, ref glow, ref point, ref hint);
            else if (tx.Template.Answer == "Approve") ApproveSteps(tx, ref glow, ref point, ref hint);
            else RejectSteps(tx, ref glow, ref point, ref hint);
        }
        else if (lastTutorial != null && tx != lastTutorial && !flow.Busy) // 튜토리얼 서류를 다 처리함
        {
            Talk($"End.{lastTutorial.Template.Stage}");
            lastTutorial = null;
        }

        if (talk != null && talk.IsOpen) { glow = null; point = null; } // 오틸리아가 말하는 동안은 가리키지 않음
        if (ClickInteractor.Blocked) glow = null; // 도장 연출 중에는 물체 깜빡임 끔 (안내 문구는 그대로)
        if (glow != glowing)
        {
            if (glowing != null) glowing.SetGlow(false);
            if (glow != null) glow.SetGlow(true);
            glowing = glow;
        }

        arrow.gameObject.SetActive(point != null);
        // 서류 화면의 버튼은 왼쪽에서 (아래 안내 문구와 안 겹치게), 서류 칸(신청인·계약자)·복원 창 카드·단말기 입력칸은 위에서 가리킨다
        if (point != null) PointAt(point, fromLeft: !fromTop && !flow.restore.panel.activeSelf && !flow.terminal.panel.activeSelf);
        ShowHint(hint);
        // 오틸리아가 말하는 동안은 안내 줄을 숨김 (말풍선 뒤로 비쳐 보이지 않게)
        bool talking = talk != null && talk.IsOpen;
        flow.feedback.enabled = !talking;
        if (guiding) hintBack.gameObject.SetActive(!talking);
    }

    // 튜토리얼 동안은 원래 안내 문구 대신 튜토리얼 문구. 끝나면 손대지 않아 DeskFlow의 안내 문구가 그대로 뜬다
    void ShowHint(string hint)
    {
        var fb = flow.feedback;
        if (hint != null)
        {
            if (!guiding)
            {
                // 이 오브젝트는 화면 전체를 덮으므로 부모를 바꿔도 자리는 그대로. 순서: 배경 → 문구 → 화살표
                fb.transform.SetParent(transform, false);
                fb.transform.SetSiblingIndex(1);
                fb.color = HintColor;
                hintBack.gameObject.SetActive(true);
                guiding = true;
            }
            fb.text = Emphasis.Rich(hint, onDark: true);
            var rt = fb.rectTransform;
            rt.anchoredPosition = feedbackPos + (view.panel.activeSelf ? new Vector2(0f, 90f) : Vector2.zero);

            // 배경 네모: 글자 크기에 맞춤 (글자는 아래 가운데 정렬이라 아래 가운데 기준)
            var c = new Vector3[4];
            rt.GetWorldCorners(c); // 0 = 왼쪽 아래, 3 = 오른쪽 아래
            hintBack.position = (c[0] + c[3]) * 0.5f;
            hintBack.anchoredPosition += new Vector2(0f, -14f);
            hintBack.sizeDelta = new Vector2(Mathf.Min(fb.preferredWidth, rt.rect.width) + 60f, fb.preferredHeight + 28f);
        }
        else if (guiding)
        {
            fb.transform.SetParent(feedbackParent, false);
            fb.transform.SetSiblingIndex(feedbackIndex);
            fb.rectTransform.anchoredPosition = feedbackPos;
            fb.color = feedbackColor;
            hintBack.gameObject.SetActive(false);
            guiding = false;
        }
    }

    // 오틸리아 안내 (컷씬_대사.txt의 [Tutorial.<id>]). 한 번만 나오고, 대사가 없으면 그냥 지나간다
    void Talk(string id)
    {
        if (talk == null || talk.IsOpen || ClickInteractor.Blocked || !said.Add(id)) return;
        var lines = flow.Lines("Tutorial." + id);
        if (lines != null) talk.Show(lines);
    }

    void Talk(Transaction tx, string step) => Talk($"{tx.Template.Id}.{step}");

    // 조회 → 복원 → 비교 → (승인 단계). 복원 창·단말기 창이 서류 화면 위에 열리므로 위에 뜬 창부터 본다
    void RestoreSteps(Transaction tx, ref Clickable glow, ref RectTransform point, ref string hint)
    {
        var r = flow.restore;
        if (r.panel.activeSelf)
        {
            if (tx.RestoreDone) { point = (RectTransform)r.closeLabel.transform.parent; hint = "기억이 복원되었습니다. [서류로 돌아가기]를 누르세요."; return; }
            Talk(tx, "Restore");
            int next = 0; // 앞에서부터 맞게 고른 장수
            while (next < r.Picks.Count && r.Picks[next] == next) next++;
            if (next < r.Picks.Count) { point = Card(tx, r.Picks[next]); hint = "이 카드는 아직 차례가 아닙니다. 다시 클릭해 빼세요."; }
            else if (next < 3) { point = Card(tx, next); hint = OrderHints[next]; }
            else { point = (RectTransform)r.confirmButton.transform; hint = "세 장을 모두 순서대로 골랐습니다. [확인]을 누르세요."; }
        }
        else if (flow.terminal.panel.activeSelf)
        { point = (RectTransform)flow.terminal.input.transform; hint = $"기억 번호 {tx.AppNumber}의 숫자 {{{tx.AppNumber.Substring(2)}}}를 입력하고 [조회]를 누르세요."; }
        else if (view.panel.activeSelf && !tx.LookedUp)
        {
            Talk(tx, "Open");
            point = closeButton;
            hint = $"이 거래는 기억을 조회해야 합니다. 신청서의 기억 번호 {{{tx.AppNumber}}}를 확인하고 [닫기]를 누르세요.";
        }
        else if (view.panel.activeSelf && !tx.RestoreDone)
        {
            Talk(tx, "Record");
            point = (RectTransform)view.restoreButton.transform;
            hint = "조회 기록이 붙었습니다. [기억 복원]을 눌러 기억을 되살리세요.";
        }
        else if (view.panel.activeSelf)
        { Talk(tx, "Restored"); point = closeButton; hint = "복원된 기억이 신청서의 기억 설명과 같습니다. 문제가 없으니 [닫기]를 누르세요."; }
        else if (tx.Viewed && !tx.LookedUp)
        { glow = terminalDesk; hint = "빛나는 [단말기]를 클릭해 기억 번호를 조회하세요."; }
        else if (tx.Viewed && !tx.RestoreDone)
        { glow = pile; hint = "[서류 더미]를 다시 클릭해 기억 복원을 진행하세요."; }
        else ApproveSteps(tx, ref glow, ref point, ref hint);
    }

    // 정답 순서 번호 card인 카드가 지금 놓인 자리
    RectTransform Card(Transaction tx, int card) =>
        (RectTransform)flow.restore.cards[System.Array.IndexOf(tx.CardOrder, card)].transform;

    void ApproveSteps(Transaction tx, ref Clickable glow, ref RectTransform point, ref string hint)
    {
        if (view.panel.activeSelf)
        {
            Talk(tx, "Open");
            point = closeButton;
            hint = "신청서와 계약서의 {이름·번호·설명·서명}이 모두 같습니다. 문제가 없으니 [닫기]를 누르세요.";
        }
        else if (!tx.Viewed)
        { Talk(tx, "Start"); glow = pile; hint = "빛나는 [서류 더미]를 클릭해 서류를 펼치세요."; }
        else if (tx.Stamp != Stamp.Approve)
        { Talk(tx, "Stamp"); glow = approveStamp; hint = "문제가 없는 거래는 승인합니다. 빛나는 [승인 도장]을 클릭하세요."; }
        else
        { Talk(tx, "File"); glow = approveTray; hint = "승인 도장을 찍은 서류는 [승인함]에 넣습니다. 승인함을 클릭하세요."; }
    }

    void RejectSteps(Transaction tx, ref Clickable glow, ref RectTransform point, ref string hint)
    {
        bool recorded = findings.Has(NameKey);
        if (reasons.panel.activeSelf)
        { point = confirmButton; hint = "찾은 사유가 체크되어 있습니다. [확인]을 눌러 반려하세요."; }
        else if (view.panel.activeSelf)
        {
            Talk(tx, "Open");
            if (recorded) { point = closeButton; hint = "발견 목록에 기록했습니다. [닫기]를 누르고 도장을 찍으러 가세요."; }
            else if (!findings.IsSelected("Application.Name")) { point = Row(view.applicationFields, "Application.Name"); fromTop = true; hint = "이번 서류는 {이름}이 다릅니다. 먼저 신청서의 [신청인]을 클릭하세요."; }
            else if (!findings.IsSelected("Contract.Name")) { point = Row(view.contractFields, "Contract.Name"); fromTop = true; hint = "이제 계약서의 [계약자]를 클릭하세요. 신청인과 {한 글자}가 다릅니다."; }
            else { point = recordButton; hint = "다른 두 항목을 골랐습니다. [기록]을 눌러 발견 목록에 넣으세요."; }
        }
        else if (!recorded)
        {
            if (!tx.Viewed) Talk(tx, "Start");
            glow = pile;
            hint = tx.Viewed ? "[서류 더미]를 다시 클릭해 이름이 다른 곳을 기록하세요." : "두 번째 서류입니다. [서류 더미]를 클릭해 펼치세요.";
        }
        else if (tx.Stamp != Stamp.Reject || !tx.Reasons.Exists(r => r.Key == NameKey))
        { Talk(tx, "Stamp"); glow = rejectStamp; hint = "문제가 있는 거래는 반려합니다. 빛나는 [반려 도장]을 클릭하세요."; }
        else
        { glow = rejectTray; hint = "반려 도장을 찍은 서류는 [반려함]에 넣습니다. 반려함을 클릭하세요."; }
    }

    static RectTransform Row(RectTransform fields, string id) => fields.Find(id) as RectTransform;

    // fromLeft: 대상 왼쪽 가운데에서 오른쪽을 가리킴 / 아니면 위쪽 가운데에서 아래를 가리킴.
    // 크게 튀고 살짝 커졌다 작아진다. 화살표 피벗(뾰족한 끝)이 대상에 닿고, 90도 돌리면 끝이 오른쪽을 향한다
    void PointAt(RectTransform target, bool fromLeft)
    {
        var corners = new Vector3[4];
        target.GetWorldCorners(corners); // 0 = 왼쪽 아래, 1 = 왼쪽 위, 2 = 오른쪽 위
        float scale = arrow.lossyScale.y;
        float wave = (Mathf.Sin(Time.unscaledTime * 6f) + 1f) * 0.5f; // 0~1
        float gap = (10f + 22f * wave) * scale;

        arrow.localRotation = Quaternion.Euler(0f, 0f, fromLeft ? 90f : 0f);
        arrow.position = fromLeft
            ? (corners[0] + corners[1]) * 0.5f + Vector3.left * gap
            : (corners[1] + corners[2]) * 0.5f + Vector3.up * gap;
        arrow.localScale = arrowScale * (1f + 0.12f * wave); // 씬에서 정한 Scale 기준으로 맥동
    }

    // 아래를 가리키는 흰 삼각형 (가장자리 부드럽게)
    static Sprite MakeArrowSprite()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float half = (y - 4f) * 0.5f;                 // 아래 끝(y=4)에서 위로 갈수록 넓어짐
                float a = Mathf.Clamp01(half - Mathf.Abs(x + 0.5f - n * 0.5f) + 0.5f);
                if (y < 4 || y > n - 4) a = 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0f), 100f);
    }
}
