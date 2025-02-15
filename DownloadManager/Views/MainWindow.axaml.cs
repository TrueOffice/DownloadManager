using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Net.Http;
using System.Text;
using System;
using System.Threading.Tasks;

namespace DownloadManager.Views
{
	public partial class MainWindow : Window
	{
		private const string AppsListUrl = "https://github.com/TrueOffice/apps/raw/refs/heads/main/apps.txt";
		private const string BaseUrl = "https://github.com/TrueOffice/";

		private HttpClient _httpClient = new HttpClient();
		private List<string> _apps = new List<string>();

		public MainWindow()
		{
			InitializeComponent();
			LoadApps();
		}

		private async void LoadApps()
		{
			try
			{
				string appsText = await _httpClient.GetStringAsync(AppsListUrl);
				_apps = new List<string>(appsText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));

				foreach (string app in _apps)
				{
					StackPanel stack = new StackPanel { Orientation = Orientation.Horizontal };
					stack.Children.Add(new TextBlock { Text = app, Width = 150 });

					string appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrueOffice", app);
					string installedVersionPath = Path.Combine(appFolder, "version.txt");
					string versionUrl = $"{BaseUrl}{app}/raw/refs/heads/main/version.txt";
					string latestVersion = await _httpClient.GetStringAsync(versionUrl);

					Button actionButton = new Button { Tag = app };

					if (Directory.Exists(appFolder) && File.Exists(installedVersionPath))
					{
						string installedVersion = File.ReadAllText(installedVersionPath).Trim();
						if (installedVersion == latestVersion.Trim())
						{
							actionButton.Content = "Lancer";
							actionButton.Click += LaunchApp;
						}
						else
						{
							actionButton.Content = "Mettre à jour";
							actionButton.Click += InstallOrUpdateApp;
						}
					}
					else
					{
						actionButton.Content = "Télécharger";
						actionButton.Click += InstallOrUpdateApp;
					}

					stack.Children.Add(actionButton);
					AppListPanel.Children.Add(stack);
				}
			}
			catch (Exception ex)
			{
				await ShowMessageAsync($"Failed to load app list: {ex.Message}");
			}
		}

		private async void InstallOrUpdateApp(object sender, RoutedEventArgs e)
		{
			if (sender is Button button && button.Tag is string appName)
			{
				string versionUrl = $"{BaseUrl}{appName}/raw/refs/heads/main/version.txt";
				string downloadUrl = $"{BaseUrl}{appName}/raw/refs/heads/main/latest_win.zip";
				string appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrueOffice", appName);
				string zipPath = Path.Combine(appFolder, "latest_win.zip");

				try
				{
					string latestVersion = await _httpClient.GetStringAsync(versionUrl);
					string installedVersionPath = Path.Combine(appFolder, "version.txt");

					if (File.Exists(installedVersionPath) && File.ReadAllText(installedVersionPath).Trim() == latestVersion.Trim())
					{
						await ShowMessageAsync($"{appName} is already up to date.");
						return;
					}

					// Supprimer le dossier de l'application s'il existe
					if (Directory.Exists(appFolder))
					{
						Directory.Delete(appFolder, true);
					}

					Directory.CreateDirectory(appFolder);
					byte[] zipBytes = await _httpClient.GetByteArrayAsync(downloadUrl);
					File.WriteAllBytes(zipPath, zipBytes);
					ZipFile.ExtractToDirectory(zipPath, appFolder, Encoding.UTF8);
					File.WriteAllText(installedVersionPath, latestVersion);
					File.Delete(zipPath);

					await ShowMessageAsync($"{appName} has been installed/updated.");
					button.Content = "Lancer";
					button.Click -= InstallOrUpdateApp;
					button.Click += LaunchApp;
				}
				catch (Exception ex)
				{
					await ShowMessageAsync($"Error downloading {appName}: {ex.Message}");
				}
			}
		}

		private void LaunchApp(object sender, RoutedEventArgs e)
		{
			if (sender is Button button && button.Tag is string appName)
			{
				string appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TrueOffice", appName);
				string exePath = Path.Combine(appFolder, "app.exe");

				if (File.Exists(exePath))
				{
					System.Diagnostics.Process.Start(exePath);
				}
				else
				{
					ShowMessageAsync($"Executable for {appName} not found.");
				}
			}
		}

		private async Task ShowMessageAsync(string message)
		{
			var dialog = new Window
			{
				Content = new TextBlock { Text = message },
				Width = 400,
				Height = 200
			};

			await dialog.ShowDialog(this);
		}
	}
}
