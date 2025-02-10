using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Playwright;

public class UploadRule
{
    public string SenderInn { get; set; }
    public string ReceiverInn { get; set; }
    public string Url { get; set; }
}
class PlaywrightAssistant
{
    // Поля для хранения настроек и состояния
    private static bool _headless, _saveMode;
    private string _chromePath;
    private static string _userDataDir;
    private static IBrowserContext _browser;
    private static IPage _page;
    private static IPlaywright _playwright;

    static Dictionary<string, string> config;

    // Конструктор для инициализации настроек
    public PlaywrightAssistant(bool headless, bool saveMode)
    {
        if (_headless != headless)
            if (_browser != null)
            {
                _browser.CloseAsync().GetAwaiter().GetResult();
                _browser = null;
                /*                _browser.CloseAsync();
                                for (int i = 0; i < 5; i++)
                                {
                                    if (_browser.Pages.Count > 0)
                                        Task.Delay(1000).Wait(); // Пауза на 1 секунду
                                }*/
            }

        _headless = headless;
        _saveMode = saveMode;

        if (_browser == null)
        {
            // Чтение конфигурации из файла
            if (config == null)
                config = ReadConfig();

            // Получение параметров из конфигурации
            _chromePath = config.ContainsKey("chromePath") ? config["chromePath"] : null;
            _userDataDir = config.ContainsKey("userDataDir") ? config["userDataDir"] : null;

            // Проверка наличия обязательных параметров
            /*           if (string.IsNullOrEmpty(_userDataDir))
                       {
                           throw new Exception("Не удалось прочитать userDataDir из config.txt.");
                       }*/
        }
    }

    // Метод для чтения конфигурации из файла
    public static Dictionary<string, string> ReadConfig()
    {
        string configFilePath = "config.txt"; // Путь к файлу конфигурации
        var config = new Dictionary<string, string>();

        if (!File.Exists(configFilePath))
        {
            throw new FileNotFoundException($"Файл конфигурации не найден: {configFilePath}");
        }

        // Чтение всех строк из файла
        string[] lines = File.ReadAllLines(configFilePath);

        foreach (var line in lines)
        {
            // Удаление пробелов в начале и конце строки
            string trimmedLine = line.Trim();

            // Пропуск пустых строк и закомментированных строк
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("//"))
            {
                continue;
            }

            // Разделение строки на ключ и значение
            var parts = trimmedLine.Split(new[] { '=' }, 2);
            if (parts.Length == 2)
            {
                string key = parts[0].Trim();
                string value = parts[1].Trim();
                config[key] = value;
            }
        }

