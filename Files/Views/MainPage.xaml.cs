using Files.DataModels.NavigationControlItems;
using Files.Filesystem;
using Files.Helpers;
using Files.UserControls;
using Files.UserControls.MultitaskingControl;
using Files.ViewModels;
using Microsoft.Toolkit.Uwp;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.Resources.Core;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Navigation;

namespace Files.Views
{
    /// <summary>
    /// The root page of Files
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public SettingsViewModel AppSettings => App.AppSettings;

        public MainPageViewModel ViewModel
        {
            get => (MainPageViewModel)DataContext;
            set => DataContext = value;
        }

        public SidebarViewModel SidebarAdaptiveViewModel = new SidebarViewModel();

        public StatusCenterViewModel StatusCenterViewModel => App.StatusCenterViewModel;

        public MainPage()
        {
            this.InitializeComponent();

            ApplicationView.PreferredLaunchWindowingMode = ApplicationViewWindowingMode.Auto;
            var CoreTitleBar = CoreApplication.GetCurrentView().TitleBar;
            CoreTitleBar.ExtendViewIntoTitleBar = true;
            CoreTitleBar.LayoutMetricsChanged += TitleBar_LayoutMetricsChanged;
            var flowDirectionSetting = ResourceContext.GetForCurrentView().QualifierValues["LayoutDirection"];

            if (flowDirectionSetting == "RTL")
            {
                FlowDirection = FlowDirection.RightToLeft;
            }
            AllowDrop = true;
        }

