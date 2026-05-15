using System;
using System.Windows.Forms;
using CostAnalysis.App.Data;
using CostAnalysis.App.UI;

namespace CostAnalysis.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            DatabaseInitializer.Initialize();
            Application.Run(new MainForm());
        }
    }
}
