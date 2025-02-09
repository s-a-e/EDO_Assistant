using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

class Program
{
    static bool headless, saveMode;

    private static ConcurrentQueue<string[]> _tasksQueue = new ConcurrentQueue<string[]>();
    private static bool _isRunning = true;

    public static async Task Main(string[] args)
    {
#if DEBUG
        Console.WriteLine("!!!Тестовая версия!!!! ");
        Console.WriteLine();
#endif
        Console.WriteLine("ЭДО ассистент версия 1.1. Ожидание подключения...");
        Console.WriteLine("Доступные команды:");
        Console.WriteLine("  x - Переключить режим браузера (Headless/Обычный)");
        Console.WriteLine("  s - Сохранять в черновиках/не сохранять");
        Console.WriteLine();

        // Получение параметров из конфигурации
        var config = PlaywrightAssistant.ReadConfig();
        var Headless = config.ContainsKey("headless") ? config["headless"] : null;
        if (Headless == null)
            headless = false;

        // Запуск обработки канала в отдельном потоке
        var pipeTask = Task.Run(() => RunPipeServer());

        // Запуск обработки задач из очереди
        var processTask = Task.Run(() => ProcessTasks());

        try
        {
            while (_isRunning)
            {
                // Проверяем, была ли нажата клавиша
                if (Console.KeyAvailable)
                {
                    // Считываем нажатую клавишу
                    ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);

                    // Приводим символ к нижнему регистру
                    char keyChar = char.ToLower(keyInfo.KeyChar);

                    if (keyChar == 'x' || keyChar == 'ч')
                    {
                        headless = !headless;
                        Console.WriteLine($"\nБраузер будет переключен в режим: {(headless ? "Headless" : "Обычный")}");
                    }
                    else if (keyChar == 's' || keyChar == 'ы')
                    {
                        saveMode = !saveMode;
                        Console.WriteLine($"\nРежим сохранения в черновики: {(saveMode ? "Включен" : "Отключен")}");
                    }
                }
                // Очищаем буфер ввода (все нажатые клавиши)
                while (Console.KeyAvailable)
                {
                    Console.ReadKey(intercept: true);
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

    private static async Task RunPipeServer()
    {
        try
        {
            while (_isRunning)
            {
                using (NamedPipeServerStream pipeServer = new NamedPipeServerStream("xAssistPipe", PipeDirection.In))
                {
                    // Ожидаем подключения клиента
                    await pipeServer.WaitForConnectionAsync();
                    Console.WriteLine("Клиент подключен.");

                    using (StreamReader reader = new StreamReader(pipeServer))
                    {
                        // Читаем данные, отправленные клиентом
                        string receivedData = await reader.ReadLineAsync();

                        if (!string.IsNullOrEmpty(receivedData))
                        {
                            // Разделяем данные по разделителю "|"
                            string[] dataParts = receivedData.Split('|');

                            if (dataParts.Length == 3)
                            {
                                // Добавляем задачу в очередь
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

                Console.WriteLine("Соединение закрыто. Ожидание нового подключения...");
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
                    string fullName = dataParts[0]; // Имя файла
                    string sellerINN = dataParts[1]; // ИНН продавца
                    string buyerINN = dataParts[2]; // ИНН покупателя

                    Console.WriteLine("Обработка задачи:");
                    Console.WriteLine($"Имя файла: {fullName}");
                    Console.WriteLine($"ИНН продавца: {sellerINN}");
                    Console.WriteLine($"ИНН покупателя: {buyerINN}");

                    // Создание экземпляра класса
                    assistant = new PlaywrightAssistant(headless, saveMode );

                    await assistant.RunAsync(
                        fullName,
                        sellerINN, // ИНН отправителя
                        buyerINN   // ИНН получателя
                    );

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
            // Закрытие браузера после выполнения всех операций
            if (assistant != null)
            {
                await assistant.CloseBrowserAsync();
            }
        }
    }
}