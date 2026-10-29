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
        public enum SoundType
        {
            Footstep,
            CombatAction,
            Generic
        }

        /// <summary>
        /// Sự kiện phát âm thanh: (Vị trí phát âm, Bán kính lan tỏa, Vật thể gây tiếng động)
        /// </summary>
        public static event Action<Vector3, float, GameObject> OnSoundEmitted;

        /// <summary>
        /// Sự kiện phát âm thanh có phân loại: (Vị trí phát âm, Loại âm thanh, Độ lớn/Bán kính)
        /// </summary>
        public static event Action<Vector3, SoundType, float> OnSoundTypeEmitted;

        /// <summary>
        /// Phát ra tiếng động tại một tọa độ trong không gian 2.5D/3D.
        /// </summary>
        public static void Emit(Vector3 origin, float radius, GameObject source = null)
        {
            if (radius <= 0f) return;
            OnSoundEmitted?.Invoke(origin, radius, source);
            OnSoundTypeEmitted?.Invoke(origin, SoundType.Generic, radius);
        }

        /// <summary>
        /// Phát ra tiếng động có phân loại (Bước chân, Giao chiến, v.v.).
        /// </summary>
        public static void EmitSound(Vector3 origin, SoundType type, float loudness)
        {
            if (loudness <= 0f) return;
            OnSoundTypeEmitted?.Invoke(origin, type, loudness);
            OnSoundEmitted?.Invoke(origin, loudness, null);
        }
    }
}
