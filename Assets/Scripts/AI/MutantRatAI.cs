using UnityEngine;

namespace ProjectShrink.AI
{
    /// <summary>
    /// AI Chuột Đột Biến (Mutant Rat) - Kẻ địch cận chiến.
    /// Kế thừa từ AIEntity, cài đặt chi tiết FSM Patrol, Alert, Chase, Attack.
    /// Thuộc gói công việc Tuần 2: W2-GC-05.
    /// </summary>
    public class MutantRatAI : AIEntity
    {
        [Header("--- Cấu hình Tuần tra (Patrol) ---")]
        [SerializeField] private Transform[] waypoints;
        [SerializeField] private float waypointTolerance = 1.0f;
        [SerializeField] private float waitAtWaypointTime = 2.0f;
        private int currentWaypointIndex = 0;
        private float waypointTimer = 0f;

        [Header("--- Cấu hình Cảnh giác (Alert) ---")]
        [SerializeField] private float searchDuration = 4.0f;
        private float searchTimer = 0f;

        [Header("--- Cấu hình Tấn công Cận chiến (Attack) ---")]
        [SerializeField] private float attackRange = 1.3f;
        [SerializeField] private float attackDamage = 15f;
        [SerializeField] private float attackCooldown = 1.5f;
        private float lastAttackTime = -99f;

        protected override void Awake()
        {
            base.Awake();
            aiName = "Mutant Rat";
        }

        #region Patrol Logic
        protected override void UpdatePatrolState()
        {
            if (waypoints == null || waypoints.Length == 0) return;

            Transform targetWaypoint = waypoints[currentWaypointIndex];
            if (targetWaypoint == null) return;

            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.SetDestination(targetWaypoint.position);

                // Khi đến gần điểm Waypoint
                if (navAgent.remainingDistance <= waypointTolerance && !navAgent.pathPending)
                {
                    waypointTimer += Time.deltaTime;
                    if (waypointTimer >= waitAtWaypointTime)
                    {
                        waypointTimer = 0f;
                        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                    }
                }
            }
        }
        #endregion

        #region Alert / Search Logic
        protected override void UpdateAlertState()
        {
            searchTimer += Time.deltaTime;

            // Xoay nhẹ quan sát xung quanh khi đang nghi ngờ
            transform.Rotate(Vector3.up, 45f * Time.deltaTime);

            if (searchTimer >= searchDuration)
            {
                searchTimer = 0f;
                // Hết thời gian tìm kiếm không thấy ai -> Quay lại tuần tra bình thường
                ChangeState(AIState.Patrol);
            }
        }
        #endregion

        #region Chase Logic
        protected override void UpdateChaseState()
        {
            Transform target = visionSensor.CurrentTarget;
            if (target == null)
            {
                ChangeState(AIState.Alert);
                return;
            }

            if (navAgent != null && navAgent.isOnNavMesh)
            {
                navAgent.SetDestination(target.position);
            }

            // Kiểm tra cự ly áp sát để chuyển sang cắn (Attack)
            float distToTarget = Vector3.Distance(transform.position, target.position);
            if (distToTarget <= attackRange)
            {
                ChangeState(AIState.Attack);
            }
        }
        #endregion

        #region Attack Logic
        protected override void UpdateAttackState()
        {
            Transform target = visionSensor.CurrentTarget;
            if (target == null)
            {
                ChangeState(AIState.Alert);
                return;
            }

            // Xoay mặt về phía người chơi khi cắn
            Vector3 lookDirection = (target.position - transform.position).normalized;
            lookDirection.y = 0;
            if (lookDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDirection), Time.deltaTime * 8f);
            }

            float distToTarget = Vector3.Distance(transform.position, target.position);

            // Nếu người chơi chạy ra xa khỏi tầm cắn -> Tiếp tục rượt đuổi
            if (distToTarget > attackRange * 1.2f)
            {
                ChangeState(AIState.Chase);
                return;
            }

            // Thực hiện đòn cắn cận chiến
            if (Time.time >= lastAttackTime + attackCooldown)
            {
                PerformBiteAttack(target);
                lastAttackTime = Time.time;
            }
        }

        private void PerformBiteAttack(Transform target)
        {
            Debug.Log($"<color=red>[MutantRat]</color> 🐀 Cắn {target.name} gây {attackDamage} sát thương!");
            
            // Nếu target có component nhận sát thương (PlayerStats hoặc IDamageable), gọi nhận damage:
            // var damageable = target.GetComponent<IDamageable>();
            // damageable?.TakeDamage(attackDamage);
        }
        #endregion

        private void OnDrawGizmosSelected()
        {
            // Vẽ phạm vi cắn cận chiến (màu đỏ tươi)
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
