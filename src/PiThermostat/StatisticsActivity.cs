using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using AndroidX.AppCompat.App;
using Microcharts.Droid;
using SkiaSharp;
using PiThermostat.Utils;
using System.Threading.Tasks;
using Microcharts;


namespace PiThermostat
{
    [Activity(Label = "Statistics")]
    public class StatisticsActivity : AppCompatActivity
    {
        
        private EditText    dateStartDate;
        private EditText    dateEndDate;
        private TextView    textAverageTemp;
        private TextView    textAverageStateDuration;
        private Button      buttonRefresh;
        private ChartView   chartTemp;
        private ChartView   chartStateDuration;
         
        private Server s = Server.Instance;

        private SKColor chartColor = SKColor.Parse("#fc670a");

        private EventHandler dateStartHandler;
        private EventHandler dateEndHandler;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_statistics);

            SupportActionBar.SetDisplayHomeAsUpEnabled(true);


            dateStartDate               = (EditText)FindViewById(Resource.Id.dateStartDate);
            dateEndDate                 = (EditText)FindViewById(Resource.Id.dateEndDate);
            textAverageTemp             = (TextView)FindViewById(Resource.Id.textAverageTemp);
            textAverageStateDuration    = (TextView)FindViewById(Resource.Id.textAverageStateDuration);
            buttonRefresh               = (Button)FindViewById(Resource.Id.buttonRefresh);
            chartTemp                   = (ChartView)FindViewById(Resource.Id.chartTemp);
            chartStateDuration          = (ChartView)FindViewById(Resource.Id.chartStateDuration);

            dateStartHandler = (sender, eventArgs) =>
            {
                var dateTimeNow = DateTime.Now;
                DatePickerDialog datePicker = new DatePickerDialog(this, (sender, e) =>
                {
                    dateStartDate.Text = new DateTime(e.Year, e.Month + 1, e.DayOfMonth).ToString("yyyy-MM-dd");
                }, dateTimeNow.Year, dateTimeNow.Month - 1, dateTimeNow.Day);
                datePicker.Show();
            };

            dateEndHandler = (sender, eventArgs) =>
            {
                var dateTimeNow = DateTime.Now;
                DatePickerDialog datePicker = new DatePickerDialog(this, (sender, e) =>
                {
                    dateEndDate.Text = new DateTime(e.Year, e.Month + 1, e.DayOfMonth).ToString("yyyy-MM-dd");
                }, dateTimeNow.Year, dateTimeNow.Month - 1, dateTimeNow.Day);
                datePicker.Show();
            };

            dateStartDate.Click += dateStartHandler;
            dateEndDate.Click   += dateEndHandler;
            buttonRefresh.Click += OnRefreshButton;

            RefreshCharts();
        }

        private void RefreshCharts(string startDate = "24h", string endDate = "")
        {
            float labelSize = 32;
            float pointSize = 0;
            float margin    = 20;

            Task.Run(async () =>
            {
                TemperaturePoint[] temperaturePoints = await s.GetTemperatures(startDate, endDate);

                List<ChartEntry> entries = new List<ChartEntry>();

                float minVal = temperaturePoints[0].value;
                float maxVal = temperaturePoints[0].value;

                foreach(TemperaturePoint t in temperaturePoints)
                {
                    if (t.Equals(temperaturePoints.First()))
                        entries.Add(new ChartEntry(t.value) { Color = chartColor, Label = t.date, ValueLabel = t.value.ToString() });
                    else if (t.Equals(temperaturePoints.Last()))
                        entries.Add(new ChartEntry(t.value) { Color = chartColor, Label = t.date, ValueLabel = t.value.ToString() });
                    else
                        entries.Add(new ChartEntry(t.value) { Color = chartColor });

                    minVal = t.value < minVal ? t.value : minVal;
                    maxVal = t.value > maxVal ? t.value : maxVal;
                }

                RunOnUiThread(() =>
                {
                    chartTemp.Chart = new LineChart { Entries = entries, MinValue = minVal - 0.5f, MaxValue = maxVal + 0.5f, Margin = margin, LabelTextSize = labelSize, PointSize = pointSize, 
                                                        ValueLabelOrientation = Microcharts.Orientation.Vertical, LabelOrientation = Microcharts.Orientation.Vertical };
                });

            });

            Task.Run(async () =>
            {
                float average = await s.GetTempAverage(startDate, endDate);

                RunOnUiThread(() =>
                {
                    textAverageTemp.Text = "Average temperature: " + ((float)((int)(average*100))/100).ToString();
                });
            });

            Task.Run(async () =>
            {
                StatePoint[] statePoints = await s.GetStates("on", startDate, endDate);

                List<ChartEntry> entries = new List<ChartEntry>();

                float minVal = statePoints[0].duration;
                float maxVal = statePoints[0].duration;

                foreach (StatePoint t in statePoints)
                {
                    if (t.Equals(statePoints.First()))
                        entries.Add(new ChartEntry(t.duration) { Color = chartColor, Label = t.date, ValueLabel = ((int)(t.duration / 3600)).ToString() + "h " + ((int)((t.duration / 60) % 60)).ToString() + "m" });
                    else if (t.Equals(statePoints.Last()))
                        entries.Add(new ChartEntry(t.duration) { Color = chartColor, Label = t.date, ValueLabel = ((int)(t.duration / 3600)).ToString() + "h " + ((int)((t.duration / 60) % 60)).ToString() + "m" });
                    else
                        entries.Add(new ChartEntry(t.duration) { Color = chartColor });

                    minVal = t.duration < minVal ? t.duration : minVal;
                    maxVal = t.duration > maxVal ? t.duration : maxVal;
                }

                RunOnUiThread(() =>
                {
                    chartStateDuration.Chart = new LineChart { Entries = entries, MinValue = minVal - 0.5f, MaxValue = maxVal + 0.5f, Margin = margin, LabelTextSize = labelSize, PointSize = pointSize, 
                                                                ValueLabelOrientation = Microcharts.Orientation.Vertical, LabelOrientation = Microcharts.Orientation.Vertical, };
                });

            });


            Task.Run(async () =>
            {
                float average = await s.GetStateAverage(startDate, endDate);

                RunOnUiThread(() =>
                {
                    textAverageStateDuration.Text = "Average duration: " + ((int)(average / 3600)).ToString() + "h " + ((int)((average / 60) % 60)).ToString() +"m";
                });
            });

        }

        private void OnRefreshButton(object o, EventArgs e)
        {
            string start    = dateStartDate.Text;
            string end      = dateEndDate.Text;

            RefreshCharts(start, end);

        }
        
        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            if(item.ItemId == Android.Resource.Id.Home)
            {
                dateStartDate.Click -= dateStartHandler;
                dateEndDate.Click   -= dateEndHandler;
                buttonRefresh.Click -= OnRefreshButton;
                Finish();
                return true;
            }
            else
                return base.OnOptionsItemSelected(item);
        }
    }
}