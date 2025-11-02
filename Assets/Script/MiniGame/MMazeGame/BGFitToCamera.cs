using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class BGFitToCamera : MonoBehaviour
{
    void Start()
    {
        FitToCamera();
    }

    void FitToCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("Main Camera not found!");
            return;
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr.sprite == null)
        {
            Debug.LogWarning("No sprite assigned to BG!");
            return;
        }

        // 카메라 높이와 너비 계산
        float worldScreenHeight = cam.orthographicSize * 2f;
        float worldScreenWidth = worldScreenHeight * cam.aspect;

        // 스프라이트 크기(유닛 단위)
        float spriteWidth = sr.sprite.bounds.size.x;
        float spriteHeight = sr.sprite.bounds.size.y;

        // 배율 계산
        Vector3 scale = transform.localScale;
        scale.x = worldScreenWidth / spriteWidth;
        scale.y = worldScreenHeight / spriteHeight;
        transform.localScale = scale;

        // 카메라 중앙에 위치
        transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, 0f);

        // 레이어 순서: 항상 미로보다 뒤
        sr.sortingOrder = -10;
    }
}