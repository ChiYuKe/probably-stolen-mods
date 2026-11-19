using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StorageExpansion
{
    internal static class WarehouseCells
    {
        internal const int AdditionalRows = 12;
        internal const int PricePerCell = 10;
        internal const string SaveKey = "warehouse_cells";
        private const int SavedColumns = 64;

        internal static int Id(int column, int extraRow) => extraRow * SavedColumns + column;

        internal static bool IsVisibleCell(int id, int width) =>
            id >= 0 && id < SavedColumns * AdditionalRows && id % SavedColumns < width;

        internal static HashSet<int> Read(string saved, int oldRows, int width)
        {
            var cells = new HashSet<int>();
            if (saved == null)
            {
                for (int row = 0; row < Math.Min(oldRows, AdditionalRows); row++)
                {
                    for (int column = 0; column < width; column++)
                    {
                        cells.Add(Id(column, row));
                    }
                }
                return cells;
            }

            foreach (string token in saved.Split(','))
            {
                if (int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int id) &&
                    IsVisibleCell(id, SavedColumns))
                {
                    cells.Add(id);
                }
            }
            return cells;
        }

        internal static string Write(HashSet<int> cells) =>
            string.Join(",", cells.OrderBy(id => id).Select(id => id.ToString(CultureInfo.InvariantCulture)));

        internal static GridShape BuildShape(int width, int originalRows, HashSet<int> unlocked)
        {
            var shape = new byte[width * (originalRows + AdditionalRows)];
            for (int row = 0; row < AdditionalRows; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    shape[(originalRows + row) * width + column] = unlocked.Contains(Id(column, row)) ? (byte)0 : (byte)1;
                }
            }
            return new GridShapeBuilder().SetData(shape, width).SetDataOutside(1).Build();
        }
    }
}
