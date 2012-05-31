﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Documents;
using Microsoft.Phone.Controls;
using Microsoft.Phone.Tasks;
using Microsoft.Phone.Shell;
using Ocell.Library;
using Ocell.Library.Twitter;
using TweetSharp;
using Ocell.Library.Filtering;
using System.Windows.Controls;
using DanielVaughan;
using DanielVaughan.Services;

namespace Ocell.Pages.Elements
{
    public partial class Tweet : PhoneApplicationPage
    {
        TweetModel viewModel;

        public Tweet()
        {
            InitializeComponent();
            ThemeFunctions.ChangeBackgroundIfLightTheme(LayoutRoot);

            viewModel = new TweetModel();
            DataContext = viewModel;

            this.Loaded += new RoutedEventHandler(Tweet_Loaded);
            img.ImageFailed += viewModel.ImageFailed;
            img.ImageOpened += viewModel.ImageOpened;
            img.Tap += viewModel.ImageTapped;
        }

        void Tweet_Loaded(object sender, RoutedEventArgs e)
        {
            CreateText(viewModel.Tweet);
            AdjustMargins();

            ContentPanel.UpdateLayout();
        }

        private void AdjustMargins()
        {
            SecondBlock.Margin = new Thickness(SecondBlock.Margin.Left, Text.ActualHeight + Text.Margin.Top + 10,
                SecondBlock.Margin.Right, SecondBlock.Margin.Bottom);
        }

        private void CreateText(ITweetable Status)
        {
            var paragraph = new Paragraph();
            var runs = new List<Inline>();

            Text.Blocks.Clear();

            string TweetText = Status.Text;
            string PreviousText;
            int i = 0;

            foreach (var Entity in viewModel.Tweet.Entities)
            {
                if (Entity.StartIndex > i)
                {
                    PreviousText = TweetText.Substring(i, Entity.StartIndex - i);
                    runs.Add(new Run { Text = HttpUtility.HtmlDecode(PreviousText) });
                }

                i = Entity.EndIndex;

                switch (Entity.EntityType)
                {
                    case TwitterEntityType.HashTag:
                        runs.Add(CreateHashtagLink((TwitterHashTag)Entity));
                        break;

                    case TwitterEntityType.Mention:
                        runs.Add(CreateMentionLink((TwitterMention)Entity));
                        break;

                    case TwitterEntityType.Url:
                        runs.Add(CreateUrlLink((TwitterUrl)Entity));
                        break;
                    case TwitterEntityType.Media:
                        runs.Add(CreateMediaLink((TwitterMedia)Entity));
                        break;
                }
            }

            if (i < TweetText.Length)
                runs.Add(new Run
                {
                    Text = HttpUtility.HtmlDecode(TweetText.Substring(i))
                });

            foreach (var run in runs)
                paragraph.Inlines.Add(run);

            Text.Blocks.Add(paragraph);

            Text.UpdateLayout();
        }

        Inline CreateBaseLink(string content, string contextHeader, string contextTag, MenuItem customButton = null)
        {
            var link = new HyperlinkButton
            {
                Content = content,
                FontSize = Text.FontSize,
                FontWeight = Text.FontWeight,
                FontStretch = Text.FontStretch,
                FontFamily = Text.FontFamily,
                Margin = new Thickness(-10, -5, -10, -8)
            };

            link.Click += new RoutedEventHandler(link_Click);


            MenuItem item = new MenuItem
            {
                Header = contextHeader,
                Tag = contextTag
            };
            item.Click += new RoutedEventHandler(CopyLink);

            ContextMenu menu = new ContextMenu();
            menu.Items.Add(item);
            if (customButton != null)
                menu.Items.Add(customButton);

            ContextMenuService.SetContextMenu(link, menu);

            InlineUIContainer container = new InlineUIContainer();
            container.Child = link;
            return container;
        }

        Inline CreateHashtagLink(TwitterHashTag Hashtag)
        {
            MenuItem item = new MenuItem
            {
                Header = "mute this hashtag",
            };
            item.Click += (sender, e) =>
                {
                    var filter = FilterManager.SetupMute(FilterType.Text, "#" + Hashtag.Text);
                    Dependency.Resolve<IMessageService>().ShowLightNotification("Muted until " + filter.IsValidUntil.ToString("f"));
                };
            return CreateBaseLink("#" + Hashtag.Text, "copy hashtag", "#" + Hashtag.Text, item);
        }

        Inline CreateMentionLink(TwitterMention Mention)
        {
            MenuItem item = new MenuItem
            {
                Header = "mute this user",
            };
            item.Click += (sender, e) =>
            {
                var filter = FilterManager.SetupMute(FilterType.User, Mention.ScreenName);
                Dependency.Resolve<IMessageService>().ShowLightNotification("Muted until " + filter.IsValidUntil.ToString("f"));
            };
            return CreateBaseLink("@" + Mention.ScreenName, "copy user name", Mention.ScreenName, item);
        }

        Inline CreateUrlLink(TwitterUrl URL)
        {
            MenuItem item = new MenuItem
            {
                Header = "mute this domain",
            };
            item.Click += (sender, e) =>
            {
                Uri uri;
                if (Uri.TryCreate(URL.ExpandedValue, UriKind.Absolute, out uri))
                {
                    var filter = FilterManager.SetupMute(FilterType.Text, uri.Host);
                    Dependency.Resolve<IMessageService>().ShowLightNotification("Muted until " + filter.IsValidUntil.ToString("f"));
                }
                else
                    Dependency.Resolve<IMessageService>().ShowError("Ooops, that's not a valid URL.");
            };
            return CreateBaseLink(TweetTextConverter.TrimUrl(URL.ExpandedValue), "copy link", URL.ExpandedValue);
        }

