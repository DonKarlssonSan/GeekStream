﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GeekStream.Core.Views;
using GeekStream.Core.Domain;
using GeekStream.Core.Commands;
using GeekStream.Core;
using System.ServiceModel.Syndication;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using GeekStream.Core.Queries;
using System.Configuration;
using OrigoDB.Core;

namespace GeekStream.Admin
{
    class Program
    {
        private IEngine<GeekStreamModel> _geekStreamDb;
        private string[] _args;


        static void Main(string[] args)
        {
            new Program(args).Run();
            Console.WriteLine("Hit enter to exit");
            Console.ReadLine();
        }

        private void Run()
        {

            //Set up the db connection or embedded engine
            string connectionString = ConfigurationManager.AppSettings["geekstream"];
            connectionString = connectionString ?? "mode=embedded";
            _geekStreamDb = Engine.For<GeekStreamModel>(connectionString);

            if (_args.Length >= 2 && _args[0] == "-a") AddUrls(_args.Skip(1));
            else if (_args.Length == 2 && _args[0] == "-o") AddFeedsFromOpml(_args[1]);
            else if (_args.Length == 2 && _args[0] == "-c") CollectContinously(_args[1]);
            else if (_args.Length == 2 && _args[0] == "-r") RemoveFeed(_args[1]);
            else if (_args.Length == 2 && _args[0] == "-f") Find(_args[1]);
            else if (_args.Length == 1 && _args[0] == "-s") ShowStatistics();
            else
            {
                Console.WriteLine("Invalid command line.");
                Console.WriteLine("usage: geekstream.admin <command> <args>");
                Console.WriteLine("add urls: -a <url> [...]");
                Console.WriteLine("add all urls from opml file: -o <url>");
                Console.WriteLine("collect from sources due: -c <pollinterval_in_minutes>");
                Console.WriteLine("remove url: -r <url> or <feedid>");
                Console.WriteLine("find feed by name or description: -f  <regex>");
                Console.WriteLine("show statistics: -s");
            }

        }

        private void ShowStatistics()
        {
            var statistics = _geekStreamDb.Execute(new GetStatisticsQuery());
            Console.WriteLine("--------------------- Statistics -------------");
            Console.WriteLine("Feeds    : {0}", statistics.TotalFeeds);
            Console.WriteLine("Items    : {0}", statistics.TotalFeedItems);
            Console.WriteLine("Words    : {0}", statistics.TotalKeywords);
            Console.WriteLine("Unique   : {0}", statistics.UniqueKeywords);
        }

        private void Find(string regex)
        {
            var query = new GetFeedsByRegexQuery(regex);
            FeedView[] feeds = _geekStreamDb.Execute(query);
            Console.WriteLine("Showing {0} feeds", feeds.Length);
            foreach (var feedView in feeds)
            {
                Console.WriteLine("--------------------------------------------------------------");
                Console.WriteLine("Id:    {0}", feedView.LongId);
                Console.WriteLine("Title: {0}", feedView.Title);
                Console.WriteLine("Url:   {0}", feedView.Url);
                Console.WriteLine("--------------------------------------------------------------");
            }
        }

        private void RemoveFeed(string urlOrId)
        {
            Command<GeekStreamModel> command;
            int feedId;
            if (Int32.TryParse(urlOrId, out feedId))
            {
                command = new RemoveFeedByIdCommand(feedId);
            }
            else
            {
                command = new RemoveFeedByUrlCommand(urlOrId);
            }

            try
            {
                _geekStreamDb.Execute(command);
                Console.WriteLine("ok");
            }
            catch (CommandAbortedException)
            {
                Console.WriteLine("no such feed");
            }
        }

        private void AddFeedsFromOpml(string path)
        {
            List<string> urls = new List<string>();
            HtmlDocument doc = new HtmlDocument();
            doc.Load(path);
            foreach (HtmlNode node in doc.DocumentNode.SelectNodes("//outline"))
            {
                string xmlUrl = node.GetAttributeValue("xmlUrl", "");
                if (xmlUrl != "")
                {
                    urls.Add(xmlUrl);
                }
            }
            AddUrls(urls);
        }

        public Program(string[] args)
        {
            _args = args;
        }

        private void AddUrls(IEnumerable<string> urls)
        {
            foreach (var url in urls)
            {
                AddUrl(url);
            }
            //Parallel.ForEach(urls, AddUrl); //server or client is broken (not thread safe)
        }

