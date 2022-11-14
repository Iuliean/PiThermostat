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
using Newtonsoft.Json.Linq;
using Org.Apache.Http.Protocol;
using Android.Net.Http;
using Android.Hardware.Biometrics;
using Org.Json;

namespace PiThermostat.Utils
{
    sealed class Server
    {
        private string url;
        private string password;

        private Action<HttpStatusCode> authCallback;

        private readonly HttpClient client;

        private static readonly object l = new object();
        private static Server instance = null;

        private Server ()
        {
            url = "";
            password = "";
            SetAuthCallBack(null);

            client = new HttpClient ();
            client.Timeout = TimeSpan.FromSeconds(1);
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

        public void SetAuthCallBack(Action<HttpStatusCode> action)
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

        private void Error(HttpStatusCode code)
        {
            if (authCallback != null)
                authCallback(code);
        }
        private async Task<JsonResponse<T>?> GetRequest<T>(string endpoint, string? parameters = null)
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync(url + endpoint + "?" + parameters);
                if (response.StatusCode == HttpStatusCode.OK)
                    return JsonConvert.DeserializeObject<JsonResponse<T>>(await response.Content.ReadAsStringAsync());
                
                if(response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await Auth();
                    response = await client.GetAsync(url + endpoint + "?" + parameters);
                    if (response.StatusCode == HttpStatusCode.OK)
                        return JsonConvert.DeserializeObject<JsonResponse<T>>(await response.Content.ReadAsStringAsync());
                    else
                    {
                        Error(HttpStatusCode.Unauthorized);
                        return null;
                    }
                }
                Error(response.StatusCode);
                return null;
            }
            catch(TimeoutException)
            {
                Error(HttpStatusCode.RequestTimeout);
                return null;
            }
            catch(Exception)
            { 
                return null;
            }

        }
        private async Task<bool> Auth()
        {
            MultipartFormDataContent form = new MultipartFormDataContent();
            form.Add(new StringContent(Password), "password");

            try
            {
                HttpResponseMessage response = await client.PostAsync(url + "/auth", form);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var token = JObject.Parse(await response.Content.ReadAsStringAsync()).GetValue("token").Value<string>();
                    if(client.DefaultRequestHeaders.Contains("Authorization"))
                    {
                        client.DefaultRequestHeaders.Remove("Authorization");
                        client.DefaultRequestHeaders.Add("Authorization", token);
                        return true;
                    }
                    else
                    {
                        client.DefaultRequestHeaders.Add("Authorization", token);
                        return true;
                    }

                    return true;
                }
                else
                    return false;
            }
            catch(TimeoutException)
            {
                Error(HttpStatusCode.OK);
                return false;
            }
            catch(Exception e)
            {
                Console.WriteLine(e.StackTrace);
                return false;
            }
        }

        public async Task<ThermostatState?> GetParams()
        {
            return (await GetRequest<ThermostatState>("/getParams"))?.data;
        }

        public async Task SetParams(Parameters parameters)
        {
            string json = JsonConvert.SerializeObject(parameters);

            HttpContent content = new StringContent(json);
            content.Headers.ContentType = new MediaTypeHeaderValue("Application/json");
            
            HttpResponseMessage response = await client.PostAsync( url + "/setParams", content);
 

            switch(response.StatusCode)
            {
                case HttpStatusCode.OK:
                    {
                        return;
                    }
                case HttpStatusCode.Unauthorized:
                    {
                        if (await Auth())
                        {
                            response = await client.PostAsync("/setParams", content);

                            if (response.StatusCode == HttpStatusCode.OK)
                                return;
                            else
                                Error(response.StatusCode);

                            return;
                        }
                        else
                        {
                            Error(HttpStatusCode.Unauthorized);
                            return;
                        }
                    }
                    default:
                    {
                        Error(response.StatusCode);
                        return;
                    }
            }

        }
        public Task ShutdownRequest()
        {
            return GetRequest<bool>("/shutdown");
        }

        public async Task<TemperaturePoint[]> GetTemperatures(string? startDate = null, string? endDate = null)
        {
            var result = await GetRequest<TemperaturePoint[]>("/temperature/get", String.Format("startDate={0}&endDate={1}", startDate, endDate));
            return result is null ? new TemperaturePoint[] { } : result?.data; 
        }

        public async Task<float> GetTempAverage(string? startDate = null, string? endDate = null)
        {
            var result = await GetRequest<AverageTemp[]>("/temperature/getAverage", String.Format("startDate={0}&endDate={1}", startDate, endDate));
            if(result != null)
            {
                return (float)result?.data[0].averageTemp;
            }
            else
            {
                return 0;
            }
        }

        public async Task<StatePoint[]> GetStates(string? state = null, string? startDate = null, string? endDate = null)
        {
            var result = await GetRequest<StatePoint[]>("/state/get", String.Format("startDate={0}&endDate{1}&state={2}", startDate, endDate, state));
            return result is null ? new StatePoint[] { } : result?.data;
        }

        public async Task<float?>GetStateAverage(string state, string startDate, string endDate = "")
        {
            var result = await GetRequest<AverageState[]>("/state/getAverage", String.Format("startDate={0}&endDate={1}", startDate, endDate));
            if (result != null)
            {
                return (float)result?.data[0].averageOnTime;
            }
            else
            {
                return 0;
            }
        }
    }
}