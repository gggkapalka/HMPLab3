using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
namespace Pizzeria;
public class BakerConfig
{
    public string Name { get; set; } = "Пекарь";
    public double Experience { get; set; } = 1.0;
}
public class CourierConfig
{
    public string Name { get; set; } = "Курьер";
    public int Capacity { get; set; } = 2;
}
public class PizzeriaConfig
{
    public List<BakerConfig> Bakers { get; set; } = new();
    public List<CourierConfig> Couriers { get; set; } = new();
    public int WarehouseSize { get; set; } = 3;
    public int OrdersCount { get; set; } = 20;
    public int OrderIntervalMs { get; set; } = 300;
    public int BaseCookMs { get; set; } = 1000;
    public int DeliveryMs { get; set; } = 700;
    public int FreeThresholdMs { get; set; } = 6000;
}
public class PizzaOrder
{
    public int Id;
    public long CreatedMs, TakenMs, ReadyMs, StoredMs, DeliveredMs;
    public string BakerName = "";
    public bool Free;
}
public class WorkerStats
{
    public string Name = "";
    public long BusyMs, IdleMs, BlockedMs;
    public int Count;
}
public class PizzeriaSimulation
{
    private readonly PizzeriaConfig _cfg;
    private readonly Stopwatch _clock = new();
    private readonly object _logLock = new();
    private readonly object _statLock = new();
    private readonly BlockingCollection<PizzaOrder> _queue = new();
    private readonly BlockingCollection<PizzaOrder> _warehouse;
    private readonly List<PizzaOrder> _orders = new();
    private int _peakWarehouse;
    public PizzeriaSimulation(PizzeriaConfig cfg)
    {
        _cfg = cfg;
        _warehouse = new BlockingCollection<PizzaOrder>(cfg.WarehouseSize);
    }
    private long Now => _clock.ElapsedMilliseconds;
    private void Log(PizzaOrder o, string state)
    {
        lock (_logLock)
            Console.WriteLine($"[{o.Id}], [{state}]   (t={Now} мс)");
    }
    public void Run()
    {
        var bakerStats = _cfg.Bakers.Select(b => new WorkerStats { Name = b.Name }).ToList();
        var courierStats = _cfg.Couriers.Select(c => new WorkerStats { Name = c.Name }).ToList();
        var bakerThreads = new List<Thread>();
        var courierThreads = new List<Thread>();
        _clock.Start();
        for (int i = 0; i < _cfg.Bakers.Count; i++)
        {
            var cfg = _cfg.Bakers[i];
            var st = bakerStats[i];
            var t = new Thread(() => BakerWork(cfg, st));
            bakerThreads.Add(t);
        }
        for (int i = 0; i < _cfg.Couriers.Count; i++)
        {
            var cfg = _cfg.Couriers[i];
            var st = courierStats[i];
            var t = new Thread(() => CourierWork(cfg, st));
            courierThreads.Add(t);
        }
        var generator = new Thread(OrderGenerator);
        Console.WriteLine("=== Начало смены ===");
        foreach (var t in bakerThreads) t.Start();
        foreach (var t in courierThreads) t.Start();
        generator.Start();
        generator.Join();
        foreach (var t in bakerThreads) t.Join();
        _warehouse.CompleteAdding();
        foreach (var t in courierThreads) t.Join();
        _clock.Stop();
        Console.WriteLine("=== Конец смены ===\n");
        Analyze(bakerStats, courierStats);
    }
    private void OrderGenerator()
    {
        for (int i = 1; i <= _cfg.OrdersCount; i++)
        {
            var order = new PizzaOrder { Id = i, CreatedMs = Now };
            lock (_statLock) _orders.Add(order);
            _queue.Add(order);
            Log(order, "Заказ принят в очередь");
            Thread.Sleep(_cfg.OrderIntervalMs);
        }
        _queue.CompleteAdding();
    }
    private void BakerWork(BakerConfig cfg, WorkerStats st)
    {
        double exp = cfg.Experience <= 0 ? 0.1 : cfg.Experience;
        while (true)
        {
            long t0 = Now;
            if (!_queue.TryTake(out PizzaOrder order, Timeout.Infinite)) break;
            st.IdleMs += Now - t0;
            order.TakenMs = Now;
            order.BakerName = cfg.Name;
            Log(order, $"Пекарь {cfg.Name} взял заказ в работу");
            long c0 = Now;
            Thread.Sleep((int)(_cfg.BaseCookMs / exp));
            st.BusyMs += Now - c0;
            order.ReadyMs = Now;
            Log(order, $"Пицца готова (пекарь {cfg.Name})");
            if (!_warehouse.TryAdd(order))
            {
                Log(order, $"Склад полон — пекарь {cfg.Name} ждёт свободное место");
                long w0 = Now;
                _warehouse.Add(order);
                st.BlockedMs += Now - w0;
            }
            order.StoredMs = Now;
            lock (_statLock) _peakWarehouse = Math.Max(_peakWarehouse, _warehouse.Count);
            Log(order, "Пицца передана на склад");
            st.Count++;
        }
    }
    private void CourierWork(CourierConfig cfg, WorkerStats st)
    {
        int capacity = Math.Max(1, cfg.Capacity);
        while (true)
        {
            long t0 = Now;
            if (!_warehouse.TryTake(out PizzaOrder first, Timeout.Infinite)) break;
            st.IdleMs += Now - t0;
            var batch = new List<PizzaOrder> { first };
            while (batch.Count < capacity && _warehouse.TryTake(out PizzaOrder next))
                batch.Add(next);
            foreach (var o in batch) Log(o, $"Курьер {cfg.Name} взял в доставку");
            foreach (var o in batch)
            {
                long d0 = Now;
                Thread.Sleep(_cfg.DeliveryMs);
                st.BusyMs += Now - d0;
                o.DeliveredMs = Now;
                o.Free = o.DeliveredMs - o.CreatedMs > _cfg.FreeThresholdMs;
                Log(o, o.Free
                    ? $"Доставлен (курьер {cfg.Name}) — слишком долго, пицца БЕСПЛАТНО"
                    : $"Доставлен (курьер {cfg.Name}), заказ выполнен");
                st.Count++;
            }
        }
    }
    private void Analyze(List<WorkerStats> bakers, List<WorkerStats> couriers)
    {
        double total = Math.Max(1, Now);
        int n = _orders.Count;
        int free = _orders.Count(o => o.Free);
        double avgTime = _orders.Average(o => o.DeliveredMs - o.CreatedMs);
        double avgWait = _orders.Average(o => o.TakenMs - o.CreatedMs);
        double lateRate = (double)free / n;
        double bakerBusy = bakers.Sum(b => b.BusyMs) / (bakers.Count * total);
        double bakerIdle = bakers.Sum(b => b.IdleMs) / (bakers.Count * total);
        double bakerBlocked = bakers.Sum(b => b.BlockedMs) / (bakers.Count * total);
        double courierBusy = couriers.Sum(c => c.BusyMs) / (couriers.Count * total);
        double courierIdle = couriers.Sum(c => c.IdleMs) / (couriers.Count * total);
        Console.WriteLine("=== Итоги смены ===");
        Console.WriteLine($"Всего заказов: {n}, бесплатных (опоздание): {free} ({lateRate:P0})");
        Console.WriteLine($"Среднее время выполнения: {avgTime:F0} мс, среднее ожидание в очереди: {avgWait:F0} мс");
        Console.WriteLine($"Пик заполнения склада: {_peakWarehouse} из {_cfg.WarehouseSize}");
        Console.WriteLine("Пекари:");
        foreach (var b in bakers)
            Console.WriteLine($"  {b.Name}: пицц {b.Count}, готовил {b.BusyMs / total:P0}, простаивал {b.IdleMs / total:P0}, ждал склад {b.BlockedMs / total:P0}");
        Console.WriteLine("Курьеры:");
        foreach (var c in couriers)
            Console.WriteLine($"  {c.Name}: доставил {c.Count}, в пути {c.BusyMs / total:P0}, ждал пиццы {c.IdleMs / total:P0}");
        Console.WriteLine("\n=== Рекомендации владельцу ===");
        var advice = new List<string>();
        bool storageFull = _peakWarehouse >= _cfg.WarehouseSize && bakerBlocked > 0.05;
        if (storageFull)
        {
            if (courierBusy > 0.7) advice.Add("Нанять курьера: склад забит, курьеры загружены, пекари ждут место.");
            else advice.Add("Расширить склад: пекари часто ждут свободное место.");
        }
        if (lateRate > 0.1 && !storageFull && (bakerBusy > 0.75 || avgWait > _cfg.FreeThresholdMs / 3.0))
            advice.Add("Нанять пекаря: заказы долго ждут в очереди, пекари загружены.");
        if (lateRate > 0.1 && courierBusy > 0.8 && !advice.Any(a => a.StartsWith("Нанять курьера")))
            advice.Add("Нанять курьера: курьеры загружены, есть опоздания.");
        if (lateRate <= 0.1 && bakerIdle > 0.5 && bakers.Count > 1)
        {
            var weakest = bakers.OrderBy(b => b.Count).First();
            advice.Add($"Уволить пекаря ({weakest.Name}): пекари простаивают более половины смены.");
        }
        if (lateRate <= 0.1 && courierIdle > 0.6 && couriers.Count > 1)
        {
            var weakest = couriers.OrderBy(c => c.Count).First();
            advice.Add($"Уволить курьера ({weakest.Name}): курьеры большую часть времени ждут пиццы.");
        }
        if (lateRate <= 0.1 && bakerIdle > 0.4 && courierIdle > 0.4)
            advice.Add("Увеличить количество заказов: мощности пиццерии используются не полностью.");
        if (advice.Count == 0) advice.Add("Работа сбалансирована, изменений не требуется.");
        foreach (var a in advice) Console.WriteLine(" - " + a);
    }
}
public static class Program
{
    private static double ReadDouble(string prompt)
    {
        while (true)
        {
            Console.Write($"{prompt}: ");
            string s = Console.ReadLine();
            if (s != null && double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v > 0)
                return v;
            Console.WriteLine("Введите положительное число.");
        }
    }
    private static int ReadInt(string prompt)
    {
        while (true)
        {
            Console.Write($"{prompt}: ");
            if (int.TryParse(Console.ReadLine()?.Trim(), out int v) && v > 0) return v;
            Console.WriteLine("Введите положительное целое число.");
        }
    }
    private static PizzeriaConfig ReadConfig()
    {
        var cfg = new PizzeriaConfig();
        int n = ReadInt("Количество пекарей N");
        for (int i = 1; i <= n; i++)
            cfg.Bakers.Add(new BakerConfig
            {
                Name = $"Пекарь{i}",
                Experience = ReadDouble($"  Опыт пекаря {i} (множитель скорости, 1 = обычный, 2 = вдвое быстрее)")
            });
        int m = ReadInt("Количество курьеров M");
        for (int i = 1; i <= m; i++)
            cfg.Couriers.Add(new CourierConfig
            {
                Name = $"Курьер{i}",
                Capacity = ReadInt($"  Вместимость багажника курьера {i}")
            });
        cfg.WarehouseSize = ReadInt("Размер склада T");
        cfg.OrdersCount = ReadInt("Количество заказов за смену");
        cfg.OrderIntervalMs = ReadInt("Интервал между заказами, мс");
        cfg.BaseCookMs = ReadInt("Время приготовления при опыте 1, мс");
        cfg.DeliveryMs = ReadInt("Время доставки одной пиццы, мс");
        cfg.FreeThresholdMs = ReadInt("Порог бесплатной пиццы (дольше — бесплатно), мс");
        return cfg;
    }
    public static void Run()
    {
        Console.OutputEncoding = Encoding.UTF8;
        PizzeriaConfig cfg = ReadConfig();
        new PizzeriaSimulation(cfg).Run();
    }
}
