using System;
using System.Globalization;
using System.Windows;

namespace MulticastDelegate
{
    public partial class MainWindow : Window
    {
        private readonly DelegateLab _lab = new DelegateLab();

        public MainWindow()
        {
            InitializeComponent();

            // Число з методичних вказівок (12345.6789) у форматі поточної культури,
            // щоб TryParse спрацьовував і з комою, і з крапкою як роздільником
            NumberTextBox.Text = (12345.6789).ToString(CultureInfo.CurrentCulture);

            // Логіка не знає про WPF - просто підписуємось на її повідомлення.
            // Повідомлення приходять з робочих потоків, тому переходимо в UI-потік через Dispatcher.
            _lab.Message += OnLabMessage;
        }

        private void OnLabMessage(string text)
        {
            Dispatcher.BeginInvoke(new Action(delegate ()
            {
                LogListBox.Items.Add(text);
                LogListBox.ScrollIntoView(LogListBox.Items[LogListBox.Items.Count - 1]);
            }));
        }

        private bool TryReadNumber(out double number)
        {
            string text = NumberTextBox.Text.Trim();

            // Приймаємо і "12345,6789" (поточна культура), і "12345.6789"
            return double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out number)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            double number;
            if (!TryReadNumber(out number))
            {
                MessageBox.Show("Введіть коректне число.", "Помилка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool parallel = ParallelRadio.IsChecked == true;

            RunButton.IsEnabled = false;
            StatusText.Text = "Виконується у потоці...";
            _lab.ResetCounter();

            _lab.RunAsync(number, parallel, delegate ()
            {
                // викликається з робочого потоку - повертаємось у UI-потік
                Dispatcher.BeginInvoke(new Action(delegate ()
                {
                    RunButton.IsEnabled = true;
                    StatusText.Text = "Готово";
                }));
            });
        }

        private void MethodGroupButton_Click(object sender, RoutedEventArgs e)
        {
            _lab.DemoMethodGroupConversion();
        }

        private void CoContraButton_Click(object sender, RoutedEventArgs e)
        {
            _lab.DemoCoContraVariance();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            LogListBox.Items.Clear();
        }
    }
}