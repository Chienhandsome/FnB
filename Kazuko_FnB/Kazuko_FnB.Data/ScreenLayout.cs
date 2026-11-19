using System;
using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;
using Kazuko_FnB.Domain;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Đọc <c>SFORM.AELAYOUT</c> (chuỗi XML nhiều nút gốc <c>&lt;Object&gt;</c>) thành <see cref="ScreenLayout"/>.
    /// Chỉ đọc thông tin cần để dựng màn; control lạ vẫn được giữ lại với <see cref="LayoutKind.Unknown"/>.
    /// </summary>
    public static class LayoutParser
    {
        public static ScreenLayout Parse(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
                throw new ArgumentException("Layout rỗng.", "xml");

            XDocument doc = XDocument.Parse("<root>" + xml + "</root>");
            var layout = new ScreenLayout();
            foreach (XElement obj in doc.Root.Elements("Object"))
            {
                string type = (string)obj.Attribute("type") ?? "";
                string name = (string)obj.Attribute("name") ?? "";
                string shortType = ShortType(type);
                LayoutKind kind = KindOf(shortType);

                if (kind == LayoutKind.Mapper)
                {
                    ReadMapper(obj, layout);
                    continue;
                }

                var c = new LayoutControl { Name = name, TypeName = shortType, Kind = kind, Enabled = true };
                foreach (XElement p in obj.Elements("Property"))
                {
                    string pn = (string)p.Attribute("name");
                    if (pn == null)
                        continue;
                    if (pn == "Items")
                    {
                        foreach (XElement it in p.Elements("Item"))
                            c.Items.Add(it.Value);
                        continue;
                    }
                    if (p.HasElements)
                        continue;
                    c.Props[pn] = p.Value;
                }
                c.Text = c.Prop("Text") ?? "";
                int[] loc = Pair(c.Prop("Location"));
                int[] size = Pair(c.Prop("Size"));
                c.X = loc[0];
                c.Y = loc[1];
                c.Width = size[0];
                c.Height = size[1];
                int tab;
                int.TryParse(c.Prop("TabIndex"), NumberStyles.Integer, CultureInfo.InvariantCulture, out tab);
                c.TabIndex = tab;
                string enabled = c.Prop("Enabled");
                c.Enabled = !string.Equals(enabled, "False", StringComparison.OrdinalIgnoreCase);

                if (kind == LayoutKind.Container && layout.Width == 0 && type.StartsWith("System.Windows.Forms.UserControl", StringComparison.Ordinal))
                {
                    layout.Width = c.Width;
                    layout.Height = c.Height;
                }
                layout.Controls.Add(c);
            }
            return layout;
        }

        private static void ReadMapper(XElement mapper, ScreenLayout layout)
        {
            foreach (XElement p in mapper.Elements("Property"))
            {
                string pn = (string)p.Attribute("name");
                if (pn == "Table")
                    layout.Table = p.Value.Trim();
                else if (pn == "Maps")
                {
                    foreach (XElement item in p.Elements("Item"))
                    {
                        XElement ex = item.Element("ObjectEx");
                        if (ex == null)
                            continue;
                        var map = new FieldMap();
                        foreach (XElement mp in ex.Elements("Property"))
                        {
                            string mn = (string)mp.Attribute("name");
                            switch (mn)
                            {
                                case "DataField":
                                    map.DataField = mp.Value.Trim();
                                    break;
                                case "AllowEmpty":
                                    map.AllowEmpty = ParseBool(mp.Value);
                                    break;
                                case "AllowDuplicate":
                                    map.AllowDuplicate = ParseBool(mp.Value);
                                    break;
                                case "LabelControl":
                                    map.LabelControl = RefName(mp);
                                    break;
                                case "EditControl":
                                    map.EditControl = RefName(mp);
                                    break;
                            }
                        }
                        if (!string.IsNullOrEmpty(map.DataField))
                            layout.Maps.Add(map);
                    }
                }
            }
        }

        private static string RefName(XElement property)
        {
            XElement r = property.Element("Reference");
            return r == null ? null : (string)r.Attribute("name");
        }

        private static bool? ParseBool(string s)
        {
            if (string.Equals(s.Trim(), "True", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(s.Trim(), "False", StringComparison.OrdinalIgnoreCase)) return false;
            return null;
        }

        private static int[] Pair(string s)
        {
            var r = new[] { 0, 0 };
            if (string.IsNullOrWhiteSpace(s))
                return r;
            string[] p = s.Split(',');
            if (p.Length >= 2)
            {
                int.TryParse(p[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out r[0]);
                int.TryParse(p[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out r[1]);
            }
            return r;
        }

        /// <summary>"No1Lib.Sys.No1TextBox, No1Lib.Sys, Version=..." → "No1TextBox".</summary>
        private static string ShortType(string type)
        {
            int comma = type.IndexOf(',');
            string full = comma >= 0 ? type.Substring(0, comma) : type;
            int dot = full.LastIndexOf('.');
            return dot >= 0 ? full.Substring(dot + 1) : full;
        }

        public static LayoutKind KindOf(string shortType)
        {
            switch (shortType)
            {
                case "AeController": return LayoutKind.Mapper;
                case "No1Label":
                case "Label": return LayoutKind.Label;
                case "No1TextBox":
                case "TextBox": return LayoutKind.Text;
                case "No1NumericUpDown":
                case "NumericUpDown": return LayoutKind.Number;
                case "No1DatePicker":
                case "DateTimePicker": return LayoutKind.Date;
                case "No1ComboBox":
                case "ComboBox": return LayoutKind.Combo;
                case "No1LookupEdit":
                case "No1GridLookup": return LayoutKind.Lookup;
                case "No1CheckBox":
                case "CheckBox": return LayoutKind.Check;
                case "Button": return LayoutKind.Button;
                case "KryptonPanel":
                case "Panel":
                case "UserControl": return LayoutKind.Container;
                default: return LayoutKind.Unknown;
            }
        }
    }
}
