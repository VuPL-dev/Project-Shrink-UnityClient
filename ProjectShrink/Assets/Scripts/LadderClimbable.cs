using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Thang Leo Đặt Trong Thế Giới (Placeable Climbable Ladder):
/// - Khi người chơi chế tạo Thang và đặt xuống đất, thang sẽ dựng đứng tại vị trí chỉ định.
/// - Khi nhân vật tiến lại gần thang, có thể bấm W / Space để leo lên cao và S để leo xuống.
/// - Khi lên tới đỉnh thang, nhân vật có thể bước thẳng lên các bề mặt trên cao (bàn ghế, kệ sách, v.v.).
/// </summary>
public class LadderClimbable : MonoBehaviour
{
    public static readonly List<LadderClimbable> AllLadders = new List<LadderClimbable>();

    [Header("1. Thông Số Thang Leo")]
    public float ladderHeight = 0.36f;
    public float climbRadius = 0.12f;
    public float bottomY = 0.08f;
    public float topY = 0.44f;

    void OnEnable()
    {
        if (!AllLadders.Contains(this))
            AllLadders.Add(this);
    }

    void OnDisable()
    {
        AllLadders.Remove(this);
    }

    void Start()
    {
        bottomY = transform.position.y;
        topY = bottomY + ladderHeight;
    }

    /// <summary>
    /// Tìm thang leo gần nhất với vị trí của nhân vật trong bán kính maxDist
    /// </summary>
    public static LadderClimbable GetNearbyLadder(Vector3 playerPos, float maxDist)
    {
        LadderClimbable best = null;
        float bestDistSq = maxDist * maxDist;

        for (int i = 0; i < AllLadders.Count; i++)
        {
            LadderClimbable ladder = AllLadders[i];
            if (ladder == null) continue;

            Vector3 ladderPos = ladder.transform.position;
            float dx = ladderPos.x - playerPos.x;
            float dz = ladderPos.z - playerPos.z;
            float horizontalDistSq = dx * dx + dz * dz;

            if (horizontalDistSq <= bestDistSq)
            {
                // Kiểm tra xem nhân vật có nằm trong khoảng độ cao của thang không
                if (playerPos.y >= ladder.bottomY - 0.05f && playerPos.y <= ladder.topY + 0.10f)
                {
                    bestDistSq = horizontalDistSq;
                    best = ladder;
                }
            }
        }

        return best;
    }
}
