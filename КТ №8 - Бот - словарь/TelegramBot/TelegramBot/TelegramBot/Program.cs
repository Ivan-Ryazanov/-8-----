using System.Text.Json;

namespace TelegramBot;

class Program
{
    private static string _botToken = " ";
    private static string _apiUrl = "https://api.telegram.org/bot";
    private static int _lastUpdateId = 0;

    private static Dictionary<string, string> _dictionary = new Dictionary<string, string>();
    private static Dictionary<long, string> _userState = new Dictionary<long, string>();

    static async Task Main(string[] args)
    {
        Console.WriteLine("Бот запущен");

        if (File.Exists("dictionary.json"))
        {
            string json = File.ReadAllText("dictionary.json");
            if (!string.IsNullOrWhiteSpace(json)) // Если файл не пустой
            {
                try
                {
                    _dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new(); // Превращаем json в словарь
                }
                catch
                {
                    _dictionary = new Dictionary<string, string>(); // Создается новый файл json, если предидущий файл сломан
                }
            }

            while (true) // Цикл обновления и логов
            {
                await GetUpdates(); // Проверяем новые сообщения
                await Task.Delay(1000);
            }
        }

        static async Task GetUpdates()
        {
            using HttpClient client = new HttpClient(); // Создаем клиент для запросов
            string url = $"{_apiUrl}{_botToken}/getUpdates?offset={_lastUpdateId + 1}"; // Запрашиваем новые сообщения

            string response = await client.GetStringAsync(url); // ответ от Telegram

            if (response.Contains("\"message\"")) // Проверка на обратную связь от пользователя
            {
                int chatIdStart = response.IndexOf("\"chat\":{\"id\":") + 13;
                int chatIdEnd = response.IndexOf(",", chatIdStart);
                long chatId = long.Parse(response.Substring(chatIdStart, chatIdEnd - chatIdStart));

                int textStart = response.IndexOf("\"text\":\"") + 8;
                int textEnd = response.IndexOf("\"", textStart);
                string userText = response.Substring(textStart, textEnd - textStart);

                userText = System.Text.RegularExpressions.Regex.Unescape(userText); // Декодирует символы в русс текст

                Console.WriteLine($"{userText} от {chatId}"); // Полученное смс от пользователя в консоль


                if (userText == "/start") // Команда start
                {
                    string keyboard = "{\"keyboard\":[[\"Определение термина\",\"Добавить термин\"]],\"resize_keyboard\":true}";
                    await SendMessage(chatId, "Выбери действие:", keyboard);
                }
                else if (userText == "Определение термина")
                {
                    _userState[chatId] = "get";
                    await SendMessage(chatId, "Введите термин:");
                }
                else if (userText == "Добавить термин")
                {
                    _userState[chatId] = "save_term";
                    await SendMessage(chatId, "Введите термин:");
                }
                else if (_userState.ContainsKey(chatId) && _userState[chatId] == "get")
                {
                    string key = userText.ToLower(); // Приводим смс к нижнему регистру
                    if (_dictionary.ContainsKey(key)) // Если термин есть в словаре
                    {
                        await SendMessage(chatId, $"Определение: {_dictionary[key]}");
                    }
                    else
                    {
                        await SendMessage(chatId, "Определение не найдено");
                    }
                    _userState.Remove(chatId);
                    await Task.Delay(2000);
                    await ShowMenu(chatId);
                }
                else if (_userState.ContainsKey(chatId) && _userState[chatId] == "save_term") // Ждем термин для сохранения
                {
                    string key = userText.ToLower();
                    if (_dictionary.ContainsKey(key))
                    {
                        await SendMessage(chatId, $"Термин [ {userText} ] уже существует!");
                        _userState.Remove(chatId);
                        await ShowMenu(chatId);
                    }
                    else
                    {
                        _userState[chatId] = $"save_def|{userText}";
                        await SendMessage(chatId, "Введите определение:");
                    }
                }
                else if (_userState.ContainsKey(chatId) && _userState[chatId].StartsWith("save_def"))
                {
                    string term = _userState[chatId].Split('|')[1];
                    _dictionary.Add(term.ToLower(), userText);

                    string json = JsonSerializer.Serialize(_dictionary);
                    File.WriteAllText("dictionary.json", json);

                    await SendMessage(chatId, $"Определение для [ {term} ] сохранено!");
                    _userState.Remove(chatId);
                    await ShowMenu(chatId);
                }
                else
                {
                    await SendMessage(chatId, "Используйте кнопки");
                    await ShowMenu(chatId);
                }

                int idStart = response.IndexOf("\"update_id\":") + 12;
                int idEnd = response.IndexOf(",", idStart);
                _lastUpdateId = int.Parse(response.Substring(idStart, idEnd - idStart));
                await Task.Delay(2000);
            }
        }

        static async Task SendMessage(long chatId, string text, string keyboard = null)
        {
            using HttpClient client = new HttpClient();
            string url = $"{_apiUrl}{_botToken}/sendMessage?chat_id={chatId}&text={text}";

            if (keyboard != null)
            {
                url += $"&reply_markup={keyboard}";
            }

            await client.GetStringAsync(url);
            Console.WriteLine($"-> {text}");
        }

        static async Task ShowMenu(long chatId)
        {
            string keyboard = "{\"keyboard\":[[\"Определение термина\",\"Добавить термин\"]],\"resize_keyboard\":true}";
            await SendMessage(chatId, "Выбери действие:", keyboard);
        }
    }
}