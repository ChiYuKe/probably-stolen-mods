// 此文件由 tools/gen_compounds.py 从 data/tools.csv 生成，不要手改。
namespace Chemistry.Data
{
    /// <summary>
    /// 化学工具表（32 件，顺序与 CATALOG.md 第 4 节一致）。
    /// 数据源：data/tools.csv；由 tools/gen_compounds.py 生成，不要手改。
    /// </summary>
    internal static class ToolTable
    {
        public static readonly ToolDef[] All =
        {
            new ToolDef { Key = "beaker", NameZh = "烧杯", NameEn = "Beaker", Category = "glassware", Value = 60, DescZh = "最基础的玻璃器皿，配溶液、隔水加热都靠它，柜台上摆一摞永远不嫌多。", DescEn = "The humblest glass on the shelf. Mixing and water baths both start here." },
            new ToolDef { Key = "conical_flask", NameZh = "锥形瓶", NameEn = "Conical flask", Category = "glassware", Value = 70, DescZh = "底宽口窄摇一摇不洒，当铺收来的样品多半装在里面。", DescEn = "Wide base and narrow neck. Most samples that come through the pawnshop end up in one." },
            new ToolDef { Key = "round_flask", NameZh = "圆底烧瓶", NameEn = "Round-bottom flask", Category = "glassware", Value = 80, DescZh = "圆肚子受热均匀，煮沸和回流的老伙计，缺它很多反应做不成。", DescEn = "A round belly for even heating. The old hand for boiling and reflux." },
            new ToolDef { Key = "volumetric_flask", NameZh = "容量瓶", NameEn = "Volumetric flask", Category = "glassware", Value = 90, DescZh = "一条刻度定生死，配标准溶液少了它只能凭眼估，价钱也就贵在这条线上。", DescEn = "One line decides everything. No standard solution gets mixed without it." },
            new ToolDef { Key = "test_tube", NameZh = "试管", NameEn = "Test tube", Category = "glassware", Value = 30, DescZh = "一支小管子随手就能试出这瓶货是真是假，摔了也不心疼。", DescEn = "A little tube that tells you in seconds whether the goods are real." },
            new ToolDef { Key = "test_tube_rack", NameZh = "试管架", NameEn = "Test tube rack", Category = "glassware", Value = 45, DescZh = "木架一排孔，试管插上去，柜台立刻有作坊的样子。", DescEn = "A row of holes in wood. Pop the tubes in and the counter looks like a workshop." },
            new ToolDef { Key = "graduated_cylinder", NameZh = "量筒", NameEn = "Graduated cylinder", Category = "glassware", Value = 65, DescZh = "细长身子一排刻度，量体积比拿眼睛估靠谱得多。", DescEn = "A tall body full of marks. Far more reliable than eyeballing a volume." },
            new ToolDef { Key = "pipette", NameZh = "移液管", NameEn = "Pipette", Category = "glassware", Value = 55, DescZh = "一滴也算数，贵重试剂全靠它省着用。", DescEn = "Every drop counts. Fine reagents are rationed with this." },
            new ToolDef { Key = "burette", NameZh = "滴定管", NameEn = "Burette", Category = "glassware", Value = 120, DescZh = "一滴一滴往下放，终点到了就该收手，测含量的活儿离不开它。", DescEn = "Drip by drip until the endpoint says stop. No content test is done without it." },
            new ToolDef { Key = "petri_dish", NameZh = "培养皿", NameEn = "Petri dish", Category = "glassware", Value = 35, DescZh = "浅浅一碟养点什么都行，也常拿来晾干样品和分装粉末。", DescEn = "A shallow dish that grows anything and doubles as a drying tray." },
            new ToolDef { Key = "glass_rod", NameZh = "玻璃棒", NameEn = "Glass rod", Category = "glassware", Value = 20, DescZh = "不值钱的搅棍，可没有它连盐都化不开。", DescEn = "A cheap stirring stick. Without it even salt will not dissolve." },
            new ToolDef { Key = "watch_glass", NameZh = "表面皿", NameEn = "Watch glass", Category = "glassware", Value = 25, DescZh = "盖住烧杯挡灰，也能托着粉末上秤，一物两用的小件。", DescEn = "Covers a beaker against dust and carries powder to the scale." },
            new ToolDef { Key = "bunsen_burner", NameZh = "本生灯", NameEn = "Bunsen burner", Category = "heating", Value = 100, DescZh = "拧开就是蓝火苗，当铺里所有加热活儿的第一把火。", DescEn = "One twist and there is a blue flame. The first fire of any heating job." },
            new ToolDef { Key = "heating_mantle", NameZh = "加热套", NameEn = "Heating mantle", Category = "heating", Value = 160, DescZh = "把烧瓶裹进怀里慢慢煨，比明火温柔也更安全。", DescEn = "Wraps the flask and warms it slowly. Gentler and safer than open flame." },
            new ToolDef { Key = "crucible", NameZh = "坩埚", NameEn = "Crucible", Category = "heating", Value = 90, DescZh = "瓷土小碗耐得住烈火，烧完往往只剩一坨值钱的货。", DescEn = "A clay bowl that survives the fire and leaves something worth selling." },
            new ToolDef { Key = "mortar_pestle", NameZh = "研钵", NameEn = "Mortar and pestle", Category = "misc", Value = 40, DescZh = "捣碎结块的货，磨得越细卖相越好。", DescEn = "Grinds the clumped goods fine. The finer the powder the better it sells." },
            new ToolDef { Key = "balance", NameZh = "分析天平", NameEn = "Analytical balance", Category = "measurement", Value = 180, DescZh = "差一毫都不行，称贵金属和细粉全靠它，镇得住场子。", DescEn = "Not a hair off. Precious metal and fine powder are weighed here." },
            new ToolDef { Key = "thermometer", NameZh = "温度计", NameEn = "Thermometer", Category = "measurement", Value = 50, DescZh = "火候全靠这根水银柱说话，差几度就是废品和好货的分界。", DescEn = "The mercury column has the final say on the heat." },
            new ToolDef { Key = "ph_meter", NameZh = "pH计", NameEn = "pH meter", Category = "measurement", Value = 150, DescZh = "一插就知道酸还是碱，省下用舌头试的危险活儿。", DescEn = "One dip tells acid from base and saves your tongue the danger." },
            new ToolDef { Key = "spectrometer", NameZh = "光谱仪", NameEn = "Spectrometer", Category = "measurement", Value = 420, DescZh = "镇店的高价货，扫一眼就报出成分，值不值全看它说话。", DescEn = "The expensive piece in the shop. One scan names the contents." },
            new ToolDef { Key = "chromatograph", NameZh = "色谱仪", NameEn = "Chromatograph", Category = "measurement", Value = 380, DescZh = "把混在一起的货色一层层分开，谁也骗不过它。", DescEn = "Separates a mixed batch layer by layer. Nobody cheats this one." },
            new ToolDef { Key = "centrifuge", NameZh = "离心机", NameEn = "Centrifuge", Category = "separation", Value = 300, DescZh = "转起来就把重的沉的甩到底，上层清液卖相干净。", DescEn = "Spin it and the heavy parts sink. The clear liquid sells clean." },
            new ToolDef { Key = "distiller", NameZh = "蒸馏器", NameEn = "Distiller", Category = "separation", Value = 260, DescZh = "一锅浑汤进去，出来就是能卖钱的纯货，提纯的看家本事。", DescEn = "Murky brew goes in and pure goods worth money come out." },
            new ToolDef { Key = "separating_funnel", NameZh = "分液漏斗", NameEn = "Separating funnel", Category = "separation", Value = 85, DescZh = "油是油水是水，一拧开关各走各路。", DescEn = "Oil is oil and water is water. One turn and each takes its path." },
            new ToolDef { Key = "filter_funnel", NameZh = "漏斗", NameEn = "Filter funnel", Category = "separation", Value = 40, DescZh = "垫张滤纸渣子留下清液进瓶，最便宜的一道提纯。", DescEn = "A paper cone in it. Grit stays behind and clear liquid goes in the bottle." },
            new ToolDef { Key = "microscope", NameZh = "显微镜", NameEn = "Microscope", Category = "measurement", Value = 320, DescZh = "凑近看一眼假货的棱角就藏不住了，验货的硬家伙。", DescEn = "Take a close look and the fake's rough edges show." },
            new ToolDef { Key = "fume_hood", NameZh = "通风橱", NameEn = "Fume hood", Category = "safety", Value = 280, DescZh = "有毒的烟气都往柜子里抽，人站在外面安心数钱。", DescEn = "The poison goes up the hood while you count money outside." },
            new ToolDef { Key = "hotplate_stirrer", NameZh = "加热磁力搅拌器", NameEn = "Hotplate stirrer", Category = "heating", Value = 220, DescZh = "边加热边转圈，一锅反应不用人守着，省下的是时间也是命。", DescEn = "Heats and spins at once. A reaction that needs no babysitter." },
            new ToolDef { Key = "autoclave", NameZh = "高压灭菌锅", NameEn = "Autoclave", Category = "misc", Value = 260, DescZh = "高压一闷什么活物都清干净，玻璃器皿也顺带消了毒。", DescEn = "Pressure kills everything alive and sterilizes the glass while it is at it." },
            new ToolDef { Key = "titration_set", NameZh = "滴定装置", NameEn = "Titration set", Category = "glassware", Value = 200, DescZh = "滴定管锥形瓶架子配齐，测含量一步到位，整套出手更值钱。", DescEn = "Burette plus flask plus stand. One setup measures the content." },
            new ToolDef { Key = "goggles", NameZh = "护目镜", NameEn = "Safety goggles", Category = "safety", Value = 30, DescZh = "护住一对招子，溅出来的酸可不讲道理。", DescEn = "Guards your eyes. Splashed acid does not negotiate." },
            new ToolDef { Key = "lab_coat", NameZh = "实验服", NameEn = "Lab coat", Category = "safety", Value = 45, DescZh = "白大褂一穿脏东西沾在布上，别拿新衣服去赌。", DescEn = "The white coat takes the stain so your shirt does not." },
        };
    }
}
