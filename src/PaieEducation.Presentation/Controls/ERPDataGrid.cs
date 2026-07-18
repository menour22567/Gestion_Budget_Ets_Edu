using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PaieEducation.Presentation.Controls;

public class ERPDataGrid : DataGrid
{
    static ERPDataGrid()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(ERPDataGrid), new FrameworkPropertyMetadata(typeof(ERPDataGrid)));
    }

    public ERPDataGrid()
    {
        AutoGenerateColumns = false;
        IsReadOnly = true;
        HeadersVisibility = DataGridHeadersVisibility.Column;
        AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xF5));
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        RowHeaderWidth = 0;
        CanUserSortColumns = true;
        CanUserReorderColumns = false;
        CanUserResizeColumns = true;
        SelectionMode = DataGridSelectionMode.Single;
        SelectionUnit = DataGridSelectionUnit.FullRow;
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
    }
}
