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
    private static bool _headless, _saveMode, _autoFillNonXml;
    private string _chromePath;
    private static string _userDataDir;
    private static IBrowserContext _browser;
    private static IPage _page;
    private static IPlaywright _playwright;

    static Dictionary<string, string> config;

    // Конструктор для инициализации настроек
    public PlaywrightAssistant(bool headless, bool saveMode, bool autoFillNonXml)
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
        _autoFillNonXml = autoFillNonXml;

        if (_browser == null)
        {
            // Чтение конфигурации из файла
            if (config == null)
                config = ReadConfig();

            // Получение параметров из конфигурации
            _chromePath = config.ContainsKey("chromePath") ? config["chromePath"] : null;
            // _userDataDir = config.ContainsKey("userDataDir") ? config["userDataDir"] : null;

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

        var extensionPath1 = @"C:\EDO_Assistant\browserExtensions\Kontur"; // Путь к распакованному расширению
        var extensionPath2 = @"C:\EDO_Assistant\browserExtensions\Sbis"; // Путь к распакованному расширению
        // Запуск браузера с использованием пользовательского профиля
        _browser = await _playwright.Chromium.LaunchPersistentContextAsync(_userDataDir, new BrowserTypeLaunchPersistentContextOptions
        {
            ExecutablePath = _chromePath, // Указываем путь к Chrome, если _chromePath не null
            Headless = _headless, // Режим запуска браузера
            Channel = _chromePath == null ? "msedge" : null, // Используем Edge, если _chromePath равен null
#if !DEBUG
            Args = new[] {
                $"--disable-extensions-except={extensionPath1},{extensionPath2}",
                $"--load-extension={extensionPath1},{extensionPath2}"
            }
#endif
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
            Console.WriteLine("\n!!!Не найдено подходящего правила загрузки для указанных ИНН." +
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
            if (!_page.Url.Contains(operUrl))
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
                await HandleSabyDomainAsync(filePath, receiverInn);
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

    /*<div class="controls-Render__wrapper"><span class="controls-Render__baseline">﻿</span><div data-qa="controls-Render__field" class="controls-InputBase__field controls-InputBase__field_margin-null controls-InputBase__field_theme_default_margin-null controls-Lookup__fieldWrapper controls-Lookup__fieldWrapper_content_width_default



     controls-Lookup__fieldWrapper_style-info"><input name="ws-input_2025-02-11" spellcheck="true" type="text" inputmode="text" autocorrect="off" autocapitalize="on" autocomplete="off" class="controls-Field js-controls-Field controls-InputBase__nativeField controls-InputBase__nativeField_caretEmpty controls-InputBase__nativeField_caretEmpty_theme_default" tabindex="0" value=""><div class="controls-InputBase__placeholder controls-InputBase__placeholder_displayed-undefined-caret"><div class="controls-Render__placeholder controls-Render__placeholder_overflow"><div tabindex="0" class="addressee-Lookup__placeholder ws-flexbox ws-flex-row controls-Lookup__placeholder_style-info"><div class="controls-Render__placeholder_overflow">Укажите получателя</div></div></div></div><div class="controls-InputBase__forCalc"></div></div><div tabindex="0" class="controls-Lookup__rightFieldWrapper controls-Lookup__rightFieldWrapper_singleLine controls-Render__afterField"><invisible-node tabindex="-1" class="ws-hidden"></invisible-node><svg data-qa="Lookup__showSelector" class="controls-icon_svg controls-icon_size-s controls-Lookup__showSelector
                          controls-Lookup__showSelector_singleLine
                          controls_lookup_theme-default
                          controls-Lookup__icon controls-icon
                          controls-Lookup__showSelector_horizontalPadding-null" fill-rule="evenodd"><use xlink:href="/static/resources/Controls-icons/common.svg? x_module = fa53816b4b206e60c934bcc042e404e1#icon-Burger"></use></svg></div></div>
    */

    private async Task HandleSabyDomainAsync(string filePath, string INN)
    {
        var fileInput = await _page.QuerySelectorAsync("input[type='file']");
        if (fileInput == null)
        {

            /*            // Ожидаем появления элемента <span> с текстом "Загрузить" или "Добавить"
                        await page.waitForSelector('span.controls-BaseButton__text');
                        // Получаем текст из элемента <span>
                        const spanText = await page.textContent('span.controls-BaseButton__text');*/

            var buttonText = await _page.TextContentAsync("span:has-text('Загрузить'), span:has-text('Добавить')");

            // Проверяем, что за текст в кнопке
            if (buttonText == "Загрузить")
                await _page.ClickAsync("text=Загрузить");
            else if (buttonText == "Добавить")
                await _page.ClickAsync("text=Добавить");
            else
            {
                Console.Beep(1000, 500);
                Console.Beep(1000, 500);
                Console.WriteLine($"!!!Обновите программу.");
            }

            await WaitForTextAsync("С компьютера");
            await _page.ClickAsync("text=С компьютера");
            await Task.Delay(555);
        }
        await UploadFileAsync(filePath);
        SendKeys.SendWait("{ESC}");


        await WaitForTextAsync("Добавить");
        await WaitForTextAsync("Добавить");
        await WaitForTextAsync("Добавить");
        await WaitForTextAsync("Добавить");
        await WaitForTextAsync("Добавить");
        await WaitForTextAsync("Добавить");
        await WaitForTextAsync("Добавить");

        if (_autoFillNonXml && !filePath.EndsWith(".xml"))
        {
            var inputElement = await _page.QuerySelectorAsync("text=Укажите получателя");
            if (inputElement != null)
            {
                await _page.WaitForSelectorAsync("text='Укажите получателя'");
                await _page.FillAsync("input[type='text']", INN);
                await Task.Delay(555);
                await _page.ClickAsync("div[data-qa='crm_ClientMainInfo__name']");
            }
        }

        if (_saveMode)
        {
            await _page.WaitForSelectorAsync("span[data-qa='edo3-ReadOnlyStateTemplate__saveButton']", new PageWaitForSelectorOptions { State = WaitForSelectorState.Visible });
            await _page.ClickAsync("span[data-qa='edo3-ReadOnlyStateTemplate__saveButton']");
        }
    }

    //До нажатия на CounteragentsSearch:
    //<div tid="CounteragentsSearch"><span><span data-tid="ComboBoxView__root" class="react-ui-1ofblmi" style="width: 100%;"><span data-tid="InputLikeText__root" aria-controls="ComboBoxView__menu03d07700a4fa7" class="react-ui-g995bx" tabindex="0" style="width: 100%;"><input data-tid="InputLikeText__nativeInput" type="hidden" value=""><span class="react-ui-1uzh48y"><span data-tid="InputLikeText__input" class="react-ui-mtea40"></span><span class="react-ui-1iiuisf">Вводите название или ИНН контрагента</span></span><span class="react-ui-xzqvt8"><span class="react-ui-yp8cqa"><span class="react-ui-1xkfc81"><span class="react-ui-11i844s"><svg viewBox="0 0 16 16" class="react-ui-8nmv89" fill="currentColor" focusable="false" aria-hidden="true" style="margin-bottom: -0.1875em;"><path fill-rule="evenodd" clip-rule="evenodd" d="M8 9.00098L11.001 6C11.3747 6 11.6322 6.10937 11.7734 6.32812C11.9147 6.54688 11.9899 6.73828 11.999 6.90234V6.99805L8 10.9971L4.00098 6.99805V6.88867C4.03744 6.51953 4.20833 6.25977 4.51367 6.10938C4.65039 6.03646 4.81217 6 4.99902 6L8 9.00098Z"></path></svg></span></span></span></span></span></span></span></div>
    //После:
    //<div tid="CounteragentsSearch"><span><span data-tid="ComboBoxView__root" class="react-ui-1ofblmi" style="width: 100%;"><label data-tid="Input__root" class="react-ui-cwpcz5" aria-controls="ComboBoxView__menud8d6dd8560814" style="width: 100%;"><span class="react-ui-1dz8sqb"></span><span class="react-ui-1uzh48y"><input class="react-ui-1b99p38" type="text" placeholder="Вводите название или ИНН контрагента" value=""></span><span class="react-ui-xzqvt8"><span class="react-ui-1g0duzl"><span class="react-ui-1xkfc81"><span class="react-ui-11i844s"><svg viewBox="0 0 16 16" class="react-ui-8nmv89" fill="currentColor" focusable="false" aria-hidden="true" style="margin-bottom: -0.1875em;"><path fill-rule="evenodd" clip-rule="evenodd" d="M8 9.00098L11.001 6C11.3747 6 11.6322 6.10937 11.7734 6.32812C11.9147 6.54688 11.9899 6.73828 11.999 6.90234V6.99805L8 10.9971L4.00098 6.99805V6.88867C4.03744 6.51953 4.20833 6.25977 4.51367 6.10938C4.65039 6.03646 4.81217 6 4.99902 6L8 9.00098Z"></path></svg></span></span></span></span></label><noscript data-render-container-id="e92523b39d0b7"></noscript></span></span><noscript data-render-container-id="d40cab0a5129e"></noscript></div>
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
        if (_autoFillNonXml && !filePath.EndsWith(".xml"))
        {
            var inputElement = await _page.QuerySelectorAsync("input[placeholder='Вводите название или ИНН контрагента']");
            if (inputElement == null)
            {
                // Ожидаем появления элемента по тексту
                var spanElement = await _page.WaitForSelectorAsync("xpath=//span[contains(text(), 'Запросить подпись контрагента для всех документов')]");
                // Находим элемент по тексту с помощью XPath
                //var spanElement = await _page.QuerySelectorAsync("xpath=//span[contains(text(), 'Запросить подпись контрагента для всех документов')]");
                if (spanElement != null)
                {
                    // Кликаем по элементу
                    await spanElement.ClickAsync();
                }

                // Ожидаем появления элемента, по которому нужно кликнуть
                //var clickableElement = await _page.QuerySelectorAsync("xpath=//span[contains(text(), 'Вводите')]");
                var clickableElement = await _page.WaitForSelectorAsync(
                    "div[data-tid='CounteragentsSearch'], div[tid='CounteragentsSearch']",
                    new PageWaitForSelectorOptions
                    {
                        State = WaitForSelectorState.Attached,
                        Timeout = 10000  // 10 секунд (можно настроить)
                    }
                );

                if (clickableElement != null)
                {
                    // Кликаем по элементу
                    await clickableElement.ClickAsync();

                    // Дожидаемся появления элемента с placeholder 'Вводите название или ИНН контрагента'
                    await _page.WaitForSelectorAsync("input[placeholder='Вводите название или ИНН контрагента']");

                    // Находим элемент
                    inputElement = await _page.QuerySelectorAsync("input[placeholder='Вводите название или ИНН контрагента']");

                    if (inputElement != null)
                    {
                        // Получаем значение атрибута "value"
                        var value = await inputElement.EvaluateAsync<string>("element => element.value");

                        // Проверяем, что значение пустое
                        if (string.IsNullOrEmpty(value))
                        {
                            // Устанавливаем фокус на поле
                            await inputElement.FocusAsync();

                            // Вводим ИНН (или любое другое значение)
                            await inputElement.FillAsync(INN);  // Замени на нужный ИНН
                            await Task.Delay(555);
                            await inputElement.PressAsync("Enter");
                        }
                    }
                }
            }
        }
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
        var button = _page.Locator("text=" + buttonText);
        if (button != null)
        {
            await button.ClickAsync();
            Console.WriteLine($"Кнопка '{buttonText}' нажата.");
        }
        else

            /*
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

            */
            Console.WriteLine($"Кнопка с текстом '{buttonText}' не найдена.");
    }


    public async Task ClickButtonByTextAsync(IPage page, string url, string buttonText)
    {
        try
        {
            // Переход по указанному URL
            await page.GotoAsync(url);
            Console.WriteLine($"Успешно перешли по адресу: {url}");

            // Поиск кнопки по тексту
            var button = page.Locator($"text={buttonText}");

            if (await button.CountAsync() > 0)
            {
                await button.ClickAsync();
                Console.WriteLine($"Кнопка '{buttonText}' успешно нажата.");
            }
            else
            {
                Console.WriteLine($"Кнопка с текстом '{buttonText}' не найдена.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Произошла ошибка: {ex.Message}");
        }
    }
}