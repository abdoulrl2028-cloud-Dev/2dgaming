using UnityEngine;
using UnityEngine.AI;

namespace CityPack.Zombies
{
    public enum ZombieState
    {
        Idle,
        Wander,
        Investigate,
        Alert,
        Chase,
        Attack,
        Search,
        Return,
        Dead
    }

    [RequireComponent(typeof(NavMeshAgent))]
    public class ZombieAI : MonoBehaviour
    {
        public ZombieDetection senses;
        public ZombieHealth health;
        public ZombieAudio audioPlayer;
        public Animator animator;
        public CityPack.Characters.HumanoidProceduralLocomotion procedural;
        public float wanderRadius = 6f;
        public float walkSpeed = 0.7f;
        public float chaseSpeed = 2.4f;
        public float attackSpeed = 1.2f;
        public float damage = 8f;
        public float attackCooldown = 1.3f;
        public Transform home;

        public ZombieState State { get; private set; } = ZombieState.Idle;

        NavMeshAgent _agent;
        Transform _target;
        Vector3 _interest;
        float _attackTimer;
        float _stateTimer;
        float _stepTimer;
        bool _active = true;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (senses == null)
                senses = GetComponent<ZombieDetection>();
            if (health == null)
                health = GetComponent<ZombieHealth>();
            if (home == null)
                home = transform;
            _interest = transform.position;
        }

        public void Activate(bool enabled)
        {
            _active = enabled;
            if (_agent != null)
                _agent.enabled = enabled;
        }

        public void Kill()
        {
            State = ZombieState.Dead;
            if (_agent != null)
                _agent.isStopped = true;
            if (audioPlayer != null)
                audioPlayer.PlayDeath();
        }

        void Update()
        {
            if (!_active || _agent == null || !_agent.enabled || !_agent.isOnNavMesh)
                return;
            if (health != null && health.IsDead)
            {
                State = ZombieState.Dead;
                return;
            }

            _attackTimer -= Time.deltaTime;
            _stateTimer += Time.deltaTime;
            var player = GameObject.FindGameObjectWithTag("Player");
            _target = player != null ? player.transform : null;
            var noise = 0f;
            if (_target != null)
            {
                var motor = _target.GetComponent<CityPack.Characters.SWATCharacterController>();
                if (motor != null)
                    noise = Mathf.Clamp01(motor.PlanarSpeed / 3f);
            }

            switch (State)
            {
                case ZombieState.Idle:
                case ZombieState.Wander:
                case ZombieState.Return:
                    _agent.speed = walkSpeed;
                    if (_target != null && senses != null && (senses.CanSee(_target) || senses.CanHear(_target, noise)))
                        Enter(ZombieState.Alert);
                    else if (State != ZombieState.Wander && _stateTimer > 2f)
                        Enter(ZombieState.Wander);
                    else if (State == ZombieState.Wander && !_agent.pathPending && _agent.remainingDistance < 0.6f)
                        Wander();
                    break;
                case ZombieState.Alert:
                    _agent.speed = 0f;
                    _agent.isStopped = true;
                    Face(_target);
                    if (_stateTimer > 0.8f)
                        Enter(ZombieState.Chase);
                    break;
                case ZombieState.Investigate:
                    _agent.speed = walkSpeed;
                    _agent.isStopped = false;
                    _agent.SetDestination(_interest);
                    if (_target != null && senses != null && senses.CanSee(_target))
                        Enter(ZombieState.Chase);
                    else if (_agent.remainingDistance < 0.8f)
                        Enter(ZombieState.Search);
                    break;
                case ZombieState.Chase:
                    _agent.isStopped = false;
                    _agent.speed = chaseSpeed;
                    if (_target == null)
                    {
                        Enter(ZombieState.Search);
                        break;
                    }
                    _interest = _target.position;
                    _agent.SetDestination(_target.position);
                    if (senses != null && Vector3.Distance(transform.position, _target.position) <= senses.attackDistance)
                        Enter(ZombieState.Attack);
                    else if (senses != null && Vector3.Distance(transform.position, _target.position) > senses.loseTargetDistance)
                        Enter(ZombieState.Search);
                    break;
                case ZombieState.Attack:
                    _agent.isStopped = true;
                    Face(_target);
                    if (_attackTimer <= 0f && _target != null)
                    {
                        _attackTimer = attackCooldown;
                        if (audioPlayer != null)
                            audioPlayer.PlayAttack();
                        if (animator != null)
                            animator.SetTrigger("Attack");
                        var motor = _target.GetComponent<CityPack.Characters.SWATCharacterController>();
                        if (motor != null)
                            motor.DebugTrigger("Hit");
                    }
                    if (_target == null || senses == null || Vector3.Distance(transform.position, _target.position) > senses.attackDistance + 0.4f)
                        Enter(ZombieState.Chase);
                    break;
                case ZombieState.Search:
                    _agent.isStopped = false;
                    _agent.speed = walkSpeed;
                    _agent.SetDestination(_interest);
                    if (_target != null && senses != null && senses.CanSee(_target))
                        Enter(ZombieState.Chase);
                    else if (_stateTimer > 6f)
                        Enter(ZombieState.Return);
                    break;
            }

            if (animator != null)
            {
                animator.SetFloat("Speed", _agent.velocity.magnitude);
                animator.SetBool("IsGrounded", true);
            }

            if (audioPlayer != null && _agent != null && _agent.velocity.magnitude > 0.15f && State != ZombieState.Dead)
            {
                _stepTimer -= Time.deltaTime;
                if (_stepTimer <= 0f)
                {
                    _stepTimer = State == ZombieState.Chase ? 0.38f : 0.62f;
                    audioPlayer.PlayStep();
                }
            }
        }

        void Enter(ZombieState next)
        {
            State = next;
            _stateTimer = 0f;
            if (_agent != null)
                _agent.isStopped = next == ZombieState.Alert || next == ZombieState.Attack || next == ZombieState.Dead;
            if (next == ZombieState.Alert && audioPlayer != null)
                audioPlayer.PlayAlert();
            if (next == ZombieState.Wander)
                Wander();
            if (next == ZombieState.Return && home != null && _agent != null)
                _agent.SetDestination(home.position);
        }

        void Wander()
        {
            var point = transform.position + Random.insideUnitSphere * wanderRadius;
            point.y = transform.position.y;
            if (NavMesh.SamplePosition(point, out var hit, wanderRadius, NavMesh.AllAreas))
                _agent.SetDestination(hit.position);
        }

        void Face(Transform target)
        {
            if (target == null)
                return;
            var flat = target.position - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.01f)
                return;
            var look = Quaternion.LookRotation(flat);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-4f * Time.deltaTime));
        }
    }
}
