using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Net;
using Newtonsoft.Json;
using System.Threading;


namespace PiThermostat.Utils
{
    public struct ThermostatState
    {
        public float temp;
        public float minTemp;
        public float maxTemp;
        public string state;

        public ThermostatState(float _temperature, float _minTemp, float _maxTemp, string _state)
        {
            temp    = _temperature;
            minTemp = _minTemp;
            maxTemp = _maxTemp;
            state   = _state;
        }
    }

    public struct Parameters
    {
        public float? minTemp;
        public float? maxTemp;

        public Parameters(float _minTemp, float _maxTemp)
        {
            if(_minTemp != float.MinValue)
            {
                minTemp = _minTemp;
            }
            else
            {
                minTemp = null;
            }
            
            if(_maxTemp != float.MinValue)
            {
                maxTemp = _maxTemp;
            }
            else
            {
                maxTemp = null;
            }
        }
    }

    public struct TempAverage
    {
        public float averageTemp;
    }

    public struct TemperaturePoint
    {
        public float value;
        public string time;
        public string date;

        public override string ToString()
        {
            return "Value: " + value + "\nTime: " + time + "\nDate: " + date + "\n";
        }
    }

    public struct StatePoint
    {
        public float duration;
        public bool state;
        public string time;
        public string date;
    }

    public struct StateAverage
    {
        public float averageOnTime;
    }

    sealed class Server
    {
        private string url;
        private string password;

        private Action<int> authCallback;

        private CookieContainer cookies;
        private readonly HttpClient client;

        private static readonly object l = new object();
        private static Server instance = null;

        private Server ()
        {
            url = "";
            password = "";
            SetAuthCallBack(null);

            cookies = new CookieContainer ();
            HttpClientHandler handler = new HttpClientHandler ();
            handler.CookieContainer = cookies;

            client = new HttpClient (handler);
        }

        public static Server Instance
        {
            get
            {
                lock (l)
                {
                    if (instance == null)
                        instance = new Server();
                }
                return instance;
            }
        }

        public void SetAuthCallBack(Action<int> action)
        {
            authCallback = action;
        }

        public string Url
        {
            get { return url; }
            set
            {
                url = value;
            }
        }

        public string Password
        {
            get { return password; }
            set
            {
                SHA256 hash = SHA256.Create();

                Encoding enc = Encoding.UTF8;
                Byte[] result = hash.ComputeHash(enc.GetBytes(value));
                StringBuilder Sb = new StringBuilder();

                foreach (Byte b in result)
                    Sb.Append(b.ToString("x2"));
                    
                password = Sb.ToString();
            }
        }

        private void Error(int code)
        {
            if (authCallback != null)
                authCallback(code);
        }

        private async Task<HttpResponseMessage> RequestWithTimeout(int time, string endpoint, string reqMethod, HttpContent content)
        {
            Task<HttpResponseMessage> responseTask = null;

            if ( reqMethod == "POST")
                responseTask = client.PostAsync(url + endpoint, content);
            else if (reqMethod == "GET")
                responseTask = client.GetAsync(url + endpoint);

            var timeout = Task.Delay(time);

            await Task.WhenAny(timeout, responseTask);

            if (timeout.IsCompleted)
            {
                Error(408);
                return null;
            }

            return responseTask.Result;
        }
        private async Task<bool> Auth()
        {
            MultipartFormDataContent form = new MultipartFormDataContent();
            form.Add(new StringContent(Password), "password");

            HttpResponseMessage response = await RequestWithTimeout(1000, "/auth", "POST", form);

            if (((int)response.StatusCode) == 200)
            {
                cookies.Add(new Uri(url), new Cookie("authToken", cookies.GetCookies(new Uri(url + "/auth"))[0].Value));
                return true;
            }
            
            else
                return false;
        }

        public async Task<ThermostatState?> GetParams()
        {
            HttpResponseMessage response = await RequestWithTimeout(1000, "/getParams", "GET", null);

            if (response == null)
                return null;

            switch ((int)response.StatusCode)
            {
                case 200:
                    {
                        return JsonConvert.DeserializeObject<ThermostatState>(await response.Content.ReadAsStringAsync());
                    }
                case 401:
                    {
                        if (await Auth())
                        {
                            response = await RequestWithTimeout(1000, "/getParams", "GET", null);

                            if (response == null)
                                return null;

                            if (response.IsSuccessStatusCode)
                                return JsonConvert.DeserializeObject<ThermostatState>(await response.Content.ReadAsStringAsync());
                            else
                                Error((int)response.StatusCode);

                            return null;
                        }
                        else
                        {
                            Error(401);
                            return null;
                        }
                    }
                default:
                    {
                        Error((int)response.StatusCode);
                        return null;
                    }
            }
        }

