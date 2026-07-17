using System;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace WpfApp3.Controls
{
    /// <summary>
    /// EventLog.xaml の相互作用ロジック
    /// </summary>
    public partial class EventLog : UserControl
    {
        private ObservableCollection<EventLogItem> _events;

        public EventLog()
        {
            InitializeComponent();
            _events = new ObservableCollection<EventLogItem>();
            EventDataGrid.ItemsSource = _events;
        }

        /// <summary>
        /// Add a new event to the log (appears at top)
        /// </summary>
        public void AddEvent(string time, string eventText, string type, string status)
        {
            var eventItem = new EventLogItem
            {
                Time = time,
                Event = eventText,
                Type = type,
                Status = status
            };

            // Insert at the beginning so latest events appear on top
            _events.Insert(0, eventItem);
        }

        /// <summary>
        /// Add a new event with current timestamp
        /// </summary>
        public void AddEvent(string eventText, string type, string status)
        {
            AddEvent(DateTime.Now.ToString("HH:mm:ss"), eventText, type, status);
        }

        /// <summary>
        /// Clear all events from the log
        /// </summary>
        public void ClearEvents()
        {
            _events.Clear();
        }

        /// <summary>
        /// Get the count of events in the log
        /// </summary>
        public int EventCount => _events.Count;
    }

    /// <summary>
    /// Event log item model
    /// </summary>
    public class EventLogItem
    {
        public string Time { get; set; }
        public string Event { get; set; }
        public string Type { get; set; }
        public string Status { get; set; }
    }
}
