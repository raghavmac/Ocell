﻿using System;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using DanielVaughan.ComponentModel;
using DanielVaughan;
using DanielVaughan.Windows;
using Ocell.Library;
using System.Collections.Generic;
using Ocell.Library.Twitter;
using System.Collections.ObjectModel;
using TweetSharp;
using System.Device.Location;
using System.Linq;
using Ocell.Pages.Search;

namespace Ocell.Pages
{
    public class TopicsModel : ExtendedViewModelBase
    {
        GeoCoordinateWatcher geoWatcher;

        string placeName;
        public string PlaceName
        {
            get { return placeName; }
            set { Assign("PlaceName", ref placeName, value); }
        }

        object listSelection;
        public object ListSelection
        {
            get { return listSelection; }
            set { Assign("ListSelection", ref listSelection, value); }
        }

        IEnumerable<TwitterTrend> collection;
        public IEnumerable<TwitterTrend> Collection
        {
            get { return collection; }
            set { Assign("Collection", ref collection, value); }
        }

        ObservableCollection<string> locations;
        public ObservableCollection<string> Locations
        {
            get { return locations; }
            set { Assign("Locations", ref locations, value); }
        }

        string selectedLocation;
        public string SelectedLocation
        {
            get { return selectedLocation; }
            set { Assign("SelectedLocation", ref selectedLocation, value); }
        }

        Dictionary<string, long> LocationMap;

        DelegateCommand refresh;
        public ICommand Refresh
        {
            get { return refresh; }
        }

        DelegateCommand showGlobal;
        public ICommand ShowGlobal
        {
            get { return showGlobal; }
        }

        DelegateCommand showLocations;
        public ICommand ShowLocations
        {
            get { return showLocations; }
        }


        long currentLocation = 1;

        public TopicsModel()
            : base("TrendingTopics")
        {
            this.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == "ListSelection")
                        OnSelectionChanged();
                    if (e.PropertyName == "SelectedLocation")
                        UserChoseLocation();
                };

            geoWatcher = new GeoCoordinateWatcher();
            if (Config.EnabledGeolocation == true)
                geoWatcher.Start();

            Locations = new ObservableCollection<string>();
            LocationMap = new Dictionary<string, long>();
            refresh = new DelegateCommand((obj) => GetTopics());
            showGlobal = new DelegateCommand((obj) => { currentLocation = 1; PlaceName = Localization.Resources.Global; GetTopics(); });
            showLocations = new DelegateCommand((obj) => RaiseShowLocations(), (obj) => Locations.Any());
            
            ServiceDispatcher.GetCurrentService().ListAvailableTrendsLocations(ReceiveLocations);

            IsLoading = true;
            if (Config.EnabledGeolocation == true && (Config.TopicPlaceId == -1 || Config.TopicPlaceId == null))
                ServiceDispatcher.GetCurrentService().ListClosestTrendsLocations(new ListClosestTrendsLocationsOptions{ Lat = geoWatcher.Position.Location.Latitude, Long = geoWatcher.Position.Location.Longitude }, ReceiveMyLocation);
            else
            {
                currentLocation = Config.TopicPlaceId.HasValue ? (long)Config.TopicPlaceId : 1;
                PlaceName = Config.TopicPlace;
                GetTopics();
            }
        }

        public event EventHandler ShowLocationsPicker;

        private void RaiseShowLocations()
        {
            if (ShowLocationsPicker != null)
                ShowLocationsPicker(this, new EventArgs());
        }

        private void ReceiveMyLocation(IEnumerable<WhereOnEarthLocation> locs, TwitterResponse resp)
        {
            if (resp.StatusCode == HttpStatusCode.OK && locs != null && locs.Any())
            {
                var loc = locs.First();
                PlaceName = loc.Name;
                currentLocation = loc.WoeId;
                Config.TopicPlace = PlaceName;
                Config.TopicPlaceId = currentLocation;
                GetTopics();
            }
        }

        private void GetTopics()
        {
            IsLoading = true;
            ServiceDispatcher.GetCurrentService().ListLocalTrendsFor(new ListLocalTrendsForOptions { Id = (int)currentLocation }, ReceiveTrends);
        }

        private void ReceiveLocations(IEnumerable<WhereOnEarthLocation> locs, TwitterResponse resp)
        {
            if (resp.StatusCode == HttpStatusCode.OK && locs != null && locs.Any())
            {
                Deployment.Current.Dispatcher.InvokeIfRequired(() =>
                {
                    foreach (var loc in locs.OrderBy(x => x.Name))
                    {
                        if (!Locations.Contains(loc.Name))
                            Locations.Add(loc.Name);

                        if (!LocationMap.ContainsKey(loc.Name))
                            LocationMap.Add(loc.Name, loc.WoeId);
                    }
                    showLocations.RaiseCanExecuteChanged();
                });
            }
        }

        void UserChoseLocation()
        {
            PlaceName = SelectedLocation;
            LocationMap.TryGetValue(SelectedLocation, out currentLocation);
            Config.TopicPlace = PlaceName;
            Config.TopicPlaceId = currentLocation;
            GetTopics();
        }

        private void ReceiveTrends(TweetSharp.TwitterTrends Trends, TweetSharp.TwitterResponse Response)
        {
            IsLoading = false;
            if (Response.StatusCode != HttpStatusCode.OK)
            {
                MessageService.ShowError(Localization.Resources.ErrorLoadingTT);
                GoBack();
            }

            Collection = Trends;
        }

        private void OnSelectionChanged()
        {
            TwitterTrend trend = ListSelection as TwitterTrend;
            
            if (trend == null)
                return;

            ListSelection = null;

            var resource = new TwitterResource
               {
                   Data = trend.Name,
                   Type = ResourceType.Search,
                   User = DataTransfer.CurrentAccount
               };
            ResourceViewModel.Resource = resource;
            Navigate(Uris.ResourceView);
        }
    }
}
