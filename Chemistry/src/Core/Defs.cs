using System;
using System.Collections.Generic;

namespace Chemistry
{
    /// <summary>
    /// 一种元素单质。数据来自 src/Data/ElementTable.cs（按 data/elements.csv 生成）。
    /// </summary>
    internal sealed class ElementDef
    {
        /// <summary>原子序数 1..118。</summary>
        public int Number;

        /// <summary>元素符号，首字母大写，如 "H"、"He"。</summary>
        public string Symbol;

        public string NameZh;
        public string NameEn;

        /// <summary>分类 key，见 CATALOG.md（alkali_metal / nonmetal / noble_gas ...）。</summary>
        public string Category;

        /// <summary>相对原子质量。</summary>
        public double Mass;

        /// <summary>基础价值（信用点）。</summary>
        public int Value;

        /// <summary>周期表列，1..18；镧系/锕系填 0。</summary>
        public int Group;

        /// <summary>周期，1..7。</summary>
        public int Period;

        // ---- 物性（来自 data/sources/periodic_table.json，经 tools/fill_element_properties.py 合并）----

        /// <summary>常温状态：Gas / Liquid / Solid（可能为空）。</summary>
        public string Phase;

        /// <summary>气体或液体物品的默认温度（摄氏度）。</summary>
        public double TemperatureC = 20.0;

        /// <summary>熔点（摄氏度）；NaN 表示数据缺失。</summary>
        public double MeltC = double.NaN;

        /// <summary>沸点（摄氏度）；NaN 表示数据缺失。</summary>
        public double BoilC = double.NaN;

        /// <summary>密度（单位见 DensityUnit）；NaN 表示数据缺失。</summary>
        public double Density = double.NaN;

        /// <summary>密度单位：g/L 或 g/cm3。</summary>
        public string DensityUnit;

        /// <summary>鲍林电负性；NaN 表示数据缺失。</summary>
        public double Electronegativity = double.NaN;

        /// <summary>电子排布简式，如 "[He] 2s2 2p4"。</summary>
        public string ElectronConfig;

        /// <summary>元素分区：s / p / d / f。</summary>
        public string Block;

        /// <summary>发现者（可能为空）。</summary>
        public string DiscoveredBy;

        /// <summary>命名者（可能为空）。</summary>
        public string NamedBy;

        /// <summary>元素简介（中文），一两句用途/特点。</summary>
        public string SummaryZh;

        /// <summary>元素简介（英文）。</summary>
        public string SummaryEn;

        public string ItemId
        {
            get { return ChemistryCatalog.ElementItemId(Symbol); }
        }

        public string SpriteKey
        {
            get { return ChemistryCatalog.ElementSpriteKey(Symbol); }
        }
    }

    /// <summary>一件化学工具（烧杯、本生灯、离心机……）。</summary>
    internal sealed class ToolDef
    {
        public string Key;
        public string NameZh;
        public string NameEn;

        /// <summary>分类 key：glassware / heating / measurement / separation / safety / misc。</summary>
        public string Category;

        public int Value;
        public string DescZh;
        public string DescEn;

        public string ItemId
        {
            get { return ChemistryCatalog.ToolItemId(Key); }
        }

        public string SpriteKey
        {
            get { return ChemistryCatalog.ToolSpriteKey(Key); }
        }
    }

    /// <summary>
    /// 一种化合物。Inputs 由 FormulaParser 从 Formula 解析出来，
    /// 保证「化学式」和「实际消耗的单质」永远一致。
    /// </summary>
    internal sealed class CompoundDef
    {
        public string Key;

        /// <summary>真实化学式，如 "H2O"、"C9H8O4"、"Ca(OH)2"。</summary>
        public string Formula;

        public string NameZh;
        public string NameEn;

        /// <summary>分类 key：acid / base / salt / oxide / organic / gas / other。</summary>
        public string Category;

        public int Value;

        /// <summary>物品参考温度（摄氏度）；NaN 表示未指定。</summary>
        public double TemperatureC = double.NaN;

        /// <summary>合成需要的工具 key，空数组表示徒手可做。</summary>
        public string[] Tools;

        public string DescZh;
        public string DescEn;

        private Dictionary<string, int> inputs;

        /// <summary>元素符号 -> 需要的单位数（化学式左右的配比）。</summary>
        public Dictionary<string, int> Inputs
        {
            get
            {
                if (inputs == null)
                {
                    inputs = FormulaParser.Parse(Formula);
                }

                return inputs;
            }
        }

        public string ItemId
        {
            get { return ChemistryCatalog.CompoundItemId(Key); }
        }

        public string SpriteKey
        {
            get { return ChemistryCatalog.CompoundSpriteKey(Key); }
        }
    }
}
