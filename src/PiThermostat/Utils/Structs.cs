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

namespace PiThermostat.Utils
{
    public class JsonResponse<T>
    {
        public int status;
        public T data;

    }
    public class AverageTemp
    {
        public float averageTemp;
    }

    public class AverageState
    {
        public float averageOnTime;
    }
    public class ThermostatState
    {
        public float temp;
        public float minTemp;
        public float maxTemp;
        public string state;

        public ThermostatState(float _temperature, float _minTemp, float _maxTemp, string _state)
        {
            temp = _temperature;
            minTemp = _minTemp;
            maxTemp = _maxTemp;
            state = _state;
        }
    }

    public class Parameters
    {
        public float? minTemp;
        public float? maxTemp;

        public Parameters(float _minTemp, float _maxTemp)
        {
            if (_minTemp != float.MinValue)
            {
                minTemp = _minTemp;
            }
            else
            {
                minTemp = null;
            }

            if (_maxTemp != float.MinValue)
            {
                maxTemp = _maxTemp;
            }
            else
            {
                maxTemp = null;
            }
        }
    }

    public class TemperaturePoint
    {
        public float value;
        public string time;
        public string date;

        public override string ToString()
        {
            return "Value: " + value + "\nTime: " + time + "\nDate: " + date + "\n";
        }
    }

    public class StatePoint
    {
        public float duration;
        public bool state;
        public string time;
        public string date;
    }

}