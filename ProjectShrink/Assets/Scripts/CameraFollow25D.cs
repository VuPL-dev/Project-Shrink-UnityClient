using UnityEngine;

/// <summary>
/// Camera 2.5D Little Nightmares:
/// - Khi chưa chạy game, bạn đặt camera ở góc nào đẹp nhất thì KHI CHẠY GAME GIỮ NGUYÊN GÓC ĐÓ 100%!
/// - Tự động tính toán Offset và Góc nhìn chuẩn xác từ góc bạn vừa đặt.
/// - Bám theo nhân vật mượt mà theo đúng góc nhìn đó, không bị văng góc hay nhìn vào tường!
/// </summary>
public class CameraFollow25D : MonoBehaviour
{
    [Header("1. Target Nhân Vật")]
    public Transform target;

    [Header("2. Tự Động Lấy Góc Hiện Tại (Khuyên Dùng)")]
    [Tooltip("Tự động ghi nhớ vị trí và góc nhìn bạn vừa đặt trước khi bấm Play")]
    public bool lockCurrentSceneView = true;

    [Header("3. Độ Mượt (Smooth Damping)")]
    [Range(0.01f, 1f)]
    public float smoothSpeed = 0.08f;

    [Header("4. Thông Số Offset (Tự tính hoặc chỉnh tay)")]
    public Vector3 offset;
    public Vector3 fixedRotation;

    private Vector3 currentVelocity;

    void Start()
    {
        if (target == null) return;

        if (lockCurrentSceneView)
        {
            // Tự động đo khoảng cách giữa camera bạn vừa căn và nhân vật
            offset = transform.position - target.position;
            fixedRotation = transform.eulerAngles;
        }
        else
        {
            transform.position = target.position + offset;
            transform.eulerAngles = fixedRotation;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Bám theo vị trí nhân vật với đúng độ lệch ban đầu
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothSpeed);

        // Khóa chặt góc xoay đã căn chuẩn, không bị lệch
        transform.eulerAngles = fixedRotation;
    }
}
