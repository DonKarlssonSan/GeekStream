﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GeekStream.Core.Commands;
using GeekStream.Core.Domain;
using GeekStream.Core.Queries;
using GeekStream.Core.Views;
using LiveDomain.Core;

namespace GeekStream.Core
{
	public class GeekStreamClientConfiguration : PartitionClusterClientConfiguration
	{
		PartitionClient<GeekStreamModel> _client;
		//FailoverClusterClient<GeekStreamModel> _client; 
		public override IEngine<M> GetClient<M>()
		{
			CreateClientIfNull();

			return (IEngine<M>)_client;
		}

		void CreateClientIfNull()
		{
			if (_client != null) return;
			_client = new PartitionClient<GeekStreamModel>();

			// Partition nodes
			_client.Nodes.Add(Engine.For<GeekStreamModel>(@"partition1"));
			_client.Nodes.Add(Engine.For<GeekStreamModel>(@"partition2"));
			_client.Nodes.Add(Engine.For<GeekStreamModel>(@"partition3"));
			_client.Nodes.Add(Engine.For<GeekStreamModel>(@"partition4"));
			
			// Dispatchers
			_client.SetDispatcherFor<AddFeedCommand>(command => command.Feed.PartitionId);
			_client.SetDispatcherFor<AddFeedItemsCommand>(command => command.Feed.LongId & Int16.MaxValue);
			_client.SetDispatcherFor<GetFeedItemById>(q => (int)q.Id >> 48);

			// Mergers
			_client.SetMergerFor<FeedView[]>(f => f.SelectMany(a => a).ToArray());
			_client.SetMergerFor<FeedItemView[]>(f => f.SelectMany(a => a).ToArray());
			_client.SetMergerFor<GetFeedByUrl,FeedView>(f => f.FirstOrDefault(i => i != null));
		}
	}
}
