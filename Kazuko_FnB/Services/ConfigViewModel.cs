using System;
using System.Collections.Generic;
using Kazuko_FnB.Data;

namespace Kazuko_FnB.Services
{
    /// <summary>
    /// Trạng thái màn "Cấu hình toàn hệ thống": nhóm, khóa hiển thị, giá trị gốc và giá trị đang sửa.
    /// Màn hình chỉ đọc/ghi qua lớp này (không có SQL trong UI). Lưu = một transaction qua ConfigRepository.
    /// </summary>
    public sealed class ConfigViewModel
    {
        // Hai khóa thuộc giao diện cũ (Office Blue/Silver/Black, độ to chữ): bản mới dùng ThemeManager + DPI.
        private static readonly HashSet<string> NotShown = new HashSet<string>(StringComparer.Ordinal)
        {
            "GiaoDienChuongTrinh", "DoToChuHienThi"
        };

        private readonly ConfigRepository _repo;
        private readonly Dictionary<string, object> _original = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, object> _current = new Dictionary<string, object>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<ConfigItem>> _byGroup = new Dictionary<string, List<ConfigItem>>();
        private readonly Dictionary<string, ConfigItem> _byName = new Dictionary<string, ConfigItem>(StringComparer.Ordinal);

        public List<ConfigGroup> Groups { get; private set; }
        public List<ConfigItem> AllItems { get; private set; }

        public ConfigViewModel(string databasePath)
        {
            _repo = new ConfigRepository(databasePath);
            Reload();
        }

        public void Reload()
        {
            _original.Clear();
            _current.Clear();
            _byGroup.Clear();
            _byName.Clear();
            Groups = _repo.LoadVisibleGroups();
            var visible = new HashSet<string>();
            foreach (ConfigGroup g in Groups)
            {
                visible.Add(g.Id);
                _byGroup[g.Id] = new List<ConfigItem>();
            }
            AllItems = new List<ConfigItem>();
            foreach (ConfigItem it in _repo.LoadItems())
            {
                if (!visible.Contains(it.GroupId) || NotShown.Contains(it.Name))
                    continue;
                // SORTORDER chung nhóm: ô phụ (Tab 30) nằm ngay sau ô cha, giữ nguyên thứ tự từ DB.
                _byGroup[it.GroupId].Add(it);
                _byName[it.Name] = it;
                AllItems.Add(it);
                object v = ReadValue(it);
                _original[it.Name] = v;
                _current[it.Name] = v;
            }
            // Bỏ nhóm không còn khóa nào để hiển thị.
            Groups.RemoveAll(g => _byGroup[g.Id].Count == 0);
        }

        private object ReadValue(ConfigItem it)
        {
            switch (it.Control)
            {
                case ConfigControl.Check:
                    return it.IntValue == SystemConfig.On;
                case ConfigControl.Number:
                    return (decimal)(it.DecimalValue != 0m ? it.DecimalValue : it.IntValue);
                case ConfigControl.Combo:
                    return it.IntValue;
                case ConfigControl.Image:
                    return null; // nạp theo yêu cầu (LoadImage) vì là blob
                default:
                    return it.TextValue ?? "";
            }
        }

        public List<ConfigItem> ItemsOf(ConfigGroup group)
        {
            return _byGroup[group.Id];
        }

        /// <summary>Khóa có nhãn chứa <paramref name="text"/> (không phân biệt hoa thường), mọi nhóm.</summary>
        public List<ConfigItem> Search(string text)
        {
            var res = new List<ConfigItem>();
            if (string.IsNullOrWhiteSpace(text))
                return res;
            string q = text.Trim().ToLowerInvariant();
            foreach (ConfigItem it in AllItems)
                if (it.Caption != null && it.Caption.ToLowerInvariant().Contains(q))
                    res.Add(it);
            return res;
        }

        public object Get(string name)
        {
            object v;
            return _current.TryGetValue(name, out v) ? v : null;
        }

        public void Set(string name, object value)
        {
            _current[name] = value;
            if (_byName.ContainsKey(name) && _byName[name].Control == ConfigControl.Image)
                _imageDirty.Add(name);
        }

        private readonly HashSet<string> _imageDirty = new HashSet<string>(StringComparer.Ordinal);

        public bool IsDirty
        {
            get { return Dirty().Count > 0; }
        }

        private List<string> Dirty()
        {
            var names = new List<string>();
            foreach (ConfigItem it in AllItems)
            {
                if (it.Control == ConfigControl.Image)
                {
                    if (_imageDirty.Contains(it.Name))
                        names.Add(it.Name);
                    continue;
                }
                if (!Equals(_original[it.Name], _current[it.Name]))
                    names.Add(it.Name);
            }
            return names;
        }

        public byte[] LoadImage(ConfigItem it)
        {
            if (_imageDirty.Contains(it.Name))
                return _current[it.Name] as byte[];
            return _repo.LoadBlob(it.Name);
        }

        public List<KeyValuePair<string, string>> LookupOf(ConfigItem it)
        {
            return _repo.LoadLookup(it.RefTableId, it.OtherConfig);
        }

        /// <summary>Ghi các khóa đã đổi. Trả về số khóa ghi được; sau đó nạp lại SystemConfig.</summary>
        public int Save(string userId)
        {
            var changes = new List<ConfigChange>();
            foreach (string name in Dirty())
            {
                ConfigItem it = _byName[name];
                object v = _current[name];
                var c = new ConfigChange { Name = name, DataType = it.DataType };
                switch (it.Control)
                {
                    case ConfigControl.Check:
                        c.IntValue = (bool)v ? SystemConfig.On : 0;
                        break;
                    case ConfigControl.Number:
                        c.IntValue = Convert.ToInt32((decimal)v);
                        break;
                    case ConfigControl.Combo:
                        c.IntValue = (int)v;
                        break;
                    case ConfigControl.Image:
                        c.IsBlob = true;
                        c.BlobValue = v as byte[];
                        break;
                    default:
                        c.DataType = 1;
                        c.TextValue = (string)v ?? "";
                        break;
                }
                changes.Add(c);
            }
            int n = _repo.Save(changes, userId);
            SystemConfig.Reload();
            Reload();
            _imageDirty.Clear();
            return n;
        }
    }
}
