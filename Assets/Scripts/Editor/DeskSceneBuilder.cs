using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// 메뉴 NUMB > Build Desk Scene: 책상 씬(Assets/Scenes/Desk.unity)을 처음부터 생성한다 (초기 생성용).
// 생성 후에는 씬이 기준이다. 배치·크기·재질은 씬에서 직접 조정하고, 이후 기능은 기존 씬에 추가하는 방식으로 넣는다.
// 크기는 실제 크기(1 unit = 1m), 각 물체의 피벗은 바닥 가운데. 3D 팀원 모델도 같은 기준으로 맞춘다.
public static partial class DeskSceneBuilder
{
    const string ScenePath = "Assets/Scenes/Desk.unity";
    const string MatDir = "Assets/Materials/Placeholder";
    const string FontPath = "Assets/Fonts/NanumGothic-Regular.ttf";
    const float DeskTop = 0.75f;

    static Font font;

    // 플레이 중에는 씬을 만들 수 없으므로 메뉴를 비활성화
    [MenuItem("NUMB/Build Desk Scene", true)]
    static bool CanBuild() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem("NUMB/Build Desk Scene")]
    static void Build()
    {
        // 씬을 직접 조정한 뒤에는 씬이 기준이므로, 덮어쓰기 전에 반드시 확인
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
            !EditorUtility.DisplayDialog("책상 씬 다시 만들기",
                $"{ScenePath}가 이미 있습니다.\n다시 만들면 씬에서 직접 조정한 배치와 설정이 모두 사라집니다.\n계속할까요?",
                "다시 만들기", "취소"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);

        // 조명
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.93f, 0.82f);
        light.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

        // 사무실 (카메라에 보이는 쪽만)
        Prop("Floor", null, Vector3.zero, new Vector3(5f, 0.01f, 5f), Mat("Floor", new Color(0.35f, 0.38f, 0.42f)), null, false);
        Prop("Wall", null, new Vector3(0f, 0f, 1.2f), new Vector3(5f, 3f, 0.05f), Mat("Wall", new Color(0.45f, 0.5f, 0.56f)), null, false);
        // 책상: 높이를 DeskTop에 맞추고, 실제 가로·세로로 책상 위 배치를 계산
        Prop("Desk", null, Vector3.zero, new Vector3(1.4f, DeskTop, 0.75f), Mat("Desk", new Color(0.4f, 0.33f, 0.27f)), "Desk/desk.fbx", true);
        halfW = lastSize.x / 2f;
        halfD = lastSize.z / 2f;

        // 책상 위 (Top의 x·z는 책상 가운데 기준 비율: -1 = 왼쪽·앞쪽 끝, 1 = 오른쪽·뒤쪽 끝)
        var pile = Click(Prop("DocumentPile", "서류 더미", Top(0f, -0.3f), new Vector3(0.21f, 0.04f, 0.30f), Mat("Paper", new Color(0.92f, 0.9f, 0.84f)), null, false));
        var approveStamp = Click(Prop("ApproveStamp", "승인 도장", Top(0.3f, -0.5f), new Vector3(0.05f, 0.08f, 0.05f), Mat("Approve", new Color(0.3f, 0.6f, 0.35f)), "ApproveStamp/Approve_stamp.fbx", false));
        var rejectStamp = Click(Prop("RejectStamp", "반려 도장", Top(0.42f, -0.5f), new Vector3(0.05f, 0.08f, 0.05f), Mat("Reject", new Color(0.7f, 0.25f, 0.22f)), "RejectStamp/Reject_Stamp.fbx", false));
        var approveTray = Click(Prop("ApproveTray", "승인함", Top(-0.74f, -0.15f), new Vector3(0.25f, 0.08f, 0.33f), Mat("Approve", default), "ApproveTray/approve.fbx", false));
        var rejectTray = Click(Prop("RejectTray", "반려함", Top(0.74f, -0.15f), new Vector3(0.25f, 0.08f, 0.33f), Mat("Reject", default), "RejectTray/reject.fbx", false));
        Prop("Terminal", "단말기", Top(0f, 0.55f), new Vector3(0.30f, 0.22f, 0.20f), Mat("Terminal", new Color(0.2f, 0.22f, 0.25f)), "Terminal/Computer.fbx", false);
        // ponytail: TerminalDesk(Computer_Drive) 용도 미확인 → 단말기 오른쪽에 장식으로 둠. 위치가 정해지면 여기만 수정
        Prop("TerminalDesk", null, Top(0.4f, 0.55f), new Vector3(0.25f, 0.15f, 0.20f), Mat("Terminal", default), "TerminalDesk/Computer_Drive.fbx", false);

        // 1인칭 고정 카메라 (앉은 눈높이에서 책상을 내려다봄)
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.05f;
        camGo.transform.position = new Vector3(0f, 1.2f, -0.65f);
        camGo.transform.LookAt(new Vector3(0f, DeskTop, 0f));
        camGo.AddComponent<ClickInteractor>();

        // UI
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var canvasGo = new GameObject("Canvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();
        var root = canvasGo.transform;

        var feedback = MakeText(root, "FeedbackLabel", "책상 위 물체를 클릭하세요", 36, Color.white, TextAnchor.LowerCenter);
        Place(feedback.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 40f), new Vector2(0f, 60f));

        var view = BuildDocumentView(root);

        // 업무 흐름: 서류 더미 → 서류 화면, 3D 도장 → 도장 찍기, 3D 서류함 → 분류·정산
        var flow = new GameObject("DeskFlow").AddComponent<DeskFlow>();
        flow.view = view;
        flow.findings = view.findings;
        flow.reasonDialog = BuildReasonDialog(root);
        flow.feedback = feedback;
        UnityEventTools.AddPersistentListener(pile.onClicked, flow.OpenDocuments);
        UnityEventTools.AddPersistentListener(approveStamp.onClicked, flow.StampApprove);
        UnityEventTools.AddPersistentListener(rejectStamp.onClicked, flow.StampReject);
        UnityEventTools.AddPersistentListener(approveTray.onClicked, flow.FileApprove);
        UnityEventTools.AddPersistentListener(rejectTray.onClicked, flow.FileReject);

        AddMissing(); // 이후 단계에서 추가된 오브젝트·UI

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log($"책상 씬 생성 완료: {ScenePath}");
    }

    // 서류 2D 화면: 어두운 배경 + 신청서·계약서 두 장 + 닫기 버튼
    static DocumentView BuildDocumentView(Transform root)
    {
        var panelGo = new GameObject("DocumentPanel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(root, false);
        panelGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f); // 뒤쪽 3D 클릭을 막음
        Place((RectTransform)panelGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var view = panelGo.AddComponent<DocumentView>();
        view.panel = panelGo;
        view.applicationFields = Paper(panelGo.transform, "Application", "기억 거래 신청서", -560f);
        view.contractFields = Paper(panelGo.transform, "Contract", "기억 거래 계약서", 60f);
        view.findings = BuildFindingPanel(panelGo.transform);

        // 항목 줄: 배경 Image(선택 시 노란색) + 글자
        var rowGo = new GameObject("RowTemplate", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        rowGo.transform.SetParent(panelGo.transform, false);
        var rowLayout = rowGo.GetComponent<VerticalLayoutGroup>();
        rowLayout.padding = new RectOffset(8, 8, 6, 6);
        rowLayout.childControlWidth = rowLayout.childControlHeight = true;
        var rowText = MakeText(rowGo.transform, "Text", "", 30, new Color(0.15f, 0.15f, 0.15f), TextAnchor.UpperLeft);
        rowText.supportRichText = true;
        var fieldRow = rowGo.AddComponent<FieldRow>();
        fieldRow.SetSelected(false);
        rowGo.SetActive(false);
        view.rowTemplate = fieldRow;

        var debug = MakeText(panelGo.transform, "DebugLabel", "", 22, new Color(1f, 1f, 1f, 0.6f), TextAnchor.UpperLeft);
        Place(debug.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(20f, -10f), new Vector2(-40f, 40f));
        debug.rectTransform.pivot = new Vector2(0f, 1f);
        view.debugLabel = debug;

        var close = MakeButton(panelGo.transform, "CloseButton", "닫기", 30);
        Place((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-250f, 40f), new Vector2(200f, 60f));
        UnityEventTools.AddPersistentListener(close.onClick, view.Close);

        return view;
    }

    // 반려 사유 선택 창: 발견 목록이 모두 체크된 상태로 뜸. [확인]/[취소]
    static ReasonDialog BuildReasonDialog(Transform root)
    {
        var dim = new GameObject("ReasonDialog", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(root, false);
        dim.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
        Place((RectTransform)dim.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        var box = new GameObject("Box", typeof(RectTransform), typeof(Image));
        box.transform.SetParent(dim.transform, false);
        box.GetComponent<Image>().color = new Color(0.94f, 0.92f, 0.86f);
        var brt = (RectTransform)box.transform;
        Place(brt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 700f));

        var title = MakeText(brt, "Title", "반려 사유 선택", 38, Color.black, TextAnchor.UpperCenter);
        title.fontStyle = FontStyle.Bold;
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -25f), new Vector2(0f, 50f));
        title.rectTransform.pivot = new Vector2(0.5f, 1f);

        var hint = MakeText(brt, "Hint", "체크를 해제한 사유는 빠집니다. 틀린 사유가 섞이면 0 기억입니다.", 22, new Color(0.3f, 0.3f, 0.3f), TextAnchor.UpperCenter);
        Place(hint.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -85f), new Vector2(-40f, 40f));
        hint.rectTransform.pivot = new Vector2(0.5f, 1f);

        var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
        list.transform.SetParent(brt, false);
        var lrt = (RectTransform)list.transform;
        Place(lrt, Vector2.zero, Vector2.one, new Vector2(0f, 0f), new Vector2(-60f, -280f));
        var layout = list.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var empty = MakeText(lrt, "Empty", "발견 목록이 비어 있습니다. 이대로 반려하면 '사유 없음'으로 0 기억입니다.", 26, new Color(0.6f, 0.15f, 0.15f), TextAnchor.UpperCenter);

        var item = MakeButton(brt, "ItemTemplate", "", 26);
        item.gameObject.AddComponent<LayoutElement>().minHeight = 56f;
        item.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
        item.transition = Selectable.Transition.None; // 체크 상태는 배경색으로만 표시
        item.gameObject.SetActive(false);

        var ok = MakeButton(brt, "ConfirmButton", "확인", 32);
        Place((RectTransform)ok.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-130f, 40f), new Vector2(220f, 70f));
        var cancel = MakeButton(brt, "CancelButton", "취소", 32);
        Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(130f, 40f), new Vector2(220f, 70f));

        var dialog = dim.AddComponent<ReasonDialog>();
        dialog.panel = dim;
        dialog.listContainer = lrt;
        dialog.itemTemplate = item;
        dialog.emptyLabel = empty;
        UnityEventTools.AddPersistentListener(ok.onClick, dialog.Confirm);
        UnityEventTools.AddPersistentListener(cancel.onClick, dialog.Cancel);
        return dialog;
    }

    // 오른쪽 발견 목록: 제목 + 목록 + [기록] 버튼. 목록 줄 템플릿은 목록 밖에 비활성으로 둔다.
    static FindingPanel BuildFindingPanel(Transform parent)
    {
        var go = new GameObject("FindingPanel", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.25f, 0.27f, 0.3f);
        var rt = (RectTransform)go.transform;
        Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(650f, 30f), new Vector2(480f, 860f));

        var title = MakeText(rt, "Title", "발견 목록", 36, Color.white, TextAnchor.UpperCenter);
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -25f), new Vector2(0f, 50f));
        title.rectTransform.pivot = new Vector2(0.5f, 1f);

        var list = new GameObject("List", typeof(RectTransform), typeof(VerticalLayoutGroup));
        list.transform.SetParent(rt, false);
        var lrt = (RectTransform)list.transform;
        Place(lrt, Vector2.zero, Vector2.one, new Vector2(0f, 10f), new Vector2(-40f, -220f));
        var layout = list.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var item = MakeButton(rt, "ItemTemplate", "", 24);
        item.gameObject.AddComponent<LayoutElement>().minHeight = 56f;
        item.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
        item.gameObject.SetActive(false);

        var record = MakeButton(rt, "RecordButton", "기록", 32);
        Place((RectTransform)record.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(240f, 70f));

        var panel = go.AddComponent<FindingPanel>();
        panel.listContainer = lrt;
        panel.itemTemplate = item;
        UnityEventTools.AddPersistentListener(record.onClick, panel.Record);
        return panel;
    }

    static Button MakeButton(Transform parent, string name, string label, int size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = new Color(0.85f, 0.85f, 0.8f);
        var text = MakeText(go.transform, "Text", label, size, Color.black, TextAnchor.MiddleCenter);
        Place(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-24f, 0f));
        return go.GetComponent<Button>();
    }

    // 종이 한 장 (가로 600 x 세로 860). 반환값은 항목들이 쌓일 영역.
    static RectTransform Paper(Transform parent, string name, string title, float x)
    {
        var paper = new GameObject(name, typeof(RectTransform), typeof(Image));
        paper.transform.SetParent(parent, false);
        paper.GetComponent<Image>().color = new Color(0.94f, 0.92f, 0.86f);
        var rt = (RectTransform)paper.transform;
        Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 30f), new Vector2(600f, 860f));

        var titleText = MakeText(rt, "Title", title, 40, Color.black, TextAnchor.UpperCenter);
        titleText.fontStyle = FontStyle.Bold;
        Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -30f), new Vector2(0f, 60f));
        titleText.rectTransform.pivot = new Vector2(0.5f, 1f);

        var fields = new GameObject("Fields", typeof(RectTransform), typeof(VerticalLayoutGroup));
        fields.transform.SetParent(rt, false);
        var frt = (RectTransform)fields.transform;
        Place(frt, Vector2.zero, Vector2.one, new Vector2(0f, -50f), new Vector2(-100f, -200f));
        var layout = fields.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 28f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return frt;
    }

    static Text MakeText(Transform parent, string name, string content, int size, Color color, TextAnchor anchor)
    {
        var text = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
        text.transform.SetParent(parent, false);
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = anchor;
        text.text = content;
        text.raycastTarget = false;
        return text;
    }

    static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static float halfW = 0.7f, halfD = 0.375f; // 책상 가로·세로의 절반 (책상 모델 크기로 갱신)
    const string ModelDir = "Assets/Modeling/";

    static Vector3 Top(float fx, float fz) => new Vector3(fx * halfW, DeskTop, fz * halfD);

    static Clickable Click(GameObject root) => root.GetComponent<Clickable>();

    // 부모(피벗=바닥 가운데, Collider, Clickable) + 자식 Visual.
    // Visual은 model이 있으면 아트 모델, 없으면 임시 Cube. displayName이 null이면 클릭 불가.
    // 모델은 size 상자 안에 들어가도록 균일 배율을 맞춤 (fitHeight면 높이만 size.y에 맞춤). 마지막 실제 크기는 lastSize.
    static Vector3 lastSize;

    static GameObject Prop(string name, string displayName, Vector3 pos, Vector3 size, Material mat, string model, bool fitHeight)
    {
        var root = new GameObject(name); // 원점에서 만들고 마지막에 이동 (bounds 계산을 단순하게)
        var prefab = model == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(ModelDir + model);
        if (model != null && prefab == null) Debug.LogWarning($"[모델 없음] {ModelDir + model} → 임시 Cube 사용");

        if (prefab != null)
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);
            foreach (var c in visual.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            var original = MeshBounds(visual);
            float s = fitHeight
                ? size.y / original.size.y
                : Mathf.Min(size.x / original.size.x, size.y / original.size.y, size.z / original.size.z);
            visual.transform.localScale *= s;
            var b = MeshBounds(visual);
            visual.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);
            lastSize = b.size;
            Debug.Log($"[모델] {name}: 원본 크기 {original.size.ToString("0.###")} → 배율 {s:0.###} → {lastSize.ToString("0.###")} m");
        }
        else
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(0f, size.y / 2f, 0f);
            visual.transform.localScale = size;
            visual.GetComponent<MeshRenderer>().sharedMaterial = mat;
            lastSize = size;
        }

        if (displayName != null)
        {
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, lastSize.y / 2f, 0f);
            col.size = lastSize;
            root.AddComponent<Clickable>().displayName = displayName;
        }
        root.transform.position = pos;
        return root;
    }

    static Bounds MeshBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        var b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    // 같은 이름이면 기존 재질을 재사용한다 (color가 default면 색을 바꾸지 않음).
    static Material Mat(string name, Color color)
    {
        var path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Materials")) AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(MatDir)) AssetDatabase.CreateFolder("Assets/Materials", "Placeholder");
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        if (color != default)
        {
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }
}
