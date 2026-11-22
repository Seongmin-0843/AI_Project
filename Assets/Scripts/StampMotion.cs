using System;
using System.Collections;
using UnityEngine;

// 도장 연출: 도장을 클릭하면 서류 더미에서 종이 한 장이 튀어나와 책상에 놓이고 → 도장이 날아가 꾹 찍고 돌아옴 → 자국이 남는다.
// 서류함을 클릭하면 그 종이가 서류함으로 날아가고 서류함에 한 장 쌓인다. 연출 중에는 책상 클릭을 막는다.
public class StampMotion : MonoBehaviour
{
    public Transform pile, approveStamp, rejectStamp;
    [Tooltip("튀어나오는 종이 재질 (업무 지시문 종이와 같게)")]
    public Material paper;
    [Tooltip("도장 찍는 종이 색 (업무 지시문 종이와 구분되게 더 밝은 흰색)")]
    public Color paperColor = new Color(0.97f, 0.96f, 0.92f);
    Material paperMat;
    [Tooltip("종이를 놓을 자리: 서류 더미 위치 기준 (m). 다른 물체와 겹치거나 화면 밖이면 조정")]
    public Vector3 paperOffset = new Vector3(-0.14f, 0f, -0.23f);
    [Tooltip("1보다 크면 빨라짐")]
    public float speed = 1f;
    [Tooltip("도장 찍는 종이를 책상에서 띄울 높이 (m). 마우스를 올려 커진 서류 더미보다는 항상 위에 놓인다")]
    public float paperLift = 0.035f;

    [Tooltip("종이 크기 배율 (1 = 업무 지시문 종이와 같은 A4). 다른 물체와 겹치면 줄이기")]
    public float paperScale = 1f;
    Vector3 PaperSize => new Vector3(0.21f * paperScale, 0.005f, 0.297f * paperScale);
    [Tooltip("도장 자국 크기 배율 (1 = 도장 밑면 크기에 맞춤)")]
    public float markScale = 1f;

    Transform sheet; // 지금 거래의 종이 (도장 찍은 뒤 서류함에 넣을 때까지 책상 위에)
    GameObject mark;
    float pileTop; // 마우스를 올려 커졌을 때 서류 더미의 윗면 높이

    void Start()
    {
        float top = pile.position.y;
        foreach (var r in pile.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y);
        float scale = pile.TryGetComponent<Clickable>(out var c) ? c.hoverScale : 1f;
        pileTop = pile.position.y + (top - pile.position.y) * scale;
    }

    public void Stamp(bool approve, Action done) => StartCoroutine(StampRun(approve, done));
    public void File(TrayStack tray, Action done) => StartCoroutine(FileRun(tray, done));

    IEnumerator StampRun(bool approve, Action done)
    {
        ClickInteractor.Blocked = true;
        var spot = pile.position + paperOffset;
        // 책상 면 높이: 위에서 아래로 쏴서 맞은 곳 (책상에 Collider가 없으면 도장 바닥 높이)
        spot.y = Physics.Raycast(spot + Vector3.up, Vector3.down, out var hit, 2f) ? hit.point.y : approveStamp.position.y;
        spot.y = Mathf.Max(spot.y + paperLift, pileTop + 0.004f);
        var flat = Quaternion.Euler(0f, 90f, 0f); // 가로로 눕혀 화면 안에 들어오게. 항상 같은 방향이라 자국도 늘 같은 모양

        if (sheet == null)
        {
            sheet = MakeSheet();
            float top = pile.position.y;
            foreach (var r in pile.GetComponentsInChildren<Renderer>()) top = Mathf.Max(top, r.bounds.max.y);
            var from = new Vector3(pile.position.x, top, pile.position.z);
            sheet.SetPositionAndRotation(from, pile.rotation);
            yield return Fly(sheet, from, spot, pile.rotation, flat, 0.12f, 0.4f);
        }
        else spot = sheet.position; // 다시 찍기: 놓인 자리 그대로

        var stamp = approve ? approveStamp : rejectStamp;
        Vector3 home = stamp.position, surface = spot + Vector3.up * PaperSize.y;
        Vector3 above = surface + Vector3.up * 0.12f, ready = surface + Vector3.up * 0.035f; // ready = 찍기 직전 높이
        var rot = stamp.rotation;
        yield return Fly(stamp, home, above, rot, rot, 0.06f, 0.3f);
        yield return Fly(stamp, above, ready, rot, rot, 0f, 0.4f, EaseOut);   // 조준하듯 점점 느려지며 내려감
        yield return new WaitForSeconds(0.08f / speed);                   // 숨 고르기
        yield return Fly(stamp, ready, surface, rot, rot, 0f, 0.045f, EaseIn); // 확 내려찍기
        PlaceMark(approve);
        yield return new WaitForSeconds(0.35f / speed);                   // 꾹 누른 채
        yield return Fly(stamp, surface, above, rot, rot, 0f, 0.09f, EaseOut); // 확 들어올림
        yield return Fly(stamp, above, home, rot, rot, 0.06f, 0.3f);

        ClickInteractor.Blocked = false;
        done();
    }

