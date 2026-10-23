using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace MulticastDelegate
{
    // ===== Делегати =====

    // Багатоадресний делегат з методичних вказівок
    public delegate void FormatNumber(double number);

    // Делегат для демонстрації групового перетворення методів
    public delegate string StrMod(string s);

    // Класи та делегат для демонстрації коваріантності / контраваріантності
    public class X
    {
        public int Val;
    }

    public class Y : X { }

    // Повертає X, приймає Y
    public delegate X ChangeIt(Y obj);

    /// <summary>
    /// Уся логіка лабораторної роботи 3: делегати, груповe перетворення методів,
    /// багатоадресні делегати, коваріантність/контраваріантність та потоки.
    /// Не містить жодного WPF-коду: результати передаються назовні через подію Message.
    /// </summary>
    public class DelegateLab
    {
        // Об'єкт синхронізації: гарантує, що рядки з різних потоків не "змішаються"
        private readonly object _sync = new object();

        // Лічильник викликів (змінюється з кількох потоків -> Interlocked)
        private int _callCount;

        /// <summary>Подія, через яку логіка повідомляє інтерфейс про результат.</summary>
        public event Action<string> Message;

        /// <summary>Кількість виконаних методів-делегатів (потокобезпечне читання).</summary>
        public int CallCount
        {
            get { return Thread.VolatileRead(ref _callCount); }
        }

        public void ResetCounter()
        {
            Interlocked.Exchange(ref _callCount, 0);
        }

        private void Log(string text)
        {
            lock (_sync)
            {
                Action<string> handler = Message;
                if (handler != null)
                    handler(text);
            }
        }

        // ===== Методи-делегати (завдання 3 методичних вказівок) =====

        private void FormatNumberAsCurrency(double number)
        {
            Interlocked.Increment(ref _callCount);
            Log(string.Format("[потік {0}] A Currency: {1:C}",
                Thread.CurrentThread.ManagedThreadId, number));
        }

        private void FormatNumberWithCommas(double number)
        {
            Interlocked.Increment(ref _callCount);
            Log(string.Format("[потік {0}] With Commas: {1:N}",
                Thread.CurrentThread.ManagedThreadId, number));
        }

        private void FormatNumberWithTwoPlaces(double number)
        {
            Interlocked.Increment(ref _callCount);
            Log(string.Format("[потік {0}] With 3 places: {1:.###}",
                Thread.CurrentThread.ManagedThreadId, number));
        }

        // ===== Побудова багатоадресного делегата (завдання 2 методичних вказівок) =====

        public FormatNumber BuildMulticastDelegate()
        {
            // групове перетворення методів - без new FormatNumber(...)
            FormatNumber format = FormatNumberAsCurrency;
            format += FormatNumberWithCommas;
            format += FormatNumberWithTwoPlaces;
            return format;
        }

        // ===== Виконання у потоках =====

        /// <summary>
        /// Обчислює функцію користувача (форматування числа) в окремому потоці.
        /// parallel = false: один робочий потік викликає весь делегат послідовно;
        /// parallel = true:  кожен метод з ланцюжка викликів виконується у власному потоці.
        /// onFinished викликається з робочого потоку після завершення всієї роботи.
        /// </summary>
        public Thread RunAsync(double number, bool parallel, Action onFinished)
        {
            Thread worker = new Thread(delegate ()
            {
                try
                {
                    FormatNumber format = BuildMulticastDelegate();

                    if (!parallel)
                    {
                        Log(string.Format("--- Послідовний режим, число = {0} ---", number));
                        format(number);
                    }
                    else
                    {
                        Log(string.Format("--- Паралельний режим, число = {0} ---", number));
                        Delegate[] methods = format.GetInvocationList();
                        List<Thread> threads = new List<Thread>();

                        foreach (Delegate d in methods)
                        {
                            FormatNumber single = (FormatNumber)d; // власна копія для замикання
                            Thread t = new Thread(delegate () { single(number); });
                            t.IsBackground = true;
                            threads.Add(t);
                            t.Start();
                        }

                        foreach (Thread t in threads)
                            t.Join(); // чекаємо завершення всіх потоків
                    }

                    Log(string.Format("Всього викликів методів: {0}", CallCount));
                }
                catch (Exception ex)
                {
                    Log("Помилка: " + ex.Message);
                }
                finally
                {
                    if (onFinished != null)
                        onFinished();
                }
            });

            worker.IsBackground = true;
            worker.Start();
            return worker;
        }

        // ===== Групове перетворення методів (теоретичні відомості) =====

        private static string ReplaceSpaces(string s)
        {
            return s.Replace(' ', '-');
        }

        private static string RemoveSpaces(string s)
        {
            return s.Replace(" ", "");
        }

        private static string Reverse(string s)
        {
            char[] chars = s.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        public void DemoMethodGroupConversion()
        {
            Log("--- Групове перетворення методів ---");

            StrMod strOp = ReplaceSpaces; // без new StrMod(...)
            string str = strOp("This is a simple test.");
            Log("ReplaceSpaces: " + str);

            strOp = RemoveSpaces;
            str = strOp("This is a simple test.");
            Log("RemoveSpaces:  " + str);

            strOp = Reverse;
            str = strOp("This is a simple test.");
            Log("Reverse:       " + str);
        }

        // ===== Коваріантність і контраваріантність =====

        // Приймає X (базовий клас) і повертає X
        private static X IncrA(X obj)
        {
            X temp = new X();
            temp.Val = obj.Val + 1;
            return temp;
        }

        // Приймає Y і повертає Y (похідний клас)
        private static Y IncrB(Y obj)
        {
            Y temp = new Y();
            temp.Val = obj.Val + 1;
            return temp;
        }

        public void DemoCoContraVariance()
        {
            Log("--- Коваріантність та контраваріантність ---");

            Y yob = new Y();

            // Контраваріантність: параметр методу (X) - базовий для параметра делегата (Y)
            ChangeIt change = IncrA;
            X xob = change(yob);
            Log("IncrA (контраваріантність), Xob.Val = " + xob.Val);

            // Коваріантність: метод повертає Y - похідний від X, що повертає делегат
            change = IncrB;
            yob = (Y)change(yob);
            Log("IncrB (коваріантність),     Yob.Val = " + yob.Val);
        }
    }
}