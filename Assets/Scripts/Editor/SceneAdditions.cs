using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 메뉴 NUMB > Add Missing Objects: 열려 있는 책상 씬에 아직 없는 오브젝트·UI만 추가한다.
// 이미 있는 오브젝트(직접 조정한 배치 포함)는 건드리지 않는다. 단계가 늘면 여기에 항목을 추가한다.
public static partial class DeskSceneBuilder
{
    [MenuItem("NUMB/Add Missing Objects", true)]
    static bool CanAdd() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem("NUMB/Add Missing Objects")]
    static void AddMissingMenu()
    {
        if (!AddMissing()) return;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("추가 완료. 새로 생긴 오브젝트의 위치를 조정한 뒤 씬을 저장(Ctrl+S)하세요.");
    }

    static bool AddMissing()
    {
        font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
        var flow = Object.FindFirstObjectByType<DeskFlow>();
        var canvas = GameObject.Find("Canvas");
        if (flow == null || canvas == null)
        {
            Debug.LogError("DeskFlow 또는 Canvas가 없습니다. Assets/Scenes/Desk.unity를 연 상태에서 실행하세요.");
            return false;
        }

        // M5: 업무 지시문 화면·결과 화면
        if (flow.directive == null) flow.directive = BuildMessagePanel(canvas.transform, "DirectivePanel");
        if (flow.result == null) flow.result = BuildMessagePanel(canvas.transform, "ResultPanel");

        // M5: 책상 위 업무 지시문 종이 (클릭하면 다시 보기). 위치는 씬에서 조정
        if (GameObject.Find("DirectiveSheet") == null)
        {
            var sheet = Click(Prop("DirectiveSheet", "업무 지시문", new Vector3(-0.28f, DeskTop, -0.22f), new Vector3(0.21f, 0.005f, 0.297f),
                Mat("Directive", new Color(0.95f, 0.92f, 0.78f)), null, false));
            UnityEventTools.AddPersistentListener(sheet.onClicked, flow.OpenDirective);
            Debug.Log("[추가] DirectiveSheet (업무 지시문 종이)");
        }

        // M5: 서류함에 처리한 서류 쌓기
        flow.approveStack = AddTrayStack("ApproveTray") ?? flow.approveStack;
        flow.rejectStack = AddTrayStack("RejectTray") ?? flow.rejectStack;

        // 교수님 피드백: 마우스를 올린 물체 이름을 커서 옆에 표시
        var interactor = Object.FindFirstObjectByType<ClickInteractor>();
        if (interactor == null) Debug.LogError("ClickInteractor가 씬에 없습니다.");
        else if (interactor.hoverLabel == null)
        {
            var label = MakeText(canvas.transform, "HoverLabel", "", 28, Color.white, TextAnchor.UpperLeft);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            label.rectTransform.sizeDelta = new Vector2(300f, 40f);
            label.transform.SetAsLastSibling(); // 다른 UI 위에 그려지게
            label.gameObject.SetActive(false);
            interactor.hoverLabel = label;
            EditorUtility.SetDirty(interactor);
            Debug.Log("[추가] HoverLabel (마우스 올린 물체 이름표)");
        }

        // M6: 단말기 번호 입력 창 + 서류 화면의 조회 기록 칸
        if (flow.terminal == null) flow.terminal = BuildTerminalPanel(canvas.transform);
        if (flow.view.lookupFields == null) AddLookupPaper(flow.view);
        var terminalObj = GameObject.Find("Terminal")?.GetComponent<Clickable>();
        if (terminalObj == null) Debug.LogError("Terminal(단말기)이 씬에 없거나 Clickable이 없습니다.");
        else if (terminalObj.onClicked.GetPersistentEventCount() == 0)
        {
            UnityEventTools.AddPersistentListener(terminalObj.onClicked, flow.OpenTerminal);
            EditorUtility.SetDirty(terminalObj);
            Debug.Log("[추가] 단말기 클릭 → 번호 입력 창");
        }

        // M6: 기억 복원 창 + 조회 기록 칸의 [기억 복원] 버튼
        if (flow.restore == null) flow.restore = BuildRestorePanel(canvas.transform);
        if (flow.view.restoreButton == null && flow.view.lookupFields != null)
        {
            var button = MakeButton(flow.view.lookupFields.parent, "RestoreButton", "기억 복원", 30);
            Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(260f, 70f));
            UnityEventTools.AddPersistentListener(button.onClick, flow.OpenRestore);
            button.gameObject.SetActive(false);
            flow.view.restoreButton = button.gameObject;
            EditorUtility.SetDirty(flow.view);
            Debug.Log("[추가] 조회 기록 칸의 [기억 복원] 버튼");
        }

        // 컷씬 (인트로·통과·실패): 모든 UI 위에 그려지도록 맨 마지막
        if (flow.cutscene == null) flow.cutscene = BuildCutscenePlayer(canvas.transform);
        var skipTr = flow.cutscene.panel.transform.Find("SkipButton");
        if (skipTr == null)
        {
            // 오른쪽 위 [건너뛰기]: 모든 컷씬 공통
            var skip = MakeButton(flow.cutscene.panel.transform, "SkipButton", "건너뛰기 ▶▶", SkipFont);
            skip.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            skip.GetComponentInChildren<Text>().color = new Color(1f, 1f, 1f, 0.85f);
            var rt = (RectTransform)skip.transform;
            Place(rt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), SkipSize);
            rt.pivot = new Vector2(1f, 1f);
            UnityEventTools.AddPersistentListener(skip.onClick, flow.cutscene.Skip);
            Debug.Log("[추가] 컷씬 [건너뛰기] 버튼");
        }
        else if (((RectTransform)skipTr).sizeDelta == new Vector2(220f, 60f))
        {
            // 처음 만든 크기(220x60) 그대로일 때만 작게 바꾼다. 씬에서 직접 조정한 크기는 두기
            var rt = (RectTransform)skipTr;
            rt.sizeDelta = SkipSize;
            rt.anchoredPosition = new Vector2(-30f, -30f);
            var label = skipTr.GetComponentInChildren<Text>();
            label.fontSize = SkipFont;
            EditorUtility.SetDirty(rt);
            EditorUtility.SetDirty(label);
            Debug.Log("[변경] 컷씬 [건너뛰기] 버튼 크기 220x60 → 150x40");
        }

        // 튜토리얼 안내: 화살표. 컷씬보다는 아래, 다른 UI보다는 위
        if (Object.FindFirstObjectByType<TutorialGuide>() == null) BuildTutorialGuide(canvas.transform, flow);

        // 튜토리얼: 오틸리아가 나와 주의사항을 말하는 창
        var guide = Object.FindFirstObjectByType<TutorialGuide>();
        if (guide.talk == null)
        {
            guide.talk = BuildOttiliaTalk(guide.transform);
            EditorUtility.SetDirty(guide);
        }
        LayoutTalk(guide.talk);
        TalkHint(guide.talk);
        ApplyArt(flow); // 서류·단말기·결과 화면 그림

        // 도장 연출: 서류 더미에서 종이가 나와 도장이 찍히고 서류함으로 날아감. 종이 자리는 StampMotion의 Paper Offset으로 조정
        if (flow.stampMotion == null)
        {
            var motion = flow.gameObject.AddComponent<StampMotion>();
            motion.pile = Find<Clickable>("DocumentPile")?.transform;
            motion.approveStamp = Find<Clickable>("ApproveStamp")?.transform;
            motion.rejectStamp = Find<Clickable>("RejectStamp")?.transform;
            var directive = GameObject.Find("DirectiveSheet");
            var directiveRenderer = directive != null ? directive.GetComponentInChildren<Renderer>() : null;
            motion.paper = directiveRenderer != null ? directiveRenderer.sharedMaterial : flow.approveStack.paper; // 업무 지시문 종이와 같은 재질
            flow.stampMotion = motion;
            Debug.Log("[추가] StampMotion (도장 찍기 연출, DeskFlow 오브젝트에 붙음)");
        }

        EditorUtility.SetDirty(flow);
        return true;
    }

    static void BuildTutorialGuide(Transform root, DeskFlow flow)
    {
        var go = new GameObject("TutorialGuide", typeof(RectTransform));
        go.transform.SetParent(root, false);
        Place((RectTransform)go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        go.transform.SetSiblingIndex(flow.cutscene.panel.transform.GetSiblingIndex()); // 컷씬 바로 아래

        var arrowGo = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        arrowGo.transform.SetParent(go.transform, false);
        var arrowImage = arrowGo.GetComponent<Image>();
        arrowImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"); // 아래를 가리키는 기본 화살표
        arrowImage.color = new Color(1f, 0.82f, 0.2f);
        arrowImage.raycastTarget = false; // 화살표가 클릭을 가로채지 않게
        var art = (RectTransform)arrowGo.transform;
        art.sizeDelta = new Vector2(70f, 70f);
        art.pivot = new Vector2(0.5f, 0f); // 아래 끝이 대상 위쪽에 닿게
        arrowGo.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

        var guide = go.AddComponent<TutorialGuide>();
        guide.flow = flow;
        guide.view = flow.view;
        guide.findings = flow.findings;
        guide.reasons = flow.reasonDialog;
        guide.pile = Find<Clickable>("DocumentPile");
        guide.approveStamp = Find<Clickable>("ApproveStamp");
        guide.rejectStamp = Find<Clickable>("RejectStamp");
        guide.approveTray = Find<Clickable>("ApproveTray");
        guide.rejectTray = Find<Clickable>("RejectTray");
        guide.closeButton = flow.view.panel.transform.Find("CloseButton") as RectTransform;
        guide.recordButton = flow.findings.transform.Find("RecordButton") as RectTransform;
        guide.confirmButton = flow.reasonDialog.panel.transform.Find("Box/ConfirmButton") as RectTransform;
        guide.arrow = art;
        if (guide.closeButton == null || guide.recordButton == null || guide.confirmButton == null)
            Debug.LogError("튜토리얼: 닫기·기록·확인 버튼 중 찾지 못한 것이 있습니다. TutorialGuide 인스펙터에서 연결하세요.");
        arrowGo.SetActive(false);
        Debug.Log("[추가] TutorialGuide (튜토리얼 화살표·물체 깜빡임, 안내는 아래 안내 줄)");
    }

    // 어두운 전체 화면(클릭 = 다음 줄) + 아래쪽 대화 상자 + 왼쪽 초상화
    static OttiliaTalk BuildOttiliaTalk(Transform guide)
    {
        var dim = new GameObject("OttiliaTalk", typeof(RectTransform), typeof(Image), typeof(Button));
        dim.transform.SetParent(guide, false);
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f); // 말하는 동안 뒤쪽 클릭을 막음
        Place((RectTransform)dim.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var box = Panel(dim.transform, "Box", new Color(0.13f, 0.14f, 0.17f));
        box.pivot = new Vector2(0.5f, 0f);
        Place(box, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1500f, 280f));

        var frame = new GameObject("Frame", typeof(RectTransform));
        frame.transform.SetParent(box, false);
        var portrait = new GameObject("Portrait", typeof(RectTransform)).AddComponent<RawImage>();
        portrait.transform.SetParent(frame.transform, false);
        portrait.raycastTarget = false;

        var speaker = MakeText(box, "Speaker", "오틸리아", 32, new Color(1f, 0.8f, 0.45f), TextAnchor.UpperLeft);
        speaker.fontStyle = FontStyle.Bold;
        speaker.rectTransform.pivot = Vector2.one;
        Place(speaker.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(-40f, -26f), new Vector2(-450f, 45f));
        var line = MakeText(box, "Line", "", 34, new Color(0.93f, 0.91f, 0.86f), TextAnchor.UpperLeft);
        line.lineSpacing = 1.2f;
        line.rectTransform.pivot = Vector2.one;
        Place(line.rectTransform, Vector2.zero, Vector2.one, new Vector2(-40f, -82f), new Vector2(-450f, -122f));
        var hint = MakeText(box, "Hint", "클릭 ▶", 22, new Color(1f, 1f, 1f, 0.5f), TextAnchor.LowerRight);
        hint.rectTransform.pivot = new Vector2(1f, 0f);
        Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 14f), new Vector2(200f, 30f));

        var talk = dim.AddComponent<OttiliaTalk>();
        talk.speaker = speaker;
        talk.line = line;
        talk.portrait = portrait;
        var button = dim.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        UnityEventTools.AddPersistentListener(button.onClick, talk.Next);
        // 켜 둔 채로 둔다: TutorialGuide.Awake가 시작할 때 숨김
        Debug.Log("[추가] OttiliaTalk (튜토리얼 오틸리아 안내 창, TutorialGuide 아래)");
        return talk;
    }

    // 말풍선 오른쪽 아래 ▼ 왼쪽에 컷씬과 같은 넘기기 안내 글
    static void TalkHint(OttiliaTalk talk)
    {
        var arrow = talk.transform.Find("Box/Bubble/Hint") as RectTransform;
        if (arrow == null || arrow.Find("HintText") != null) return;
        var text = MakeText(arrow, "HintText", "클릭 또는 스페이스", 22, new Color(0.33f, 0.19f, 0.35f), TextAnchor.MiddleRight);
        text.raycastTarget = false;
        text.rectTransform.pivot = new Vector2(1f, 0.5f);
        Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-4f, 0f), new Vector2(260f, 34f));
        Debug.Log("[추가] 오틸리아 대화 UI: 클릭 또는 스페이스 안내");
    }

    // 예시 그림(Ottilia_example.png)대로: 왼쪽에 크게 선 오틸리아, 그 앞 화면 아래를 가로지르는 말풍선(Ottilia_text).
    // 말풍선이 이미 늘어나는 그림(Image)이면 배치를 마친 것이므로 건드리지 않음 (손으로 조정한 값 유지)
    static void LayoutTalk(OttiliaTalk talk)
    {
        const string path = "Assets/Resources/Portraits/Ottilia_text.png";
        var box = talk.transform.Find("Box") as RectTransform;
        if (box == null || !(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
        var bubbleRt = box.Find("Bubble") as RectTransform;
        if (bubbleRt != null && bubbleRt.GetComponent<Image>() != null) return;

        // 이름판·꼬리·테두리는 그대로 두고 빈 종이 부분만 늘어나게 자르는 선 (왼쪽, 아래, 오른쪽, 위)
        var border = new Vector4(540f, 130f, 64f, 360f);
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteBorder != border)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        if (box.TryGetComponent<Image>(out var dark)) Object.DestroyImmediate(dark);
        talk.GetComponent<Image>().color = Color.clear; // 화면을 어둡게 하지 않음 (클릭은 계속 막음)
        Place(box, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 1080f)); // 화면 왼쪽 아래 기준 1920x1080

        // 오틸리아: 전신 그림 크기 그대로, 허리 아래는 말풍선에 가려짐 (그림 비율 1061:1482)
        var portrait = talk.portrait;
        var frame = (RectTransform)portrait.transform.parent;
        if (frame.TryGetComponent<Image>(out var frameImage)) Object.DestroyImmediate(frameImage);
        frame.pivot = Vector2.zero;
        Place(frame, Vector2.zero, Vector2.zero, new Vector2(-25f, -48f), new Vector2(798f, 1115f));
        Place(portrait.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        portrait.texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Portraits/Ottilia_01.png"); // 실행하면 OttiliaTalk가 바꿈
        portrait.uvRect = new Rect(0f, 0f, 1f, 1f);

        // 말풍선: 그림 둘레에 투명한 여백이 있어 보이는 크기(1818x345)보다 크게 잡는다
        var name = talk.speaker;
        var line = talk.line;
        var hint = System.Array.Find(box.GetComponentsInChildren<Text>(true), t => t.name == "Hint");
        var bubble = new GameObject("Bubble", typeof(RectTransform)).AddComponent<Image>();
        bubble.transform.SetParent(box, false);
        bubble.transform.SetAsLastSibling(); // 초상화보다 앞
        bubble.raycastTarget = false;
        bubble.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        bubble.type = Image.Type.Sliced;
        bubble.pixelsPerUnitMultiplier = 1f / 0.85f; // 테두리·이름판 크기 = 원본의 0.85배
        bubble.rectTransform.pivot = Vector2.zero;
        Place(bubble.rectTransform, Vector2.zero, Vector2.zero, new Vector2(35f, -48f), new Vector2(1846f, 498f));

        var ink = new Color(0.15f, 0.13f, 0.14f);
        name.transform.SetParent(bubble.transform, false);
        name.alignment = TextAnchor.MiddleCenter;
        name.fontSize = 34;
        name.color = ink;
        name.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(259f, -115f), new Vector2(280f, 50f));

        line.transform.SetParent(bubble.transform, false);
        line.alignment = TextAnchor.MiddleLeft;
        line.fontSize = 44;
        line.color = ink;
        line.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        Place(line.rectTransform, Vector2.zero, Vector2.one, new Vector2(15f, -2f), new Vector2(-300f, -280f));

        if (hint != null)
        {
            hint.transform.SetParent(bubble.transform, false);
            hint.text = "▼";
            hint.fontSize = 26;
            hint.alignment = TextAnchor.MiddleCenter;
            hint.color = new Color(0.33f, 0.19f, 0.35f);
            hint.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-91f, 134f), new Vector2(40f, 34f));
            var bar = Panel(hint.transform, "Bar", hint.color);
            Place(bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(30f, 3f));
        }
        if (bubbleRt != null) Object.DestroyImmediate(bubbleRt.gameObject); // 예전 말풍선 (글자는 위에서 옮김)
        EditorUtility.SetDirty(talk.gameObject);
        Debug.Log("[변경] 오틸리아 대화 UI: 예시 그림대로 배치");
    }

    static RectTransform Panel(Transform parent, string name, Color color)
    {
        var image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(parent, false);
        image.color = color;
        image.raycastTarget = false;
        return image.rectTransform;
    }

    static T Find<T>(string name) where T : Component
    {
        var go = GameObject.Find(name);
        var c = go != null ? go.GetComponent<T>() : null;
        if (c == null) Debug.LogError($"{name}({typeof(T).Name})를 씬에서 찾지 못했습니다.");
        return c;
    }

    static readonly Vector2 SkipSize = new Vector2(150f, 40f);
    const int SkipFont = 20;

    // 전체 화면 검은 배경 + 16:9 이미지(비율 유지) + 화면 아래 자막 띠(이름 + 대사)
    static CutscenePlayer BuildCutscenePlayer(Transform root)
    {
        var bg = new GameObject("CutscenePanel", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(root, false);
        bg.GetComponent<Image>().color = Color.black; // 뒤쪽 UI·3D 클릭도 막음
        Place((RectTransform)bg.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var imgGo = new GameObject("Image", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
        imgGo.transform.SetParent(bg.transform, false);
        Place((RectTransform)imgGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var raw = imgGo.GetComponent<RawImage>();
        raw.raycastTarget = false;
        var fitter = imgGo.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 16f / 9f;

        var bar = new GameObject("SubtitleBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(bg.transform, false);
        var barImage = bar.GetComponent<Image>();
        barImage.color = new Color(0f, 0f, 0f, 0.75f);
        barImage.raycastTarget = false;
        var brt = (RectTransform)bar.transform;
        Place(brt, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 230f));
        brt.pivot = new Vector2(0.5f, 0f);

        var speaker = MakeText(brt, "Speaker", "", 34, new Color(1f, 0.8f, 0.45f), TextAnchor.UpperLeft);
        speaker.fontStyle = FontStyle.Bold;
        Place(speaker.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -25f), new Vector2(-360f, 45f));
        speaker.rectTransform.pivot = new Vector2(0.5f, 1f);

        var line = MakeText(brt, "Line", "", 36, Color.white, TextAnchor.UpperLeft);
        line.lineSpacing = 1.2f;
        Place(line.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, -40f), new Vector2(-360f, -100f));

        var hint = MakeText(brt, "Hint", "클릭 또는 스페이스 ▶", 22, new Color(1f, 1f, 1f, 0.5f), TextAnchor.LowerRight);
        Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 20f), new Vector2(400f, 40f));
        hint.rectTransform.pivot = new Vector2(1f, 0f);

        var player = bg.AddComponent<CutscenePlayer>();
        player.panel = bg;
        player.image = raw;
        player.fitter = fitter;
        player.speaker = speaker;
        player.line = line;
        bg.transform.SetAsLastSibling();
        // 켜 둔 채로 둔다: CutscenePlayer.Awake가 시작할 때 숨김
        Debug.Log("[추가] CutscenePanel (컷씬 이미지 + 자막)");
        return player;
    }

    // 복원 창: 글 카드 3장 가로 배치 + 안내 문구 + 복원 결과 + [확인]/[닫기]
    static RestorePanel BuildRestorePanel(Transform root)
    {
        var dim = new GameObject("RestorePanel", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(root, false);
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        Place((RectTransform)dim.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(dim.transform, false);
        box.GetComponent<Image>().color = new Color(0.25f, 0.27f, 0.3f);
        var brt = (RectTransform)box.transform;
        Place(brt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1500f, 820f));

        var title = MakeText(brt, "Title", "기억 복원", 40, Color.white, TextAnchor.UpperCenter);
        title.fontStyle = FontStyle.Bold;
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(0f, 50f));
        title.rectTransform.pivot = new Vector2(0.5f, 1f);

        var message = MakeText(brt, "Message", "", 28, new Color(1f, 0.88f, 0.5f), TextAnchor.MiddleCenter);
        Place(message.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -120f), new Vector2(-60f, 50f));

        var panel = dim.AddComponent<RestorePanel>();
        for (int i = 0; i < 3; i++)
        {
            var card = MakeButton(brt, $"Card{i + 1}", "", 28);
            card.transition = Selectable.Transition.None; // 고른 상태는 배경색으로만 표시
            var text = card.GetComponentInChildren<Text>();
            text.supportRichText = true;
            text.alignment = TextAnchor.UpperCenter;
            Place(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, -10f), new Vector2(-50f, -60f));
            Place((RectTransform)card.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 470f, 70f), new Vector2(430f, 340f));
            panel.cards[i] = card;
        }

        var result = MakeText(brt, "Result", "", 30, Color.white, TextAnchor.MiddleCenter);
        result.supportRichText = true;
        Place(result.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 200f), new Vector2(-100f, 100f));

        var ok = MakeButton(brt, "ConfirmButton", "확인", 32);
        Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-160f, 50f), new Vector2(260f, 70f));
        var close = MakeButton(brt, "CloseButton", "닫기", 32);
        Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(160f, 50f), new Vector2(260f, 70f));

        panel.panel = dim;
        panel.confirmButton = ok;
        panel.message = message;
        panel.result = result;
        panel.closeLabel = close.GetComponentInChildren<Text>();
        UnityEventTools.AddPersistentListener(ok.onClick, panel.Confirm);
        UnityEventTools.AddPersistentListener(close.onClick, panel.Close);
        // 켜 둔 채로 둔다: RestorePanel.Awake가 시작할 때 숨김
        Debug.Log("[추가] RestorePanel (기억 복원 창)");
        return panel;
    }

    // 단말기 창: "M-" + 숫자 4자리 입력, [조회]/[닫기], 안내 문구
    static TerminalPanel BuildTerminalPanel(Transform root)
    {
        var dim = new GameObject("TerminalPanel", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(root, false);
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        Place((RectTransform)dim.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(dim.transform, false);
        box.GetComponent<Image>().color = new Color(0.2f, 0.22f, 0.25f);
        var brt = (RectTransform)box.transform;
        Place(brt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 460f));

        var title = MakeText(brt, "Title", "기억 등록 조회", 38, Color.white, TextAnchor.UpperCenter);
        title.fontStyle = FontStyle.Bold;
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(0f, 50f));
        title.rectTransform.pivot = new Vector2(0.5f, 1f);

        var prefix = MakeText(brt, "Prefix", "M-", 44, Color.white, TextAnchor.MiddleRight);
        Place(prefix.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-170f, 40f), new Vector2(80f, 80f));

        var fieldGo = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
        fieldGo.transform.SetParent(brt, false);
        fieldGo.GetComponent<Image>().color = new Color(0.94f, 0.92f, 0.86f);
        Place((RectTransform)fieldGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(20f, 40f), new Vector2(300f, 80f));
        var inputText = MakeText(fieldGo.transform, "Text", "", 44, Color.black, TextAnchor.MiddleLeft);
        inputText.supportRichText = false;
        Place(inputText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-30f, 0f));
        var placeholder = MakeText(fieldGo.transform, "Placeholder", "0000", 44, new Color(0f, 0f, 0f, 0.3f), TextAnchor.MiddleLeft);
        Place(placeholder.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-30f, 0f));
        var input = fieldGo.GetComponent<InputField>();
        input.textComponent = inputText;
        input.placeholder = placeholder;
        input.contentType = InputField.ContentType.IntegerNumber;
        input.characterLimit = 4;

        var message = MakeText(brt, "Message", "", 28, new Color(1f, 0.88f, 0.5f), TextAnchor.MiddleCenter);
        Place(message.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -55f), new Vector2(-40f, 70f));

        var ok = MakeButton(brt, "LookupButton", "조회", 32);
        Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-130f, 40f), new Vector2(220f, 70f));
        var close = MakeButton(brt, "CloseButton", "닫기", 32);
        Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(130f, 40f), new Vector2(220f, 70f));

        var panel = dim.AddComponent<TerminalPanel>();
        panel.panel = dim;
        panel.input = input;
        panel.message = message;
        UnityEventTools.AddPersistentListener(ok.onClick, panel.Submit);
        UnityEventTools.AddPersistentListener(close.onClick, panel.Close);
        // 켜 둔 채로 둔다: TerminalPanel.Awake가 시작할 때 숨김 (꺼 두면 Awake가 늦게 돌아 첫 Show를 덮어씀)
        Debug.Log("[추가] TerminalPanel (단말기 번호 입력 창)");
        return panel;
    }

    // 서류 화면에 세 번째 종이(조회 기록)를 넣고, 네 칸이 1920 폭에 들어가게 다시 배치한다.
    // 처음 생성한 배치(신청서·계약서 600, 발견 목록 480)를 기준으로 한 번만 실행된다.
    static void AddLookupPaper(DocumentView view)
    {
        var lookup = Paper(view.panel.transform, "Lookup", "조회 기록", 310f);
        ((RectTransform)lookup.parent).sizeDelta = new Vector2(440f, 860f);
        Resize(view.applicationFields.parent, -700f, 500f);
        Resize(view.contractFields.parent, -180f, 500f);
        Resize(view.findings.transform, 750f, 400f);

        var hint = MakeText(lookup.parent, "Hint", "단말기에서 기억 번호를 조회하면\n등록 기록이 여기에 표시됩니다.", 26, new Color(0.4f, 0.4f, 0.4f), TextAnchor.MiddleCenter);
        Place(hint.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-60f, 0f));

        lookup.parent.gameObject.SetActive(false);
        view.lookupFields = lookup;
        view.lookupHint = hint;
        EditorUtility.SetDirty(view);
        Debug.Log("[추가] 서류 화면 조회 기록 칸 (신청서·계약서·발견 목록 폭을 줄여 다시 배치)");
    }

    static void Resize(Transform t, float x, float width)
    {
        var rt = (RectTransform)t;
        rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
        rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
    }

    static TrayStack AddTrayStack(string trayName)
    {
        var tray = GameObject.Find(trayName);
        if (tray == null) { Debug.LogError($"{trayName}가 씬에 없습니다."); return null; }
        var stack = tray.GetComponent<TrayStack>();
        if (stack == null)
        {
            stack = tray.AddComponent<TrayStack>();
            Debug.Log($"[추가] {trayName}에 TrayStack");
        }
        if (stack.paper == null) stack.paper = Mat("Paper", default);
        return stack;
    }

    // 종이 한 장 + 버튼 하나 (업무 지시문, 결과 화면)
    static MessagePanel BuildMessagePanel(Transform root, string name)
    {
        var dim = new GameObject(name, typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(root, false);
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        Place((RectTransform)dim.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var paper = new GameObject("Paper", typeof(RectTransform), typeof(Image));
        paper.transform.SetParent(dim.transform, false);
        paper.GetComponent<Image>().color = new Color(0.94f, 0.92f, 0.86f);
        var prt = (RectTransform)paper.transform;
        Place(prt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 940f));

        var body = MakeText(prt, "Body", "", 28, new Color(0.15f, 0.15f, 0.15f), TextAnchor.UpperLeft);
        body.supportRichText = true;
        body.lineSpacing = 1.15f;
        Place(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(0f, 40f), new Vector2(-120f, -200f));

        var button = MakeButton(prt, "Button", "", 32);
        Place((RectTransform)button.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(300f, 70f));

        var panel = dim.AddComponent<MessagePanel>();
        panel.panel = dim;
        panel.body = body;
        panel.buttonLabel = button.GetComponentInChildren<Text>();
        UnityEventTools.AddPersistentListener(button.onClick, panel.Press);
        Debug.Log($"[추가] {name}");
        return panel;
    }
}
