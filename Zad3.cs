using System.Text;
namespace ArrayTask;
public class IntArray
{
    private int[] _data;
    private int _length;
    public int Length => _length;
    public IntArray(int size)
    {
        if (size < 0) throw new ArgumentException("Размер не может быть отрицательным");
        _data = new int[size];
        _length = size;
    }
    public int this[int index]
    {
        get => _data[index];
        set => _data[index] = value;
    }
    public void InputData()
    {
        for (int i = 0; i < _length; i++)
        {
            int value;
            while (true)
            {
                Console.Write($"  a[{i}] = ");
                if (int.TryParse(Console.ReadLine(), out value)) break;
                Console.WriteLine("  Введите целое число.");
            }
            _data[i] = value;
        }
    }
    public void InputDataRandom(in int min, in int max)
    {
        var rnd = new Random();
        for (int i = 0; i < _length; i++)
            _data[i] = rnd.Next(min, max + 1);
    }
    public void InputDataRandom() => InputDataRandom(0, 100);
    public void Print(in int from, in int to)
    {
        if (_length == 0) { Console.WriteLine("(массив пуст)"); return; }
        int start = Math.Max(0, from);
        int end = Math.Min(_length - 1, to);
        if (start > end) { Console.WriteLine("(пустой диапазон)"); return; }
        var sb = new StringBuilder();
        for (int i = start; i <= end; i++)
            sb.Append($"[{i}]={_data[i]}  ");
        Console.WriteLine(sb.ToString().TrimEnd());
    }
    public void Print() => Print(0, _length - 1);
    public List<int> FindValue(in int value, out int count)
    {
        var indexes = new List<int>();
        for (int i = 0; i < _length; i++)
            if (_data[i] == value) indexes.Add(i);
        count = indexes.Count;
        return indexes;
    }
    public void DelValue(in int value, out int removed)
    {
        int newLength = 0;
        for (int i = 0; i < _length; i++)
            if (_data[i] != value) newLength++;
        removed = _length - newLength;
        if (removed == 0) return;
        var result = new int[newLength];
        int k = 0;
        for (int i = 0; i < _length; i++)
            if (_data[i] != value) result[k++] = _data[i];
        _data = result;
        _length = newLength;
    }
    public int FindMax(out int index)
    {
        if (_length == 0) throw new InvalidOperationException("Массив пуст");
        index = 0;
        for (int i = 1; i < _length; i++)
            if (_data[i] > _data[index]) index = i;
        return _data[index];
    }
    public int FindMax() => FindMax(out _);
    public static bool Add(in IntArray a, in IntArray b, out IntArray result)
    {
        if (a.Length != b.Length)
        {
            result = null;
            return false;
        }
        result = new IntArray(a.Length);
        for (int i = 0; i < a.Length; i++)
            result._data[i] = a._data[i] + b._data[i];
        return true;
    }
    public void Sort(ref int swaps)
    {
        for (int i = 0; i < _length - 1; i++)
            for (int j = 0; j < _length - 1 - i; j++)
                if (_data[j] > _data[j + 1])
                {
                    Swap(ref _data[j], ref _data[j + 1]);
                    swaps++;
                }
    }
    public void Sort()
    {
        int swaps = 0;
        Sort(ref swaps);
    }
    private static void Swap(ref int x, ref int y)
    {
        int t = x;
        x = y;
        y = t;
    }
}
public static class Program
{
    private static int ReadInt(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            if (int.TryParse(Console.ReadLine(), out int v)) return v;
            Console.WriteLine("Введите целое число.");
        }
    }
    private static IntArray CreateArray(string title)
    {
        int size;
        do { size = ReadInt($"{title}: размер массива: "); } while (size < 0);
        var arr = new IntArray(size);
        Console.WriteLine("1 — ввод вручную, 2 — случайные числа");
        if (ReadInt("Выбор: ") == 1) arr.InputData();
        else
        {
            int min = ReadInt("Минимум: ");
            int max = ReadInt("Максимум: ");
            if (min > max) (min, max) = (max, min);
            arr.InputDataRandom(in min, in max);
        }
        return arr;
    }
    public static void Run()
    {
        Console.OutputEncoding = Encoding.UTF8;
        IntArray arr = CreateArray("Массив A");
        while (true)
        {
            Console.WriteLine("\n===== МЕНЮ =====");
            Console.WriteLine("1. Вывести весь массив");
            Console.WriteLine("2. Вывести диапазон индексов");
            Console.WriteLine("3. Найти индексы элемента");
            Console.WriteLine("4. Удалить элемент (все вхождения)");
            Console.WriteLine("5. Максимальное значение");
            Console.WriteLine("6. Сложить с другим массивом");
            Console.WriteLine("7. Сортировать по возрастанию");
            Console.WriteLine("8. Заполнить заново");
            Console.WriteLine("0. Выход");
            switch (ReadInt("Выбор: "))
            {
                case 1:
                    arr.Print();
                    break;
                case 2:
                    {
                        int from = ReadInt("С индекса: ");
                        int to = ReadInt("По индекс: ");
                        arr.Print(in from, in to);
                        break;
                    }
                case 3:
                    {
                        int v = ReadInt("Искомое значение: ");
                        var idx = arr.FindValue(in v, out int count);
                        Console.WriteLine(count == 0
                            ? "Не найдено."
                            : $"Найдено {count} шт., индексы: {string.Join(", ", idx)}");
                        break;
                    }
                case 4:
                    {
                        int v = ReadInt("Удаляемое значение: ");
                        arr.DelValue(in v, out int removed);
                        Console.WriteLine($"Удалено элементов: {removed}. Новая длина: {arr.Length}");
                        break;
                    }
                case 5:
                    if (arr.Length == 0) { Console.WriteLine("Массив пуст."); break; }
                    int max = arr.FindMax(out int maxIndex);
                    Console.WriteLine($"Максимум: {max} (индекс {maxIndex})");
                    break;
                case 6:
                    {
                        IntArray other = CreateArray("Массив B");
                        Console.Write("B: "); other.Print();
                        if (IntArray.Add(in arr, in other, out IntArray sum))
                        {
                            Console.Write("A + B: ");
                            sum.Print();
                        }
                        else Console.WriteLine("Длины массивов различаются — сложение невозможно.");
                        break;
                    }
                case 7:
                    {
                        int swaps = 0;
                        arr.Sort(ref swaps);
                        Console.WriteLine($"Отсортировано, обменов: {swaps}");
                        arr.Print();
                        break;
                    }
                case 8:
                    arr = CreateArray("Массив A");
                    break;
                case 0:
                    return;
                default:
                    Console.WriteLine("Неизвестный пункт меню.");
                    break;
            }
        }
    }
}
