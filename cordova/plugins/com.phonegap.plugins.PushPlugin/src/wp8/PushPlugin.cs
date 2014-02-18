using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Notification;
using Microsoft.Phone.Shell;
using Newtonsoft.Json.Linq;

namespace WPCordovaClassLib.Cordova.Commands
{
	public class PushPlugin : BaseCommand
	{
		private HttpNotificationChannel pushChannel;
		private string channelName;
		private string toastCallback;

		public void register(string options)
		{
			try
			{
				var args = JSON.JsonHelper.Deserialize<string[]>(options);
				var pushOptions = JSON.JsonHelper.Deserialize<Options>(args[0]);
				this.channelName = pushOptions.ChannelName;
				this.toastCallback = pushOptions.NotificationCallback;
			}
			catch (Exception)
			{
				this.DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION));
				return;
			}

			this.pushChannel = HttpNotificationChannel.Find(this.channelName);
			if (this.pushChannel == null)
			{
				this.pushChannel = new HttpNotificationChannel(this.channelName);
				this.PushChannel_HookEvents();
				this.pushChannel.Open();
				this.pushChannel.BindToShellToast();
				this.pushChannel.BindToShellTile();
			}
			else
			{
				this.PushChannel_HookEvents();

				var result = new RegisterResult();
				result.ChannelName = this.channelName;
				result.Uri = this.pushChannel.ChannelUri.ToString();
				this.DispatchCommandResult(new PluginResult(PluginResult.Status.OK, result));
			}
		}

		public void showToastNotification(string options)
		{
			try
			{
				var args = JSON.JsonHelper.Deserialize<string[]>(options);
				var toast = JSON.JsonHelper.Deserialize<ShellToast>(args[0]);

				Deployment.Current.Dispatcher.BeginInvoke(() =>
				{
					toast.Show();
				});
			}
			catch (Exception)
			{
				this.DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION));
				return;
			}
		}

		public void showRawNotification(string options)
		{
			try
			{
				var args = JSON.JsonHelper.Deserialize<string[]>(options);
				var message = args[0];

				Deployment.Current.Dispatcher.BeginInvoke(() =>
				{
					MessageBox.Show(String.Format("Received Notification {0}:\n{1}",
						DateTime.Now.ToShortTimeString(), message));
				});
			}
			catch (Exception)
			{
				this.DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION));
				return;
			}
		}

		private void PushChannel_HookEvents()
		{
			this.pushChannel.ChannelUriUpdated += new EventHandler<NotificationChannelUriEventArgs>(this.PushChannel_ChannelUriUpdated);
			this.pushChannel.ErrorOccurred += new EventHandler<NotificationChannelErrorEventArgs>(this.PushChannel_ErrorOccurred);
			this.pushChannel.ShellToastNotificationReceived += this.PushChannel_ShellToastNotificationReceived;
			this.pushChannel.HttpNotificationReceived += this.PushChannel_HttpNotificationReceived;
		}

		private void PushChannel_ChannelUriUpdated(object sender, NotificationChannelUriEventArgs e)
		{
			// return uri to js
			var result = new RegisterResult();
			result.ChannelName = this.channelName;
			result.Uri = this.pushChannel.ChannelUri.ToString();
			this.DispatchCommandResult(new PluginResult(PluginResult.Status.OK, result));
		}

		private void PushChannel_ErrorOccurred(object sender, NotificationChannelErrorEventArgs e)
		{
			// call error handler and return uri
			var err = new RegisterError();
			err.Code = e.ErrorCode.ToString();
			err.Message = e.Message;
			this.DispatchCommandResult(new PluginResult(PluginResult.Status.ERROR, err));
		}

		private void PushChannel_ShellToastNotificationReceived(object sender, NotificationEventArgs e)
		{
			var toast = new PushNotification
			{
				Type = "toast"
			};

			toast.JsonContent = new ExpandoObject();
			foreach (var item in e.Collection)
			{
				toast.JsonContent.Add(item.Key, item.Value);
			}

			this.ExecuteCallback(JValue.FromObject(toast).ToString());
		}

		private void PushChannel_HttpNotificationReceived(object sender, HttpNotificationEventArgs e)
		{
			var raw = new PushNotification
			{
				Type = "raw"
			};

			raw.JsonContent = new ExpandoObject();

			using (var reader = new StreamReader(e.Notification.Body))
			{
				raw.JsonContent.Add("Body", reader.ReadToEnd());
			}

			this.ExecuteCallback(JValue.FromObject(raw).ToString());
		}

		private void ExecuteCallback(string callbackResult)
		{
			Deployment.Current.Dispatcher.BeginInvoke(() =>
			{
				PhoneApplicationFrame frame = Application.Current.RootVisual as PhoneApplicationFrame;
				if (frame != null)
				{
					var page = frame.Content as PhoneApplicationPage;
					if (page != null)
					{
						var cView = page.FindName("CordovaView") as CordovaView;
						if (cView != null)
						{
							cView.Browser.Dispatcher.BeginInvoke((ThreadStart)delegate()
							{
								try
								{
									cView.Browser.InvokeScript("execScript", this.toastCallback + "(" + callbackResult + ")");
								}
								catch (Exception ex)
								{
									Debug.WriteLine("ERROR: Exception in InvokeScriptCallback :: " + ex.Message);
								}
							});
						}
					}
				}
			});
		}

		[DataContract]
		public class Options
		{
			[DataMember(Name = "channelName", IsRequired = true)]
			public string ChannelName { get; set; }

			[DataMember(Name = "ecb", IsRequired = false)]
			public string NotificationCallback { get; set; }
		}

		[DataContract]
		public class RegisterResult
		{
			[DataMember(Name = "uri", IsRequired = true)]
			public string Uri { get; set; }

			[DataMember(Name = "channel", IsRequired = true)]
			public string ChannelName { get; set; }
		}

		[DataContract]
		public class PushNotification
		{
			[DataMember(Name = "jsonContent", IsRequired = true)]
			public IDictionary<string, object> JsonContent { get; set; }

			[DataMember(Name = "type", IsRequired = true)]
			public string Type { get; set; }
		}

		[DataContract]
		public class RegisterError
		{
			[DataMember(Name = "code", IsRequired = true)]
			public string Code { get; set; }

			[DataMember(Name = "message", IsRequired = true)]
			public string Message { get; set; }
		}
	}
}