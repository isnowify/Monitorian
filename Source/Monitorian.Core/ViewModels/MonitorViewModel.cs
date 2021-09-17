using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Monitorian.Core.Helper;
using Monitorian.Core.Models;
using Monitorian.Core.Models.Monitor;
using Monitorian.Core.Properties;

namespace Monitorian.Core.ViewModels
{
	public class MonitorViewModel : ViewModelBase
	{
		private readonly AppControllerCore _controller;
		public SettingsCore Settings => _controller.Settings;

		private IMonitor _monitor;
		private readonly InputSourceItemHandler _inputSourceItemHandler;
		public MonitorViewModel(AppControllerCore controller, IMonitor monitor)
		{
			this._controller = controller ?? throw new ArgumentNullException(nameof(controller));
			this._monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
			_inputSourceItemHandler = new InputSourceItemHandler();
			LoadCustomization();
		}

		private readonly object _lock = new object();

		internal void Replace(IMonitor monitor)
		{
			if (monitor?.IsReachable is true)
			{
				lock (_lock)
				{
					this._monitor.Dispose();
					this._monitor = monitor;
				}
			}
			else
			{
				monitor?.Dispose();
			}
		}

		public string DeviceInstanceId => _monitor.DeviceInstanceId;
		public string Description => _monitor.Description;
		public byte DisplayIndex => _monitor.DisplayIndex;
		public byte MonitorIndex => _monitor.MonitorIndex;
		public double MonitorTop => _monitor.MonitorRect.Top;

		#region Customization

		private void LoadCustomization() => _controller.TryLoadCustomization(DeviceInstanceId, ref _name, ref _isUnison, ref _rangeLowest, ref _rangeHighest);
		private void SaveCustomization() => _controller.SaveCustomization(DeviceInstanceId, _name, _isUnison, _rangeLowest, _rangeHighest);

		public string Name
		{
			get => _name ?? _monitor.Description;
			set
			{
				if (SetPropertyValue(ref _name, GetValueOrNull(value)))
					SaveCustomization();
			}
		}
		private string _name;

		private static string GetValueOrNull(string value) => !string.IsNullOrWhiteSpace(value) ? value : null;

		public bool IsUnison
		{
			get => _isUnison;
			set
			{
				if (SetPropertyValue(ref _isUnison, value))
					SaveCustomization();
			}
		}
		private bool _isUnison;

		/// <summary>
		/// Whether the range of brightness is changing
		/// </summary>
		public bool IsRangeChanging
		{
			get => _isRangeChanging;
			set => SetPropertyValue(ref _isRangeChanging, value);
		}
		private bool _isRangeChanging = false;

		public bool IsInputSourceSwitching
		{
			get => _isInputSourceSwitching && IsInputSourceSwitchSupported;
			set {
				if (SetPropertyValue(ref _isInputSourceSwitching, value) && value)
				{
					UpdateInputSource();
					GetInputSourceItems();
				} }
		}

		private bool _isInputSourceSwitching = false;

		/// <summary>
		/// Lowest brightness in the range of brightness
		/// </summary>
		public int RangeLowest
		{
			get => _rangeLowest;
			set
			{
				if (SetPropertyValue(ref _rangeLowest, (byte)value))
					SaveCustomization();
			}
		}
		private byte _rangeLowest = 0;

		/// <summary>
		/// Highest brightness in the range of brightness
		/// </summary>
		public int RangeHighest
		{
			get => _rangeHighest;
			set
			{
				if (SetPropertyValue(ref _rangeHighest, (byte)value))
					SaveCustomization();
			}
		}
		private byte _rangeHighest = 100;

		private double GetRangeRate() => Math.Abs(RangeHighest - RangeLowest) / 100D;

		#endregion

		#region Brightness

		public int Brightness
		{
			get => _monitor.Brightness;
			set
			{
				if (_monitor.Brightness == value)
					return;

				SetBrightness(value);

				if (IsSelected)
					_controller.SaveMonitorUserChanged(this);
			}
		}

		public int BrightnessSystemAdjusted => _monitor.BrightnessSystemAdjusted;
		public int BrightnessSystemChanged => Brightness;

