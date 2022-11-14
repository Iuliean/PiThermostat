using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Views;
using AndroidX.AppCompat.App;
using Android.Content;
using Android.Widget;
using PiThermostat.Utils;
using System;
using Xamarin.Essentials;
using System.Threading.Tasks;
using Android.Graphics;
using System.Net.Http;
using System.Globalization;
using System.Net;

namespace PiThermostat
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", Icon = "@drawable/ic_display", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        private Server server = Server.Instance;
        private TextView textTemperature;
        private EditText inputMinTemp;
        private EditText inputMaxTemp;
        private Button   buttonSubmit;

        private readonly object _update = new object();
        private bool            update;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            Server connection = Server.Instance;

            textTemperature = (TextView)FindViewById(Resource.Id.textTemperature);
            inputMinTemp    = (EditText)FindViewById(Resource.Id.inputMinTemp);
            inputMaxTemp    = (EditText)FindViewById(Resource.Id.inputMaxTemp);
            buttonSubmit    = (Button)FindViewById(Resource.Id.buttonSubmit);
            update          = true;

            server.Url = Preferences.Get("url", "http://localhost");
            server.Password = Preferences.Get("password", "");

            server.SetAuthCallBack((HttpStatusCode code) =>
            {
                switch(code)
                {
                    case HttpStatusCode.Unauthorized:
                        {
                            RunOnUiThread(() =>
                            {
                                Toast.MakeText(this, "Unauthorized access", ToastLength.Short).Show();
                            });
                            return;
                        }
                    case HttpStatusCode.NotFound:
                        {
                            RunOnUiThread(() =>
                            {
                                Toast.MakeText(this, "404 Page does not exist", ToastLength.Short).Show();
                            });
                            return;
                        }
                    case HttpStatusCode.RequestTimeout:
                        {
                            RunOnUiThread(() =>
                            {
                                Toast.MakeText(this, "408 Request timed out", ToastLength.Short).Show();
                            });
                            return;
                        }
                    default:
                        {
                            RunOnUiThread(() =>
                            {
                                Toast.MakeText(this, (int)code + " unhandled error code", ToastLength.Short).Show();
                            });
                            return;
                        }
                }
            });

            buttonSubmit.Click += OnSubmitButton;

            var updateTask = Task.Run(UpdateLoop);
        }

        protected override void OnPause()
        {
            base.OnPause();
            lock(_update)
            {
                update = false;
            }
        }

        protected override void OnResume()
        {
            base.OnResume();
            lock(_update)
            {
                update = true;
            }
            UpdateLoop();
        }
        private async Task UpdateLoop()
        {
            while(true)
            {
                lock(_update)
                {
                    if (!update)
                        return;
                }
                try
                {
                    ThermostatState state = await server.GetParams();
                    
                    if (state != null)
                    {
                        textTemperature.Text = state.temp.ToString();
                        inputMinTemp.Hint = state.minTemp.ToString();
                        inputMaxTemp.Hint = state.maxTemp.ToString();

                        if (state.state == "ON")
                            textTemperature.SetTextColor(Color.Green);
                        else
                            textTemperature.SetTextColor(Color.Red);

                        await Task.Delay(1000);
                    }
                    else
                    {
                        RunOnUiThread(() =>
                        {
                            textTemperature.Text = "No Connection";
                            textTemperature.SetTextColor(new Color(100, 100, 100));
                        });
                        await Task.Delay(5000);
                    }
                }
                catch (HttpRequestException)
                {
                    RunOnUiThread(() =>
                    {
                        Toast.MakeText(this, "No such host is known", ToastLength.Short).Show();
                    });

                    await Task.Delay(5000);
                }
            }
        }

        private void OnSubmitButton(object o, EventArgs e)
        {

            float min;
            float max;

            if (!float.TryParse(inputMinTemp.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out min))
                min = float.MinValue;
            if (!float.TryParse(inputMaxTemp.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out max))
                max = float.MinValue;

            var setParamsTask = server.SetParams(new Parameters(min, max));

            inputMinTemp.Text = "";
            inputMaxTemp.Text = "";
            return;
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.mainMenu, menu);
            return true;
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch(item.ItemId)
            {
                case Resource.Id.menuButtonShutdown:
                    {
                        var shutdownTask = server.ShutdownRequest();
                        return true;
                    }
                case Resource.Id.menuButtonSettings:
                    {
                        Intent nextActivity = new Intent(this, typeof(SettingsActivity));
                        StartActivity(nextActivity);
                        return true;
                    }
                case Resource.Id.menuButtonStatistics:
                    {
                        Intent nextActivity = new Intent(this, typeof(StatisticsActivity));
                        StartActivity(nextActivity);
                        return true;
                    }
            }
            return base.OnOptionsItemSelected(item);
        }
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}