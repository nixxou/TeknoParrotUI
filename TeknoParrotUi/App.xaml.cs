using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CefSharp;
using CefSharp.Wpf;
using TeknoParrotUi.Common;
using TeknoParrotUi.Helpers;
using TeknoParrotUi.Views;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MessageBox = System.Windows.MessageBox;


namespace TeknoParrotUi
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    /// 
    // Hello
    public partial class App
    {
		private static NotifyIcon notifyIcon;
		// Importation de la fonction SetWindowPos
		[DllImport("user32.dll", SetLastError = true)]
		private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
		// Constantes utilis�es avec SetWindowPos
		private static readonly IntPtr HWND_BOTTOM = new IntPtr(1); // D�finit la fen�tre en arri�re-plan
		private const uint SWP_NOMOVE = 0x0002;  // Ne pas changer la position
		private const uint SWP_NOSIZE = 0x0001;  // Ne pas changer la taille
		private const uint SWP_NOACTIVATE = 0x0010;  // Ne pas activer la fen�tre


		private GameProfile _profile;
        private bool _emuOnly, _test, _tpOnline, _startMin;
        private bool _profileLaunch;

        public static bool startInvisible = false;
		public static bool Is64Bit()
        {
            // for testing
            //return false;
            return Environment.Is64BitOperatingSystem;
        }

        private void TerminateProcesses()
        {
            var currentId = Process.GetCurrentProcess().Id;
            foreach (var process in Process.GetProcessesByName("TeknoParrotUi"))
            {
                if (process.Id != currentId)
                {
                    process.Kill();
                }
            }
        }

        private bool HandleArgs(string[] args)
        {
            _test = args.Any(x => x == "--test");
            if (args.Contains("--tponline"))
            {
                _tpOnline = true;
            }

            if (args.Contains("--startMinimized"))
            {
                _startMin = true;
            }
			if (args.Contains("--startInvisible"))
			{
				startInvisible = true;
			}
			if (args.Any(x => x.StartsWith("--profile=")) && args.All(x => x != "--emuonly"))
            {
                // Run game + emu
                if (!FetchProfile(args.FirstOrDefault(x => x.StartsWith("--profile="))))
                    return false;
                _emuOnly = false;
                _profileLaunch = true;
                if (string.IsNullOrWhiteSpace(_profile.GamePath))
                {
                    MessageBoxHelper.ErrorOK(TeknoParrotUi.Properties.Resources.ErrorGamePathNotSet);
                    return false;
                }

                return true;
            }

            if (args.Any(x => x.StartsWith("--profile=")) && args.Any(x => x == "--emuonly"))
            {
                // Run emu only
                if (!FetchProfile(args.FirstOrDefault(x => x.StartsWith("--profile="))))
                    return false;
                _emuOnly = true;
                return true;
            }

            return false;
        }

        private bool FetchProfile(string profile)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(profile))
                    return false;
                var a = profile.Substring(10, profile.Length - 10);
                if (string.IsNullOrWhiteSpace(a))
                    return false;
                var b = Path.Combine("GameProfiles\\", a);
                if (!File.Exists(b))
                    return false;
                if (File.Exists(Path.Combine("UserProfiles\\", a)))
                {
                    _profile = JoystickHelper.DeSerializeGameProfile(Path.Combine("UserProfiles\\", a), true);
                }
                else
                {
                    _profile = JoystickHelper.DeSerializeGameProfile(b, false);
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        static PaletteHelper ph = new PaletteHelper();
        static SwatchesProvider sp = new SwatchesProvider();
        static string GetResourceString(string input)
        {
            return $"pack://application:,,,/{input}";
        }

        private static string GetColorResourcePath(string colorName, bool isPrimary)
        {
            string colorType = isPrimary ? "Primary" : "Accent";
            return $"MaterialDesignColors;component/Themes/Recommended/{colorType}/MaterialDesignColor.{colorName}.xaml";
        }

        public static void LoadTheme(string colourname, bool darkmode, bool holiday)
        {
            // if user isn't patreon, use defaults
            if (!IsPatreon())
            {
                colourname = "lightblue";

                if (holiday)
                {
                    var now = DateTime.Now;

                    if (now.Month == 10 && now.Day == 31)
                    {
                        // halloween - orange title
                        colourname = "orange";
                    }

                    if (now.Month == 12 && now.Day == 25)
                    {
                        // christmas - red title
                        colourname = "red";
                    }
                }
            }

            Debug.WriteLine($"UI colour: {colourname} | Dark mode: {darkmode}");

            ph.SetLightDark(darkmode);
            var colour = sp.Swatches.FirstOrDefault(a => a.Name == colourname);
            if (colour != null)
            {
                ph.ReplacePrimaryColor(colour);
            }
        }

        // Load theme via modified resource paths on boot, to avoid having to load
        // default colors only to replace them directly after via LoadTheme
        // because ReplacePrimaryColor is very expensive performance wise
        public static void InitializeTheme(string primaryColorName, string accentColorName, bool darkMode, bool holiday)
        {
            // Apply holiday overrides if necessary
            if (!IsPatreon() && holiday)
            {
                var now = DateTime.Now;
                if (now.Month == 10 && now.Day == 31)
                {
                    primaryColorName = "Orange";
                }
                else if (now.Month == 12 && now.Day == 25)
                {
                    primaryColorName = "Red";
                }
            }

            Debug.WriteLine($"UI colour: Primary={primaryColorName}, Accent={accentColorName} | Dark mode: {darkMode}");

            Current.Resources.MergedDictionaries.Clear();
            Current.Resources.MergedDictionaries.Add(new ResourceDictionary()
            {
                Source = new Uri(GetResourceString($"MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.{(darkMode ? "Dark" : "Light")}.xaml"))
            });
            Current.Resources.MergedDictionaries.Add(new ResourceDictionary()
            {
                Source = new Uri(GetResourceString("MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Defaults.xaml"))
            });
            Current.Resources.MergedDictionaries.Add(new ResourceDictionary()
            {
                Source = new Uri(GetResourceString(GetColorResourcePath(primaryColorName, true)))
            });
            Current.Resources.MergedDictionaries.Add(new ResourceDictionary()
            {
                Source = new Uri(GetResourceString(GetColorResourcePath(accentColorName, false)))
            });
        }

        public static bool IsPatreon()
        {
            var tp = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"SOFTWARE\TeknoGods\TeknoParrot");
            return (tp != null && tp.GetValue("PatreonSerialKey") != null);
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // This fixes the paths when the ui is started through the command line in a different folder
            Directory.SetCurrentDirectory(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location));

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler((_, ex) =>
            {
                // give us the exception in english
                System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en");
                var exceptiontext = (ex.ExceptionObject as Exception).ToString();
                MessageBoxHelper.ErrorOK($"TeknoParrotUI ran into an exception!\nPlease send exception.txt to the #teknoparrothelp channel on Discord or create a Github issue!\n{exceptiontext}");
                File.WriteAllText("exception.txt", exceptiontext);
                Environment.Exit(1);
            });
            // Localization testing without changing system language.
            // Language code list: https://docs.microsoft.com/en-us/openspecs/windows_protocols/ms-lcid/70feba9f-294e-491e-b6eb-56532684c37f
            //System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("fr-FR");

            //this'll sort dumb stupid tp online gay shit
            HandleArgs(e.Args);
            JoystickHelper.DeSerialize();
            if (!Lazydata.ParrotData.HasReadPolicies)
            {
                MessageBox.Show(
                    "We take your privacy very seriously, and in accordance with the European Union's strict data protection laws, known as the General Data Protection Regulation (GDPR), we are required to inform you about how we handle your personal data.\r\n\r\nPlease take a moment to review our Terms of Service and Privacy Policy.\n\r\n\rThese regulations are designed to give you, the individual, greater control and transparency over your data - something we believe everyone deserves.",
                    "Privacy and Terms Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                var policyWindow = new PoliciesWindow(0, Current);
                policyWindow.ShowDialog();

                policyWindow.SetPolicyText(1);
                policyWindow.ShowDialog();

                Lazydata.ParrotData.HasReadPolicies = true;
                JoystickHelper.Serialize();
            }
            if (!_tpOnline)
            {
                if (Process.GetProcessesByName("TeknoParrotUi").Where((p) => p.Id != Process.GetCurrentProcess().Id)
                    .Count() > 0)
                {
                    if (MessageBoxHelper.ErrorYesNo(TeknoParrotUi.Properties.Resources.ErrorAlreadyRunning))
                    {
                        TerminateProcesses();
                    }
                    else
                    {
                        Current.Shutdown(0);
                        return;
                    }
                }

                if (!Lazydata.ParrotData.HideVanguardWarning)
                {
                    if (Process.GetProcessesByName("vgc").Where((p) => p.Id != Process.GetCurrentProcess().Id).Count() > 0 || Process.GetProcessesByName("vgtray").Where((p) => p.Id != Process.GetCurrentProcess().Id).Count() > 0)
                    {
                        MessageBoxHelper.WarningOK(TeknoParrotUi.Properties.Resources.VanguardDetected);
                    }
                }
            }

            if (File.Exists("DumbJVSManager.exe"))
            {
                MessageBoxHelper.ErrorOK(TeknoParrotUi.Properties.Resources.ErrorOldTeknoParrotDirectory);
                Current.Shutdown(0);
                return;
            }

            // updater cleanup
            try
            {
                var bakfiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.bak", SearchOption.AllDirectories);
                foreach (var file in bakfiles)
                {
                    try
                    {
                        Debug.WriteLine($"Deleting old updater file {file}");
                        File.Delete(file);
                    }
                    catch
                    {
                        // ignore..
                    }
                }
            }
            catch
            {
                // do nothing honestly
            }

            // old description file cleanup
            try
            {
                var olddescriptions = Directory.GetFiles("Descriptions", "*.xml");
                foreach (var file in olddescriptions)
                {
                    try
                    {
                        Debug.WriteLine($"Deleting old description file {file}");
                        File.Delete(file);
                    }
                    catch
                    {
                        // ignore..
                    }
                }
            }
            catch
            {
                // ignore
            }

            InitializeTheme(Lazydata.ParrotData.UiColour, "Lime", Lazydata.ParrotData.UiDarkMode,  Lazydata.ParrotData.UiHolidayThemes);

            if (Lazydata.ParrotData.UiDisableHardwareAcceleration)
                RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            if (e.Args.Length != 0)
            {
                // Process command args
                if (HandleArgs(e.Args) && Views.Library.ValidateAndRun(_profile, out var loader, out var dll, _emuOnly, null, _test))
                {
                    var gamerunning = new Views.GameRunning(_profile, loader, dll, _test, _emuOnly, _profileLaunch);
                    // Args ok, let's do stuff
                    Window window = null;

					if (startInvisible)
                    {
						window = new Window
						{
							//fuck you nezarn no more resizing smh /s
							Title = "GameRunning",
							Content = gamerunning,
							MaxWidth = 800,
							MinWidth = 800,
							MaxHeight = 800,
							MinHeight = 800,
							AllowsTransparency = true,    // Permet la transparence
							WindowStyle = WindowStyle.None, // Supprime les bordures
							Background = Brushes.Transparent, // D�finit l'arri�re-plan comme transparent
							Opacity = 0, // Rend la fen�tre totalement invisible                        
							ShowInTaskbar = false,
						};
					}
                    else
                    {
						window = new Window
						{
							//fuck you nezarn no more resizing smh /s
							Title = "GameRunning",
							Content = gamerunning,
							MaxWidth = 800,
							MinWidth = 800,
							MaxHeight = 800,
							MinHeight = 800,
						};
					}

					if (_startMin)
                    {
                        window.WindowState = WindowState.Minimized;
                    }
					
					//             d:DesignHeight="800" d:DesignWidth="800" Loaded="GameRunning_OnLoaded" Unloaded="GameRunning_OnUnloaded">
					window.Dispatcher.ShutdownStarted += (x, x2) => gamerunning.GameRunning_OnUnloaded(null, null);

                    window.Show();

                    if (startInvisible)
                    {
						IntPtr hWnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
						SetWindowPos(hWnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
						System.Drawing.Icon appIcon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Application.ResourceAssembly.Location);
						Thread notifyIconThread = new Thread(() =>
						{
							// Association du menu contextuel au NotifyIcon
							notifyIcon = new NotifyIcon
							{
								Icon = appIcon,
								Visible = true,
								Text = "Teknoparrot",
							};

							notifyIcon.Click += (nsender, ne) =>
							{
								// Utiliser le Dispatcher pour mettre � jour l'interface utilisateur
								System.Windows.Application.Current.Dispatcher.Invoke(() =>
								{
									window.Opacity = window.Opacity == 0 ? 1 : 0;
									window.ShowInTaskbar = window.ShowInTaskbar ? false : true;
									if (window.ShowInTaskbar)
									{
										window.Activate();
									}
									else
									{
										SetWindowPos(new System.Windows.Interop.WindowInteropHelper(window).Handle, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
									}
								});
							};

							System.Windows.Forms.Application.Run();
						});
						notifyIconThread.IsBackground = true;
						notifyIconThread.Start();

					}


					return;
                }
            }
            DiscordRPC.StartOrShutdown();

            StartApp();
        }

        private void StartApp()
        {
            MainWindow wnd = new MainWindow();
            wnd.Show();
        }
    }
}
