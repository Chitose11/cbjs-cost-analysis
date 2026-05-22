using System;
using System.Windows.Forms;
using CostAnalysis.App.Data;
using CostAnalysis.App.Services;
using CostAnalysis.App.UI;

namespace CostAnalysis.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            DatabaseInitializer.Initialize();
            var mainForm = new MainForm();
            mainForm.Shown += (_, __) => EnvironmentCheckForm.ShowStartupWarningIfNeeded(mainForm);
            Application.Run(mainForm);
        }

        private static void OnThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            ErrorLogService.Write("UI线程未处理异常", e.Exception);
            MessageBox.Show(
                "程序遇到错误，但已阻止直接退出。\r\n\r\n" + e.Exception.Message + "\r\n\r\n错误日志：" + ErrorLogService.LogFilePath,
                "程序错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;
            ErrorLogService.Write("非UI线程未处理异常", exception ?? new Exception(Convert.ToString(e.ExceptionObject)));
        }
    }
}
