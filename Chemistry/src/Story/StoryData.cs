using System.Collections.Generic;

namespace Chemistry
{
    /// <summary>
    /// 地下实验室背景故事线的 8 章文案（中英双语）。
    /// 只放数据，解锁/存档/奖励逻辑都在 StoryService 里。
    /// 内容方向：当铺地板下的暗门 → 前任化学家的旧实验室 → 旧笔记里的配方 →
    /// 酸碱、有机物、危险品越做越大 → 笔记反复提到「深空化学公司」→
    /// 终章把 DSC-07 的线索交给玩家，为后续「化学公司」阶段留钩子。
    /// </summary>
    internal static class StoryData
    {
        public static StoryChapter[] CreateChapters()
        {
            return new[]
            {
                new StoryChapter
                {
                    Id = "ch1",
                    TitleZh = "地板下的暗门",
                    TitleEn = "The Trapdoor Under the Floor",
                    TextZh = "你撬开柜台后那块总在响的地板，下面是一段向下的铁梯。\n"
                        + "冷气从井口漫上来，带着旧试剂和焊锡的味道——这间当铺还有个连房东都没提过的地下室。\n"
                        + "墙上有人用记号笔写着：「别动通风橱的第三格。」字迹已经被潮气泡花了。",
                    TextEn = "You pry up the floorboard behind the counter — the one that always echoed underfoot — and find an iron ladder going down.\n"
                        + "Cold air climbs out, smelling of old reagents and solder. The shop has a basement nobody bothered to mention, not even the landlord.\n"
                        + "Someone wrote on the wall in marker: 'Don't touch the third drawer of the fume hood.' The damp has eaten half the letters.",
                    HintZh = "第一次下到地下实验室。",
                    HintEn = "Visit the underground lab for the first time.",
                    Reward = 80,
                },
                new StoryChapter
                {
                    Id = "ch2",
                    TitleZh = "旧笔记的第一页",
                    TitleEn = "The First Page of the Old Notes",
                    TextZh = "实验台抽屉里压着一本线圈本，第一页只写了半句：「所有东西都能拆开，包括……」\n"
                        + "你照着上面褪色的配方做出第一瓶化合物，托盘里的玻璃轻轻一响，像很久以前有人替你数过拍子。\n"
                        + "落款是一个写得很急的签名，姓氏看着像「沈」，也可能是「申」。",
                    TextEn = "A spiral notebook is wedged in the bench drawer. The first page holds half a sentence: 'Everything can be taken apart, including…'\n"
                        + "You follow the faded recipe for your first compound, and the glass on the tray ticks softly, as if someone long ago counted the beat for you.\n"
                        + "It is signed in a hurried hand. The surname reads like Shen, or maybe Sen.",
                    HintZh = "在实验室完成第一次合成。",
                    HintEn = "Synthesize anything in the lab for the first time.",
                    Reward = 120,
                },
                new StoryChapter
                {
                    Id = "ch3",
                    TitleZh = "酸写在纸上是不会疼的",
                    TitleEn = "Acid on Paper Doesn't Burn",
                    TextZh = "笔记中间夹着一张烧穿角的配方纸，页边写着：「酸是这行最老实的伙计，它只烧它碰到的东西。」\n"
                        + "你制出第一瓶酸，气味顺着口罩边缘钻进来，眼睛先替你疼了一下。\n"
                        + "翻到页脚，那里贴着一张采购单的残片：硝酸、硫酸，每月二十桶，收货地址被撕掉了，只剩一个编号——DSC-07。",
                    TextEn = "A recipe sheet with a scorched corner sits in the middle of the notebook. In the margin: 'Acid is the most honest hand in this trade. It only burns what it touches.'\n"
                        + "Your first acid comes off the bench and the fumes find the edge of your mask; your eyes ache before you do.\n"
                        + "At the foot of the page is a torn purchase order — nitric acid, sulfuric acid, twenty drums a month. The address is ripped away, leaving one code: DSC-07.",
                    HintZh = "合成第一种酸。",
                    HintEn = "Synthesize your first acid.",
                    Reward = 150,
                },
                new StoryChapter
                {
                    Id = "ch4",
                    TitleZh = "会有东西记得你",
                    TitleEn = "Something Will Remember You",
                    TextZh = "笔记写到有机物这几页明显不耐烦了，只剩结构式，步骤一个字都没有。\n"
                        + "你第一次合成出有机物时，通风橱的风停了一秒，又转起来——像这地方憋了一口气。\n"
                        + "页边有一行小字：「他们造出来的东西会记得自己是谁，所以别让他们回收你。」",
                    TextEn = "The organic pages are visibly impatient. Only structures are left; not one step is written down.\n"
                        + "The first time you synthesize an organic compound, the fume hood pauses for a second and then starts again, as if the place were holding its breath.\n"
                        + "A line in the margin: 'What they make remembers who it is. So don't let them recycle you.'",
                    HintZh = "合成第一种有机物。",
                    HintEn = "Synthesize your first organic compound.",
                    Reward = 200,
                },
                new StoryChapter
                {
                    Id = "ch5",
                    TitleZh = "第三格",
                    TitleEn = "The Third Drawer",
                    TextZh = "通风橱的第三格没锁，里面只有一张防静电垫和一副烧焦的手套。\n"
                        + "你把第一份危险品放进托盘时，头顶的旧灯闪了两下，整间地下室安静得像在等一个答案。\n"
                        + "这一页的笔记比哪里都工整：危险品不是货，是筹码。落款下面按着一枚指纹，指纹底下还有一个化学公司的缩写。",
                    TextEn = "The third drawer of the fume hood isn't locked. Inside there is nothing but an anti-static mat and a pair of scorched gloves.\n"
                        + "When your first hazardous batch lands on the tray, the old lamp overhead flickers twice, and the whole basement goes quiet as if waiting for an answer.\n"
                        + "The notes on this page are neater than anywhere else: dangerous goods aren't merchandise, they're leverage. A thumbprint sits under the signature, and beneath the thumbprint, a chemical company's initials.",
                    HintZh = "合成第一种危险品（TNT / 硝酸甘油 / 叠氮化钠）。",
                    HintEn = "Synthesize your first hazardous compound (TNT, nitroglycerin or sodium azide).",
                    Reward = 250,
                },
                new StoryChapter
                {
                    Id = "ch6",
                    TitleZh = "图鉴开始有分量",
                    TitleEn = "The Compendium Gains Weight",
                    TextZh = "五十种化合物，从水到硫酸铜，档案抽屉被你的记录撑得合不上了。\n"
                        + "一本旧册子从笔记里滑出来，夹着一张当铺的老照片：柜台后站着个穿实验服的年轻人，背后墙上钉着一块牌子——「深空化学公司 · 认证供应商」。\n"
                        + "照片背面只有一行字：「当铺是门面，地下室才是真的。」",
                    TextEn = "Fifty compounds, from water to copper sulfate, and the archive drawer no longer shuts around your records.\n"
                        + "An old booklet slides out of the notebook with a photograph tucked inside: behind the counter stands a young man in a lab coat, and on the wall behind him a plaque reads 'Deep Space Chemical Co. — Certified Supplier'.\n"
                        + "On the back, one line: 'The pawnshop is the front. The basement is the real business.'",
                    HintZh = "合成 50 种不同的化合物。",
                    HintEn = "Synthesize 50 different compounds.",
                    Reward = 300,
                },
                new StoryChapter
                {
                    Id = "ch7",
                    TitleZh = "他们在看你的档案",
                    TitleEn = "They Are Reading Your File",
                    TextZh = "一百五十种化合物。你开始用笔记里那套只有前任看得懂的缩写整理库存。\n"
                        + "昨晚离店时，街角停着一辆没有牌照的黑色货车，车厢侧面喷着和照片里一样的标志。\n"
                        + "今天地下室的留言板上多了一行不属于你的字迹：「还差一半。别停。」",
                    TextEn = "One hundred and fifty compounds. You've started sorting stock with the abbreviations only your predecessor could read.\n"
                        + "As you left the shop last night, an unplated black truck idled on the corner, wearing the same mark as the plaque in the photograph.\n"
                        + "This morning the basement board carries a line in handwriting that isn't yours: 'Halfway there. Don't stop.'",
                    HintZh = "合成 150 种不同的化合物。",
                    HintEn = "Synthesize 150 different compounds.",
                    Reward = 500,
                },
                new StoryChapter
                {
                    Id = "ch8",
                    TitleZh = "地下室以后归你管",
                    TitleEn = "The Basement Is Yours",
                    TextZh = "四百种化合物，档案抽屉快被记录撑爆了——这间实验室的家底已经铺满整张柜台。\n"
                        + "笔记最后一页终于把话说完了：「读到这里的人，说明我已经被回收了。深空化学公司不造货，它造的是配方的主人。」\n"
                        + "信封里滑出一枚磁卡，编号 DSC-07，背面写着一个还没开张的地址。\n"
                        + "地下室以后归你管——公司那边，该有人去敲一敲了。",
                    TextEn = "Four hundred compounds and the archive is bursting at the seams. This lab's estate has filled the whole counter.\n"
                        + "The last page of the notebook finally finishes its sentence: 'If you are reading this, I have been recycled. Deep Space Chemical does not make goods. It makes the people who own the recipes.'\n"
                        + "A key card slips out of the envelope, numbered DSC-07, with an address on the back that hasn't opened for business yet.\n"
                        + "The basement is yours now. As for the company — somebody ought to go knock.",
                    HintZh = "合成 400 种不同的化合物。",
                    HintEn = "Synthesize 400 different compounds.",
                    Reward = 800,
                },
            };
        }
    }
}
