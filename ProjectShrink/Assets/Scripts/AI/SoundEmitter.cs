using System;
using UnityEngine;

namespace ProjectShrink.AI
{
    /// <summary>
    /// Helper phát sóng âm thanh toàn cục (Decoupled Sound System).
    /// Bất kỳ script nào (Player bước chân, Ném đồ rơi xuống sàn, Cửa sập...)
    /// chỉ cần gọi: SoundEmitter.Emit(transform.position, bánKínhÂmThanh);
    /// Tất cả HearingSensor trong tầm nghe sẽ lập tức phát hiện được.
    /// </summary>
    public static class SoundEmitter
    {
        /// <summary>
        /// Sự kiện phát âm thanh: (Vị trí phát âm, Bán kính lan tỏa, Vật thể gây tiếng động)
        /// </summary>
        public static event Action<Vector3, float, GameObject> OnSoundEmitted;

        /// <summary>
        /// Phát ra tiếng động tại một tọa độ trong không gian 2.5D/3D.
        /// </summary>
        /// <param name="origin">Tọa độ phát ra âm thanh</param>
        /// <param name="radius">Bán kính lan truyền của âm thanh (mét)</param>
        /// <param name="source">Game object phát ra tiếng (Ví dụ: Player, Chai lọ vỡ)</param>
        public static void Emit(Vector3 origin, float radius, GameObject source = null)
        {
            if (radius <= 0f) return;
            OnSoundEmitted?.Invoke(origin, radius, source);
        }
    }
}
