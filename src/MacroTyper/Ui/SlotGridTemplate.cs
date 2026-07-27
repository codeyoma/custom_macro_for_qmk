using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace MacroTyper.Ui;

/// <summary>
/// 슬롯 격자의 행·열을 바꾸려면 ItemsPanel 을 통째로 갈아 끼워야 한다.
/// ItemsPanelTemplate 안의 요소는 밖에서 이름으로 잡을 수 없고,
/// 항목이 만들어진 뒤에야 시각 트리에 나타나기 때문이다.
///
/// 크기 조합은 (4,6)과 (6,4) 둘뿐이라 만들어 두고 재사용한다.
/// </summary>
internal static class SlotGridTemplate
{
    private static readonly Dictionary<(int Rows, int Columns), ItemsPanelTemplate> Cache = [];

    public static ItemsPanelTemplate For(int rows, int columns)
    {
        if (Cache.TryGetValue((rows, columns), out ItemsPanelTemplate? cached))
            return cached;

        var factory = new FrameworkElementFactory(typeof(UniformGrid));
        factory.SetValue(UniformGrid.RowsProperty, rows);
        factory.SetValue(UniformGrid.ColumnsProperty, columns);

        var template = new ItemsPanelTemplate { VisualTree = factory };
        template.Seal();

        Cache[(rows, columns)] = template;
        return template;
    }
}
