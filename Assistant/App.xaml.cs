﻿using System.Configuration;
using System.Data;
using System.Windows;
using Utils;

namespace Assistant
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ThemeManager.ApplyTheme();
        }
    }

}
