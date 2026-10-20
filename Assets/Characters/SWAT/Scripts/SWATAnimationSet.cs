using UnityEngine;

namespace CityPack.Characters
{
    /// <summary>
    /// Drop real AnimationClips here. Empty slots stay on the procedural pose.
    /// </summary>
    [CreateAssetMenu(fileName = "SWATAnimationSet", menuName = "City Pack/SWAT Animation Set")]
    public class SWATAnimationSet : ScriptableObject
    {
        public AnimationClip idle;
        public AnimationClip walk;
        public AnimationClip run;
        public AnimationClip sprint;
        public AnimationClip crouch;
        public AnimationClip crouchWalk;
        public AnimationClip jump;
        public AnimationClip fall;
        public AnimationClip land;
        public AnimationClip aim;
        public AnimationClip shoot;
        public AnimationClip reload;
        public AnimationClip hit;
        public AnimationClip death;
        public AnimationClip turnLeft;
        public AnimationClip turnRight;
        public AnimationClip melee;
        public AnimationClip sourceMixamo;
    }
}
