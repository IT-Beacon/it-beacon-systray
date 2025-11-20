using it_beacon_systray.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using it_beacon_systray.Commands;

namespace it_beacon_systray.ViewModels
{
    public class ReminderOverlayViewModel : INotifyPropertyChanged
    {
        private readonly App? _mainApp;
        private readonly ReminderSettings _settings;
        private readonly int _deferenceCount;
        private string _currentUptime = string.Empty;
        private string _cooldownTooltip = string.Empty;
        private bool _isDeferralAllowed = true;

        public ReminderOverlayViewModel(int deferenceCount, string reminderMessage, ReminderSettings settings)
        {
            _mainApp = System.Windows.Application.Current as App;
            _settings = settings;
            _deferenceCount = deferenceCount;
            ReminderMessage = reminderMessage;

            RestartNowCommand = new RelayCommand(RestartNow);
            RestartLaterCommand = new RelayCommand(RestartLater, CanRestartLater);

            UpdateDeferenceUI();
        }

        public string Title => _settings.Title;
        public string ReminderMessage { get; set; }
        public string PrimaryButtonText => _settings.PrimaryButtonText;
        public string DeferralButtonText => _settings.DeferralButtonText;
        public string GlyphIcon => ((char)int.Parse(_settings.Glyph, System.Globalization.NumberStyles.HexNumber)).ToString();
        public string DeferenceCounterText => $"Deferrals used: {_deferenceCount} / {_settings.MaxDeferrals}";

        public string CurrentUptime
        {
            get => _currentUptime;
            set => SetProperty(ref _currentUptime, value);
        }

        public string CooldownTooltip
        {
            get => _cooldownTooltip;
            set => SetProperty(ref _cooldownTooltip, value);
        }

        public bool IsDeferralAllowed
        {
            get => _isDeferralAllowed;
            set => SetProperty(ref _isDeferralAllowed, value);
        }

        public ICommand RestartNowCommand { get; }
        public ICommand RestartLaterCommand { get; }

        public event Action? RequestClose;
        public event PropertyChangedEventHandler? PropertyChanged;

        public void UpdateUptime()
        {
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            CurrentUptime = $"System Uptime: {(int)uptime.TotalDays}d {uptime.Hours:00}h {uptime.Minutes:00}m {uptime.Seconds:00}s";
        }

        private void UpdateDeferenceUI()
        {
            if (_deferenceCount >= _settings.MaxDeferrals)
            {
                IsDeferralAllowed = false;
                CooldownTooltip = "Maximum deferrals reached. Please restart.";
            }
            else
            {
                IsDeferralAllowed = true;
                string cooldownString;
                if (_settings.DeferralDuration % (60 * 24) == 0 && _settings.DeferralDuration > 0)
                {
                    int days = _settings.DeferralDuration / (60 * 24);
                    cooldownString = $"{days} day{(days > 1 ? "s" : "")}";
                }
                else if (_settings.DeferralDuration % 60 == 0 && _settings.DeferralDuration > 0)
                {
                    int hours = _settings.DeferralDuration / 60;
                    cooldownString = $"{hours} hour{(hours > 1 ? "s" : "")}";
                }
                else
                {
                    cooldownString = $"{_settings.DeferralDuration} minute{(_settings.DeferralDuration > 1 ? "s" : "s")}";
                }
                CooldownTooltip = $"Postpone the reminder for {cooldownString}.";
            }
        }

        private bool CanRestartLater(object? parameter) => IsDeferralAllowed;

        private void RestartLater(object? parameter)
        {
            bool isShiftHeld = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
            _mainApp?.RegisterDeference(isReset: isShiftHeld);
            RequestClose?.Invoke();
        }

        private void RestartNow(object? parameter)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("shutdown.exe", "/r /t 0")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to initiate restart: {ex.Message}", "Error");
            }
            RequestClose?.Invoke();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}