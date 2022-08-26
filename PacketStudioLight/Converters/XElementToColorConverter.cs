using System;
using System.Linq;
using System.Xml.Linq;
using System.Windows.Data;
using System.Windows.Media;
using System.Collections.Generic;

namespace PacketStudioLight
{
    //MyColorsConverter
    public class XElementToColorConverter : IValueConverter
    {
        XName showname = XName.Get("showname");


        // Heirarchy:
        // normal ->
        //          normal -> white
        //          error -> pink
        //          warn -> yellow
        //          layer -> gray
        // selected ->
        //          normal -> blue
        //          error -> pink-ish
        //          warn -> yellow-ish
        //          layer -> gray-ish
        // selected inactive -> ...
        static Dictionary<string, Dictionary<string, Brush>> stateToBrushes = new Dictionary<string, Dictionary<string, Brush>>();

        static XElementToColorConverter()
        {
            stateToBrushes["normal"] = new Dictionary<string, Brush>
            {
                {"normal", Brushes.Transparent },
                {"error",  new SolidColorBrush(Color.FromRgb(255, 92, 92)) },
                {"warn", Brushes.Yellow },
                {"layer",  new SolidColorBrush(Color.FromRgb(240, 240, 240)) }
            };
            stateToBrushes["selected"] = new Dictionary<string, Brush>
            {
                {"normal", new SolidColorBrush(Color.FromRgb(205, 232, 255)) }, // TODO: Make this transparent-ish blue instead of opaque
                {"error",  new SolidColorBrush(Color.FromRgb(205, 101, 124)) },
                {"warn",  new SolidColorBrush(Color.FromRgb(199, 222, 117)) },
                {"layer",  new SolidColorBrush(Color.FromRgb(193, 220, 243)) },
            };
            // TODO: Those are just the colors of selected
            stateToBrushes["selected_inactive"] = new Dictionary<string, Brush>
            {
                {"normal", new SolidColorBrush(Color.FromRgb(205, 232, 255)) },
                {"error",  new SolidColorBrush(Color.FromRgb(205, 101, 124)) },
                {"warn",  new SolidColorBrush(Color.FromRgb(199, 222, 117)) },
                {"layer",  new SolidColorBrush(Color.FromRgb(193, 220, 243)) },
            };

        }

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            string state = "normal";

            XElement element = value as XElement;
            bool isLayer = false;
            if(element.Parent == null)
            {
                isLayer = true;
            }

            if(parameter is string strParam)
                state = strParam;

            System.Collections.Generic.List<string?>? erroElements = element?.DescendantsAndSelf().Select(x => x.Attribute(showname)?.Value).Where(x => x?.StartsWith("Severity level") == true).ToList();

            // Note that Errors/Warns override layers tint
            if (erroElements?.Contains("Severity level: Error") == true)
                return stateToBrushes[state]["error"];
            if (erroElements?.Contains("Severity level: Warning") == true)
                return stateToBrushes[state]["warn"];

            return stateToBrushes[state][isLayer ? "layer" : "normal"];
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return null;
            //...
        }
    }
}
