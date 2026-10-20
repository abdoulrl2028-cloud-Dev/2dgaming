using UnityEngine;

namespace CityPack.Characters
{
    public class SWATDebugController : MonoBehaviour
    {
        public SWATCharacterController body;
        public Animator animator;
        public bool showOverlay = true;

        void Reset()
        {
            body = GetComponent<SWATCharacterController>();
            animator = GetComponentInChildren<Animator>();
        }

        [ContextMenu("Idle")] public void Idle() => Play("IDLE");
        [ContextMenu("Walk")] public void Walk() => Play("WALK");
        [ContextMenu("Run")] public void Run() => Play("RUN");
        [ContextMenu("Sprint")] public void Sprint() => Play("SPRINT");
        [ContextMenu("Crouch")] public void Crouch() => Play("CROUCH");
        [ContextMenu("Aim")] public void Aim() => Play("AIM");
        [ContextMenu("Shoot")] public void Shoot() { if (body != null) body.DebugTrigger("Shoot"); else Play("SHOOT"); }
        [ContextMenu("Reload")] public void Reload() => Play("RELOAD");
        [ContextMenu("Jump")] public void Jump() => Play("JUMP");
        [ContextMenu("Hit")] public void Hit() { if (body != null) body.DebugTrigger("Hit"); else Play("HIT"); }
        [ContextMenu("Death")] public void Death() { if (body != null) body.DebugTrigger("Death"); else Play("DEATH"); }

        void Play(string state)
        {
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (animator != null)
                animator.Play(state, 0, 0f);
        }

        void OnGUI()
        {
            if (!showOverlay || !Application.isPlaying)
                return;
            GUILayout.BeginArea(new Rect(16, 110, 220, 280), GUI.skin.box);
            GUILayout.Label("SWAT debug");
            if (GUILayout.Button("Idle")) Idle();
            if (GUILayout.Button("Walk")) Walk();
            if (GUILayout.Button("Run")) Run();
            if (GUILayout.Button("Sprint")) Sprint();
            if (GUILayout.Button("Crouch")) Crouch();
            if (GUILayout.Button("Aim")) Aim();
            if (GUILayout.Button("Shoot")) Shoot();
            if (GUILayout.Button("Reload")) Reload();
            if (GUILayout.Button("Jump")) Jump();
            if (GUILayout.Button("Hit")) Hit();
            if (GUILayout.Button("Death")) Death();
            GUILayout.EndArea();
        }
    }
}
