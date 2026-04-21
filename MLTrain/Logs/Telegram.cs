using Telegram.Bot;
using Telegram.Bot.Types;

namespace MLTrain.Logs
{
    public static class Telegram
    {
        // Lưu ý: Không nên để Token trực tiếp trong code nếu dự án thực tế
        private readonly static string botToken = "8128106058:AAHMlsoXK6cooA6dYyE1kQ7kshujnStbgqo";
        private readonly static long chatId = 8128106058;

        // Khởi tạo client dùng chung cho class
        private readonly static TelegramBotClient botClient = new TelegramBotClient(botToken);

        public static async Task SendTelegramMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;

            try
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: message
                );
                Console.WriteLine("Telegram: Gửi tin nhắn thành công.");
            }
            catch (Exception ex)
            {
                // Log lỗi nếu gửi thất bại (sai token, mất mạng, chat id sai...)
                Console.WriteLine($"Telegram Error: {ex.Message}");
            }
        }
    }
}