		public bool UpdateBrightness(int brightness = -1)
		{
			AccessResult result;
			lock (_lock)
			{
				result = _monitor.UpdateBrightness(brightness);
			}

			switch (result.Status)
			{
				case AccessStatus.Succeeded:
					RaisePropertyChanged(nameof(BrightnessSystemChanged)); // This must be prior to Brightness.
					RaisePropertyChanged(nameof(Brightness));
					RaisePropertyChanged(nameof(BrightnessSystemAdjusted));
					OnSucceeded();
					return true;

				default:
					_controller.OnMonitorAccessFailed(result);

					switch (result.Status)
					{
						case AccessStatus.NoLongerExist:
							_controller.OnMonitorsChangeFound();
							break;
					}
					OnFailed();
					return false;
			}
		}

		public void IncrementBrightness()
		{
			IncrementBrightness(10);

			if (IsSelected)
				_controller.SaveMonitorUserChanged(this);
		}

		public void IncrementBrightness(int tickSize, bool isCycle = true)
		{
			if (IsRangeChanging)
				return;

			var size = tickSize * GetRangeRate();
			var count = Math.Floor((Brightness - RangeLowest) / size);
			int brightness = RangeLowest + (int)Math.Ceiling((count + 1) * size);

			SetBrightness(brightness, isCycle);
		}

		public void DecrementBrightness()
		{
			DecrementBrightness(10);

			if (IsSelected)
				_controller.SaveMonitorUserChanged(this);
		}

		public void DecrementBrightness(int tickSize, bool isCycle = true)
		{
			if (IsRangeChanging)
				return;

			var size = tickSize * GetRangeRate();
			var count = Math.Ceiling((Brightness - RangeLowest) / size);
			int brightness = RangeLowest + (int)Math.Floor((count - 1) * size);

			SetBrightness(brightness, isCycle);
		}

		private void SetBrightness(int brightness, bool isCycle)
		{
			if (brightness < RangeLowest)
				brightness = isCycle ? RangeHighest : RangeLowest;
			else if (RangeHighest < brightness)
				brightness = isCycle ? RangeLowest : RangeHighest;

			SetBrightness(brightness);
		}

		private bool SetBrightness(int brightness)
		{
			AccessResult result;
			lock (_lock)
			{
				result = _monitor.SetBrightness(brightness);
			}

			switch (result.Status)
			{
				case AccessStatus.Succeeded:
					RaisePropertyChanged(nameof(Brightness));
					OnSucceeded();
					return true;

				default:
					_controller.OnMonitorAccessFailed(result);

					switch (result.Status)
					{
						case AccessStatus.DdcFailed:
						case AccessStatus.TransmissionFailed:
						case AccessStatus.NoLongerExist:
							_controller.OnMonitorsChangeFound();
							break;
					}
					OnFailed();
					return false;
			}
		}

		#endregion

		#region Contrast

		public bool IsContrastSupported => _monitor.IsContrastSupported;

		public bool IsContrastChanging
		{
			get => IsContrastSupported && _isContrastChanging;
			set
			{
				if (SetPropertyValue(ref _isContrastChanging, value) && value)
					UpdateContrast();
			}
		}
		private bool _isContrastChanging = false;

		public int Contrast
		{
			get => _monitor.Contrast;
			set
			{
				if (_monitor.Contrast == value)
					return;

				SetContrast(value);

				if (IsSelected)
					_controller.SaveMonitorUserChanged(this);
			}
		}

		public bool UpdateContrast()
		{
			AccessResult result;
			lock (_lock)
			{
				result = _monitor.UpdateContrast();
			}

			switch (result.Status)
			{
				case AccessStatus.Succeeded:
					RaisePropertyChanged(nameof(Contrast));
					OnSucceeded();
					return true;

				default:
					_controller.OnMonitorAccessFailed(result);

					switch (result.Status)
					{
						case AccessStatus.NoLongerExist:
							_controller.OnMonitorsChangeFound();
							break;
					}
					OnFailed();
					return false;
			}
		}

		private bool SetContrast(int contrast)
		{
			AccessResult result;
			lock (_lock)
			{
				result = _monitor.SetContrast(contrast);
			}

			switch (result.Status)
			{
				case AccessStatus.Succeeded:
					RaisePropertyChanged(nameof(Contrast));
					OnSucceeded();
					return true;

				default:
					_controller.OnMonitorAccessFailed(result);

					switch (result.Status)
					{
						case AccessStatus.DdcFailed:
						case AccessStatus.TransmissionFailed:
						case AccessStatus.NoLongerExist:
							_controller.OnMonitorsChangeFound();
							break;
					}
					OnFailed();
					return false;
			}
		}

		#endregion

		#region InputSource

