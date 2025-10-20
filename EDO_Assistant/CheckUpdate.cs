
using System;
using System.Deployment.Application; // Добавьте ссылку на сборку System.Deployment
using System.Diagnostics;
using System.Windows.Forms;

namespace EDO_Assistant
{
    public static class CheckUpdateConsole
    {
        /// <summary>
        /// Проверяет наличие обновления ClickOnce, при необходимости спрашивает пользователя и устанавливает.
        /// </summary>
        /// <param name="askUser">Спросить у пользователя перед установкой.</param>
        /// <param name="autoRestart">Автоматически перезапустить приложение после обновления.</param>
        public static void CheckForApplicationUpdate(bool askUser = true, bool autoRestart = true)
        {
            if (!ApplicationDeployment.IsNetworkDeployed)
            {
                Console.WriteLine("Обновления недоступны — приложение не развернуто через ClickOnce (локальная установка).");
                return;
            }

            var ad = ApplicationDeployment.CurrentDeployment;

            try
            {
                bool updateAvailable = ad.CheckForUpdate();

                if (!updateAvailable)
                {
                    Console.WriteLine($"Вы используете последнюю версию приложения ({ad.CurrentVersion}).");
                    return;
                }

                Console.WriteLine($"Доступно обновление: {ad.CurrentVersion} → {ad.UpdatedVersion}");

                bool consent = true;
                if (askUser)
                {
                    Console.Write("Установить обновление? [Y/n]: ");
                    var input = Console.ReadLine()?.Trim().ToLowerInvariant();
                    // Enter/пусто — Да; поддержим yes/да
                    consent = string.IsNullOrEmpty(input) || input == "y" || input == "yes" || input == "д" || input == "да";
                }

                if (!consent)
                {
                    Console.WriteLine("Обновление отменено пользователем.");
                    return;
                }

                Console.WriteLine("Загрузка и установка обновления...");
                ad.Update();
                Console.WriteLine("Приложение обновлено.");

                if (autoRestart)
                {
                    Console.WriteLine("Перезапуск приложения...");
                    Application.Restart();
                    Restart();
                }
                else
                {
                    Console.WriteLine("Перезапустите приложение, чтобы применить обновление.");
                }
            }
            catch (DeploymentDownloadException dde)
            {
                Console.WriteLine("Не удалось загрузить новую версию. Проверьте подключение к сети.");
                Console.WriteLine("Ошибка: " + dde.Message);
            }
            catch (InvalidDeploymentException ide)
            {
                Console.WriteLine("Не удается проверить наличие обновлений. Манифест недоступен или некорректен.");
                Console.WriteLine("Ошибка: " + ide.Message);
            }
            catch (InvalidOperationException ioe)
            {
                Console.WriteLine("Это приложение не является ClickOnce-приложением.");
                Console.WriteLine("Ошибка: " + ioe.Message);
            }
        }

        private static void Restart()
        {
            try
            {
                // Вариант без WinForms — стартуем текущий exe и выходим из текущего процесса.
                // Для .NET Framework:
                var path = Process.GetCurrentProcess().MainModule.FileName;

                // Для .NET 6+ можно использовать Environment.ProcessPath (если перейдете на Core, но там ClickOnce API нет):
                // var path = Environment.ProcessPath;

                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Не удалось перезапустить автоматически: " + ex.Message);
                Console.WriteLine("Пожалуйста, запустите приложение вручную ещё раз.");
            }
            finally
            {
                Environment.Exit(0);
            }
        }
    }
}
