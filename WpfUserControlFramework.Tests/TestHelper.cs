using System.Windows;

namespace RestaurantPosWpf.Tests
{
    /// <summary>
    /// Shared test infrastructure for WPF tests.
    /// Ensures a single Application instance and provides STA thread execution.
    /// </summary>
    public static class TestHelper
    {
        private static readonly object AppLock = new();
        private static bool _appInitialized;

        public static void EnsureApplication()
        {
            lock (AppLock)
            {
                if (_appInitialized) return;

                if (Application.Current == null)
                {
                    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    var themeDict = new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/WpfUserControlFramework;component/PeoplePosTheme.xaml")
                    };
                    app.Resources.MergedDictionaries.Add(themeDict);
                    app.Resources["UiFontScale"] = 1.0;
                }

                _appInitialized = true;
            }
        }

        public static void RunOnSta(Action action)
        {
            Exception? caught = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { caught = ex; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (caught != null)
                throw new Exception("STA thread exception", caught);
        }
    }
}