        private void AddUrl(string url)
        {
            var query = new GetFeedByUrl(url);
            FeedView feed = _geekStreamDb.Execute(query);

            if (feed == null) AddFeed(url);
            else Console.WriteLine("[=] " + url);

        }

        private void AddFeed(string url)
        {
            try
            {
                SyndicationFeed syndicationFeed = FeedCollector<FeedView>.GetFeed(url);
                CreateFeed(syndicationFeed, url);
                Console.WriteLine("[+] " + url);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[!] " + url);
                Console.WriteLine(ex.Message);
            }

        }

        /// <summary>
        /// Connect to geekstream db and get list of feeds to be collected
        /// </summary>
        private void CollectContinously(string minutes)
        {
            if (!Regex.IsMatch(minutes, @"^\d+$"))
            {
                Console.WriteLine("not a number: {0}", minutes);
                return;
            }

            var pollIntervall = TimeSpan.FromMinutes(int.Parse(minutes));

            while (true)
            {
                DateTime collectedBefore = DateTime.Now.Add(-pollIntervall);
                FeedView[] feedsToCollect = _geekStreamDb.Execute(new GetFeedsToCollectQuery(collectedBefore,0,20));
                if (feedsToCollect.Length == 0)
                {
                    Console.WriteLine("\nNo feeds to collect, waiting 1 minute");
                    Thread.Sleep(TimeSpan.FromMinutes(1));
                }
                else
                {
                    List<int> collectedFeedIds = new List<int>();
                    var collector = new FeedCollector<FeedView>(feedsToCollect, feedView => feedView.Url);
                    collector.ItemCollected += ItemCollected;
                    collector.SourceCollected += (sender, args) => collectedFeedIds.Add(args.Source.Id);
                    collector.Collect();
                    _geekStreamDb.Execute(new SetFeedsLastCollectedCommand(collectedFeedIds.ToArray(), DateTime.Now));

                }
            }
        }


        private void ItemCollected(object sender, FeedCollector<FeedView>.SyndicationItemEventArgs e)
        {
            try
            {
                FeedView feedView = e.Source;

                string[] indexKeys;
                FeedItem item = CreateFeedItem(e.SyndicationItem, out indexKeys);

                //Include feed title in search terms
                var set = new HashSet<string>(indexKeys, StringComparer.InvariantCultureIgnoreCase);
                set.UnionWith(Regex.Split(feedView.Title, @"\W+"));
                indexKeys = set.ToArray();

                if (!feedView.ItemUrls.Contains(item.Url))
                {
                    Console.WriteLine(" + " + item.Title);
                    var command = new AddFeedItemCommand(item, indexKeys, feedView.Id, DateTime.Now);
                    _geekStreamDb.Execute(command);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nSkipping item, error:" + ex.Message);
            }
        }

        private void CreateFeed(SyndicationFeed syndicationFeed, string url)
        {

            var feed = new Feed
                           {
                               PartitionId = 0,
                               Title = syndicationFeed.Title != null ? syndicationFeed.Title.Text : "Untitled feed",
                               Description = syndicationFeed.Description != null ? syndicationFeed.Description.Text : "No description",
                               ImageUrl = syndicationFeed.ImageUrl != null ? syndicationFeed.ImageUrl.ToString() : null,
                               LastCollected = DateTime.MinValue,
                               Url = url,
                               Created = DateTime.Now
                           };
            var command = new AddFeedCommand(feed);
            _geekStreamDb.Execute(command);


        }

        private static FeedItem CreateFeedItem(SyndicationItem syndicationItem, out string[] searchWords)
        {
            FeedItem newFeedItem = new FeedItem();

            try
            {
                var link = syndicationItem.Links.FirstOrDefault(l => l.RelationshipType == "alternate")
                           ?? syndicationItem.Links.FirstOrDefault(l => l.RelationshipType == "self")
                           ?? syndicationItem.Links.First();
                newFeedItem.Url = link.Uri.ToString();
            }
            catch (Exception ex)
            {
                throw new Exception("Syndication item has no links", ex);
            }

            newFeedItem.Published = syndicationItem.PublishDate;

            var builder = new SyndicationItemParser(syndicationItem);
            newFeedItem.Summary = builder.Summary;
            searchWords = builder.Keywords;

            newFeedItem.Title = syndicationItem.Title.Text;
            return newFeedItem;

        }
    }
}
