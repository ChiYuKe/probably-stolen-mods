using System.Collections.Generic;
using UnityEngine;

namespace NeteaseCassette
{
    // 往容器里翻磁带：收纳盒放进磁带仓，里面的磁带也要能当曲目
    public static class CassetteScan
    {
        // 只收容器里的磁带；直接放在仓里的磁带原版已经收过了，别再收一遍
        public static void CollectTracks(GameInventory inventory, List<string> tracks)
        {
            if (inventory == null || tracks == null) return;

            foreach (var item in inventory.childItems)
            {
                if (item == null) continue;
                if (item.IsTag(CassetteHelper.CASSETTE_TAG)) continue;
                CollectInner(InnerInventory(item), tracks);
            }
        }

        private static void CollectInner(GameInventory inventory, List<string> tracks)
        {
            if (inventory == null) return;

            foreach (var item in inventory.childItems)
            {
                if (item == null) continue;

                if (item.IsTag(CassetteHelper.CASSETTE_TAG))
                {
                    if (item.IsTag(CassetteHelper.CASSETTE_CONTENT))
                    {
                        string id = item.GetTagReadonly(CassetteHelper.CASSETTE_CONTENT).GetString();
                        if (!string.IsNullOrEmpty(id)) tracks.Add(id);
                    }
                    continue;
                }

                CollectInner(InnerInventory(item), tracks);
            }
        }

        public static GameItem Find(GameInventory inventory, string trackId)
        {
            if (inventory == null || string.IsNullOrEmpty(trackId)) return null;

            foreach (var item in inventory.childItems)
            {
                if (item == null) continue;

                if (item.IsTag(CassetteHelper.CASSETTE_TAG))
                {
                    if (item.IsTag(CassetteHelper.CASSETTE_CONTENT) &&
                        item.GetTagReadonly(CassetteHelper.CASSETTE_CONTENT).GetString() == trackId)
                    {
                        return item;
                    }
                    continue;
                }

                GameItem found = Find(InnerInventory(item), trackId);
                if (found != null) return found;
            }

            return null;
        }

        public static GameItem FindIn(GraphNodeStorage node, string trackId)
        {
            return Find(node as GameInventory, trackId);
        }

        private static GameInventory InnerInventory(GameItem item)
        {
            if (item == null || item.contentWindow == null) return null;
            return item.contentWindow.child as GameInventory;
        }
    }
}
