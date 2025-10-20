using System;
using System.Diagnostics;
using System.Deployment.Application;
using System.Windows.Forms; // Требуется для ApplicationDeployment

/// <summary>
/// Класс для управления обновлениями ClickOnce.
/// </summary>
public static class ClickOnceUpdater
{
    /// <summary>
    /// Проверяет наличие обновления и, если оно доступно, скачивает и устанавливает его.
    /// Если обновление установлено, перезапускает приложение.
    /// </summary>
    public static void CheckForUpdateAndRestart()
    {
        if (ApplicationDeployment.IsNetworkDeployed)
        {
            try
            {
                UpdateCheckInfo info = ApplicationDeployment.CurrentDeployment.CheckForDetailedUpdate();
                if (info.UpdateAvailable)
                {
                    Console.WriteLine("Обнаружено обновление. Скачивание и установка...");
                    ApplicationDeployment.CurrentDeployment.Update();

                    Console.WriteLine("Обновление установлено. Перезапуск приложения...");

                    // --- Правильный способ перезапуска для консольного ClickOnce-приложения ---
/*                    
                        // Получаем полный путь к новому исполняемому файлу
                        string newExecutablePath = ApplicationDeployment.CurrentDeployment.UpdatedApplicationFullName;

                        // Запускаем новый процесс
                        Process.Start(newExecutablePath);
*/                    
                    //Однако есть более "официальный" и надежный способ, который рекомендует Microsoft, даже для консольных приложений. Он заключается в добавлении ссылки на сборку System.Windows.Forms и использовании статического метода System.Windows.Forms.Application.Restart().Этот метод корректно обрабатывает все внутренние механизмы ClickOnce для перезапуска.
                    System.Windows.Forms.Application.Restart();

                    // Завершаем текущий процесс
                    Environment.Exit(0);
                }
            }
            catch (DeploymentDownloadException dde)
            {
                Console.WriteLine("Не удалось скачать новую версию приложения. Проверьте подключение к сети.");
                Console.WriteLine(dde.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Произошла ошибка при обновлении: " + ex.Message);
            }
        }
        else
        {
            Console.WriteLine("Приложение запущено не через ClickOnce. Обновление невозможно.");
        }
    }
}