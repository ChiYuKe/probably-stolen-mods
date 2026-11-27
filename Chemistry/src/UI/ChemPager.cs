using System;
using TMPro;
using UnityEngine;

namespace Chemistry
{
    /// <summary>
    /// 合成台 / 百科 / 实验室实验台共用这一套「上一页 / 下一页」。
    /// 分页条放在滚动区里当第一行重建，所以每次刷新都用全新的元素，id 传 null。
    /// </summary>
    internal static class ChemPager
    {
        public const int PageSize = 24;

        public static int PageCount(int total)
        {
            if (total <= 0)
            {
                return 1;
            }

            return (total + PageSize - 1) / PageSize;
        }

        public static int Clamp(int page, int total)
        {
            int pages = PageCount(total);
            if (page < 0)
            {
                return 0;
            }

            return page >= pages ? pages - 1 : page;
        }

        /// <summary>建一行「上一页 / 第 x/y 页 / 下一页」。</summary>
        public static void AddControls(CustomUIBuilder builder, Action prev, Action next,
            out CustomUIElement label, out CustomUIElement prevButton, out CustomUIElement nextButton)
        {
            label = null;
            prevButton = null;
            nextButton = null;

            if (builder == null)
            {
                return;
            }

            builder.BeginRow(6f, TextAnchor.MiddleLeft);

            builder.AddButton(Loc.Get("pager_prev", "上一页", "Prev"), prev);
            prevButton = builder.Last;
            if (prevButton != null)
            {
                prevButton.SetFixedWidth(92f);
                prevButton.SetPreferredSize(92f, 28f);
                ChemUi.StyleButtonText(prevButton, 12f, false, ChemUi.Main);
            }

            builder.AddLabel(" ");
            label = builder.Last;
            ChemUi.Style(label, 12f, 220f, 24f, false, ChemUi.Dim, false, TextAlignmentOptions.Center);

            builder.AddButton(Loc.Get("pager_next", "下一页", "Next"), next);
            nextButton = builder.Last;
            if (nextButton != null)
            {
                nextButton.SetFixedWidth(92f);
                nextButton.SetPreferredSize(92f, 28f);
                ChemUi.StyleButtonText(nextButton, 12f, false, ChemUi.Main);
            }

            builder.AddFlexibleSpace();
            builder.End();
        }

        public static void Update(CustomUIElement label, CustomUIElement prevButton, CustomUIElement nextButton,
            int page, int total)
        {
            int pages = PageCount(total);
            int current = Clamp(page, total);

            if (label != null)
            {
                label.SetText(Loc.Get(
                    "pager_status",
                    "第 {0}/{1} 页 · 共 {2} 种",
                    "Page {0}/{1} · {2} total",
                    current + 1,
                    pages,
                    total));
            }

            if (prevButton != null)
            {
                prevButton.SetInteractable(current > 0);
            }

            if (nextButton != null)
            {
                nextButton.SetInteractable(current < pages - 1);
            }
        }
    }
}
