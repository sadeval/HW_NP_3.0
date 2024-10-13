using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace RestaurantOrderServer
{
    class Program
    {
        static ConcurrentDictionary<int, Order> orders = new ConcurrentDictionary<int, Order>();
        static ConcurrentDictionary<int, TcpClient> clientConnections = new ConcurrentDictionary<int, TcpClient>();
        static int orderCounter = 1;

        static void Main()
        {
            TcpListener server = new TcpListener(IPAddress.Any, 5500);
            server.Start();
            Console.WriteLine("Сервер запущен и ожидает подключений...");

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.Start();
            }
        }

        private static void HandleClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                StreamReader reader = new StreamReader(stream, Encoding.UTF8);
                StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

                string clientRequest;
                while ((clientRequest = reader.ReadLine()) != null)
                {
                    var request = JsonConvert.DeserializeObject<ClientRequest>(clientRequest);
                    string response = HandleRequest(request, client);
                    writer.WriteLine(response);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при обработке клиента: {ex.Message}");
            }
        }

        private static string HandleRequest(ClientRequest request, TcpClient client)
        {
            switch (request.Command)
            {
                case "ADD_ORDER":
                    var order = new Order
                    {
                        OrderId = orderCounter++,
                        RestaurantName = request.RestaurantName,
                        Details = request.OrderDetails,
                        Status = "Принят"
                    };
                    orders.TryAdd(order.OrderId, order);
                    clientConnections.TryAdd(order.OrderId, client);

                    // Симулируем выполнение заказа и отправляем уведомление о готовности через 10 секунд
                    ThreadPool.QueueUserWorkItem(_ => ProcessOrder(order.OrderId));
                    return $"Заказ добавлен. ID заказа: {order.OrderId}";

                case "CHECK_STATUS":
                    if (orders.TryGetValue(request.OrderId, out Order? existingOrder))
                    {
                        return $"Статус заказа {existingOrder.OrderId}: {existingOrder.Status}";
                    }
                    return "Заказ не найден.";

                case "CANCEL_ORDER":
                    if (orders.TryRemove(request.OrderId, out Order? removedOrder))
                    {
                        clientConnections.TryRemove(removedOrder.OrderId, out _);
                        return $"Заказ {removedOrder.OrderId} отменен.";
                    }
                    return "Не удалось отменить заказ.";

                default:
                    return "Неверная команда.";
            }
        }

        private static void ProcessOrder(int orderId)
        {
            // Симуляция обработки заказа (10 секунд)
            Thread.Sleep(10000);

            if (orders.TryGetValue(orderId, out Order? order))
            {
                order.Status = "Готов";
                NotifyClient(orderId, $"Ваш заказ {orderId} готов!");
            }
        }

        private static void NotifyClient(int orderId, string message)
        {
            if (clientConnections.TryGetValue(orderId, out TcpClient? client))
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    StreamWriter writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                    writer.WriteLine(message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при отправке уведомления клиенту: {ex.Message}");
                }
            }
        }
    }

    public class Order
    {
        public int OrderId { get; set; }
        public string? RestaurantName { get; set; }
        public string? Details { get; set; }
        public string? Status { get; set; }
    }

    public class ClientRequest
    {
        public string? Command { get; set; }
        public string? RestaurantName { get; set; }
        public string? OrderDetails { get; set; }
        public int OrderId { get; set; }
    }
}
