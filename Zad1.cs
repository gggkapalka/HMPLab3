using System.Text;
namespace Shop;
public class Product
{
    public string Name { get; }
    public decimal Price { get; }
    public int Stock { get; set; }
    public Product(string name, decimal price, int stock)
    {
        Name = name;
        Price = price;
        Stock = stock;
    }
}
public class Customer
{
    public string Name { get; }
    public decimal LoyaltyPercent { get; }
    public Customer(string name, decimal loyaltyPercent = 0)
    {
        Name = name;
        LoyaltyPercent = loyaltyPercent;
    }
}
public class OrderItem
{
    public Product Product;
    public int Quantity;
    public OrderItem(Product product, int quantity)
    {
        Product = product;
        Quantity = quantity;
    }
    public decimal Sum => Product.Price * Quantity;
}
public class Order
{
    private static int _nextId = 1;
    public int Id { get; }
    public Customer Customer { get; }
    public List<OrderItem> Items { get; } = new();
    public bool IsPlaced { get; set; }
    public Order(Customer customer)
    {
        Id = _nextId++;
        Customer = customer;
    }
    public void AddItem(Product product) => AddItem(product, 1);
    public void AddItem(Product product, int quantity)
    {
        if (quantity <= 0) throw new ArgumentException("Количество должно быть > 0");
        var existing = Items.Find(i => i.Product == product);
        if (existing != null) existing.Quantity += quantity;
        else Items.Add(new OrderItem(product, quantity));
    }
    public void AddItems(params Product[] products)
    {
        foreach (var p in products) AddItem(p);
    }
    public void AddItems(int quantityEach, params Product[] products)
    {
        foreach (var p in products) AddItem(p, quantityEach);
    }
    public static bool ChangeQuantity(ref int quantity, int newQuantity, in Product product)
    {
        if (newQuantity < 0 || newQuantity > product.Stock) return false;
        quantity = newQuantity;
        return true;
    }
    public bool ChangeQuantity(Product product, int newQuantity)
    {
        var item = Items.Find(i => i.Product == product);
        if (item == null) return false;
        bool ok = ChangeQuantity(ref item.Quantity, newQuantity, in product);
        if (ok && item.Quantity == 0) Items.Remove(item);
        return ok;
    }
    public decimal CalculateTotal() => Items.Sum(i => i.Sum);
    public decimal CalculateDiscount() => CalculateDiscount(CalculateTotal());
    public decimal CalculateDiscount(decimal total) => CalculateDiscount(total, Customer);
    public decimal CalculateDiscount(decimal total, Customer customer)
    {
        decimal percent = customer.LoyaltyPercent;
        if (total >= 5000) percent += 10;
        else if (total >= 2000) percent += 5;
        percent = Math.Min(percent, 30);
        return Math.Round(total * percent / 100, 2);
    }
    public decimal CalculateFinalTotal()
    {
        decimal total = CalculateTotal();
        return total - CalculateDiscount(total);
    }
    public string GetReceipt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("==============================================");
        sb.AppendLine($"  ЧЕК по заказу {Id}");
        sb.AppendLine($"  Покупатель: {Customer.Name}");
        sb.AppendLine("----------------------------------------------");
        foreach (var i in Items)
            sb.AppendLine($"  {i.Product.Name,-16} {i.Quantity,3} x {i.Product.Price,9:F2} = {i.Sum,10:F2}");
        sb.AppendLine("----------------------------------------------");
        decimal total = CalculateTotal();
        decimal discount = CalculateDiscount(total);
        sb.AppendLine($"  Сумма:               {total,20:F2}");
        sb.AppendLine($"  Скидка:              {discount,20:F2}");
        sb.AppendLine($"  ИТОГО К ОПЛАТЕ:      {total - discount,20:F2}");
        sb.AppendLine("==============================================");
        return sb.ToString();
    }
}
public class OrderManager
{
    private readonly List<Product> _catalog = new();
    private readonly List<Order> _orders = new();
    public void AddProduct(Product p) => _catalog.Add(p);
    public Product FindProduct(string name) =>
        _catalog.Find(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    public bool IsAvailable(Product p) => p.Stock > 0;
    public bool IsAvailable(Product p, int quantity) => p.Stock >= quantity;
    public bool IsAvailable(string name)
    {
        var p = FindProduct(name);
        return p != null && p.Stock > 0;
    }
    public bool IsAvailable(Order order, out string problem)
    {
        foreach (var item in order.Items)
        {
            if (!IsAvailable(item.Product, item.Quantity))
            {
                problem = $"Недостаточно товара «{item.Product.Name}»: нужно {item.Quantity}, на складе {item.Product.Stock}";
                return false;
            }
        }
        problem = "Все товары в наличии";
        return true;
    }
    public static void PrintProductInfo(in Product p) =>
        Console.WriteLine($"  {p.Name,-16} цена: {p.Price,9:F2}  на складе: {p.Stock}");
    public void PrintCatalog()
    {
        Console.WriteLine("Каталог:");
        foreach (var p in _catalog) PrintProductInfo(in p);
    }
    public Order CreateOrder(Customer customer, params Product[] products)
    {
        var order = new Order(customer);
        order.AddItems(products);
        _orders.Add(order);
        return order;
    }
    public bool PlaceOrder(Order order, out decimal finalTotal, out string message)
    {
        finalTotal = 0;
        if (order.IsPlaced) { message = "Заказ уже оформлен"; return false; }
        if (order.Items.Count == 0) { message = "Заказ пуст"; return false; }
        if (!IsAvailable(order, out message)) return false;
        foreach (var item in order.Items) item.Product.Stock -= item.Quantity;
        order.IsPlaced = true;
        finalTotal = order.CalculateFinalTotal();
        message = $"Заказ № {order.Id} успешно оформлен";
        return true;
    }
    public bool PlaceOrder(Order order, out string message) =>
        PlaceOrder(order, out _, out message);
}
public static class Program
{
    public static void Run()
    {
        Console.OutputEncoding = Encoding.UTF8;
        var manager = new OrderManager();
        var laptop = new Product("Ноутбук", 55000m, 3);
        var mouse = new Product("Мышь", 800m, 10);
        var keyboard = new Product("Клавиатура", 2500m, 5);
        var monitor = new Product("Монитор", 18000m, 1);
        manager.AddProduct(laptop);
        manager.AddProduct(mouse);
        manager.AddProduct(keyboard);
        manager.AddProduct(monitor);
        manager.PrintCatalog();
        var customer = new Customer("Иван Петров", 3);
        Console.WriteLine("\n--- Заказ 1: AddItems(params) ---");
        Order order = manager.CreateOrder(customer, mouse, keyboard);
        order.AddItem(mouse);
        order.AddItem(keyboard, 2);
        order.AddItems(2, monitor);
        Console.WriteLine($"Сумма: {order.CalculateTotal():F2}, скидка: {order.CalculateDiscount():F2}");
        Console.WriteLine("\n--- Попытка оформить (монитора только 1) ---");
        bool ok = manager.PlaceOrder(order, out decimal total, out string msg);
        Console.WriteLine($"Результат: {ok}. {msg}");
        Console.WriteLine("\n--- Изменяем количество монитора на 1 (ref) ---");
        Console.WriteLine(order.ChangeQuantity(monitor, 1) ? "Изменено" : "Не удалось изменить");
        Console.WriteLine(order.ChangeQuantity(mouse, 100) ? "Изменено" : "Мышей 100 нет на складе — отказ");
        Console.WriteLine("\n--- Оформление ---");
        ok = manager.PlaceOrder(order, out total, out msg);
        Console.WriteLine($"Результат: {ok}. {msg}. К оплате: {total:F2}");
        Console.WriteLine();
        Console.WriteLine(order.GetReceipt());
        Console.WriteLine("Проверка наличия (перегрузки):");
        Console.WriteLine($"  Монитор есть? {manager.IsAvailable(monitor)}");
        Console.WriteLine($"  Мышей 5 шт.? {manager.IsAvailable(mouse, 5)}");
        Console.WriteLine($"  \"Ноутбук\" есть? {manager.IsAvailable("Ноутбук")}");
        Console.WriteLine();
        manager.PrintCatalog();
    }
}
