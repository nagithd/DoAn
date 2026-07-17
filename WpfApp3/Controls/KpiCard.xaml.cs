using System.Windows;
using System.Windows.Controls;

namespace WpfApp3.Controls
{
    public partial class KpiCard : UserControl
    {
        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            "Label", typeof(string), typeof(KpiCard), new PropertyMetadata("Label", OnLabelChanged));

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            "Value", typeof(string), typeof(KpiCard), new PropertyMetadata("0", OnValueChanged));

        public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
            "Icon", typeof(string), typeof(KpiCard), new PropertyMetadata("📊", OnIconChanged));

        public static readonly DependencyProperty TrendProperty = DependencyProperty.Register(
            "Trend", typeof(string), typeof(KpiCard), new PropertyMetadata(string.Empty, OnTrendChanged));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public string Icon
        {
            get => (string)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public string Trend
        {
            get => (string)GetValue(TrendProperty);
            set => SetValue(TrendProperty, value);
        }

        public KpiCard()
        {
            InitializeComponent();
        }

        private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KpiCard)d).LabelBlock.Text = e.NewValue as string ?? string.Empty;
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KpiCard)d).ValueBlock.Text = e.NewValue as string ?? string.Empty;
        }

        private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KpiCard)d).IconBlock.Text = e.NewValue as string ?? string.Empty;
        }

        private static void OnTrendChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((KpiCard)d).TrendBlock.Text = e.NewValue as string ?? string.Empty;
        }
    }
}
