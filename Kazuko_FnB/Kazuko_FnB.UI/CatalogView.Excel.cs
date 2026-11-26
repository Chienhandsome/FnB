using System.Windows.Forms;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Printing;

namespace Kazuko_FnB.UI
{
    /// <summary>Phần xuất Excel lưới đang hiện của <see cref="CatalogView"/> và hook kiểm thử.</summary>
    internal sealed partial class CatalogView
    {
        internal DataGridView GridForTest { get { return _grid; } }
        internal FlatButton ExcelExportButtonForTest { get { return _btnExcelExport; } }
        internal string LastExportMessageForTest { get; private set; }
        internal IGridExportOutput GridExportOutputForTest { get; set; }

        internal bool ExportGridToExcel()
        {
            return GridExcelExporter.Export(
                this,
                _grid,
                "",
                GridExportOutputForTest,
                delegate (string msg) { LastExportMessageForTest = msg; });
        }
    }
}
