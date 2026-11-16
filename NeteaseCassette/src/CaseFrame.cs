using System;
using System.Collections;
using UnityEngine;

namespace NeteaseCassette
{
    // 原版只给「仓里直接放着的磁带」画播放框，盒子里的那盘得自己补
    public static class CaseFrame
    {
        private static bool running;
        private static float nextScan;

        public static void Start()
        {
            if (running) return;
            running = true;
            ModHelper.StartCoroutine(ModEntry.ModId, Run());
        }

        public static void Stop()
        {
            running = false;
        }

        private static IEnumerator Run()
        {
            try
            {
                while (running)
                {
                    try
                    {
                        Tick();
                    }
                    catch (Exception e)
                    {
                        ModEntry.Log?.Warning("刷新播放框出错：" + e.Message);
                    }
                    yield return null;
                }
            }
            finally
            {
                running = false;
            }
        }

        private static void Tick()
        {
            if (Time.unscaledTime < nextScan) return;
            nextScan = Time.unscaledTime + 0.5f;

            var machine = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.cassettePlayer : null;
            if (machine == null) return;

            var deck = MachineCassettePlayer.GetCassetteInventory();
            if (deck == null) return;

            foreach (var item in deck.childItems)
            {
                if (item == null || item.IsTag(CassetteHelper.CASSETTE_TAG)) continue;

                var inner = item.contentWindow != null ? item.contentWindow.child as GameInventory : null;
                if (inner == null) continue;

                foreach (var child in inner.childItems)
                {
                    var element = child as GameItemElement;
                    if (element != null) element.RefreshPlayingFrame();
                }
            }
        }

        // 磁带在仓里的某个容器里、而且正是当前播放的那盘时，返回绿色（暂停时半透明）
        public static Color? ContainerColor(GameItem item)
        {
            if (item == null || !item.IsTag(CassetteHelper.CASSETTE_TAG)) return null;

            var parent = item.parentInventory;
            if (parent == null) return null;

            var deck = MachineCassettePlayer.GetCassetteInventory();
            if (deck == null || parent == deck) return null;   // 直系磁带交给原版

            var container = parent.GetParentItem();
            if (container == null || container.parentInventory != deck) return null;

            var machine = EmporiumEntry.Instance != null ? EmporiumEntry.Instance.cassettePlayer : null;
            if (machine == null) return null;

            int state = machine.GetTagReadonly(MachineCassettePlayer.CASETTE_PLAYER_STATE_INT).GetInt();
            if (state != 1 && state != 2) return null;

            string track = machine.GetTagReadonly(MachineCassettePlayer.CURRENT_PLAYING_TRACK_ID_TAG).GetString();
            if (string.IsNullOrEmpty(track)) return null;
            if (!item.IsTag(CassetteHelper.CASSETTE_CONTENT)) return null;
            if (item.GetTagReadonly(CassetteHelper.CASSETTE_CONTENT).GetString() != track) return null;

            Color color = RenderHandler.GetColorFromPalette(RenderHandler.ColorPalette.Green);
            if (state == 2) color.a *= 0.45f;
            return color;
        }
    }
}