		public bool UpdateInputSource()
		{
			AccessResult result;
			lock (_lock)
			{
				result = _monitor.UpdateInputSource();
			}

			switch (result.Status)
			{
				case AccessStatus.Succeeded:
					RaisePropertyChanged(nameof(InputSource));
					OnSucceeded();
					return true;

				default:
					_controller.OnMonitorAccessFailed(result);

					switch (result.Status)
					{
						case AccessStatus.NoLongerExist:
							_controller.OnMonitorsChangeFound();
							break;
					}
					OnFailed();
					return false;
			}
		}

		public int InputSource
		{
			get => _monitor.InputSource;
			set
			{
				if (_monitor.InputSource == value)
					return;

				SetInputSource(value);

				if (IsSelected)
					_controller.SaveMonitorUserChanged(this);
			}
		}
		private void GetInputSourceItems()
		{
			if (_monitor.IsInputSourceSupported)
			{
				_inputSourceItemHandler.CleanAll();
				int currentInputSourceId = -1;
				foreach (var value in _monitor.InputSourcePossibleValues)
				{
					AddInputSourceItem(value);
					if (_monitor.InputSource == value) currentInputSourceId = value;
				}
				if (currentInputSourceId != -1)
				{
					String currentInputSourceName =
						Enum.GetName(typeof(MonitorConfiguration.InputSource), currentInputSourceId);
					SelectedSource = new InputSourceItem((byte)currentInputSourceId,currentInputSourceName);
				}

			}
		}



		public ObservableCollection<InputSourceItem> InputSourceItems
		{
			get { return _inputSourceItemHandler.InputSourceItems; }
		}

		private InputSourceItem _SelectedSource;
		public InputSourceItem SelectedSource
		{
			get => _SelectedSource;
			set
			{
				if (SetPropertyValue(ref _SelectedSource, value)&&_isInputSourceSwitching)
					SetInputSource(value.InputSourceId);

			}
		}

		private void AddInputSourceItem(byte inputSourceId)
		{
			String inputSourceName = Enum.GetName(typeof(MonitorConfiguration.InputSource), inputSourceId);
			InputSourceItem inputSourceItem = new InputSourceItem(inputSourceId, inputSourceName);
			_inputSourceItemHandler.Add(inputSourceItem);
		}

		public ICommand OnInputSourceButtonClickedCommand
		{
			get { return new DelegateCommand(OnInputSourceButtonClicked); }
		}


		private void OnInputSourceButtonClicked(object sender)
		{
			InputSourceSelectionWindow inputSourceMenu = new InputSourceSelectionWindow(this);
			inputSourceMenu.Show();
		}

		public bool IsInputSourceSwitchSupported => _monitor.IsInputSourceSupported;


		private bool SetInputSource(int inputSource)
		{
			AccessResult result;
			lock (_lock)
			{
				result = _monitor.SetInputSource(inputSource);
			}

			switch (result.Status)
			{
				case AccessStatus.Succeeded:
					RaisePropertyChanged(nameof(InputSource));
					OnSucceeded();
					return true;

				default:
					_controller.OnMonitorAccessFailed(result);

					switch (result.Status)
					{
						case AccessStatus.DdcFailed:
						case AccessStatus.TransmissionFailed:
						case AccessStatus.NoLongerExist:
							_controller.OnMonitorsChangeFound();
							break;
					}
					OnFailed();
					return false;
			}
		}

		#endregion

		#region Controllable

		public bool IsReachable => _monitor.IsReachable;

		public bool IsControllable => IsReachable && ((0 < _controllableCount) || _isConfirmed);
		private bool _isConfirmed;

		// This count is for determining IsControllable property.
		// To set this count, the following points need to be taken into account: 
		// - The initial value of IsControllable property should be true (provided IsReachable is
		//   true) because a monitor is expected to be controllable. Therefore, the initial count
		//   should be greater than 0.
		// - The initial count is intended to give allowance for failures before the first success.
		//   If the count has been consumed without any success, the monitor will be regarded as
		//   uncontrollable at all.
		// - _isConfirmed field indicates that the monitor has succeeded at least once. It will be
		//   set true at the first success and at a succeeding success after a failure.
		// - The normal count gives allowance for failures after the first and succeeding successes.
		//   As long as the monitor continues to succeed, the count will stay at the normal count.
		//   Each time the monitor fails, the count decreases. The decreased count will be reverted
		//   to the normal count when the monitor succeeds again.
		// - The initial count must be smaller than the normal count so that _isConfirmed field
		//   will be set at the first success while reducing unnecessary access to the field.
		private short _controllableCount = InitialCount;
		private const short InitialCount = 3;
		private const short NormalCount = 5;

