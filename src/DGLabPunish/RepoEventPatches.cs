using HarmonyLib;
using UnityEngine;

namespace DGLabPunish
{
    internal static class RepoGuards
    {
        internal static bool IsLocalAvatar(PlayerAvatar avatar)
        {
            if (avatar == null)
            {
                return false;
            }

            if (object.ReferenceEquals(avatar, PlayerAvatar.instance))
            {
                return true;
            }

            try
            {
                return avatar.photonView != null && avatar.photonView.IsMine;
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsLocalHealth(PlayerHealth health)
        {
            PlayerAvatar local = PlayerAvatar.instance;
            return local != null && object.ReferenceEquals(local.playerHealth, health);
        }

        internal static StimController Stim()
        {
            return Plugin.Instance == null ? null : Plugin.Instance.Stim;
        }
    }

    [HarmonyPatch(typeof(PlayerHealth), "Hurt")]
    internal static class PlayerHealthHurtPatch
    {
        private static void Postfix(PlayerHealth __instance, int damage, bool savingGrace, int enemyIndex, bool hurtByHeal)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null && RepoGuards.IsLocalHealth(__instance))
            {
                stim.TriggerHurt(damage);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "PlayerDeathRPC")]
    internal static class PlayerAvatarDeathPatch
    {
        private static void Postfix(PlayerAvatar __instance)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null && RepoGuards.IsLocalAvatar(__instance))
            {
                stim.TriggerDeath();
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatarVisuals), "FootstepLight")]
    internal static class PlayerAvatarVisualsFootstepLightPatch
    {
        private static void Prefix(PlayerAvatarVisuals __instance)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null && __instance != null && RepoGuards.IsLocalAvatar(__instance.playerAvatar))
            {
                stim.TriggerFootstepFromVisuals("轻");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatarVisuals), "FootstepMedium")]
    internal static class PlayerAvatarVisualsFootstepMediumPatch
    {
        private static void Prefix(PlayerAvatarVisuals __instance)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null && __instance != null && !__instance.isMenuAvatar && RepoGuards.IsLocalAvatar(__instance.playerAvatar))
            {
                stim.TriggerFootstepFromVisuals("中");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatarVisuals), "FootstepHeavy")]
    internal static class PlayerAvatarVisualsFootstepHeavyPatch
    {
        private static void Prefix(PlayerAvatarVisuals __instance)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null && __instance != null && RepoGuards.IsLocalAvatar(__instance.playerAvatar))
            {
                stim.TriggerFootstepFromVisuals("重");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatarVisuals), "LeftFootDown")]
    internal static class PlayerAvatarVisualsLeftFootDownPatch
    {
        private static void Prefix(PlayerAvatarVisuals __instance)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null && __instance != null && RepoGuards.IsLocalAvatar(__instance.playerAvatar))
            {
                stim.TriggerFootDown(true, "左脚落地");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatarVisuals), "RightFootDown")]
    internal static class PlayerAvatarVisualsRightFootDownPatch
    {
        private static void Prefix(PlayerAvatarVisuals __instance)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null && __instance != null && RepoGuards.IsLocalAvatar(__instance.playerAvatar))
            {
                stim.TriggerFootDown(false, "右脚落地");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatarVisuals), "LeftFootDownSlow")]
    internal static class PlayerAvatarVisualsLeftFootDownSlowPatch
    {
        private static void Prefix(PlayerAvatarVisuals __instance)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null && __instance != null && RepoGuards.IsLocalAvatar(__instance.playerAvatar))
            {
                stim.TriggerFootDown(true, "左脚慢落地");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatarVisuals), "RightFootDownSlow")]
    internal static class PlayerAvatarVisualsRightFootDownSlowPatch
    {
        private static void Prefix(PlayerAvatarVisuals __instance)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null && __instance != null && RepoGuards.IsLocalAvatar(__instance.playerAvatar))
            {
                stim.TriggerFootDown(false, "右脚慢落地");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "Footstep")]
    internal static class PlayerAvatarFootstepPatch
    {
        private static void Postfix(PlayerAvatar __instance)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null && RepoGuards.IsLocalAvatar(__instance))
            {
                stim.TriggerFootstepFallback();
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "Jump")]
    internal static class PlayerAvatarJumpPatch
    {
        private static void Postfix(PlayerAvatar __instance)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null && RepoGuards.IsLocalAvatar(__instance))
            {
                stim.TriggerJump();
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "Land")]
    internal static class PlayerAvatarLandPatch
    {
        private static void Postfix(PlayerAvatar __instance)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null && RepoGuards.IsLocalAvatar(__instance))
            {
                stim.TriggerLand();
            }
        }
    }

    [HarmonyPatch(typeof(PlayerAvatar), "Slide")]
    internal static class PlayerAvatarSlidePatch
    {
        private static void Postfix(PlayerAvatar __instance)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null && RepoGuards.IsLocalAvatar(__instance))
            {
                stim.TriggerSlide();
            }
        }
    }

    [HarmonyPatch(typeof(PlayerController), "Update")]
    internal static class PlayerControllerUpdatePatch
    {
        private static void Postfix(PlayerController __instance)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null)
            {
                stim.UpdatePlayerMovementState(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(EnemyHunterAnim), "FootstepShort")]
    internal static class EnemyHunterFootstepShortPatch
    {
        private static void Postfix(EnemyHunterAnim __instance)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null && __instance != null)
            {
                stim.TriggerEnemyFootstep(__instance.transform.position);
            }
        }
    }

    [HarmonyPatch(typeof(EnemyHunterAnim), "FootstepLong")]
    internal static class EnemyHunterFootstepLongPatch
    {
        private static void Postfix(EnemyHunterAnim __instance)
        {
            StimController stim = RepoGuards.Stim();
            if (stim != null && __instance != null)
            {
                stim.TriggerEnemyFootstep(__instance.transform.position);
            }
        }
    }
}
