using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 고정 카메라에 붙여 마우스 위치를 레이캐스트로 Clickable에 전달한다 (올리기 = 확대 + 이름표, 클릭 = 실행).
[RequireComponent(typeof(Camera))]
public class ClickInteractor : MonoBehaviour
{
    [Tooltip("마우스를 올린 물체 이름을 커서 옆에 표시할 텍스트")]
    public Text hoverLabel;

    // 도장 연출(StampMotion) 중에는 클릭을 받지 않는다 (올리기·이름표는 그대로)
    public static bool Blocked;

    Camera cam;
    Clickable hovered;

    void Awake()
    {
        cam = GetComponent<Camera>();
        Blocked = false; // 도메인 리로드를 끈 에디터에서 이전 플레이 값이 남지 않게
    }

    void Update()
    {
        var mouse = Mouse.current;
        Clickable target = null;
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(); // UI(서류 화면 등) 위면 3D 무시
        if (mouse != null && !overUI && Physics.Raycast(cam.ScreenPointToRay(mouse.position.ReadValue()), out var hit, 10f))
            target = hit.collider.GetComponentInParent<Clickable>();

        if (target != hovered)
        {
            if (hovered != null) hovered.SetHover(false);
            if (target != null) target.SetHover(true);
            hovered = target;
        }

        if (hovered != null && !Blocked && mouse.leftButton.wasPressedThisFrame) hovered.Click();

        if (hoverLabel == null) return;
        bool show = hovered != null && !string.IsNullOrEmpty(hovered.displayName);
        if (hoverLabel.gameObject.activeSelf != show) hoverLabel.gameObject.SetActive(show);
        if (!show) return;
        hoverLabel.text = hovered.displayName;
        // 커서 오른쪽 아래에 표시. 화면 오른쪽·아래 끝에 가까우면 반대쪽으로 넘긴다
        Vector2 p = mouse.position.ReadValue();
        bool flipX = p.x > Screen.width - 300f, flipY = p.y < 80f;
        var rt = hoverLabel.rectTransform;
        rt.pivot = new Vector2(flipX ? 1f : 0f, flipY ? 0f : 1f);
        rt.position = p + new Vector2(flipX ? -16f : 16f, flipY ? 20f : -20f);
    }
}
