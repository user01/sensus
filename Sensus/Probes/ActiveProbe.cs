﻿using Sensus.Exceptions;
using Sensus.UI.Properties;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sensus.Probes
{
    /// <summary>
    /// A probe that polls a data source for samples on a predetermined schedule.
    /// </summary>
    public abstract class ActiveProbe : Probe
    {
        private int _sleepDurationMS;
        private Thread _pollThread;
        private AutoResetEvent _pollTrigger;

        [EntryIntegerUiProperty("Sleep Duration (MS):", true)]
        public int SleepDurationMS
        {
            get { return _sleepDurationMS; }
            set
            {
                if (value != _sleepDurationMS)
                {
                    _sleepDurationMS = value;
                    OnPropertyChanged();
                }
            }
        }

        public ActiveProbe()
        {
            _sleepDurationMS = 1000;
            _pollTrigger = new AutoResetEvent(true); // start polling immediately
        }

        public override void Start()
        {
            ChangeState(ProbeState.Initialized, ProbeState.Started);

            _pollThread = new Thread(() =>
                {
                    while (State == ProbeState.Started)
                    {
                        _pollTrigger.WaitOne(_sleepDurationMS);

                        if (State == ProbeState.Started)
                        {
                            Datum d = null;

                            try { d = Poll(); }
                            catch (Exception ex) { if(Logger.Level >= LoggingLevel.Normal) Logger.Log("Failed to poll probe \"" + Name + "\":  " + ex.Message + Environment.NewLine + ex.StackTrace); }

                            try { StoreDatum(d); }
                            catch (Exception ex) { if (Logger.Level >= LoggingLevel.Normal) Logger.Log("Failed to store datum:  " + ex.Message + Environment.NewLine + ex.StackTrace); }
                        }
                    }
                });

            _pollThread.Start();
        }

        protected abstract Datum Poll();

        public override async void StopAsync()
        {
            await Task.Run(() =>
                {
                    ChangeState(ProbeState.Started, ProbeState.Stopping);

                    _pollTrigger.Set();  // don't wait for current sleep cycle to end -- wake up immediately so thread can be joined
                    _pollThread.Join();

                    ChangeState(ProbeState.Stopping, ProbeState.Stopped);
                });
        }
    }
}