        private void TitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            RightMarginGrid.Margin = new Thickness(0, 0, sender.SystemOverlayRightInset, 0);
        }

        private void HorizontalMultitaskingControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (!(ViewModel.MultitaskingControl is HorizontalMultitaskingControl))
            {
                // Set multitasking control if changed and subscribe it to event for sidebar items updating
                if (ViewModel.MultitaskingControl != null)
                {
                    ViewModel.MultitaskingControl.CurrentInstanceChanged -= MultitaskingControl_CurrentInstanceChanged;
                }
                ViewModel.MultitaskingControl = horizontalMultitaskingControl;
                ViewModel.MultitaskingControl.CurrentInstanceChanged += MultitaskingControl_CurrentInstanceChanged;
            }
        }

        public void TabItemContent_ContentChanged(object sender, TabItemArguments e)
        {
            if (SidebarAdaptiveViewModel.PaneHolder != null)
            {
                SidebarAdaptiveViewModel.UpdateSidebarSelectedItemFromArgs((e.NavigationArg as PaneNavigationArguments).LeftPaneNavPathParam);
                UpdateStatusBarProperties();
            }
        }

        public void MultitaskingControl_CurrentInstanceChanged(object sender, CurrentInstanceChangedEventArgs e)
        {
            if (SidebarAdaptiveViewModel.PaneHolder != null)
            {
                SidebarAdaptiveViewModel.PaneHolder.ActivePaneChanged -= PaneHolder_ActivePaneChanged;
            }
            SidebarAdaptiveViewModel.PaneHolder = e.CurrentInstance as IPaneHolder;
            SidebarAdaptiveViewModel.PaneHolder.ActivePaneChanged += PaneHolder_ActivePaneChanged;
            SidebarAdaptiveViewModel.NotifyInstanceRelatedPropertiesChanged((e.CurrentInstance.TabItemArguments?.NavigationArg as PaneNavigationArguments).LeftPaneNavPathParam);
            UpdateStatusBarProperties();
            e.CurrentInstance.ContentChanged -= TabItemContent_ContentChanged;
            e.CurrentInstance.ContentChanged += TabItemContent_ContentChanged;
        }

        private void PaneHolder_ActivePaneChanged(object sender, EventArgs e)
        {
            SidebarAdaptiveViewModel.NotifyInstanceRelatedPropertiesChanged(SidebarAdaptiveViewModel.PaneHolder.ActivePane.TabItemArguments.NavigationArg.ToString());
            UpdateStatusBarProperties();
        }
        
        private void UpdateStatusBarProperties()
        {
            if(StatusBarControl != null)
            {
                StatusBarControl.DirectoryPropertiesViewModel = SidebarAdaptiveViewModel.PaneHolder?.ActivePane.SlimContentPage?.DirectoryPropertiesViewModel;
                StatusBarControl.SelectedItemsPropertiesViewModel = SidebarAdaptiveViewModel.PaneHolder?.ActivePane.SlimContentPage?.SelectedItemsPropertiesViewModel;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            ViewModel.OnNavigatedTo(e);
            SidebarControl.SidebarItemInvoked += SidebarControl_SidebarItemInvoked;
            SidebarControl.SidebarItemPropertiesInvoked += SidebarControl_SidebarItemPropertiesInvoked;
            SidebarControl.SidebarItemDropped += SidebarControl_SidebarItemDropped;
            SidebarControl.RecycleBinItemRightTapped += SidebarControl_RecycleBinItemRightTapped;
            SidebarControl.SidebarItemNewPaneInvoked += SidebarControl_SidebarItemNewPaneInvoked;
        }

        private async void SidebarControl_RecycleBinItemRightTapped(object sender, EventArgs e)
        {
            var recycleBinHasItems = false;
            var connection = await AppServiceConnectionHelper.Instance;
            if (connection != null)
            {
                var value = new ValueSet
                {
                    { "Arguments", "RecycleBin" },
                    { "action", "Query" }
                };
                var (status, response) = await connection.SendMessageForResponseAsync(value);
                if (status == AppServiceResponseStatus.Success && response.TryGetValue("NumItems", out var numItems))
                {
                    recycleBinHasItems = (long)numItems > 0;
                }
            }
            SidebarControl.RecycleBinHasItems = recycleBinHasItems;
        }

        private async void SidebarControl_SidebarItemDropped(object sender, SidebarItemDroppedEventArgs e)
        {
            await SidebarAdaptiveViewModel.FilesystemHelpers.PerformOperationTypeAsync(e.AcceptedOperation, e.Package, e.ItemPath, false, true);
        }

        private async void SidebarControl_SidebarItemPropertiesInvoked(object sender, SidebarItemPropertiesInvokedEventArgs e)
        {
            if (e.InvokedItemDataContext is DriveItem)
            {
                await FilePropertiesHelpers.OpenPropertiesWindowAsync(e.InvokedItemDataContext, SidebarAdaptiveViewModel.PaneHolder.ActivePane);
            }
            else if (e.InvokedItemDataContext is LibraryLocationItem library)
            {
                await FilePropertiesHelpers.OpenPropertiesWindowAsync(new LibraryItem(library), SidebarAdaptiveViewModel.PaneHolder.ActivePane);
            }
            else if (e.InvokedItemDataContext is LocationItem)
            {
                ListedItem listedItem = new ListedItem(null)
                {
                    ItemPath = (e.InvokedItemDataContext as LocationItem).Path,
                    ItemName = (e.InvokedItemDataContext as LocationItem).Text,
                    PrimaryItemAttribute = StorageItemTypes.Folder,
                    ItemType = "FileFolderListItem".GetLocalized(),
                    LoadFolderGlyph = true
                };
                await FilePropertiesHelpers.OpenPropertiesWindowAsync(listedItem, SidebarAdaptiveViewModel.PaneHolder.ActivePane);
            }
        }

        private void SidebarControl_SidebarItemNewPaneInvoked(object sender, SidebarItemNewPaneInvokedEventArgs e)
        {
            if (e.InvokedItemDataContext is INavigationControlItem navItem)
            {
                SidebarAdaptiveViewModel.PaneHolder.OpenPathInNewPane(navItem.Path);
            }
        }

        private void SidebarControl_SidebarItemInvoked(object sender, SidebarItemInvokedEventArgs e)
        {
            var invokedItemContainer = e.InvokedItemContainer;

            // All items must have DataContext except Settings item
            if (invokedItemContainer.DataContext is MainPageViewModel)
            {
                Frame rootFrame = Window.Current.Content as Frame;
                rootFrame.Navigate(typeof(Settings));

                return;
            }

            string navigationPath; // path to navigate
            Type sourcePageType = null; // type of page to navigate

            switch ((invokedItemContainer.DataContext as INavigationControlItem).ItemType)
            {
                case NavigationControlItemType.Location:
                    {
                        var ItemPath = (invokedItemContainer.DataContext as INavigationControlItem).Path; // Get the path of the invoked item

                        if (string.IsNullOrEmpty(ItemPath)) // Section item
                        {
                            navigationPath = invokedItemContainer.Tag?.ToString();
                        }
                        else if (ItemPath.Equals("Home", StringComparison.OrdinalIgnoreCase)) // Home item
                        {
                            if (ItemPath.Equals(SidebarAdaptiveViewModel.SidebarSelectedItem?.Path, StringComparison.OrdinalIgnoreCase))
                            {
                                return; // return if already selected
                            }

                            navigationPath = "NewTab".GetLocalized();
                            sourcePageType = typeof(WidgetsPage);
                        }
                        else // Any other item
                        {
                            navigationPath = invokedItemContainer.Tag?.ToString();
                        }

                        break;
                    }
                default:
                    {
                        navigationPath = invokedItemContainer.Tag?.ToString();
                        break;
                    }
            }

            SidebarAdaptiveViewModel.PaneHolder.ActivePane?.NavigateToPath(navigationPath, sourcePageType);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            if (SidebarControl != null)
            {
                SidebarControl.SidebarItemInvoked -= SidebarControl_SidebarItemInvoked;
                SidebarControl.SidebarItemPropertiesInvoked -= SidebarControl_SidebarItemPropertiesInvoked;
                SidebarControl.SidebarItemDropped -= SidebarControl_SidebarItemDropped;
                SidebarControl.RecycleBinItemRightTapped -= SidebarControl_RecycleBinItemRightTapped;
                SidebarControl.SidebarItemNewPaneInvoked -= SidebarControl_SidebarItemNewPaneInvoked;
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // Defers the status bar loading until after the page has loaded to improve startup perf
            FindName(nameof(StatusBarControl));
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            BeginTestButton.IsEnabled = false;
            try
            {
                await NavigationHelpers.OpenPath(@"C:\Windows\System32", SidebarAdaptiveViewModel.PaneHolder?.ActivePane);
                SidebarAdaptiveViewModel.PaneHolder.ActivePane.FilesystemViewModel.ItemLoadStatusChanged += FilesystemViewModel_ItemLoadStatusChanged;
            }
            catch (Exception)
            {
            }
        }

        private void FilesystemViewModel_ItemLoadStatusChanged(object sender, ItemLoadStatusChangedEventArgs e)
        {
            if (e.Status == ItemLoadStatusChangedEventArgs.ItemLoadStatus.Complete)
            {
                Test();
                SidebarAdaptiveViewModel.PaneHolder.ActivePane.FilesystemViewModel.ItemLoadStatusChanged -= FilesystemViewModel_ItemLoadStatusChanged;
            }
        }

        private async void Test()
        {
            Stopwatch sw = new Stopwatch();

            var numOfTests = NumberOfTestssBox.Value;
            long enumTimeSum = 0;
            long itemPropsTimeSum = 0;

            sw.Start();

            for (int i = 1; i <= numOfTests; i++)
            {
                await SidebarAdaptiveViewModel.PaneHolder.ActivePane.FilesystemViewModel.RefreshItems(null);
                var enumTime = sw.ElapsedMilliseconds;
                sw.Restart();

                foreach (var item in SidebarAdaptiveViewModel.PaneHolder.ActivePane.FilesystemViewModel.FilesAndFolders.ToList().GetRange(0,300))
                {
                    if(!item.ItemPropertiesInitialized)
                    {
                        item.ItemPropertiesInitialized = true;
                        await SidebarAdaptiveViewModel.PaneHolder.ActivePane.FilesystemViewModel.LoadExtendedItemProperties(item, 20);
                    }
                }

                sw.Stop();
                (OutputTextBlock.Blocks[0] as Paragraph).Inlines.Add(new Run()
                {
                    Text = string.Format("{0, 16}  {1, 40}ms  {2, 40}ms", i, enumTime, sw.ElapsedMilliseconds)
                });

                (OutputTextBlock.Blocks[0] as Paragraph).Inlines.Add(new LineBreak());

                enumTimeSum += enumTime;
                itemPropsTimeSum += sw.ElapsedMilliseconds;
                sw.Restart();
            }

            (OutputTextBlock.Blocks[0] as Paragraph).Inlines.Add(new LineBreak());
            (OutputTextBlock.Blocks[0] as Paragraph).Inlines.Add(new Run()
            {
                Text = string.Format("{0, 16}  {1, 40}ms {2, 40}ms", "Avg", enumTimeSum / numOfTests, itemPropsTimeSum / numOfTests)
            });


            BeginTestButton.IsEnabled = true;
        }

        private void OutputTextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            (OutputTextBlock.Blocks[0] as Paragraph).Inlines.Add(new Run()
            {
                Text = string.Format("{0, 16} | {1, 40} | {2, 40}", "Test #", "Enumeration", "300 item props")
            });
            (OutputTextBlock.Blocks[0] as Paragraph).Inlines.Add(new LineBreak());
        }
    }
}