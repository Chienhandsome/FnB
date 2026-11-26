using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Kazuko_FnB.Domain;
using Kazuko_FnB.Services;
using Kazuko_FnB.UI.Controls;
using Kazuko_FnB.UI.Dialogs;

namespace Kazuko_FnB.UI
{
    internal sealed partial class ServiceOrderView
    {
        private readonly FlatButton _btnTransferItem = new FlatButton { Text = "Chuyển món" };
        private readonly FlatButton _btnCancelBill = new FlatButton { Text = "Hủy bill", Tag = "danger" };

        internal FlatButton MergeTableButtonForTest { get { return _btnMergeTable; } }
        internal FlatButton TransferItemButtonForTest { get { return _btnTransferItem; } }
        internal FlatButton CancelBillButtonForTest { get { return _btnCancelBill; } }
        internal FlatButton ReprintButtonForTest { get { return _btnReprint; } }
        internal TableTransferResult LastTransferResultForTest { get; private set; }
        internal Func<ServiceTableInfo> TargetTableOverrideForTest { get; set; }
        internal Func<decimal?> TransferQtyOverrideForTest { get; set; }
        internal Func<string> CancelReasonOverrideForTest { get; set; }

        private void InitTransferControls(float s)
        {
            int btnH = Math.Max((int)(44 * s), 44);
            SetupBarButton(_btnTransferItem, (int)(98 * s), btnH, s);
            _lineBar.Controls.Add(_btnTransferItem);

            FlowLayoutPanel actionFlow = _actionCol.Controls.Count > 0 ? _actionCol.Controls[0] as FlowLayoutPanel : null;
            if (actionFlow != null)
            {
                int innerW = _actionCol.Width - (int)(12 * s);
                AddActionBtn(actionFlow, _btnCancelBill, innerW, btnH, s);
            }
        }

        private void DoMoveTable()
        {
            if (_currentBill == null || _currentTable == null)
            {
                ShowWarning(ServiceOrderConstants.SelectTableFirst);
                return;
            }
            if (_currentBill.PaidFlag != 0)
            {
                ShowWarning(ServiceTransferConstants.TableAlreadyPaidBlocked);
                return;
            }
            if (_currentBill.TempPrintCount > 0 && !SystemConfig.ChoPhepThayDoiHoaDonSauKhiInTamTinh)
            {
                ShowWarning(ServiceTransferConstants.TableTempPrintedBlocked);
                return;
            }

            UserSession authSession = ResolvePermissionSession(ServiceOrderConstants.FuncChuyenBan, "Chuyển bàn");
            if (authSession == null)
                return;

            ServiceTableInfo target = PickTargetTable(TableTransferMode.MoveTable, null);
            if (target == null)
                return;

            if (!SuppressPopup && !Msg.Confirm(this, ServiceTransferConstants.MoveTableConfirm(_currentTable.Name, target.Name)))
                return;

            TableTransferResult res = _service.MoveTable(new MoveTableRequest
            {
                SourceBillId = _currentBill.Id,
                SourceTableId = _currentTable.Id,
                TargetTableId = target.Id
            }, authSession);
            LastTransferResultForTest = res;

            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                return;
            }

            RefreshAfterTableSwitch(target.Id, res.TargetBill);
        }

        private void DoMergeTable()
        {
            if (_currentBill == null || _currentTable == null)
            {
                ShowWarning(ServiceOrderConstants.SelectTableFirst);
                return;
            }
            if (_currentBill.PaidFlag != 0)
            {
                ShowWarning(ServiceTransferConstants.TableAlreadyPaidBlocked);
                return;
            }
            if (_currentBill.TempPrintCount > 0 && !SystemConfig.ChoPhepThayDoiHoaDonSauKhiInTamTinh)
            {
                ShowWarning(ServiceTransferConstants.TableTempPrintedBlocked);
                return;
            }

            UserSession authSession = ResolvePermissionSession(ServiceOrderConstants.FuncGopBan, "Gộp bàn");
            if (authSession == null)
                return;

            ServiceTableInfo target = PickTargetTable(TableTransferMode.MergeTable, null);
            if (target == null)
                return;

            if (!SuppressPopup && !Msg.Confirm(this, ServiceTransferConstants.MergeTableConfirm(_currentTable.Name, target.Name)))
                return;

            TableTransferResult res = _service.MergeTable(new MergeTableRequest
            {
                SourceBillId = _currentBill.Id,
                SourceTableId = _currentTable.Id,
                TargetTableId = target.Id
            }, authSession);
            LastTransferResultForTest = res;

            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                return;
            }

