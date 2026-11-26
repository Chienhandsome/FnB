using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Windows.Forms;
using Kazuko_FnB.Data;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Theme;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI.Printing
{
    /// <summary>
    /// Hộp "CHỌN MẪU IN" (docs/specs/in-xuat-excel/giao-dien.md mục A1, chốt Q8 không có Thiết kế mẫu in):
    /// danh sách mẫu sắp theo tên (chọn sẵn mẫu cuối), nhóm Tùy chọn 4 nút radio (mặc định Xem trên màn hình),
    /// ô chọn máy in chỉ bật khi chọn "In ra máy in", nút OK (Enter) và Cancel (Esc), nhấp đúp tên mẫu = OK.
    /// </summary>
    internal sealed class PrintTemplateDialog : ThemedDialog
    {
        private readonly Label _lblTemplates = new Label { AutoSize = false, Text = "Mẫu in", TextAlign = ContentAlignment.MiddleLeft };
        private readonly ListBox _lstTemplates = new ListBox
        {
            DrawMode = DrawMode.OwnerDrawFixed,
            IntegralHeight = false,
            BorderStyle = BorderStyle.FixedSingle
        };
        private readonly ComboBox _cboTemplates = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly FieldFrame _templateFrame;

        private readonly Panel _optionsPanel = new Panel { Tag = "surface" };
        private readonly Label _lblOptions = new Label { AutoSize = false, Text = "Tùy chọn", TextAlign = ContentAlignment.MiddleLeft };
        private readonly RadioButton _rbPreview = new RadioButton { Text = "Xem trên màn hình", Checked = true, AutoSize = false };
        private readonly RadioButton _rbPrint = new RadioButton { Text = "In ra máy in", Checked = false, AutoSize = false };
        private readonly RadioButton _rbExcel = new RadioButton { Text = "Xuất excel", Checked = false, AutoSize = false };
        private readonly RadioButton _rbPdf = new RadioButton { Text = "Xuất pdf", Checked = false, AutoSize = false };

        private readonly ComboBox _cboPrinter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat };
        private readonly FieldFrame _printerFrame;

        private readonly FlatButton _btnOk;
        private readonly FlatButton _btnCancel;
        private readonly Font _bodyFont;
        private readonly Font _headingFont;
        private readonly PrintDialogOptions _options;
        private readonly bool _useComboBox;

        private ThemeTokens _theme = ThemeManager.Current;

        public PrintSelectionResult Selection { get; private set; }

        public PrintTemplateDialog(IList<PrintTemplateRecord> templates)
            : this(templates, LoadInstalledPrinters(), GetDefaultWindowsPrinter(), null)
        {
        }

        public PrintTemplateDialog(IList<PrintTemplateRecord> templates, PrintDialogOptions options)
            : this(templates, LoadInstalledPrinters(), GetDefaultWindowsPrinter(), options)
        {
        }

        internal PrintTemplateDialog(
            IList<PrintTemplateRecord> templates,
            IList<string> installedPrinters,
            string defaultPrinterName)
            : this(templates, installedPrinters, defaultPrinterName, null)
        {
        }

        internal PrintTemplateDialog(
            IList<PrintTemplateRecord> templates,
            IList<string> installedPrinters,
            string defaultPrinterName,
            PrintDialogOptions options)
            : base(
                ResolveDialogTitle(options),
                ResolveDialogSubtitle(options),
                480,
                options != null && options.UseComboBox ? 378 : 460)
        {
            _options = options ?? PrintDialogOptions.ForFormSelectDialog();
            _useComboBox = _options.UseComboBox;

            float s = Dpi;
            _bodyFont = new Font(ThemeManager.BodyFontName, ThemeManager.BodyFontSize);
            _headingFont = new Font(ThemeManager.HeadingFontName, ThemeManager.BodyFontSize);
            _templateFrame = new FieldFrame(_cboTemplates, 0);
            _printerFrame = new FieldFrame(_cboPrinter, 0);

            string cancelText = string.IsNullOrWhiteSpace(_options.CancelButtonText) ? "Cancel" : _options.CancelButtonText;
            string okText = string.IsNullOrWhiteSpace(_options.OkButtonText) ? "OK" : _options.OkButtonText;

            _btnCancel = AddButton(cancelText, null, delegate
            {
                Selection = null;
                DialogResult = DialogResult.Cancel;
                Close();
            });
            _btnOk = AddButton(okText, "primary", delegate { AcceptSelection(); });
            AcceptButton = _btnOk;
            CancelButton = _btnCancel;

            Body.AutoScroll = false;
            FitBody(480, _useComboBox ? 262 : 344);

            BuildLayout(s);
            PopulateTemplates(templates);
            PopulatePrinters(installedPrinters, defaultPrinterName);
            UpdatePrinterControlState();

            Shown += delegate
            {
                if (_useComboBox)
                    _cboTemplates.Focus();
                else
                    _lstTemplates.Focus();
            };
        }

        private static string ResolveDialogTitle(PrintDialogOptions options)
        {
            return options == null || string.IsNullOrWhiteSpace(options.Title)
                ? PrintConstants.SelectTemplateDialogTitle
                : options.Title;
        }

        private static string ResolveDialogSubtitle(PrintDialogOptions options)
        {
            return options == null || string.IsNullOrWhiteSpace(options.Subtitle)
                ? "Chọn mẫu in và hình thức xuất báo cáo"
                : options.Subtitle;
        }

        private void BuildLayout(float s)
        {
            int padX = (int)(16 * s);
            int fullW = (int)(448 * s);

            _lblTemplates.Font = _headingFont;
            _lblTemplates.SetBounds(padX, (int)(12 * s), fullW, (int)(22 * s));

            if (_useComboBox)
            {
                _cboTemplates.Font = _bodyFont;
                _templateFrame.SetBounds(padX, (int)(38 * s), fullW, (int)(34 * s));
                _lstTemplates.Visible = false;
                _templateFrame.Visible = true;
            }
            else
            {
                _lstTemplates.Font = _bodyFont;
                _lstTemplates.ItemHeight = (int)(32 * s);
                _lstTemplates.SetBounds(padX, (int)(38 * s), fullW, (int)(116 * s));
                _lstTemplates.DrawItem += DrawTemplateItem;
                _lstTemplates.DoubleClick += delegate { AcceptSelection(); };
                _lstTemplates.Visible = true;
                _templateFrame.Visible = false;
            }

            int optionsTop = _useComboBox ? (int)(84 * s) : (int)(166 * s);
            _optionsPanel.SetBounds(padX, optionsTop, fullW, (int)(166 * s));
            _optionsPanel.Paint += DrawOptionsPanelBorder;

            int innerX = (int)(14 * s);
            int innerW = fullW - innerX * 2;
            int radioH = (int)(28 * s);

            _lblOptions.Font = _headingFont;
            _lblOptions.SetBounds(innerX, (int)(10 * s), innerW, (int)(22 * s));

            _rbPreview.Font = _bodyFont;
            _rbPreview.SetBounds(innerX, (int)(36 * s), innerW, radioH);

            _rbPrint.Font = _bodyFont;
            int printRadioW = Math.Max((int)(126 * s), TextRenderer.MeasureText(_rbPrint.Text, _bodyFont).Width + (int)(28 * s));
            _rbPrint.SetBounds(innerX, (int)(68 * s), printRadioW, radioH);

            _cboPrinter.Font = _bodyFont;
            int printerX = innerX + printRadioW + (int)(8 * s);
            int printerW = Math.Max((int)(160 * s), fullW - printerX - innerX);
            _printerFrame.SetBounds(printerX, (int)(66 * s), printerW, (int)(32 * s));

            _rbExcel.Font = _bodyFont;
            _rbExcel.SetBounds(innerX, (int)(102 * s), innerW, radioH);

            _rbPdf.Font = _bodyFont;
            _rbPdf.SetBounds(innerX, (int)(132 * s), innerW, radioH);

            EventHandler onOptionChanged = delegate { UpdatePrinterControlState(); };
            _rbPreview.CheckedChanged += onOptionChanged;
            _rbPrint.CheckedChanged += onOptionChanged;
            _rbExcel.CheckedChanged += onOptionChanged;
            _rbPdf.CheckedChanged += onOptionChanged;

            _optionsPanel.Controls.Add(_lblOptions);
            _optionsPanel.Controls.Add(_rbPreview);
            _optionsPanel.Controls.Add(_rbPrint);
            _optionsPanel.Controls.Add(_printerFrame);
            _optionsPanel.Controls.Add(_rbExcel);
            _optionsPanel.Controls.Add(_rbPdf);

            Body.Controls.Add(_lblTemplates);
            if (_useComboBox)
                Body.Controls.Add(_templateFrame);
            else
                Body.Controls.Add(_lstTemplates);
            Body.Controls.Add(_optionsPanel);
        }

        private void PopulateTemplates(IList<PrintTemplateRecord> templates)
        {
            int defaultIndex;
            List<PrintTemplateRecord> sorted = PrintTemplateOrdering.OrderAndFindDefault(templates, _options, out defaultIndex);

            _lstTemplates.Items.Clear();
            _cboTemplates.Items.Clear();
            foreach (PrintTemplateRecord t in sorted)
            {
                _lstTemplates.Items.Add(t);
                _cboTemplates.Items.Add(t);
            }

            if (defaultIndex >= 0 && defaultIndex < sorted.Count)
            {
                _lstTemplates.SelectedIndex = defaultIndex;
                _cboTemplates.SelectedIndex = defaultIndex;
            }
            _btnOk.Enabled = sorted.Count > 0;
        }

        private void PopulatePrinters(IList<string> installedPrinters, string defaultPrinterName)
        {
            _cboPrinter.Items.Clear();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (installedPrinters != null)
            {
                foreach (string name in installedPrinters)
                {
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    string clean = name.Trim();
                    if (seen.Add(clean))
                        _cboPrinter.Items.Add(clean);
                }
            }

            string def = string.IsNullOrWhiteSpace(defaultPrinterName) ? "" : defaultPrinterName.Trim();
            if (def.Length > 0 && seen.Add(def))
                _cboPrinter.Items.Add(def);

            int selectedIdx = -1;
            if (def.Length > 0)
            {
                for (int i = 0; i < _cboPrinter.Items.Count; i++)
                {
                    if (string.Equals(_cboPrinter.Items[i] as string, def, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedIdx = i;
                        break;
                    }
                }
            }
            if (selectedIdx < 0 && _cboPrinter.Items.Count > 0)
                selectedIdx = 0;
            if (selectedIdx >= 0)
                _cboPrinter.SelectedIndex = selectedIdx;
        }

        private void UpdatePrinterControlState()
        {
            bool enablePrinter = _rbPrint.Checked;
            _cboPrinter.Enabled = enablePrinter;
            _printerFrame.Enabled = enablePrinter;
            ApplyPrinterFrameTheme();
        }

        private void AcceptSelection()
        {
            PrintTemplateRecord selected = _useComboBox
                ? (_cboTemplates.SelectedItem as PrintTemplateRecord)
                : (_lstTemplates.SelectedItem as PrintTemplateRecord);
            if (selected == null)
                return;

            PrintActionKind action = PrintActionKind.Preview;
            if (_rbPrint.Checked)
                action = PrintActionKind.Print;
            else if (_rbExcel.Checked)
                action = PrintActionKind.ExportExcel;
            else if (_rbPdf.Checked)
                action = PrintActionKind.ExportPdf;

            Selection = new PrintSelectionResult
            {
                Template = selected,
                Action = action,
                PrinterName = action == PrintActionKind.Print ? (_cboPrinter.SelectedItem as string) : null
            };
            DialogResult = DialogResult.OK;
            Close();
        }

        private void DrawTemplateItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _lstTemplates.Items.Count)
                return;

            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color back = selected ? _theme.Primary : _theme.Surface;
            Color fore = selected ? _theme.OnPrimary : _theme.Text;

            using (var b = new SolidBrush(back))
                e.Graphics.FillRectangle(b, e.Bounds);

            int pad = (int)(10 * Dpi);
            Rectangle textRect = new Rectangle(e.Bounds.X + pad, e.Bounds.Y, Math.Max(0, e.Bounds.Width - pad * 2), e.Bounds.Height);
            string text = Convert.ToString(_lstTemplates.Items[e.Index]) ?? "";
            TextRenderer.DrawText(e.Graphics, text, _bodyFont, textRect, fore,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private void DrawOptionsPanelBorder(object sender, PaintEventArgs e)
        {
            using (var pen = new Pen(Draw.StrongBorder(_theme)))
                e.Graphics.DrawRectangle(pen, 0, 0, _optionsPanel.Width - 1, _optionsPanel.Height - 1);
        }

        protected override void ApplyDialogTheme()
        {
            base.ApplyDialogTheme();
            _theme = ThemeManager.Current;
            _lblTemplates.ForeColor = _theme.Primary;
            _lblOptions.ForeColor = _theme.Primary;
            _lstTemplates.BackColor = _theme.Surface;
            _lstTemplates.ForeColor = _theme.Text;
            if (_useComboBox)
            {
                _templateFrame.ApplyTheme(_theme, Body.BackColor);
                _templateFrame.Invalidate();
            }
            ApplyPrinterFrameTheme();
            _lstTemplates.Invalidate();
            _optionsPanel.Invalidate();
        }

        private void ApplyPrinterFrameTheme()
        {
            _printerFrame.ApplyTheme(_theme, _optionsPanel.BackColor);
            if (!_cboPrinter.Enabled)
            {
                Color mutedBg = Draw.Mix(_theme.Surface, _theme.Background, 0.55f);
                _printerFrame.FieldColor = mutedBg;
                _cboPrinter.BackColor = mutedBg;
                _cboPrinter.ForeColor = _theme.TextMuted;
            }
            _printerFrame.Invalidate();
        }

        private static List<string> LoadInstalledPrinters()
        {
            var list = new List<string>();
            try
            {
                foreach (string p in PrinterSettings.InstalledPrinters)
                {
                    if (!string.IsNullOrWhiteSpace(p))
                        list.Add(p);
                }
            }
            catch
            {
            }
            return list;
        }

        private static string GetDefaultWindowsPrinter()
        {
            try
            {
                var settings = new PrinterSettings();
                return settings.PrinterName ?? "";
            }
            catch
            {
                return "";
            }
        }

        /// <summary>Tạo hộp thoại mẫu để chụp bằng --render-screen preview.printtemplate.</summary>
        internal static PrintTemplateDialog CreateForPreview(UserSession session)
        {
            List<PrintTemplateRecord> list = null;
            if (session != null && !string.IsNullOrEmpty(session.DatabasePath))
            {
                try
                {
                    list = new PrintTemplateRepository(session.DatabasePath).LoadTemplatesForForm(PrintConstants.ItemSalesFormId);
                }
                catch
                {
                    list = null;
                }
            }
            if (list == null || list.Count == 0)
            {
                list = new List<PrintTemplateRecord>
                {
                    new PrintTemplateRecord { Id = "1", Name = "54mm" },
                    new PrintTemplateRecord { Id = "2", Name = "80mm" },
                    new PrintTemplateRecord { Id = "3", Name = "A4" }
                };
            }
            return new PrintTemplateDialog(list);
        }

        // ---- Thuộc tính nội bộ cho selftest ----
        internal ListBox TemplateListForTest { get { return _lstTemplates; } }
        internal ComboBox TemplateComboForTest { get { return _cboTemplates; } }
        internal RadioButton RadioPreviewForTest { get { return _rbPreview; } }
        internal RadioButton RadioPrintForTest { get { return _rbPrint; } }
        internal RadioButton RadioExcelForTest { get { return _rbExcel; } }
        internal RadioButton RadioPdfForTest { get { return _rbPdf; } }
        internal ComboBox PrinterComboForTest { get { return _cboPrinter; } }
        internal FlatButton OkButtonForTest { get { return _btnOk; } }
        internal FlatButton CancelButtonForTest { get { return _btnCancel; } }
        internal Panel BodyPanelForTest { get { return Body; } }
        internal Panel OptionsPanelForTest { get { return _optionsPanel; } }

        internal void TriggerTemplateDoubleClickForTest()
        {
            AcceptSelection();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _bodyFont.Dispose();
                _headingFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
