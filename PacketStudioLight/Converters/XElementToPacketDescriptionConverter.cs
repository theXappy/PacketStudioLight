using System;
using System.Xml.Linq;
using System.Windows.Data;

namespace PacketStudioLight
{
    public class XElementToPacketDescriptionConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            XElement element = value as XElement;

            var shownameAttr = element?.Attribute(XName.Get("showname"));
            if (shownameAttr != null)
            {
                string desc = shownameAttr.Value;
                if (!String.IsNullOrEmpty(desc) && !desc.Contains(":"))
                {
                    // In this case 'show' holds the value
                    var showAttr = element?.Attribute(XName.Get("show"));
                    string val = showAttr?.Value;
                    if (!string.IsNullOrEmpty(val))
                    {
                        desc = $"{desc}: {val}";
                    }
                }
                return desc;
            }

            var attr = element?.Attribute(XName.Get("show")) ??
                        element?.Attribute(XName.Get("name"));

            return attr?.Value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
        }
    }
}