            RefreshAfterTableSwitch(target.Id, res.TargetBill);
        }

        private void DoTransferItem()
        {
            if (_currentBill == null || _currentTable == null)
            {
                ShowWarning(ServiceOrderConstants.SelectTableFirst);
                return;
            }
            ServiceOrderLine line = SelectedParentLine();
            if (line == null)
            {
                ShowWarning(ServiceTransferConstants.TransferItemSelectLine);
                return;
            }
            if (_currentBill.TempPrintCount > 0 && !SystemConfig.ChoPhepThayDoiHoaDonSauKhiInTamTinh)
            {
                ShowWarning(ServiceTransferConstants.TableTempPrintedBlocked);
                return;
            }

            UserSession authSession = ResolvePermissionSession(ServiceOrderConstants.FuncChuyenMatHang, "Chuyển mặt hàng");
            if (authSession == null)
                return;

            ServiceTableInfo target;
            decimal? transferQty;
            if (!PickTargetForItemTransfer(line, out target, out transferQty))
                return;

            TableTransferResult res = _service.TransferItem(new TransferItemRequest
            {
                SourceBillId = _currentBill.Id,
                SourceTableId = _currentTable.Id,
                TargetTableId = target.Id,
                LineId = line.Id,
                TransferQty = transferQty
            }, authSession);
            LastTransferResultForTest = res;

            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                return;
            }

            _currentBill = res.SourceBill;
            _tableMap.ReloadAll();
            if (_currentTable != null)
                _tableMap.SelectTableVisual(_currentTable.Id);
            RenderCurrentBill();
        }

        private void DoCancelBill()
        {
            if (_currentBill == null || _currentTable == null)
            {
                ShowWarning(ServiceOrderConstants.SelectTableFirst);
                return;
            }

            UserSession authSession = ResolvePermissionSession(ServiceOrderConstants.FuncHuyHoaDon, "Hủy hóa đơn");
            if (authSession == null)
                return;

            string reason = "";
            if (CancelReasonOverrideForTest != null)
            {
                reason = CancelReasonOverrideForTest() ?? "";
            }
            else if (!SuppressPopup)
            {
                if (SystemConfig.NhapLyDoKhiHuyHoaDon)
                {
                    reason = PromptDialog.Ask(this, "LÝ DO HỦY HÓA ĐƠN",
                        "Nhập lý do hủy hóa đơn trên bàn '" + _currentTable.Name + "':", "");
                    if (string.IsNullOrWhiteSpace(reason))
                        return;
                }
                else
                {
                    if (!Msg.Confirm(this, ServiceTransferConstants.CancelTableConfirm(_currentTable.Name)))
                        return;
                    reason = ServiceTransferConstants.CancelDefaultReason;
                }
            }

            TableTransferResult res = _service.CancelBill(new CancelBillRequest
            {
                BillId = _currentBill.Id,
                TableId = _currentTable.Id,
                Reason = reason
            }, authSession);
            LastTransferResultForTest = res;

            if (!res.Ok)
            {
                ShowWarning(res.ErrorMessage);
                return;
            }

            _lockRenewTimer.Stop();
            _currentBill = null;
            _tableMap.ReloadAll();
            if (_currentTable != null)
            {
                _currentTable = _tableMap.FindTable(_currentTable.Id);
                if (_currentTable != null)
                    _tableMap.SelectTableVisual(_currentTable.Id);
            }
            RenderCurrentBill();
        }

        private UserSession ResolvePermissionSession(string functionId, string actionTitle)
        {
            if (TableService.HasFunction(_session, functionId))
                return _session;
            if (SuppressPopup)
            {
                ShowWarning(ServiceOrderConstants.NoPermission);
                return null;
            }
            using (var dlg = new ServiceReLoginDialog(_session.DatabasePath, functionId, actionTitle))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && dlg.VerifiedSession != null)
                    return dlg.VerifiedSession;
            }
            return null;
        }

        private ServiceTableInfo PickTargetTable(TableTransferMode mode, ServiceOrderLine line)
        {
            if (TargetTableOverrideForTest != null)
                return TargetTableOverrideForTest();
            if (SuppressPopup)
                return null;

            List<ServiceAreaInfo> areas = _service.LoadAreas();
            List<ServiceTableInfo> tables = _service.LoadTables();
            using (var dlg = new MoveMergeTableDialog(mode, _currentTable.Id, _currentTable.Name, areas, tables, line))
            {
                return dlg.ShowDialog(this) == DialogResult.OK ? dlg.SelectedTargetTable : null;
            }
        }

        private bool PickTargetForItemTransfer(ServiceOrderLine line, out ServiceTableInfo target, out decimal? qty)
        {
            target = null;
            qty = null;
            if (TargetTableOverrideForTest != null)
            {
                target = TargetTableOverrideForTest();
                qty = TransferQtyOverrideForTest != null ? TransferQtyOverrideForTest() : null;
                return target != null;
            }
            if (SuppressPopup)
                return false;

            List<ServiceAreaInfo> areas = _service.LoadAreas();
            List<ServiceTableInfo> tables = _service.LoadTables();
            using (var dlg = new MoveMergeTableDialog(TableTransferMode.TransferItem, _currentTable.Id, _currentTable.Name, areas, tables, line))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.SelectedTargetTable == null)
                    return false;
                target = dlg.SelectedTargetTable;
                qty = dlg.TransferQty;
                return true;
            }
        }

        private void RefreshAfterTableSwitch(string targetTableId, ServiceOrderHeader targetBill)
        {
            _tableMap.ReloadAll();
            _currentTable = _tableMap.FindTable(targetTableId);
            _currentBill = targetBill;
            if (_currentTable != null)
            {
                _tableMap.SelectTableVisual(_currentTable.Id);
                _menuPicker.SetPriceLevel(_currentTable.AreaPriceLevelId);
            }
            if (_currentBill != null && SystemConfig.HeThongChayNhieuMayTram)
                _lockRenewTimer.Start();
            else
                _lockRenewTimer.Stop();
            RenderCurrentBill();
        }
    }
}
