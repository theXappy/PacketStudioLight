using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PacketStudioLight
{
    // Without this class, menus will open to the left on some systems
    // See here https://stackoverflow.com/questions/37326546/why-are-my-menus-opening-right-to-left-instead-of-left-to-right
    internal static class LefToRightHack
    {
        private static FieldInfo _menuDropAlignmentField;
        public static void Init()
        {
            _menuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
            System.Diagnostics.Debug.Assert(_menuDropAlignmentField != null);

            EnsureStandardPopupAlignment();
            SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        }
        

        private static void SystemParameters_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            EnsureStandardPopupAlignment();
        }

        private static void EnsureStandardPopupAlignment()
        {
            if (SystemParameters.MenuDropAlignment && _menuDropAlignmentField != null)
            {
                _menuDropAlignmentField.SetValue(null, false);
            }
        }

    }
}
