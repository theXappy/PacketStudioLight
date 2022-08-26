using System;
using System.Xml.Linq;
using System.Windows.Data;

namespace PacketStudioLight
{
    public class XElementToPacketDescriptionConverter : IValueConverter
    {

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            XElement element = (XElement)value;
            var attr = (element.Attribute(XName.Get("showname")) ??
                element.Attribute(XName.Get("show")) ??
                element.Attribute(XName.Get("name"))
                );

            return attr?.Value;
            //...
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
            //...
        }
    }
}