        public async Task SetParams(Parameters parameters)
        {
            string json = JsonConvert.SerializeObject(parameters);

            HttpContent content = new StringContent(json);
            content.Headers.ContentType = new MediaTypeHeaderValue("Application/json");

            HttpResponseMessage response = await RequestWithTimeout(1000, "/setParams", "POST", content);

            if (response == null)
                return;

            switch((int)response.StatusCode)
            {
                case 200:
                    {
                        return;
                    }
                case 401:
                    {
                        if (await Auth())
                        {
                            response = await RequestWithTimeout(1000, "/setParams", "POST", content);

                            if (response == null)
                                return;

                            if (response.IsSuccessStatusCode)
                                return;
                            else
                                Error((int)response.StatusCode);

                            return;
                        }
                        else
                        {
                            Error(401);
                            return;
                        }
                    }
                    default:
                    {
                        Error((int)response.StatusCode);
                        return;
                    }
            }
        }
        public async Task ShutdownRequest()
        {
            HttpResponseMessage response = await RequestWithTimeout(1000, "/shutdown", "GET", null);

            if (response == null)
                return;

            switch((int)response.StatusCode)
            {
                case 200:
                    {
                        return;
                    }
                case 401:
                    {
                        if(await Auth())
                        {
                            response = await RequestWithTimeout(1000, "/shutdown", "GET", null);

                            if (response == null)
                                return;

                            if ((int)response.StatusCode == 200)
                                return;
                            else
                                Error((int)response.StatusCode);

                            return;
                        }
                        else
                        {
                            Error(401);
                            return;
                        }
                    }
                default:
                    {
                        Error((int)response.StatusCode);
                        return;
                    }
            }
        }

        public Task<TemperaturePoint[]> GetTemperatures(string startDate, string endDate = "")
        {
            return GetJson<TemperaturePoint>("/temperature/get", startDate == "" ? "" : "/" + startDate, endDate == "" ? "" : "/" + endDate);
        }

        public async Task<float> GetTempAverage(string startDate, string endDate = "")
        {
            return (await GetJson<TempAverage>("/temperature/getAverage", startDate == "" ? "" : "/" + startDate, endDate == "" ? "" : "/" + endDate))[0].averageTemp;
        }

        public Task<StatePoint[]> GetStates(string state, string startDate, string endDate = "")
        {
            return GetJson<StatePoint>("/state/get/" + state, startDate == "" ? "" : "/" + startDate, endDate == "" ? "" : "/" + endDate);
        }

        public async Task<float>GetStateAverage(string startDate, string endDate = "")
        {
            return (await GetJson<StateAverage>("/state/getAverageOnTime", startDate == "" ? "" : "/" + startDate, endDate == "" ? "" : "/" + endDate))[0].averageOnTime;
        }

        private async Task<T[]> GetJson<T>(string urlExtension, string startDate, string endDate = "") where T : struct
        {
            HttpResponseMessage response = await RequestWithTimeout(1000, urlExtension + startDate + endDate, "GET", null);

            if (response == null)
                return null;

            switch((int)response.StatusCode)
            {
                case 200:
                    {
                        return JsonConvert.DeserializeObject<T[]>(await response.Content.ReadAsStringAsync());
                    }
                case 401:
                    {
                        if (await Auth())
                        {
                            response = await RequestWithTimeout(1000, urlExtension + startDate + endDate, "GET", null);

                            if (response == null)
                                return null;

                            if ((int)response.StatusCode == 200)
                                return JsonConvert.DeserializeObject<T[]>(await response.Content.ReadAsStringAsync());
                            else
                                Error((int)response.StatusCode);

                            return null;
                        }
                        else
                        {
                            Error(401);
                            return null;
                        }
                    }
                default:
                    {
                        Error((int)response.StatusCode);
                        return null;
                    }
            }
        }

    }
}