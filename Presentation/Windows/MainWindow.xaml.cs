using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Threading;
using OnPass.Presentation.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System;
using System.IO;


namespace OnPass.Presentation.Windows
{
    // Hosts the main desktop shell, manages inactivity locking and tray behavior,
    // and coordinates navigation between login and dashboard views.
    public partial class MainWindow : Window
    {
        private const double NormalTopBarHeight = 40;
        private const double MaximizedTopBarHeight = 44;

        private TaskbarIcon? trayIcon;

        private DispatcherTimer? activityTimer;
        private DateTime lastActivityTime;
        private bool isLocked = false;
        private string? currentUsername;
        private int autoLockMinutes = 5;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            MainContent.Content = new LoginControl(this);
            InitializeTrayIcon();
            InitializeActivityMonitoring();

            Closing += MainWindow_Closing!;
        }

        // Starts the global inactivity timer so auto-lock behaves consistently across every screen.
        private void InitializeActivityMonitoring()
        {
            // Auto-lock is enforced centrally at the shell level so every screen
            // participates in the same inactivity policy once a user is logged in.
            activityTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(10)
            };
            activityTimer.Tick += ActivityTimer_Tick!;

            ResetActivityTimer();

            activityTimer.Start();

            this.MouseMove += MainWindow_UserActivity;
            this.KeyDown += MainWindow_UserActivity;
            this.PreviewMouseDown += MainWindow_UserActivity;
        }

        private void ResetActivityTimer()
        {
            lastActivityTime = DateTime.Now;
        }

        private void MainWindow_UserActivity(object sender, EventArgs e)
        {
            if (!isLocked)
            {
                ResetActivityTimer();
            }
        }

        private void ActivityTimer_Tick(object sender, EventArgs e)
        {
            if (isLocked || string.IsNullOrEmpty(currentUsername))
                return;

            TimeSpan inactiveTime = DateTime.Now - lastActivityTime;

            if (inactiveTime.TotalMinutes >= autoLockMinutes && autoLockMinutes > 0)
            {
                LockApplication();
            }
        }

        // Resets the UI and tears down the localhost bridge when the user session is locked.
        public void LockApplication()
        {
            isLocked = true;

            // Locking tears down the extension bridge as well as the UI session so
            // the browser can no longer pull passwords from a stale desktop login.
            App.MinimizeToTrayEnabled = false;
            App.CurrentUsername = null!;
            App.CurrentAccessToken = null;
            App.WebServer?.Stop();
            App.WebServer = null;

            typeof(LoginControl).GetProperty("CurrentEncryptionKey",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static)?.SetValue(null, null);

            MainContent.Content = new LoginControl(this);

            MessageBox.Show("Application locked due to inactivity.", "Security",
                            MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Records the active user session so the shell can enforce auto-lock and tray behavior correctly.
        public void UserLoggedIn(string username, int lockTimeMinutes)
        {
            currentUsername = username;
            autoLockMinutes = lockTimeMinutes;
            isLocked = false;
            ResetActivityTimer();
        }

        // Creates the tray icon and its context menu so the app can stay available while hidden.
        private void InitializeTrayIcon()
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Images", "OnPassLogo.ico");
                trayIcon = new TaskbarIcon
                {
                    Icon = File.Exists(iconPath)
                        ? new System.Drawing.Icon(iconPath)
                        : null,
                    ToolTipText = "OnPass Password Manager",
                    Visibility = Visibility.Hidden
                };
            }
            catch
            {
                trayIcon = new TaskbarIcon
                {
                    ToolTipText = "OnPass Password Manager",
                    Visibility = Visibility.Hidden
                };
            }

            var contextMenu = new ContextMenu();

            ControlTemplate contextMenuTemplate = new ControlTemplate(typeof(ContextMenu));
            var contextBorderFactory = new FrameworkElementFactory(typeof(Border), "Border");
            contextBorderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#232b38")));
            contextBorderFactory.SetValue(Border.BorderBrushProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#273145")));
            contextBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            contextBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            contextBorderFactory.SetValue(Border.PaddingProperty, new Thickness(3));
            contextBorderFactory.SetValue(Border.MinHeightProperty, 60.0);

            var itemsPresenterFactory = new FrameworkElementFactory(typeof(ItemsPresenter), "ItemsPresenter");
            itemsPresenterFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(0));
            contextBorderFactory.AppendChild(itemsPresenterFactory);

            contextMenuTemplate.VisualTree = contextBorderFactory;
            contextMenu.Template = contextMenuTemplate;

            contextMenu.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 270,
                ShadowDepth = 4,
                Opacity = 0.6,
                BlurRadius = 8
            };

            contextMenu.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#232b38"));
            contextMenu.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#273145"));
            contextMenu.BorderThickness = new Thickness(1);
            contextMenu.Padding = new Thickness(3);

            Style menuItemStyle = new Style(typeof(MenuItem));
            menuItemStyle.Setters.Add(new Setter(MenuItem.ForegroundProperty, new SolidColorBrush(Colors.White)));
            menuItemStyle.Setters.Add(new Setter(MenuItem.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#232b38"))));
            menuItemStyle.Setters.Add(new Setter(MenuItem.PaddingProperty, new Thickness(12, 12, 12, 12)));
            menuItemStyle.Setters.Add(new Setter(MenuItem.BorderThicknessProperty, new Thickness(0)));
            menuItemStyle.Setters.Add(new Setter(MenuItem.FontSizeProperty, 14.0));
            menuItemStyle.Setters.Add(new Setter(MenuItem.MinHeightProperty, 30.0));
            menuItemStyle.Setters.Add(new Setter(MenuItem.MinWidthProperty, 180.0));
            menuItemStyle.Setters.Add(new Setter(MenuItem.MarginProperty, new Thickness(2)));

            Style iconTextBlockStyle = new Style(typeof(TextBlock));
            iconTextBlockStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 12.0));
            iconTextBlockStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new SolidColorBrush(Colors.White)));
            iconTextBlockStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
            iconTextBlockStyle.Setters.Add(new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center));
            iconTextBlockStyle.Setters.Add(new Setter(TextBlock.MarginProperty, new Thickness(0)));

            var borderFactory = new FrameworkElementFactory(typeof(Border), "Border");
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(MenuItem.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(MenuItem.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(MenuItem.BorderThicknessProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));

            var gridFactory = new FrameworkElementFactory(typeof(Grid));

            var iconColumn = new FrameworkElementFactory(typeof(ColumnDefinition));
            iconColumn.SetValue(ColumnDefinition.WidthProperty, new GridLength(22));

            var textColumn = new FrameworkElementFactory(typeof(ColumnDefinition));
            textColumn.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));

            gridFactory.AppendChild(iconColumn);
            gridFactory.AppendChild(textColumn);

            var iconPresenter = new FrameworkElementFactory(typeof(ContentPresenter), "IconContent");
            iconPresenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(MenuItem.IconProperty));
            iconPresenter.SetValue(Grid.ColumnProperty, 0);
            iconPresenter.SetValue(ContentPresenter.WidthProperty, 18.0);
            iconPresenter.SetValue(ContentPresenter.HeightProperty, 18.0);
            iconPresenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            iconPresenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter), "HeaderContent");
            contentPresenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(MenuItem.HeaderProperty));
            contentPresenter.SetValue(Grid.ColumnProperty, 1);
            contentPresenter.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            contentPresenter.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenter.SetValue(ContentPresenter.MarginProperty, new Thickness(-16, 0, 0, 0));

            gridFactory.AppendChild(iconPresenter);
            gridFactory.AppendChild(contentPresenter);

            borderFactory.AppendChild(gridFactory);

            var menuItemTemplate = new ControlTemplate(typeof(MenuItem));
            menuItemTemplate.VisualTree = borderFactory;
            menuItemStyle.Setters.Add(new Setter(MenuItem.TemplateProperty, menuItemTemplate));

            Trigger hoverTrigger = new Trigger { Property = MenuItem.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(MenuItem.BackgroundProperty, new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3BA7FF"))));
            hoverTrigger.Setters.Add(new Setter(MenuItem.ForegroundProperty, new SolidColorBrush(Colors.White)));
            menuItemStyle.Triggers.Add(hoverTrigger);

            var enterAnimation = new Storyboard();
            var colorAnimation = new ColorAnimation
            {
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                To = (Color)ColorConverter.ConvertFromString("#3BA7FF")
            };
            Storyboard.SetTargetProperty(colorAnimation, new PropertyPath("Background.Color"));
            enterAnimation.Children.Add(colorAnimation);

            var scaleXAnimation = new DoubleAnimation
            {
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                To = 1.02
            };
            Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("RenderTransform.ScaleX"));
            enterAnimation.Children.Add(scaleXAnimation);

            var scaleYAnimation = new DoubleAnimation
            {
                Duration = new Duration(TimeSpan.FromSeconds(0.2)),
                To = 1.02
            };
            Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("RenderTransform.ScaleY"));
            enterAnimation.Children.Add(scaleYAnimation);

            EventTrigger mouseEnterTrigger = new EventTrigger { RoutedEvent = MenuItem.MouseEnterEvent };
            mouseEnterTrigger.Actions.Add(new BeginStoryboard { Storyboard = enterAnimation });
            menuItemStyle.Triggers.Add(mouseEnterTrigger);

            var openMenuItem = new MenuItem
            {
                Header = "Open OnPass",
                Style = menuItemStyle,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1)
            };

            var openIconTextBlock = new TextBlock
            {
                Text = "??",
                Style = iconTextBlockStyle
            };
            openMenuItem.Icon = openIconTextBlock;
            openMenuItem.Click += (s, e) => ShowMainWindow();

            var settingsMenuItem = new MenuItem
            {
                Header = "Settings",
                Style = menuItemStyle,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1)
            };

            var settingsIconTextBlock = new TextBlock
            {
                Text = "??",
                Style = iconTextBlockStyle
            };
            settingsMenuItem.Icon = settingsIconTextBlock;
            settingsMenuItem.Click += (s, e) => SettingsControl();

            var separator = new Separator
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#273145")),
                Margin = new Thickness(8, 4, 8, 4),
                Height = 1
            };

            var exitMenuItem = new MenuItem
            {
                Header = "Exit",
                Style = menuItemStyle,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(1, 1)
            };

            var exitIconTextBlock = new TextBlock
            {
                Text = "??",
                Style = iconTextBlockStyle
            };
            exitMenuItem.Icon = exitIconTextBlock;
            exitMenuItem.Click += (s, e) => ExitApplication();

            contextMenu.Items.Add(openMenuItem);
            contextMenu.Items.Add(settingsMenuItem);
            contextMenu.Items.Add(separator);
            contextMenu.Items.Add(exitMenuItem);

            trayIcon.ContextMenu = contextMenu;
            trayIcon.TrayMouseDoubleClick += (s, e) => ShowMainWindow();
        }

        // Restores the hidden window when the user opens the app from the tray.
        private void ShowMainWindow()
        {
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
        }

        // Opens the settings flow through the dashboard so the same authenticated navigation path is reused.
        private void SettingsControl()
        {

            if (string.IsNullOrEmpty(currentUsername) || isLocked)
            {
                MessageBox.Show("Please login to access settings.", "Authentication Required",
                                 MessageBoxButton.OK, MessageBoxImage.Information);

                MainContent.Content = new LoginControl(this);
            }
            else
            {
                byte[]? encryptionKey = (byte[]?)typeof(LoginControl).GetProperty("CurrentEncryptionKey",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Static)?.GetValue(null);

                if (MainContent.Content is DashboardControl dashboard)
                {
                    dashboard.Settings_Click(this, new RoutedEventArgs());
                    ShowMainWindow();
                }
                else
                {
                    var dashboardControl = new DashboardControl(this, currentUsername, encryptionKey!);
                    MainContent.Content = dashboardControl;
                    dashboardControl.Settings_Click(this, new RoutedEventArgs());
                    ShowMainWindow();
                }
            }
        }

        // Disposes the tray icon before shutting down the desktop process.
        private void ExitApplication()
        {
            trayIcon!.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (App.MinimizeToTrayEnabled)
            {
                // Closing is reinterpreted as "hide to tray" when the user enables
                // that setting, which keeps the desktop-extension connection alive.
                e.Cancel = true;
                this.Hide();
                trayIcon!.Visibility = Visibility.Visible;
            }
            else
            {
                if (trayIcon != null)
                {
                    trayIcon.Dispose();
                }
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetSizeAndPositionOnPrimaryScreen();
        }

        private void SetSizeAndPositionOnPrimaryScreen()
        {
            var primaryScreen = System.Windows.SystemParameters.WorkArea;
            double widthRatio = 0.75;
            double heightRatio = 0.75;
            this.Width = primaryScreen.Width * widthRatio;
            this.Height = primaryScreen.Height * heightRatio;
            this.Left = primaryScreen.Left + (primaryScreen.Width - this.Width) / 2;
            this.Top = primaryScreen.Top + (primaryScreen.Height - this.Height) / 2;
        }

        // Switches the content area to the requested screen while preserving shell-level behavior.
        public void Navigate(UserControl newPage)
        {
            MainContent.Content = newPage;

            if (newPage is DashboardControl dashboard)
            {
                dashboard.SetTopBarHeight(NormalTopBarHeight);
            }
        }   

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (App.MinimizeToTrayEnabled)
            {
                this.Hide();
                trayIcon!.Visibility = Visibility.Visible;
            }
            else
            {
                Window window = Window.GetWindow(this);
                if (window != null)
                {
                    window.Close();
                    App.CurrentUsername = null!;
                    App.CurrentAccessToken = null;
                    App.WebServer?.Stop();
                    App.WebServer = null;
                }
            }
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleWindowState();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            Window window = Window.GetWindow(this);
            if (window != null)
            {
                window.WindowState = WindowState.Minimized;
            }
        }

        // Keeps the custom chrome layout aligned when the window toggles between normal and maximized states.
        public void ToggleWindowState()
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                SetSizeAndPositionOnPrimaryScreen();

                if (MainContent.Content is DashboardControl dashboard)
                {
                    dashboard.TopBar.Height = NormalTopBarHeight;
                }

                MinimizeButton.Margin = new Thickness(0, 0, 0, 0);
                MaximizeButton.Margin = new Thickness(0, 0, 0, 0);
                CloseButton.Margin = new Thickness(0, 0, 0, 0);
            }
            else
            {
                this.WindowState = WindowState.Maximized;

                if (MainContent.Content is DashboardControl dashboard)
                {
                    dashboard.TopBar.Height = MaximizedTopBarHeight;
                }

                MinimizeButton.Margin = new Thickness(0, 4, 0, 0);
                MaximizeButton.Margin = new Thickness(0, 4, 0, 0);
                CloseButton.Margin = new Thickness(0, 4, 4, 0);
            }

            MaximizeIcon.Text = (this.WindowState == WindowState.Maximized) ? "\uE923" : "\uE922";
        }
    }
}


