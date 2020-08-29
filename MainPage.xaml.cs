using Microsoft.Gaming.XboxGameBar;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Windows.UI.Core;
using Windows.UI.Text.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace LiveSplit
{
	/// <summary>
	/// LiveSplit current time component
	/// </summary>
	public sealed partial class MainPage : Page
	{
		XboxGameBarWidget widget = null;
		DispatcherTimer dispatcherTimer;
		byte[] data = new byte[1024];
		string input = "getcurrenttime\r\n", stringData;
		int recv;
		TcpClient server;
		NetworkStream ns;
		bool connected = false;

		public MainPage()
		{
			this.InitializeComponent();

			Connect();

			dispatcherTimer = new DispatcherTimer();
			dispatcherTimer.Tick += DispatcherTimer_Tick;
			dispatcherTimer.Interval = TimeSpan.FromMilliseconds(16);

			dispatcherTimer.Start();
		}

		private void Connect()
        {
			connected = false;
			try
			{
				server = new TcpClient("127.0.0.1", 16834);
				ns = server.GetStream();
				ns.ReadTimeout = 100;
				connected = true;
			}
			catch (Exception ex)
			{
				connected = false;
				Connect();
			}
		}

		private void DispatcherTimer_Tick(object sender, object e)
		{
			if (!connected) return;
			try
			{
				// get current time
				ns.Write(Encoding.ASCII.GetBytes(input), 0, input.Length);
				ns.Flush();
				data = new byte[32];
				recv = ns.Read(data, 0, data.Length);
				stringData = Encoding.ASCII.GetString(data, 0, recv);
				string[] time = stringData.Split(".");
				MainText.Text = time[0];
				if (time.Length > 1)
					MilliText.Text = "." + time[1];
				else
					MilliText.Text = "";
			}
			catch (Exception ex)
			{
				Connect();
				return;
			}

		}

		protected override void OnNavigatedTo(NavigationEventArgs e)
		{
			// you will need access to the XboxGameBarWidget, in this case it was passed as a parameter when navigating to the widget page, your implementation may differ.
			widget = e.Parameter as XboxGameBarWidget;

			// subscribe for RequestedOpacityChanged events
			if (widget != null)
				widget.RequestedOpacityChanged += Widget_RequestedOpacityChanged;
		}

		private async void Widget_RequestedOpacityChanged(XboxGameBarWidget sender, object args)
		{
			// be sure to dispatch to the correct UI thread for this widget, Game Bar events are not guaranteed to come in on the same thread.
			await Page_Main.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
			{
				// adjust the opacity of your background as appropriate
				Background.Opacity = widget.RequestedOpacity;
			});
		}

	}
}