		private void OnSucceeded()
		{
			if (_controllableCount < NormalCount)
			{
				var formerCount = _controllableCount;
				_controllableCount = NormalCount;
				if (formerCount <= 0)
				{
					RaisePropertyChanged(nameof(IsControllable));
					RaisePropertyChanged(nameof(Message));
				}

				_isConfirmed = true;
			}
		}

		private void OnFailed()
		{
			if (--_controllableCount == 0)
			{
				RaisePropertyChanged(nameof(IsControllable));
				RaisePropertyChanged(nameof(Message));
			}
		}

		public string Message
		{
			get
			{
				if (0 < _controllableCount)
					return null;

				LanguageService.Switch();

				var reason = _monitor switch
				{
					DdcMonitorItem => Resources.StatusReasonDdcFailing,
					UnreachableMonitorItem { IsInternal: false } => Resources.StatusReasonDdcNotEnabled,
					_ => null,
				};

				return Resources.StatusNotControllable + (reason is null ? string.Empty : Environment.NewLine + reason);
			}
		}

		#endregion

		#region Focus

		public bool IsByKey
		{
			get => _isByKey;
			set
			{
				if (SetPropertyValue(ref _isByKey, value))
					RaisePropertyChanged(nameof(IsSelectedByKey));
			}
		}
		private bool _isByKey;

		public bool IsSelected
		{
			get => _isSelected;
			set
			{
				if (SetPropertyValue(ref _isSelected, value))
					RaisePropertyChanged(nameof(IsSelectedByKey));
			}
		}
		private bool _isSelected;

		public bool IsSelectedByKey => IsSelected && IsByKey;

		#endregion

		public bool IsTarget
		{
			get => _isTarget;
			set => SetPropertyValue(ref _isTarget, value);
		}
		private bool _isTarget;

		public override string ToString()
		{
			return SimpleSerialization.Serialize(
				("Item", _monitor),
				(nameof(Name), Name),
				(nameof(IsUnison), IsUnison),
				(nameof(IsControllable), IsControllable),
				("IsConfirmed", _isConfirmed),
				("ControllableCount", _controllableCount),
				(nameof(IsByKey), IsByKey),
				(nameof(IsSelected), IsSelected),
				(nameof(IsTarget), IsTarget));
		}

		#region IDisposable

		private bool _isDisposed = false;

		protected override void Dispose(bool disposing)
		{
			lock (_lock)
			{
				if (_isDisposed)
					return;

				if (disposing)
				{
					_monitor.Dispose();
				}

				_isDisposed = true;

				base.Dispose(disposing);
			}
		}

		#endregion
	}

	public class DelegateCommand : ICommand
	{
		private readonly Predicate<object> _canExecute;
		private readonly Action<object> _execute;

		public event EventHandler CanExecuteChanged;

		public DelegateCommand(Action<object> execute)
			: this(execute, null)
		{
		}

		public DelegateCommand(Action<object> execute,
			Predicate<object> canExecute)
		{
			_execute = execute;
			_canExecute = canExecute;
		}

		public bool CanExecute(object parameter)
		{
			if (_canExecute == null)
			{
				return true;
			}

			return _canExecute(parameter);
		}

		public void Execute(object parameter)
		{
			_execute(parameter);
		}

		public void RaiseCanExecuteChanged()
		{
			if( CanExecuteChanged != null )
			{
				CanExecuteChanged(this, EventArgs.Empty);
			}
		}
	}


	//Available Input Source in selection menu
	public class InputSourceItem
	{
		public InputSourceItem(byte inputSourceId, string inputSourceName)
		{
			this.InputSourceId = inputSourceId;
			this.InputSourceName = inputSourceName;
		}
		public byte InputSourceId { get; }
		public string InputSourceName { get;  }
	}

	public class InputSourceItemHandler
	{
		public InputSourceItemHandler()
		{
			InputSourceItems = new ObservableCollection<InputSourceItem>();
		}

		public ObservableCollection<InputSourceItem> InputSourceItems { get; private set; }

		public void Add(InputSourceItem item)
		{
			InputSourceItems.Add(item);
		}

		public void CleanAll()
		{
			InputSourceItems = new ObservableCollection<InputSourceItem>();
		}
	}


}