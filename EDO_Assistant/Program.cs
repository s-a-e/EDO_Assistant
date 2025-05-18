using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading.Tasks;

class Program
{
    static bool headless = false, saveDraft = false, autoFillNonXml = false;

    private static ConcurrentQueue<string[]> _tasksQueue = new ConcurrentQueue<string[]>();
    private static bool _isRunning = true;

    public static async Task Main(string[] args)
    {
        // Получение параметров из конфигурации
        var config = PlaywrightAssistant.ReadConfig();
        headless = config.ContainsKey("headless") ? bool.Parse(config["headless"]) : false;
        saveDraft = config.ContainsKey("saveDraft") ? bool.Parse(config["saveDraft"]) : false;
        autoFillNonXml = config.ContainsKey("autoFillNonXml") ? bool.Parse(config["autoFillNonXml"]) : false;

#if DEBUG
        Console.WriteLine("!!!Тестовая версия!!!! ");
        Console.WriteLine();
#endif
        DisplaySettings();

        // Запуск обработки канала в отдельном потоке
        var pipeTask = Task.Run(RunPipeServer);
        var processTask = Task.Run(ProcessTasks);

        try
        {
            while (_isRunning)
            {
                if (Console.KeyAvailable)
                {
                    var keyChar = char.ToLower(Console.ReadKey(intercept: true).KeyChar);
                    HandleKeyPress(keyChar);
                }

                await Task.Delay(100); // Небольшая задержка для уменьшения нагрузки на CPU
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
        finally
        {
            _isRunning = false; // Останавливаем обработку задач
            await Task.WhenAll(pipeTask, processTask); // Ожидаем завершения всех задач
        }
    }

    private static void DisplaySettings()
    {
        Console.WriteLine("ЭДО ассистент версия 1.6. Ожидание подключения...");
        Console.WriteLine("Доступные команды:");
        Console.WriteLine($"  x - Переключить режим браузера (Headless/Обычный) (Текущий: {(headless ? "Headless" : "Обычный")})");
        Console.WriteLine($"  s - Сохранять в черновиках/не сохранять (Текущий: {(saveDraft ? "Сохранять" : "Не сохранять")})");
        Console.WriteLine($"  n - Заполнение поля КА для не-XML документов (Текущий: {(autoFillNonXml ? "Включено" : "Отключено")})");
        Console.WriteLine($"  c - Открыть файл конфигурации config.txt");
        Console.WriteLine();
    }

    private static void HandleKeyPress(char keyChar)
    {
        if (keyChar == 'x' || keyChar == 'ч')
        {
            headless = !headless;
            Console.WriteLine($"\nБраузер будет переключен в режим: {(headless ? "Headless" : "Обычный")}");
        }
        else if (keyChar == 's' || keyChar == 'ы')
        {
            saveDraft = !saveDraft;
            Console.WriteLine($"\nРежим сохранения в черновики: {(saveDraft ? "Включен" : "Отключен")}");
        }
        else if (keyChar == 'n' || keyChar == 'т')
        {
            autoFillNonXml = !autoFillNonXml;
            Console.WriteLine($"\nЗаполнение поля КА для не-XML документов: {(autoFillNonXml ? "Включено" : "Отключено")}");
        }
        else if (keyChar == 'c' || keyChar == 'с')
        {
            OpenConfigFile();
        }
    }

    private static void OpenConfigFile()
    {
        try
        {
            string configPath = "config.txt";
            if (!File.Exists(configPath))
            {
                /*                // Создаем файл конфигурации с настройками по умолчанию, если он не существует
                                File.WriteAllText(configPath,
                                    "headless=false\n" +
                                    "saveDraft=false\n" +
                                    "autoFillNonXml=false");*/
                Console.WriteLine("\nФайл config.txt не найден.");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = configPath,
                UseShellExecute = true
            });
            //Console.WriteLine("\nФайл конфигурации открыт.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nОшибка при открытии файла конфигурации: {ex.Message}");
        }
    }

    private static async Task RunPipeServer()
    {
        try
        {
            while (_isRunning)
            {
                using (var pipeServer = new NamedPipeServerStream("xAssistPipe", PipeDirection.In))
                {
                    await pipeServer.WaitForConnectionAsync().ConfigureAwait(false);
                    //Console.WriteLine("Клиент подключен.");

                    using (var reader = new StreamReader(pipeServer))
                    {
                        var receivedData = await reader.ReadLineAsync().ConfigureAwait(false);

                        if (!string.IsNullOrEmpty(receivedData))
                        {
                            var dataParts = receivedData.Split('|');
                            if (dataParts.Length == 3)
                            {
                                _tasksQueue.Enqueue(dataParts);
                                Console.WriteLine("Задача добавлена в очередь.");
                            }
                            else
                            {
                                Console.WriteLine("Ошибка: получены неполные данные.");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Ошибка: данные не получены.");
                        }
                    }
                }

                Console.WriteLine("Ожидание нового подключения...");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка в потоке канала: {ex.Message}");
        }
    }

    private static async Task ProcessTasks()
    {
        PlaywrightAssistant assistant = null;

        try
        {
            while (_isRunning || !_tasksQueue.IsEmpty)
            {
                if (_tasksQueue.TryDequeue(out var dataParts))
                {
                    string fullName = dataParts[0], sellerINN = dataParts[1], buyerINN = dataParts[2];
                    Console.WriteLine("Обработка задачи:");
                    Console.WriteLine($"Имя файла: {fullName}");
                    Console.WriteLine($"ИНН продавца: {sellerINN}");
                    Console.WriteLine($"ИНН покупателя: {buyerINN}");

                    // Создание экземпляра класса
                    assistant = new PlaywrightAssistant(headless, saveDraft, autoFillNonXml);

                    await assistant.RunAsync(fullName, sellerINN, buyerINN).ConfigureAwait(false);

                    Console.WriteLine("Задача успешно обработана.");
                    Console.WriteLine();
                }
                else
                {
                    await Task.Delay(100); // Небольшая задержка, если очередь пуста
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка при обработке задачи: {ex.Message}");
        }
        finally
        {
            if (assistant != null)
            {
                await assistant.CloseBrowserAsync().ConfigureAwait(false);
            }
        }
    }
}