        Inline CreateMediaLink(TwitterMedia Media)
        {
            MenuItem item = new MenuItem
            {
                Header = "mute this domain",
            };
            item.Click += (sender, e) =>
            {
                Uri uri;
                if (Uri.TryCreate(Media.DisplayUrl, UriKind.Absolute, out uri))
                {
                    var filter = FilterManager.SetupMute(FilterType.Text, uri.Host);
                    Dependency.Resolve<IMessageService>().ShowLightNotification("Muted until " + filter.IsValidUntil.ToString("f"));
                }
                else
                    Dependency.Resolve<IMessageService>().ShowError("Ooops, that's not a valid URL.");
            };
            return CreateBaseLink(Media.DisplayUrl, "copy link", Media.DisplayUrl);
        }

        void CopyLink(object sender, RoutedEventArgs e)
        {
            MenuItem item = sender as MenuItem;
            if (item != null && item.Tag is string && !(string.IsNullOrWhiteSpace(item.Tag as string)))
                Clipboard.SetText(item.Tag as string);
        }

        void link_Click(object sender, RoutedEventArgs e)
        {
            Hyperlink link = sender as Hyperlink;
            Uri uri;
            WebBrowserTask browser;

            if (link == null || string.IsNullOrWhiteSpace(link.TargetName))
                return;

            if (Uri.TryCreate(link.TargetName, UriKind.Absolute, out uri) ||
                (link.TargetName.StartsWith("www.") && Uri.TryCreate("http://" + link.TargetName, UriKind.Absolute, out uri)))
            {
                browser = new WebBrowserTask();
                browser.Uri = uri;
                browser.Show();
            }
            else if (link.TargetName[0] == '@')
                NavigationService.Navigate(new Uri("/Pages/Elements/User.xaml?user=" + link.TargetName.Substring(0), UriKind.Relative));
            else if (link.TargetName[0] == '#')
            {
                DataTransfer.Search = link.TargetName;
                NavigationService.Navigate(new Uri("/Pages/Search/Search.xaml?q=" + link.TargetName, UriKind.Relative));
            }

        }

        private void Grid_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            NavigationService.Navigate(new Uri("/Pages/Elements/User.xaml?user=" + viewModel.Tweet.Author.ScreenName, UriKind.Relative));
        }

        private void Image_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var image = sender as Image;
            if(image != null & image.Tag is ITweeter)
            {
            NavigationService.Navigate(new Uri("/Pages/Elements/User.xaml?user=" + (image.Tag as ITweeter).ScreenName, UriKind.Relative));
            }
        }

        private void Replies_Tap(object sender, EventArgs e)
        {
            NavigationService.Navigate(Uris.Conversation);
        }

        private ITweetableFilter CreateNewFilter(FilterType type, string data)
        {
            if (Config.GlobalFilter == null)
                Config.GlobalFilter = new ColumnFilter();

            if (Config.DefaultMuteTime == null)
                Config.DefaultMuteTime = TimeSpan.FromHours(8);

            ITweetableFilter filter = new ITweetableFilter();
            filter.Type = type;
            filter.Filter = data;
            if (Config.DefaultMuteTime == TimeSpan.MaxValue)
                filter.IsValidUntil = DateTime.MaxValue;
            else
                filter.IsValidUntil = DateTime.Now + (TimeSpan)Config.DefaultMuteTime;
            filter.Inclusion = IncludeOrExclude.Exclude;

            Config.GlobalFilter.AddFilter(filter);
            Config.GlobalFilter = Config.GlobalFilter;

            return filter;
        }

        private void MuteUser_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            var filter = FilterManager.SetupMute(FilterType.User, viewModel.Tweet.Author.ScreenName);
            Dependency.Resolve<IMessageService>().
                ShowMessage("The user " + viewModel.Tweet.Author.ScreenName +
                " is now muted until " + filter.IsValidUntil.ToString("f") + ".", "");
            viewModel.IsMuting = false;
        }

        private void MuteHashtags_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            ITweetableFilter filter = null;
            string message = "";
            foreach (var entity in viewModel.Tweet.Entities)
            {
                if (entity.EntityType == TwitterEntityType.HashTag)
                {
                    filter = FilterManager.SetupMute(FilterType.Text, ((TwitterHashTag)entity).Text);
                    message += ((TwitterHashTag)entity).Text + ", ";
                }
            }
            if (message == "")
                Dependency.Resolve<IMessageService>().ShowMessage("No hashtags to mute");
            else
                Dependency.Resolve<IMessageService>().
                ShowMessage("The hashtag(s) " + message.Substring(0, message.Length - 2) +
                " are now muted until " + filter.IsValidUntil.ToString("f") + ".");
            viewModel.IsMuting = false;
        }

        private void Source_Tap(object sender, System.Windows.Input.GestureEventArgs e)
        {
            RemoveHTML conv = new RemoveHTML();
            string source = conv.Convert(viewModel.Tweet.Source, null, null, null) as string;
            var filter = FilterManager.SetupMute(FilterType.Source, source);
            Dependency.Resolve<IMessageService>().ShowMessage("The source " + source + 
                " is now muted until " + filter.IsValidUntil.ToString("f") + ".");
            viewModel.IsMuting = false;
        }

        void HideMuteGrid(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
            viewModel.IsMuting = false;
            this.BackKeyPress -= HideMuteGrid;
        }

        private void MuteBtn_Click(object sender, EventArgs e)
        {
            this.BackKeyPress += HideMuteGrid;
            viewModel.IsMuting = true;
        }


    }
}