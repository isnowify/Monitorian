using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Monitorian.Core.Models;
using Monitorian.Core.ViewModels;
using Monitorian.Core.Views.Controls;
using ScreenFrame;
using ScreenFrame.Movers;

namespace Monitorian.Core.Views
{
	public partial class InputSourceSelectionWindow : Window
	{
		public MonitorViewModel ViewModel => (MonitorViewModel)this.DataContext;

		public InputSourceSelectionWindow(MonitorViewModel monitorViewModel)
		{
			InitializeComponent();

			this.DataContext = monitorViewModel;

		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

		}



		#region Show/Close

		public void DepartFromForeground()
		{
			this.Topmost = false;
		}

		public async void ReturnToForeground()
		{
			// Wait for this window to be able to be activated.
			await Task.Delay(TimeSpan.FromMilliseconds(100));

			if (_isClosing)
				return;

			// Activate this window. This is necessary to assure this window is foreground.
			this.Activate();

			this.Topmost = true;
		}

		private bool _isClosing = false;

		private void OnCloseTriggered(object sender, EventArgs e)
		{
			if (!_isClosing && this.IsLoaded)
				this.Close();
		}

		protected override void OnDeactivated(EventArgs e)
		{
			base.OnDeactivated(e);

			if (!this.Topmost)
				return;

			if (!_isClosing)
				this.Close();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			if (!e.Cancel)
			{
				_isClosing = true;
			}

			base.OnClosing(e);
		}

		#endregion
	}
}