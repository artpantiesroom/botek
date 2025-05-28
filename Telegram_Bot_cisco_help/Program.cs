using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

//TOKEN = ("7403025632:AAGdPcbDirZhdZbWh4xXodgp1QhZ9ub2Kzw");
class Program
{
    
    static async Task Main()
    {

       

        var botClient = new TelegramBotClient("7403025632:AAGdPcbDirZhdZbWh4xXodgp1QhZ9ub2Kzw"); // 🔁 ВСТАВЬ СВОЙ ТОКЕН

        using var cts = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken: cts.Token
        );

        Console.WriteLine("Бот запущен. Нажмите Enter для выхода.");
        Console.ReadLine();
    }



    static void StartPingServer()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add("http://0.0.0.0:5000/");
        listener.Start();

        _ = Task.Run(async () =>
        {
            while (true)
            {
                var context = await listener.GetContextAsync();
                var response = context.Response;
                string responseString = "Bot is alive!";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
        });

        Console.WriteLine("Ping-сервер запущен на http://0.0.0.0:5000");
    }


static Dictionary<long, int> LastBotMessageIds = new();        // Кнопки
    static Dictionary<long, int> LastContentMessageIds = new();    // Текст

    static async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken token)
    {
        if (update.Message is { } message)
        {
            long chatId = message.Chat.Id;
            string text = message.Text?.Trim();

            if (text == "/start")
            {
                await DeletePreviousMessages(bot, chatId);

                var sent = await bot.SendTextMessageAsync(
                    chatId,
                    "Добро пожаловать! Выберите категорию:",
                    replyMarkup: GetMainKeyboard(),
                    cancellationToken: token);

                LastBotMessageIds[chatId] = sent.MessageId;
            }
            else if (IsCategoryAvailable(text))
            {
                await DeletePreviousMessages(bot, chatId);

                var sent = await bot.SendTextMessageAsync(
                    chatId,
                    $"Раздел: {text}",
                    replyMarkup: GenerateFileButtons(text),
                    cancellationToken: token);

                LastBotMessageIds[chatId] = sent.MessageId;
            }
        }
        else if (update.CallbackQuery is { } callbackQuery)
        {
            string data = callbackQuery.Data;
            long chatId = callbackQuery.Message.Chat.Id;

            await bot.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: token);

            if (data == "back")
            {
                await DeletePreviousMessages(bot, chatId);

                var sent = await bot.SendTextMessageAsync(
                    chatId,
                    "Вы вернулись в главное меню:",
                    replyMarkup: GetMainKeyboard(),
                    cancellationToken: token);

                LastBotMessageIds[chatId] = sent.MessageId;
            }
            else if (data.Contains("|"))
            {
                var parts = data.Split('|');
                if (parts.Length == 2)
                {
                    string category = parts[0];
                    string fileName = parts[1];

                    try
                    {
                        string content = await FileReader.ReadTextAsync(category, fileName);

                        await DeletePreviousMessages(bot, chatId);

                        var contentMsg = await bot.SendTextMessageAsync(chatId, content, cancellationToken: token);
                        LastContentMessageIds[chatId] = contentMsg.MessageId;

                        var menuMsg = await bot.SendTextMessageAsync(
                            chatId,
                            $"Раздел: {category}",
                            replyMarkup: GenerateFileButtons(category),
                            cancellationToken: token);

                        LastBotMessageIds[chatId] = menuMsg.MessageId;
                    }
                    catch (Exception ex)
                    {
                        await bot.SendTextMessageAsync(chatId, $"Ошибка: {ex.Message}", cancellationToken: token);
                    }
                }
            }
        }
    }

    static async Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken token)
    {
        Console.WriteLine($"Ошибка: {exception.Message}");

    }

    static async Task DeletePreviousMessages(ITelegramBotClient bot, long chatId)
    {
        if (LastBotMessageIds.TryGetValue(chatId, out int buttonsMsgId))
        {
            try { await bot.DeleteMessageAsync(chatId, buttonsMsgId); } catch { }
        }

        if (LastContentMessageIds.TryGetValue(chatId, out int contentMsgId))
        {
            try { await bot.DeleteMessageAsync(chatId, contentMsgId); } catch { }
        }
    }

    static bool IsCategoryAvailable(string category)
    {
        return category == "Switch" || category == "Router" || category == "Show";
    }

    static ReplyKeyboardMarkup GetMainKeyboard()
    {
        return new ReplyKeyboardMarkup(new[]
        {
        new KeyboardButton[] { "Switch", "Router"},
        new KeyboardButton[] { "Show" }
    })
        {
            ResizeKeyboard = true
        };
    }
        

    static InlineKeyboardMarkup GenerateFileButtons(string category)
    {
        var files = Directory.GetFiles($"Base_Text/{category}")
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        var buttons = files.Select(f =>
            InlineKeyboardButton.WithCallbackData(f, $"{category}|{f}")).ToList();

        buttons.Add(InlineKeyboardButton.WithCallbackData("🔙 Назад", "back"));

        return new InlineKeyboardMarkup(buttons.Select(b => new[] { b }));
    }

}
