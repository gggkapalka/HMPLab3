using System.Text;

namespace PizzeriaApp;

public static class Program
{
    public static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("ВЫБОР ЗАДАЧИ");
            Console.WriteLine("1 Магазин (Задача 1)");
            Console.WriteLine("2 Пиццерия (Задача 2)");
            Console.WriteLine("3 Массив (Задача 3)");
            Console.WriteLine("0 Выход");
            Console.Write("Введите номер: ");

            string? input = Console.ReadLine()?.Trim();
            if (!int.TryParse(input, out int choice))
            {
                Console.WriteLine("Нужно ввести целое число.");
                continue;
            }
            try
            {
                switch (choice)
                {
                    case 1: Shop.Program.Run(); break;
                    case 2: Pizzeria.Program.Run(); break;
                    case 3: ArrayTask.Program.Run(); break;
                    case 0:
                        Console.WriteLine("Выход.");
                        return;
                    default:
                        Console.WriteLine("Нет такой задачи. Введите 0–3.");
                        continue;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Ошибка выполнения задачи]: {ex.Message}");
            }
            Console.WriteLine();
            Console.WriteLine("Нажмите Enter, чтобы вернуться в меню...");
            Console.ReadLine();
        }
    }
}