using System;
using UnityEngine;
using UnityEngine.AI;

namespace ProjectShrink.AI
{
    /// <summary>
    /// Các trạng thái cơ bản của Finite State Machine (FSM) cho Enemy AI.
    /// Chuẩn bị sẵn cho W2-GC-05 (Chuột Đột Biến) và W3-GC-03 (Robot An Ninh).
    /// </summary>
    public enum AIState
    {
        Idle,           // Đứng yên quan sát
        Patrol,         // Đi tuần theo các điểm Waypoint
        Alert,          // Cảnh giác, quay đầu điều tra tiếng động nghe được
        Chase,          // Phát hiện người chơi trong tầm mắt -> Rượt đuổi
        Attack,         // Áp sát mục tiêu -> Tấn công cận chiến hoặc bắn đạn
        Dead            // Đã bị tiêu diệt
    }

    /// <summary>
    /// Lớp cơ sở (Base Class) cho toàn bộ AI Quái vật trong Project Shrink.
    /// Tích hợp sẵn VisionSensor, HearingSensor, NavMeshAgent và luồng chuyển trạng thái FSM.
    /// Thuộc gói công việc Tuần 1: W1-GC-04.
    /// </summary>
    [RequireComponent(typeof(VisionSensor))]
    [RequireComponent(typeof(HearingSensor))]
    public abstract class AIEntity : MonoBehaviour
    {
        [Header("--- Thông số Cơ bản ---")]
        [SerializeField] protected string aiName = "Mutant Enemy";
        [SerializeField] protected float maxHealth = 100f;
        [SerializeField] protected float currentHealth;

        [Header("--- Trạng thái AI FSM ---")]
        [SerializeField] protected AIState currentState = AIState.Patrol;

        [Header("--- Tốc độ di chuyển ---")]
        [SerializeField] protected float patrolSpeed = 2.0f;
        [SerializeField] protected float chaseSpeed = 4.5f;

        // Caching components
        protected VisionSensor visionSensor;
        protected HearingSensor hearingSensor;
        protected NavMeshAgent navAgent;

        // Events
        public event Action<AIState> OnStateChanged;
        public event Action OnDeath;

        public AIState CurrentState => currentState;
        public bool IsDead => currentState == AIState.Dead;
        public VisionSensor Vision => visionSensor;
        public HearingSensor Hearing => hearingSensor;

        protected virtual void Awake()
        {
            currentHealth = maxHealth;
            visionSensor = GetComponent<VisionSensor>();
            hearingSensor = GetComponent<HearingSensor>();
            navAgent = GetComponent<NavMeshAgent>();
        }

        protected virtual void Start()
        {
            // Đăng ký nhận sự kiện từ các cảm biến
            visionSensor.OnTargetSpotted += HandleTargetSpotted;
            visionSensor.OnTargetLost += HandleTargetLost;
            hearingSensor.OnSoundHeard += HandleSoundHeard;

            if (navAgent != null)
            {
                navAgent.speed = patrolSpeed;
            }
        }

        protected virtual void Update()
        {
            if (IsDead) return;

            // Xử lý logic hành vi tương ứng theo từng trạng thái
            switch (currentState)
            {
                case AIState.Idle:
                    UpdateIdleState();
                    break;
                case AIState.Patrol:
                    UpdatePatrolState();
                    break;
                case AIState.Alert:
                    UpdateAlertState();
                    break;
                case AIState.Chase:
                    UpdateChaseState();
                    break;
                case AIState.Attack:
                    UpdateAttackState();
                    break;
            }
        }

        /// <summary>
        /// Thay đổi trạng thái FSM và phát sự kiện thông báo.
        /// </summary>
        public virtual void ChangeState(AIState newState)
        {
            if (currentState == newState || IsDead) return;

            AIState oldState = currentState;
            currentState = newState;

            OnExitState(oldState);
            OnEnterState(newState);

            OnStateChanged?.Invoke(newState);
        }

        protected virtual void OnEnterState(AIState state)
        {
            if (navAgent == null) return;

            switch (state)
            {
                case AIState.Patrol:
                    navAgent.speed = patrolSpeed;
                    navAgent.isStopped = false;
                    break;
                case AIState.Chase:
                    navAgent.speed = chaseSpeed;
                    navAgent.isStopped = false;
                    break;
                case AIState.Attack:
                    navAgent.isStopped = true;
                    break;
                case AIState.Alert:
                    navAgent.speed = patrolSpeed * 0.8f;
                    break;
            }
        }

        protected virtual void OnExitState(AIState state)
        {
            // Dọn dẹp trạng thái cũ nếu cần
        }

        #region Xử lý Sự kiện Cảm biến (Sensors Handlers)
        /// <summary>
        /// Khi mắt nhìn thấy người chơi.
        /// </summary>
        protected virtual void HandleTargetSpotted(Transform target)
        {
            if (IsDead) return;
            // Phát hiện thấy người chơi -> Lập tức chuyển sang rượt đuổi
            ChangeState(AIState.Chase);
        }

        /// <summary>
        /// Khi người chơi khuất khỏi tầm nhìn (núp sau tường hoặc chạy ra xa).
        /// </summary>
        protected virtual void HandleTargetLost()
        {
            if (IsDead || currentState != AIState.Chase) return;
            // Mất dấu -> Chuyển sang nghi ngờ/tìm kiếm quanh khu vực
            ChangeState(AIState.Alert);
        }

        /// <summary>
        /// Khi tai nghe thấy tiếng động (bước chân, ném đồ).
        /// </summary>
        protected virtual void HandleSoundHeard(Vector3 soundPos, GameObject source)
        {
            if (IsDead) return;

            // Nếu đang rượt đuổi hoặc đang cắn thì không bị xao nhãng bởi tiếng động nhỏ
            if (currentState == AIState.Chase || currentState == AIState.Attack) return;

            // Chuyển sang trạng thái Alert để tiến lại gần vị trí tiếng động kiểm tra
            ChangeState(AIState.Alert);

            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.SetDestination(soundPos);
            }
        }
        #endregion

        #region Các hàm ảo cập nhật từng trạng thái (Override ở lớp con)
        protected virtual void UpdateIdleState() { }
        protected virtual void UpdatePatrolState() { }
        protected virtual void UpdateAlertState() { }
        protected virtual void UpdateChaseState() { }
        protected virtual void UpdateAttackState() { }
        #endregion

        #region Nhận sát thương & Tiêu diệt
        public virtual void TakeDamage(float damage)
        {
            if (IsDead) return;

            currentHealth -= damage;
            if (currentHealth <= 0f)
            {
                currentHealth = 0f;
                Die();
            }
        }

        protected virtual void Die()
        {
            ChangeState(AIState.Dead);
            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.isStopped = true;
                navAgent.enabled = false;
            }

            OnDeath?.Invoke();
            // Tắt va chạm hoặc trigger hiệu ứng chết
        }
        #endregion
    }
}