        return config;
    }

    private List<UploadRule> ReadUploadRules()
    {
        var rules = new List<UploadRule>();
        string configFilePath = "config.txt"; // Путь к файлу конфигурации

        if (!File.Exists(configFilePath))
        {
            throw new FileNotFoundException($"Файл конфигурации не найден: {configFilePath}");
        }

        // Чтение всех строк из файла
        string[] lines = File.ReadAllLines(configFilePath);

        string _lastUploadUrl = null; // Сохраняем последний URL из uploadRule
        foreach (var line in lines)
        {
            // Удаление пробелов в начале и конце строки
            string trimmedLine = line.Trim();

            // Пропуск пустых строк и закомментированных строк
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";"))
            {
                continue;
            }

            // Обработка строк с uploadRule
            if (trimmedLine.StartsWith("uploadRule=") || trimmedLine.StartsWith("="))
            {
                var parts = trimmedLine.Split(new[] { '=' }, 2);
                if (parts.Length == 2)
                {
                    var ruleParts = parts[1].Split(new[] { ',' }, 3);
                    if (ruleParts.Length >= 2)
                    {
                        var rule = new UploadRule
                        {
                            SenderInn = ruleParts[0].Trim(),
                            ReceiverInn = ruleParts[1].Trim(),
                            Url = (ruleParts.Length == 3 ? ruleParts[2].Trim() : _lastUploadUrl)
                        };
                        rules.Add(rule);
                        _lastUploadUrl = rule.Url;
                    }
                }
            }
        }
        return rules;
    }
    // Основной метод для запуска процесса
    public async Task RunAsync(string filePath, string senderInn, string receiverInn)
    {
        // Проверка существования файла перед началом работы
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Файл не найден: {filePath}");
            return;
        }

        try
        {
            // Инициализация браузера (если ещё не инициализирован)
            if (_browser == null || _browser.Pages.Count == 0)
            {
                await InitializeBrowserAsync();
                _page = await _browser.NewPageAsync();
            }

            // Вызов метода для выполнения основных операций на странице
            var retCode = await PerformOperationsAsync(filePath, senderInn, receiverInn);
            if (retCode == -1)
            {
                Console.WriteLine("Перезапускаем браузер.");
                await _browser.CloseAsync();
                _headless = false;
                await InitializeBrowserAsync();
                _page = await _browser.NewPageAsync();
                await PerformOperationsAsync(filePath, senderInn, receiverInn);
                await _browser.CloseAsync();
            }

            // Пауза между загрузками файлов
            //Console.WriteLine("Ожидание 5 секунд перед следующей загрузкой...");
            //await Task.Delay(1000);
        }
        catch (Exception ex)
        {
            // Обработка исключений
            Console.WriteLine($"Произошла ошибка: {ex.Message}");
            Console.Beep(1000, 500);
        }
    }
    // Метод для инициализации браузера
    private async Task InitializeBrowserAsync()
    {
        // Инициализация Playwright
        _playwright = await Playwright.CreateAsync();
        // Определяем путь к папке профиля
        if (_userDataDir == null)
        {
            string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _userDataDir = Path.Combine(userProfilePath, "EDO_Assistant", "browserProfile");

            // Создаём папку, если её нет
            if (!Directory.Exists(_userDataDir))
            {
                Directory.CreateDirectory(_userDataDir);
            }
        }

        // Запуск браузера с использованием пользовательского профиля
        _browser = await _playwright.Chromium.LaunchPersistentContextAsync(_userDataDir, new BrowserTypeLaunchPersistentContextOptions
        {
            //            ExecutablePath = _chromePath, // Указываем путь к Chrome
            Headless = _headless, // Режим запуска браузера
            Channel = "msedge", // Использование Edge вместо Chromium
            //Args = new[] { "--window-position=0,0 --window-size=1,1" }, // Дополнительные аргументы
        });

        Console.WriteLine("Браузер успешно инициализирован.");
    }

    // Метод для закрытия браузера
    public async Task CloseBrowserAsync()
    {
        if (_browser != null)
        {
            await _browser.CloseAsync();
            Console.WriteLine("Браузер успешно закрыт.");
        }

        if (_playwright != null)
        {
            _playwright.Dispose();
            Console.WriteLine("Playwright успешно завершён.");
        }
    }
    readonly static string cfg = Environment.CurrentDirectory + "\\Config.txt";
    static string clipboardText = "";
    static void WorkerThread()
    {
        try
        {
            Process.Start("notepad.exe", cfg);
            Console.WriteLine("config.txt открыт.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Не удалось открыть файл {cfg}: {ex.Message}");
        }
        // В этом потоке будет работать Clipboard
        Clipboard.SetText(clipboardText);
        Console.WriteLine($"{clipboardText} - текст скопирован в буфер обмена.\n");
    }
    // Метод для выполнения основных операций на странице
    static string lastSenderInn, lastReceiverInn;
    private async Task<int> PerformOperationsAsync(string filePath, string senderInn, string receiverInn)
    {
        // Чтение правил загрузки
        var uploadRules = ReadUploadRules();

        // Поиск подходящего правила
        string operUrl = GetUrlFromRules(uploadRules, senderInn, receiverInn);

        if (string.IsNullOrEmpty(operUrl))
        {
            Console.Beep(1000, 500);
            clipboardText = "=" + senderInn + "," + receiverInn;
            Console.WriteLine("!!!Не найдено подходящего правила загрузки для указанных ИНН." +
                $"\nОткройте файл {cfg} и вставьте правило:\n" +
                clipboardText +
                "\nв нужное место. Сохраните изменения и закройте config.txt.");

            // Создаем новый поток с состоянием STA
            Thread thread = new Thread(WorkerThread);
            thread.SetApartmentState(ApartmentState.STA);  // Устанавливаем состояние STA
            thread.Start();
            return 0;
        }
#if !DEBUG
        if (operUrl.Contains("/f10d3327-d6c4-4cbc-b207-37506030b9e6/"))
        {
            Console.WriteLine($"!!!Откройте {cfg} и отредактируйте.");
            Console.WriteLine(@"!!!Замените /f10d3327-d6c4-4cbc-b207-37506030b9e6/ на свой ящик.");

            // Попытка открыть файл Config.txt в блокноте
            try
            {
                Process.Start("notepad.exe", cfg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Не удалось открыть файл {cfg}: {ex.Message}");
            }
            await Task.Delay(5555); 
            // Завершение программы с кодом 0x100
            Environment.Exit(0x100);
        }
#endif
        try
        {
            // Переход на указанный URL
            if (_saveMode || _page.Url != operUrl)
            {
                await _page.GotoAsync(operUrl);
            }

            var authUrls = new HashSet<string> { "auth.kontur.ru", "identity.astral.ru", "sbis.ru/auth" };
            byte isBeepPlayed = 0; // Флаг для отслеживания воспроизведения звука
            while (authUrls.Any(url => _page.Url.Contains(url)))
            {
                if (_headless)
                    return -1;
                if (++isBeepPlayed == 1)
                    Console.Beep(1000, 500);
                Console.Write("Запрос Пароля. ");
                await Task.Delay(3000); // Пауза для ожидания ввода пароля
            }

            // Проверка, совпадает ли текущий URL с ожидаемым
            if (_page.Url != operUrl)
            {
                Console.WriteLine($"Текущий URL не совпадает с ожидаемым.\nОжидаемый: {operUrl}\nТекущий: {_page.Url}");
            }
            else
            {
                Console.WriteLine(operUrl);
            }

            // Обработка различных доменов
            if (_page.Url.Contains("saby.ru") || _page.Url.Contains("sabyd.ru"))
            {
                await HandleSabyDomainAsync(filePath);
            }
            else if (_page.Url.Contains("kontur.ru"))
            {
                await HandleKonturDomainAsync(filePath, receiverInn);
            }
            else if (_page.Url.Contains("astral.ru"))
            {
                await UploadFileAsync(filePath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Произошла ошибка: {ex.Message}");
            Console.Beep(1000, 500);
        }
        return 0;
    }

    private async Task HandleSabyDomainAsync(string filePath)
    {
        var fileInput = await _page.QuerySelectorAsync("input[type='file']");
        if (fileInput == null)
        {
            await _page.ClickAsync("text=Загрузить");
            await WaitForTextAsync("С компьютера");
            await _page.ClickAsync("text=С компьютера");
            await Task.Delay(555);
            SendKeys.SendWait("{ESC}");
            await Task.Delay(555);
        }
        await UploadFileAsync(filePath);

        await WaitForTextAsync("Добавить");
        await WaitForTextAsync("Добавить");
        await WaitForTextAsync("Добавить");

        await _page.WaitForSelectorAsync("span[data-qa='edo3-ReadOnlyStateTemplate__saveButton']", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });
        await _page.ClickAsync("span[data-qa='edo3-ReadOnlyStateTemplate__saveButton']");
    }

    private async Task HandleKonturDomainAsync(string filePath, string INN)
    {
        /*                if(lastSenderInn != null)
                     {
                         if (lastSenderInn != senderInn || lastReceiverInn != receiverInn)
                         {
                             await ClickButtonAsync("Сохранить в черновиках");
                             lastSenderInn = senderInn; lastReceiverInn = receiverInn;
                         }
                     }
                     else
                     {
                         lastSenderInn = senderInn; lastReceiverInn = receiverInn;
                     }*/

        // Загрузка файла
        await UploadFileAsync(filePath);

        await WaitForTextAsync("Комментарий");

        string pageText = await _page.TextContentAsync("body");
        if (pageText.Contains("ошибк"))
        {
            Console.Beep(1000, 500);
            Console.WriteLine($"!!!Ошибки в документе.");
            await Task.Delay(9999);
        }

/*        if (!filePath.EndsWith(".xml"))
        {

            var hiddenInput = await _page.QuerySelectorAsync("input[type='hidden']");
            if (hiddenInput != null)
            {
                // Делаем элемент видимым с помощью JS
                await hiddenInput.EvaluateAsync("element => element.style.display = 'block'");
            }
            // Дожидаемся появления элемента с placeholder 'Вводите название или ИНН контрагента'
            await _page.WaitForSelectorAsync("input[placeholder='Вводите название или ИНН контрагента']");

            // Находим элемент
            var inputElement = await _page.QuerySelectorAsync("input[placeholder='Вводите название или ИНН контрагента']");

            if (inputElement != null)
            {
                // Устанавливаем фокус на поле
                await inputElement.FocusAsync();

                // Вводим ИНН (или любое другое значение)
                await inputElement.FillAsync("1234567890");  // Замени на нужный ИНН

                // Нажимаем Enter
                await inputElement.PressAsync("Enter");
            }
        }*/
        if (_saveMode)
            // Поиск и нажатие кнопки "Сохранить в черновиках"
            await ClickButtonAsync("Сохранить в черновиках");
    }

    private string GetUrlFromRules(List<UploadRule> rules, string senderInn, string receiverInn)
    {
        foreach (var rule in rules)
        {
            // Проверка на соответствие ИНН отправителя и получателя
            bool senderMatch = rule.SenderInn == "*" || rule.SenderInn == senderInn;
            bool receiverMatch = rule.ReceiverInn == "*" || rule.ReceiverInn == receiverInn;

            if (senderMatch && receiverMatch)
            {
                return rule.Url;
            }
        }

        return null;
    }
    // Метод для загрузки файла
    private async Task UploadFileAsync(string filePath)
    {
        // Поиск элемента для загрузки файла
        //var fileInput = await _page.QuerySelectorAsync("input[data-tid='fileUploadInput']");
        // Найдите скрытый input для загрузки файла
        //var fileInput = await _page.QuerySelectorAsync("input[type='file'][style*='display: none']");

        IElementHandle fileInput = null;

        // Цикл for для повторных попыток
        for (int i = 0; i < 7; i++)
        {
            //            fileInput = await _page.QuerySelectorAsync("input[type='file'][style*='display: none']");
            fileInput = await _page.QuerySelectorAsync("input[type='file']");
            if (fileInput != null)
            {
                break;
            }
            await Task.Delay(1000);
        }

        //        var fileInput = await _page.QuerySelectorAsync("input[multiple][type='file'][style*='display: none']");
        if (fileInput != null)
        {
            // Загрузка файла через элемент <input type="file">
            await fileInput.SetInputFilesAsync(filePath);
            //            Console.WriteLine($"Файл '{filePath}' успешно загружен.");
            Console.WriteLine($"Файл успешно загружен.");
        }
        else
        {
            Console.WriteLine("Элемент для загрузки файла не найден.");
        }
    }

    // Метод для ожидания появления текста на странице
    private async Task WaitForTextAsync(string text)
    {
        bool textFound = false;
        int retryCount = 5; // Количество попыток
        int timeout = 1000; // Тайм-аут в миллисекундах

        for (int i = 0; i < retryCount; i++)
        {
            try
            {
                // Ожидание появления текста
                await _page.WaitForFunctionAsync(
                    $"() => document.body.textContent.includes('{text}')",
                    null,
                    new PageWaitForFunctionOptions { Timeout = timeout }
                );

                textFound = true;
                break; // Выход из цикла, если текст найден
            }
            catch (TimeoutException)
            {
                Console.WriteLine($"Попытка {i + 1}: Текст '{text}' не появился в течение {timeout} мс.");
            }
        }

        if (!textFound)
        {
            Console.WriteLine($"Текст '{text}' не найден после всех попыток.");
        }
    }

    // Метод для поиска и нажатия кнопки
    private async Task ClickButtonAsync(string buttonText)
    {
        // Поиск всех кнопок с атрибутом data-tid='Button__root'
        var buttons = await _page.QuerySelectorAllAsync("button[data-tid='Button__root']");

        if (buttons.Count > 0)
        {
            // Перебор всех найденных кнопок
            foreach (var button in buttons)
            {
                // Получение текста кнопки
                var currentButtonText = await button.InnerTextAsync();

                // Если текст кнопки совпадает с искомым, нажимаем её
                if (currentButtonText == buttonText)
                {
                    await button.ClickAsync();
                    Console.WriteLine($"Кнопка '{buttonText}' нажата.");
                    return; // Выход из метода после нажатия
                }
            }
        }

        Console.WriteLine($"Кнопка с текстом '{buttonText}' не найдена.");
    }
}