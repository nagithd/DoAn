using System.Windows.Controls;

namespace WpfApp3.Controls
{
    /// <summary>
    /// StatisticsCards.xaml の相互作用ロジック
    /// </summary>
    public partial class StatisticsCards : UserControl
    {
        public StatisticsCards()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Update the Good card
        /// </summary>
        public void SetGoodCard(int value, int todayTrend)
        {
            GoodValue.Text = value.ToString();
            GoodTrend.Text = $"↑ {todayTrend} today";
        }

        /// <summary>
        /// Update the Scratch card
        /// </summary>
        public void SetScratchCard(int value, int todayTrend)
        {
            ScratchValue.Text = value.ToString();
            ScratchTrend.Text = $"↑ {todayTrend} today";
        }

        /// <summary>
        /// Update the Dent card
        /// </summary>
        public void SetDentCard(int value, int todayTrend)
        {
            DentValue.Text = value.ToString();
            DentTrend.Text = $"↑ {todayTrend} today";
        }

        /// <summary>
        /// Update the Other card
        /// </summary>
        public void SetOtherCard(int value, int todayTrend)
        {
            OtherValue.Text = value.ToString();
            OtherTrend.Text = $"↑ {todayTrend} today";
        }

        /// <summary>
        /// Update the Total card
        /// </summary>
        public void SetTotalCard(int value, int todayTrend)
        {
            TotalValue.Text = value.ToString();
            TotalTrend.Text = $"↑ {todayTrend} today";
        }

        /// <summary>
        /// Update the Today's Production card
        /// </summary>
        public void SetProductionCard(int value, int todayTrend)
        {
            ProductionValue.Text = value.ToString();
            ProductionTrend.Text = $"↑ {todayTrend} today";
        }

        /// <summary>
        /// Update all cards at once
        /// </summary>
        public void UpdateAllCards(int good, int scratch, int dent, int other, int total, int todaysProduction, 
                                   int goodTrend = 0, int scratchTrend = 0, int dentTrend = 0, 
                                   int otherTrend = 0, int totalTrend = 0, int productionTrend = 0)
        {
            SetGoodCard(good, goodTrend);
            SetScratchCard(scratch, scratchTrend);
            SetDentCard(dent, dentTrend);
            SetOtherCard(other, otherTrend);
            SetTotalCard(total, totalTrend);
            SetProductionCard(todaysProduction, productionTrend);
        }
    }
}
