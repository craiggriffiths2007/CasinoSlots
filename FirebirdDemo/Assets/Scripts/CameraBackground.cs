using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class CameraBackground : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    private void LateUpdate()
    {
        if (targetCamera == null || !targetCamera.orthographic)
            return;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer.sprite == null)
            return;

        Vector2 size = renderer.sprite.bounds.size;
        float height = targetCamera.orthographicSize * 2f;
        float scale = Mathf.Max(height / size.y, height * targetCamera.aspect / size.x);
        transform.position = targetCamera.transform.position + targetCamera.transform.forward * 20f;
        transform.rotation = targetCamera.transform.rotation;
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}
