// Copyright 2014 The Rector & Visitors of the University of Virginia
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Sensus;
using System;
using Android.Hardware;

namespace Sensus.Android.Probes
{
    public class AndroidSensorListener : Java.Lang.Object, ISensorEventListener
    {
        private SensorType _sensorType;
        private TimeSpan _sensorDelay;
        private Action<SensorStatus> _sensorAccuracyChangedCallback;
        private Action<SensorEvent> _sensorValueChangedCallback;
        private SensorManager _sensorManager;
        private Sensor _sensor;
        private bool _listening;

        private readonly object _locker = new object();

        public AndroidSensorListener(SensorType sensorType, Action<SensorStatus> sensorAccuracyChangedCallback, Action<SensorEvent> sensorValueChangedCallback)
        {
            _sensorType = sensorType;
            _sensorAccuracyChangedCallback = sensorAccuracyChangedCallback;
            _sensorValueChangedCallback = sensorValueChangedCallback;
            _listening = false;
        }

        public void Initialize(TimeSpan sensorDelay)
        {
            _sensorDelay = sensorDelay;
            _sensorManager = ((AndroidSensusServiceHelper)SensusServiceHelper.Get()).GetSensorManager();
            _sensor = _sensorManager.GetDefaultSensor(_sensorType);

            if (_sensor == null)
            {
                throw new NotSupportedException("No sensors present for sensor type " + _sensorType);
            }
        }

        public void Start()
        {
            if (_sensor == null)
                return;

            lock (_locker)
            {
                if (_listening)
                    return;
                else
                    _listening = true;
            }

            // use the largest delay that will provide samples at the desired rate:  https://developer.android.com/guide/topics/sensors/sensors_overview.html#sensors-monitor
            SensorDelay sensorDelay = SensorDelay.Fastest;
            long sensorDelayMicroseconds = _sensorDelay.Ticks / 10;
            if (sensorDelayMicroseconds >= 200000)
                sensorDelay = SensorDelay.Normal;
            else if (sensorDelayMicroseconds >= 60000)
                sensorDelay = SensorDelay.Ui;
            else if (sensorDelayMicroseconds >= 20000)
                sensorDelay = SensorDelay.Game;
            else
                sensorDelay = SensorDelay.Fastest;
            
            _sensorManager.RegisterListener(this, _sensor, sensorDelay);
        }

        public void Stop()
        {
            if (_sensor == null)
                return;

            lock (_locker)
            {
                if (_listening)
                    _listening = false;
                else
                    return;
            }

            _sensorManager.UnregisterListener(this);
        }

        public void OnAccuracyChanged(Sensor sensor, SensorStatus accuracy)
        {
            if (_sensorAccuracyChangedCallback != null)
                _sensorAccuracyChangedCallback(accuracy);
        }

        public void OnSensorChanged(SensorEvent e)
        {
            if (_sensorValueChangedCallback != null && e != null && e.Values != null && e.Values.Count > 0)
                _sensorValueChangedCallback(e);
        }
    }
}
