using Android.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using System;
using AndroidX.AppCompat.App;
using Xamarin.Essentials;
using PiThermostat.Utils;

namespace PiThermostat
{
    [Activity(Label = "Settings", Theme ="@style/AppTheme")]
    public class SettingsActivity : AppCompatActivity
    {
        private EditText    inputPassword;
        private EditText    inputURL;
        private Button      buttonSave;

        private Server      server;
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_settings);
            
            SupportActionBar.SetDisplayHomeAsUpEnabled(true);

            inputPassword   = FindViewById<EditText>(Resource.Id.inputPassword);
            inputURL        = FindViewById<EditText>(Resource.Id.inputURL);
            buttonSave      = FindViewById<Button>(Resource.Id.buttonSave);
            server          = Server.Instance;

            inputPassword.Text  = Preferences.Get("password", "");
            inputURL.Text       = Preferences.Get("url", "");

            buttonSave.Click += OnSaveButton;
        }
        public void OnSaveButton(object o, EventArgs e)
        {

            Preferences.Set("password", inputPassword.Text);
            server.Password = inputPassword.Text;
            string url = inputURL.Text;

            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                Preferences.Set("url", url);
                server.Url = url;
            }
            else
            {
                Preferences.Set("url", "http://" + url);
                server.Url = url;
            }
        }
        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            if(item.ItemId == Android.Resource.Id.Home)
            {
                buttonSave.Click -= OnSaveButton;
                Finish();
                return true;
            }
            else
                return base.OnOptionsItemSelected(item);
        }

        
    }
}