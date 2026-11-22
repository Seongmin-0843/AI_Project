using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 튜토리얼에서 오틸리아가 나와 주의사항을 말하는 창. 화면 아무 곳이나 클릭하거나 스페이스바를 누르면 다음 줄, 마지막 줄 뒤에 닫힌다.
// 대사는 컷씬_대사.txt의 [Tutorial.…] 칸. 언제 띄울지는 TutorialGuide가 정한다.
public class OttiliaTalk : MonoBehaviour
{
    public Text speaker, line;
    public RawImage portrait;

    List<CutLine> lines;
    int index;

    public bool IsOpen => gameObject.activeSelf;

    // 클릭은 화면 전체 버튼(Next)이 받는다
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) Next();
    }

    // Resources/Portraits 폴더의 그림(이름이 Ottilia로 시작하는 것만, 말풍선 Ottilia_text 제외) 중 하나를 대사마다 무작위로 (바로 앞 대사와는 다른 그림). 없으면 씬에 지정된 임시 그림.
    // 씬의 Portrait 칸과 비율이 다르면 찌그러뜨리지 않고 자른다 (위쪽 가운데 기준)
    Texture2D[] faces;
    int face = -1;

    void NextFace()
    {
        if (faces == null) faces = Resources.LoadAll<Texture2D>("Portraits").Where(t => t.name.StartsWith("Ottilia") && !t.name.Contains("text")).ToArray();
        if (faces.Length == 0 || portrait == null) return;
        if (face < 0 || faces.Length == 1) face = Random.Range(0, faces.Length);
        else face = (face + Random.Range(1, faces.Length)) % faces.Length; // 1칸 이상 옮기므로 앞 그림과 겹치지 않음
        var tex = faces[face];
        var box = portrait.rectTransform.rect;
        float ratio = box.width / box.height * tex.height / tex.width; // 1보다 작으면 그림이 칸보다 옆으로 넓음
        float w = Mathf.Min(1f, ratio), h = Mathf.Min(1f, 1f / ratio);
        portrait.texture = tex;
        portrait.uvRect = new Rect((1f - w) / 2f, 1f - h, w, h);
    }

    public void Show(List<CutLine> talk)
    {
        lines = talk;
        index = -1;
        gameObject.SetActive(true);
        Next();
    }

    // 화면 전체 버튼, 스페이스바
    public void Next()
    {
        if (lines == null || ++index >= lines.Count) { gameObject.SetActive(false); return; }
        NextFace();
        speaker.text = lines[index].Speaker;
        // 말풍선이라 대사 앞뒤 따옴표는 빼고, 문장이 끝나면 줄을 바꾼다
        var text = Emphasis.Rich(lines[index].Text.Trim('“', '”'), onDark: line.color.grayscale > 0.5f);
        line.text = Regex.Replace(text, @"\.((?:</\w+>)*) ", ".$1\n"); // 밝은 말풍선(어두운 글씨)이면 종이용 강조색
    }
}
