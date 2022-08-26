using System;
using System.Linq;
using System.Xml.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace PacketStudioLight
{
    //MyColorsConverter
    public class XElementToColorConverter : IValueConverter
    {
        XName showname = XName.Get("showname");

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            XElement element = (XElement)value;
            var erroElements = element.DescendantsAndSelf().Select(x => x.Attribute(showname)?.Value).Where(x => x?.StartsWith("Severity level") == true).ToList();
            if (erroElements.Contains("Severity level: Error"))
                return new SolidColorBrush(Color.FromRgb(255, 92, 92));
            if (erroElements.Contains("Severity level: Warning"))
                return Brushes.Yellow;
            return Brushes.Transparent;          //...
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
            //...
        }
    }
}
