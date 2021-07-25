using Files.Helpers;
using Files.ViewModels;
using Files.Views;
using Microsoft.Toolkit.Uwp;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Files.UserControls.MultitaskingControl
{
    public class BaseMultitaskingControl : TabView, IMultitaskingControl, INotifyPropertyChanged
    {
        private static bool isRestoringClosedTab = false; // Avoid reopening two tabs

        public const string TabDropHandledIdentifier = "FilesTabViewItemDropHandled";

        public const string TabPathIdentifier = "FilesTabViewItemPath";

        public event PropertyChangedEventHandler PropertyChanged;

        public BaseMultitaskingControl()
        {
            SelectionChanged += TabStrip_SelectionChanged;
            TabItemsChanged += TabView_TabItemsChanged;
        }

        public ObservableCollection<TabItem> Items => MainPageViewModel.AppInstances;

        // RecentlyClosedTabs is shared between all multitasking controls
        public static List<ITabItem> RecentlyClosedTabs { get; private set; } = new List<ITabItem>();

        private void TabView_TabItemsChanged(TabView sender, Windows.Foundation.Collections.IVectorChangedEventArgs args)
        {
            SelectedIndex = Items.IndexOf(SelectedItem as TabItem);
        }

        protected void TabStrip_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SelectedIndex >= 0)
            {
                foreach(TabItem item in TabItems)
                {
                    var tabViewItem = ContainerFromItem(item) as TabViewItem;
                    if (tabViewItem != null && tabViewItem.IsLoaded)
                    {
                        var x = tabViewItem.ContentTemplate.LoadContent() as Frame;
                        if (x.IsLoaded)
                        {
                            (x.Content as ITabItemContent).IsCurrentInstance = tabViewItem.IsSelected;
                        }
                    }
                }
            }
        }

        public async Task AddNewTabAsync()
        {
            await MainPageViewModel.AddNewTabByPathAsync(typeof(PaneHolderPage), "NewTab".GetLocalized());
            //SelectedIndex = Items.Count - 1;
        }

        protected void TabStrip_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            CloseTab(args.Item as TabItem);
        }

        protected async void TabView_AddTabButtonClick(TabView sender, object args)
        {
            await MainPageViewModel.AddNewTabByPathAsync(typeof(PaneHolderPage), "NewTab".GetLocalized());
            //this.SelectedIndex = TabItems.Count - 1;
        }


        public async void AddNewTabAtIndex(object sender, RoutedEventArgs e)
        {
            await AddNewTabAsync();
        }

        public async void DuplicateTabAtIndex(object sender, RoutedEventArgs e)
        {
            var tabItem = ((FrameworkElement)sender).DataContext as TabItem;
            var index = Items.IndexOf(tabItem);

            if (Items[index].TabItemArguments != null)
            {
                var tabArgs = Items[index].TabItemArguments;
                await MainPageViewModel.AddNewTabByParam(tabArgs.InitialPageType, tabArgs.NavigationArg, index + 1);
            }
            else
            {
                await MainPageViewModel.AddNewTabByPathAsync(typeof(PaneHolderPage), "NewTab".GetLocalized());
            }
        }

        public void CloseTabsToTheRight(object sender, RoutedEventArgs e)
        {
            MultitaskingTabsHelpers.CloseTabsToTheRight(((FrameworkElement)sender).DataContext as TabItem, this);
        }

        public async void ReopenClosedTab(object sender, RoutedEventArgs e)
        {
            if (!isRestoringClosedTab && RecentlyClosedTabs.Any())
            {
                isRestoringClosedTab = true;
                ITabItem lastTab = RecentlyClosedTabs.Last();
                RecentlyClosedTabs.Remove(lastTab);
                await MainPageViewModel.AddNewTabByParam(lastTab.TabItemArguments.InitialPageType, lastTab.TabItemArguments.NavigationArg);
                isRestoringClosedTab = false;
            }
        }

        public async void MoveTabToNewWindow(object sender, RoutedEventArgs e)
        {
            await MultitaskingTabsHelpers.MoveTabToNewWindow(((FrameworkElement)sender).DataContext as TabItem, this);
        }

        public void CloseTab(TabItem tabItem)
        {
            if (Items.Count == 1)
            {
                App.CloseApp();
            }
            else if (Items.Count > 1)
            {
                Items.Remove(tabItem);
                tabItem?.Unload(); // Dispose and save tab arguments
                RecentlyClosedTabs.Add((ITabItem)tabItem);
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void SetLoadingIndicatorStatus(ITabItem item, bool loading)
        {
            var tabItem = ContainerFromItem(item) as Control;
            if(tabItem is null)
            {
                return;
            }

            if (loading)
            {
                VisualStateManager.GoToState(tabItem, "Loading", false);
            }
            else
            {
                VisualStateManager.GoToState(tabItem, "NotLoading", false);
            }
        }
    }
}