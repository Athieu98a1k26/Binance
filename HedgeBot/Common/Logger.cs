using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HedgeBot
{
    public static class Logger
    {
        private static Queue<string> _logs = new Queue<string>();
        private const int MaxLogs = 20;
        private static readonly object _lock = new object(); // Chống xung đột luồng

        public static void Write(string message)
        {
            lock (_lock) // Đảm bảo an toàn khi chạy đa luồng
            {
                try
                {
                    // 1. Thêm log mới
                    string logEntry = $"[{DateTime.Now:HH:mm:ss}] {message}";
                    _logs.Enqueue(logEntry);

                    // 2. Giới hạn 50 dòng
                    while (_logs.Count > MaxLogs)
                    {
                        _logs.Dequeue();
                    }

                    // 3. In ra màn hình (Dùng Console gốc, KHÔNG gọi Logger.Write ở đây)
                    Console.Clear();
                    foreach (var log in _logs)
                    {
                        Console.WriteLine(log);
                    }
                }
                catch (Exception)
                {
                    // Nếu có lỗi trong lúc ghi log, in thẳng ra console để tránh vòng lặp
                    Console.WriteLine("Lỗi ghi Log!");
                }
            }
        }


        public static void WriteToFile(string message)
        {
            lock (_lock)
            {
                try
                {
                    // Tự động tạo thư mục Logs nếu chưa có
                    if (!Directory.Exists("Logs")) Directory.CreateDirectory("Logs");

                    string fileName = $"TradeLog_{DateTime.Now:yyyyMMdd}.txt";
                    string filePath = Path.Combine("Logs", fileName);
                    string logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";

                    // In ra Console bình thường (không xóa màn hình)
                    Console.WriteLine(logEntry);

                    // Ghi nối tiếp vào file
                    File.AppendAllText(filePath, logEntry + Environment.NewLine);
                }
                catch { /* Tránh treo bot */ }
            }
        }
    }
}
