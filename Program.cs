using Project_auto_push_to_gitlab.SqlOptimizer.Application;

namespace Project_auto_push_to_gitlab
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();

            SqlOptimizerApplicationRuntime sqlOptimizerRuntime =
                new SqlOptimizerApplicationRuntime();
            try
            {
                sqlOptimizerRuntime.StartAsync(CancellationToken.None)
                    .GetAwaiter()
                    .GetResult();

                frm_SessionGrid mainForm = new frm_SessionGrid
                {
                    SqlOptimizerFormFactory =
                        sqlOptimizerRuntime.CreateOptimizerForm
                };
                Application.Run(mainForm);
            }
            finally
            {
                sqlOptimizerRuntime.DisposeAsync()
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
            }
        }
    }
}