    // 도장 찍힌 종이를 그대로 서류함에 쌓는다 (종이가 없으면 TrayStack이 빈 종이를 쌓음)
    IEnumerator FileRun(TrayStack tray, Action done)
    {
        if (sheet == null) tray.Add();
        else
        {
            ClickInteractor.Blocked = true;
            var land = tray.NextTop();
            var rot = tray.transform.rotation * Quaternion.Euler(0f, UnityEngine.Random.Range(-4f, 4f), 0f);
            StartCoroutine(Shrink(sheet, tray.Fit(PaperSize.x, PaperSize.z), 0.4f / speed)); // 날아가며 서류함 크기에 맞게 줄어듦
            yield return Fly(sheet, sheet.position, land, sheet.rotation, rot, 0.15f, 0.4f);
            tray.Keep(sheet.gameObject);
            sheet = null;
            mark = null;
            ClickInteractor.Blocked = false;
        }
        done(); // 점수 계산·다음 거래는 DeskFlow가
    }

    // 종이 = 빈 부모(피벗 바닥 가운데) + 늘린 Cube. 자국은 부모에 붙여 같이 날아가게
    Transform MakeSheet()
    {
        var root = new GameObject("StampedPaper").transform;
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(cube.GetComponent<Collider>()); // 책상 클릭을 가리지 않게
        if (paperMat == null) paperMat = new Material(paper) { color = paperColor };
        cube.GetComponent<MeshRenderer>().sharedMaterial = paperMat;
        cube.transform.SetParent(root, false);
        cube.transform.localScale = PaperSize;
        cube.transform.localPosition = Vector3.up * PaperSize.y * 0.5f;
        return root;
    }

    // 다시 찍으면 앞 자국은 지우고 새 자국 (판정은 마지막 도장)
    void PlaceMark(bool approve)
    {
        if (mark != null) Destroy(mark);
        // 도장 밑면(정사각형)의 방향·크기 그대로 찍는다
        var stamp = approve ? approveStamp : rejectStamp;
        var face = stamp.GetComponentInChildren<MeshRenderer>();
        float side = face.localBounds.size.x * face.transform.lossyScale.x * markScale;
        mark = new GameObject("StampMark");
        mark.transform.SetParent(sheet, false);
        mark.transform.localPosition = new Vector3(0f, PaperSize.y + 0.0006f, 0f);
        mark.transform.rotation = stamp.rotation * Quaternion.Euler(90f, 0f, 0f);

        // 그림 속 테두리의 자리·크기·기울기(픽셀, 왼쪽 아래 0,0). 기울기를 펴서 테두리가 밑면과 겹치게 한다. 그림을 바꾸면 다시 재야 함
        var tex = DocumentView.MarkTexture(approve);
        var center = approve ? new Vector2(732f, 526f) : new Vector2(736f, 517f);
        var frame = approve ? new Vector2(1095f, 859f) : new Vector2(1140f, 876f);
        float tilt = approve ? 8.5f : 8f;
        mark.transform.localScale = new Vector3(side / frame.x, side / frame.y, 1f);
        var ink = new GameObject("Ink").AddComponent<SpriteRenderer>();
        ink.sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(center.x / tex.width, center.y / tex.height), 1f, 0, SpriteMeshType.FullRect);
        ink.color = new Color(1f, 1f, 1f, 0.92f); // 종이에 밴 듯 살짝 비치게
        ink.transform.SetParent(mark.transform, false);
        ink.transform.localRotation = Quaternion.Euler(0f, 0f, -tilt);
    }

    IEnumerator Shrink(Transform t, float to, float time)
    {
        for (float e = 0f; e < time; e += Time.deltaTime) { t.localScale = Vector3.one * Mathf.Lerp(1f, to, e / time); yield return null; }
        t.localScale = Vector3.one * to;
    }

    // from → to로 이동 (height만큼 포물선), 회전도 함께
    // 완급: 기본은 처음·끝 느림, EaseOut = 처음 빠르고 끝 느림, EaseIn = 처음 느리고 끝 빠름
    static float EaseOut(float x) => 1f - (1f - x) * (1f - x) * (1f - x);
    static float EaseIn(float x) => x * x * x;

    IEnumerator Fly(Transform t, Vector3 from, Vector3 to, Quaternion r0, Quaternion r1, float height, float time, Func<float, float> ease = null)
    {
        time /= speed;
        for (float e = 0f; e < time; e += Time.deltaTime)
        {
            float k = ease != null ? ease(e / time) : Mathf.SmoothStep(0f, 1f, e / time);
            t.SetPositionAndRotation(Vector3.Lerp(from, to, k) + Vector3.up * (height * 4f * k * (1f - k)), Quaternion.Slerp(r0, r1, k));
            yield return null;
        }
        t.SetPositionAndRotation(to, r1);
    }
}
